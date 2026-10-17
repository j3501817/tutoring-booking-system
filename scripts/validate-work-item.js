// scripts/validate-work-item.js
// Validates agentic work-item delivery artifacts and template guardrails.

const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');
const {
    getRequiredArtifacts,
    getCompletionOneOf,
    arraysEqualAsSets
} = require('./artifact-contract');

const root = path.resolve(__dirname, '..');

const args = process.argv.slice(2);
const changedOnly = args.includes('--changed');
const explicitWorkItem = readOption('--work-item') || process.env.WORK_ITEM_ID;
const explicitWorkItemsRoot = readOption('--work-items-root') || process.env.WORK_ITEMS_ROOT;
const workItemsRoot = explicitWorkItemsRoot
    ? path.resolve(root, explicitWorkItemsRoot)
    : path.join(root, '.agent-tmp', 'work-items');

const errors = [];
const warnings = [];

function readOption(name) {
    const index = args.indexOf(name);
    if (index === -1) return undefined;
    return args[index + 1];
}

function rel(filePath) {
    return path.relative(root, filePath).replace(/\\/g, '/');
}

function existsFile(filePath) {
    return fs.existsSync(filePath) && fs.statSync(filePath).isFile();
}

function nonEmptyFile(filePath) {
    return existsFile(filePath) && fs.statSync(filePath).size > 0;
}

function readText(filePath) {
    return fs.readFileSync(filePath, 'utf8').replace(/^\uFEFF/, '');
}

function parseJsonFile(filePath) {
    try {
        return JSON.parse(readText(filePath));
    } catch (error) {
        errors.push(`${rel(filePath)} is not valid JSON: ${error.message}`);
        return undefined;
    }
}

function getChangedFiles() {
    const candidates = [];
    const changedFiles = new Set();

    if (process.env.GITHUB_BASE_REF) {
        candidates.push(['diff', '--name-only', '--diff-filter=ACMR', `origin/${process.env.GITHUB_BASE_REF}...HEAD`]);
    }

    candidates.push(['diff', '--name-only', '--diff-filter=ACMR', 'HEAD~1..HEAD']);
    candidates.push(['diff', '--name-only', '--diff-filter=ACMR']);
    candidates.push(['ls-files', '--others', '--exclude-standard']);

    for (const command of candidates) {
        try {
            const output = execFileSync('git', command, {
                cwd: root,
                encoding: 'utf8',
                stdio: ['ignore', 'pipe', 'ignore']
            });
            const files = output.split(/\r?\n/).filter(Boolean);
            for (const file of files) changedFiles.add(file);
        } catch (error) {
            // Try the next strategy.
        }
    }

    return [...changedFiles];
}

function discoverWorkItems() {
    if (explicitWorkItem) return [explicitWorkItem];

    if (changedOnly) {
        const changedFiles = getChangedFiles();
        const workItemIds = new Set();

        for (const file of changedFiles) {
            const normalized = file.replace(/\\/g, '/');
            const match = normalized.match(/^\.agent-tmp\/work-items\/([^/]+)\//);
            if (match) workItemIds.add(match[1]);
        }

        if (workItemIds.size === 0) {
            if (process.env.GITHUB_ACTIONS) {
                errors.push('No changed work item found under .agent-tmp/work-items/. Every delivery PR must include its work-item artifacts.');
            } else {
                warnings.push('Could not resolve changed work items locally; falling back to all manifest-backed work items.');
                return discoverManifestWorkItems();
            }
        }

        return [...workItemIds];
    }

    return discoverManifestWorkItems();
}

function discoverManifestWorkItems() {
    if (!fs.existsSync(workItemsRoot)) return [];

    return fs.readdirSync(workItemsRoot, { withFileTypes: true })
        .filter((entry) => entry.isDirectory())
        .map((entry) => entry.name)
        .filter((id) => existsFile(path.join(workItemsRoot, id, 'work-item.json')));
}

function validateStatusFile(filePath, options = {}) {
    if (!existsFile(filePath)) return;

    const text = readText(filePath);
    if (/\[[ xX]\]/.test(text)) {
        errors.push(`${rel(filePath)} uses checkbox syntax; status.md must use emoji status markers.`);
    }

    const validStatusMarker = /^(?:\u2705|\uD83D\uDEA7|\u2B1C\uFE0F?|\u274C)\s+/u;
    const statusLines = text.split(/\r?\n/)
        .map((line, index) => ({ line, index }))
        .filter(({ line }) => line.trim().startsWith('- '));

    if (options.requireStatusLines && statusLines.length === 0) {
        errors.push(`${rel(filePath)} must include at least one emoji status line.`);
    }

    for (const { line, index } of statusLines) {
        const markerText = line.trim().slice(2);
        if (!validStatusMarker.test(markerText)) {
            errors.push(`${rel(filePath)} line ${index + 1} must start with an approved emoji status marker.`);
        }
    }
}

function validateWorkItem(id) {
    const dir = path.join(workItemsRoot, id);
    const manifestPath = path.join(dir, 'work-item.json');

    if (!fs.existsSync(dir)) {
        errors.push(`Work item directory does not exist: .agent-tmp/work-items/${id}`);
        return;
    }

    if (!existsFile(manifestPath)) {
        if (changedOnly) {
            warnings.push(`Manifest is missing for changed work item .agent-tmp/work-items/${id}/work-item.json; treating it as a legacy/intent work item and skipping manifest-only delivery checks.`);
            validateStatusFile(path.join(dir, 'status.md'), {
                requireStatusLines: false
            });
            return;
        }

        errors.push(`Missing manifest: .agent-tmp/work-items/${id}/work-item.json`);
        return;
    }

    const manifest = parseJsonFile(manifestPath);
    if (!manifest) return;

    if (manifest.id !== id) {
        errors.push(`${rel(manifestPath)} id must match directory name "${id}".`);
    }

    if (manifest.pmApproved !== true) {
        errors.push(`${rel(manifestPath)} must set pmApproved to true before delivery.`);
    }

    const contractRequiredArtifacts = getRequiredArtifacts(manifest.source);
    const requiredArtifacts = Array.isArray(manifest.requiredArtifacts)
        ? manifest.requiredArtifacts
        : contractRequiredArtifacts;

    if (manifest.source === 'github-issue' && !arraysEqualAsSets(requiredArtifacts, contractRequiredArtifacts)) {
        errors.push(`${rel(manifestPath)} requiredArtifacts must match shared artifact contract: ${contractRequiredArtifacts.join(', ')}`);
    } else if (manifest.source !== 'github-issue' && !arraysEqualAsSets(requiredArtifacts, contractRequiredArtifacts)) {
        warnings.push(`${rel(manifestPath)} uses legacy requiredArtifacts; shared contract enforcement is strict for github-issue work items.`);
    }

    for (const artifact of requiredArtifacts) {
        const artifactPath = path.join(dir, artifact);
        if (!nonEmptyFile(artifactPath)) {
            errors.push(`Required artifact is missing or empty: .agent-tmp/work-items/${id}/${artifact}`);
        }
    }

    if (manifest.source === 'github-issue') {
        const completionArtifacts = getCompletionOneOf(manifest.source);
        const hasCompletionArtifact = completionArtifacts.some((artifact) => nonEmptyFile(path.join(dir, artifact)));

        if (!hasCompletionArtifact) {
            errors.push(`GitHub issue work item must include one completion artifact (${completionArtifacts.join(' or ')}): .agent-tmp/work-items/${id}`);
        }

        for (const artifact of ['issue.json', 'issue.md', 'CONTEXT.md']) {
            const artifactPath = path.join(dir, artifact);
            if (!nonEmptyFile(artifactPath)) {
                errors.push(`GitHub issue context is missing or empty: .agent-tmp/work-items/${id}/${artifact}`);
            }
        }
    }

    validateStatusFile(path.join(dir, 'status.md'), {
        requireStatusLines: manifest.source === 'github-issue'
    });
}

function validateSchemas() {
    const schemasDir = path.join(root, 'docs', 'schemas');
    if (!fs.existsSync(schemasDir)) {
        warnings.push('docs/schemas/ does not exist; skipping schema parse checks.');
        return;
    }

    const schemaFiles = fs.readdirSync(schemasDir)
        .filter((name) => /\.(json|ya?ml)$/i.test(name));

    if (schemaFiles.length === 0) {
        warnings.push('docs/schemas/ has no JSON/YAML schema files.');
        return;
    }

    for (const name of schemaFiles) {
        const filePath = path.join(schemasDir, name);
        if (!nonEmptyFile(filePath)) {
            errors.push(`Schema file is empty: ${rel(filePath)}`);
            continue;
        }

        if (/\.json$/i.test(name)) {
            parseJsonFile(filePath);
            continue;
        }

        const text = readText(filePath);
        if (!/^\s*(openapi|asyncapi|\$schema)\s*:/m.test(text)) {
            errors.push(`${rel(filePath)} must declare openapi:, asyncapi:, or $schema:.`);
        }
    }
}

function validateReferences() {
    const referencesDir = path.join(root, '.agent', 'references');
    const requiredReferences = [
        'security-checklist.md',
        'testing-patterns.md',
        'performance-checklist.md',
        'accessibility-checklist.md'
    ];

    for (const name of requiredReferences) {
        const filePath = path.join(referencesDir, name);
        if (!nonEmptyFile(filePath)) {
            errors.push(`Reference checklist is missing or empty: ${rel(filePath)}`);
        }
    }
}

function validateAgenticConfig() {
    const configPath = path.join(root, 'agentic.config.json');
    if (!existsFile(configPath)) {
        warnings.push('agentic.config.json does not exist; optional application checks are not configured.');
        return;
    }

    const config = parseJsonFile(configPath);
    if (!config) return;

    if (config.version !== 1) {
        errors.push('agentic.config.json must set version to 1.');
    }

    if (!config.governance || config.governance.runtime !== 'node') {
        errors.push('agentic.config.json must declare governance.runtime as "node" for the current governance scripts.');
    }

    const projectProfile = config.projectProfile || {};
    if (typeof projectProfile.path !== 'string' || projectProfile.path.trim() === '') {
        errors.push('agentic.config.json projectProfile.path must be a non-empty string.');
    }

    if (!Array.isArray(projectProfile.safeValidationCommands) || projectProfile.safeValidationCommands.length === 0) {
        errors.push('agentic.config.json projectProfile.safeValidationCommands must be a non-empty array.');
    }

    if (projectProfile.requiredEnvPlaceholders && typeof projectProfile.requiredEnvPlaceholders !== 'object') {
        errors.push('agentic.config.json projectProfile.requiredEnvPlaceholders must be an object when provided.');
    }

    if (!Array.isArray(projectProfile.optInTestMarkers)) {
        errors.push('agentic.config.json projectProfile.optInTestMarkers must be an array.');
    }

    const applicationChecks = config.applicationChecks;
    if (!applicationChecks || typeof applicationChecks !== 'object') {
        errors.push('agentic.config.json must declare applicationChecks.');
        return;
    }

    if (applicationChecks.enabled !== true && applicationChecks.enabled !== false) {
        errors.push('agentic.config.json applicationChecks.enabled must be true or false.');
    }

    const profiles = applicationChecks.profiles || {};
    for (const [name, profile] of Object.entries(profiles)) {
        if (!Array.isArray(profile.detect) || profile.detect.length === 0) {
            errors.push(`agentic.config.json applicationChecks.profiles.${name}.detect must be a non-empty array.`);
        }

        if (!Array.isArray(profile.commands) || profile.commands.length === 0) {
            errors.push(`agentic.config.json applicationChecks.profiles.${name}.commands must be a non-empty array.`);
        }
    }
}

function main() {
    const workItemIds = discoverWorkItems();

    for (const id of workItemIds) {
        validateWorkItem(id);
    }

    validateSchemas();
    validateReferences();
    validateAgenticConfig();

    for (const warning of warnings) {
        console.warn(`WARN: ${warning}`);
    }

    if (errors.length > 0) {
        console.error('Agentic validation failed:');
        for (const error of errors) console.error(`- ${error}`);
        process.exit(1);
    }

    const scope = workItemIds.length > 0 ? workItemIds.join(', ') : 'no manifest-backed work items';
    console.log(`Agentic validation passed: ${scope}`);
}

main();

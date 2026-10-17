// scripts/run-app-checks.js
// Runs optional application-language checks configured in agentic.config.json.

const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

const root = path.resolve(__dirname, '..');
const configPath = path.join(root, 'agentic.config.json');
const ignoredDirs = new Set(['.git', 'node_modules', 'dist', 'build', '.agent-tmp']);

function readConfig() {
    if (!fs.existsSync(configPath)) {
        return { applicationChecks: { enabled: false } };
    }

    return JSON.parse(fs.readFileSync(configPath, 'utf8'));
}

function readProjectProfile(config) {
    const profileRelPath = config.projectProfile?.path || '.agent-tmp/project-profile.json';
    const profilePath = path.join(root, profileRelPath);

    if (!fs.existsSync(profilePath)) {
        return {};
    }

    return JSON.parse(fs.readFileSync(profilePath, 'utf8'));
}

function bootstrapPlaceholderEnvironment(profile) {
    const placeholders = profile.requiredEnvPlaceholders || {};
    const names = Object.keys(placeholders);

    if (names.length === 0) {
        return;
    }

    const applied = [];
    for (const name of names) {
        if (process.env[name]) continue;

        const configured = placeholders[name];
        const value = typeof configured === 'string' && configured.trim()
            ? configured
            : `LOCAL_PLACEHOLDER_${name}`;

        process.env[name] = value;
        applied.push(name);
    }

    if (applied.length > 0) {
        console.log(`Using local validation placeholders for: ${applied.join(', ')}`);
        console.log('Placeholder values are local-only and must not contain production secrets.');
    }
}

function hasExactFile(relativePath) {
    return fs.existsSync(path.join(root, relativePath));
}

function hasWildcardMatch(pattern) {
    const suffix = pattern.slice(1);

    function walk(dir) {
        for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
            if (ignoredDirs.has(entry.name)) continue;

            const entryPath = path.join(dir, entry.name);
            if (entry.isDirectory()) {
                if (walk(entryPath)) return true;
            } else if (entry.isFile() && entry.name.endsWith(suffix)) {
                return true;
            }
        }

        return false;
    }

    return walk(root);
}

function matchesDetectRule(rule) {
    if (rule.startsWith('*')) return hasWildcardMatch(rule);
    return hasExactFile(rule);
}

function detectProfiles(profiles) {
    return Object.entries(profiles)
        .filter(([, profile]) => Array.isArray(profile.detect) && profile.detect.some(matchesDetectRule))
        .map(([name]) => name);
}

function resolveProfileNames(applicationChecks) {
    const profiles = applicationChecks.profiles || {};
    const mode = applicationChecks.mode || 'auto';

    if (Array.isArray(applicationChecks.selectedProfiles)) {
        return applicationChecks.selectedProfiles;
    }

    if (mode === 'auto') {
        return detectProfiles(profiles);
    }

    if (typeof mode === 'string' && profiles[mode]) {
        return [mode];
    }

    return [];
}

function runCommand(command) {
    console.log(`Running application check: ${command}`);
    execSync(command, {
        cwd: root,
        stdio: 'inherit',
        shell: true
    });
}

function main() {
    const config = readConfig();
    const profile = readProjectProfile(config);
    bootstrapPlaceholderEnvironment(profile);

    const applicationChecks = config.applicationChecks || {};

    if (applicationChecks.enabled !== true) {
        console.log('Application checks are disabled in agentic.config.json; skipping language-specific checks.');
        return;
    }

    const profiles = applicationChecks.profiles || {};
    const profileNames = resolveProfileNames(applicationChecks);

    if (profileNames.length === 0) {
        console.log('Application checks are enabled, but no matching language profile was detected.');
        return;
    }

    for (const name of profileNames) {
        const profile = profiles[name];
        if (!profile) {
            throw new Error(`Application check profile is not defined: ${name}`);
        }

        if (!Array.isArray(profile.commands) || profile.commands.length === 0) {
            throw new Error(`Application check profile has no commands: ${name}`);
        }

        for (const command of profile.commands) {
            runCommand(command);
        }
    }
}

main();

// scripts/generate-project-profile.js
// Generates a deterministic project profile for future issue automation.

const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const configPath = path.join(root, 'agentic.config.json');
const ignoredDirs = new Set(['.git', 'node_modules', 'dist', 'build', '.agent-tmp']);

function readJson(filePath, fallback) {
    if (!fs.existsSync(filePath)) return fallback;
    return JSON.parse(fs.readFileSync(filePath, 'utf8'));
}

function ensureDir(dir) {
    fs.mkdirSync(dir, { recursive: true });
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

function detectPackageManager() {
    if (hasExactFile('pnpm-lock.yaml')) return 'pnpm';
    if (hasExactFile('yarn.lock')) return 'yarn';
    if (hasExactFile('package-lock.json')) return 'npm';
    if (hasExactFile('package.json')) return 'npm';
    return 'unknown';
}

function buildProfile(config) {
    const applicationChecks = config.applicationChecks || {};
    const profiles = applicationChecks.profiles || {};
    const detectedProfiles = detectProfiles(profiles);
    const projectProfileConfig = config.projectProfile || {};

    return {
        version: 1,
        createdAt: new Date().toISOString(),
        primaryLanguage: projectProfileConfig.primaryLanguage || detectedProfiles[0] || 'unknown',
        applicationType: projectProfileConfig.applicationType || 'unknown',
        governanceRuntime: config.governance?.runtime || 'node',
        packageManager: projectProfileConfig.packageManager || config.governance?.packageManager || detectPackageManager(),
        safeValidationCommands: projectProfileConfig.safeValidationCommands || ['npm run agentic:test'],
        focusedValidationCommands: projectProfileConfig.focusedValidationCommands || [],
        requiredEnvPlaceholders: projectProfileConfig.requiredEnvPlaceholders || {},
        optInTestMarkers: projectProfileConfig.optInTestMarkers || ['connectivity', 'e2e'],
        refreshTriggers: projectProfileConfig.refreshTriggers || [
            'agentic.config.json changed',
            'package.json changed',
            'package-lock.json changed',
            '.github/workflows changed',
            'manual refresh requested'
        ],
        detectedProfiles
    };
}

function main() {
    const args = process.argv.slice(2);
    const config = readJson(configPath, {});
    const profileRelPath = config.projectProfile?.path || '.agent-tmp/project-profile.json';
    const profilePath = path.join(root, profileRelPath);
    const profile = buildProfile(config);

    if (args.includes('--print')) {
        console.log(JSON.stringify(profile, null, 2));
        return;
    }

    ensureDir(path.dirname(profilePath));
    fs.writeFileSync(profilePath, `${JSON.stringify(profile, null, 2)}\n`, 'utf8');
    console.log(`Project profile written: ${profileRelPath.replace(/\\/g, '/')}`);
}

main();

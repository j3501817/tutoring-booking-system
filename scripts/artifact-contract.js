// scripts/artifact-contract.js
// Shared work-item artifact contract used by validators and generators.

const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const contractPath = path.join(root, 'docs', 'schemas', 'work-item-artifacts.schema.json');

function readContractSchema() {
    const schema = JSON.parse(fs.readFileSync(contractPath, 'utf8'));
    if (!schema.agenticArtifactContract) {
        throw new Error('work-item artifact schema is missing agenticArtifactContract.');
    }

    return schema.agenticArtifactContract;
}

function unique(items) {
    return [...new Set(items)];
}

function getRequiredArtifacts(source) {
    const contract = readContractSchema();
    const common = contract.requiredArtifacts?.common || [];
    const sourceSpecific = source === 'github-issue'
        ? contract.requiredArtifacts?.githubIssue || []
        : [];

    return unique([...common, ...sourceSpecific]);
}

function getCompletionOneOf(source) {
    const contract = readContractSchema();
    if (source === 'github-issue') {
        return contract.completionArtifacts?.githubIssueOneOf || [];
    }

    return [];
}

function getSkeletonArtifacts(source, queueState = 'ready-for-codex') {
    const contract = readContractSchema();
    if (source !== 'github-issue') return getRequiredArtifacts(source);

    if (queueState === 'manual-review') {
        return contract.skeletonArtifacts?.githubIssueManualReview || [];
    }

    return contract.skeletonArtifacts?.githubIssueReady || [];
}

function arraysEqualAsSets(a, b) {
    const left = [...a].sort();
    const right = [...b].sort();
    return left.length === right.length && left.every((value, index) => value === right[index]);
}

module.exports = {
    contractPath,
    readContractSchema,
    getRequiredArtifacts,
    getCompletionOneOf,
    getSkeletonArtifacts,
    arraysEqualAsSets
};

if (require.main === module) {
    const args = process.argv.slice(2);
    const source = args.includes('--source')
        ? args[args.indexOf('--source') + 1]
        : 'github-issue';

    if (args.includes('--required')) {
        console.log(JSON.stringify(getRequiredArtifacts(source), null, 2));
        return;
    }

    if (args.includes('--completion-one-of')) {
        console.log(JSON.stringify(getCompletionOneOf(source), null, 2));
        return;
    }

    if (args.includes('--skeleton')) {
        const state = args.includes('--state')
            ? args[args.indexOf('--state') + 1]
            : 'ready-for-codex';
        console.log(JSON.stringify(getSkeletonArtifacts(source, state), null, 2));
        return;
    }

    console.log(JSON.stringify(readContractSchema(), null, 2));
}

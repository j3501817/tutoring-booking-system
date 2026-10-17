// scripts/self-check-artifact-contract.js
// Creates a deterministic dry-run work item and validates the shared artifact contract.

const fs = require('fs');
const os = require('os');
const path = require('path');
const { execFileSync } = require('child_process');
const {
    getRequiredArtifacts,
    getSkeletonArtifacts,
    arraysEqualAsSets
} = require('./artifact-contract');

const root = path.resolve(__dirname, '..');
const workItemId = 'dry-run-contract-self-check';
const tempRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'agentic-artifact-contract-'));
const tempWorkItemsRoot = path.join(tempRoot, 'work-items');
const workItemDir = path.join(tempWorkItemsRoot, workItemId);
const keepArtifact = process.argv.includes('--keep');

function assertSafeSelfCheckDir() {
    const relative = path.relative(tempWorkItemsRoot, workItemDir);
    if (relative.startsWith('..') || path.isAbsolute(relative) || relative !== workItemId) {
        throw new Error(`Refusing to clean unexpected self-check path: ${workItemDir}`);
    }
}

function writeFile(name, content) {
    fs.mkdirSync(workItemDir, { recursive: true });
    fs.writeFileSync(path.join(workItemDir, name), content, 'utf8');
}

function main() {
    assertSafeSelfCheckDir();

    const requiredArtifacts = getRequiredArtifacts('github-issue');
    const skeletonArtifacts = getSkeletonArtifacts('github-issue', 'ready-for-codex');

    if (!arraysEqualAsSets(
        skeletonArtifacts.filter((name) => name !== 'DELIVERY_REPORT.md'),
        requiredArtifacts
    )) {
        throw new Error('Artifact skeleton and required artifact contract are out of sync.');
    }

    const manifest = {
        id: workItemId,
        title: 'Dry-run artifact contract self-check',
        source: 'github-issue',
        issueNumber: 0,
        issueUrl: 'dry-run',
        pmApproved: true,
        queueState: 'ready-for-codex',
        retryCount: 0,
        maxRetries: 0,
        requiredArtifacts
    };

    writeFile('work-item.json', `${JSON.stringify(manifest, null, 2)}\n`);
    writeFile('issue.json', `${JSON.stringify({ number: 0, title: manifest.title, body: 'Dry-run self-check issue.', url: 'dry-run', labels: [] }, null, 2)}\n`);
    writeFile('issue.md', '# Issue #0\n\nDry-run self-check issue.\n');
    writeFile('CONTEXT.md', '# Context Pack\n\nDry-run artifact contract self-check.\n');
    writeFile('assumptions.md', '# Assumptions\n\n- Dry-run self-check uses local placeholder issue metadata.\n');
    writeFile('PLAN.md', '# Plan\n\nDry-run artifact contract self-check.\n');
    writeFile('status.md', '# Status\n\n- ✅ INTAKE: Dry-run issue context generated\n- ✅ VERIFY: Artifact contract self-check ready\n');
    writeFile('TEST_REPORT.md', '# Test Report\n\nDry-run artifact contract self-check pending validator execution.\n');
    writeFile('DELIVERY_REPORT.md', '# Delivery Report\n\nDry-run artifact contract self-check artifact.\n');

    execFileSync(process.execPath, ['scripts/validate-work-item.js', '--work-item', workItemId, '--work-items-root', tempWorkItemsRoot], {
        cwd: root,
        stdio: 'inherit'
    });

    if (!keepArtifact) {
        fs.rmSync(tempRoot, { recursive: true, force: true });
    }

    const suffix = keepArtifact ? ` and kept: ${workItemDir}` : '';
    console.log(`Artifact contract self-check passed${suffix}`);
}

main();

// scripts/check-secrets.js
// Conservative secret scan for code and agentic work-item artifacts.

const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const findings = [];

const excludedDirs = new Set([
    '.git',
    'node_modules',
    'dist',
    'build'
]);

const includedExtensions = new Set([
    '.js',
    '.json',
    '.md',
    '.yaml',
    '.yml',
    '.env',
    '.example'
]);

const placeholderWords = [
    'YOUR',
    'EXAMPLE',
    'PLACEHOLDER',
    'REPLACE',
    'DUMMY',
    'SAMPLE',
    'NONE',
    '您的',
    '範例',
    '請填入'
];

const secretPatterns = [
    {
        name: 'private key',
        pattern: /-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----/i
    },
    {
        name: 'bearer token',
        pattern: /\bBearer\s+[A-Za-z0-9._~+/=-]{20,}\b/i
    },
    {
        name: 'assigned secret-like value',
        pattern: /\b(?:api[_-]?key|access[_-]?token|secret|password|passwd|pwd|database_url|client[_-]?secret)\b\s*[:=]\s*["']?([^"'\s]{16,})/i
    }
];

function rel(filePath) {
    return path.relative(root, filePath).replace(/\\/g, '/');
}

function shouldScan(filePath) {
    const relative = rel(filePath);
    if (relative === 'package-lock.json') return false;

    const lower = path.basename(filePath).toLowerCase();
    if (lower === '.env.example') return true;

    const extension = path.extname(filePath).toLowerCase();
    return includedExtensions.has(extension);
}

function isPlaceholder(line) {
    const upper = line.toUpperCase();
    return placeholderWords.some((word) => upper.includes(word.toUpperCase()))
        || /x{6,}/i.test(line)
        || /<[^>]+>/.test(line);
}

function walk(dir, files = []) {
    if (!fs.existsSync(dir)) return files;

    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        if (excludedDirs.has(entry.name)) continue;

        const entryPath = path.join(dir, entry.name);
        if (entry.isDirectory() && rel(entryPath) === '.agent/skills') continue;

        if (entry.isDirectory()) {
            walk(entryPath, files);
        } else if (entry.isFile() && shouldScan(entryPath)) {
            files.push(entryPath);
        }
    }

    return files;
}

function scanFile(filePath) {
    const lines = fs.readFileSync(filePath, 'utf8').split(/\r?\n/);

    lines.forEach((line, index) => {
        if (isPlaceholder(line)) return;

        for (const { name, pattern } of secretPatterns) {
            if (pattern.test(line)) {
                findings.push(`${rel(filePath)}:${index + 1} possible ${name}`);
            }
        }
    });
}

function main() {
    for (const filePath of walk(root)) {
        scanFile(filePath);
    }

    if (findings.length > 0) {
        console.error('Secret scan failed:');
        for (const finding of findings) console.error(`- ${finding}`);
        process.exit(1);
    }

    console.log('Secret scan passed.');
}

main();

# Test Report

## Detected stack

- GitHub Actions workflow
- Node.js governance tooling

## Checks

- ✅ Workflow structure check confirmed only `agentic-governance` and `project-validation` are declared as CI jobs.
- ✅ `npm run agentic:test`
- ✅ `git diff --check`

## Skipped checks

- `actionlint` was not available in the local environment.
- Local `bash --version` failed with access denied, so the embedded bash script cannot be executed locally in this shell. GitHub Actions will execute it on `ubuntu-latest`.

## Result

- Local governance validation passed.
- Diff format check passed; Git emitted Windows LF-to-CRLF warnings only.
- The workflow no longer declares fixed language jobs such as Python, Go, Rust, PHP, Ruby, or Docker as top-level PR checks.

## PM review

- Not required for this scoped CI presentation change.

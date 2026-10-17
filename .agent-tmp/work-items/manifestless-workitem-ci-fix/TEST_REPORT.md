# Test Report

## Commands

- `npm run agentic:lint`: passed.
- `npm run agentic:test`: passed after rebasing onto latest `origin/main`.

## Regression Coverage

- `npm run agentic:test` now runs `npm run agentic:test:manifestless`.
- `agentic:test:manifestless` validates `.agent-tmp/work-items/manifestless-changed-validation-test/`, which intentionally has no `work-item.json`.
- Expected behavior: `--changed` validation passes and emits a warning instead of failing with `Missing manifest`.
- `agentic:test` still runs `npm run agentic:contract:self-check` before validation, preserving the shared artifact contract checks from `main`.

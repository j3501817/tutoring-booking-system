# Delivery Report

## Summary

- Changed `scripts/validate-work-item.js` so `--changed` treats manifestless changed work items as legacy/intent work items and warns instead of failing.
- Added `--work-items-root` support for targeted validator tests and future deterministic fixtures.
- Added a committed manifestless fixture at `.agent-tmp/work-items/manifestless-changed-validation-test/status.md`.
- Added `npm run agentic:test:manifestless` and included it in `npm run agentic:test`.
- Preserved the `agentic:contract:self-check` test step from latest `main` while adding the manifestless regression.
- Documented changed validation behavior in `docs/auto-fix-flow.md`.

## Behavior Kept Strict

Full validation without `--changed` still fails when a specific work item is missing `work-item.json`.

## Merge Status

- Rebased onto latest `origin/main`.
- Resolved conflicts in `package.json` and `scripts/validate-work-item.js`.

# Test Report

## Detected Stack

- Governance runtime: Node.js.
- Package manager: npm.
- Application checks: disabled in `agentic.config.json`.

## Commands

- `npm run agentic:lint`: passed.
- `npm run agentic:test`: passed.
- `npm run agentic:validate`: passed.
- PowerShell parser check for `scripts/codex-batch-issues.ps1` and `scripts/init-codex-template.ps1`: passed.
- Re-ran `npm run agentic:lint` and `npm run agentic:test` after adding the template history adoption guard: passed.
- Re-ran `npm run agentic:lint` and `npm run agentic:test` after resolving PR #15 merge conflicts with `origin/main`: passed.

## Notes

- `npm run agentic:test` runs `npm run agentic:contract:self-check` before governance validation.
- `agentic:validate` reports warnings for legacy historical work items whose `requiredArtifacts` predate the shared contract. Strict shared-contract enforcement applies to `source: "github-issue"` work items.
- `run-app-checks.js` skipped language-specific checks because application checks are disabled in `agentic.config.json`.

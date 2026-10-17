# Test Report

## Detected stack

- Governance tooling: Node.js scripts with PowerShell workflow wrappers.
- CI: GitHub Actions.

## Validation commands

- `Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz"`: passed.
- PowerShell parser check for `scripts/codex-batch-issues.ps1`, `scripts/init-codex-template.ps1`, `scripts/setup-codex-labels.ps1`: passed.
- `node -e "JSON.parse(...agentic.config.json...)"`: passed.
- `npm run agentic:lint`: passed.
- `npm run agentic:test`: passed.
- `.\scripts\codex-batch-issues.ps1 -DryRun -IssueJsonPath ".agent-tmp\work-items\auto-fix-hardening-20260520-1145\mock-issues.json" -MaxParallel 1`: passed.
- `npm run agentic:validate` after dry-run output isolation: passed.

## Notes

- `agentic:app-checks` is intentionally skipped because `agentic.config.json` has `applicationChecks.enabled=false`.
- GitHub CLI execution was not run from this Codex shell because this change only updates scripts and CI; the user has confirmed `gh version 2.92.0` exists in their PowerShell environment.
- Dry-run initially exposed UTF-8 BOM JSON parsing in generated `work-item.json`; this was fixed by writing UTF-8 without BOM and making the validator strip a leading BOM defensively.
- Dry-run now writes to `dry-run-issue-<number>` work-item IDs to avoid overwriting real issue artifacts.
- Full status-line enforcement is required for GitHub Issue work items; historical non-Issue work items are still checked for forbidden checkbox syntax and invalid status markers if status lines are present.

## PM Review

- Not required.

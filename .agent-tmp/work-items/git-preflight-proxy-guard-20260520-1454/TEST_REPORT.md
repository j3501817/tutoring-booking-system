# Test Report

## Detected stack

- PowerShell automation scripts
- Node.js governance validation scripts

## Commands

- ✅ `PowerShell Parser.ParseFile` for `scripts/git-github-preflight.ps1`, `scripts/codex-batch-issues.ps1`, `scripts/init-codex-template.ps1`, and `scripts/setup-codex-labels.ps1`
- ✅ `npm run agentic:test`
- ✅ Manual proxy cleanup check by injecting `HTTP_PROXY`, `HTTPS_PROXY`, and `ALL_PROXY` with `http://127.0.0.1:9`
- ✅ `.\scripts\codex-batch-issues.ps1 -DryRun -IssueJsonPath .\.agent-tmp\work-items\auto-fix-hardening-20260520-1145\mock-issues.json -MaxParallel 1 -DryRunOutputRoot .agent-tmp/work-items` with injected bad proxy
- ✅ `Test-AgenticGitHubPreflight -GhCommand gh -RemoteName origin` with injected bad proxy
- ✅ Final `npm run agentic:test` after README/reference documentation updates
- ✅ Final `git diff --check` after README/reference documentation updates

## Results

- PowerShell parser check passed.
- Governance validation passed.
- Secret scan passed.
- App checks were intentionally skipped because `agentic.config.json` disables them.
- Dry run completed after the helper cleared injected proxy variables.
- Live preflight completed `gh auth status` and `git ls-remote origin HEAD` after clearing injected proxy variables.
- Final governance validation passed after documentation updates.
- `git diff --check` passed; Git emitted Windows LF-to-CRLF warnings only.

## PM review

- Not required for this change.

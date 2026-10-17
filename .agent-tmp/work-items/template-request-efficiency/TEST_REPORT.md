# Test Report

## Detected Stack

- Governance runtime: Node.js.
- Template automation: PowerShell batch wrapper plus Node.js governance scripts.
- Application checks: disabled in `agentic.config.json` for this template repository.

## Commands Run

- ✅ `npm run agentic:lint`
  - Result: passed.
  - Reason: validates Node.js syntax for governance scripts, including the new profile generator.

- ✅ `npm run agentic:profile:refresh`
  - Result: passed.
  - Reason: verifies `.agent-tmp/project-profile.json` can be generated from `agentic.config.json`.

- ✅ `.\scripts\codex-batch-issues.ps1 -DryRun -IssueJsonPath .\.agent-tmp\work-items\template-request-efficiency\dry-run-issues.json -MaxParallel 2`
  - Result: passed after fixing two wrapper defects found by the first dry-run.
  - Coverage: complete issue reached the ready dry-run path; incomplete issue routed to manual review before Codex execution.

- ✅ `npm run agentic:test`
  - Result: passed.
  - Details: `agentic:validate`, `agentic:check-secrets`, and `agentic:app-checks` all completed successfully.

- ✅ Post-documentation validation: `npm run agentic:test`
  - Result: passed.
  - Reason: README and usage documentation were updated after the first commit.

## Failures Found And Fixed

- Dry-run initially exposed JSON array handling that collapsed two issues into one job. Fixed by normalizing `ConvertFrom-Json` output.
- Dry-run initially exposed unsafe mojibake regex handling in PowerShell. Fixed by using deterministic character/string checks that keep the script parseable.
- Dry-run initially exposed typed argument shifting for boolean values in `Start-Job`. Fixed with parenthesized boolean arguments and typed scriptblock parameters.

## Skipped

- No live GitHub issue execution, Codex CLI execution, push, or PR creation was performed because this is a template change request rather than a specific GitHub Issue automation run.

## PM Review

- Not required.

# Agentic Governance Flow Improvements

Taiwan time checked: 2026-05-28 14:58:29 +08:00

## Fact Check

- The PM supplied two improvement documents from the downstream project.
- Both documents identify repeated project discovery, missing skeleton artifacts, weak issue gates, missing context packs, and PRs created before local CI-parity validation.
- The current template already had early support for project profiles, context packs, issue routing gates, and placeholder environment bootstrap.
- Before this change, artifact rules were still duplicated between `scripts/validate-work-item.js` and `scripts/codex-batch-issues.ps1`.
- Before this change, template initialization generated a project profile but did not run a dry-run artifact contract self-check.

## Implemented Changes

### Shared Artifact Contract

The canonical work-item artifact contract now lives at:

```text
docs/schemas/work-item-artifacts.schema.json
```

The contract defines common required artifacts, GitHub Issue context artifacts, completion artifact alternatives, and skeleton artifact sets for ready and manual-review work items.

Both the validator and generator read from this contract:

- `scripts/validate-work-item.js` validates `work-item.json.requiredArtifacts` against the shared contract.
- `scripts/codex-batch-issues.ps1` writes `requiredArtifacts` and `completionArtifactsOneOf` from the shared contract.
- `scripts/artifact-contract.js` provides deterministic contract access for scripts and tests.

### Initialization Self-Check

Template initialization now runs:

```powershell
node scripts/self-check-artifact-contract.js
```

The self-check creates a temporary `dry-run-contract-self-check` work item outside the repository, writes a complete dry-run skeleton, and validates it with `scripts/validate-work-item.js --work-item dry-run-contract-self-check --work-items-root <temp-work-items-root>`.

If generator and validator rules diverge, template initialization fails before issue automation starts.

### Work Item Initialization Gate

Each work item validates its skeleton artifact contract before Codex execution. The wrapper stops before Codex when the skeleton is invalid.

Manual-review routed work items are also validated after `PM_Review.md` is written, so review artifacts remain contract-compliant even when no implementation branch is created.

### Prompt Efficiency

The Codex prompt now explicitly says:

- Read `AGENTS.md`, `.agent-tmp/project-profile.json`, `issue.md`, and `CONTEXT.md`.
- Do not rediscover language, framework, or validation commands unless the profile is missing, stale, or explicitly in issue scope.
- Record profile/context gaps in `assumptions.md` before extra discovery.
- Use only safe validation by default.

### Pre-PR CI-Parity Validation

Before push and PR creation, the wrapper now runs:

```powershell
node scripts/validate-work-item.js --work-item <work-item-id>
node scripts/check-secrets.js
node scripts/run-app-checks.js
```

Results are appended to `TEST_REPORT.md`. If any command fails, the wrapper writes `PM_Review.md` and does not create a PR by default.

### Post-PR Check Recording

After PR creation or detection of an existing PR, the wrapper fetches PR check status with GitHub CLI and appends the result to `DELIVERY_REPORT.md`. If checks cannot be fetched, the reason is recorded instead of silently omitting the status.

## Remaining Backlog

- Add richer automated tests for PowerShell routing behavior without invoking Codex.
- Add explicit operator override recording in `work-item.json`.
- Add CI-only failure classification with check links when GitHub returns detailed URLs.
- Add contention detection beyond labels and simple route keywords.

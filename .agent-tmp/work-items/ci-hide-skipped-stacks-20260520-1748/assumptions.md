# Assumptions

- Team members primarily interpret the PR checks list, not internal step-level details.
- It is acceptable for the workflow job internals to conditionally set up only detected language runtimes.
- Unused languages should remain visible as informational Step Summary entries, not as skipped PR checks.
- Branch protection should require stable check names such as `Agentic governance` and `Project validation`.

# Delivery Report

## Summary

- Replaced fixed per-language CI jobs with one stable `Project validation` job.
- Kept `Agentic governance` as a stable required governance check.
- Moved detected/not-detected stack information into GitHub Step Summary.
- Updated README and auto-fix flow documentation to explain the PR checks behavior.

## Known risks

- Step-level setup actions may still be marked skipped inside the `Project validation` job details when a stack is not detected, but they no longer appear as separate PR check items.
- Teams should update branch protection to require `Agentic governance` and `Project validation` rather than language-specific job names.

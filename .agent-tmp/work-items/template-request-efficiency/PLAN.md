# Plan

## Objective

Reduce avoidable Codex request usage by moving deterministic setup, routing, context generation, validation bootstrap, governance artifact creation, and retry budget tracking into template scripts.

## Implementation Tasks

- Add a project profile generator and refresh path for `.agent-tmp/project-profile.json`.
- Update the batch issue wrapper to load the profile, preflight issue completeness, create `CONTEXT.md`, create governance skeletons, preserve `MaxParallel`, and track queue/retry state.
- Update validation execution to use safe placeholder environment variables without writing secrets to artifacts.
- Replace the Codex prompt's repeated project-discovery instructions with profile/context-pack instructions.
- Add documentation that records prompt changes, routing gates, execution budget policy, and split work items with acceptance criteria.

## Verification

- Run Node syntax checks through `npm run agentic:lint`.
- Run governance validation through `npm run agentic:validate -- --work-item template-request-efficiency`.
- Run a dry-run batch against representative complete and incomplete issues.

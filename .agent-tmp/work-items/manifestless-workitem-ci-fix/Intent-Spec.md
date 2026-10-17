# Intent Spec

## Problem

New projects can create legacy or intent work-item directories under `.agent-tmp/work-items/<id>/` without a `work-item.json`. CI runs `npm run agentic:validate:changed`, which previously failed immediately with `Missing manifest`.

## Desired Behavior

Changed validation must not fail only because a changed legacy/intent work item is missing `work-item.json`. Full explicit validation should still fail so template-generated delivery work items keep their manifest discipline.

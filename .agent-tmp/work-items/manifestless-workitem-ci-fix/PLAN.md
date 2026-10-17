# Plan

1. Change `validate-work-item.js --changed` so manifestless changed work items are treated as legacy/intent work items and emit a warning instead of an error.
2. Keep full validation strict for explicit or all-manifest validation.
3. Add a committed manifestless fixture and include it in `npm run agentic:test` so changed validation covers the regression.
4. Run governance lint and test commands.

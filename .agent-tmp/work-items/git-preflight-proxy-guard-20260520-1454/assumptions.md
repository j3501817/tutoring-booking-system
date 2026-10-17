# Assumptions

- The Codex shell may continue to inherit unusable process proxy variables from outside this repository.
- The project should avoid asking the PM to manually clear proxy variables before each automation run.
- Local scripts may clear only process-level proxy variables; user-level or machine-level environment settings are not modified.
- GitHub CLI authentication remains managed by `gh auth login`; this change validates it but does not create or rotate credentials.

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir "git-github-preflight.ps1")
Initialize-AgenticGitShellEnvironment | Out-Null

$labels = @(
    @{ Name = "codex:auto-fix"; Description = "Ready for local Codex automation"; Color = "0E8A16" },
    @{ Name = "codex:working"; Description = "Codex is working on this issue"; Color = "FBCA04" },
    @{ Name = "codex:pr-created"; Description = "Codex created a PR for this issue"; Color = "5319E7" },
    @{ Name = "codex:failed"; Description = "Codex failed to process this issue"; Color = "D93F0B" },
    @{ Name = "codex:manual-review"; Description = "Needs human review before Codex continues"; Color = "BFD4F2" },
    @{ Name = "risk:high"; Description = "High-risk change; requires manual review"; Color = "B60205" },
    @{ Name = "area:frontend"; Description = "Frontend-related issue"; Color = "1D76DB" },
    @{ Name = "area:backend"; Description = "Backend-related issue"; Color = "0052CC" },
    @{ Name = "area:infra"; Description = "Infrastructure-related issue"; Color = "5319E7" },
    @{ Name = "area:test"; Description = "Testing-related issue"; Color = "C5DEF5" }
)

foreach ($label in $labels) {
    Write-Host "Creating or updating label: $($label.Name)"
    gh label create $label.Name `
        --description $label.Description `
        --color $label.Color `
        --force
}

Write-Host "Codex labels initialized."

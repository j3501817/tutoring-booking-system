param(
    [string]$DefaultBranch = "main",
    [string]$TemplateRepository = "RayCheng75/agentic-project-template",
    [switch]$AllowTemplateRepository,
    [switch]$AllowExistingHistory
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir "git-github-preflight.ps1")
Initialize-AgenticGitShellEnvironment | Out-Null

function Require-Command {
    param([string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Write-Error "Required command not found in PATH: $Name"
    }
}

function Get-NormalizedGitHubRepository {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ""
    }

    $normalized = $Value.Trim().Replace('\', '/')
    if ($normalized.EndsWith(".git")) {
        $normalized = $normalized.Substring(0, $normalized.Length - 4)
    }

    if ($normalized -match 'github\.com[:/](?<owner>[^/\s:]+)/(?<repo>[^/\s]+)$') {
        return "$($Matches.owner)/$($Matches.repo)".ToLowerInvariant()
    }

    if ($normalized -match '^(?<owner>[^/\s:]+)/(?<repo>[^/\s]+)$') {
        return "$($Matches.owner)/$($Matches.repo)".ToLowerInvariant()
    }

    return $normalized.ToLowerInvariant()
}

function Test-TemplateAdoptionState {
    param(
        [string]$TemplateRepository,
        [bool]$AllowTemplateRepository,
        [bool]$AllowExistingHistory
    )

    Write-Host "Checking template adoption state..."

    $originUrl = git remote get-url origin 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($originUrl)) {
        Write-Error "Git remote 'origin' is not configured. Create a new GitHub repository, add it as origin, then run this initializer."
    }

    $originRepo = Get-NormalizedGitHubRepository $originUrl
    $templateRepo = Get-NormalizedGitHubRepository $TemplateRepository

    if ($originRepo -eq $templateRepo -and -not $AllowTemplateRepository) {
        Write-Error "origin still points to the template repository ($TemplateRepository). Create a new repository from the template first, then clone that new repository. If you are maintaining the template itself, rerun with -AllowTemplateRepository -AllowExistingHistory."
    }

    if ((Test-Path ".git/shallow") -and -not $AllowExistingHistory) {
        Write-Error "This repository is a shallow clone. Do not use a shallow clone of the template as a new system baseline. Use GitHub 'Use this template' or copy files without .git, then rerun."
    }

    git rev-parse --verify HEAD 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        return
    }

    $commitCountText = git rev-list --count HEAD
    $commitCount = [int]$commitCountText

    if ($commitCount -gt 1 -and -not $AllowExistingHistory) {
        Write-Error "This repository already has $commitCount commits. For a new system, this usually means template history was inherited. Recreate the repository via GitHub 'Use this template' or copy files without .git. If you are intentionally installing into an existing product repository, rerun with -AllowExistingHistory."
    }
}

Write-Host "Checking prerequisites..."
Require-Command "git"
Require-Command "gh"
Require-Command "node"
Require-Command "codex"

Write-Host "Checking Git repository..."
git rev-parse --show-toplevel | Out-Null
Test-TemplateAdoptionState `
    -TemplateRepository $TemplateRepository `
    -AllowTemplateRepository ([bool]$AllowTemplateRepository) `
    -AllowExistingHistory ([bool]$AllowExistingHistory)

Test-AgenticGitHubPreflight -GhCommand "gh" -RemoteName "origin"

Write-Host "Initializing labels..."
.\scripts\setup-codex-labels.ps1

Write-Host "Checking AGENTS.md..."
if (-not (Test-Path "AGENTS.md")) {
    Write-Error "AGENTS.md not found. Please copy AGENTS_multilang.md to AGENTS.md first."
}

Write-Host "Checking CI workflow..."
if (-not (Test-Path ".github/workflows/ci.yml")) {
    Write-Error ".github/workflows/ci.yml not found."
}

Write-Host "Creating agent temp directory..."
New-Item -ItemType Directory -Force -Path ".agent-tmp/work-items" | Out-Null

Write-Host "Generating project profile cache..."
node scripts/generate-project-profile.js

Write-Host "Running work item artifact contract self-check..."
node scripts/self-check-artifact-contract.js

Write-Host "Creating .gitignore entries..."

$gitignoreLines = @(
    "",
    "# Agent temporary work items",
    ".agent-tmp/project-profile.json"
)

if (-not (Test-Path ".gitignore")) {
    New-Item -ItemType File -Path ".gitignore" | Out-Null
}

$currentGitignore = Get-Content ".gitignore" -Raw

foreach ($line in $gitignoreLines) {
    if ($currentGitignore -notlike "*$line*") {
        Add-Content ".gitignore" $line
    }
}

Write-Host "Template initialization completed."
Write-Host "Next steps:"
Write-Host "1. Create GitHub issues"
Write-Host "2. Add label codex:auto-fix"
Write-Host "3. Run: .\scripts\codex-batch-issues.ps1 -MaxParallel 1"

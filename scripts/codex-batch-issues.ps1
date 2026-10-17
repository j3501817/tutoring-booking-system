param(
    [string]$Label = "codex:auto-fix",
    [int]$MaxParallel = 5,
    [string]$BaseBranch = "main",
    [string]$CodexCommand = "codex",
    [string]$GhCommand = "gh",
    [switch]$DryRun,
    [string]$IssueJsonPath,
    [string]$DryRunOutputRoot = ".agent-tmp/work-items",
    [string]$ProjectProfilePath = ".agent-tmp/project-profile.json",
    [int]$MaxRetries = 1,
    [int]$MinimumIssueBodyLength = 80,
    [switch]$OverrideRoutingGates
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$GitHubPreflightScript = Join-Path $ScriptDir "git-github-preflight.ps1"
. $GitHubPreflightScript
Initialize-AgenticGitShellEnvironment | Out-Null

function Require-Command {
    param([string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command not found in PATH: $Name"
    }
}

Require-Command "git"
Require-Command "node"

$NeedsGitHub = (-not $DryRun -or -not $IssueJsonPath)

if ($NeedsGitHub) {
    Require-Command $GhCommand
}

if (-not $DryRun) {
    Require-Command $CodexCommand
}

$RepoRoot = git rev-parse --show-toplevel
$RepoName = Split-Path $RepoRoot -Leaf
$ParentDir = Split-Path $RepoRoot -Parent

Set-Location $RepoRoot

if ($NeedsGitHub) {
    Test-AgenticGitHubPreflight -GhCommand $GhCommand -SkipGitRemote:$DryRun
}

Write-Host "Updating base branch: $BaseBranch"
if ($DryRun) {
    Write-Host "Dry run enabled; skipping checkout and pull."
}
else {
    git checkout $BaseBranch
    git pull --ff-only origin $BaseBranch
}

if (-not (Test-Path $ProjectProfilePath)) {
    Write-Host "Project profile not found; generating: $ProjectProfilePath"
    node scripts/generate-project-profile.js
}
else {
    Write-Host "Using existing project profile: $ProjectProfilePath"
}

$ProjectProfileJson = Get-Content -Raw -Path $ProjectProfilePath

if ($IssueJsonPath) {
    Write-Host "Loading issues from JSON file: $IssueJsonPath"
    $issuesJson = Get-Content -Raw -Path $IssueJsonPath
}
else {
    Write-Host "Fetching issues with label: $Label"
    $issuesJson = & $GhCommand issue list `
        --label $Label `
        --state open `
        --limit $MaxParallel `
        --json number,title,body,url,labels
}

$parsedIssues = $issuesJson | ConvertFrom-Json
$issues = if ($parsedIssues -is [array]) { $parsedIssues } else { @($parsedIssues) }

if (-not $issues -or $issues.Count -eq 0) {
    Write-Host "No issues found."
    exit 0
}

$jobs = @()

function Get-IssueRouteKey {
    param($IssueData)

    $labelNames = @()
    if ($IssueData.labels) {
        $labelNames = @($IssueData.labels | ForEach-Object { $_.name })
    }

    foreach ($label in $labelNames) {
        if ($label -match '^area:(.+)$') {
            return "area:$($Matches[1])"
        }
    }

    $text = "$($IssueData.title)`n$($IssueData.body)".ToLowerInvariant()
    if ($text -match '\b(auth|authentication|authorization|permission|rbac)\b') { return "risk-surface:auth" }
    if ($text -match '\b(database|db|migration|schema)\b') { return "risk-surface:data" }
    if ($text -match '\b(ci|workflow|github actions|deployment|deploy)\b') { return "risk-surface:delivery" }
    if ($text -match '\b(infra|terraform|kubernetes|docker)\b') { return "risk-surface:infra" }
    if ($text -match '\b(shared|common|core|template|agentic)\b') { return "risk-surface:shared" }

    return ""
}

foreach ($issue in $issues) {
    while (($jobs | Where-Object { $_.State -eq "Running" }).Count -ge $MaxParallel) {
        Start-Sleep -Seconds 3
    }

    $issueNumber = $issue.number
    $issueSeedJson = $issue | ConvertTo-Json -Depth 20
    $routeKey = Get-IssueRouteKey $issue

    if ($routeKey -and -not $OverrideRoutingGates) {
        while (($jobs | Where-Object { $_.State -eq "Running" -and $_.RouteKey -eq $routeKey }).Count -gt 0) {
            Write-Host "Waiting for route gate: $routeKey"
            Start-Sleep -Seconds 3
        }
    }

    $job = Start-Job -ArgumentList $RepoRoot, $RepoName, $ParentDir, $BaseBranch, $issueNumber, $issueSeedJson, $Label, $CodexCommand, $GhCommand, ([bool]$DryRun), $DryRunOutputRoot, $GitHubPreflightScript, $ProjectProfileJson, $ProjectProfilePath, $MaxRetries, $MinimumIssueBodyLength, ([bool]$OverrideRoutingGates) -ScriptBlock {
        param($RepoRoot, $RepoName, $ParentDir, $BaseBranch, $IssueNumber, $IssueSeedJson, $TriggerLabel, $CodexCommand, $GhCommand, [bool]$DryRun, $DryRunOutputRoot, $GitHubPreflightScript, $ProjectProfileJson, $ProjectProfilePath, [int]$MaxRetries, [int]$MinimumIssueBodyLength, [bool]$OverrideRoutingGates)

        $ErrorActionPreference = "Stop"

        . $GitHubPreflightScript
        Initialize-AgenticGitShellEnvironment -Quiet | Out-Null

        $Branch = "codex/issue-$IssueNumber"
        $Worktree = if ($DryRun) { $RepoRoot } else { Join-Path $ParentDir "$RepoName-issue-$IssueNumber" }
        $WorkItemId = if ($DryRun) { "dry-run-issue-$IssueNumber" } else { "issue-$IssueNumber" }
        $SandboxRel = if ($DryRun) { "$DryRunOutputRoot/$WorkItemId" } else { ".agent-tmp/work-items/$WorkItemId" }
        $SandboxRel = $SandboxRel.Replace('\', '/')
        $SandboxPath = Join-Path $Worktree $SandboxRel
        $RootSandboxPath = Join-Path $RepoRoot $SandboxRel
        $ProjectProfile = $ProjectProfileJson | ConvertFrom-Json
        $ArtifactContractPath = Join-Path $RepoRoot "docs/schemas/work-item-artifacts.schema.json"
        $ArtifactContract = (Get-Content -Raw -Path $ArtifactContractPath | ConvertFrom-Json).agenticArtifactContract

        $Done = [char]::ConvertFromUtf32(0x2705)
        $InProgress = [char]::ConvertFromUtf32(0x1F6A7)
        $Pending = [char]::ConvertFromUtf32(0x2B1C) + [char]0xFE0F

        function Invoke-GhIssueLabelUpdate {
            param(
                [int]$IssueNumber,
                [string[]]$RemoveLabels = @(),
                [string[]]$AddLabels = @()
            )

            $args = @("issue", "edit", $IssueNumber)

            foreach ($label in $RemoveLabels) {
                $args += @("--remove-label", $label)
            }

            foreach ($label in $AddLabels) {
                $args += @("--add-label", $label)
            }

            if ($args.Count -gt 3) {
                & $GhCommand @args
            }
        }

        function Write-Utf8File {
            param(
                [string]$Path,
                [string]$Content
            )

            $parent = Split-Path $Path -Parent
            if ($parent) {
                New-Item -ItemType Directory -Force -Path $parent | Out-Null
            }

            $encoding = New-Object System.Text.UTF8Encoding($false)
            [System.IO.File]::WriteAllText($Path, $Content, $encoding)
        }

        function Assert-CleanWorktree {
            param([string]$Path)

            Push-Location $Path
            try {
                $status = git status --porcelain
                if ($status) {
                    throw "Existing worktree is not clean: $Path"
                }
            }
            finally {
                Pop-Location
            }
        }

        function Commit-PathIfChanged {
            param(
                [string[]]$PathSpec,
                [string]$Message
            )

            git add -- @PathSpec
            git diff --cached --quiet -- @PathSpec
            if ($LASTEXITCODE -ne 0) {
                git commit -m $Message
                return $true
            }

            return $false
        }

        function Get-IssueLabelNames {
            param($IssueData)

            if (-not $IssueData.labels) {
                return @()
            }

            return @($IssueData.labels | ForEach-Object { $_.name })
        }

        function Test-HighRiskIssue {
            param($IssueData)

            $labelNames = Get-IssueLabelNames $IssueData
            if ($labelNames -contains "risk:high") {
                return $true
            }

            $text = "$($IssueData.title)`n$($IssueData.body)"
            $highRiskPattern = '(?i)\b(authentication|authorization|auth|payment|billing|security|secret|secrets|database migration|db migration|migration|production deploy|production deployment|infrastructure|terraform|kubernetes|permission model|permission|data deletion|delete data|destructive)\b'
            return $text -match $highRiskPattern
        }

        function Test-InsufficientIssue {
            param($IssueData)

            $body = [string]$IssueData.body
            if ([string]::IsNullOrWhiteSpace($body)) {
                return $true
            }

            if ($body.Trim().Length -lt 40) {
                return $true
            }

            return $false
        }

        function Get-IssueCompletenessFailures {
            param($IssueData)

            $failures = @()
            $body = [string]$IssueData.body
            $text = "$($IssueData.title)`n$body"

            if ([string]::IsNullOrWhiteSpace($body)) {
                $failures += "Issue body is empty."
            }
            elseif ($body.Trim().Length -lt $MinimumIssueBodyLength) {
                $failures += "Issue body is shorter than the configured minimum useful length: $MinimumIssueBodyLength characters."
            }

            $mojibakeLead = [string][char]0x5699
            if ($text.Contains([char]0xFFFD) -or $text.Contains($mojibakeLead) -or $text.Contains("????????")) {
                $failures += "Issue text appears corrupted or contains mojibake/replacement characters."
            }

            $hasRequestedChange = $text -match '(?i)\b(please|request|fix|add|update|change|implement|create|remove|refactor|document)\b'
            $hasCjkContent = $text -match '\p{IsCJKUnifiedIdeographs}'
            if (-not $hasRequestedChange -and -not $hasCjkContent) {
                $failures += "Issue does not contain a clear requested change."
            }

            $hasAreaLabel = @(Get-IssueLabelNames $IssueData | Where-Object { $_ -match '^area:' }).Count -gt 0
            if ($text -notmatch '(?i)\b(acceptance|criteria|expected|behavior|component|area|scope|affected)\b' -and -not $hasAreaLabel -and -not $hasCjkContent) {
                $failures += "Issue does not include acceptance criteria, expected behavior, or an affected component."
            }

            return $failures
        }

        function Get-CandidateFiles {
            param($IssueData)

            $text = "$($IssueData.title) $($IssueData.body)".ToLowerInvariant()
            $tokens = @($text -split '[^a-z0-9_\-/\.]+' | Where-Object { $_.Length -ge 4 } | Select-Object -Unique)
            if ($tokens.Count -eq 0) {
                return @()
            }

            $files = @(git ls-files | Where-Object { $_ -notmatch '^\.agent-tmp/' })
            $matches = @()

            foreach ($file in $files) {
                $lowerFile = $file.ToLowerInvariant()
                foreach ($token in $tokens) {
                    if ($lowerFile.Contains($token)) {
                        $matches += $file
                        break
                    }
                }
            }

            return @($matches | Select-Object -First 20)
        }

        function ConvertTo-MarkdownList {
            param(
                [string[]]$Items,
                [string]$EmptyText = "None"
            )

            if (-not $Items -or $Items.Count -eq 0) {
                return "- $EmptyText"
            }

            return (($Items | ForEach-Object { "- $_" }) -join "`n")
        }

        function Get-ContractRequiredArtifacts {
            param([string]$Source = "github-issue")

            $items = @($ArtifactContract.requiredArtifacts.common)
            if ($Source -eq "github-issue") {
                $items += @($ArtifactContract.requiredArtifacts.githubIssue)
            }

            return @($items | Select-Object -Unique)
        }

        function Get-ContractCompletionArtifacts {
            param([string]$Source = "github-issue")

            if ($Source -eq "github-issue") {
                return @($ArtifactContract.completionArtifacts.githubIssueOneOf)
            }

            return @()
        }

        function Assert-WorkItemArtifactContract {
            param([string]$Stage)

            Push-Location $Worktree
            try {
                $output = & node scripts/validate-work-item.js --work-item $WorkItemId 2>&1
                $exitCode = $LASTEXITCODE
                if ($exitCode -ne 0) {
                    throw "Work item artifact contract validation failed during $Stage.`n$($output -join "`n")"
                }
            }
            finally {
                Pop-Location
            }
        }

        function Add-TestReportSection {
            param(
                [string]$Heading,
                [string[]]$Lines
            )

            $content = "`n## $Heading`n`n" + (($Lines | ForEach-Object { "$_" }) -join "`n") + "`n"
            Add-Content -Encoding utf8 -Path "$SandboxPath/TEST_REPORT.md" -Value $content
        }

        function Invoke-PrePrValidationCommand {
            param(
                [string]$DisplayName,
                [string[]]$Command
            )

            $executable = $Command[0]
            $arguments = @()
            if ($Command.Count -gt 1) {
                $arguments = @($Command[1..($Command.Count - 1)])
            }

            $output = & $executable @arguments 2>&1
            $exitCode = $LASTEXITCODE
            $result = if ($exitCode -eq 0) { "passed" } else { "failed" }

            Add-TestReportSection "Pre-PR CI-Parity: $DisplayName" @(
                "- Command: $($Command -join ' ')",
                "- Result: $result",
                "",
                '```text',
                ($output -join "`n"),
                '```'
            )

            if ($exitCode -ne 0) {
                throw "Pre-PR CI-parity validation failed: $DisplayName"
            }
        }

        function Invoke-PrePrCiParityValidation {
            Write-Host "Running pre-PR CI-parity validation for $WorkItemId"

            Invoke-PrePrValidationCommand "artifact contract" @("node", "scripts/validate-work-item.js", "--work-item", $WorkItemId)
            Invoke-PrePrValidationCommand "secret scan" @("node", "scripts/check-secrets.js")
            Invoke-PrePrValidationCommand "safe application checks" @("node", "scripts/run-app-checks.js")

            Add-TestReportSection "Pre-PR CI-Parity Summary" @(
                "- Work item artifact contract: passed",
                "- Secret scan: passed",
                "- Safe application checks: passed",
                "- Opt-in DB, Blob, Function Host, e2e, connectivity, and external-notification tests were not requested by this wrapper."
            )
        }

        function Add-PrCheckStatusToDeliveryReport {
            param([string]$BranchName)

            try {
                $prInfoJson = & $GhCommand pr view $BranchName --json number,url,statusCheckRollup
                $prInfo = $prInfoJson | ConvertFrom-Json
                $checks = @($prInfo.statusCheckRollup)

                $lines = @(
                    "## PR Check Status",
                    "",
                    "- PR: #$($prInfo.number)",
                    "- URL: $($prInfo.url)"
                )

                if ($checks.Count -eq 0) {
                    $lines += "- Status: no checks reported yet or checks are still pending."
                }
                else {
                    foreach ($check in $checks) {
                        $name = if ($check.name) { $check.name } elseif ($check.context) { $check.context } else { "unnamed check" }
                        $state = if ($check.conclusion) { $check.conclusion } elseif ($check.status) { $check.status } elseif ($check.state) { $check.state } else { "unknown" }
                        $lines += "- ${name}: $state"
                    }
                }

                Add-Content -Encoding utf8 -Path "$SandboxPath/DELIVERY_REPORT.md" -Value ("`n" + ($lines -join "`n") + "`n")
            }
            catch {
                Add-Content -Encoding utf8 -Path "$SandboxPath/DELIVERY_REPORT.md" -Value "`n## PR Check Status`n`n- Status: unable to fetch PR checks after PR creation.`n- Reason: $($_.Exception.Message)`n"
            }
        }

        function Write-ContextPack {
            param($IssueData)

            $safeCommands = @($ProjectProfile.safeValidationCommands)
            $focusedCommands = @($ProjectProfile.focusedValidationCommands)
            $envNames = if ($ProjectProfile.requiredEnvPlaceholders) {
                @($ProjectProfile.requiredEnvPlaceholders.PSObject.Properties | ForEach-Object { $_.Name })
            }
            else {
                @()
            }
            $optInMarkers = @($ProjectProfile.optInTestMarkers)
            $candidateFiles = @(Get-CandidateFiles $IssueData)

            $content = @"
# Context Pack

## Project Profile Summary

- Profile path: $ProjectProfilePath
- Primary language: $($ProjectProfile.primaryLanguage)
- Application type: $($ProjectProfile.applicationType)
- Governance runtime: $($ProjectProfile.governanceRuntime)
- Package manager: $($ProjectProfile.packageManager)

## Issue Metadata

- Issue: #$($IssueData.number)
- Title: $($IssueData.title)
- URL: $($IssueData.url)
- Labels: $((Get-IssueLabelNames $IssueData) -join ", ")

## Safe Validation Commands

$(ConvertTo-MarkdownList $safeCommands "No safe validation commands configured")

## Focused Validation Commands

$(ConvertTo-MarkdownList $focusedCommands "No focused validation commands configured")

## Required Environment Placeholders

$(ConvertTo-MarkdownList $envNames "No required placeholder environment variables configured")

## Opt-In Tests

Do not run these by default unless the issue explicitly requires them:

$(ConvertTo-MarkdownList $optInMarkers "No opt-in markers configured")

## Candidate Files

These are deterministic hints from file-name matching, not authoritative requirements:

$(ConvertTo-MarkdownList $candidateFiles "No candidate files found")

## Scope Boundaries

- Solve only issue #$($IssueData.number).
- Prefer this context pack and `.agent-tmp/project-profile.json` over broad rediscovery.
- Do not run real external service, database, Blob, Function Host, connectivity, or e2e tests unless the issue explicitly requires them.
- Do not use production secrets for local validation.
"@

            Write-Utf8File "$SandboxPath/CONTEXT.md" $content
        }

        function Write-StatusArtifact {
            param(
                [string[]]$Lines
            )

            $statusMd = "# Status`n`n" + ($Lines -join "`n") + "`n"
            Write-Utf8File "$SandboxPath/status.md" $statusMd
        }

        function Write-InitialArtifacts {
            param($IssueData)

            New-Item -ItemType Directory -Force -Path $SandboxPath | Out-Null

            $labelText = ((Get-IssueLabelNames $IssueData) -join ", ")
            $issueJson = $IssueData | ConvertTo-Json -Depth 20

            Write-Utf8File "$SandboxPath/issue.json" $issueJson

            $issueMd = @"
# Issue #$($IssueData.number)

## Title
$($IssueData.title)

## URL
$($IssueData.url)

## Labels
$labelText

## Body
$($IssueData.body)
"@
            Write-Utf8File "$SandboxPath/issue.md" $issueMd

            $requiredArtifacts = @(Get-ContractRequiredArtifacts "github-issue")
            $completionArtifacts = @(Get-ContractCompletionArtifacts "github-issue")

            $manifest = @{
                id = $WorkItemId
                title = $IssueData.title
                source = "github-issue"
                issueNumber = $IssueData.number
                issueUrl = $IssueData.url
                pmApproved = $true
                queueState = "intake"
                retryCount = 0
                maxRetries = $MaxRetries
                requiredArtifacts = $requiredArtifacts
                completionArtifactsOneOf = $completionArtifacts
            } | ConvertTo-Json -Depth 10
            Write-Utf8File "$SandboxPath/work-item.json" $manifest

            Write-StatusArtifact @(
                "- $Done INTAKE: GitHub Issue context saved",
                "- $InProgress SPEC: Validating issue scope and risk",
                "- $Pending PLAN: Pending Codex implementation",
                "- $Pending BUILD: Pending Codex implementation",
                "- $Pending REVIEW: Pending review",
                "- $Pending VERIFY: Pending validation",
                "- $Pending DELIVER: Pending PR delivery"
            )

            Write-Utf8File "$SandboxPath/assumptions.md" "# Assumptions`n`n- The issue is approved for local automation because it has the trigger label: $TriggerLabel.`n"
            Write-Utf8File "$SandboxPath/PLAN.md" "# Plan`n`nPending Codex implementation plan.`n"
            Write-Utf8File "$SandboxPath/TEST_REPORT.md" "# Test Report`n`nPending Codex validation.`n"
            Write-Utf8File "$SandboxPath/DELIVERY_REPORT.md" "# Delivery Report`n`nPending Codex delivery summary.`n"
        }

        function Set-WorkItemQueueState {
            param(
                [string]$QueueState,
                [int]$RetryCount = 0
            )

            $manifestPath = "$SandboxPath/work-item.json"
            if (-not (Test-Path $manifestPath)) {
                return
            }

            $manifest = Get-Content -Raw -Path $manifestPath | ConvertFrom-Json
            $manifest.queueState = $QueueState
            $manifest.retryCount = $RetryCount
            $manifest.maxRetries = $MaxRetries
            Write-Utf8File $manifestPath ($manifest | ConvertTo-Json -Depth 20)
        }

        function Write-PmReview {
            param(
                [string]$Reason,
                [string]$Stage = "preflight",
                [string[]]$Attempts = @()
            )

            if (-not (Test-Path $SandboxPath)) {
                New-Item -ItemType Directory -Force -Path $SandboxPath | Out-Null
            }

            $attemptText = if ($Attempts.Count -gt 0) {
                ($Attempts | ForEach-Object { "- $_" }) -join "`n"
            }
            else {
                "- No automated recovery attempted because the issue requires PM review or the failure is outside script control."
            }

            $content = @"
# PM Review

## Work item
$WorkItemId

## Stage
$Stage

## Reason
$Reason

## Attempts
$attemptText
"@
            Write-Utf8File "$SandboxPath/PM_Review.md" $content

            if ($DryRun) {
                return
            }

            try {
                node scripts/notify-pm.js $WorkItemId
            }
            catch {
                Add-Content -Encoding utf8 -Path "$SandboxPath/PM_Review.md" -Value "`n## Notification failure`n$($_.Exception.Message)`n"
            }
        }

        try {
            Write-Host "Preparing issue #$IssueNumber"
            Set-Location $RepoRoot

            $issueJson = if ($DryRun) {
                $IssueSeedJson
            }
            else {
                & $GhCommand issue view $IssueNumber --json number,title,body,url,labels
            }
            $issueData = $issueJson | ConvertFrom-Json

            $riskDetected = Test-HighRiskIssue $issueData
            $completenessFailures = @(Get-IssueCompletenessFailures $issueData)
            $hasCompletenessFailures = $completenessFailures.Count -gt 0

            if (($riskDetected -or $hasCompletenessFailures) -and -not $OverrideRoutingGates) {
                $SandboxPath = $RootSandboxPath
                Write-InitialArtifacts $issueData
                Write-ContextPack $issueData
                Set-WorkItemQueueState "manual-review"

                if ($riskDetected) {
                    Write-PmReview "Issue is high risk and requires PM review before automation." "risk-check"
                }

                if ($completenessFailures.Count -gt 0) {
                    Write-PmReview (($completenessFailures -join "`n") + "`nNo implementation branch was created because the issue failed completeness preflight.") "spec-check"
                }

                Write-StatusArtifact @(
                    "- $Done INTAKE: GitHub Issue context saved",
                    "- $Done SPEC: Routing gate evaluated",
                    "- $Pending PLAN: Implementation blocked before branch creation",
                    "- $Pending BUILD: Not started",
                    "- $Pending REVIEW: PM manual review required",
                    "- $Pending VERIFY: Not run",
                    "- $Pending DELIVER: Manual-review artifact produced"
                )

                $Worktree = $RepoRoot
                Assert-WorkItemArtifactContract "manual-review routing"

                if (-not $DryRun) {
                    Invoke-GhIssueLabelUpdate -IssueNumber $IssueNumber -RemoveLabels @($TriggerLabel, "codex:working") -AddLabels @("codex:manual-review")
                }

                Write-Host "Issue #$IssueNumber moved to manual review before Codex execution."
                return
            }

            if ($DryRun) {
                Write-Host "Dry run enabled; using current repository as artifact target."
            }
            elseif (-not (Test-Path $Worktree)) {
                $branchExists = $false
                try {
                    git show-ref --verify --quiet "refs/heads/$Branch"
                    $branchExists = $true
                }
                catch {
                    $branchExists = $false
                }

                if ($branchExists) {
                    git worktree add $Worktree $Branch
                }
                else {
                    git worktree add $Worktree -b $Branch $BaseBranch
                }
            }
            else {
                Write-Host "Worktree already exists: $Worktree"
                Assert-CleanWorktree $Worktree
            }

            Set-Location $Worktree

            Write-InitialArtifacts $issueData
            Write-ContextPack $issueData
            Assert-WorkItemArtifactContract "work-item initialization"
            Set-WorkItemQueueState "ready-for-codex"

            if (-not $DryRun) {
                Invoke-GhIssueLabelUpdate -IssueNumber $IssueNumber -RemoveLabels @($TriggerLabel) -AddLabels @("codex:working")
            }

            Write-StatusArtifact @(
                "- $Done INTAKE: GitHub Issue context saved",
                "- $Done SPEC: Scope and risk checks passed",
                "- $Done PLAN: Local worktree and branch prepared",
                "- $InProgress BUILD: Codex implementation in progress",
                "- $Pending REVIEW: Pending review",
                "- $Pending VERIFY: Pending validation",
                "- $Pending DELIVER: Pending PR delivery"
            )
            Set-WorkItemQueueState "codex-running"

            if ($DryRun) {
                Set-WorkItemQueueState "ready-for-codex"
                Write-Utf8File "$SandboxPath/DELIVERY_REPORT.md" "# Delivery Report`n`nDry run completed after artifact generation and routing checks. Codex, commit, push, PR, and label mutations were skipped.`n"
                Write-Utf8File "$SandboxPath/TEST_REPORT.md" "# Test Report`n`nDry run completed. No application validation commands were executed.`n"
                Write-Host "Dry run completed for issue #$IssueNumber."
                return
            }

            $prompt = @"
Read AGENTS.md, $ProjectProfilePath, $SandboxRel/issue.md, and $SandboxRel/CONTEXT.md.

You are assigned only GitHub issue #$IssueNumber.

Requirements:
- Follow AGENTS.md strictly.
- Use work-item-id: $WorkItemId.
- Use sandbox directory: $SandboxRel.
- Use the existing project profile unless this issue explicitly requires changing project structure.
- Prefer validation commands and required environment placeholders from $ProjectProfilePath and $SandboxRel/CONTEXT.md.
- Do not rediscover project language, framework, or validation commands unless the project profile is missing, stale, or explicitly in scope for this issue.
- If the profile or context pack is insufficient, record the gap in $SandboxRel/assumptions.md before doing additional discovery.
- Do not run opt-in, connectivity, e2e, real external service, database, Blob, or Function Host tests unless the issue explicitly requires them.
- Solve only this issue.
- Keep changes minimal and scoped.
- Add or update tests when appropriate.
- Treat this as one Codex implementation run with one focused validation pass and one final safe validation pass by default.
- If validation fails for deterministic environment or governance setup, repair once; if product behavior still fails, write PM_Review.md instead of cycling indefinitely.
- Write validation results to $SandboxRel/TEST_REPORT.md.
- Write delivery summary to $SandboxRel/DELIVERY_REPORT.md.
- Do not push to main.
- Do not create or merge PRs.
- Leave final changes in the working tree; this wrapper will commit, push, and open the PR.
"@

            & $CodexCommand exec $prompt

            if (-not (Test-Path "$SandboxPath/TEST_REPORT.md") -or ((Get-Item "$SandboxPath/TEST_REPORT.md").Length -eq 0)) {
                throw "Codex did not produce a non-empty TEST_REPORT.md."
            }

            if (-not (Test-Path "$SandboxPath/DELIVERY_REPORT.md") -or ((Get-Item "$SandboxPath/DELIVERY_REPORT.md").Length -eq 0)) {
                Write-Utf8File "$SandboxPath/DELIVERY_REPORT.md" "# Delivery Report`n`nCodex completed execution. Review git diff and CI results before merge.`n"
            }

            Invoke-PrePrCiParityValidation

            $status = git status --porcelain

            if ($status) {
                git add -A
                git commit -m "Fix #$IssueNumber`: $($issueData.title)"
            }
            else {
                Write-PmReview "Codex completed without producing file changes." "delivery"
                Commit-PathIfChanged @($SandboxRel) "Document no-op result for #$IssueNumber" | Out-Null
            }

            git push -u origin $Branch

            $existingPr = $null
            try {
                $existingPr = & $GhCommand pr view $Branch --json number,url 2>$null
            }
            catch {
                $existingPr = $null
            }

            if (-not $existingPr) {
                $prBody = @"
## Issue
- Issue: #$IssueNumber
- URL: $($issueData.url)

## Change summary
- Local Codex automation processed the assigned issue.
- See $SandboxRel/DELIVERY_REPORT.md for the delivery summary.

## Modified files
- Review the PR file diff for the authoritative file list.

## Tests and validation
- See $SandboxRel/TEST_REPORT.md.
- GitHub Actions CI must pass before merge.

## Known risks
- Local automation result requires human PR review before merge.

## PM decision
- PM review required before merge: yes
"@

                & $GhCommand pr create `
                    --title "Fix #$IssueNumber`: $($issueData.title)" `
                    --body $prBody `
                    --base $BaseBranch `
                    --head $Branch
            }
            else {
                Write-Host "PR already exists for $Branch"
            }

            Add-PrCheckStatusToDeliveryReport $Branch

            Write-StatusArtifact @(
                "- $Done INTAKE: GitHub Issue context saved",
                "- $Done SPEC: Scope and risk checks passed",
                "- $Done PLAN: Local worktree and branch prepared",
                "- $Done BUILD: Codex implementation completed",
                "- $Done REVIEW: PR created for human review",
                "- $Done VERIFY: Validation report produced",
                "- $Done DELIVER: Branch pushed and PR available"
            )
            Set-WorkItemQueueState "pr-created"

            if (Commit-PathIfChanged @("$SandboxRel/status.md", "$SandboxRel/DELIVERY_REPORT.md") "Update delivery status for #$IssueNumber") {
                git push -u origin $Branch
            }

            Invoke-GhIssueLabelUpdate -IssueNumber $IssueNumber -RemoveLabels @("codex:working") -AddLabels @("codex:pr-created")

            Write-Host "Done issue #$IssueNumber"
        }
        catch {
            $message = $_.Exception.Message
            Write-Host "Issue #$IssueNumber failed: $message"

            try {
                if (Test-Path $Worktree) {
                    Set-Location $Worktree
                }
                else {
                    New-Item -ItemType Directory -Force -Path (Join-Path $RepoRoot $SandboxRel) | Out-Null
                    $SandboxPath = Join-Path $RepoRoot $SandboxRel
                }

                Write-PmReview $message "failure" @("Captured exception from batch wrapper.", "Stopped further automated work for this issue.", "Marked issue for failed/manual review label handling.")
                Set-WorkItemQueueState "codex-failed" 1

                if (-not $DryRun -and (Test-Path $Worktree)) {
                    try {
                        if (Commit-PathIfChanged @($SandboxRel) "Document automation failure for #$IssueNumber") {
                            git push -u origin $Branch
                        }
                    }
                    catch {
                        Add-Content -Encoding utf8 -Path "$SandboxPath/PM_Review.md" -Value "`n## Artifact commit failure`n$($_.Exception.Message)`n"
                    }
                }
            }
            catch {
                Write-Host "Failed to write PM review artifacts: $($_.Exception.Message)"
            }

            try {
                if (-not $DryRun) {
                    Invoke-GhIssueLabelUpdate -IssueNumber $IssueNumber -RemoveLabels @($TriggerLabel, "codex:working") -AddLabels @("codex:failed")
                }
            }
            catch {
                Write-Host "Failed to update failure labels: $($_.Exception.Message)"
            }
        }
    }

    $job | Add-Member -NotePropertyName RouteKey -NotePropertyValue $routeKey
    $jobs += $job
}

Write-Host "Waiting for Codex jobs..."
Wait-Job $jobs

foreach ($job in $jobs) {
    Receive-Job $job
}

Remove-Job $jobs

Write-Host "All jobs completed."

$AgenticProxyEnvironmentNames = @(
    "HTTP_PROXY",
    "HTTPS_PROXY",
    "ALL_PROXY",
    "http_proxy",
    "https_proxy",
    "all_proxy"
)

function Initialize-AgenticGitShellEnvironment {
    param(
        [switch]$Quiet
    )

    $removed = @()

    foreach ($name in $AgenticProxyEnvironmentNames) {
        $value = [Environment]::GetEnvironmentVariable($name, "Process")
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            $removed += "$name=$value"
            Remove-Item -Path "Env:\$name" -ErrorAction SilentlyContinue
        }
    }

    $env:GIT_TERMINAL_PROMPT = "0"
    $env:GH_PROMPT_DISABLED = "1"

    if (-not $Quiet -and $removed.Count -gt 0) {
        Write-Host "Cleared process proxy variables for local Git/GitHub commands:"
        foreach ($item in $removed) {
            Write-Host " - $item"
        }
    }

    return $removed
}

function Invoke-AgenticNativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $FilePath $($ArgumentList -join ' ')"
    }
}

function Test-AgenticGitHubPreflight {
    param(
        [string]$GhCommand = "gh",
        [string]$RemoteName = "origin",
        [switch]$SkipGhAuth,
        [switch]$SkipGitRemote
    )

    Initialize-AgenticGitShellEnvironment -Quiet | Out-Null

    if (-not $SkipGhAuth) {
        Write-Host "Checking GitHub CLI auth..."
        Invoke-AgenticNativeCommand -FilePath $GhCommand -ArgumentList @("auth", "status")
    }

    if (-not $SkipGitRemote) {
        Write-Host "Checking Git remote access: $RemoteName"
        Invoke-AgenticNativeCommand -FilePath "git" -ArgumentList @("ls-remote", $RemoteName, "HEAD")
    }
}

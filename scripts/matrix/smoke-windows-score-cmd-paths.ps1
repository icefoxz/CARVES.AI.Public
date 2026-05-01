[CmdletBinding()]
param(
    [string] $ZipPath = "",
    [string] $WorkRoot = "",
    [string] $Configuration = "Release",
    [string] $BuildLabel = "local-path-smoke",
    [switch] $ForceBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string] $PathValue)
    return [System.IO.Path]::GetFullPath($PathValue)
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string] $FileName,
        [Parameter(Mandatory = $true)][string[]] $Arguments,
        [Parameter(Mandatory = $true)][string] $WorkingDirectory
    )

    $output = & $FileName @Arguments 2>&1
    $lastExitCodeVariable = Get-Variable -Name LASTEXITCODE -Scope Global -ErrorAction SilentlyContinue
    $exitCode = if ($null -eq $lastExitCodeVariable) { 0 } else { [int] $lastExitCodeVariable.Value }
    if ($exitCode -ne 0) {
        $output | ForEach-Object { Write-Error $_ }
        throw "Command failed with exit code $exitCode`: $FileName $($Arguments -join ' ')"
    }

    return $output
}

function Convert-ToSmokeRelativePath {
    param(
        [Parameter(Mandatory = $true)][string] $RootPath,
        [Parameter(Mandatory = $true)][string] $PathValue
    )

    $fullPath = Resolve-FullPath $PathValue
    if (-not $fullPath.StartsWith($RootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        return "<external-path-redacted>"
    }

    return [System.IO.Path]::GetRelativePath($RootPath, $fullPath).Replace('\', '/')
}

function Convert-ToRedactedSmokeMessage {
    param(
        [Parameter(Mandatory = $true)][string] $Message,
        [Parameter(Mandatory = $true)][string] $WorkRootPath,
        [Parameter(Mandatory = $true)][string] $RepoRootPath
    )

    $redacted = $Message
    foreach ($item in @(
        @{ value = $WorkRootPath; replacement = "<redacted-work-root>" },
        @{ value = $RepoRootPath; replacement = "<redacted-repo-root>" }
    )) {
        if (-not [string]::IsNullOrWhiteSpace($item.value)) {
            $redacted = [System.Text.RegularExpressions.Regex]::Replace(
                $redacted,
                [System.Text.RegularExpressions.Regex]::Escape($item.value),
                $item.replacement,
                [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        }
    }

    return $redacted
}

function Write-SmokeSummary {
    param(
        [Parameter(Mandatory = $true)][string] $SummaryPath,
        [Parameter(Mandatory = $true)][string] $WorkRootPath,
        [Parameter(Mandatory = $true)][string] $ReleaseRoot,
        [Parameter(Mandatory = $true)][string] $ZipPathValue,
        [Parameter(Mandatory = $true)][string] $BuildLabel,
        [Parameter(Mandatory = $true)][string] $Status,
        [string] $FailureMessage = ""
    )

    New-Item -ItemType Directory -Path (Split-Path -Parent $SummaryPath) -Force | Out-Null
    $summary = [ordered]@{
        schema_version = "carves-windows-scorecmd-path-smoke-summary.v0"
        status = $Status
        smoke_kind = "windows_scorecmd_path"
        build_label = $BuildLabel
        work_root = "<redacted-work-root>"
        work_root_absolute_path_redacted = $true
        release_root = Convert-ToSmokeRelativePath -RootPath $WorkRootPath -PathValue $ReleaseRoot
        zip_path = Convert-ToSmokeRelativePath -RootPath $WorkRootPath -PathValue $ZipPathValue
        success_results_root = Convert-ToSmokeRelativePath -RootPath $WorkRootPath -PathValue (Join-Path $WorkRootPath "fresh extracted success/results")
        missing_scorer_results_root = Convert-ToSmokeRelativePath -RootPath $WorkRootPath -PathValue (Join-Path $WorkRootPath "fresh extracted missing scorer/results")
        path_assertions = [ordered]@{
            work_root_contains_space = $WorkRootPath.Contains(" ", [System.StringComparison]::Ordinal)
            work_root_contains_non_ascii_marker = $WorkRootPath.Contains("路径", [System.StringComparison]::Ordinal)
            non_ascii_marker = "路径"
        }
        local_only = $true
        server_submission = $false
        certification = $false
        leaderboard_eligible = $false
        non_claims = @(
            "not_hosted_verification",
            "not_package_signing",
            "not_public_download_hosting",
            "not_certification",
            "not_leaderboard_eligibility",
            "not_clean_machine_certification"
        )
        failure = $null
    }

    if ($Status -ne "passed") {
        $summary.failure = [ordered]@{
            stage = "windows_scorecmd_path_smoke"
            message = $FailureMessage
        }
    }

    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $SummaryPath -Encoding UTF8
}

if (-not [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
    throw "Windows SCORE.cmd path smoke must run on Windows."
}

$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = Resolve-FullPath (Join-Path $scriptRoot "../..")
$scoreCmdSmoke = Join-Path $scriptRoot "smoke-windows-score-cmd.ps1"
if (-not (Test-Path -LiteralPath $scoreCmdSmoke -PathType Leaf)) {
    throw "SCORE.cmd clean smoke script missing: $scoreCmdSmoke"
}

if ([string]::IsNullOrWhiteSpace($WorkRoot)) {
    $WorkRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("CARVES Score Cmd Path Smoke 路径 " + [System.Guid]::NewGuid().ToString("N"))
}

$workRootPath = Resolve-FullPath $WorkRoot
$releaseRoot = Join-Path $workRootPath "release output 路径"
$summaryPath = Join-Path $workRootPath "windows-scorecmd-path-smoke-summary.json"
$zipPathValue = if ([string]::IsNullOrWhiteSpace($ZipPath)) {
    Join-Path $releaseRoot "carves-agent-trial-pack-win-x64.zip"
}
else {
    Resolve-FullPath $ZipPath
}
$smokeArgs = @(
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    $scoreCmdSmoke,
    "-WorkRoot",
    $workRootPath,
    "-BuildOutputRoot",
    $releaseRoot,
    "-Configuration",
    $Configuration,
    "-BuildLabel",
    $BuildLabel
)

if (-not [string]::IsNullOrWhiteSpace($ZipPath)) {
    $smokeArgs += @("-ZipPath", (Resolve-FullPath $ZipPath))
}
if ($ForceBuild) {
    $smokeArgs += "-ForceBuild"
}

try {
    Invoke-Checked -FileName "pwsh.exe" -Arguments $smokeArgs -WorkingDirectory $repoRoot | Out-Null

    if (-not $workRootPath.Contains(" ", [System.StringComparison]::Ordinal)) {
        throw "Path smoke work root must contain a space: $workRootPath"
    }
    if (-not $workRootPath.Contains("路径", [System.StringComparison]::Ordinal)) {
        throw "Path smoke work root must contain non-ASCII path text: $workRootPath"
    }

    Write-SmokeSummary `
        -SummaryPath $summaryPath `
        -WorkRootPath $workRootPath `
        -ReleaseRoot $releaseRoot `
        -ZipPathValue $zipPathValue `
        -BuildLabel $BuildLabel `
        -Status "passed"

    Write-Host "Windows SCORE.cmd path smoke passed."
    Write-Host "Path smoke root: $workRootPath"
    Write-Host "Path smoke summary: $summaryPath"
}
catch {
    $message = Convert-ToRedactedSmokeMessage `
        -Message $_.Exception.Message `
        -WorkRootPath $workRootPath `
        -RepoRootPath $repoRoot
    try {
        Write-SmokeSummary `
            -SummaryPath $summaryPath `
            -WorkRootPath $workRootPath `
            -ReleaseRoot $releaseRoot `
            -ZipPathValue $zipPathValue `
            -BuildLabel $BuildLabel `
            -Status "failed" `
            -FailureMessage $message
        Write-Host "Path smoke failure summary: $summaryPath"
    }
    catch {
        Write-Warning "Failed to write path smoke failure summary: $($_.Exception.Message)"
    }
    throw
}

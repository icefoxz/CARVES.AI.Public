[CmdletBinding()]
param(
    [string]$RuntimeRoot = "",
    [string]$EvidencePath = "",
    [string]$OutputDirectory = "",
    [string]$Configuration = "Release",
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string]$PathValue)
    return [System.IO.Path]::GetFullPath($PathValue)
}

function Convert-ToRepoRelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$PathValue
    )

    $rootUri = [System.Uri]::new((Resolve-FullPath $Root).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar)
    $pathUri = [System.Uri]::new((Resolve-FullPath $PathValue))
    return [System.Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString()).Replace("\", "/")
}

function Join-ProcessArguments {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    return ($Arguments | ForEach-Object {
        if ($_ -match '[\s"]') {
            '"' + ($_.Replace('"', '\"')) + '"'
        }
        else {
            $_
        }
    }) -join ' '
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string]$FileName,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FileName
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.UseShellExecute = $false
    if ($null -ne $startInfo.GetType().GetProperty("ArgumentList")) {
        foreach ($argument in $Arguments) {
            $startInfo.ArgumentList.Add($argument)
        }
    }
    else {
        $startInfo.Arguments = Join-ProcessArguments -Arguments $Arguments
    }

    $process = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) {
        throw "Failed to start command: $FileName"
    }

    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    $command = "$FileName $($Arguments -join ' ')"
    if ($process.ExitCode -ne 0) {
        throw "Command failed with exit code $($process.ExitCode): $command`nSTDOUT:`n$stdout`nSTDERR:`n$stderr"
    }

    return [pscustomobject]@{
        command = $command
        exit_code = $process.ExitCode
        stdout = $stdout
        stderr = $stderr
    }
}

if ([string]::IsNullOrWhiteSpace($RuntimeRoot)) {
    $RuntimeRoot = Resolve-FullPath (Join-Path $PSScriptRoot "../..")
}
else {
    $RuntimeRoot = Resolve-FullPath $RuntimeRoot
}

if ([string]::IsNullOrWhiteSpace($EvidencePath)) {
    $EvidencePath = Join-Path $RuntimeRoot "docs/shield/examples/shield-evidence-standard.example.json"
}
elseif (-not [System.IO.Path]::IsPathRooted($EvidencePath)) {
    $EvidencePath = Join-Path $RuntimeRoot $EvidencePath
}
$EvidencePath = Resolve-FullPath $EvidencePath

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $RuntimeRoot "artifacts/shield"
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $RuntimeRoot $OutputDirectory
}
$OutputDirectory = Resolve-FullPath $OutputDirectory
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$cliProject = Join-Path $RuntimeRoot "src/CARVES.Shield.Cli/Carves.Shield.Cli.csproj"
$shieldCommandName = "carves-shield"

$build = $null
if (-not $SkipBuild) {
    $build = Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("build", $cliProject, "--configuration", $Configuration, "--no-restore") `
        -WorkingDirectory $RuntimeRoot
}

$evaluateJsonPath = Join-Path $OutputDirectory "shield-evaluate.json"
$badgeJsonPath = Join-Path $OutputDirectory "shield-badge.json"
$badgeSvgPath = Join-Path $OutputDirectory "shield-badge.svg"
$proofJsonPath = Join-Path $OutputDirectory "shield-github-actions-proof.json"

$evaluate = Invoke-Checked `
    -FileName "dotnet" `
    -Arguments @(
        "run",
        "--project",
        $cliProject,
        "--configuration",
        $Configuration,
        "--no-build",
        "--",
        "evaluate",
        $EvidencePath,
        "--json",
        "--output",
        "combined"
    ) `
    -WorkingDirectory $RuntimeRoot
$evaluateJson = ($evaluate.stdout | Out-String).Trim()
$evaluateResult = $evaluateJson | ConvertFrom-Json
Set-Content -Path $evaluateJsonPath -Value $evaluateJson -Encoding UTF8

$badge = Invoke-Checked `
    -FileName "dotnet" `
    -Arguments @(
        "run",
        "--project",
        $cliProject,
        "--configuration",
        $Configuration,
        "--no-build",
        "--",
        "badge",
        $EvidencePath,
        "--json",
        "--output",
        $badgeSvgPath
    ) `
    -WorkingDirectory $RuntimeRoot
$badgeJson = ($badge.stdout | Out-String).Trim()
$badgeResult = $badgeJson | ConvertFrom-Json
Set-Content -Path $badgeJsonPath -Value $badgeJson -Encoding UTF8

if ($evaluateResult.status -ne "ok") {
    throw "Shield evaluate did not pass: $($evaluateResult.status)"
}

if ($badgeResult.status -ne "ok") {
    throw "Shield badge did not pass: $($badgeResult.status)"
}

if ([bool]$badgeResult.certification) {
    throw "Shield badge proof must not claim certification."
}

if (-not (Test-Path $badgeSvgPath)) {
    throw "Shield badge SVG was not written: $badgeSvgPath"
}

$proof = [pscustomobject]@{
    schema_version = "shield-github-actions-proof.v0"
    proof = "shield_github_actions_self_check"
    configuration = $Configuration
    ci_safe = [pscustomobject]@{
        provider_secrets_required = $false
        hosted_api_required = $false
        network_calls_required = $false
        source_upload_required = $false
        raw_diff_upload_required = $false
        prompt_upload_required = $false
        secret_upload_required = $false
        credential_upload_required = $false
        public_directory_required = $false
        certification_claimed = $false
    }
    inputs = [pscustomobject]@{
        evidence_path = Convert-ToRepoRelativePath -Root $RuntimeRoot -PathValue $EvidencePath
        evidence_schema_version = $evaluateResult.evidence_schema_version
    }
    tool = [pscustomobject]@{
        command_name = $shieldCommandName
        project = Convert-ToRepoRelativePath -Root $RuntimeRoot -PathValue $cliProject
    }
    commands = [pscustomobject]@{
        build = if ($null -eq $build) { $null } else { [pscustomobject]@{ command = $build.command; exit_code = $build.exit_code } }
        evaluate = [pscustomobject]@{ command = $evaluate.command; exit_code = $evaluate.exit_code }
        badge = [pscustomobject]@{ command = $badge.command; exit_code = $badge.exit_code }
    }
    outputs = [pscustomobject]@{
        evaluate_json = Convert-ToRepoRelativePath -Root $RuntimeRoot -PathValue $evaluateJsonPath
        badge_json = Convert-ToRepoRelativePath -Root $RuntimeRoot -PathValue $badgeJsonPath
        badge_svg = Convert-ToRepoRelativePath -Root $RuntimeRoot -PathValue $badgeSvgPath
        proof_json = Convert-ToRepoRelativePath -Root $RuntimeRoot -PathValue $proofJsonPath
    }
    result = [pscustomobject]@{
        status = $evaluateResult.status
        standard_label = $evaluateResult.standard.label
        standard_compact = $badgeResult.badge.standard_compact
        lite_score = [int]$evaluateResult.lite.score
        lite_band = $evaluateResult.lite.band
        badge_color = $badgeResult.badge.color_name
        self_check = [bool]$badgeResult.self_check
        certification = [bool]$badgeResult.certification
    }
    verdict = "passed"
}

$proofJson = $proof | ConvertTo-Json -Depth 16
Set-Content -Path $proofJsonPath -Value $proofJson -Encoding UTF8
$proofJson

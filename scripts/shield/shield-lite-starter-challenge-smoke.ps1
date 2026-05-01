[CmdletBinding()]
param(
    [string]$RuntimeRoot = "",
    [string]$Configuration = "Release",
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string]$PathValue)
    return [System.IO.Path]::GetFullPath($PathValue)
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

$cliProject = Join-Path $RuntimeRoot "src/CARVES.Shield.Cli/Carves.Shield.Cli.csproj"
$starterPack = Join-Path $RuntimeRoot "docs/shield/examples/shield-lite-starter-challenge-pack.example.json"

if (-not (Test-Path -LiteralPath $starterPack -PathType Leaf)) {
    throw "Starter challenge pack not found: $starterPack"
}

if (-not $SkipBuild) {
    Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("build", $cliProject, "--configuration", $Configuration, "--no-restore") `
        -WorkingDirectory $RuntimeRoot | Out-Null
}

$challenge = Invoke-Checked `
    -FileName "dotnet" `
    -Arguments @(
        "run",
        "--project",
        $cliProject,
        "--configuration",
        $Configuration,
        "--no-build",
        "--",
        "challenge",
        $starterPack,
        "--json"
    ) `
    -WorkingDirectory $RuntimeRoot

$result = $challenge.stdout | ConvertFrom-Json
if ($result.schema_version -ne "shield-lite-challenge-result.v0") {
    throw "Unexpected schema_version: $($result.schema_version)"
}

if ($result.status -ne "passed") {
    throw "Starter challenge pack did not pass: $($result.status)"
}

if ($result.case_count -lt 10) {
    throw "Starter challenge pack must expose at least 10 cases; found $($result.case_count)"
}

if ($result.passed_count -ne $result.case_count -or $result.failed_count -ne 0) {
    throw "Starter challenge pass counts are inconsistent."
}

if ($result.certification -ne $false) {
    throw "Starter challenge result must keep certification=false."
}

if ($result.summary_label -ne "local challenge result, not certified safe") {
    throw "Starter challenge result lost the local challenge summary label."
}

$failedCases = @($result.results | Where-Object { $_.passed -ne $true })
if ($failedCases.Count -ne 0) {
    throw "Starter challenge contains failed case results."
}

[pscustomobject]@{
    schema_version = $result.schema_version
    status = $result.status
    case_count = $result.case_count
    passed_count = $result.passed_count
    summary_label = $result.summary_label
    certification = $result.certification
} | ConvertTo-Json -Depth 4

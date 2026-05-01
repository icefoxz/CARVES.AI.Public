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
    foreach ($argument in $Arguments) {
        $startInfo.ArgumentList.Add($argument)
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

function Invoke-PowerShellJsonScript {
    param(
        [Parameter(Mandatory = $true)][string]$ScriptPath,
        [Parameter(Mandatory = $true)][hashtable]$Arguments
    )

    $jsonText = & $ScriptPath @Arguments
    return ($jsonText | Out-String) | ConvertFrom-Json
}

if ([string]::IsNullOrWhiteSpace($RuntimeRoot)) {
    $RuntimeRoot = Resolve-FullPath (Join-Path $PSScriptRoot "../..")
}
else {
    $RuntimeRoot = Resolve-FullPath $RuntimeRoot
}

$build = $null
if (-not $SkipBuild) {
    $build = Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("build", (Join-Path $RuntimeRoot "CARVES.Runtime.sln"), "--configuration", $Configuration) `
        -WorkingDirectory $RuntimeRoot
}

$applicationFilter = "GuardPolicyEvaluatorTests|GuardDiffAdapterTests|GuardDecisionReadServiceTests|GuardRunDecisionServiceTests|AlphaGuardTrustBasisCoverageAuditTests|AlphaGuardReleaseCheckpointTests"
$integrationFilter = "GuardCheckCliTests|CliDistributionClosureTests"

$applicationTests = Invoke-Checked `
    -FileName "dotnet" `
    -Arguments @(
        "test",
        (Join-Path $RuntimeRoot "tests/Carves.Runtime.Application.Tests/Carves.Runtime.Application.Tests.csproj"),
        "--no-build",
        "--configuration",
        $Configuration,
        "--filter",
        $applicationFilter
    ) `
    -WorkingDirectory $RuntimeRoot

$integrationTests = Invoke-Checked `
    -FileName "dotnet" `
    -Arguments @(
        "test",
        (Join-Path $RuntimeRoot "tests/Carves.Runtime.IntegrationTests/Carves.Runtime.IntegrationTests.csproj"),
        "--no-build",
        "--configuration",
        $Configuration,
        "--filter",
        $integrationFilter
    ) `
    -WorkingDirectory $RuntimeRoot

$packagedSmoke = Invoke-PowerShellJsonScript `
    -ScriptPath (Join-Path $RuntimeRoot "scripts/beta/guard-packaged-install-smoke.ps1") `
    -Arguments @{
        RuntimeRoot = $RuntimeRoot
    }

$pilotMatrix = Invoke-PowerShellJsonScript `
    -ScriptPath (Join-Path $RuntimeRoot "scripts/beta/guard-external-pilot-matrix.ps1") `
    -Arguments @{
        RuntimeRoot = $RuntimeRoot
        Configuration = $Configuration
        SkipBuild = $true
    }

[pscustomobject]@{
    schema_version = "beta-guard-proof-lane.v1"
    lane = "guard_beta_readiness"
    configuration = $Configuration
    ci_safe = [pscustomobject]@{
        provider_secrets_required = $false
        remote_package_publication_required = $false
        live_worker_tests_included = $false
    }
    steps = [pscustomobject]@{
        build = if ($null -eq $build) { $null } else { [pscustomobject]@{ command = $build.command; exit_code = $build.exit_code } }
        application_tests = [pscustomobject]@{
            command = "dotnet test tests/Carves.Runtime.Application.Tests --filter $applicationFilter"
            exit_code = $applicationTests.exit_code
            filter = $applicationFilter
        }
        integration_tests = [pscustomobject]@{
            command = "dotnet test tests/Carves.Runtime.IntegrationTests --filter $integrationFilter"
            exit_code = $integrationTests.exit_code
            filter = $integrationFilter
        }
        packaged_install_smoke = [pscustomobject]@{
            smoke = $packagedSmoke.smoke
            version = $packagedSmoke.version
            remote_registry_published = [bool]$packagedSmoke.distribution.remote_registry_published
            target_requires_task_truth = [bool]$packagedSmoke.target_truth.requires_carves_task_truth
            allow_decision = $packagedSmoke.commands.guard_check_allow.decision
            block_decision = $packagedSmoke.commands.guard_check_block.decision
        }
        external_pilot_matrix = [pscustomobject]@{
            pilot = $pilotMatrix.pilot
            repository_count = $pilotMatrix.aggregate.repository_count
            scenario_count = $pilotMatrix.aggregate.scenario_count
            allow_count = $pilotMatrix.aggregate.allow_count
            block_count = $pilotMatrix.aggregate.block_count
            readback_sets = $pilotMatrix.aggregate.readback_sets
            pilot_discovered_block_level_issue_count = $pilotMatrix.aggregate.pilot_discovered_block_level_issue_count
        }
    }
    verdict = "passed"
} | ConvertTo-Json -Depth 16

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

$coreFilter = "FullyQualifiedName~HostStart_DeploysResidentHostOutsideSourceBuildOutput|FullyQualifiedName~HostEnsureJson_FailsClosedWhenAliveStaleDescriptorExists|FullyQualifiedName~HostEnsureJson_FailsClosedWhenStartupLockIsHeld|FullyQualifiedName~HostEnsureJson_ReconcilesHealthyExistingGenerationWhenActiveDescriptorIsStale|FullyQualifiedName~HostStatusJson_ProjectsHealthyWithPointerRepairWhenActiveDescriptorIsRepaired|FullyQualifiedName~HostHonesty_ProjectsConsistentConflictAcrossHostStatusDoctorAndPilotStatus|FullyQualifiedName~Doctor_ProjectsHealthyWithPointerRepairWhenDoctorFirstReconcilesStalePointer|FullyQualifiedName~PilotStatusApi_ProjectsHealthyWithPointerRepairWhenPilotStatusFirstReconcilesStalePointer|FullyQualifiedName~HostReconcileJson_ReplaceStale_ReplacesConflictingGenerationAndStartsFreshHost|FullyQualifiedName~HostReconcileJson_ReplaceStale_DoesNotReplaceWhenHealthyGenerationCanBeReconciled|FullyQualifiedName~HostReconcileJson_ReplaceStale_DoesNotStartFreshHostWhenNoStaleConflictExists|FullyQualifiedName~HostReconcileJson_ReplaceStale_FailsClosedWhenConflictingProcessCannotBeTerminated|FullyQualifiedName~Workbench_CommandFallsBackWithConflictHonestyWhenResidentHostSessionConflicts|FullyQualifiedName~Workbench_CommandTracksCurrentHostAfterResidentRestart|FullyQualifiedName~HostStatus_RecoversTransientHandshakeAndReplacesPersistedStaleSnapshot"
$entryFilter = "FullyQualifiedName~Doctor_WhenHostSessionConflictExists_ProjectsConflictAndReconcileNextAction|FullyQualifiedName~Init_WhenHostSessionConflictExists_ProjectsJsonReconcileNextAction|FullyQualifiedName~Attach_WhenHostSessionConflictExists_ShowsFriendlyReconcileGuidance|FullyQualifiedName~Run_WithHostTransportWhenHostSessionConflictExists_ShowsFriendlyReconcileGuidance|FullyQualifiedName~Status_WhenHostSessionConflictExists_ShowsFriendlyReconcileGuidance|FullyQualifiedName~HostEnsureJson_StartsOrValidatesResidentHost|FullyQualifiedName~ColdLauncher_StatusConflictFallsBackWithReconcileGuidance|FullyQualifiedName~PilotStatusApi_ProjectsHostHonestyWhenResidentHostIsNotRunning|FullyQualifiedName~GuardRunJson_WithHostSessionConflict_ProjectsReconcileNextAction"

$coreTests = Invoke-Checked `
    -FileName "dotnet" `
    -Arguments @(
        "test",
        (Join-Path $RuntimeRoot "tests/Carves.Runtime.IntegrationTests/Carves.Runtime.IntegrationTests.csproj"),
        "--no-build",
        "--configuration",
        $Configuration,
        "-m:1",
        "--filter",
        $coreFilter
    ) `
    -WorkingDirectory $RuntimeRoot

$entryTests = Invoke-Checked `
    -FileName "dotnet" `
    -Arguments @(
        "test",
        (Join-Path $RuntimeRoot "tests/Carves.Runtime.IntegrationTests/Carves.Runtime.IntegrationTests.csproj"),
        "--no-build",
        "--configuration",
        $Configuration,
        "-m:1",
        "--filter",
        $entryFilter
    ) `
    -WorkingDirectory $RuntimeRoot

[pscustomobject]@{
    schema_version = "beta-host-lifecycle-proof-lane.v1"
    lane = "resident_host_lifecycle_release_gate"
    configuration = $Configuration
    ci_safe = [pscustomobject]@{
        provider_secrets_required = $false
        remote_package_publication_required = $false
        live_worker_tests_included = $false
        serial_execution_required = $true
    }
    steps = [pscustomobject]@{
        build = if ($null -eq $build) { $null } else { [pscustomobject]@{ command = $build.command; exit_code = $build.exit_code } }
        host_lifecycle_core = [pscustomobject]@{
            command = "dotnet test tests/Carves.Runtime.IntegrationTests/Carves.Runtime.IntegrationTests.csproj -m:1 --filter ""$coreFilter"""
            exit_code = $coreTests.exit_code
            filter = $coreFilter
        }
        default_entry_and_cold_fallback = [pscustomobject]@{
            command = "dotnet test tests/Carves.Runtime.IntegrationTests/Carves.Runtime.IntegrationTests.csproj -m:1 --filter ""$entryFilter"""
            exit_code = $entryTests.exit_code
            filter = $entryFilter
        }
    }
    non_claims = @(
        "This lane does not claim full-suite host perfection.",
        "This lane does not include provider-backed execution or dashboard UX proof beyond bounded honesty/readiness checks.",
        "This lane does not replace broader release gates outside the resident host lifecycle line."
    )
    verdict = "passed"
} | ConvertTo-Json -Depth 16

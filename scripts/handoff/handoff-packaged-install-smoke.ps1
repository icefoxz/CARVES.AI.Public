[CmdletBinding()]
param(
    [string]$Version = "0.1.0-alpha.1",
    [string]$RuntimeRoot = "",
    [string]$WorkRoot = "",
    [switch]$Keep
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string]$PathValue)
    return [System.IO.Path]::GetFullPath($PathValue)
}

function Assert-PathInside {
    param(
        [Parameter(Mandatory = $true)][string]$PathValue,
        [Parameter(Mandatory = $true)][string]$RootValue
    )

    $resolvedPath = Resolve-FullPath $PathValue
    $resolvedRoot = Resolve-FullPath $RootValue
    if (-not $resolvedRoot.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $resolvedRoot += [System.IO.Path]::DirectorySeparatorChar
    }

    if (-not $resolvedPath.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe path outside work root. Path='$resolvedPath' Root='$resolvedRoot'."
    }
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string]$FileName,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [int[]]$AllowedExitCodes = @(0)
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
    if ($process.ExitCode -notin $AllowedExitCodes) {
        throw "Command failed with exit code $($process.ExitCode): $FileName $($Arguments -join ' ')`nSTDOUT:`n$stdout`nSTDERR:`n$stderr"
    }

    return [pscustomobject]@{
        exit_code = $process.ExitCode
        stdout = $stdout
        stderr = $stderr
        command = "$FileName $($Arguments -join ' ')"
    }
}

function Invoke-Handoff {
    param(
        [Parameter(Mandatory = $true)][string]$HandoffCommand,
        [Parameter(Mandatory = $true)][string]$TargetRoot,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [int[]]$AllowedExitCodes = @(0)
    )

    $commandArguments = @("--repo-root", $TargetRoot) + $Arguments
    $result = Invoke-Checked -FileName $HandoffCommand -Arguments $commandArguments -WorkingDirectory $TargetRoot -AllowedExitCodes $AllowedExitCodes
    $json = $null
    if (-not [string]::IsNullOrWhiteSpace($result.stdout) -and $result.stdout.TrimStart().StartsWith("{", [System.StringComparison]::Ordinal)) {
        $json = $result.stdout | ConvertFrom-Json
    }

    return [pscustomobject]@{
        exit_code = $result.exit_code
        stdout = $result.stdout
        stderr = $result.stderr
        json = $json
        command = "carves-handoff $($commandArguments -join ' ')"
    }
}

if ([string]::IsNullOrWhiteSpace($RuntimeRoot)) {
    $RuntimeRoot = Resolve-FullPath (Join-Path $PSScriptRoot "../..")
}
else {
    $RuntimeRoot = Resolve-FullPath $RuntimeRoot
}

if ([string]::IsNullOrWhiteSpace($WorkRoot)) {
    $WorkRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("carves-handoff-packaged-install-" + [Guid]::NewGuid().ToString("N"))
}
$WorkRoot = Resolve-FullPath $WorkRoot

$packageSource = Join-Path $WorkRoot "packages"
$toolPath = Join-Path $WorkRoot "tool"
$targetRoot = Join-Path $WorkRoot "external-target"
New-Item -ItemType Directory -Force $packageSource, $toolPath, $targetRoot | Out-Null

try {
    $coreProject = Join-Path $RuntimeRoot "src/CARVES.Handoff.Core/Carves.Handoff.Core.csproj"
    $cliProject = Join-Path $RuntimeRoot "src/CARVES.Handoff.Cli/Carves.Handoff.Cli.csproj"
    $packCore = Invoke-Checked -FileName "dotnet" -Arguments @("pack", $coreProject, "--configuration", "Release", "--output", $packageSource, "/p:Version=$Version") -WorkingDirectory $RuntimeRoot
    $packCli = Invoke-Checked -FileName "dotnet" -Arguments @("pack", $cliProject, "--configuration", "Release", "--output", $packageSource, "/p:Version=$Version") -WorkingDirectory $RuntimeRoot

    $package = Get-ChildItem -LiteralPath $packageSource -Filter "CARVES.Handoff.Cli.$Version.nupkg" | Select-Object -First 1
    if ($null -eq $package) {
        throw "Expected CARVES.Handoff.Cli.$Version.nupkg in $packageSource."
    }

    $install = Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("tool", "install", "--tool-path", $toolPath, "--add-source", $packageSource, "CARVES.Handoff.Cli", "--version", $Version, "--ignore-failed-sources") `
        -WorkingDirectory $packageSource

    $handoffCommand = Join-Path $toolPath "carves-handoff.exe"
    if (-not (Test-Path -LiteralPath $handoffCommand)) {
        $handoffCommand = Join-Path $toolPath "carves-handoff"
    }

    if (-not (Test-Path -LiteralPath $handoffCommand)) {
        throw "Installed carves-handoff command was not found in $toolPath."
    }

    $help = Invoke-Handoff -HandoffCommand $handoffCommand -TargetRoot $targetRoot -Arguments @("help")
    $draft = Invoke-Handoff -HandoffCommand $handoffCommand -TargetRoot $targetRoot -Arguments @("draft", "--json")
    if ($null -eq $draft.json -or $draft.json.packet_path -ne ".ai/handoff/handoff.json") {
        throw "Handoff draft did not write the default packet path."
    }

    $inspect = Invoke-Handoff -HandoffCommand $handoffCommand -TargetRoot $targetRoot -Arguments @("inspect", "--json") -AllowedExitCodes @(1)
    $next = Invoke-Handoff -HandoffCommand $handoffCommand -TargetRoot $targetRoot -Arguments @("next", "--json") -AllowedExitCodes @(1)
    if ($null -eq $inspect.json -or $inspect.json.schema_version -ne "carves-continuity-handoff-inspection.v1") {
        throw "Handoff inspect did not emit inspection JSON."
    }

    if ($null -eq $next.json -or $next.json.schema_version -ne "carves-continuity-handoff-next.v1") {
        throw "Handoff next did not emit projection JSON."
    }

    [pscustomobject]@{
        smoke = "handoff_packaged_install"
        version = $Version
        local_package = $package.FullName
        remote_registry_published = $false
        nuget_org_push_required = $false
        target_repository = $targetRoot
        default_packet_path = ".ai/handoff/handoff.json"
        commands = [pscustomobject]@{
            pack_core = [pscustomobject]@{ command = $packCore.command; exit_code = $packCore.exit_code }
            pack_cli = [pscustomobject]@{ command = $packCli.command; exit_code = $packCli.exit_code }
            install = [pscustomobject]@{ command = $install.command; exit_code = $install.exit_code }
            help = [pscustomobject]@{ command = $help.command; exit_code = $help.exit_code }
            draft = [pscustomobject]@{ command = $draft.command; exit_code = $draft.exit_code; packet_path = $draft.json.packet_path; draft_status = $draft.json.draft_status }
            inspect = [pscustomobject]@{ command = $inspect.command; exit_code = $inspect.exit_code; readiness = $inspect.json.readiness.decision }
            next = [pscustomobject]@{ command = $next.command; exit_code = $next.exit_code; action = $next.json.action }
        }
    } | ConvertTo-Json -Depth 10
}
finally {
    if (-not $Keep -and (Test-Path -LiteralPath $WorkRoot)) {
        Assert-PathInside -PathValue $WorkRoot -RootValue ([System.IO.Path]::GetTempPath())
        Remove-Item -LiteralPath $WorkRoot -Recurse -Force
    }
}

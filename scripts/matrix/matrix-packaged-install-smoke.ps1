[CmdletBinding()]
param(
    [string] $GuardVersion = "0.2.0-beta.1",
    [string] $HandoffVersion = "0.1.0-alpha.1",
    [string] $AuditVersion = "0.1.0-alpha.1",
    [string] $ShieldVersion = "0.1.0-alpha.1",
    [string] $MatrixVersion = "0.2.0-alpha.1",
    [string] $RuntimeRoot = "",
    [string] $WorkRoot = "",
    [string] $ArtifactRoot = "",
    [string] $Configuration = "Release",
    [switch] $Keep
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "matrix-checked-process.ps1")

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string] $PathValue)
    return [System.IO.Path]::GetFullPath($PathValue)
}

function Assert-PathInside {
    param(
        [Parameter(Mandatory = $true)][string] $PathValue,
        [Parameter(Mandatory = $true)][string] $RootValue
    )

    $resolvedPath = Resolve-FullPath $PathValue
    $resolvedRoot = Resolve-FullPath $RootValue
    if (-not $resolvedRoot.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $resolvedRoot += [System.IO.Path]::DirectorySeparatorChar
    }

    if (-not $resolvedPath.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe path outside expected root. Path='$resolvedPath' Root='$resolvedRoot'."
    }
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string] $FileName,
        [Parameter(Mandatory = $true)][string[]] $Arguments,
        [Parameter(Mandatory = $true)][string] $WorkingDirectory,
        [string] $OutputPath = "",
        [int[]] $AllowedExitCodes = @(0)
    )

    $result = Invoke-MatrixCheckedProcess `
        -FileName $FileName `
        -Arguments $Arguments `
        -WorkingDirectory $WorkingDirectory `
        -AllowedExitCodes $AllowedExitCodes

    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        $resolvedOutputPath = Resolve-FullPath $OutputPath
        $outputDirectory = Split-Path -Parent $resolvedOutputPath
        if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
            New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
        }

        $result.stdout | Set-Content -Path $resolvedOutputPath -Encoding UTF8
        if (-not [string]::IsNullOrWhiteSpace($result.stderr)) {
            $result.stderr | Set-Content -Path "$resolvedOutputPath.stderr.txt" -Encoding UTF8
        }
    }

    if (-not $result.passed) {
        $failure = if ($result.timed_out) {
            "Command timed out after $($result.timeout_seconds)s"
        }
        else {
            "Command failed with exit code $($result.exit_code)"
        }
        throw "$failure`: $($result.command)`nSTDOUT:`n$($result.stdout)`nSTDERR:`n$($result.stderr)"
    }

    return $result
}

function Read-JsonStdout {
    param(
        [Parameter(Mandatory = $true)] $CommandResult,
        [Parameter(Mandatory = $true)][string] $StepName
    )

    $text = $CommandResult.stdout.Trim()
    if (-not $text.StartsWith("{", [System.StringComparison]::Ordinal)) {
        throw "$StepName did not emit JSON on stdout. STDOUT:`n$text`nSTDERR:`n$($CommandResult.stderr)"
    }

    return $text | ConvertFrom-Json -Depth 100
}

function Find-ToolCommand {
    param(
        [Parameter(Mandatory = $true)][string] $ToolRoot,
        [Parameter(Mandatory = $true)][string] $CommandName
    )

    $windowsPath = Join-Path $ToolRoot "$CommandName.exe"
    if (Test-Path -LiteralPath $windowsPath) {
        return $windowsPath
    }

    $portablePath = Join-Path $ToolRoot $CommandName
    if (Test-Path -LiteralPath $portablePath) {
        return $portablePath
    }

    throw "Installed command '$CommandName' was not found in $ToolRoot."
}

if ([string]::IsNullOrWhiteSpace($RuntimeRoot)) {
    $RuntimeRoot = Resolve-FullPath (Join-Path $PSScriptRoot "../..")
}
else {
    $RuntimeRoot = Resolve-FullPath $RuntimeRoot
}

$workRootWasDefault = [string]::IsNullOrWhiteSpace($WorkRoot)
if ($workRootWasDefault) {
    $WorkRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("carves-matrix-packaged-" + [Guid]::NewGuid().ToString("N"))
}
$WorkRoot = Resolve-FullPath $WorkRoot

if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) {
    $ArtifactRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("carves-matrix-packaged-artifacts-" + [Guid]::NewGuid().ToString("N"))
}
$ArtifactRoot = Resolve-FullPath $ArtifactRoot

$packageRoot = Join-Path $WorkRoot "packages"
$toolRoot = Join-Path $WorkRoot "tool"
New-Item -ItemType Directory -Force -Path $WorkRoot, $ArtifactRoot, $packageRoot, $toolRoot | Out-Null

try {
    $packResults = @()
    $packResults += Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("pack", (Join-Path $RuntimeRoot "src/CARVES.Guard.Core/Carves.Guard.Core.csproj"), "--configuration", $Configuration, "--output", $packageRoot, "/p:Version=$GuardVersion") `
        -WorkingDirectory $RuntimeRoot `
        -OutputPath (Join-Path $ArtifactRoot "pack-guard-core.log")
    $packResults += Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("pack", (Join-Path $RuntimeRoot "src/CARVES.Guard.Cli/Carves.Guard.Cli.csproj"), "--configuration", $Configuration, "--output", $packageRoot, "/p:Version=$GuardVersion") `
        -WorkingDirectory $RuntimeRoot `
        -OutputPath (Join-Path $ArtifactRoot "pack-guard-cli.log")
    $packResults += Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("pack", (Join-Path $RuntimeRoot "src/CARVES.Handoff.Core/Carves.Handoff.Core.csproj"), "--configuration", $Configuration, "--output", $packageRoot, "/p:Version=$HandoffVersion") `
        -WorkingDirectory $RuntimeRoot `
        -OutputPath (Join-Path $ArtifactRoot "pack-handoff-core.log")
    $packResults += Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("pack", (Join-Path $RuntimeRoot "src/CARVES.Handoff.Cli/Carves.Handoff.Cli.csproj"), "--configuration", $Configuration, "--output", $packageRoot, "/p:Version=$HandoffVersion") `
        -WorkingDirectory $RuntimeRoot `
        -OutputPath (Join-Path $ArtifactRoot "pack-handoff-cli.log")
    $packResults += Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("pack", (Join-Path $RuntimeRoot "src/CARVES.Audit.Core/Carves.Audit.Core.csproj"), "--configuration", $Configuration, "--output", $packageRoot, "/p:Version=$AuditVersion") `
        -WorkingDirectory $RuntimeRoot `
        -OutputPath (Join-Path $ArtifactRoot "pack-audit-core.log")
    $packResults += Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("pack", (Join-Path $RuntimeRoot "src/CARVES.Audit.Cli/Carves.Audit.Cli.csproj"), "--configuration", $Configuration, "--output", $packageRoot, "/p:Version=$AuditVersion") `
        -WorkingDirectory $RuntimeRoot `
        -OutputPath (Join-Path $ArtifactRoot "pack-audit-cli.log")
    $packResults += Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("pack", (Join-Path $RuntimeRoot "src/CARVES.Shield.Core/Carves.Shield.Core.csproj"), "--configuration", $Configuration, "--output", $packageRoot, "/p:Version=$ShieldVersion") `
        -WorkingDirectory $RuntimeRoot `
        -OutputPath (Join-Path $ArtifactRoot "pack-shield-core.log")
    $packResults += Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("pack", (Join-Path $RuntimeRoot "src/CARVES.Shield.Cli/Carves.Shield.Cli.csproj"), "--configuration", $Configuration, "--output", $packageRoot, "/p:Version=$ShieldVersion") `
        -WorkingDirectory $RuntimeRoot `
        -OutputPath (Join-Path $ArtifactRoot "pack-shield-cli.log")
    $packResults += Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("pack", (Join-Path $RuntimeRoot "src/CARVES.Matrix.Core/Carves.Matrix.Core.csproj"), "--configuration", $Configuration, "--output", $packageRoot, "/p:Version=$MatrixVersion") `
        -WorkingDirectory $RuntimeRoot `
        -OutputPath (Join-Path $ArtifactRoot "pack-matrix-core.log")
    $packResults += Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("pack", (Join-Path $RuntimeRoot "src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj"), "--configuration", $Configuration, "--output", $packageRoot, "/p:Version=$MatrixVersion") `
        -WorkingDirectory $RuntimeRoot `
        -OutputPath (Join-Path $ArtifactRoot "pack-matrix-cli.log")

    foreach ($package in @(
        "CARVES.Guard.Cli.$GuardVersion.nupkg",
        "CARVES.Handoff.Cli.$HandoffVersion.nupkg",
        "CARVES.Audit.Cli.$AuditVersion.nupkg",
        "CARVES.Shield.Cli.$ShieldVersion.nupkg",
        "CARVES.Matrix.Cli.$MatrixVersion.nupkg"
    )) {
        if (-not (Test-Path -LiteralPath (Join-Path $packageRoot $package))) {
            throw "Expected local package was not produced: $package"
        }
    }

    $installResults = @()
    $installResults += Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("tool", "install", "--tool-path", $toolRoot, "--add-source", $packageRoot, "CARVES.Guard.Cli", "--version", $GuardVersion, "--ignore-failed-sources") `
        -WorkingDirectory $RuntimeRoot `
        -OutputPath (Join-Path $ArtifactRoot "install-guard-cli.log")
    $installResults += Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("tool", "install", "--tool-path", $toolRoot, "--add-source", $packageRoot, "CARVES.Handoff.Cli", "--version", $HandoffVersion, "--ignore-failed-sources") `
        -WorkingDirectory $RuntimeRoot `
        -OutputPath (Join-Path $ArtifactRoot "install-handoff-cli.log")
    $installResults += Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("tool", "install", "--tool-path", $toolRoot, "--add-source", $packageRoot, "CARVES.Audit.Cli", "--version", $AuditVersion, "--ignore-failed-sources") `
        -WorkingDirectory $RuntimeRoot `
        -OutputPath (Join-Path $ArtifactRoot "install-audit-cli.log")
    $installResults += Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("tool", "install", "--tool-path", $toolRoot, "--add-source", $packageRoot, "CARVES.Shield.Cli", "--version", $ShieldVersion, "--ignore-failed-sources") `
        -WorkingDirectory $RuntimeRoot `
        -OutputPath (Join-Path $ArtifactRoot "install-shield-cli.log")
    $installResults += Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("tool", "install", "--tool-path", $toolRoot, "--add-source", $packageRoot, "CARVES.Matrix.Cli", "--version", $MatrixVersion, "--ignore-failed-sources") `
        -WorkingDirectory $RuntimeRoot `
        -OutputPath (Join-Path $ArtifactRoot "install-matrix-cli.log")

    $guardCommand = Find-ToolCommand -ToolRoot $toolRoot -CommandName "carves-guard"
    $handoffCommand = Find-ToolCommand -ToolRoot $toolRoot -CommandName "carves-handoff"
    $auditCommand = Find-ToolCommand -ToolRoot $toolRoot -CommandName "carves-audit"
    $shieldCommand = Find-ToolCommand -ToolRoot $toolRoot -CommandName "carves-shield"
    $matrixCommand = Find-ToolCommand -ToolRoot $toolRoot -CommandName "carves-matrix"

    $matrixResult = Invoke-Checked `
        -FileName $matrixCommand `
        -Arguments @(
            "e2e",
            "--tool-mode",
            "Installed",
            "--runtime-root",
            $RuntimeRoot,
            "--work-root",
            (Join-Path $WorkRoot "matrix-target"),
            "--artifact-root",
            $ArtifactRoot,
            "--guard-command",
            $guardCommand,
            "--handoff-command",
            $handoffCommand,
            "--audit-command",
            $auditCommand,
            "--shield-command",
            $shieldCommand
        ) `
        -WorkingDirectory $RuntimeRoot `
        -OutputPath (Join-Path $ArtifactRoot "matrix-e2e-output.json")
    $matrixJson = Read-JsonStdout -CommandResult $matrixResult -StepName "matrix e2e installed"

    $summary = [pscustomobject]@{
        smoke = "matrix_packaged_install"
        guard_version = $GuardVersion
        handoff_version = $HandoffVersion
        audit_version = $AuditVersion
        shield_version = $ShieldVersion
        matrix_version = $MatrixVersion
        package_root = "<redacted-local-package-root>"
        tool_root = "<redacted-local-tool-root>"
        artifact_root = "."
        remote_registry_published = $false
        nuget_org_push_required = $false
        installed_commands = [pscustomobject]@{
            carves_guard = "carves-guard"
            carves_handoff = "carves-handoff"
            carves_audit = "carves-audit"
            carves_shield = "carves-shield"
            carves_matrix = "carves-matrix"
        }
        packages = [pscustomobject]@{
            guard = "CARVES.Guard.Cli.$GuardVersion.nupkg"
            handoff = "CARVES.Handoff.Cli.$HandoffVersion.nupkg"
            audit = "CARVES.Audit.Cli.$AuditVersion.nupkg"
            shield = "CARVES.Shield.Cli.$ShieldVersion.nupkg"
            matrix = "CARVES.Matrix.Cli.$MatrixVersion.nupkg"
        }
        matrix = $matrixJson
        privacy = $matrixJson.privacy
        public_claims = $matrixJson.public_claims
        pack_command_count = $packResults.Count
        install_command_count = $installResults.Count
    }

    $summaryJson = $summary | ConvertTo-Json -Depth 100
    $summaryJson | Set-Content -Path (Join-Path $ArtifactRoot "matrix-packaged-summary.json") -Encoding UTF8
    $summaryJson
}
finally {
    if (-not $Keep -and $workRootWasDefault -and (Test-Path -LiteralPath $WorkRoot)) {
        Assert-PathInside -PathValue $WorkRoot -RootValue ([System.IO.Path]::GetTempPath())
        Remove-Item -LiteralPath $WorkRoot -Recurse -Force
    }
}

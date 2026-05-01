[CmdletBinding()]
param(
    [string]$Version = "0.2.0-beta.1",
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

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory
    )

    return Invoke-Checked -FileName "git" -Arguments $Arguments -WorkingDirectory $WorkingDirectory
}

function Invoke-Guard {
    param(
        [Parameter(Mandatory = $true)][string]$GuardCommand,
        [Parameter(Mandatory = $true)][string]$TargetRoot,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [int[]]$AllowedExitCodes = @(0)
    )

    $commandArguments = @("--repo-root", $TargetRoot) + $Arguments
    $result = Invoke-Checked -FileName $GuardCommand -Arguments $commandArguments -WorkingDirectory $TargetRoot -AllowedExitCodes $AllowedExitCodes
    $json = $null
    if (-not [string]::IsNullOrWhiteSpace($result.stdout) -and $result.stdout.TrimStart().StartsWith("{", [System.StringComparison]::Ordinal)) {
        $json = $result.stdout | ConvertFrom-Json
    }

    return [pscustomobject]@{
        exit_code = $result.exit_code
        stdout = $result.stdout
        stderr = $result.stderr
        json = $json
        command = "carves-guard $($commandArguments -join ' ')"
    }
}

function Install-CarvesTool {
    param(
        [Parameter(Mandatory = $true)][string]$PackageSource,
        [Parameter(Mandatory = $true)][string]$ToolPath,
        [Parameter(Mandatory = $true)][string]$Version
    )

    return Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("tool", "install", "--tool-path", $ToolPath, "--add-source", $PackageSource, "CARVES.Guard.Cli", "--version", $Version) `
        -WorkingDirectory $PackageSource
}

if ([string]::IsNullOrWhiteSpace($RuntimeRoot)) {
    $RuntimeRoot = Resolve-FullPath (Join-Path $PSScriptRoot "../..")
}
else {
    $RuntimeRoot = Resolve-FullPath $RuntimeRoot
}

if ([string]::IsNullOrWhiteSpace($WorkRoot)) {
    $WorkRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("carves-guard-packaged-install-" + [Guid]::NewGuid().ToString("N"))
}
$WorkRoot = Resolve-FullPath $WorkRoot

$packageSource = Join-Path $WorkRoot "packages"
$toolPath = Join-Path $WorkRoot "tool"
$targetRoot = Join-Path $WorkRoot "external-target"
New-Item -ItemType Directory -Force $packageSource, $toolPath, $targetRoot | Out-Null

try {
    $projectPath = Join-Path $RuntimeRoot "src/CARVES.Guard.Cli/Carves.Guard.Cli.csproj"
    $pack = Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("pack", $projectPath, "--configuration", "Release", "--output", $packageSource, "/p:Version=$Version") `
        -WorkingDirectory $RuntimeRoot

    $package = Get-ChildItem -LiteralPath $packageSource -Filter "CARVES.Guard.Cli.$Version.nupkg" | Select-Object -First 1
    if ($null -eq $package) {
        throw "Expected CARVES.Guard.Cli.$Version.nupkg in $packageSource."
    }

    $install = Install-CarvesTool -PackageSource $packageSource -ToolPath $toolPath -Version $Version
    $guardCommand = Join-Path $toolPath "carves-guard.exe"
    if (-not (Test-Path -LiteralPath $guardCommand)) {
        $guardCommand = Join-Path $toolPath "carves-guard"
    }

    if (-not (Test-Path -LiteralPath $guardCommand)) {
        throw "Installed carves-guard command was not found in $toolPath."
    }

    Invoke-Git -Arguments @("-c", "init.defaultBranch=main", "init") -WorkingDirectory $targetRoot | Out-Null
    Invoke-Git -Arguments @("config", "user.email", "guard-packaged-smoke@example.invalid") -WorkingDirectory $targetRoot | Out-Null
    Invoke-Git -Arguments @("config", "user.name", "Guard Packaged Smoke") -WorkingDirectory $targetRoot | Out-Null

    New-Item -ItemType Directory -Force (Join-Path $targetRoot "src"), (Join-Path $targetRoot "tests") | Out-Null
    "export const todos = [];" | Set-Content -Encoding UTF8 (Join-Path $targetRoot "src/todo.ts")
    "test('baseline', () => expect(true).toBe(true));" | Set-Content -Encoding UTF8 (Join-Path $targetRoot "tests/todo.test.ts")

    $help = Invoke-Guard -GuardCommand $guardCommand -TargetRoot $targetRoot -Arguments @("help")
    $init = Invoke-Guard -GuardCommand $guardCommand -TargetRoot $targetRoot -Arguments @("init", "--json")
    if ($null -eq $init.json -or $init.json.schema_version -ne "guard-init.v1") {
        throw "guard init did not return guard-init.v1 JSON."
    }

    Invoke-Git -Arguments @("add", ".") -WorkingDirectory $targetRoot | Out-Null
    Invoke-Git -Arguments @("commit", "-m", "baseline") -WorkingDirectory $targetRoot | Out-Null

    Add-Content -Encoding UTF8 (Join-Path $targetRoot "src/todo.ts") "export function countTodos() { return todos.length; }"
    "test('count', () => expect(0).toBe(0));" | Set-Content -Encoding UTF8 (Join-Path $targetRoot "tests/todo-count.test.ts")
    $check = Invoke-Guard -GuardCommand $guardCommand -TargetRoot $targetRoot -Arguments @("check", "--json")
    if ($null -eq $check.json -or $check.json.decision -ne "allow") {
        throw "Expected guard check to allow the starter-policy sample patch."
    }

    [pscustomobject]@{
        smoke = "guard_packaged_install"
        version = $Version
        local_package = $package.FullName
        remote_registry_published = $false
        nuget_org_push_required = $false
        target_repository = $targetRoot
        commands = [pscustomobject]@{
            pack = [pscustomobject]@{ command = $pack.command; exit_code = $pack.exit_code }
            install = [pscustomobject]@{ command = $install.command; exit_code = $install.exit_code }
            help = [pscustomobject]@{ command = $help.command; exit_code = $help.exit_code }
            init = [pscustomobject]@{ command = $init.command; exit_code = $init.exit_code; status = $init.json.status; policy_path = $init.json.policy_path }
            check = [pscustomobject]@{ command = $check.command; exit_code = $check.exit_code; decision = $check.json.decision; run_id = $check.json.run_id }
        }
    } | ConvertTo-Json -Depth 10
}
finally {
    if (-not $Keep -and (Test-Path -LiteralPath $WorkRoot)) {
        Assert-PathInside -PathValue $WorkRoot -RootValue ([System.IO.Path]::GetTempPath())
        Remove-Item -LiteralPath $WorkRoot -Recurse -Force
    }
}

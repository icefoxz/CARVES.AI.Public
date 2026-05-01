[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    return [System.IO.Path]::GetFullPath($PSScriptRoot)
}

function Get-RepoHash([string]$RepoRoot) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($RepoRoot)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha.ComputeHash($bytes)
        return (($hash | ForEach-Object { $_.ToString("x2") }) -join "").Substring(0, 16)
    }
    finally {
        $sha.Dispose()
    }
}

function Copy-DirectoryFiltered([string]$SourceDirectory, [string]$DestinationDirectory) {
    New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null

    foreach ($directory in Get-ChildItem -LiteralPath $SourceDirectory -Directory -Force) {
        if ($directory.Name -in @("bin", "obj", "TestResults", ".git")) {
            continue
        }

        Copy-DirectoryFiltered -SourceDirectory $directory.FullName -DestinationDirectory (Join-Path $DestinationDirectory $directory.Name)
    }

    foreach ($file in Get-ChildItem -LiteralPath $SourceDirectory -File -Force) {
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $DestinationDirectory $file.Name) -Force
    }
}

function Copy-IfPresent([string]$SourcePath, [string]$DestinationPath) {
    if (-not (Test-Path -LiteralPath $SourcePath)) {
        return
    }

    if ((Get-Item -LiteralPath $SourcePath).PSIsContainer) {
        Copy-DirectoryFiltered -SourceDirectory $SourcePath -DestinationDirectory $DestinationPath
        return
    }

    New-Item -ItemType Directory -Path (Split-Path -Parent $DestinationPath) -Force | Out-Null
    Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
}

function Cleanup-StaleBuilds([string]$BuildRoot, [string]$ActiveGenerationRoot) {
    if (-not (Test-Path -LiteralPath $BuildRoot)) {
        return
    }

    $resolvedBuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
    if (-not $resolvedBuildRoot.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $resolvedBuildRoot = $resolvedBuildRoot + [System.IO.Path]::DirectorySeparatorChar
    }
    $directories = @(Get-ChildItem -LiteralPath $BuildRoot -Directory | Sort-Object LastWriteTimeUtc -Descending)
    for ($index = 0; $index -lt $directories.Count; $index++) {
        $directory = $directories[$index]
        if ($directory.FullName -eq $ActiveGenerationRoot) {
            continue
        }

        if ($index -lt 5) {
            continue
        }

        try {
            $resolvedDirectory = [System.IO.Path]::GetFullPath($directory.FullName)
            if (-not $resolvedDirectory.StartsWith($resolvedBuildRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            Remove-Item -LiteralPath $resolvedDirectory -Recurse -Force -ErrorAction Stop
        }
        catch {
            # Leave failed cleanup for the normal runtime cleanup path.
        }
    }
}

function Test-AppControlBlocked($Output) {
    $text = ($Output | Out-String)
    return $text.Contains("0x800711C7") -or $text.Contains("Application Control policy")
}

function Invoke-NativeCaptured([string]$FileName, [object[]]$CommandArguments) {
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & $FileName @CommandArguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    return [pscustomobject]@{
        Output = $output
        ExitCode = $exitCode
    }
}

$repoRoot = Get-RepoRoot
$env:CARVES_RUNTIME_ROOT = $repoRoot
$publishedAssemblyPath = Join-Path (Join-Path $repoRoot "runtime-cli") "carves.dll"
if ([string]::IsNullOrWhiteSpace($env:CARVES_RUNTIME_FORCE_SOURCE) -and (Test-Path -LiteralPath $publishedAssemblyPath -PathType Leaf)) {
    $runInvocation = Invoke-NativeCaptured -FileName "dotnet" -CommandArguments (@($publishedAssemblyPath) + $Arguments)
    $runInvocation.Output | ForEach-Object {
        if ($_ -is [System.Management.Automation.ErrorRecord]) {
            [Console]::Error.WriteLine($_.ToString())
        }
        else {
            [Console]::Out.WriteLine($_)
        }
    }

    exit $runInvocation.ExitCode
}

$runtimeRoot = Join-Path ([System.IO.Path]::GetTempPath()) "carves-runtime-cli"
$repoHash = Get-RepoHash $repoRoot
$buildRoot = Join-Path (Join-Path $runtimeRoot $repoHash) "source-tree-wrapper"
$generationId = "cli-build-" + (Get-Date -Format "yyyyMMddHHmmssfff") + "-" + [Guid]::NewGuid().ToString("N")
$generationRoot = Join-Path $buildRoot $generationId
$stagingRoot = Join-Path $generationRoot "source"

New-Item -ItemType Directory -Path $generationRoot -Force | Out-Null

Copy-IfPresent -SourcePath (Join-Path $repoRoot "src") -DestinationPath (Join-Path $stagingRoot "src")
Copy-IfPresent -SourcePath (Join-Path $repoRoot "templates") -DestinationPath (Join-Path $stagingRoot "templates")
Copy-IfPresent -SourcePath (Join-Path $repoRoot "docs/guides/CARVES_CLI_DISTRIBUTION.md") -DestinationPath (Join-Path $stagingRoot "docs/guides/CARVES_CLI_DISTRIBUTION.md")
Copy-IfPresent -SourcePath (Join-Path $repoRoot "Directory.Build.props") -DestinationPath (Join-Path $stagingRoot "Directory.Build.props")
Copy-IfPresent -SourcePath (Join-Path $repoRoot "Directory.Build.targets") -DestinationPath (Join-Path $stagingRoot "Directory.Build.targets")
Copy-IfPresent -SourcePath (Join-Path $repoRoot "NuGet.Config") -DestinationPath (Join-Path $stagingRoot "NuGet.Config")
Copy-IfPresent -SourcePath (Join-Path $repoRoot "nuget.config") -DestinationPath (Join-Path $stagingRoot "nuget.config")
Copy-IfPresent -SourcePath (Join-Path $repoRoot "global.json") -DestinationPath (Join-Path $stagingRoot "global.json")

$projectPath = Join-Path $stagingRoot "src/CARVES.Runtime.Cli/carves.csproj"
if (-not (Test-Path -LiteralPath $projectPath)) {
    [Console]::Error.WriteLine("CARVES CLI project is missing from staged source tree at '$projectPath'.")
    exit 1
}

$buildArguments = @(
    "build",
    $projectPath,
    "--nologo",
    "--disable-build-servers",
    "-m:1",
    "-p:UseSharedCompilation=false"
)

$buildInvocation = Invoke-NativeCaptured -FileName "dotnet" -CommandArguments $buildArguments
$buildOutput = $buildInvocation.Output
$buildExitCode = $buildInvocation.ExitCode
if ($buildExitCode -ne 0) {
    $buildOutput | ForEach-Object { [Console]::Error.WriteLine($_) }
    exit $buildExitCode
}

$assemblyPath = Join-Path $stagingRoot "src/CARVES.Runtime.Cli/bin/Debug/net10.0/carves.dll"
if (-not (Test-Path -LiteralPath $assemblyPath)) {
    [Console]::Error.WriteLine("CARVES CLI wrapper expected assembly at '$assemblyPath', but it was not produced.")
    exit 1
}

Cleanup-StaleBuilds -BuildRoot $buildRoot -ActiveGenerationRoot $generationRoot

$runArguments = @($assemblyPath) + $Arguments
$runInvocation = Invoke-NativeCaptured -FileName "dotnet" -CommandArguments $runArguments
$runOutput = $runInvocation.Output
$runExitCode = $runInvocation.ExitCode
if ($runExitCode -ne 0 -and (Test-AppControlBlocked -Output $runOutput)) {
    Start-Sleep -Seconds 1
    $runInvocation = Invoke-NativeCaptured -FileName "dotnet" -CommandArguments $runArguments
    $runOutput = $runInvocation.Output
    $runExitCode = $runInvocation.ExitCode
}

if ($runExitCode -ne 0 -and (Test-AppControlBlocked -Output $runOutput)) {
    [Console]::Error.WriteLine("CARVES CLI staged temp assembly was blocked by Windows Application Control; falling back to source project execution.")
    $fallbackProjectPath = Join-Path $repoRoot "src/CARVES.Runtime.Cli/carves.csproj"
    $fallbackArguments = @(
        "run",
        "--project",
        $fallbackProjectPath,
        "--",
        $Arguments
    )
    $runInvocation = Invoke-NativeCaptured -FileName "dotnet" -CommandArguments $fallbackArguments
    $runOutput = $runInvocation.Output
    $runExitCode = $runInvocation.ExitCode
}

$runOutput | ForEach-Object {
    if ($_ -is [System.Management.Automation.ErrorRecord]) {
        [Console]::Error.WriteLine($_.ToString())
    }
    else {
        [Console]::Out.WriteLine($_)
    }
}

exit $runExitCode

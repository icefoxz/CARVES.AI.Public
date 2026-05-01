[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
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

function Ensure-TrailingSeparator([string]$PathValue) {
    if ($PathValue.EndsWith([System.IO.Path]::DirectorySeparatorChar) -or $PathValue.EndsWith([System.IO.Path]::AltDirectorySeparatorChar)) {
        return $PathValue
    }

    return $PathValue + [System.IO.Path]::DirectorySeparatorChar
}

function Cleanup-StaleColdBuilds([string]$ColdCommandsRoot, [string]$ActiveGenerationRoot) {
    if (-not (Test-Path -LiteralPath $ColdCommandsRoot)) {
        return
    }

    $directories = @(Get-ChildItem -LiteralPath $ColdCommandsRoot -Directory | Sort-Object LastWriteTimeUtc -Descending)
    for ($index = 0; $index -lt $directories.Count; $index++) {
        $directory = $directories[$index]
        if ($directory.FullName -eq $ActiveGenerationRoot) {
            continue
        }

        if ($index -lt 5) {
            continue
        }

        try {
            Remove-Item -LiteralPath $directory.FullName -Recurse -Force -ErrorAction Stop
        }
        catch {
            # Leave failed cleanup for the normal runtime cleanup path.
        }
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

$repoRoot = Get-RepoRoot
$runtimeRoot = Join-Path ([System.IO.Path]::GetTempPath()) "carves-runtime-host"
$repoHash = Get-RepoHash $repoRoot
$repoRuntimeRoot = Join-Path $runtimeRoot $repoHash
$coldCommandsRoot = Join-Path $repoRuntimeRoot "cold-commands"
$generationId = "cold-build-" + (Get-Date -Format "yyyyMMddHHmmssfff") + "-" + [Guid]::NewGuid().ToString("N")
$generationRoot = Join-Path $coldCommandsRoot $generationId
$stagingRoot = Join-Path $generationRoot "source"

New-Item -ItemType Directory -Path $generationRoot -Force | Out-Null

Copy-IfPresent -SourcePath (Join-Path $repoRoot "src") -DestinationPath (Join-Path $stagingRoot "src")
Copy-IfPresent -SourcePath (Join-Path $repoRoot "templates") -DestinationPath (Join-Path $stagingRoot "templates")
Copy-IfPresent -SourcePath (Join-Path $repoRoot "Directory.Build.props") -DestinationPath (Join-Path $stagingRoot "Directory.Build.props")
Copy-IfPresent -SourcePath (Join-Path $repoRoot "Directory.Build.targets") -DestinationPath (Join-Path $stagingRoot "Directory.Build.targets")
Copy-IfPresent -SourcePath (Join-Path $repoRoot "NuGet.Config") -DestinationPath (Join-Path $stagingRoot "NuGet.Config")
Copy-IfPresent -SourcePath (Join-Path $repoRoot "nuget.config") -DestinationPath (Join-Path $stagingRoot "nuget.config")
Copy-IfPresent -SourcePath (Join-Path $repoRoot "global.json") -DestinationPath (Join-Path $stagingRoot "global.json")

$projectPath = Join-Path $stagingRoot "src/CARVES.Runtime.Host/Carves.Runtime.Host.csproj"
if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "CARVES host project is missing from staged source tree at '$projectPath'."
}

$buildArguments = @(
    "build",
    $projectPath,
    "--nologo",
    "--disable-build-servers",
    "-m:1",
    "-p:UseSharedCompilation=false"
)

$buildOutput = & dotnet @buildArguments 2>&1
$buildExitCode = $LASTEXITCODE
if ($buildExitCode -ne 0) {
    $buildOutput | ForEach-Object { [Console]::Error.WriteLine($_) }
    exit $buildExitCode
}

$assemblyPath = Join-Path $stagingRoot "src/CARVES.Runtime.Host/bin/Debug/net10.0/Carves.Runtime.Host.dll"
if (-not (Test-Path -LiteralPath $assemblyPath)) {
    throw "Cold command launcher expected host assembly at '$assemblyPath', but it was not produced."
}

Cleanup-StaleColdBuilds -ColdCommandsRoot $coldCommandsRoot -ActiveGenerationRoot $generationRoot

& dotnet $assemblyPath @Arguments
exit $LASTEXITCODE

[CmdletBinding()]
param(
    [string] $OutputRoot = "",
    [string] $ScorerRoot = "",
    [string] $PackageRoot = "",
    [string] $ZipOutput = "",
    [string] $Configuration = "Release",
    [string] $BuildLabel = "local",
    [switch] $Force,
    [switch] $SkipPublish
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

function Assert-FileExists {
    param(
        [Parameter(Mandatory = $true)][string] $PathValue,
        [Parameter(Mandatory = $true)][string] $Label
    )

    if (-not (Test-Path -LiteralPath $PathValue -PathType Leaf)) {
        throw "$Label missing: $PathValue"
    }
}

function Assert-ZipContains {
    param(
        [Parameter(Mandatory = $true)] $Entries,
        [Parameter(Mandatory = $true)][string] $EntryName
    )

    if (-not ($Entries | Where-Object { $_.FullName -eq $EntryName } | Select-Object -First 1)) {
        throw "Playable zip missing entry: $EntryName"
    }
}

function Assert-ZipDoesNotContainPrefix {
    param(
        [Parameter(Mandatory = $true)] $Entries,
        [Parameter(Mandatory = $true)][string] $Prefix
    )

    if ($Entries | Where-Object { $_.FullName.StartsWith($Prefix, [System.StringComparison]::Ordinal) } | Select-Object -First 1) {
        throw "Playable zip contains forbidden entry prefix: $Prefix"
    }
}

function Assert-ZipEntryNamesArePortable {
    param([Parameter(Mandatory = $true)] $Entries)

    foreach ($entry in $Entries) {
        if ($entry.FullName.StartsWith("/", [System.StringComparison]::Ordinal) -or
            $entry.FullName.Contains("\", [System.StringComparison]::Ordinal) -or
            $entry.FullName.Contains(":", [System.StringComparison]::Ordinal)) {
            throw "Playable zip entry is not portable-relative: $($entry.FullName)"
        }
    }
}

function Assert-NoLocalRootLeak {
    param(
        [Parameter(Mandatory = $true)][string] $PathValue,
        [Parameter(Mandatory = $true)][string[]] $LocalRoots
    )

    $text = Get-Content -LiteralPath $PathValue -Raw
    foreach ($root in $LocalRoots) {
        if ([string]::IsNullOrWhiteSpace($root)) {
            continue
        }

        $resolved = Resolve-FullPath $root
        if ($text.Contains($resolved, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Local absolute path leaked into $PathValue`: $resolved"
        }
    }
}

$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = Resolve-FullPath (Join-Path $scriptRoot "../..")
$pwshCommand = if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
    "pwsh.exe"
}
else {
    "pwsh"
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "artifacts/release/windows-playable"
}

$outputRootPath = Resolve-FullPath $OutputRoot
if ([string]::IsNullOrWhiteSpace($ScorerRoot)) {
    $ScorerRoot = Join-Path $outputRootPath "scorer/carves-win-x64"
}

if ([string]::IsNullOrWhiteSpace($PackageRoot)) {
    $PackageRoot = Join-Path $outputRootPath "package/carves-agent-trial-pack-win-x64"
}

if ([string]::IsNullOrWhiteSpace($ZipOutput)) {
    $ZipOutput = Join-Path $outputRootPath "carves-agent-trial-pack-win-x64.zip"
}

$scorerRootPath = Resolve-FullPath $ScorerRoot
$packageRootPath = Resolve-FullPath $PackageRoot
$zipOutputPath = Resolve-FullPath $ZipOutput

if (-not $SkipPublish) {
    $publishArgs = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        (Join-Path $scriptRoot "publish-windows-playable-scorer.ps1"),
        "-OutputRoot",
        $scorerRootPath,
        "-Configuration",
        $Configuration,
        "-BuildLabel",
        $BuildLabel,
        "-SkipRunSmoke"
    )
    if ($Force) {
        $publishArgs += "-Force"
    }

    Invoke-Checked -FileName $pwshCommand -Arguments $publishArgs -WorkingDirectory $repoRoot | Out-Null
}

Assert-FileExists -PathValue (Join-Path $scorerRootPath "carves.exe") -Label "Windows scorer entrypoint"
Assert-FileExists -PathValue (Join-Path $scorerRootPath "scorer-root-manifest.json") -Label "Windows scorer root manifest"

$carvesEntrypoint = Join-Path $scorerRootPath "carves.exe"
$packageArgs = @(
    "test",
    "package",
    "--pack-root",
    (Join-Path $repoRoot "docs/matrix/starter-packs/official-agent-dev-safety-v1-local-mvp"),
    "--output",
    $packageRootPath,
    "--windows-playable",
    "--scorer-root",
    $scorerRootPath,
    "--zip-output",
    $zipOutputPath,
    "--runtime-identifier",
    "win-x64",
    "--build-label",
    $BuildLabel,
    "--json"
)
if ($Force) {
    $packageArgs += "--force"
}

Invoke-Checked -FileName $carvesEntrypoint -Arguments $packageArgs -WorkingDirectory $repoRoot | Out-Null

Assert-FileExists -PathValue $zipOutputPath -Label "Windows playable zip"
Assert-FileExists -PathValue (Join-Path $packageRootPath "tools/carves/carves.exe") -Label "Package-local scorer"
Assert-FileExists -PathValue (Join-Path $packageRootPath "tools/carves/scorer-root-manifest.json") -Label "Packaged scorer root manifest"
Assert-FileExists -PathValue (Join-Path $packageRootPath "tools/carves/scorer-manifest.json") -Label "Packaged scorer manifest"

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($zipOutputPath)
try {
    $entries = @($zip.Entries)
    Assert-ZipEntryNamesArePortable -Entries $entries
    Assert-ZipContains -Entries $entries -EntryName "README-FIRST.md"
    Assert-ZipContains -Entries $entries -EntryName "SCORE.cmd"
    Assert-ZipContains -Entries $entries -EntryName "score.sh"
    Assert-ZipContains -Entries $entries -EntryName ".carves-pack/state.json"
    Assert-ZipContains -Entries $entries -EntryName "agent-workspace/AGENTS.md"
    Assert-ZipContains -Entries $entries -EntryName "tools/carves/carves.exe"
    Assert-ZipContains -Entries $entries -EntryName "tools/carves/scorer-root-manifest.json"
    Assert-ZipContains -Entries $entries -EntryName "tools/carves/scorer-manifest.json"
    Assert-ZipDoesNotContainPrefix -Entries $entries -Prefix "agent-workspace/tools/"
}
finally {
    $zip.Dispose()
}

Assert-NoLocalRootLeak `
    -PathValue (Join-Path $packageRootPath "tools/carves/scorer-root-manifest.json") `
    -LocalRoots @($repoRoot, $outputRootPath, $packageRootPath, $scorerRootPath)
Assert-NoLocalRootLeak `
    -PathValue (Join-Path $packageRootPath "tools/carves/scorer-manifest.json") `
    -LocalRoots @($repoRoot, $outputRootPath, $packageRootPath, $scorerRootPath)

Write-Host "Windows playable package root: $packageRootPath"
Write-Host "Windows playable zip: $zipOutputPath"
Write-Host "Package-local scorer: $(Join-Path $packageRootPath 'tools/carves/carves.exe')"

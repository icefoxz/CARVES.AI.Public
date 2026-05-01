[CmdletBinding()]
param(
    [string] $OutputRoot = "",
    [string] $Configuration = "Release",
    [string] $RuntimeIdentifier = "win-x64",
    [string] $BuildLabel = "local",
    [switch] $Force,
    [switch] $NoRestore,
    [switch] $SkipRunSmoke
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string] $PathValue)
    return [System.IO.Path]::GetFullPath($PathValue)
}

function Test-DirectoryEmpty {
    param([Parameter(Mandatory = $true)][string] $PathValue)
    if (-not (Test-Path -LiteralPath $PathValue -PathType Container)) {
        return $true
    }

    return $null -eq (Get-ChildItem -LiteralPath $PathValue -Force | Select-Object -First 1)
}

function Assert-SafeOutputRoot {
    param(
        [Parameter(Mandatory = $true)][string] $OutputPath,
        [Parameter(Mandatory = $true)][string] $RepoRootPath
    )

    $resolvedOutput = Resolve-FullPath $OutputPath
    $resolvedRepo = Resolve-FullPath $RepoRootPath

    if ($resolvedOutput.Equals($resolvedRepo, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to publish the scorer into the source repository root."
    }

    if ([string]::IsNullOrWhiteSpace([System.IO.Path]::GetFileName($resolvedOutput.TrimEnd([System.IO.Path]::DirectorySeparatorChar)))) {
        throw "Refusing to publish the scorer into a filesystem root: $resolvedOutput"
    }
}

function Test-ReplaceableScorerOutputRoot {
    param([Parameter(Mandatory = $true)][string] $PathValue)

    if (Test-Path -LiteralPath (Join-Path $PathValue "scorer-root-manifest.json") -PathType Leaf) {
        return $true
    }

    return [System.IO.Path]::GetFileName($PathValue).Equals("carves-win-x64", [System.StringComparison]::OrdinalIgnoreCase)
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

$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = Resolve-FullPath (Join-Path $scriptRoot "../..")
$dotnetCommand = if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
    "dotnet.exe"
}
else {
    "dotnet"
}

if (-not $RuntimeIdentifier.Equals("win-x64", [System.StringComparison]::Ordinal)) {
    throw "CARD-950 only stages the self-contained win-x64 scorer root. RuntimeIdentifier='$RuntimeIdentifier'."
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "artifacts/publish/carves-win-x64"
}

$outputPath = Resolve-FullPath $OutputRoot
Assert-SafeOutputRoot -OutputPath $outputPath -RepoRootPath $repoRoot

if ((Test-Path -LiteralPath $outputPath) -and -not (Test-DirectoryEmpty -PathValue $outputPath)) {
    if (-not $Force) {
        throw "Scorer output already exists and is not empty: $outputPath. Pass -Force to replace it."
    }

    Assert-SafeOutputRoot -OutputPath $outputPath -RepoRootPath $repoRoot
    if (-not (Test-ReplaceableScorerOutputRoot -PathValue $outputPath)) {
        throw "Refusing to replace a non-scorer output directory: $outputPath"
    }

    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

$projectPath = Join-Path $repoRoot "src/CARVES.Runtime.Cli/carves.csproj"
$publishArguments = @(
    "publish",
    $projectPath,
    "--configuration",
    $Configuration,
    "--runtime",
    $RuntimeIdentifier,
    "--self-contained",
    "true",
    "--output",
    $outputPath,
    "--nologo",
    "--disable-build-servers",
    "-m:1",
    "-p:RestoreAdditionalProjectFallbackFolders=",
    "-p:RestoreFallbackFolders=",
    "-p:PublishSingleFile=false",
    "-p:UseAppHost=true",
    "-p:UseSharedCompilation=false"
)

if ($NoRestore) {
    $publishArguments += "--no-restore"
}

Invoke-Checked -FileName $dotnetCommand -Arguments $publishArguments -WorkingDirectory $repoRoot | Out-Null

$entrypoint = Join-Path $outputPath "carves.exe"
if (-not (Test-Path -LiteralPath $entrypoint -PathType Leaf)) {
    throw "Self-contained Windows scorer publish did not create carves.exe: $entrypoint"
}

$smokeStatus = "skipped"
$smokeReason = "non_windows_host_or_skip_requested"
if (-not $SkipRunSmoke -and [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
    $smokeRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("carves-scorer-smoke-" + [System.Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $smokeRoot -Force | Out-Null
    try {
        Invoke-Checked -FileName $entrypoint -Arguments @("test", "--help") -WorkingDirectory $smokeRoot | Out-Null
        $smokeStatus = "passed"
        $smokeReason = $null
    }
    finally {
        Remove-Item -LiteralPath $smokeRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$manifest = [ordered]@{
    schema_version = "carves-windows-scorer-root.v0"
    scorer_kind = "runtime_cli"
    runtime_identifier = $RuntimeIdentifier
    entrypoint = "carves.exe"
    target_project = "src/CARVES.Runtime.Cli/carves.csproj"
    configuration = $Configuration
    build_label = $BuildLabel
    self_contained = $true
    requires_source_checkout_to_publish = $true
    requires_source_checkout_to_run = $false
    requires_dotnet_to_run = $false
    uses_dotnet_run = $false
    supported_commands = @("test collect", "test reset", "test verify", "test result")
    run_smoke = [ordered]@{
        status = $smokeStatus
        reason = $smokeReason
        command = "carves.exe test --help"
    }
    non_claims = @(
        "not_tamper_proof_signature",
        "not_certification",
        "not_server_receipt",
        "not_leaderboard_proof",
        "not_producer_identity",
        "not_anti_cheat",
        "not_os_sandbox"
    )
}

$manifestPath = Join-Path $outputPath "scorer-root-manifest.json"
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host "Windows playable scorer root: $outputPath"
Write-Host "Entrypoint: $entrypoint"
Write-Host "Scorer root manifest: $manifestPath"

[CmdletBinding()]
param(
    [string]$Version = "0.6.2-beta",
    [string]$OutputRoot,
    [ValidateSet("release", "dev")]
    [string]$DistKind = "release",
    [switch]$Force,
    [switch]$AllowDirty,
    [switch]$SkipSmoke
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FullPath([string]$PathValue) {
    return [System.IO.Path]::GetFullPath($PathValue)
}

function Assert-SafeDistPath([string]$OutputPath, [string]$OutputRootPath, [string]$RepoRootPath) {
    $resolvedOutput = Resolve-FullPath $OutputPath
    $resolvedRoot = Resolve-FullPath $OutputRootPath
    $resolvedRepo = Resolve-FullPath $RepoRootPath

    if (-not $resolvedRoot.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $resolvedRoot += [System.IO.Path]::DirectorySeparatorChar
    }

    if (-not $resolvedOutput.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to write dist outside output root. Output='$resolvedOutput' OutputRoot='$resolvedRoot'."
    }

    if ($resolvedOutput.Equals($resolvedRepo, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to use the source repository as the dist output path."
    }
}

function Copy-FileIfPresent([string]$SourcePath, [string]$DestinationPath) {
    if (-not (Test-Path -LiteralPath $SourcePath -PathType Leaf)) {
        return
    }

    New-Item -ItemType Directory -Path (Split-Path -Parent $DestinationPath) -Force | Out-Null
    Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
}

function Copy-RequiredRelativeFile([string]$RepoRootPath, [string]$OutputRootPath, [string]$RelativePath) {
    $sourcePath = Join-Path $RepoRootPath $RelativePath
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Required release input is missing: $RelativePath"
    }

    Copy-FileIfPresent `
        -SourcePath $sourcePath `
        -DestinationPath (Join-Path $OutputRootPath $RelativePath)
}

function Copy-RequiredRelativeFiles([string]$RepoRootPath, [string]$OutputRootPath, [string[]]$RelativePaths) {
    foreach ($path in ($RelativePaths | Sort-Object -Unique)) {
        Copy-RequiredRelativeFile -RepoRootPath $RepoRootPath -OutputRootPath $OutputRootPath -RelativePath $path
    }
}

function Copy-DirectoryFiltered([string]$SourceDirectory, [string]$DestinationDirectory, [string[]]$ExcludedNames) {
    if (-not (Test-Path -LiteralPath $SourceDirectory -PathType Container)) {
        return
    }

    New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null

    foreach ($directory in Get-ChildItem -LiteralPath $SourceDirectory -Directory -Force) {
        if ($directory.Name -in $ExcludedNames) {
            continue
        }

        Copy-DirectoryFiltered `
            -SourceDirectory $directory.FullName `
            -DestinationDirectory (Join-Path $DestinationDirectory $directory.Name) `
            -ExcludedNames $ExcludedNames
    }

    foreach ($file in Get-ChildItem -LiteralPath $SourceDirectory -File -Force) {
        if ($file.Name -in $ExcludedNames) {
            continue
        }

        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $DestinationDirectory $file.Name) -Force
    }
}

function Assert-DistRelativePathsAreWindowsExtractable([string]$DistRoot, [int]$MaxRelativePathLength) {
    foreach ($item in Get-ChildItem -LiteralPath $DistRoot -Recurse -Force) {
        $relativePath = [System.IO.Path]::GetRelativePath($DistRoot, $item.FullName)
        $manifestPath = Convert-ToManifestPath $relativePath
        if ($manifestPath.Length -gt $MaxRelativePathLength) {
            throw "Dist contains Windows-hostile relative path length $($manifestPath.Length) > $MaxRelativePathLength`: $manifestPath"
        }
    }
}

function Invoke-Checked([string]$FileName, [string[]]$Arguments, [string]$WorkingDirectory) {
    $output = & $FileName @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        $output | ForEach-Object { Write-Error $_ }
        throw "Command failed with exit code $exitCode`: $FileName $($Arguments -join ' ')"
    }

    return $output
}

function Convert-ToManifestPath([string]$PathValue) {
    return $PathValue.Replace([System.IO.Path]::DirectorySeparatorChar, "/").Replace([System.IO.Path]::AltDirectorySeparatorChar, "/")
}

function Write-ReleaseWrappers([string]$DistRoot) {
    $unixWrapper = @'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
CLI_ENTRY="$SCRIPT_DIR/runtime-cli/carves.dll"

if [[ ! -f "$CLI_ENTRY" ]]; then
  echo "CARVES release dist is incomplete: runtime-cli/carves.dll is missing." >&2
  exit 1
fi

export CARVES_RUNTIME_ROOT="$SCRIPT_DIR"
exec dotnet "$CLI_ENTRY" "$@"
'@

    $powerShellWrapper = @'
$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$CliEntry = Join-Path $ScriptRoot "runtime-cli/carves.dll"

if (-not (Test-Path -LiteralPath $CliEntry -PathType Leaf)) {
    Write-Error "CARVES release dist is incomplete: runtime-cli/carves.dll is missing."
    exit 1
}

$env:CARVES_RUNTIME_ROOT = $ScriptRoot
& dotnet $CliEntry @args
exit $LASTEXITCODE
'@

    $cmdWrapper = @'
@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0carves.ps1" %*
exit /b %ERRORLEVEL%
'@

    Set-Content -LiteralPath (Join-Path $DistRoot "carves") -Value $unixWrapper -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $DistRoot "carves.ps1") -Value $powerShellWrapper -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $DistRoot "carves.cmd") -Value $cmdWrapper -Encoding ASCII
}

function Get-ReleaseDocPaths([string]$RepoRootPath, [string]$Version) {
    $paths = New-Object System.Collections.Generic.List[string]

    $productClosurePhasePaths = @(
        "docs/runtime/carves-product-closure-phase-0-baseline.md",
        "docs/runtime/carves-product-closure-phase-1-cli-distribution.md",
        "docs/runtime/carves-product-closure-phase-2-readiness-separation.md",
        "docs/runtime/carves-product-closure-phase-3-minimal-init-onboarding.md",
        "docs/runtime/carves-product-closure-phase-4-external-target-dogfood-proof.md",
        "docs/runtime/carves-product-closure-phase-5-real-project-pilot.md",
        "docs/runtime/carves-product-closure-phase-6-official-truth-writeback.md",
        "docs/runtime/carves-product-closure-phase-7-managed-workspace-execution.md",
        "docs/runtime/carves-product-closure-phase-8-managed-workspace-writeback.md",
        "docs/runtime/carves-product-closure-phase-9-productized-pilot-guide.md",
        "docs/runtime/carves-product-closure-phase-10-productized-pilot-status.md",
        "docs/runtime/carves-product-closure-phase-11b-target-agent-bootstrap-pack.md",
        "docs/runtime/carves-product-closure-phase-12-existing-target-bootstrap-repair.md",
        "docs/runtime/carves-product-closure-phase-13-target-commit-hygiene.md",
        "docs/runtime/carves-product-closure-phase-14-target-commit-plan.md",
        "docs/runtime/carves-product-closure-phase-15-target-commit-closure.md",
        "docs/runtime/carves-product-closure-phase-16-local-dist-handoff.md",
        "docs/runtime/carves-product-closure-phase-17-product-pilot-proof.md",
        "docs/runtime/carves-product-closure-phase-18-external-consumer-resource-pack.md",
        "docs/runtime/carves-product-closure-phase-19-cli-invocation-contract.md",
        "docs/runtime/carves-product-closure-phase-20-cli-activation-plan.md",
        "docs/runtime/carves-product-closure-phase-21-target-dist-binding-plan.md",
        "docs/runtime/carves-product-closure-phase-22-local-dist-freshness-smoke.md",
        "docs/runtime/carves-product-closure-phase-23-frozen-dist-target-readback-proof.md",
        "docs/runtime/carves-product-closure-phase-24-wrapper-runtime-root-binding.md",
        "docs/runtime/carves-product-closure-phase-25-external-target-product-proof-closure.md",
        "docs/runtime/carves-product-closure-phase-26-real-external-repo-pilot.md",
        "docs/runtime/carves-product-closure-phase-26a-product-closure-projection-cleanup.md",
        "docs/runtime/carves-product-closure-phase-27-external-target-residue-policy.md",
        "docs/runtime/carves-product-closure-phase-28-target-ignore-decision-plan.md",
        "docs/runtime/carves-product-closure-phase-29-target-ignore-decision-record.md",
        "docs/runtime/carves-product-closure-phase-30-target-ignore-decision-record-audit.md",
        "docs/runtime/carves-product-closure-phase-31-target-ignore-decision-record-commit-readback.md",
        "docs/runtime/carves-product-closure-phase-32-alpha-external-use-readiness-rollup.md",
        "docs/runtime/carves-product-closure-phase-33-external-target-pilot-start-bundle.md",
        "docs/runtime/carves-product-closure-phase-34-agent-problem-intake.md",
        "docs/runtime/carves-product-closure-phase-35-agent-problem-triage-ledger.md",
        "docs/runtime/carves-product-closure-phase-36-agent-problem-follow-up-candidates.md",
        "docs/runtime/carves-product-closure-phase-37-agent-problem-follow-up-decision-plan.md",
        "docs/runtime/carves-product-closure-phase-38-agent-problem-follow-up-decision-record.md",
        "docs/runtime/carves-product-closure-phase-39-agent-problem-follow-up-planning-intake.md",
        "docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md"
    )

    foreach ($path in $productClosurePhasePaths) {
        if (Test-Path -LiteralPath (Join-Path $RepoRootPath $path) -PathType Leaf) {
            $paths.Add($path)
        }
    }

    $releaseDocPattern = ("runtime-{0}-*.md" -f $Version)
    $releaseDocs = @(Get-ChildItem -LiteralPath (Join-Path $RepoRootPath "docs/release") -Filter $releaseDocPattern -File -ErrorAction SilentlyContinue)
    if ($releaseDocs.Count -eq 0) {
        $releaseDocs = @(Get-ChildItem -LiteralPath (Join-Path $RepoRootPath "docs/release") -Filter "runtime-0.6.1-beta-*.md" -File -ErrorAction SilentlyContinue)
    }

    foreach ($file in $releaseDocs) {
        $paths.Add((Convert-ToManifestPath ([System.IO.Path]::GetRelativePath($RepoRootPath, $file.FullName))))
    }

    $manualPaths = @(
        "docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_CANDIDATES.md",
        "docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_PLAN.md",
        "docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_RECORD.md",
        "docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_GATE.md",
        "docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_INTAKE.md",
        "docs/guides/CARVES_AGENT_PROBLEM_INTAKE.md",
        "docs/guides/CARVES_AGENT_PROBLEM_TRIAGE_LEDGER.md",
        "docs/guides/CARVES_CLI_ACTIVATION_PLAN.md",
        "docs/guides/CARVES_CLI_DISTRIBUTION.md",
        "docs/guides/CARVES_CLI_INVOCATION_CONTRACT.md",
        "docs/guides/CARVES_EXTERNAL_AGENT_QUICKSTART.md",
        "docs/guides/CARVES_EXTERNAL_CONSUMER_RESOURCE_PACK.md",
        "docs/guides/CARVES_FROZEN_DIST_TARGET_READBACK_PROOF.md",
        "docs/guides/CARVES_LOCAL_DIST_FRESHNESS_SMOKE.md",
        "docs/guides/CARVES_PRODUCTIZED_PILOT_GUIDE.md",
        "docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md",
        "docs/guides/CARVES_RUNTIME_LOCAL_DIST.md",
        "docs/guides/CARVES_TARGET_AGENT_BOOTSTRAP_PACK.md",
        "docs/guides/CARVES_TARGET_DIST_BINDING_PLAN.md",
        "docs/guides/HOST_AND_PROVIDER_QUICKSTART.md",
        "docs/guides/RUNTIME_AGENT_V1_DELIVERY_READINESS.md",
        "docs/guides/RUNTIME_AGENT_V1_OPERATOR_FEEDBACK_GUIDE.md",
        "docs/guides/RUNTIME_AGENT_V1_VALIDATION_BUNDLE.md",
        "docs/release/runtime-versioning-policy.md",
        "docs/runtime/runtime-adapter-handoff-contract.md",
        "docs/runtime/runtime-agent-governed-failure-classification-recovery-closure-contract.md",
        "docs/runtime/runtime-agent-governed-operator-feedback-closure-contract.md",
        "docs/runtime/runtime-agent-governed-packaging-closure-delivery-readiness-contract.md",
        "docs/runtime/runtime-agent-working-modes-and-constraint-ladder.md",
        "docs/runtime/runtime-agent-working-modes-implementation-plan.md",
        "docs/runtime/runtime-cli-first-architecture.md",
        "docs/runtime/runtime-collaboration-and-surface-projection.md",
        "docs/runtime/runtime-constraint-ladder-and-collaboration-plane.md",
        "docs/runtime/runtime-first-run-operator-packet.md",
        "docs/runtime/runtime-governance-program-reaudit.md",
        "docs/runtime/runtime-governed-agent-handoff-proof.md",
        "docs/runtime/runtime-guided-planning-intent-stabilizer-graph-boundary.md",
        "docs/runtime/runtime-hotspot-backlog-drain-governance.md",
        "docs/runtime/runtime-hotspot-cross-family-patterns.md",
        "docs/runtime/runtime-managed-workspace-file-operation-model.md",
        "docs/runtime/runtime-managed-workspace-lease.md",
        "docs/runtime/runtime-mode-d-scoped-task-workspace-hardening.md",
        "docs/runtime/runtime-mode-e-brokered-execution.md",
        "docs/runtime/runtime-packaging-proof-federation-maturity.md",
        "docs/runtime/runtime-plan-mode-and-active-planning-card.md",
        "docs/runtime/runtime-plan-required-and-workspace-required-gates.md",
        "docs/runtime/runtime-planning-packet-and-replan-rules.md",
        "docs/runtime/runtime-protected-truth-root-policy.md",
        "docs/session-gateway/ALPHA_QUICKSTART.md",
        "docs/session-gateway/ALPHA_SETUP.md",
        "docs/session-gateway/BUG_REPORT_BUNDLE.md",
        "docs/session-gateway/KNOWN_LIMITATIONS.md",
        "docs/session-gateway/adapter-handoff-contract.md",
        "docs/session-gateway/dogfood-validation.md",
        "docs/session-gateway/governed-agent-handoff-proof.md",
        "docs/session-gateway/operator-proof-contract.md",
        "docs/session-gateway/release-surface.md",
        "docs/session-gateway/repeatability-readiness.md",
        "docs/session-gateway/session-gateway-v1-post-closure-execution-plan.md",
        "docs/session-gateway/session-gateway-v1.md",
        "docs/runtime/workbench-v1-scope-and-boundary.md"
    )

    foreach ($path in $manualPaths) {
        if (Test-Path -LiteralPath (Join-Path $RepoRootPath $path) -PathType Leaf) {
            $paths.Add($path)
        }
    }

    return @($paths | Sort-Object -Unique)
}

$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = Resolve-FullPath (Join-Path $scriptRoot "..")
$isWindowsPlatform = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path (Split-Path -Parent $repoRoot) ".dist"
}

$outputRootPath = Resolve-FullPath $OutputRoot
$outputPath = Resolve-FullPath (Join-Path $outputRootPath "CARVES.Runtime-$Version")
Assert-SafeDistPath -OutputPath $outputPath -OutputRootPath $outputRootPath -RepoRootPath $repoRoot

$dirty = @(Invoke-Checked -FileName "git" -Arguments @("-C", $repoRoot, "status", "--porcelain") -WorkingDirectory $repoRoot)
if (-not $AllowDirty -and $dirty.Count -gt 0) {
    throw "Runtime source repository is dirty. Commit or clean it before creating a dist, or pass -AllowDirty intentionally."
}

$sourceCommit = (@(Invoke-Checked -FileName "git" -Arguments @("-C", $repoRoot, "rev-parse", "HEAD") -WorkingDirectory $repoRoot) | Select-Object -First 1).ToString().Trim()

if (Test-Path -LiteralPath $outputPath) {
    if (-not $Force) {
        throw "Dist output already exists: $outputPath. Pass -Force to replace it."
    }

    Assert-SafeDistPath -OutputPath $outputPath -OutputRootPath $outputRootPath -RepoRootPath $repoRoot
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

$excludedNames = @(
    ".git",
    ".vs",
    "bin",
    "obj",
    "TestResults",
    ".carves-temp",
    ".codex-temp",
    ".tmp"
)

$devRootFiles = @(
    ".editorconfig",
    ".gitattributes",
    ".gitignore",
    "AGENTS.md",
    "APPLY_NOTES.md",
    "carves",
    "carves.cmd",
    "carves.ps1",
    "CLAUDE.md",
    "README.md",
    "repo.lint.yaml",
    "Directory.Build.props",
    "Directory.Build.targets",
    "global.json",
    "NuGet.Config",
    "nuget.config"
)

$releaseRootFiles = @(
    "AGENTS.md",
    "README.md",
    "START_CARVES.md"
)

$releaseInteractionTemplateFiles = @(
    "templates/interaction/CARVES_PROMPT_KERNEL.md",
    "templates/interaction/card-proposal.template.md",
    "templates/interaction/intent-summary.template.md",
    "templates/interaction/review-explanation.template.md",
    "templates/interaction/task-proposal.template.md"
)

if ($DistKind -eq "dev") {
    foreach ($file in $devRootFiles) {
        Copy-FileIfPresent -SourcePath (Join-Path $repoRoot $file) -DestinationPath (Join-Path $outputPath $file)
    }

    foreach ($directory in @("src", "docs", "templates", "scripts")) {
        Copy-DirectoryFiltered `
            -SourceDirectory (Join-Path $repoRoot $directory) `
            -DestinationDirectory (Join-Path $outputPath $directory) `
            -ExcludedNames $excludedNames
    }
}
else {
    Copy-RequiredRelativeFiles -RepoRootPath $repoRoot -OutputRootPath $outputPath -RelativePaths $releaseRootFiles
    Write-ReleaseWrappers -DistRoot $outputPath
    Copy-RequiredRelativeFiles -RepoRootPath $repoRoot -OutputRootPath $outputPath -RelativePaths (Get-ReleaseDocPaths -RepoRootPath $repoRoot -Version $Version)
    Copy-RequiredRelativeFiles -RepoRootPath $repoRoot -OutputRootPath $outputPath -RelativePaths $releaseInteractionTemplateFiles
    Copy-RequiredRelativeFile -RepoRootPath $repoRoot -OutputRootPath $outputPath -RelativePath ".ai/PROJECT_BOUNDARY.md"
}

$publishedCliDirectoryName = "runtime-cli"
$publishedCliRoot = Join-Path $outputPath $publishedCliDirectoryName
$publishedCliEntry = Convert-ToManifestPath (Join-Path $publishedCliDirectoryName "carves.dll")
Invoke-Checked `
    -FileName "dotnet" `
    -Arguments @(
        "publish",
        (Join-Path $repoRoot "src/CARVES.Runtime.Cli/carves.csproj"),
        "-c",
        "Release",
        "-o",
        $publishedCliRoot,
        "--nologo",
        "--disable-build-servers",
        "-m:1",
        "-p:UseSharedCompilation=false",
        "-p:UseAppHost=false"
    ) `
    -WorkingDirectory $repoRoot | Out-Null

if (-not $isWindowsPlatform -and (Test-Path -LiteralPath (Join-Path $outputPath "carves") -PathType Leaf)) {
    Invoke-Checked -FileName "chmod" -Arguments @("+x", (Join-Path $outputPath "carves")) -WorkingDirectory $outputPath | Out-Null
}

if ($DistKind -eq "dev") {
    $aiOutputRoot = Join-Path $outputPath ".ai"
    New-Item -ItemType Directory -Path $aiOutputRoot -Force | Out-Null
    foreach ($file in @("README.md", "PROJECT_BOUNDARY.md", "STATE.md", "TASK_QUEUE.md", "DEV_LOOP.md", "DRY_RUN_PROTOCOL.md", "TASK_DISCOVERY.md")) {
        Copy-FileIfPresent -SourcePath (Join-Path (Join-Path $repoRoot ".ai") $file) -DestinationPath (Join-Path $aiOutputRoot $file)
    }

    Copy-DirectoryFiltered `
        -SourceDirectory (Join-Path $repoRoot ".ai/memory") `
        -DestinationDirectory (Join-Path $aiOutputRoot "memory") `
        -ExcludedNames ($excludedNames + @("execution"))
}

Set-Content -LiteralPath (Join-Path $outputPath "VERSION") -Value $Version -Encoding UTF8

$commandEntry = if ($isWindowsPlatform) { "carves.ps1" } else { "carves" }
$usageCommand = Join-Path $outputPath $commandEntry
$includedRoots = if ($DistKind -eq "dev") {
    @("runtime-cli", "src", "docs", "templates", "scripts", ".ai/memory")
} else {
    @("runtime-cli", "docs/guides", "docs/runtime", "docs/session-gateway", "docs/release", "templates/interaction")
}
$includedFiles = if ($DistKind -eq "dev") {
    $devRootFiles
} else {
    @("AGENTS.md", "README.md", "START_CARVES.md", "carves", "carves.cmd", "carves.ps1", ".ai/PROJECT_BOUNDARY.md")
}
$excludedRoots = if ($DistKind -eq "dev") {
    @(".git", ".vs", "bin", "obj", "TestResults", ".ai/runtime", ".ai/tasks", ".ai/artifacts", ".ai/tmp", ".ai/memory/execution", ".carves-platform")
} else {
    @(".git", ".vs", "bin", "obj", "TestResults", "src", "tests", "scripts", "CARVES.Runtime.sln", ".ai/TASK_QUEUE.md", ".ai/tasks", ".ai/runtime", ".ai/artifacts", ".ai/tmp", ".ai/execution", ".ai/failures", ".ai/memory", ".carves-platform", "docs/archive")
}

$manifest = [ordered]@{
    schema_version = "carves-runtime-dist.v1"
    dist_kind = $DistKind
    version = $Version
    source_repo_root = $repoRoot
    source_commit = $sourceCommit
    generated_at = [DateTimeOffset]::UtcNow.ToString("O")
    output_path = $outputPath
    command_entry = $commandEntry
    published_cli_entry = $publishedCliEntry
    published_cli_kind = "framework_dependent_dotnet_dll"
    hot_start_supported = $true
    source_tree_included = ($DistKind -eq "dev")
    docs_profile = if ($DistKind -eq "dev") { "full" } else { "runtime_release_whitelist" }
    ai_profile = if ($DistKind -eq "dev") { "dev_memory_projection" } else { "project_boundary_only" }
    included_roots = $includedRoots
    included_files = $includedFiles
    excluded_roots = $excludedRoots
    usage = @(
        "$usageCommand --help",
        "$usageCommand pilot status --json"
    )
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputPath "MANIFEST.json") -Encoding UTF8

Assert-DistRelativePathsAreWindowsExtractable -DistRoot $outputPath -MaxRelativePathLength 180

if ($DistKind -eq "release") {
    $powerShellExecutable = (Get-Process -Id $PID).Path
    Invoke-Checked `
        -FileName $powerShellExecutable `
        -Arguments @(
            "-NoProfile",
            "-File",
            (Join-Path $scriptRoot "assert-runtime-release-dist.ps1"),
            "-DistRoot",
            $outputPath
        ) `
        -WorkingDirectory $repoRoot | Out-Null
}

if (-not $SkipSmoke) {
    $smokeWorkingDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "carves-runtime-dist-smoke-$([Guid]::NewGuid().ToString("N"))"
    New-Item -ItemType Directory -Path $smokeWorkingDirectory -Force | Out-Null

    try {
        if ($isWindowsPlatform) {
            # Smoke in a temp cwd so first-run policy/cache material does not pollute the release dist.
            Invoke-Checked `
                -FileName "powershell" `
                -Arguments @(
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    (Join-Path $outputPath "carves.ps1"),
                    "--help"
                ) `
                -WorkingDirectory $smokeWorkingDirectory | Out-Null
        }
        else {
            Invoke-Checked `
                -FileName (Join-Path $outputPath "carves") `
                -Arguments @("--help") `
                -WorkingDirectory $smokeWorkingDirectory | Out-Null
        }
    }
    finally {
        Remove-Item -LiteralPath $smokeWorkingDirectory -Recurse -Force -ErrorAction SilentlyContinue
    }

    if ($DistKind -eq "release") {
        Invoke-Checked `
            -FileName $powerShellExecutable `
            -Arguments @(
                "-NoProfile",
                "-File",
                (Join-Path $scriptRoot "assert-runtime-release-dist.ps1"),
                "-DistRoot",
                $outputPath
            ) `
            -WorkingDirectory $repoRoot | Out-Null
    }
}

Write-Host "CARVES Runtime dist created."
Write-Host "Version: $Version"
Write-Host "Source commit: $sourceCommit"
Write-Host "Output: $outputPath"

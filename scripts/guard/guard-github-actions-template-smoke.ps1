[CmdletBinding()]
param(
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

if ([string]::IsNullOrWhiteSpace($RuntimeRoot)) {
    $RuntimeRoot = Resolve-FullPath (Join-Path $PSScriptRoot "../..")
}
else {
    $RuntimeRoot = Resolve-FullPath $RuntimeRoot
}

if ([string]::IsNullOrWhiteSpace($WorkRoot)) {
    $WorkRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("carves-guard-actions-template-" + [Guid]::NewGuid().ToString("N"))
}
$WorkRoot = Resolve-FullPath $WorkRoot

$templatePath = Join-Path $RuntimeRoot "docs/guard/github-actions-template.yml"
if (-not (Test-Path -LiteralPath $templatePath)) {
    throw "Guard GitHub Actions template not found at $templatePath."
}

$targetWorkflow = Join-Path $WorkRoot ".github/workflows/carves-guard.yml"
New-Item -ItemType Directory -Force (Split-Path -Parent $targetWorkflow) | Out-Null

try {
    Copy-Item -LiteralPath $templatePath -Destination $targetWorkflow
    $template = Get-Content -Raw -LiteralPath $targetWorkflow
    $requiredSnippets = @(
        "pull_request:",
        "actions/checkout@v4",
        "actions/setup-dotnet@v4",
        "repository: CARVES-AI/CARVES.Runtime",
        "guard init",
        "guard check --json",
        "guard-check.json",
        "actions/upload-artifact@v4",
        "carves-guard-decision"
    )

    $missing = @($requiredSnippets | Where-Object { -not $template.Contains($_, [System.StringComparison]::Ordinal) })
    if ($missing.Count -gt 0) {
        throw "Guard GitHub Actions template is missing required snippets: $($missing -join ', ')"
    }

    if ($template.Contains("secrets.", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Guard GitHub Actions template must not require hosted secrets."
    }

    [pscustomobject]@{
        smoke = "guard_github_actions_template_shape"
        workflow_path = $targetWorkflow
        required_snippets_checked = $requiredSnippets.Count
        missing_snippets = $missing
        hosted_secrets_required = $false
        remote_registry_publication_required = $false
        expected_guard_exit_behavior = "review_or_block_fails_job"
    } | ConvertTo-Json -Depth 8
}
finally {
    if (-not $Keep -and (Test-Path -LiteralPath $WorkRoot)) {
        Assert-PathInside -PathValue $WorkRoot -RootValue ([System.IO.Path]::GetTempPath())
        Remove-Item -LiteralPath $WorkRoot -Recurse -Force
    }
}

[CmdletBinding()]
param(
    [string] $RuntimeRoot = "",
    [string] $OutputPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string] $PathValue)
    return [System.IO.Path]::GetFullPath($PathValue)
}

if ([string]::IsNullOrWhiteSpace($RuntimeRoot)) {
    $RuntimeRoot = Resolve-FullPath (Join-Path $PSScriptRoot "../..")
}
else {
    $RuntimeRoot = Resolve-FullPath $RuntimeRoot
}

$catalogPath = Join-Path $RuntimeRoot "docs/matrix/examples/matrix-external-repo-pilot-set.v0.example.json"
if (-not (Test-Path -LiteralPath $catalogPath -PathType Leaf)) {
    throw "Matrix external repo pilot catalog was not found: $catalogPath"
}

$catalog = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json -Depth 100
$requiredPilots = @(
    "node_single_package",
    "dotnet_small_project",
    "python_package",
    "monorepo_nested_project",
    "dirty_worktree"
)

if ($catalog.schema_version -ne "matrix-external-repo-pilot-set.v0") {
    throw "Unexpected pilot set schema_version: $($catalog.schema_version)"
}

if ($catalog.local_only -ne $true -or $catalog.summary_only -ne $true) {
    throw "Pilot set must be local_only=true and summary_only=true."
}

if ($catalog.pilot_count -lt $requiredPilots.Count) {
    throw "Pilot set must include at least $($requiredPilots.Count) pilots."
}

$pilotIds = @($catalog.pilots | ForEach-Object { $_.pilot_id })
foreach ($pilotId in $requiredPilots) {
    if ($pilotId -notin $pilotIds) {
        throw "Missing required pilot: $pilotId"
    }
}

foreach ($pilot in $catalog.pilots) {
    if ([string]::IsNullOrWhiteSpace([string] $pilot.setup.summary)) {
        throw "Pilot $($pilot.pilot_id) is missing setup summary."
    }

    if (@($pilot.expected_matrix_behavior).Count -eq 0) {
        throw "Pilot $($pilot.pilot_id) is missing expected Matrix behavior."
    }

    if (@($pilot.known_limitations).Count -eq 0) {
        throw "Pilot $($pilot.pilot_id) is missing known limitations."
    }

    if ($pilot.artifact_policy.summary_only -ne $true) {
        throw "Pilot $($pilot.pilot_id) must keep summary_only artifact policy."
    }

    foreach ($forbidden in @("source_code", "raw_git_diff", "prompt", "model_response", "secret", "credential", "hosted_upload", "certification_claim")) {
        if ($forbidden -notin @($pilot.artifact_policy.forbidden_artifacts)) {
            throw "Pilot $($pilot.pilot_id) is missing forbidden artifact marker: $forbidden"
        }
    }
}

foreach ($privacyFlag in @("source_included", "raw_diff_included", "prompt_included", "model_response_included", "secrets_included", "credentials_included", "customer_payload_included", "hosted_upload_required")) {
    if ($catalog.privacy.$privacyFlag -ne $false) {
        throw "Pilot set privacy flag must be false: $privacyFlag"
    }
}

foreach ($claim in @("certification", "hosted_verification", "public_leaderboard", "model_safety_benchmark", "semantic_correctness", "operating_system_sandbox", "automatic_rollback")) {
    if ($catalog.public_claims.$claim -ne $false) {
        throw "Pilot set public claim must be false: $claim"
    }
}

$json = $catalog | ConvertTo-Json -Depth 100
if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $resolvedOutputPath = [System.IO.Path]::IsPathRooted($OutputPath) ? (Resolve-FullPath $OutputPath) : (Resolve-FullPath (Join-Path $RuntimeRoot $OutputPath))
    $outputDirectory = Split-Path -Parent $resolvedOutputPath
    if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
        New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
    }

    $json | Set-Content -LiteralPath $resolvedOutputPath -Encoding UTF8
}

$json

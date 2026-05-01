[CmdletBinding()]
param(
    [string] $RuntimeRoot = "",
    [string] $ArtifactRoot = "",
    [string] $Configuration = "Release",
    [switch] $SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "matrix-checked-process.ps1")

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string] $PathValue)
    return [System.IO.Path]::GetFullPath($PathValue)
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string] $FileName,
        [Parameter(Mandatory = $true)][string[]] $Arguments,
        [Parameter(Mandatory = $true)][string] $WorkingDirectory,
        [int[]] $AllowedExitCodes = @(0)
    )

    $result = Invoke-MatrixCheckedProcess `
        -FileName $FileName `
        -Arguments $Arguments `
        -WorkingDirectory $WorkingDirectory `
        -AllowedExitCodes $AllowedExitCodes

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

function ConvertTo-JsonObject {
    param(
        [Parameter(Mandatory = $true)] $CommandResult,
        [Parameter(Mandatory = $true)][string] $StepName
    )

    $text = $CommandResult.stdout.Trim()
    if (-not $text.StartsWith("{", [System.StringComparison]::Ordinal)) {
        throw "$StepName did not emit JSON. STDOUT:`n$text`nSTDERR:`n$($CommandResult.stderr)"
    }

    return $text | ConvertFrom-Json -Depth 100
}

function New-SummaryOnlyPrivacy {
    return [ordered]@{
        summary_only = $true
        source_included = $false
        raw_diff_included = $false
        prompt_included = $false
        model_response_included = $false
        secrets_included = $false
        credentials_included = $false
        private_payload_included = $false
        customer_payload_included = $false
        hosted_upload_required = $false
        certification_claim = $false
        public_leaderboard_claim = $false
    }
}

function Convert-ToArtifactPath {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][string] $RelativePath
    )

    return Join-Path $Root $RelativePath.Replace("/", [System.IO.Path]::DirectorySeparatorChar)
}

function Write-Artifact {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][string] $RelativePath,
        [Parameter(Mandatory = $true)][string] $Content
    )

    $path = Convert-ToArtifactPath -Root $Root -RelativePath $RelativePath
    $directory = Split-Path -Parent $path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $Content | Set-Content -LiteralPath $path -Encoding UTF8
}

function Get-Sha256 {
    param([Parameter(Mandatory = $true)][string] $PathValue)
    return (Get-FileHash -LiteralPath $PathValue -Algorithm SHA256).Hash.ToLowerInvariant()
}

function New-ManifestEntry {
    param(
        [Parameter(Mandatory = $true)][string] $BundleRoot,
        [Parameter(Mandatory = $true)][string] $ArtifactKind,
        [Parameter(Mandatory = $true)][string] $RelativePath,
        [Parameter(Mandatory = $true)][string] $SchemaVersion,
        [Parameter(Mandatory = $true)][string] $Producer,
        [Parameter(Mandatory = $true)][string] $CreatedAt
    )

    $path = Convert-ToArtifactPath -Root $BundleRoot -RelativePath $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required pilot artifact missing: $RelativePath"
    }

    return [ordered]@{
        artifact_kind = $ArtifactKind
        path = $RelativePath
        sha256 = Get-Sha256 -PathValue $path
        size = (Get-Item -LiteralPath $path).Length
        schema_version = $SchemaVersion
        producer = $Producer
        created_at = $CreatedAt
        privacy_flags = New-SummaryOnlyPrivacy
    }
}

function Write-Manifest {
    param(
        [Parameter(Mandatory = $true)][string] $BundleRoot,
        [Parameter(Mandatory = $true)][string] $CreatedAt
    )

    $entries = @(
        New-ManifestEntry -BundleRoot $BundleRoot -ArtifactKind "guard_decision" -RelativePath "project/decisions.jsonl" -SchemaVersion "guard-decision-jsonl" -Producer "carves-guard" -CreatedAt $CreatedAt
        New-ManifestEntry -BundleRoot $BundleRoot -ArtifactKind "handoff_packet" -RelativePath "project/handoff.json" -SchemaVersion "carves-continuity-handoff.v1" -Producer "carves-handoff" -CreatedAt $CreatedAt
        New-ManifestEntry -BundleRoot $BundleRoot -ArtifactKind "audit_evidence" -RelativePath "project/shield-evidence.json" -SchemaVersion "shield-evidence.v0" -Producer "carves-audit" -CreatedAt $CreatedAt
        New-ManifestEntry -BundleRoot $BundleRoot -ArtifactKind "shield_evaluation" -RelativePath "project/shield-evaluate.json" -SchemaVersion "shield-evaluate.v0" -Producer "carves-shield" -CreatedAt $CreatedAt
        New-ManifestEntry -BundleRoot $BundleRoot -ArtifactKind "shield_badge_json" -RelativePath "project/shield-badge.json" -SchemaVersion "shield-badge.v0" -Producer "carves-shield" -CreatedAt $CreatedAt
        New-ManifestEntry -BundleRoot $BundleRoot -ArtifactKind "shield_badge_svg" -RelativePath "project/shield-badge.svg" -SchemaVersion "shield-badge-svg.v0" -Producer "carves-shield" -CreatedAt $CreatedAt
        New-ManifestEntry -BundleRoot $BundleRoot -ArtifactKind "matrix_summary" -RelativePath "project/matrix-summary.json" -SchemaVersion "matrix-summary.v0" -Producer "carves-matrix" -CreatedAt $CreatedAt
    )

    $manifest = [ordered]@{
        schema_version = "matrix-artifact-manifest.v0"
        created_at = $CreatedAt
        producer = [ordered]@{
            tool = "carves-matrix"
            component = "CARVES.Matrix.Core"
            mode = "external_pilot_verify_bundle"
        }
        artifact_root = "."
        producer_artifact_root = "<redacted-local-artifact-root>"
        privacy = New-SummaryOnlyPrivacy
        artifacts = $entries
    }

    $manifestPath = Join-Path $BundleRoot "matrix-artifact-manifest.json"
    $manifest | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    return $manifestPath
}

function Write-ProofSummary {
    param(
        [Parameter(Mandatory = $true)][string] $BundleRoot,
        [Parameter(Mandatory = $true)][string] $ManifestPath,
        [Parameter(Mandatory = $true)][string] $EvidenceSha256
    )

    $summary = [ordered]@{
        schema_version = "matrix-proof-summary.v0"
        smoke = "matrix_native_minimal_proof_lane"
        shell = "carves-matrix"
        proof_mode = "native_minimal"
        proof_capabilities = [ordered]@{
            proof_lane = "native_minimal"
            execution_backend = "dotnet_runner_chain"
            coverage = [ordered]@{
                project_mode = $true
                packaged_install = $false
                full_release = $false
            }
            requirements = [ordered]@{
                powershell = $false
                source_checkout = $false
                dotnet_sdk = $true
                git = $true
            }
        }
        artifact_root = "."
        artifact_manifest = [ordered]@{
            path = "matrix-artifact-manifest.json"
            schema_version = "matrix-artifact-manifest.v0"
            sha256 = Get-Sha256 -PathValue $ManifestPath
            verification_posture = "verified"
            issue_count = 0
        }
        trust_chain_hardening = [ordered]@{
            gates_satisfied = $true
            computed_by = "matrix_verifier"
            gates = @(
                [ordered]@{
                    gate_id = "manifest_integrity"
                    satisfied = $true
                    reason = "Manifest hashes, sizes, privacy flags, and artifact file presence verified."
                    issue_codes = @()
                    reason_codes = @()
                },
                [ordered]@{
                    gate_id = "required_artifacts"
                    satisfied = $true
                    reason = "All required artifact entries are present with expected path, schema, and producer metadata."
                    issue_codes = @()
                    reason_codes = @()
                },
                [ordered]@{
                    gate_id = "shield_score"
                    satisfied = $true
                    reason = "Shield evaluation status, certification posture, Standard label, and Lite score fields are verified."
                    issue_codes = @()
                    reason_codes = @()
                },
                [ordered]@{
                    gate_id = "summary_consistency"
                    satisfied = $true
                    reason = "Matrix proof summary references the current manifest hash, posture, and issue count."
                    issue_codes = @()
                    reason_codes = @()
                }
            )
        }
        native = [ordered]@{
            passed = $true
            proof_role = "composition_orchestrator"
            scoring_owner = "shield"
            alters_shield_score = $false
            shield_status = "ok"
            shield_standard_label = "CARVES G1.H1.A1 /1d PASS"
            lite_score = 50
            consumed_shield_evidence_sha256 = $EvidenceSha256
            guard_decision_artifact = "project/decisions.jsonl"
            handoff_packet_artifact = "project/handoff.json"
            consumed_shield_evidence_artifact = "project/shield-evidence.json"
            shield_evaluation_artifact = "project/shield-evaluate.json"
            shield_badge_json_artifact = "project/shield-badge.json"
            shield_badge_svg_artifact = "project/shield-badge.svg"
            matrix_summary_artifact = "project/matrix-summary.json"
            artifact_root = "."
        }
        privacy = [ordered]@{
            summary_only = $true
            source_upload_required = $false
            raw_diff_upload_required = $false
            prompt_upload_required = $false
            model_response_upload_required = $false
            secrets_required = $false
            hosted_api_required = $false
        }
        public_claims = [ordered]@{
            certification = $false
            hosted_verification = $false
            public_leaderboard = $false
            os_sandbox_claim = $false
        }
    }

    $summary | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $BundleRoot "matrix-proof-summary.json") -Encoding UTF8
}

function New-PilotBundle {
    param(
        [Parameter(Mandatory = $true)] $Pilot,
        [Parameter(Mandatory = $true)][string] $BundleRoot
    )

    New-Item -ItemType Directory -Force -Path $BundleRoot | Out-Null
    $pilotId = [string] $Pilot.pilot_id
    $shape = [string] $Pilot.shape
    $createdAt = "2026-04-15T00:00:00+00:00"

    Write-Artifact -Root $BundleRoot -RelativePath "project/decisions.jsonl" -Content "{`"schema_version`":`"guard-decision-jsonl`",`"pilot_id`":`"$pilotId`",`"decision`":`"allow`",`"changed_file_count`":1}"
    Write-Artifact -Root $BundleRoot -RelativePath "project/handoff.json" -Content (@{
        schema_version = "carves-continuity-handoff.v1"
        handoff_id = "HND-$($pilotId.ToUpperInvariant())"
        resume_status = "ready"
        current_objective = "Review Matrix external pilot summary artifacts."
        remaining_work = @("Run carves-matrix verify for the pilot bundle.")
    } | ConvertTo-Json -Depth 20)
    Write-Artifact -Root $BundleRoot -RelativePath "project/shield-evidence.json" -Content (@{
        schema_version = "shield-evidence.v0"
        pilot_id = $pilotId
        shape = $shape
        privacy = @{
            source_included = $false
            raw_diff_included = $false
            prompt_included = $false
            model_response_included = $false
            secrets_included = $false
            credentials_included = $false
            upload_intent = "none"
        }
    } | ConvertTo-Json -Depth 20)
    $evidenceSha256 = Get-Sha256 -PathValue (Convert-ToArtifactPath -Root $BundleRoot -RelativePath "project/shield-evidence.json")
    Write-Artifact -Root $BundleRoot -RelativePath "project/shield-evaluate.json" -Content @"
{
  "schema_version": "shield-evaluate.v0",
  "status": "ok",
  "certification": false,
  "consumed_evidence_sha256": "$evidenceSha256",
  "standard": {
    "label": "CARVES G1.H1.A1 /1d PASS"
  },
  "lite": {
    "score": 50,
    "band": "disciplined"
  }
}
"@
    Write-Artifact -Root $BundleRoot -RelativePath "project/shield-badge.json" -Content "{`"schema_version`":`"shield-badge.v0`",`"status`":`"ok`",`"certification`":false}"
    Write-Artifact -Root $BundleRoot -RelativePath "project/shield-badge.svg" -Content "<svg xmlns=`"http://www.w3.org/2000/svg`"><text>CARVES local self-check</text></svg>"
    Write-Artifact -Root $BundleRoot -RelativePath "project/matrix-summary.json" -Content (@{
        schema_version = "matrix-summary.v0"
        smoke = "matrix_cross_platform_verify_pilot"
        proof_role = "composition_orchestrator"
        proof_mode = "native_minimal"
        scoring_owner = "shield"
        alters_shield_score = $false
        artifact_root = "."
        pilot_id = $pilotId
        shape = $shape
        setup_summary = [string] $Pilot.setup.summary
        expected_matrix_behavior = @($Pilot.expected_matrix_behavior)
        known_limitations = @($Pilot.known_limitations)
        shield = @{
            status = "ok"
            standard_label = "CARVES G1.H1.A1 /1d PASS"
            lite_score = 50
            consumed_evidence_sha256 = $evidenceSha256
        }
        summary_only = $true
        public_claims = @{
            certification = $false
            hosted_verification = $false
            public_leaderboard = $false
        }
    } | ConvertTo-Json -Depth 50)

    $manifestPath = Write-Manifest -BundleRoot $BundleRoot -CreatedAt $createdAt
    Write-ProofSummary -BundleRoot $BundleRoot -ManifestPath $manifestPath -EvidenceSha256 $evidenceSha256
}

function Invoke-MatrixVerify {
    param(
        [Parameter(Mandatory = $true)][string] $BundleRoot,
        [Parameter(Mandatory = $true)][int[]] $AllowedExitCodes
    )

    $arguments = @(
        "run",
        "--project",
        $script:MatrixProject,
        "--configuration",
        $script:Configuration,
        "--no-build",
        "--",
        "verify",
        $BundleRoot,
        "--json"
    )

    $result = Invoke-Checked -FileName "dotnet" -Arguments $arguments -WorkingDirectory $script:RuntimeRoot -AllowedExitCodes $AllowedExitCodes
    $json = ConvertTo-JsonObject -CommandResult $result -StepName "matrix verify"
    return [pscustomobject]@{
        command = $result.command
        exit_code = $result.exit_code
        json = $json
    }
}

if ([string]::IsNullOrWhiteSpace($RuntimeRoot)) {
    $RuntimeRoot = Resolve-FullPath (Join-Path $PSScriptRoot "../..")
}
else {
    $RuntimeRoot = Resolve-FullPath $RuntimeRoot
}

if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) {
    $ArtifactRoot = Resolve-FullPath (Join-Path $RuntimeRoot "artifacts/matrix/external-pilot-verify")
}
else {
    $ArtifactRoot = Resolve-FullPath $ArtifactRoot
}

New-Item -ItemType Directory -Force -Path $ArtifactRoot | Out-Null

$script:RuntimeRoot = $RuntimeRoot
$script:Configuration = $Configuration
$script:MatrixProject = Join-Path $RuntimeRoot "src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj"

if (-not $SkipBuild) {
    Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("build", $script:MatrixProject, "--configuration", $Configuration) `
        -WorkingDirectory $RuntimeRoot | Out-Null
}

$catalogOutput = Join-Path $ArtifactRoot "matrix-external-repo-pilot-set.json"
$catalogCommand = Invoke-Checked `
    -FileName "pwsh" `
    -Arguments @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        (Join-Path $RuntimeRoot "scripts/matrix/matrix-external-pilot-set.ps1"),
        "-RuntimeRoot",
        $RuntimeRoot,
        "-OutputPath",
        $catalogOutput
    ) `
    -WorkingDirectory $RuntimeRoot
$catalog = ConvertTo-JsonObject -CommandResult $catalogCommand -StepName "matrix external pilot catalog"

$pilotResults = @()
foreach ($pilot in @($catalog.pilots)) {
    $pilotId = [string] $pilot.pilot_id
    $bundleRoot = Join-Path (Join-Path $ArtifactRoot "pilots") $pilotId
    New-PilotBundle -Pilot $pilot -BundleRoot $bundleRoot
    $verify = Invoke-MatrixVerify -BundleRoot $bundleRoot -AllowedExitCodes @(0)
    if ($verify.json.status -ne "verified" -or $verify.json.trust_chain_hardening.gates_satisfied -ne $true) {
        throw "Matrix verify did not pass for pilot $pilotId."
    }

    $pilotResults += [pscustomobject]@{
        pilot_id = $pilotId
        shape = [string] $pilot.shape
        artifact_root = "pilots/$pilotId"
        verify_status = [string] $verify.json.status
        verification_posture = [string] $verify.json.verification_posture
        exit_code = [int] $verify.exit_code
        reason_codes = @($verify.json.reason_codes)
        required_artifacts_present = [int] $verify.json.required_artifacts.present_count
        trust_chain_gates_satisfied = [bool] $verify.json.trust_chain_hardening.gates_satisfied
    }
}

$failurePilot = @($catalog.pilots)[0]
$failureBundleRoot = Join-Path $ArtifactRoot "failure-reason-code-probe"
New-PilotBundle -Pilot $failurePilot -BundleRoot $failureBundleRoot
Add-Content -LiteralPath (Convert-ToArtifactPath -Root $failureBundleRoot -RelativePath "project/shield-evidence.json") -Encoding UTF8 -Value "{`"mutated_after_manifest`":true}"
$failureVerify = Invoke-MatrixVerify -BundleRoot $failureBundleRoot -AllowedExitCodes @(1)
$failureReasonCodes = @($failureVerify.json.reason_codes)
if ("hash_mismatch" -notin $failureReasonCodes) {
    throw "Failure probe did not emit expected hash_mismatch reason code."
}

$checkpoint = [ordered]@{
    schema_version = "matrix-cross-platform-verify-pilot-checkpoint.v0"
    generated_at_utc = [DateTimeOffset]::UtcNow.ToString("O")
    runtime_root = "<redacted-runtime-root>"
    artifact_root = "."
    platform = [ordered]@{
        os_description = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
        framework_description = [System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
        process_architecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    }
    pilot_set = [ordered]@{
        path = "docs/matrix/examples/matrix-external-repo-pilot-set.v0.example.json"
        schema_version = [string] $catalog.schema_version
        pilot_count = [int] $catalog.pilot_count
        covered_shapes = @($catalog.covered_shapes)
    }
    verified_pilot_count = @($pilotResults).Count
    pilots = $pilotResults
    failure_probe = [ordered]@{
        pilot_id = [string] $failurePilot.pilot_id
        verify_status = [string] $failureVerify.json.status
        verification_posture = [string] $failureVerify.json.verification_posture
        exit_code = [int] $failureVerify.exit_code
        reason_codes = $failureReasonCodes
        expected_reason_code = "hash_mismatch"
    }
    privacy = [ordered]@{
        summary_only = $true
        source_included = $false
        raw_diff_included = $false
        prompt_included = $false
        model_response_included = $false
        secrets_included = $false
        credentials_included = $false
        customer_payload_included = $false
        hosted_upload_required = $false
    }
    public_claims = [ordered]@{
        certification = $false
        hosted_verification = $false
        public_leaderboard = $false
        model_safety_benchmark = $false
        semantic_correctness = $false
        operating_system_sandbox = $false
        automatic_rollback = $false
    }
}

$checkpointPath = Join-Path $ArtifactRoot "matrix-cross-platform-verify-pilot-checkpoint.json"
$checkpointJson = $checkpoint | ConvertTo-Json -Depth 100
$checkpointJson | Set-Content -LiteralPath $checkpointPath -Encoding UTF8
$checkpointJson

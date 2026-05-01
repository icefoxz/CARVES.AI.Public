[CmdletBinding()]
param(
    [ValidateSet("Project", "Installed")]
    [string] $ToolMode = "Project",
    [string] $RuntimeRoot = "",
    [string] $WorkRoot = "",
    [string] $ArtifactRoot = "",
    [string] $Configuration = "Release",
    [string] $GuardCommand = "",
    [string] $HandoffCommand = "",
    [string] $AuditCommand = "",
    [string] $ShieldCommand = "",
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

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)][string[]] $Arguments,
        [Parameter(Mandatory = $true)][string] $WorkingDirectory
    )

    return Invoke-Checked -FileName "git" -Arguments $Arguments -WorkingDirectory $WorkingDirectory
}

function Invoke-Guard {
    param(
        [Parameter(Mandatory = $true)][string[]] $Arguments,
        [Parameter(Mandatory = $true)][string] $OutputPath,
        [int[]] $AllowedExitCodes = @(0)
    )

    if ($script:ToolMode -eq "Project") {
        $commandArguments = @(
            "run",
            "--no-build",
            "--configuration",
            $script:Configuration,
            "--project",
            $script:GuardProject,
            "--",
            "--repo-root",
            $script:TargetRepo
        ) + $Arguments
        return Invoke-Checked -FileName "dotnet" -Arguments $commandArguments -WorkingDirectory $script:RuntimeRoot -OutputPath $OutputPath -AllowedExitCodes $AllowedExitCodes
    }

    $installedArguments = @("--repo-root", $script:TargetRepo) + $Arguments
    return Invoke-Checked -FileName $script:GuardCommand -Arguments $installedArguments -WorkingDirectory $script:TargetRepo -OutputPath $OutputPath -AllowedExitCodes $AllowedExitCodes
}

function Invoke-Handoff {
    param(
        [Parameter(Mandatory = $true)][string[]] $Arguments,
        [Parameter(Mandatory = $true)][string] $OutputPath,
        [int[]] $AllowedExitCodes = @(0)
    )

    if ($script:ToolMode -eq "Project") {
        $commandArguments = @(
            "run",
            "--no-build",
            "--configuration",
            $script:Configuration,
            "--project",
            $script:HandoffProject,
            "--",
            "--repo-root",
            $script:TargetRepo
        ) + $Arguments
        return Invoke-Checked -FileName "dotnet" -Arguments $commandArguments -WorkingDirectory $script:RuntimeRoot -OutputPath $OutputPath -AllowedExitCodes $AllowedExitCodes
    }

    $installedArguments = @("--repo-root", $script:TargetRepo) + $Arguments
    return Invoke-Checked -FileName $script:HandoffCommand -Arguments $installedArguments -WorkingDirectory $script:TargetRepo -OutputPath $OutputPath -AllowedExitCodes $AllowedExitCodes
}

function Invoke-Audit {
    param(
        [Parameter(Mandatory = $true)][string[]] $Arguments,
        [Parameter(Mandatory = $true)][string] $OutputPath,
        [int[]] $AllowedExitCodes = @(0)
    )

    if ($script:ToolMode -eq "Project") {
        $commandArguments = @(
            "run",
            "--no-build",
            "--configuration",
            $script:Configuration,
            "--project",
            $script:AuditProject,
            "--"
        ) + $Arguments
        return Invoke-Checked -FileName "dotnet" -Arguments $commandArguments -WorkingDirectory $script:TargetRepo -OutputPath $OutputPath -AllowedExitCodes $AllowedExitCodes
    }

    return Invoke-Checked -FileName $script:AuditCommand -Arguments $Arguments -WorkingDirectory $script:TargetRepo -OutputPath $OutputPath -AllowedExitCodes $AllowedExitCodes
}

function Invoke-Shield {
    param(
        [Parameter(Mandatory = $true)][string[]] $Arguments,
        [Parameter(Mandatory = $true)][string] $OutputPath,
        [int[]] $AllowedExitCodes = @(0)
    )

    if ($script:ToolMode -eq "Project") {
        $commandArguments = @(
            "run",
            "--no-build",
            "--configuration",
            $script:Configuration,
            "--project",
            $script:ShieldProject,
            "--",
            "--repo-root",
            $script:TargetRepo
        ) + $Arguments
        return Invoke-Checked -FileName "dotnet" -Arguments $commandArguments -WorkingDirectory $script:RuntimeRoot -OutputPath $OutputPath -AllowedExitCodes $AllowedExitCodes
    }

    $installedArguments = @("--repo-root", $script:TargetRepo) + $Arguments
    return Invoke-Checked -FileName $script:ShieldCommand -Arguments $installedArguments -WorkingDirectory $script:TargetRepo -OutputPath $OutputPath -AllowedExitCodes $AllowedExitCodes
}

function Write-ReadyHandoffPacket {
    param(
        [Parameter(Mandatory = $true)][string] $PacketPath,
        [Parameter(Mandatory = $true)][string] $GuardRunId
    )

    $handoffId = "HND-MATRIX-" + [Guid]::NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant()
    $createdAt = [DateTimeOffset]::UtcNow.ToString("O")
    $payload = @"
{
  "schema_version": "carves-continuity-handoff.v1",
  "handoff_id": "$handoffId",
  "created_at_utc": "$createdAt",
  "producer": {
    "agent": "matrix-e2e-smoke",
    "tool": "carves-handoff",
    "version": "local"
  },
  "repo": {
    "name": "external-target",
    "path_hint": ".",
    "branch": "main"
  },
  "resume_status": "ready",
  "current_objective": "Review the matrix smoke artifacts and keep the Guard decision linked to this handoff packet.",
  "current_cursor": {
    "kind": "guard-run",
    "id": "$GuardRunId"
  },
  "completed_facts": [
    {
      "statement": "Guard produced a local diff-only decision for the matrix smoke patch.",
      "evidence_refs": [
        "guard-run:$GuardRunId",
        ".ai/runtime/guard/decisions.jsonl"
      ],
      "confidence": "high"
    }
  ],
  "remaining_work": [
    {
      "action": "Inspect Audit evidence and Shield evaluation outputs before publishing the matrix proof."
    }
  ],
  "blocked_reasons": [],
  "must_not_repeat": [
    {
      "item": "Do not upload source code, raw diffs, prompts, model responses, secrets, credentials, or private payloads as proof artifacts.",
      "reason": "The public matrix proof is summary-only and local-only."
    }
  ],
  "open_questions": [],
  "decision_refs": [
    "guard-run:$GuardRunId"
  ],
  "evidence_refs": [
    {
      "kind": "guard",
      "ref": "guard-run:$GuardRunId",
      "summary": "Guard decision produced by the matrix smoke."
    }
  ],
  "context_refs": [
    {
      "ref": ".ai/runtime/guard/decisions.jsonl",
      "reason": "Append-only Guard decision readback for this matrix smoke.",
      "priority": 1
    }
  ],
  "recommended_next_action": {
    "action": "Run carves-audit evidence and carves-shield evaluate.",
    "rationale": "Audit-generated shield-evidence.v0 is the stable Shield input."
  },
  "confidence": "medium",
  "confidence_notes": [
    "This packet is generated by the matrix smoke after a real Guard decision is recorded."
  ]
}
"@

    $payload | Set-Content -Path $PacketPath -Encoding UTF8
}

function Write-GuardWorkflowFixture {
    param([Parameter(Mandatory = $true)][string] $RepositoryRoot)

    $workflowRoot = Join-Path $RepositoryRoot ".github/workflows"
    New-Item -ItemType Directory -Force -Path $workflowRoot | Out-Null
    @'
name: CARVES Guard

on:
  pull_request:

jobs:
  guard:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Check AI patch boundary
        run: carves-guard check --json
'@ | Set-Content -Path (Join-Path $workflowRoot "carves-guard.yml") -Encoding UTF8
}

if ([string]::IsNullOrWhiteSpace($RuntimeRoot)) {
    $RuntimeRoot = Resolve-FullPath (Join-Path $PSScriptRoot "../..")
}
else {
    $RuntimeRoot = Resolve-FullPath $RuntimeRoot
}

$workRootWasDefault = [string]::IsNullOrWhiteSpace($WorkRoot)
if ($workRootWasDefault) {
    $WorkRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("carves-matrix-e2e-" + [Guid]::NewGuid().ToString("N"))
}
$WorkRoot = Resolve-FullPath $WorkRoot

if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) {
    $ArtifactRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("carves-matrix-artifacts-" + [Guid]::NewGuid().ToString("N"))
}
$ArtifactRoot = Resolve-FullPath $ArtifactRoot
New-Item -ItemType Directory -Force -Path $WorkRoot, $ArtifactRoot | Out-Null

$script:ToolMode = $ToolMode
$script:RuntimeRoot = $RuntimeRoot
$script:Configuration = $Configuration
$script:ArtifactRoot = $ArtifactRoot
$script:TargetRepo = Join-Path $WorkRoot "external-target"
$script:GuardProject = Join-Path $RuntimeRoot "src/CARVES.Guard.Cli/Carves.Guard.Cli.csproj"
$script:HandoffProject = Join-Path $RuntimeRoot "src/CARVES.Handoff.Cli/Carves.Handoff.Cli.csproj"
$script:AuditProject = Join-Path $RuntimeRoot "src/CARVES.Audit.Cli/Carves.Audit.Cli.csproj"
$script:ShieldProject = Join-Path $RuntimeRoot "src/CARVES.Shield.Cli/Carves.Shield.Cli.csproj"
$script:GuardCommand = $GuardCommand
$script:HandoffCommand = $HandoffCommand
$script:AuditCommand = $AuditCommand
$script:ShieldCommand = $ShieldCommand

try {
    if ($ToolMode -eq "Project") {
        Invoke-Checked `
            -FileName "dotnet" `
            -Arguments @("build", (Join-Path $RuntimeRoot "CARVES.Runtime.sln"), "--configuration", $Configuration) `
            -WorkingDirectory $RuntimeRoot `
            -OutputPath (Join-Path $ArtifactRoot "dotnet-build.log") | Out-Null
    }
    else {
        foreach ($entry in @(
            [pscustomobject]@{ Name = "carves-guard"; Path = $GuardCommand },
            [pscustomobject]@{ Name = "carves-handoff"; Path = $HandoffCommand },
            [pscustomobject]@{ Name = "carves-audit"; Path = $AuditCommand },
            [pscustomobject]@{ Name = "carves-shield"; Path = $ShieldCommand }
        )) {
            if ([string]::IsNullOrWhiteSpace($entry.Path) -or -not (Test-Path -LiteralPath $entry.Path)) {
                throw "Installed tool command '$($entry.Name)' was not found: $($entry.Path)"
            }
        }
    }

    New-Item -ItemType Directory -Force -Path $TargetRepo | Out-Null
    Invoke-Git -Arguments @("-c", "init.defaultBranch=main", "init") -WorkingDirectory $TargetRepo | Out-Null
    Invoke-Git -Arguments @("config", "user.email", "matrix-smoke@example.invalid") -WorkingDirectory $TargetRepo | Out-Null
    Invoke-Git -Arguments @("config", "user.name", "CARVES Matrix Smoke") -WorkingDirectory $TargetRepo | Out-Null

    New-Item -ItemType Directory -Force -Path (Join-Path $TargetRepo "src"), (Join-Path $TargetRepo "tests") | Out-Null
    @'
namespace MatrixSmoke;

public static class App
{
    public static string Greeting() => "hello";
}
'@ | Set-Content -Path (Join-Path $TargetRepo "src/App.cs") -Encoding UTF8
    @'
namespace MatrixSmoke.Tests;

public static class AppTests
{
    public static bool Baseline() => true;
}
'@ | Set-Content -Path (Join-Path $TargetRepo "tests/AppTests.cs") -Encoding UTF8
    Invoke-Git -Arguments @("add", ".") -WorkingDirectory $TargetRepo | Out-Null
    Invoke-Git -Arguments @("commit", "-m", "baseline") -WorkingDirectory $TargetRepo | Out-Null

    $guardInit = Invoke-Guard -Arguments @("init", "--json") -OutputPath (Join-Path $ArtifactRoot "guard-init.json")
    $guardInitJson = Read-JsonStdout -CommandResult $guardInit -StepName "guard init"
    if ($guardInitJson.schema_version -ne "guard-init.v1") {
        throw "guard init returned unexpected schema: $($guardInitJson.schema_version)"
    }

    Invoke-Git -Arguments @("add", ".ai/guard-policy.json") -WorkingDirectory $TargetRepo | Out-Null
    Invoke-Git -Arguments @("commit", "-m", "add guard policy") -WorkingDirectory $TargetRepo | Out-Null

    Add-Content -Path (Join-Path $TargetRepo "src/App.cs") -Encoding UTF8 -Value "public static class MatrixSmokePatch { public static int Count() => 1; }"
    Add-Content -Path (Join-Path $TargetRepo "tests/AppTests.cs") -Encoding UTF8 -Value "public static class MatrixSmokePatchTests { public static bool CountTest() => MatrixSmokePatch.Count() == 1; }"

    $guardCheck = Invoke-Guard -Arguments @("check", "--json") -OutputPath (Join-Path $ArtifactRoot "guard-check.json")
    $guardCheckJson = Read-JsonStdout -CommandResult $guardCheck -StepName "guard check"
    if ($guardCheckJson.decision -ne "allow") {
        throw "Expected guard check to allow the matrix smoke patch. Decision: $($guardCheckJson.decision)"
    }

    $guardRunId = [string] $guardCheckJson.run_id
    if ([string]::IsNullOrWhiteSpace($guardRunId)) {
        throw "Guard check did not emit run_id."
    }

    $decisionsPath = Join-Path $TargetRepo ".ai/runtime/guard/decisions.jsonl"
    if (-not (Test-Path -LiteralPath $decisionsPath)) {
        throw "Guard decisions read model was not written: $decisionsPath"
    }

    Copy-Item -LiteralPath $decisionsPath -Destination (Join-Path $ArtifactRoot "decisions.jsonl") -Force
    Write-GuardWorkflowFixture -RepositoryRoot $TargetRepo

    $handoffDraft = Invoke-Handoff -Arguments @("draft", "--json") -OutputPath (Join-Path $ArtifactRoot "handoff-draft.json")
    $handoffDraftJson = Read-JsonStdout -CommandResult $handoffDraft -StepName "handoff draft"
    if ($handoffDraftJson.packet_path -ne ".ai/handoff/handoff.json") {
        throw "Handoff draft did not use the default packet path."
    }

    $handoffPacketPath = Join-Path $TargetRepo ".ai/handoff/handoff.json"
    Write-ReadyHandoffPacket -PacketPath $handoffPacketPath -GuardRunId $guardRunId
    Copy-Item -LiteralPath $handoffPacketPath -Destination (Join-Path $ArtifactRoot "handoff.json") -Force

    $handoffInspect = Invoke-Handoff -Arguments @("inspect", "--json") -OutputPath (Join-Path $ArtifactRoot "handoff-inspect.json")
    $handoffInspectJson = Read-JsonStdout -CommandResult $handoffInspect -StepName "handoff inspect"
    if ($handoffInspectJson.readiness.decision -ne "ready") {
        throw "Expected handoff inspect readiness to be ready. Decision: $($handoffInspectJson.readiness.decision)"
    }

    $auditSummary = Invoke-Audit -Arguments @("summary", "--json") -OutputPath (Join-Path $ArtifactRoot "audit-summary.json")
    $auditSummaryJson = Read-JsonStdout -CommandResult $auditSummary -StepName "audit summary"
    if ($auditSummaryJson.event_count -lt 2) {
        throw "Expected Audit summary to discover Guard and Handoff evidence."
    }

    $auditTimeline = Invoke-Audit -Arguments @("timeline", "--json") -OutputPath (Join-Path $ArtifactRoot "audit-timeline.json")
    $auditTimelineJson = Read-JsonStdout -CommandResult $auditTimeline -StepName "audit timeline"
    if ($auditTimelineJson.event_count -lt 2) {
        throw "Expected Audit timeline to include Guard and Handoff events."
    }

    $auditExplain = Invoke-Audit -Arguments @("explain", $guardRunId, "--json") -OutputPath (Join-Path $ArtifactRoot "audit-explain.json")
    $auditExplainJson = Read-JsonStdout -CommandResult $auditExplain -StepName "audit explain"
    if ($auditExplainJson.found -ne $true) {
        throw "Audit explain did not find Guard run $guardRunId."
    }

    $auditEvidence = Invoke-Audit -Arguments @("evidence", "--json", "--output", ".carves/shield-evidence.json") -OutputPath (Join-Path $ArtifactRoot "audit-evidence.json")
    $auditEvidenceJson = Read-JsonStdout -CommandResult $auditEvidence -StepName "audit evidence"
    if ($auditEvidenceJson.schema_version -ne "shield-evidence.v0") {
        throw "Audit evidence emitted unexpected schema: $($auditEvidenceJson.schema_version)"
    }

    $shieldEvidencePath = Join-Path $TargetRepo ".carves/shield-evidence.json"
    if (-not (Test-Path -LiteralPath $shieldEvidencePath)) {
        throw "Audit evidence output file was not written: $shieldEvidencePath"
    }

    Copy-Item -LiteralPath $shieldEvidencePath -Destination (Join-Path $ArtifactRoot "shield-evidence.json") -Force

    $shieldEvaluate = Invoke-Shield `
        -Arguments @("evaluate", ".carves/shield-evidence.json", "--json", "--output", "combined") `
        -OutputPath (Join-Path $ArtifactRoot "shield-evaluate.json")
    $shieldEvaluateJson = Read-JsonStdout -CommandResult $shieldEvaluate -StepName "shield evaluate"
    if ($shieldEvaluateJson.status -ne "ok") {
        throw "Shield evaluate did not return ok. Status: $($shieldEvaluateJson.status)"
    }

    $badgeSvgPath = Join-Path $ArtifactRoot "shield-badge.svg"
    $shieldBadge = Invoke-Shield `
        -Arguments @("badge", ".carves/shield-evidence.json", "--json", "--output", $badgeSvgPath) `
        -OutputPath (Join-Path $ArtifactRoot "shield-badge.json")
    $shieldBadgeJson = Read-JsonStdout -CommandResult $shieldBadge -StepName "shield badge"
    if ($shieldBadgeJson.status -ne "ok" -or -not (Test-Path -LiteralPath $badgeSvgPath)) {
        throw "Shield badge did not produce an ok JSON result and SVG badge."
    }

    $summary = [pscustomobject]@{
        smoke = "matrix_e2e"
        tool_mode = $ToolMode.ToLowerInvariant()
        target_repository = "<redacted-target-repository>"
        artifact_root = "."
        guard_run_id = $guardRunId
        artifacts = [pscustomobject]@{
            guard_init = "guard-init.json"
            guard_check = "guard-check.json"
            guard_decisions = "decisions.jsonl"
            handoff_packet = "handoff.json"
            handoff_inspect = "handoff-inspect.json"
            audit_summary = "audit-summary.json"
            audit_timeline = "audit-timeline.json"
            audit_explain = "audit-explain.json"
            shield_evidence = "shield-evidence.json"
            shield_evaluate = "shield-evaluate.json"
            shield_badge_json = "shield-badge.json"
            shield_badge_svg = "shield-badge.svg"
        }
        matrix = [pscustomobject]@{
            proof_role = "composition_orchestrator"
            scoring_owner = "shield"
            alters_shield_score = $false
            consumed_shield_evidence_artifact = "shield-evidence.json"
            shield_evaluation_artifact = "shield-evaluate.json"
            shield_badge_json_artifact = "shield-badge.json"
            shield_badge_svg_artifact = "shield-badge.svg"
            trust_chain_hardening = [pscustomobject]@{
                audit_evidence_integrity = "complete_card_796"
                guard_deletion_replacement_honesty = "complete_card_797"
                shield_evidence_contract_alignment = "complete_card_798"
                guard_audit_store_multiprocess_durability = "complete_card_799"
                handoff_completed_state_semantics = "complete_card_800"
                matrix_shield_proof_bridge_claim_boundary = "complete_card_801"
                large_log_streaming_output_boundaries = "complete_card_802"
                handoff_reference_freshness_portability = "complete_card_803"
                usability_coverage_cleanup = "complete_card_804"
                release_checkpoint = "complete_card_805"
                public_rating_claim = "local_self_check_only"
                public_rating_claims_allowed = "limited_to_local_self_check"
            }
        }
        guard = [pscustomobject]@{
            decision = $guardCheckJson.decision
            changed_file_count = $guardCheckJson.patch_stats.changed_file_count
            requires_runtime_task_truth = $guardCheckJson.requires_runtime_task_truth
        }
        handoff = [pscustomobject]@{
            packet_path = ".ai/handoff/handoff.json"
            readiness = $handoffInspectJson.readiness.decision
            linked_guard_run = "guard-run:$guardRunId"
        }
        audit = [pscustomobject]@{
            event_count = $auditSummaryJson.event_count
            confidence_posture = $auditSummaryJson.confidence_posture
            evidence_schema = $auditEvidenceJson.schema_version
        }
        shield = [pscustomobject]@{
            status = $shieldEvaluateJson.status
            standard_label = $shieldEvaluateJson.standard.label
            lite_score = $shieldEvaluateJson.lite.score
            lite_band = $shieldEvaluateJson.lite.band
            consumed_evidence_sha256 = $shieldEvaluateJson.consumed_evidence_sha256
            badge_message = $shieldBadgeJson.badge.message
        }
        privacy = [pscustomobject]@{
            source_included = $auditEvidenceJson.privacy.source_included
            raw_diff_included = $auditEvidenceJson.privacy.raw_diff_included
            prompt_included = $auditEvidenceJson.privacy.prompt_included
            secrets_included = $auditEvidenceJson.privacy.secrets_included
            upload_intent = $auditEvidenceJson.privacy.upload_intent
            hosted_api_required = $false
            provider_secrets_required = $false
            source_upload_required = $false
            raw_diff_upload_required = $false
            prompt_upload_required = $false
            model_response_upload_required = $false
        }
        public_claims = [pscustomobject]@{
            certification = $false
            public_leaderboard = $false
            hosted_verification = $false
            os_sandbox_claim = $false
        }
    }

    $summaryJson = $summary | ConvertTo-Json -Depth 100
    $summaryJson | Set-Content -Path (Join-Path $ArtifactRoot "matrix-summary.json") -Encoding UTF8
    $summaryJson
}
finally {
    if (-not $Keep -and $workRootWasDefault -and (Test-Path -LiteralPath $WorkRoot)) {
        Assert-PathInside -PathValue $WorkRoot -RootValue ([System.IO.Path]::GetTempPath())
        Remove-Item -LiteralPath $WorkRoot -Recurse -Force
    }
}

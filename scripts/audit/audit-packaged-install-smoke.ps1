param(
    [string] $Version = "0.1.0-alpha.1",
    [string] $RuntimeRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string] $WorkRoot = (Join-Path ([System.IO.Path]::GetTempPath()) ("carves-audit-packaged-install-" + [Guid]::NewGuid().ToString("N"))),
    [switch] $Keep
)

$ErrorActionPreference = "Stop"

function Invoke-Checked {
    param(
        [string] $FileName,
        [string[]] $Arguments,
        [string] $WorkingDirectory
    )

    $start = [System.Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $FileName
    $start.WorkingDirectory = $WorkingDirectory
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.UseShellExecute = $false
    foreach ($argument in $Arguments) {
        [void] $start.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        throw "Command failed ($($process.ExitCode)): $FileName $($Arguments -join ' ')`nSTDOUT:`n$stdout`nSTDERR:`n$stderr"
    }

    [pscustomobject]@{
        command = "$FileName $($Arguments -join ' ')"
        exit_code = $process.ExitCode
        stdout = $stdout
        stderr = $stderr
    }
}

function Read-Json {
    param([string] $Text)
    $Text | ConvertFrom-Json -Depth 64
}

$packageRoot = Join-Path $WorkRoot "packages"
$toolRoot = Join-Path $WorkRoot "tool"
$targetRepo = Join-Path $WorkRoot "external-target"

try {
    New-Item -ItemType Directory -Force -Path $packageRoot, $toolRoot, $targetRepo | Out-Null

    $guardRoot = Join-Path $targetRepo ".ai\runtime\guard"
    $handoffRoot = Join-Path $targetRepo ".ai\handoff"
    New-Item -ItemType Directory -Force -Path $guardRoot, $handoffRoot | Out-Null

    @'
{"schema_version":1,"run_id":"GRD-SMOKE-1","recorded_at_utc":"2026-04-14T01:00:00Z","source":"guard-check","outcome":"allow","policy_id":"guard-policy.v1","summary":"Smoke allow decision.","requires_runtime_task_truth":false,"task_id":null,"execution_outcome":null,"execution_failure_kind":null,"changed_files":["src/App.cs"],"patch_stats":{"changed_file_count":1,"added_file_count":0,"modified_file_count":1,"deleted_file_count":0,"renamed_file_count":0,"binary_file_count":0,"total_additions":1,"total_deletions":0},"violations":[],"warnings":[],"evidence_refs":["guard://decision/GRD-SMOKE-1"]}
'@ | Set-Content -Path (Join-Path $guardRoot "decisions.jsonl") -Encoding UTF8

    @'
{
  "schema_version": "carves-continuity-handoff.v1",
  "handoff_id": "HND-SMOKE-1",
  "created_at_utc": "2026-04-14T02:00:00Z",
  "producer": { "agent": "smoke" },
  "repo": { "name": "external-target" },
  "resume_status": "ready",
  "current_objective": "Continue the smoke proof.",
  "current_cursor": { "kind": "manual", "id": "SMOKE" },
  "completed_facts": [
    { "statement": "Smoke input exists.", "evidence_refs": [ "docs/evidence.md" ], "confidence": "high" }
  ],
  "remaining_work": [
    { "action": "Inspect Audit output." }
  ],
  "blocked_reasons": [],
  "must_not_repeat": [
    { "item": "Do not rediscover default paths.", "reason": "Audit auto discovery should read them." }
  ],
  "open_questions": [],
  "decision_refs": [ "guard-run:GRD-SMOKE-1" ],
  "evidence_refs": [
    { "kind": "doc", "ref": "docs/evidence.md", "summary": "Smoke evidence." }
  ],
  "context_refs": [
    { "ref": "docs/context.md", "reason": "Smoke context.", "priority": 1 }
  ],
  "recommended_next_action": { "action": "Read Audit summary.", "rationale": "Smoke proof." },
  "confidence": "medium",
  "confidence_notes": [ "Smoke fixture." ]
}
'@ | Set-Content -Path (Join-Path $handoffRoot "handoff.json") -Encoding UTF8

    $packCore = Invoke-Checked "dotnet" @(
        "pack",
        (Join-Path $RuntimeRoot "src\CARVES.Audit.Core\Carves.Audit.Core.csproj"),
        "--configuration",
        "Release",
        "--output",
        $packageRoot,
        "/p:Version=$Version"
    ) $RuntimeRoot

    $packCli = Invoke-Checked "dotnet" @(
        "pack",
        (Join-Path $RuntimeRoot "src\CARVES.Audit.Cli\Carves.Audit.Cli.csproj"),
        "--configuration",
        "Release",
        "--output",
        $packageRoot,
        "/p:Version=$Version"
    ) $RuntimeRoot

    $install = Invoke-Checked "dotnet" @(
        "tool",
        "install",
        "--tool-path",
        $toolRoot,
        "--add-source",
        $packageRoot,
        "CARVES.Audit.Cli",
        "--version",
        $Version,
        "--ignore-failed-sources"
    ) $RuntimeRoot

    $toolCommand = Join-Path $toolRoot "carves-audit"
    $windowsToolCommand = "$toolCommand.exe"
    if (Test-Path $windowsToolCommand) {
        $toolCommand = $windowsToolCommand
    }

    $help = Invoke-Checked $toolCommand @("help") $targetRepo
    $summary = Invoke-Checked $toolCommand @("summary", "--json") $targetRepo
    $timeline = Invoke-Checked $toolCommand @("timeline", "--json") $targetRepo
    $explain = Invoke-Checked $toolCommand @("explain", "GRD-SMOKE-1", "--json") $targetRepo
    $evidence = Invoke-Checked $toolCommand @("evidence", "--json", "--output", ".carves/shield-evidence.json") $targetRepo

    $summaryJson = Read-Json $summary.stdout
    $timelineJson = Read-Json $timeline.stdout
    $explainJson = Read-Json $explain.stdout
    $evidenceJson = Read-Json $evidence.stdout
    $evidencePath = Join-Path $targetRepo ".carves\shield-evidence.json"
    if (-not (Test-Path $evidencePath)) {
        throw "Expected evidence output was not written: $evidencePath"
    }

    [pscustomobject]@{
        smoke = "audit_packaged_install"
        version = $Version
        local_package = (Join-Path $packageRoot "CARVES.Audit.Cli.$Version.nupkg")
        remote_registry_published = $false
        nuget_org_push_required = $false
        target_repository = $targetRepo
        default_guard_decisions_path = ".ai/runtime/guard/decisions.jsonl"
        default_handoff_packet_path = ".ai/handoff/handoff.json"
        evidence_output_path = ".carves/shield-evidence.json"
        commands = [pscustomobject]@{
            pack_core = [pscustomobject]@{ command = $packCore.command; exit_code = $packCore.exit_code }
            pack_cli = [pscustomobject]@{ command = $packCli.command; exit_code = $packCli.exit_code }
            install = [pscustomobject]@{ command = $install.command; exit_code = $install.exit_code }
            help = [pscustomobject]@{ command = $help.command; exit_code = $help.exit_code }
            summary = [pscustomobject]@{
                command = $summary.command
                exit_code = $summary.exit_code
                event_count = $summaryJson.event_count
                posture = $summaryJson.confidence_posture
            }
            timeline = [pscustomobject]@{
                command = $timeline.command
                exit_code = $timeline.exit_code
                event_count = $timelineJson.event_count
            }
            explain = [pscustomobject]@{
                command = $explain.command
                exit_code = $explain.exit_code
                found = $explainJson.found
            }
            evidence = [pscustomobject]@{
                command = $evidence.command
                exit_code = $evidence.exit_code
                schema_version = $evidenceJson.schema_version
                guard_enabled = $evidenceJson.dimensions.guard.enabled
                handoff_enabled = $evidenceJson.dimensions.handoff.enabled
                audit_enabled = $evidenceJson.dimensions.audit.enabled
            }
        }
    } | ConvertTo-Json -Depth 64
}
finally {
    if (-not $Keep -and (Test-Path $WorkRoot)) {
        Remove-Item -LiteralPath $WorkRoot -Recurse -Force
    }
}

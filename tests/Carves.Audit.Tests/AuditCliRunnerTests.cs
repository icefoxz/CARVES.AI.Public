using System.Text.Json;
using Carves.Audit.Core;

namespace Carves.Audit.Tests;

public sealed class AuditCliRunnerTests
{
    [Fact]
    public void PublicServiceName_DoesNotExposeAlphaImplementationName()
    {
        var service = new AuditService();

        Assert.Equal("AuditService", service.GetType().Name);
        Assert.Null(typeof(AuditService).Assembly.GetType("Carves.Audit.Core.AuditAlphaService"));
    }

    [Fact]
    public void Summary_ReadsGuardDecisionJsonlAndExplicitHandoffPacket()
    {
        using var workspace = new TemporaryWorkspace();
        var guardPath = workspace.WriteFile(".ai/runtime/guard/decisions.jsonl", string.Join(
            Environment.NewLine,
            GuardDecisionJson("GRD-1", "allow", "guard-check", "2026-04-14T01:00:00Z"),
            "{ malformed",
            """{"schema_version":999,"run_id":"future"}""",
            GuardDecisionJson("GRD-2", "block", "guard-run", "2026-04-14T02:00:00Z")));
        var handoffPath = workspace.WriteFile("handoff.json", HandoffPacketJson("HND-1", "ready", "medium", "2026-04-14T03:00:00Z"));

        var result = Capture(() => AuditCliRunner.Run([
            "summary",
            "--json",
            "--guard-decisions",
            guardPath,
            "--handoff",
            handoffPath,
        ]));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("carves-audit-summary.v1", root.GetProperty("schema_version").GetString());
        Assert.Equal("degraded", root.GetProperty("confidence_posture").GetString());
        Assert.Equal(3, root.GetProperty("event_count").GetInt32());
        Assert.Equal(1, root.GetProperty("guard").GetProperty("allow_count").GetInt32());
        Assert.Equal(1, root.GetProperty("guard").GetProperty("block_count").GetInt32());
        Assert.Equal("GRD-2", root.GetProperty("guard").GetProperty("latest_run_id").GetString());
        Assert.True(root.GetProperty("guard").GetProperty("diagnostics").GetProperty("is_degraded").GetBoolean());
        Assert.Equal(1, root.GetProperty("handoff").GetProperty("loaded_packet_count").GetInt32());
        Assert.Equal("HND-1", root.GetProperty("handoff").GetProperty("latest_handoff_id").GetString());
    }

    [Fact]
    public void Timeline_OrdersGuardAndHandoffEvents()
    {
        using var workspace = new TemporaryWorkspace();
        var guardPath = workspace.WriteFile(".ai/runtime/guard/decisions.jsonl", GuardDecisionJson("GRD-1", "review", "guard-check", "2026-04-14T02:00:00Z"));
        var handoffPath = workspace.WriteFile("handoff.json", HandoffPacketJson("HND-1", "blocked", "low", "2026-04-14T01:00:00Z"));

        var result = Capture(() => AuditCliRunner.Run([
            "timeline",
            "--json",
            "--guard-decisions",
            guardPath,
            "--handoff",
            handoffPath,
        ]));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var events = document.RootElement.GetProperty("events");
        Assert.Equal("HND-1", events[0].GetProperty("subject_id").GetString());
        Assert.Equal("GRD-1", events[1].GetProperty("subject_id").GetString());
    }

    [Fact]
    public void Explain_FindsGuardRunById()
    {
        using var workspace = new TemporaryWorkspace();
        var guardPath = workspace.WriteFile(".ai/runtime/guard/decisions.jsonl", GuardDecisionJson("GRD-1", "block", "guard-check", "2026-04-14T01:00:00Z"));

        var result = Capture(() => AuditCliRunner.Run([
            "explain",
            "GRD-1",
            "--json",
            "--guard-decisions",
            guardPath,
        ]));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.True(root.GetProperty("found").GetBoolean());
        Assert.False(root.GetProperty("ambiguous").GetBoolean());
        Assert.Equal("guard", root.GetProperty("matches")[0].GetProperty("source_product").GetString());
        Assert.Equal("block", root.GetProperty("matches")[0].GetProperty("status").GetString());
    }

    [Fact]
    public void Summary_MissingExplicitHandoffPathReturnsInputError()
    {
        using var workspace = new TemporaryWorkspace();
        var guardPath = workspace.WriteFile(".ai/runtime/guard/decisions.jsonl", GuardDecisionJson("GRD-1", "allow", "guard-check", "2026-04-14T01:00:00Z"));

        var result = Capture(() => AuditCliRunner.Run([
            "summary",
            "--json",
            "--guard-decisions",
            guardPath,
            "--handoff",
            Path.Combine(workspace.RootPath, "missing.json"),
        ]));

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal("input_error", document.RootElement.GetProperty("confidence_posture").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("handoff").GetProperty("input_error_count").GetInt32());
    }

    [Fact]
    public void Summary_AutoDiscoversDefaultGuardAndHandoffInputs()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/runtime/guard/decisions.jsonl", GuardDecisionJson("GRD-AUTO", "allow", "guard-check", "2026-04-14T01:00:00Z"));
        workspace.WriteFile(".ai/handoff/handoff.json", HandoffPacketJson("HND-AUTO", "ready", "medium", "2026-04-14T03:00:00Z"));

        var result = CaptureIn(workspace.RootPath, () => AuditCliRunner.Run(["summary", "--json"]));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("complete_for_supplied_inputs", root.GetProperty("confidence_posture").GetString());
        Assert.Equal(2, root.GetProperty("event_count").GetInt32());
        Assert.Equal(1, root.GetProperty("guard").GetProperty("total_count").GetInt32());
        Assert.Equal(1, root.GetProperty("handoff").GetProperty("loaded_packet_count").GetInt32());
        Assert.Equal("HND-AUTO", root.GetProperty("handoff").GetProperty("latest_handoff_id").GetString());
    }

    [Fact]
    public void Summary_EmptyRepositoryUsesDefaultInputsWithoutCrashing()
    {
        using var workspace = new TemporaryWorkspace();

        var result = CaptureIn(workspace.RootPath, () => AuditCliRunner.Run(["summary", "--json"]));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("empty", root.GetProperty("confidence_posture").GetString());
        Assert.Equal("missing", root.GetProperty("guard").GetProperty("input_status").GetString());
        Assert.Equal(0, root.GetProperty("handoff").GetProperty("loaded_packet_count").GetInt32());
        Assert.Equal("missing", root.GetProperty("handoff").GetProperty("inputs")[0].GetProperty("input_status").GetString());
    }

    [Fact]
    public void Summary_GuardOnlyDefaultDiscoveryKeepsMissingHandoffNonFatal()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/runtime/guard/decisions.jsonl", GuardDecisionJson("GRD-ONLY", "allow", "guard-check", "2026-04-14T01:00:00Z"));

        var result = CaptureIn(workspace.RootPath, () => AuditCliRunner.Run(["summary", "--json"]));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("complete_for_supplied_inputs", root.GetProperty("confidence_posture").GetString());
        Assert.Equal(1, root.GetProperty("guard").GetProperty("total_count").GetInt32());
        Assert.Equal(0, root.GetProperty("handoff").GetProperty("input_error_count").GetInt32());
        Assert.Equal("missing", root.GetProperty("handoff").GetProperty("inputs")[0].GetProperty("input_status").GetString());
    }

    [Fact]
    public void Summary_HandoffOnlyDefaultDiscoveryKeepsMissingGuardNonFatal()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/handoff/handoff.json", HandoffPacketJson("HND-ONLY", "ready", "medium", "2026-04-14T03:00:00Z"));

        var result = CaptureIn(workspace.RootPath, () => AuditCliRunner.Run(["summary", "--json"]));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("complete_for_supplied_inputs", root.GetProperty("confidence_posture").GetString());
        Assert.Equal("missing", root.GetProperty("guard").GetProperty("input_status").GetString());
        Assert.Equal(1, root.GetProperty("handoff").GetProperty("loaded_packet_count").GetInt32());
    }

    [Fact]
    public void Summary_MalformedDefaultHandoffPacketDegradesWithoutFatalExit()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/handoff/handoff.json", "{ malformed");

        var result = CaptureIn(workspace.RootPath, () => AuditCliRunner.Run(["summary", "--json"]));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("degraded", root.GetProperty("confidence_posture").GetString());
        Assert.Equal(0, root.GetProperty("handoff").GetProperty("input_error_count").GetInt32());
        Assert.Equal("degraded", root.GetProperty("handoff").GetProperty("inputs")[0].GetProperty("input_status").GetString());
        Assert.Equal("handoff_malformed_json", root.GetProperty("handoff").GetProperty("inputs")[0].GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void Summary_FutureHandoffSchemaDegradesAsWarning()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/handoff/handoff.json", """
        {
          "schema_version": "carves-continuity-handoff.v99",
          "handoff_id": "HND-FUTURE",
          "created_at_utc": "2026-04-14T03:00:00Z"
        }
        """);

        var result = CaptureIn(workspace.RootPath, () => AuditCliRunner.Run(["summary", "--json"]));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var input = document.RootElement.GetProperty("handoff").GetProperty("inputs")[0];
        Assert.Equal("degraded", document.RootElement.GetProperty("confidence_posture").GetString());
        Assert.Equal("degraded", input.GetProperty("input_status").GetString());
        Assert.Equal("warning", input.GetProperty("diagnostics")[0].GetProperty("severity").GetString());
        Assert.Equal("handoff_unsupported_schema", input.GetProperty("diagnostics")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void Timeline_UsesAutoDiscoveredInputs()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/runtime/guard/decisions.jsonl", GuardDecisionJson("GRD-TL", "review", "guard-check", "2026-04-14T02:00:00Z"));
        workspace.WriteFile(".ai/handoff/handoff.json", HandoffPacketJson("HND-TL", "ready", "medium", "2026-04-14T01:00:00Z"));

        var result = CaptureIn(workspace.RootPath, () => AuditCliRunner.Run(["timeline", "--json"]));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var events = document.RootElement.GetProperty("events");
        Assert.Equal("HND-TL", events[0].GetProperty("subject_id").GetString());
        Assert.Equal("GRD-TL", events[1].GetProperty("subject_id").GetString());
    }

    [Fact]
    public void Explain_UsesAutoDiscoveredGuardDecision()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/runtime/guard/decisions.jsonl", GuardDecisionJson("GRD-EXPLAIN-AUTO", "block", "guard-check", "2026-04-14T01:00:00Z"));

        var result = CaptureIn(workspace.RootPath, () => AuditCliRunner.Run(["explain", "GRD-EXPLAIN-AUTO", "--json"]));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.True(root.GetProperty("found").GetBoolean());
        Assert.Equal("guard", root.GetProperty("matches")[0].GetProperty("source_product").GetString());
    }

    [Fact]
    public void Evidence_EmitsShieldEvidenceV0WithoutScoring()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/runtime/guard/decisions.jsonl", string.Join(
            Environment.NewLine,
            GuardDecisionJson("GRD-EVI-1", "allow", "guard-check", "2026-04-14T01:00:00Z"),
            GuardDecisionJson("GRD-EVI-2", "block", "guard-check", "2026-04-14T02:00:00Z")));
        workspace.WriteFile(".ai/handoff/handoff.json", HandoffPacketJson("HND-EVI", "ready", "medium", "2026-04-14T03:00:00Z"));
        workspace.WriteFile(".ai/guard-policy.json", GuardPolicyJson());

        var result = CaptureIn(workspace.RootPath, () => AuditCliRunner.Run(["evidence", "--json"]));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("shield-evidence.v0", root.GetProperty("schema_version").GetString());
        Assert.Equal("both", root.GetProperty("mode_hint").GetString());
        Assert.True(root.GetProperty("dimensions").GetProperty("guard").GetProperty("enabled").GetBoolean());
        Assert.True(root.GetProperty("dimensions").GetProperty("handoff").GetProperty("enabled").GetBoolean());
        Assert.True(root.GetProperty("dimensions").GetProperty("audit").GetProperty("enabled").GetBoolean());
        Assert.Equal(1, root.GetProperty("dimensions").GetProperty("guard").GetProperty("decisions").GetProperty("allow_count").GetInt32());
        Assert.Equal(1, root.GetProperty("dimensions").GetProperty("guard").GetProperty("decisions").GetProperty("block_count").GetInt32());
        Assert.False(root.GetProperty("privacy").GetProperty("source_included").GetBoolean());
        Assert.False(root.GetProperty("privacy").GetProperty("raw_diff_included").GetBoolean());
        Assert.False(root.TryGetProperty("standard", out _));
        Assert.False(root.TryGetProperty("lite", out _));
        Assert.False(root.TryGetProperty("badge", out _));
    }

    [Fact]
    public void Evidence_DoesNotInventAuditIntegrityCoverageOrReports()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/runtime/guard/decisions.jsonl", string.Join(
            Environment.NewLine,
            GuardDecisionJson("GRD-AUDIT-BLOCK", "block", "guard-check", "2026-04-14T01:00:00Z"),
            GuardDecisionJson("GRD-AUDIT-REVIEW", "review", "guard-check", "2026-04-14T02:00:00Z")));
        workspace.WriteFile(".ai/handoff/handoff.json", HandoffPacketJson("HND-AUDIT", "ready", "medium", "2026-04-14T03:00:00Z"));

        var result = CaptureIn(workspace.RootPath, () => AuditCliRunner.Run(["evidence", "--json"]));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var audit = document.RootElement.GetProperty("dimensions").GetProperty("audit");
        var log = audit.GetProperty("log");
        Assert.True(log.GetProperty("integrity_check_passed").GetBoolean());
        Assert.False(log.GetProperty("append_only_claimed").GetBoolean());

        var coverage = audit.GetProperty("coverage");
        Assert.Equal(1, coverage.GetProperty("block_decision_count").GetInt32());
        Assert.Equal(0, coverage.GetProperty("block_explain_covered_count").GetInt32());
        Assert.Equal(1, coverage.GetProperty("review_decision_count").GetInt32());
        Assert.Equal(0, coverage.GetProperty("review_explain_covered_count").GetInt32());

        var reports = audit.GetProperty("reports");
        Assert.False(reports.GetProperty("summary_generated_in_window").GetBoolean());
        Assert.False(reports.GetProperty("change_report_generated_in_window").GetBoolean());
        Assert.False(reports.GetProperty("failure_pattern_distribution_present").GetBoolean());
    }

    [Fact]
    public void Evidence_WritesOutputFileWhenRequested()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/runtime/guard/decisions.jsonl", GuardDecisionJson("GRD-WRITE", "allow", "guard-check", "2026-04-14T01:00:00Z"));

        var result = CaptureIn(workspace.RootPath, () => AuditCliRunner.Run(["evidence", "--output", ".carves/shield-evidence.json"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("CARVES Audit evidence", result.StandardOutput, StringComparison.Ordinal);
        var evidencePath = Path.Combine(workspace.RootPath, ".carves", "shield-evidence.json");
        Assert.True(File.Exists(evidencePath));
        using var document = JsonDocument.Parse(File.ReadAllText(evidencePath));
        Assert.Equal("shield-evidence.v0", document.RootElement.GetProperty("schema_version").GetString());
    }

    [Fact]
    public void Evidence_RejectsOutputOutsideRepository()
    {
        using var workspace = new TemporaryWorkspace();
        var outsidePath = Path.Combine(Directory.GetParent(workspace.RootPath)!.FullName, $"shield-{Guid.NewGuid():N}.json");

        var result = CaptureIn(workspace.RootPath, () => AuditCliRunner.Run(["evidence", "--output", outsidePath]));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("must stay inside the repository", result.StandardError, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(outsidePath));
    }

    [Fact]
    public void Evidence_RejectsProtectedTruthOutputPath()
    {
        using var workspace = new TemporaryWorkspace();

        var result = CaptureIn(workspace.RootPath, () => AuditCliRunner.Run(["evidence", "--output", ".ai/tasks/shield-evidence.json"]));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("protected", result.StandardError, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(workspace.RootPath, ".ai", "tasks", "shield-evidence.json")));
    }

    [Fact]
    public void Summary_ReadsOnlyBoundedRecentGuardTailForLargeJsonl()
    {
        using var workspace = new TemporaryWorkspace();
        var startedAt = DateTimeOffset.Parse("2026-04-14T01:00:00Z");
        var lines = Enumerable.Range(0, AuditInputReader.MaxGuardLineCount + 2)
            .Select(index => GuardDecisionJson($"GRD-LARGE-{index:D4}", "allow", "guard-check", startedAt.AddMinutes(index).ToString("O")));
        workspace.WriteFile(".ai/runtime/guard/decisions.jsonl", string.Join(Environment.NewLine, lines));

        var result = CaptureIn(workspace.RootPath, () => AuditCliRunner.Run(["summary", "--json"]));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var guard = document.RootElement.GetProperty("guard");
        var diagnostics = guard.GetProperty("diagnostics");
        Assert.Equal(AuditInputReader.MaxGuardLineCount + 2, diagnostics.GetProperty("total_line_count").GetInt32());
        Assert.Equal(AuditInputReader.MaxGuardLineCount, diagnostics.GetProperty("effective_line_count").GetInt32());
        Assert.Equal(AuditInputReader.MaxGuardLineCount, diagnostics.GetProperty("loaded_record_count").GetInt32());
        Assert.Equal("degraded", guard.GetProperty("input_status").GetString());
        Assert.Equal("GRD-LARGE-1001", guard.GetProperty("latest_run_id").GetString());
    }

    [Fact]
    public void Summary_LoadsLargePatchMetadataWithinRecordByteLimit()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(
            ".ai/runtime/guard/decisions.jsonl",
            LargePatchGuardDecisionJson(
                "GRD-LARGE-PATCH",
                "review",
                "2026-04-14T02:00:00Z",
                changedFileCount: 400));

        var result = CaptureIn(workspace.RootPath, () => AuditCliRunner.Run(["summary", "--json"]));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var guard = document.RootElement.GetProperty("guard");
        var diagnostics = guard.GetProperty("diagnostics");
        Assert.Equal("loaded", guard.GetProperty("input_status").GetString());
        Assert.Equal(1, guard.GetProperty("total_count").GetInt32());
        Assert.Equal(1, guard.GetProperty("review_count").GetInt32());
        Assert.Equal(1, diagnostics.GetProperty("loaded_record_count").GetInt32());
        Assert.Equal(0, diagnostics.GetProperty("oversized_record_count").GetInt32());
        Assert.Equal(AuditInputReader.MaxGuardRecordByteCount, diagnostics.GetProperty("max_record_byte_count").GetInt32());
    }

    [Fact]
    public void Evidence_SkipsOversizedGuardDecisionRecordWithExplicitWarning()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(
            ".ai/runtime/guard/decisions.jsonl",
            string.Join(
                Environment.NewLine,
                OversizedGuardDecisionJson("GRD-OVERSIZED", "2026-04-14T01:00:00Z"),
                GuardDecisionJson("GRD-AFTER-OVERSIZED", "allow", "guard-check", "2026-04-14T02:00:00Z")));

        var result = CaptureIn(workspace.RootPath, () => AuditCliRunner.Run(["evidence", "--json"]));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        var records = root.GetProperty("dimensions").GetProperty("audit").GetProperty("records");
        Assert.Equal(1, records.GetProperty("record_count").GetInt32());
        Assert.Equal(1, records.GetProperty("oversized_record_count").GetInt32());
        Assert.Contains(root.GetProperty("provenance").GetProperty("warnings").EnumerateArray(), warning =>
            warning.GetString() == "guard_oversized_records:1");
    }

    [Fact]
    public void Evidence_EmptyRepositoryDisablesAllDimensions()
    {
        using var workspace = new TemporaryWorkspace();

        var result = CaptureIn(workspace.RootPath, () => AuditCliRunner.Run(["evidence", "--json"]));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var dimensions = document.RootElement.GetProperty("dimensions");
        Assert.False(dimensions.GetProperty("guard").GetProperty("enabled").GetBoolean());
        Assert.False(dimensions.GetProperty("handoff").GetProperty("enabled").GetBoolean());
        Assert.False(dimensions.GetProperty("audit").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void Evidence_PrivacyFieldsForbidRichPayloads()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/runtime/guard/decisions.jsonl", GuardDecisionJson("GRD-PRIVACY", "block", "guard-check", "2026-04-14T01:00:00Z"));

        var result = CaptureIn(workspace.RootPath, () => AuditCliRunner.Run(["evidence", "--json"]));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var privacy = document.RootElement.GetProperty("privacy");
        Assert.False(privacy.GetProperty("source_included").GetBoolean());
        Assert.False(privacy.GetProperty("raw_diff_included").GetBoolean());
        Assert.False(privacy.GetProperty("prompt_included").GetBoolean());
        Assert.False(privacy.GetProperty("secrets_included").GetBoolean());
        Assert.Equal("local_only", privacy.GetProperty("upload_intent").GetString());
        Assert.DoesNotContain("test evidence", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("model_response", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private_payload", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evidence_DetectsGuardCiCommandWithHeuristicStandaloneToolName()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".github/workflows/guard.yml", """
        name: Guard
        on: [pull_request]
        jobs:
          guard:
            runs-on: ubuntu-latest
            steps:
              - run: carves-guard check --json
        """);

        var result = CaptureIn(workspace.RootPath, () => AuditCliRunner.Run(["evidence", "--json"]));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var ci = document.RootElement.GetProperty("dimensions").GetProperty("guard").GetProperty("ci");
        Assert.True(ci.GetProperty("detected").GetBoolean());
        Assert.True(ci.GetProperty("guard_check_command_detected").GetBoolean());
        Assert.True(ci.GetProperty("fails_on_review_or_block").GetBoolean());
        Assert.Contains(ci.GetProperty("workflow_paths").EnumerateArray(), item =>
            item.GetString() == ".github/workflows/guard.yml");
    }

    [Fact]
    public void Evidence_ReportsMalformedAndFutureGuardRecords()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/runtime/guard/decisions.jsonl", string.Join(
            Environment.NewLine,
            GuardDecisionJson("GRD-OK", "allow", "guard-check", "2026-04-14T01:00:00Z"),
            "{ malformed",
            """{"schema_version":999,"run_id":"future"}"""));

        var result = CaptureIn(workspace.RootPath, () => AuditCliRunner.Run(["evidence", "--json"]));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var records = document.RootElement.GetProperty("dimensions").GetProperty("audit").GetProperty("records");
        Assert.Equal(1, records.GetProperty("record_count").GetInt32());
        Assert.Equal(1, records.GetProperty("malformed_record_count").GetInt32());
        Assert.Equal(1, records.GetProperty("future_schema_record_count").GetInt32());
        Assert.Contains(document.RootElement.GetProperty("provenance").GetProperty("warnings").EnumerateArray(), warning =>
            warning.GetString() == "guard_malformed_records:1");
    }

    [Fact]
    public void MissingCommandReturnsUsageExitCode()
    {
        var result = Capture(() => AuditCliRunner.Run([]));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Usage: carves-audit", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpReturnsZeroAndMentionsEvidence()
    {
        var result = Capture(() => AuditCliRunner.Run(["help"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("carves-audit evidence", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(".ai/runtime/guard/decisions.jsonl", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(".ai/handoff/handoff.json", result.StandardOutput, StringComparison.Ordinal);
    }

    private static string GuardDecisionJson(string runId, string outcome, string source, string recordedAtUtc)
    {
        return $$"""
        {"schema_version":1,"run_id":"{{runId}}","recorded_at_utc":"{{recordedAtUtc}}","source":"{{source}}","outcome":"{{outcome}}","policy_id":"guard-policy.v1","summary":"Guard {{outcome}} decision.","requires_runtime_task_truth":false,"task_id":null,"execution_outcome":null,"execution_failure_kind":null,"changed_files":["src/App.cs"],"patch_stats":{"changed_file_count":1,"added_file_count":0,"modified_file_count":1,"deleted_file_count":0,"renamed_file_count":0,"binary_file_count":0,"total_additions":3,"total_deletions":1},"violations":[{"rule_id":"policy.test","severity":"block","message":"blocked","file_path":"src/App.cs","evidence":"test evidence","evidence_ref":"guard://policy.test"}],"warnings":[],"evidence_refs":["guard://decision/{{runId}}"]}
        """;
    }

    private static string LargePatchGuardDecisionJson(string runId, string outcome, string recordedAtUtc, int changedFileCount)
    {
        var changedFiles = string.Join(
            ',',
            Enumerable.Range(0, changedFileCount).Select(index => $"\"src/generated/File{index:D4}.cs\""));
        return $$"""
        {"schema_version":1,"run_id":"{{runId}}","recorded_at_utc":"{{recordedAtUtc}}","source":"guard-check","outcome":"{{outcome}}","policy_id":"guard-policy.v1","summary":"Large patch metadata decision.","requires_runtime_task_truth":false,"task_id":null,"execution_outcome":null,"execution_failure_kind":null,"changed_files":[{{changedFiles}}],"patch_stats":{"changed_file_count":{{changedFileCount}},"added_file_count":0,"modified_file_count":{{changedFileCount}},"deleted_file_count":0,"renamed_file_count":0,"binary_file_count":0,"total_additions":24000,"total_deletions":1200},"violations":[],"warnings":[{"rule_id":"policy.large_patch","severity":"review","message":"large patch metadata fixture","file_path":"src/generated/File0000.cs","evidence":"summary-only large patch metadata","evidence_ref":"guard://policy.large_patch"}],"evidence_refs":["guard://decision/{{runId}}"]}
        """;
    }

    private static string OversizedGuardDecisionJson(string runId, string recordedAtUtc)
    {
        var padding = new string('x', AuditInputReader.MaxGuardRecordByteCount + 1);
        return $$"""
        {"schema_version":1,"run_id":"{{runId}}","recorded_at_utc":"{{recordedAtUtc}}","source":"guard-check","outcome":"review","policy_id":"guard-policy.v1","summary":"{{padding}}","requires_runtime_task_truth":false,"task_id":null,"execution_outcome":null,"execution_failure_kind":null,"changed_files":["src/Large.cs"],"patch_stats":{"changed_file_count":1,"added_file_count":0,"modified_file_count":1,"deleted_file_count":0,"renamed_file_count":0,"binary_file_count":0,"total_additions":1,"total_deletions":0},"violations":[],"warnings":[],"evidence_refs":["guard://decision/{{runId}}"]}
        """;
    }

    private static string HandoffPacketJson(string handoffId, string resumeStatus, string confidence, string createdAtUtc)
    {
        return $$"""
        {
          "schema_version": "carves-continuity-handoff.v1",
          "handoff_id": "{{handoffId}}",
          "created_at_utc": "{{createdAtUtc}}",
          "producer": { "agent": "test", "session_id": "session-1" },
          "repo": { "name": "external", "root_hint": "temp", "branch": "main", "base_commit": "unknown", "dirty_state": "dirty" },
          "resume_status": "{{resumeStatus}}",
          "current_objective": "Continue bounded work.",
          "current_cursor": { "kind": "manual", "id": "CUR-1", "title": "Cursor", "status": "in_progress", "scope": [] },
          "completed_facts": [],
          "remaining_work": [],
          "blocked_reasons": [],
          "must_not_repeat": [{ "text": "Do not repeat setup." }],
          "open_questions": [],
          "decision_refs": [],
          "evidence_refs": [{ "kind": "doc", "ref": "docs/evidence.md", "summary": "Evidence." }],
          "context_refs": [{ "ref": "docs/context.md", "reason": "Context.", "priority": 1 }],
          "recommended_next_action": { "action": "Continue.", "rationale": "Ready." },
          "confidence": "{{confidence}}",
          "confidence_notes": []
        }
        """;
    }

    private static string GuardPolicyJson()
    {
        return """
        {
          "schema_version": 1,
          "policy_id": "audit-evidence-policy",
          "path_policy": {
            "path_case": "case_insensitive",
            "allowed_path_prefixes": [ "src/", "tests/" ],
            "protected_path_prefixes": [ ".ai/tasks/", ".git/" ],
            "outside_allowed_action": "review",
            "protected_path_action": "block"
          },
          "change_budget": {
            "max_changed_files": 5,
            "max_total_additions": 200,
            "max_total_deletions": 200,
            "max_file_additions": 100,
            "max_file_deletions": 100,
            "max_renames": 2
          },
          "dependency_policy": {
            "manifest_paths": [ "*.csproj" ],
            "lockfile_paths": [ "packages.lock.json" ],
            "manifest_without_lockfile_action": "review",
            "lockfile_without_manifest_action": "review",
            "new_dependency_action": "review"
          },
          "change_shape": {
            "allow_rename_with_content_change": false,
            "allow_delete_without_replacement": false,
            "generated_path_prefixes": [ "bin/", "obj/" ],
            "generated_path_action": "review",
            "mixed_feature_and_refactor_action": "review",
            "require_tests_for_source_changes": true,
            "source_path_prefixes": [ "src/" ],
            "test_path_prefixes": [ "tests/" ],
            "missing_tests_action": "review"
          },
          "decision": {
            "fail_closed": true,
            "default_outcome": "allow",
            "review_is_passing": false,
            "emit_evidence": true
          }
        }
        """;
    }

    private static CapturedRun CaptureIn(string workingDirectory, Func<int> run)
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workingDirectory);
        try
        {
            return Capture(run);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    private static CapturedRun Capture(Func<int> run)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        Console.SetOut(standardOutput);
        Console.SetError(standardError);

        try
        {
            var exitCode = run();
            return new CapturedRun(exitCode, standardOutput.ToString(), standardError.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private sealed record CapturedRun(int ExitCode, string StandardOutput, string StandardError);
}

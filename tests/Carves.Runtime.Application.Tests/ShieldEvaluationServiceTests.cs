using Carves.Runtime.Application.Shield;

namespace Carves.Runtime.Application.Tests;

public sealed class ShieldEvaluationServiceTests
{
    [Fact]
    public void Evaluate_ProjectsStandardAndLiteFromSummaryEvidence()
    {
        var result = new ShieldEvaluationService().Evaluate(CompleteEvidence(), ShieldEvaluationOutput.Combined);

        Assert.True(result.IsOk);
        Assert.False(result.Certification);
        Assert.Equal("evidence_summary", result.PrivacyPosture);
        Assert.NotNull(result.Standard);
        Assert.Equal("CARVES G8.H8.A8 /30d PASS", result.Standard!.Label);
        Assert.Equal(8, result.Standard.Dimensions["guard"].NumericLevel);
        Assert.Equal(8, result.Standard.Dimensions["handoff"].NumericLevel);
        Assert.Equal(8, result.Standard.Dimensions["audit"].NumericLevel);
        Assert.Null(result.Standard.OverallScore);
        Assert.NotNull(result.Lite);
        Assert.Equal(90, result.Lite!.Score);
        Assert.Equal("strong", result.Lite.Band);
        Assert.Equal(36, result.Lite.DimensionContributions["guard"].Points);
        Assert.Equal(27, result.Lite.DimensionContributions["handoff"].Points);
        Assert.Equal(27, result.Lite.DimensionContributions["audit"].Points);
    }

    [Fact]
    public void Evaluate_AppliesCriticalCapToLiteScore()
    {
        var evidence = CompleteEvidence().Replace(
            "\"unresolved_block_count\": 0",
            "\"unresolved_block_count\": 1",
            StringComparison.Ordinal);

        var result = new ShieldEvaluationService().Evaluate(evidence, ShieldEvaluationOutput.Combined);

        Assert.True(result.IsOk);
        Assert.Equal("C", result.Standard!.Dimensions["guard"].Level);
        Assert.Contains("CG-04", result.Standard.CriticalGates);
        Assert.Equal("critical", result.Lite!.Band);
        Assert.True(result.Lite.Score <= 39);
        Assert.Contains("CG-04", result.Lite.CriticalGates);
    }

    [Fact]
    public void Evaluate_UncoveredAuditBlockDecisionsTriggerCriticalGate()
    {
        var evidence = CompleteEvidence()
            .Replace("\"append_only_claimed\": true", "\"append_only_claimed\": false", StringComparison.Ordinal)
            .Replace("\"block_explain_covered_count\": 2", "\"block_explain_covered_count\": 0", StringComparison.Ordinal)
            .Replace("\"review_explain_covered_count\": 4", "\"review_explain_covered_count\": 0", StringComparison.Ordinal)
            .Replace("\"summary_generated_in_window\": true", "\"summary_generated_in_window\": false", StringComparison.Ordinal);

        var result = new ShieldEvaluationService().Evaluate(evidence, ShieldEvaluationOutput.Combined);

        Assert.True(result.IsOk);
        Assert.Equal("C", result.Standard!.Dimensions["audit"].Level);
        Assert.Contains("CA-03", result.Standard.CriticalGates);
        Assert.Equal("critical", result.Lite!.Band);
        Assert.True(result.Lite.Score <= 39);
    }

    [Fact]
    public void Evaluate_UncoveredAuditReviewDecisionsCannotInflateAuditLevel()
    {
        var evidence = CompleteEvidence()
            .Replace("\"append_only_claimed\": true", "\"append_only_claimed\": false", StringComparison.Ordinal)
            .Replace("\"block_count\": 2", "\"block_count\": 0", StringComparison.Ordinal)
            .Replace("\"block_decision_count\": 2", "\"block_decision_count\": 0", StringComparison.Ordinal)
            .Replace("\"block_explain_covered_count\": 2", "\"block_explain_covered_count\": 0", StringComparison.Ordinal)
            .Replace("\"review_explain_covered_count\": 4", "\"review_explain_covered_count\": 0", StringComparison.Ordinal)
            .Replace("\"summary_generated_in_window\": true", "\"summary_generated_in_window\": false", StringComparison.Ordinal);

        var result = new ShieldEvaluationService().Evaluate(evidence, ShieldEvaluationOutput.Combined);

        Assert.True(result.IsOk);
        Assert.Empty(result.Standard!.CriticalGates);
        Assert.Equal(5, result.Standard.Dimensions["audit"].NumericLevel);
        Assert.DoesNotContain("CA-03", result.Standard.CriticalGates);
        Assert.True(result.Lite!.Score < 90);
    }

    [Fact]
    public void Evaluate_RejectsOptionalPrivacyFieldsWhenTheyAreTrue()
    {
        var evidence = CompleteEvidence().Replace(
            "\"secrets_included\": false,",
            """
            "secrets_included": false,
                "model_response_included": true,
            """,
            StringComparison.Ordinal);

        var result = new ShieldEvaluationService().Evaluate(evidence, ShieldEvaluationOutput.Combined);

        Assert.False(result.IsOk);
        Assert.Equal(ShieldEvaluationStatuses.InvalidPrivacyPosture, result.Status);
        Assert.Contains(result.Errors, error => error.EvidenceRefs.Contains("privacy.model_response_included"));
    }

    [Fact]
    public void Evaluate_RejectsReservedRichEvidenceUploadIntent()
    {
        var evidence = CompleteEvidence().Replace(
            "\"upload_intent\": \"api_evidence_summary\"",
            "\"upload_intent\": \"api_opt_in_rich_evidence\"",
            StringComparison.Ordinal);

        var result = new ShieldEvaluationService().Evaluate(evidence, ShieldEvaluationOutput.Combined);

        Assert.False(result.IsOk);
        Assert.Equal(ShieldEvaluationStatuses.InvalidPrivacyPosture, result.Status);
        Assert.Contains(result.Errors, error => error.EvidenceRefs.Contains("privacy.upload_intent"));
    }

    [Fact]
    public void Evaluate_MissingHandoffScoringFieldsConservativelyDegrade()
    {
        var result = new ShieldEvaluationService().Evaluate("""
        {
          "schema_version": "shield-evidence.v0",
          "evidence_id": "missing-handoff-fields",
          "generated_at_utc": "2026-04-14T10:45:00Z",
          "mode_hint": "both",
          "repository": {
            "host": "github",
            "visibility": "public"
          },
          "privacy": {
            "source_included": false,
            "raw_diff_included": false,
            "prompt_included": false,
            "secrets_included": false,
            "redaction_applied": true,
            "upload_intent": "local_only"
          },
          "dimensions": {
            "guard": {
              "enabled": false
            },
            "handoff": {
              "enabled": true,
              "packets": {
                "present": true,
                "count": 1
              }
            },
            "audit": {
              "enabled": false
            }
          },
          "provenance": {
            "producer": "unit-test",
            "generated_by": "local",
            "source": "fixture"
          }
        }
        """, ShieldEvaluationOutput.Combined);

        Assert.True(result.IsOk);
        Assert.Empty(result.Errors);
        Assert.Equal(1, result.Standard!.Dimensions["handoff"].NumericLevel);
        Assert.Equal("CARVES G0.H1.A0 /no-window PASS", result.Standard.Label);
    }

    [Theory]
    [InlineData(7, "8", null)]
    [InlineData(8, "4", null)]
    [InlineData(30, "4", null)]
    [InlineData(31, "C", "CH-04")]
    public void Evaluate_HandoffAgeBoundaryIsContinuous(int ageDays, string expectedLevel, string? expectedGate)
    {
        var evidence = CompleteEvidence().Replace(
            "\"age_days\": 2",
            $"\"age_days\": {ageDays}",
            StringComparison.Ordinal);

        var result = new ShieldEvaluationService().Evaluate(evidence, ShieldEvaluationOutput.Combined);

        Assert.True(result.IsOk);
        Assert.Equal(expectedLevel, result.Standard!.Dimensions["handoff"].Level);
        if (expectedGate is null)
        {
            Assert.DoesNotContain(result.Standard.CriticalGates, gate => gate.StartsWith("CH-", StringComparison.Ordinal));
        }
        else
        {
            Assert.Contains(expectedGate, result.Standard.CriticalGates);
            Assert.Equal("critical", result.Lite!.Band);
            Assert.True(result.Lite.Score <= 39);
        }
    }

    [Theory]
    [InlineData(29, 7)]
    [InlineData(30, 8)]
    [InlineData(89, 8)]
    [InlineData(90, 9)]
    public void Evaluate_HandoffSampleWindowBoundaryIsContinuous(int windowDays, int expectedLevel)
    {
        var evidence = CompleteEvidence()
            .Replace("\"sample_window_days\": 30", $"\"sample_window_days\": {windowDays}", StringComparison.Ordinal)
            .Replace("\"window_days\": 30", $"\"window_days\": {windowDays}", StringComparison.Ordinal)
            .Replace("\"count\": 3", "\"count\": 5", StringComparison.Ordinal)
            .Replace("\"session_switch_count\": 3", "\"session_switch_count\": 5", StringComparison.Ordinal)
            .Replace("\"session_switches_with_packet\": 3", "\"session_switches_with_packet\": 5", StringComparison.Ordinal)
            .Replace("\"completed_facts_with_evidence_count\": 5", "\"completed_facts_with_evidence_count\": 10", StringComparison.Ordinal)
            .Replace("\"decision_refs_count\": 2", "\"decision_refs_count\": 3", StringComparison.Ordinal);

        var result = new ShieldEvaluationService().Evaluate(evidence, ShieldEvaluationOutput.Combined);

        Assert.True(result.IsOk);
        Assert.Equal(expectedLevel, result.Standard!.Dimensions["handoff"].NumericLevel);
    }

    [Fact]
    public void Evaluate_LiteRoundingUsesNamedWeightsAndStandardLevelNineDenominator()
    {
        var evidence = CompleteEvidence()
            .Replace("\"change_budget_present\": true", "\"change_budget_present\": false", StringComparison.Ordinal)
            .Replace("\"repo_orientation_fresh\": true", "\"repo_orientation_fresh\": false", StringComparison.Ordinal)
            .Replace("\"block_decision_count\": 2", "\"block_decision_count\": 0", StringComparison.Ordinal)
            .Replace("\"block_explain_covered_count\": 2", "\"block_explain_covered_count\": 0", StringComparison.Ordinal)
            .Replace("\"review_explain_covered_count\": 4", "\"review_explain_covered_count\": 0", StringComparison.Ordinal)
            .Replace("\"summary_generated_in_window\": true", "\"summary_generated_in_window\": false", StringComparison.Ordinal);

        var result = new ShieldEvaluationService().Evaluate(evidence, ShieldEvaluationOutput.Combined);

        Assert.True(result.IsOk);
        Assert.Equal(3, result.Standard!.Dimensions["guard"].NumericLevel);
        Assert.Equal(4, result.Standard.Dimensions["handoff"].NumericLevel);
        Assert.Equal(5, result.Standard.Dimensions["audit"].NumericLevel);
        Assert.Equal(43, result.Lite!.Score);
        Assert.Equal("basic", result.Lite.Band);
        Assert.Equal(13, result.Lite.DimensionContributions["guard"].Points);
        Assert.Equal(13, result.Lite.DimensionContributions["handoff"].Points);
        Assert.Equal(17, result.Lite.DimensionContributions["audit"].Points);
    }

    [Fact]
    public void Evaluate_ReturnsUnsupportedSchemaWithoutThrowing()
    {
        var evidence = CompleteEvidence().Replace(
            "\"schema_version\": \"shield-evidence.v0\"",
            "\"schema_version\": \"shield-evidence.v99\"",
            StringComparison.Ordinal);

        var result = new ShieldEvaluationService().Evaluate(evidence, ShieldEvaluationOutput.Combined);

        Assert.False(result.IsOk);
        Assert.Equal(ShieldEvaluationStatuses.UnsupportedSchema, result.Status);
        Assert.Contains(result.Errors, error => error.Code == "unsupported_schema");
    }

    [Fact]
    public void Evaluate_ReturnsInvalidPrivacyPostureWithoutThrowing()
    {
        var evidence = CompleteEvidence().Replace(
            "\"raw_diff_included\": false",
            "\"raw_diff_included\": true",
            StringComparison.Ordinal);

        var result = new ShieldEvaluationService().Evaluate(evidence, ShieldEvaluationOutput.Combined);

        Assert.False(result.IsOk);
        Assert.Equal(ShieldEvaluationStatuses.InvalidPrivacyPosture, result.Status);
        Assert.Contains(result.Errors, error => error.EvidenceRefs.Contains("privacy.raw_diff_included"));
    }

    [Fact]
    public void Evaluate_ReturnsInvalidInputForMalformedJsonWithoutThrowing()
    {
        var result = new ShieldEvaluationService().Evaluate("{ malformed", ShieldEvaluationOutput.Combined);

        Assert.False(result.IsOk);
        Assert.Equal(ShieldEvaluationStatuses.InvalidInput, result.Status);
        Assert.Contains(result.Errors, error => error.Code == "invalid_json");
    }

    [Fact]
    public void Evaluate_MissingDimensionEvidenceProjectsZeroLevels()
    {
        var result = new ShieldEvaluationService().Evaluate("""
        {
          "schema_version": "shield-evidence.v0",
          "evidence_id": "missing-dimensions",
          "generated_at_utc": "2026-04-14T10:45:00Z",
          "mode_hint": "both",
          "repository": {
            "host": "github",
            "visibility": "public"
          },
          "privacy": {
            "source_included": false,
            "raw_diff_included": false,
            "prompt_included": false,
            "secrets_included": false,
            "redaction_applied": true,
            "upload_intent": "local_only"
          },
          "provenance": {
            "producer": "unit-test",
            "generated_by": "local",
            "source": "fixture"
          }
        }
        """, ShieldEvaluationOutput.Combined);

        Assert.True(result.IsOk);
        Assert.Equal("CARVES G0.H0.A0 /no-window PASS", result.Standard!.Label);
        Assert.Equal(0, result.Lite!.Score);
        Assert.Equal("no_evidence", result.Lite.Band);
    }

    [Fact]
    public void EvaluateFile_MissingEvidenceReturnsInvalidInput()
    {
        using var workspace = new TemporaryWorkspace();

        var result = new ShieldEvaluationService().EvaluateFile(workspace.RootPath, "missing.json", ShieldEvaluationOutput.Combined);

        Assert.False(result.IsOk);
        Assert.Equal(ShieldEvaluationStatuses.InvalidInput, result.Status);
        Assert.Contains(result.Errors, error => error.Code == "missing_evidence");
    }

    private static string CompleteEvidence()
    {
        return """
        {
          "schema_version": "shield-evidence.v0",
          "evidence_id": "shev_standard_example_20260414",
          "generated_at_utc": "2026-04-14T10:45:00Z",
          "mode_hint": "standard",
          "sample_window_days": 30,
          "repository": {
            "host": "github",
            "visibility": "private",
            "default_branch": "main",
            "repo_id_hash": "sha256:example-private-repo",
            "commit_sha": "fedcba9876543210"
          },
          "privacy": {
            "source_included": false,
            "raw_diff_included": false,
            "prompt_included": false,
            "secrets_included": false,
            "redaction_applied": true,
            "upload_intent": "api_evidence_summary"
          },
          "dimensions": {
            "guard": {
              "enabled": true,
              "policy": {
                "present": true,
                "schema_valid": true,
                "effective_protected_path_prefixes": [
                  ".git/",
                  ".github/workflows/",
                  ".env"
                ],
                "protected_path_action": "block",
                "outside_allowed_action": "review",
                "fail_closed": true,
                "change_budget_present": true,
                "dependency_policy_present": true,
                "source_test_rule_present": true,
                "mixed_feature_refactor_rule_present": true
              },
              "ci": {
                "detected": true,
                "workflow_paths": [
                  ".github/workflows/carves-guard.yml"
                ],
                "guard_check_command_detected": true,
                "fails_on_review_or_block": true
              },
              "decisions": {
                "present": true,
                "window_days": 30,
                "allow_count": 18,
                "review_count": 4,
                "block_count": 2,
                "unresolved_review_count": 0,
                "unresolved_block_count": 0
              },
              "proofs": [
                {
                  "kind": "ci_workflow",
                  "ref": ".github/workflows/carves-guard.yml"
                }
              ]
            },
            "handoff": {
              "enabled": true,
              "packets": {
                "present": true,
                "count": 3,
                "window_days": 30
              },
              "latest_packet": {
                "schema_valid": true,
                "age_days": 2,
                "repo_orientation_fresh": true,
                "target_repo_matches": true,
                "current_objective_present": true,
                "remaining_work_present": true,
                "must_not_repeat_present": true,
                "completed_facts_with_evidence_count": 5,
                "decision_refs_count": 2,
                "confidence": "medium"
              },
              "continuity": {
                "session_switch_count": 3,
                "session_switches_with_packet": 3,
                "stale_packet_count": 0
              }
            },
            "audit": {
              "enabled": true,
              "log": {
                "present": true,
                "readable": true,
                "schema_supported": true,
                "append_only_claimed": true,
                "integrity_check_passed": true
              },
              "records": {
                "record_count": 24,
                "malformed_record_count": 0,
                "future_schema_record_count": 0,
                "earliest_recorded_at_utc": "2026-03-15T00:00:00Z",
                "latest_recorded_at_utc": "2026-04-14T10:45:00Z",
                "records_with_rule_id_count": 24,
                "records_with_evidence_count": 24
              },
              "coverage": {
                "block_decision_count": 2,
                "block_explain_covered_count": 2,
                "review_decision_count": 4,
                "review_explain_covered_count": 4
              },
              "reports": {
                "summary_generated_in_window": true,
                "change_report_generated_in_window": false,
                "failure_pattern_distribution_present": false
              }
            }
          },
          "provenance": {
            "producer": "carves-cli",
            "producer_version": "0.2.0-beta.1",
            "generated_by": "ci",
            "source": "github_actions_pull_request",
            "evidence_hash": "sha256:example-standard-evidence",
            "warnings": []
          }
        }
        """;
    }
}

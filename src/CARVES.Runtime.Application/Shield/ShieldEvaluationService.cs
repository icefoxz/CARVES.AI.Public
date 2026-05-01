using System.Security.Cryptography;
using System.Text.Json;

namespace Carves.Runtime.Application.Shield;

public sealed partial class ShieldEvaluationService
{
    public const string ResultSchemaVersion = "shield-evaluate.v0";
    public const string EvidenceSchemaVersion = "shield-evidence.v0";
    public const string StandardRubricId = "shield-standard-gha-rubric.v0";
    public const string LiteModelId = "shield-lite-scoring.v0";
    private const int StandardMaxNumericLevel = 9;
    private const int SustainedLevelMinimumWindowDays = 30;
    private const int StrongLevelMinimumWindowDays = 90;
    private const int HandoffFreshMaximumAgeDays = 7;
    private const int HandoffCriticalMaximumAgeDays = 30;
    private const int LiteGuardWeight = 40;
    private const int LiteHandoffWeight = 30;
    private const int LiteAuditWeight = 30;
    private const int LiteCriticalCapScore = 39;
    private const int LiteBasicMaximumScore = 49;
    private const int LiteDisciplinedMaximumScore = 79;

    public ShieldEvaluationResult EvaluateFile(string repositoryRoot, string evidencePath, ShieldEvaluationOutput output)
    {
        if (string.IsNullOrWhiteSpace(evidencePath))
        {
            return InvalidInput("missing_evidence", "An evidence path is required.", "argument.evidence_path");
        }

        var resolvedPath = Path.IsPathRooted(evidencePath)
            ? evidencePath
            : Path.GetFullPath(Path.Combine(repositoryRoot, evidencePath));

        if (!File.Exists(resolvedPath))
        {
            return InvalidInput("missing_evidence", $"Evidence file was not found: {evidencePath}", "argument.evidence_path");
        }

        try
        {
            var evidenceJson = File.ReadAllText(resolvedPath);
            var consumedEvidenceSha256 = ComputeFileSha256(resolvedPath);
            return Evaluate(evidenceJson, output, consumedEvidenceSha256);
        }
        catch (IOException ex)
        {
            return InvalidInput("evidence_read_failed", ex.Message, "argument.evidence_path");
        }
        catch (UnauthorizedAccessException ex)
        {
            return InvalidInput("evidence_read_failed", ex.Message, "argument.evidence_path");
        }
    }

    public ShieldEvaluationResult Evaluate(string evidenceJson, ShieldEvaluationOutput output)
    {
        return Evaluate(evidenceJson, output, consumedEvidenceSha256: null);
    }

    private static ShieldEvaluationResult Evaluate(
        string evidenceJson,
        ShieldEvaluationOutput output,
        string? consumedEvidenceSha256)
    {
        if (string.IsNullOrWhiteSpace(evidenceJson))
        {
            return InvalidInput("missing_evidence", "Evidence JSON is empty.", "evidence");
        }

        try
        {
            using var document = JsonDocument.Parse(evidenceJson);
            return Evaluate(document.RootElement, output, consumedEvidenceSha256);
        }
        catch (JsonException ex)
        {
            return InvalidInput("invalid_json", ex.Message, "evidence");
        }
    }

    private static ShieldEvaluationResult Evaluate(
        JsonElement root,
        ShieldEvaluationOutput output,
        string? consumedEvidenceSha256)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return InvalidInput("invalid_json_root", "Evidence JSON root must be an object.", "evidence");
        }

        var evidenceSchemaVersion = GetString(root, "schema_version");
        var evidenceId = GetString(root, "evidence_id");
        var sampleWindowDays = GetInt(root, "sample_window_days");
        if (!string.Equals(evidenceSchemaVersion, EvidenceSchemaVersion, StringComparison.Ordinal))
        {
            return new ShieldEvaluationResult(
                ResultSchemaVersion,
                ShieldEvaluationStatuses.UnsupportedSchema,
                "self_check",
                "unknown",
                Certification: false,
                evidenceSchemaVersion,
                evidenceId,
                sampleWindowDays,
                consumedEvidenceSha256,
                Standard: null,
                Lite: null,
                Errors:
                [
                    new ShieldEvaluationError(
                        "unsupported_schema",
                        $"Expected {EvidenceSchemaVersion}.",
                        ["schema_version"]),
                ]);
        }

        var privacyErrors = ValidatePrivacy(root);
        if (privacyErrors.Count > 0)
        {
            return new ShieldEvaluationResult(
                ResultSchemaVersion,
                ShieldEvaluationStatuses.InvalidPrivacyPosture,
                "self_check",
                ResolvePrivacyPosture(root),
                Certification: false,
                evidenceSchemaVersion,
                evidenceId,
                sampleWindowDays,
                consumedEvidenceSha256,
                Standard: null,
                Lite: null,
                privacyErrors);
        }

        var standard = EvaluateStandard(root, sampleWindowDays);
        var lite = EvaluateLite(standard, sampleWindowDays);
        return new ShieldEvaluationResult(
            ResultSchemaVersion,
            ShieldEvaluationStatuses.Ok,
            "self_check",
            ResolvePrivacyPosture(root),
            Certification: false,
            evidenceSchemaVersion,
            evidenceId,
            sampleWindowDays,
            consumedEvidenceSha256,
            output is ShieldEvaluationOutput.Standard or ShieldEvaluationOutput.Combined ? standard : null,
            output is ShieldEvaluationOutput.Lite or ShieldEvaluationOutput.Combined ? lite : null,
            Array.Empty<ShieldEvaluationError>());
    }

    private static ShieldStandardResult EvaluateStandard(JsonElement root, int? sampleWindowDays)
    {
        var guard = EvaluateGuard(root, sampleWindowDays);
        var handoff = EvaluateHandoff(root, sampleWindowDays);
        var audit = EvaluateAudit(root, sampleWindowDays);
        var dimensions = new Dictionary<string, ShieldStandardDimensionResult>(StringComparer.Ordinal)
        {
            ["guard"] = ToStandardDimension("guard", guard),
            ["handoff"] = ToStandardDimension("handoff", handoff),
            ["audit"] = ToStandardDimension("audit", audit),
        };
        var criticalGates = dimensions.Values.SelectMany(dimension => dimension.CriticalGates).ToArray();
        var label = BuildStandardLabel(guard, handoff, audit, sampleWindowDays, criticalGates);

        return new ShieldStandardResult(
            StandardRubricId,
            label,
            dimensions,
            OverallScore: null,
            criticalGates);
    }

    private static ShieldLiteResult EvaluateLite(ShieldStandardResult standard, int? sampleWindowDays)
    {
        var weights = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["guard"] = LiteGuardWeight,
            ["handoff"] = LiteHandoffWeight,
            ["audit"] = LiteAuditWeight,
        };

        var contributions = new Dictionary<string, ShieldDimensionContribution>(StringComparer.Ordinal);
        foreach (var (dimension, weight) in weights)
        {
            var standardDimension = standard.Dimensions[dimension];
            var points = standardDimension.NumericLevel is null
                ? 0
                : (int)Math.Round(
                    weight * standardDimension.NumericLevel.Value / (double)StandardMaxNumericLevel,
                    MidpointRounding.AwayFromZero);
            contributions[dimension] = new ShieldDimensionContribution(standardDimension.Level, weight, points);
        }

        var score = contributions.Values.Sum(contribution => contribution.Points);
        var criticalGates = standard.CriticalGates;
        if (criticalGates.Count > 0)
        {
            score = Math.Min(score, LiteCriticalCapScore);
        }

        return new ShieldLiteResult(
            LiteModelId,
            Math.Clamp(score, 0, 100),
            ResolveLiteBand(score, criticalGates.Count > 0),
            SelfCheck: true,
            sampleWindowDays,
            contributions,
            criticalGates,
            BuildTopRisks(standard, contributions),
            BuildNextSteps(standard));
    }

    private static DimensionEvaluation EvaluateGuard(JsonElement root, int? sampleWindowDays)
    {
        var enabled = GetBool(root, "dimensions", "guard", "enabled") == true;
        var criticalGates = new List<string>();
        if (enabled && GetBool(root, "dimensions", "guard", "policy", "schema_valid") == false)
        {
            criticalGates.Add("CG-01");
        }

        if (enabled
            && (GetBool(root, "dimensions", "guard", "policy", "fail_closed") == false
                || IsPresentNonMatchingString(root, "block", "dimensions", "guard", "policy", "protected_path_action")))
        {
            criticalGates.Add("CG-02");
        }

        if (GetBool(root, "dimensions", "guard", "ci", "detected") == true
            && GetBool(root, "dimensions", "guard", "ci", "guard_check_command_detected") == true
            && GetBool(root, "dimensions", "guard", "ci", "fails_on_review_or_block") == false)
        {
            criticalGates.Add("CG-03");
        }

        if (GetInt(root, "dimensions", "guard", "decisions", "unresolved_block_count") > 0)
        {
            criticalGates.Add("CG-04");
        }

        if (criticalGates.Count > 0)
        {
            return DimensionEvaluation.Critical("G", criticalGates);
        }

        var level = 0;
        if (!enabled || GetBool(root, "dimensions", "guard", "policy", "present") != true)
        {
            return DimensionEvaluation.Numeric("G", level, "Guard is not enabled or has no policy evidence.");
        }

        level = 1;
        if (GetBool(root, "dimensions", "guard", "policy", "schema_valid") == true)
        {
            level = 2;
        }

        if (level == 2
            && GetBool(root, "dimensions", "guard", "policy", "fail_closed") == true
            && StringEquals(root, "block", "dimensions", "guard", "policy", "protected_path_action")
            && GetArrayCount(root, "dimensions", "guard", "policy", "effective_protected_path_prefixes") > 0)
        {
            level = 3;
        }

        if (level == 3
            && GetBool(root, "dimensions", "guard", "policy", "change_budget_present") == true
            && IsReviewOrBlock(root, "dimensions", "guard", "policy", "outside_allowed_action"))
        {
            level = 4;
        }

        if (level == 4
            && GetBool(root, "dimensions", "guard", "ci", "detected") == true
            && GetBool(root, "dimensions", "guard", "ci", "guard_check_command_detected") == true
            && GetBool(root, "dimensions", "guard", "ci", "fails_on_review_or_block") == true)
        {
            level = 5;
        }

        if (level == 5
            && GetBool(root, "dimensions", "guard", "policy", "source_test_rule_present") == true
            && (GetBool(root, "dimensions", "guard", "policy", "dependency_policy_present") == true
                || GetBool(root, "dimensions", "guard", "policy", "mixed_feature_refactor_rule_present") == true))
        {
            level = 6;
        }

        if (level == 6
            && GetBool(root, "dimensions", "guard", "decisions", "present") == true
            && GetInt(root, "dimensions", "guard", "decisions", "unresolved_review_count") == 0
            && GetInt(root, "dimensions", "guard", "decisions", "unresolved_block_count") == 0)
        {
            level = 7;
        }

        var window = GetInt(root, "dimensions", "guard", "decisions", "window_days") ?? sampleWindowDays;
        if (level == 7 && window >= SustainedLevelMinimumWindowDays && GetArrayCount(root, "dimensions", "guard", "proofs") > 0)
        {
            level = 8;
        }

        if (level == 8
            && window >= StrongLevelMinimumWindowDays
            && GetBool(root, "dimensions", "guard", "policy", "dependency_policy_present") == true
            && GetBool(root, "dimensions", "guard", "policy", "source_test_rule_present") == true
            && GetBool(root, "dimensions", "guard", "policy", "mixed_feature_refactor_rule_present") == true
            && HasCiProof(root))
        {
            level = 9;
        }

        level = ApplyWindowCaps(level, window);
        if (GetInt(root, "dimensions", "guard", "decisions", "unresolved_review_count") > 0
            && (GetInt(root, "dimensions", "guard", "decisions", "unresolved_block_count") ?? 0) == 0)
        {
            level = Math.Min(level, 6);
        }

        return DimensionEvaluation.Numeric("G", level, $"Guard projected to level {level}.");
    }

    private static DimensionEvaluation EvaluateHandoff(JsonElement root, int? sampleWindowDays)
    {
        var enabled = GetBool(root, "dimensions", "handoff", "enabled") == true;
        var criticalGates = new List<string>();
        if (enabled && GetBool(root, "dimensions", "handoff", "latest_packet", "schema_valid") == false)
        {
            criticalGates.Add("CH-01");
        }

        if (enabled && GetBool(root, "dimensions", "handoff", "latest_packet", "target_repo_matches") == false)
        {
            criticalGates.Add("CH-02");
        }

        if (enabled
            && (GetBool(root, "dimensions", "handoff", "latest_packet", "current_objective_present") == false
                || GetBool(root, "dimensions", "handoff", "latest_packet", "remaining_work_present") == false))
        {
            criticalGates.Add("CH-03");
        }

        if (enabled && GetInt(root, "dimensions", "handoff", "latest_packet", "age_days") > HandoffCriticalMaximumAgeDays)
        {
            criticalGates.Add("CH-04");
        }

        if (criticalGates.Count > 0)
        {
            return DimensionEvaluation.Critical("H", criticalGates);
        }

        var level = 0;
        if (!enabled || GetBool(root, "dimensions", "handoff", "packets", "present") != true)
        {
            return DimensionEvaluation.Numeric("H", level, "Handoff is not enabled or has no packet evidence.");
        }

        if (GetInt(root, "dimensions", "handoff", "packets", "count") >= 1)
        {
            level = 1;
        }

        if (level == 1 && GetBool(root, "dimensions", "handoff", "latest_packet", "schema_valid") == true)
        {
            level = 2;
        }

        if (level == 2
            && GetBool(root, "dimensions", "handoff", "latest_packet", "current_objective_present") == true
            && GetBool(root, "dimensions", "handoff", "latest_packet", "remaining_work_present") == true)
        {
            level = 3;
        }

        if (level == 3
            && GetBool(root, "dimensions", "handoff", "latest_packet", "must_not_repeat_present") == true
            && GetInt(root, "dimensions", "handoff", "latest_packet", "completed_facts_with_evidence_count") >= 1)
        {
            level = 4;
        }

        if (level == 4
            && GetBool(root, "dimensions", "handoff", "latest_packet", "repo_orientation_fresh") == true
            && GetBool(root, "dimensions", "handoff", "latest_packet", "target_repo_matches") == true
            && GetInt(root, "dimensions", "handoff", "latest_packet", "age_days") <= HandoffFreshMaximumAgeDays)
        {
            level = 5;
        }

        if (level == 5
            && GetInt(root, "dimensions", "handoff", "latest_packet", "decision_refs_count") >= 1
            && IsMediumOrHigh(root, "dimensions", "handoff", "latest_packet", "confidence"))
        {
            level = 6;
        }

        var switches = GetInt(root, "dimensions", "handoff", "continuity", "session_switch_count");
        var switchesWithPackets = GetInt(root, "dimensions", "handoff", "continuity", "session_switches_with_packet");
        if (level == 6
            && switches.HasValue
            && switchesWithPackets.HasValue
            && switches == switchesWithPackets
            && GetInt(root, "dimensions", "handoff", "continuity", "stale_packet_count") == 0)
        {
            level = 7;
        }

        var window = GetInt(root, "dimensions", "handoff", "packets", "window_days") ?? sampleWindowDays;
        if (level == 7 && window >= SustainedLevelMinimumWindowDays && GetInt(root, "dimensions", "handoff", "packets", "count") >= 3)
        {
            level = 8;
        }

        if (level == 8
            && window >= StrongLevelMinimumWindowDays
            && GetInt(root, "dimensions", "handoff", "continuity", "session_switch_count") >= 5
            && GetInt(root, "dimensions", "handoff", "latest_packet", "completed_facts_with_evidence_count") >= 10
            && GetInt(root, "dimensions", "handoff", "latest_packet", "decision_refs_count") >= 3)
        {
            level = 9;
        }

        level = ApplyWindowCaps(level, window);
        var age = GetInt(root, "dimensions", "handoff", "latest_packet", "age_days");
        if (age > HandoffFreshMaximumAgeDays && age <= HandoffCriticalMaximumAgeDays)
        {
            level = Math.Min(level, 4);
        }

        return DimensionEvaluation.Numeric("H", level, $"Handoff projected to level {level}.");
    }

    private static DimensionEvaluation EvaluateAudit(JsonElement root, int? sampleWindowDays)
    {
        var enabled = GetBool(root, "dimensions", "audit", "enabled") == true;
        var criticalGates = new List<string>();
        if (enabled
            && GetBool(root, "dimensions", "audit", "log", "present") == true
            && (GetBool(root, "dimensions", "audit", "log", "readable") == false
                || GetBool(root, "dimensions", "audit", "log", "schema_supported") == false))
        {
            criticalGates.Add("CA-01");
        }

        if (GetBool(root, "dimensions", "audit", "log", "append_only_claimed") == true
            && GetBool(root, "dimensions", "audit", "log", "integrity_check_passed") == false)
        {
            criticalGates.Add("CA-02");
        }

        if ((GetInt(root, "dimensions", "audit", "coverage", "block_decision_count") ?? 0)
            > (GetInt(root, "dimensions", "audit", "coverage", "block_explain_covered_count") ?? 0))
        {
            criticalGates.Add("CA-03");
        }

        if (criticalGates.Count > 0)
        {
            return DimensionEvaluation.Critical("A", criticalGates);
        }

        var level = 0;
        if (!enabled
            || GetBool(root, "dimensions", "audit", "log", "present") != true
            || GetBool(root, "dimensions", "audit", "log", "readable") != true)
        {
            return DimensionEvaluation.Numeric("A", level, "Audit is not enabled or has no readable log evidence.");
        }

        level = 1;
        if (GetBool(root, "dimensions", "audit", "log", "schema_supported") == true)
        {
            level = 2;
        }

        if (level == 2
            && GetInt(root, "dimensions", "audit", "records", "record_count") >= 1
            && !string.IsNullOrWhiteSpace(GetString(root, "dimensions", "audit", "records", "earliest_recorded_at_utc"))
            && !string.IsNullOrWhiteSpace(GetString(root, "dimensions", "audit", "records", "latest_recorded_at_utc")))
        {
            level = 3;
        }

        if (level == 3
            && GetInt(root, "dimensions", "audit", "records", "records_with_rule_id_count") > 0
            && GetInt(root, "dimensions", "audit", "records", "records_with_evidence_count") > 0)
        {
            level = 4;
        }

        if (level == 4
            && GetInt(root, "dimensions", "audit", "records", "malformed_record_count") == 0
            && GetInt(root, "dimensions", "audit", "records", "future_schema_record_count") == 0)
        {
            level = 5;
        }

        if (level == 5
            && CoverageEquals(root, "block")
            && CoverageEquals(root, "review"))
        {
            level = 6;
        }

        if (level == 6 && GetBool(root, "dimensions", "audit", "reports", "summary_generated_in_window") == true)
        {
            level = 7;
        }

        var window = sampleWindowDays ?? DeriveAuditWindowDays(root);
        if (level == 7 && window >= SustainedLevelMinimumWindowDays && GetInt(root, "dimensions", "audit", "records", "record_count") >= 10)
        {
            level = 8;
        }

        if (level == 8
            && window >= StrongLevelMinimumWindowDays
            && GetBool(root, "dimensions", "audit", "log", "integrity_check_passed") == true
            && (GetBool(root, "dimensions", "audit", "reports", "change_report_generated_in_window") == true
                || GetBool(root, "dimensions", "audit", "reports", "failure_pattern_distribution_present") == true))
        {
            level = 9;
        }

        level = ApplyWindowCaps(level, window);
        if (GetInt(root, "dimensions", "audit", "records", "malformed_record_count") > 0
            || GetInt(root, "dimensions", "audit", "records", "future_schema_record_count") > 0)
        {
            level = Math.Min(level, 4);
        }

        return DimensionEvaluation.Numeric("A", level, $"Audit projected to level {level}.");
    }

    private static IReadOnlyList<ShieldEvaluationError> ValidatePrivacy(JsonElement root)
    {
        if (!TryGet(root, out var privacy, "privacy") || privacy.ValueKind != JsonValueKind.Object)
        {
            return
            [
                new ShieldEvaluationError(
                    "invalid_privacy_posture",
                    "Evidence must include a privacy object proving no private payload is included.",
                    ["privacy"]),
            ];
        }

        var errors = new List<ShieldEvaluationError>();
        var fieldsThatMustRemainFalse = new[]
        {
            "source_included",
            "raw_diff_included",
            "prompt_included",
            "secrets_included",
            "credential_included",
            "credentials_included",
            "model_response_included",
            "model_responses_included",
            "private_payload_included",
            "private_file_payloads_included",
        };

        foreach (var field in fieldsThatMustRemainFalse)
        {
            if (GetBool(privacy, field) == true)
            {
                errors.Add(new ShieldEvaluationError(
                    "invalid_privacy_posture",
                    $"privacy.{field} must be false for local Shield evaluation.",
                    [$"privacy.{field}"]));
            }
        }

        foreach (var requiredField in new[] { "source_included", "raw_diff_included", "prompt_included", "secrets_included" })
        {
            if (!TryGet(privacy, out var requiredValue, requiredField) || requiredValue.ValueKind != JsonValueKind.False)
            {
                errors.Add(new ShieldEvaluationError(
                    "invalid_privacy_posture",
                    $"privacy.{requiredField} must be present and false.",
                    [$"privacy.{requiredField}"]));
            }
        }

        var uploadIntent = GetString(privacy, "upload_intent");
        if (string.IsNullOrWhiteSpace(uploadIntent))
        {
            errors.Add(new ShieldEvaluationError(
                "invalid_privacy_posture",
                "privacy.upload_intent must be local_only or api_evidence_summary.",
                ["privacy.upload_intent"]));
        }
        else if (!string.Equals(uploadIntent, "local_only", StringComparison.Ordinal)
                 && !string.Equals(uploadIntent, "api_evidence_summary", StringComparison.Ordinal))
        {
            errors.Add(new ShieldEvaluationError(
                "invalid_privacy_posture",
                "privacy.upload_intent is outside the local prototype contract.",
                ["privacy.upload_intent"]));
        }

        return errors;
    }

    private static string ResolvePrivacyPosture(JsonElement root)
    {
        var uploadIntent = GetString(root, "privacy", "upload_intent");
        return uploadIntent switch
        {
            "local_only" => "local_only",
            "api_evidence_summary" => "evidence_summary",
            null or "" => "unknown",
            _ => "invalid",
        };
    }

    private static ShieldEvaluationResult InvalidInput(string code, string message, string evidenceRef)
    {
        return new ShieldEvaluationResult(
            ResultSchemaVersion,
            ShieldEvaluationStatuses.InvalidInput,
            "self_check",
            "unknown",
            Certification: false,
            EvidenceSchemaVersion: null,
            EvidenceId: null,
            SampleWindowDays: null,
            ConsumedEvidenceSha256: null,
            Standard: null,
            Lite: null,
            Errors: [new ShieldEvaluationError(code, message, [evidenceRef])]);
    }

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static ShieldStandardDimensionResult ToStandardDimension(string dimension, DimensionEvaluation evaluation)
    {
        return new ShieldStandardDimensionResult(
            dimension,
            evaluation.Level,
            evaluation.NumericLevel,
            ResolveStandardBand(evaluation.Level),
            evaluation.CriticalGates,
            evaluation.Summary);
    }

    private static string BuildStandardLabel(
        DimensionEvaluation guard,
        DimensionEvaluation handoff,
        DimensionEvaluation audit,
        int? sampleWindowDays,
        IReadOnlyList<string> criticalGates)
    {
        var window = sampleWindowDays.HasValue ? $"/{sampleWindowDays.Value}d" : "/no-window";
        var disposition = criticalGates.Count == 0 ? "PASS" : "FAIL";
        var label = $"CARVES {guard.Prefix}{guard.Level}.{handoff.Prefix}{handoff.Level}.{audit.Prefix}{audit.Level} {window} {disposition}";
        return criticalGates.Count == 0 ? label : $"{label} {string.Join(",", criticalGates)}";
    }

    private static string ResolveStandardBand(string level)
    {
        if (string.Equals(level, "C", StringComparison.Ordinal))
        {
            return "red";
        }

        if (!int.TryParse(level, out var numeric))
        {
            return "gray";
        }

        return numeric switch
        {
            0 => "gray",
            >= 1 and <= 4 => "white",
            >= 5 and <= 7 => "yellow",
            >= 8 and <= 9 => "green",
            _ => "gray",
        };
    }

    private static string ResolveLiteBand(int score, bool critical)
    {
        if (critical)
        {
            return "critical";
        }

        return score switch
        {
            0 => "no_evidence",
            >= 1 and <= LiteBasicMaximumScore => "basic",
            > LiteBasicMaximumScore and <= LiteDisciplinedMaximumScore => "disciplined",
            _ => "strong",
        };
    }

    private static IReadOnlyList<string> BuildTopRisks(
        ShieldStandardResult standard,
        IReadOnlyDictionary<string, ShieldDimensionContribution> contributions)
    {
        var risks = new List<string>();
        foreach (var gate in standard.CriticalGates)
        {
            risks.Add($"Critical gate failed: {gate}.");
        }

        foreach (var dimension in standard.Dimensions.Values.Where(dimension => dimension.NumericLevel == 0))
        {
            risks.Add($"No Shield evidence for {dimension.Dimension}.");
        }

        var guard = standard.Dimensions["guard"];
        if ((guard.NumericLevel ?? 0) < 5 && guard.CriticalGates.Count == 0)
        {
            risks.Add("Guard lacks CI fail-on-review/block proof or fail-closed depth.");
        }

        var handoff = standard.Dimensions["handoff"];
        if ((handoff.NumericLevel ?? 0) < 5 && handoff.CriticalGates.Count == 0)
        {
            risks.Add("Handoff lacks fresh target-matched packet evidence.");
        }

        var audit = standard.Dimensions["audit"];
        if ((audit.NumericLevel ?? 0) < 6 && audit.CriticalGates.Count == 0)
        {
            risks.Add("Audit lacks explain coverage or readable supported log evidence.");
        }

        var lowest = contributions.OrderBy(pair => pair.Value.Points).First();
        risks.Add($"Lowest contribution is {lowest.Key} with {lowest.Value.Points} points.");
        return risks.Distinct(StringComparer.Ordinal).Take(3).ToArray();
    }

    private static IReadOnlyList<string> BuildNextSteps(ShieldStandardResult standard)
    {
        if (standard.CriticalGates.Count > 0)
        {
            return standard.CriticalGates
                .Take(3)
                .Select(gate => $"Resolve {gate} before using this Shield result as a positive signal.")
                .ToArray();
        }

        return standard.Dimensions.Values
            .OrderBy(dimension => dimension.NumericLevel ?? -1)
            .Take(3)
            .Select(BuildNextStep)
            .ToArray();
    }

    private static string BuildNextStep(ShieldStandardDimensionResult dimension)
    {
        return dimension.Dimension switch
        {
            "guard" => "Add fail-closed Guard CI evidence and keep unresolved review/block counts at zero.",
            "handoff" => "Keep a fresh target-matched Handoff packet with objective, remaining work, decisions, and continuity evidence.",
            "audit" => "Record readable Audit decisions with rule/evidence refs and explain coverage for review/block outcomes.",
            _ => $"Add sustained evidence for {dimension.Dimension}.",
        };
    }

    private static int ApplyWindowCaps(int level, int? window)
    {
        if (!window.HasValue)
        {
            return Math.Min(level, 7);
        }

        if (window < SustainedLevelMinimumWindowDays)
        {
            return Math.Min(level, 7);
        }

        if (window < StrongLevelMinimumWindowDays)
        {
            return Math.Min(level, 8);
        }

        return level;
    }

    private static int? DeriveAuditWindowDays(JsonElement root)
    {
        var earliest = GetString(root, "dimensions", "audit", "records", "earliest_recorded_at_utc");
        var latest = GetString(root, "dimensions", "audit", "records", "latest_recorded_at_utc");
        if (!DateTimeOffset.TryParse(earliest, out var earliestAt)
            || !DateTimeOffset.TryParse(latest, out var latestAt)
            || latestAt < earliestAt)
        {
            return null;
        }

        return Math.Max(1, (int)Math.Floor((latestAt - earliestAt).TotalDays));
    }

    private static bool CoverageEquals(JsonElement root, string kind)
    {
        var total = GetInt(root, "dimensions", "audit", "coverage", $"{kind}_decision_count");
        var covered = GetInt(root, "dimensions", "audit", "coverage", $"{kind}_explain_covered_count");
        return total.HasValue && covered.HasValue && total == covered;
    }

    private static bool HasCiProof(JsonElement root)
    {
        if (GetArrayCount(root, "dimensions", "guard", "ci", "workflow_paths") > 0)
        {
            return true;
        }

        if (!TryGet(root, out var proofs, "dimensions", "guard", "proofs") || proofs.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return proofs.EnumerateArray().Any(proof =>
            proof.ValueKind == JsonValueKind.Object
            && (StringContains(proof, "ci", "kind") || StringContains(proof, ".github/workflows/", "ref")));
    }

    private static bool TryGet(JsonElement root, out JsonElement value, params string[] path)
    {
        value = root;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                value = default;
                return false;
            }
        }

        return true;
    }

    private static bool? GetBool(JsonElement root, params string[] path)
    {
        return TryGet(root, out var value, path) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
    }

    private static int? GetInt(JsonElement root, params string[] path)
    {
        return TryGet(root, out var value, path) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;
    }

    private static string? GetString(JsonElement root, params string[] path)
    {
        return TryGet(root, out var value, path) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int GetArrayCount(JsonElement root, params string[] path)
    {
        return TryGet(root, out var value, path) && value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength()
            : 0;
    }

    private static bool StringEquals(JsonElement root, string expected, params string[] path)
    {
        return string.Equals(GetString(root, path), expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPresentNonMatchingString(JsonElement root, string expected, params string[] path)
    {
        var value = GetString(root, path);
        return !string.IsNullOrWhiteSpace(value) && !string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool StringContains(JsonElement root, string expected, params string[] path)
    {
        var value = GetString(root, path);
        return value?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsReviewOrBlock(JsonElement root, params string[] path)
    {
        var value = GetString(root, path);
        return string.Equals(value, "review", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "block", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMediumOrHigh(JsonElement root, params string[] path)
    {
        var value = GetString(root, path);
        return string.Equals(value, "medium", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "high", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record DimensionEvaluation(
        string Prefix,
        string Level,
        int? NumericLevel,
        IReadOnlyList<string> CriticalGates,
        string Summary)
    {
        public static DimensionEvaluation Numeric(string prefix, int level, string summary)
        {
            return new DimensionEvaluation(prefix, level.ToString(), level, Array.Empty<string>(), summary);
        }

        public static DimensionEvaluation Critical(string prefix, IReadOnlyList<string> criticalGates)
        {
            return new DimensionEvaluation(prefix, "C", null, criticalGates, $"{prefix} critical gate failed.");
        }
    }
}

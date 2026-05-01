using System.Text.Json;

namespace Carves.Shield.Core;

public sealed class ShieldLiteChallengeRunner
{
    private static readonly IReadOnlyDictionary<string, ChallengeExpectation> Expectations =
        new Dictionary<string, ChallengeExpectation>(StringComparer.Ordinal)
        {
            ["protected_path_violation"] = new("block", "critical"),
            ["deletion_without_credible_replacement"] = new("block", "critical"),
            ["fake_audit_evidence"] = new("reject", "critical"),
            ["stale_handoff_packet"] = new("review", "critical"),
            ["privacy_leakage_flag"] = new("reject", "critical"),
            ["missing_ci_evidence"] = new("review", "basic"),
            ["oversized_patch"] = new("review", "basic"),
        };

    public ShieldLiteChallengeRunResult RunFile(string repoRoot, string challengePackPath)
    {
        if (string.IsNullOrWhiteSpace(challengePackPath))
        {
            return BuildInvalidPackResult("(missing)", "argument.challenge_pack_path", "Challenge pack path is required.");
        }

        var resolvedPath = Path.IsPathRooted(challengePackPath)
            ? Path.GetFullPath(challengePackPath)
            : Path.GetFullPath(Path.Combine(repoRoot, challengePackPath));
        if (!File.Exists(resolvedPath))
        {
            return BuildInvalidPackResult(
                challengePackPath,
                "challenge_pack_missing",
                $"Challenge pack was not found: {challengePackPath}");
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(resolvedPath));
            return Run(document.RootElement, challengePackPath);
        }
        catch (JsonException ex)
        {
            return BuildInvalidPackResult(
                challengePackPath,
                "challenge_pack_invalid_json",
                $"Challenge pack JSON is invalid: {ex.GetType().Name}");
        }
        catch (IOException ex)
        {
            return BuildInvalidPackResult(
                challengePackPath,
                "challenge_pack_read_failed",
                $"Challenge pack could not be read: {ex.GetType().Name}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return BuildInvalidPackResult(
                challengePackPath,
                "challenge_pack_read_failed",
                $"Challenge pack could not be read: {ex.GetType().Name}");
        }
    }

    private static ShieldLiteChallengeRunResult Run(JsonElement root, string challengePackPath)
    {
        var suiteIssues = new List<string>();
        var suiteId = GetString(root, "challenge_suite_id") ?? "(unknown)";
        VerifyField(suiteIssues, GetString(root, "schema_version"), ShieldLiteChallengeContract.SchemaVersion, "schema_version_mismatch");
        VerifyField(suiteIssues, GetString(root, "mode"), "local_challenge", "mode_mismatch");
        VerifyField(suiteIssues, GetBool(root, "local_only"), true, "local_only_mismatch");
        VerifyField(suiteIssues, GetBool(root, "certification"), false, "certification_mismatch");
        VerifyField(suiteIssues, GetString(root, "scoring_model"), "shield-lite-scoring.v0", "scoring_model_mismatch");
        VerifyField(suiteIssues, GetString(root, "evidence_schema"), "shield-evidence.v0", "evidence_schema_mismatch");

        var caseResults = new List<ShieldLiteChallengeCaseResult>();
        if (!root.TryGetProperty("cases", out var cases) || cases.ValueKind != JsonValueKind.Array)
        {
            suiteIssues.Add("cases_missing");
        }
        else
        {
            var seenCaseIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var challengeCase in cases.EnumerateArray())
            {
                var result = EvaluateCase(challengeCase, seenCaseIds);
                caseResults.Add(result);
            }
        }

        foreach (var requiredKind in ShieldLiteChallengeContract.RequiredChallengeKinds)
        {
            if (caseResults.Any(result => string.Equals(result.ChallengeKind, requiredKind, StringComparison.Ordinal)))
            {
                continue;
            }

            caseResults.Add(new ShieldLiteChallengeCaseResult(
                CaseId: $"missing:{requiredKind}",
                ChallengeKind: requiredKind,
                Status: "failed",
                Passed: false,
                ExpectedDecision: Expectations[requiredKind].Decision,
                ObservedDecision: null,
                ShieldLiteBandCeiling: Expectations[requiredKind].BandCeiling,
                ReasonCodes: [requiredKind],
                EvidenceRefs: [],
                ShareableArtifacts: [],
                Issues: ["missing_required_challenge_kind"]));
        }

        if (suiteIssues.Count > 0)
        {
            caseResults.Insert(0, new ShieldLiteChallengeCaseResult(
                CaseId: "suite",
                ChallengeKind: "suite",
                Status: "failed",
                Passed: false,
                ExpectedDecision: null,
                ObservedDecision: null,
                ShieldLiteBandCeiling: null,
                ReasonCodes: [],
                EvidenceRefs: [],
                ShareableArtifacts: [],
                Issues: suiteIssues));
        }

        var passedCount = caseResults.Count(result => result.Passed);
        var failedCount = caseResults.Count - passedCount;
        return new ShieldLiteChallengeRunResult(
            ShieldLiteChallengeContract.ResultSchemaVersion,
            failedCount == 0 ? "passed" : "failed",
            suiteId,
            challengePackPath,
            ShieldLiteChallengeContract.SummaryLabel,
            LocalOnly: true,
            Certification: false,
            caseResults.Count,
            passedCount,
            failedCount,
            caseResults.Count == 0 ? 0 : Math.Round(passedCount / (double)caseResults.Count, 4),
            caseResults,
            [
                "local challenge result only",
                "not certified safe",
                "not certification",
                "not hosted verification",
                "not a public leaderboard",
                "not a model safety benchmark",
            ]);
    }

    private static ShieldLiteChallengeCaseResult EvaluateCase(JsonElement challengeCase, HashSet<string> seenCaseIds)
    {
        var issues = new List<string>();
        var caseId = GetString(challengeCase, "case_id") ?? "(missing)";
        var kind = GetString(challengeCase, "challenge_kind") ?? "(missing)";
        if (!Expectations.TryGetValue(kind, out var expectation))
        {
            issues.Add("unknown_challenge_kind");
            expectation = new ChallengeExpectation(null, null);
        }
        if (!string.IsNullOrWhiteSpace(caseId) && !seenCaseIds.Add(caseId))
        {
            issues.Add("duplicate_case_id");
        }

        var posture = TryGet(challengeCase, out var postureElement, "expected_local_decision_posture")
            ? postureElement
            : default;
        if (posture.ValueKind != JsonValueKind.Object)
        {
            issues.Add("expected_local_decision_posture_missing");
        }

        var observedDecision = posture.ValueKind == JsonValueKind.Object ? GetString(posture, "decision") : null;
        var bandCeiling = posture.ValueKind == JsonValueKind.Object ? GetString(posture, "shield_lite_band_ceiling") : null;
        if (expectation.Decision is not null && !string.Equals(observedDecision, expectation.Decision, StringComparison.Ordinal))
        {
            issues.Add("decision_posture_mismatch");
        }

        if (expectation.BandCeiling is not null && !string.Equals(bandCeiling, expectation.BandCeiling, StringComparison.Ordinal))
        {
            issues.Add("band_ceiling_mismatch");
        }

        var reasonCodes = posture.ValueKind == JsonValueKind.Object ? GetStringArray(posture, "reason_codes") : [];
        if (Expectations.ContainsKey(kind) && !reasonCodes.Contains(kind, StringComparer.Ordinal))
        {
            issues.Add("reason_code_missing");
        }

        var evidenceRefs = posture.ValueKind == JsonValueKind.Object ? GetStringArray(posture, "evidence_refs") : [];
        if (evidenceRefs.Count == 0)
        {
            issues.Add("evidence_refs_missing");
        }

        var allowedOutputs = TryGet(challengeCase, out var allowedOutputsElement, "allowed_outputs")
            ? allowedOutputsElement
            : default;
        if (allowedOutputs.ValueKind != JsonValueKind.Object)
        {
            issues.Add("allowed_outputs_missing");
        }

        if (allowedOutputs.ValueKind == JsonValueKind.Object)
        {
            if (GetBool(allowedOutputs, "local_result") != true)
            {
                issues.Add("local_result_not_true");
            }

            if (GetBool(allowedOutputs, "certification") != false)
            {
                issues.Add("allowed_outputs_certification_not_false");
            }
        }

        var shareableArtifacts = allowedOutputs.ValueKind == JsonValueKind.Object
            ? GetStringArray(allowedOutputs, "shareable_artifacts")
            : [];
        if (shareableArtifacts.Count == 0)
        {
            issues.Add("shareable_artifacts_missing");
        }

        var forbiddenOutputs = allowedOutputs.ValueKind == JsonValueKind.Object
            ? GetStringArray(allowedOutputs, "forbidden_outputs")
            : [];
        if (!forbiddenOutputs.Contains("certification", StringComparer.Ordinal))
        {
            issues.Add("certification_forbidden_output_missing");
        }

        var privacy = TryGet(challengeCase, out var privacyElement, "privacy")
            ? privacyElement
            : default;
        if (privacy.ValueKind != JsonValueKind.Object)
        {
            issues.Add("privacy_missing");
        }
        else
        {
            foreach (var flag in new[] { "source_included", "raw_diff_included", "prompt_included", "model_response_included", "secrets_included", "credentials_included" })
            {
                if (GetBool(privacy, flag) != false)
                {
                    issues.Add($"privacy_flag_not_false:{flag}");
                }
            }
        }

        return new ShieldLiteChallengeCaseResult(
            caseId,
            kind,
            issues.Count == 0 ? "passed" : "failed",
            issues.Count == 0,
            expectation.Decision,
            observedDecision,
            bandCeiling,
            reasonCodes,
            evidenceRefs,
            shareableArtifacts,
            issues);
    }

    private static ShieldLiteChallengeRunResult BuildInvalidPackResult(string challengePackPath, string issue, string message)
    {
        return new ShieldLiteChallengeRunResult(
            ShieldLiteChallengeContract.ResultSchemaVersion,
            "failed",
            "(unknown)",
            challengePackPath,
            ShieldLiteChallengeContract.SummaryLabel,
            LocalOnly: true,
            Certification: false,
            CaseCount: 1,
            PassedCount: 0,
            FailedCount: 1,
            PassRate: 0,
            Results:
            [
                new ShieldLiteChallengeCaseResult(
                    "suite",
                    "suite",
                    "failed",
                    Passed: false,
                    ExpectedDecision: null,
                    ObservedDecision: null,
                    ShieldLiteBandCeiling: null,
                    ReasonCodes: [],
                    EvidenceRefs: [],
                    ShareableArtifacts: [],
                    Issues: [issue, message]),
            ],
            NonClaims:
            [
                "local challenge result only",
                "not certified safe",
                "not certification",
                "not hosted verification",
                "not a public leaderboard",
                "not a model safety benchmark",
            ]);
    }

    private static void VerifyField(List<string> issues, string? actual, string expected, string issue)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            issues.Add(issue);
        }
    }

    private static void VerifyField(List<string> issues, bool? actual, bool expected, string issue)
    {
        if (actual != expected)
        {
            issues.Add(issue);
        }
    }

    private static string? GetString(JsonElement element, params string[] path)
    {
        return TryGet(element, out var current, path) && current.ValueKind == JsonValueKind.String
            ? current.GetString()
            : null;
    }

    private static bool? GetBool(JsonElement element, params string[] path)
    {
        return TryGet(element, out var current, path) && current.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? current.GetBoolean()
            : null;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray();
    }

    private static bool TryGet(JsonElement element, out JsonElement value, params string[] path)
    {
        value = element;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                return false;
            }
        }

        return true;
    }

    private sealed record ChallengeExpectation(
        string? Decision,
        string? BandCeiling);
}

public sealed record ShieldLiteChallengeRunResult(
    string SchemaVersion,
    string Status,
    string ChallengeSuiteId,
    string ChallengePackPath,
    string SummaryLabel,
    bool LocalOnly,
    bool Certification,
    int CaseCount,
    int PassedCount,
    int FailedCount,
    double PassRate,
    IReadOnlyList<ShieldLiteChallengeCaseResult> Results,
    IReadOnlyList<string> NonClaims)
{
    public bool IsPassed => string.Equals(Status, "passed", StringComparison.Ordinal);
}

public sealed record ShieldLiteChallengeCaseResult(
    string CaseId,
    string ChallengeKind,
    string Status,
    bool Passed,
    string? ExpectedDecision,
    string? ObservedDecision,
    string? ShieldLiteBandCeiling,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<string> ShareableArtifacts,
    IReadOnlyList<string> Issues);

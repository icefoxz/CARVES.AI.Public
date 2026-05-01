using Carves.Shield.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Carves.Shield.Tests;

internal sealed class ShieldChallengePackFixture : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private ShieldChallengePackFixture(string rootPath, JsonObject pack)
    {
        RootPath = rootPath;
        Pack = pack;
    }

    public string RootPath { get; }

    public string RelativePath => "challenge.json";

    private JsonObject Pack { get; }

    public static ShieldChallengePackFixture CreateValid()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "carves-shield-challenge-runner-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        var fixture = new ShieldChallengePackFixture(rootPath, BuildValidPack());
        fixture.Write();
        return fixture;
    }

    public void SetString(string fieldName, string value)
    {
        Pack[fieldName] = value;
    }

    public void SetBool(string fieldName, bool value)
    {
        Pack[fieldName] = value;
    }

    public JsonObject FindCase(string challengeKind)
    {
        return Cases()
            .Select(item => item!.AsObject())
            .Single(item => string.Equals(
                item["challenge_kind"]?.GetValue<string>(),
                challengeKind,
                StringComparison.Ordinal));
    }

    public JsonObject CloneCase(string challengeKind)
    {
        return JsonNode.Parse(FindCase(challengeKind).ToJsonString())!.AsObject();
    }

    public void AddCase(JsonObject challengeCase)
    {
        Cases().Add(challengeCase);
    }

    public void RemoveCase(string challengeKind)
    {
        var cases = Cases();
        var target = cases.Single(item =>
            string.Equals(item?["challenge_kind"]?.GetValue<string>(), challengeKind, StringComparison.Ordinal));
        cases.Remove(target);
    }

    public void Write()
    {
        File.WriteAllText(Path.Combine(RootPath, RelativePath), Pack.ToJsonString(JsonOptions));
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }

    private static JsonObject BuildValidPack()
    {
        var cases = new JsonArray();
        var index = 1;
        foreach (var kind in ShieldLiteChallengeContract.RequiredChallengeKinds)
        {
            cases.Add(BuildCase(kind, index++));
        }

        return new JsonObject
        {
            ["schema_version"] = ShieldLiteChallengeContract.SchemaVersion,
            ["challenge_suite_id"] = "shield-lite-direct-runner-tests",
            ["created_at_utc"] = "2026-04-15T00:00:00Z",
            ["mode"] = "local_challenge",
            ["local_only"] = true,
            ["certification"] = false,
            ["scoring_model"] = "shield-lite-scoring.v0",
            ["evidence_schema"] = "shield-evidence.v0",
            ["cases"] = cases,
            ["non_claims"] = StringArray(
                "local challenge result only",
                "not certified safe",
                "not certification",
                "not hosted verification",
                "not a public leaderboard",
                "not a model safety benchmark"),
        };
    }

    private static JsonObject BuildCase(string kind, int index)
    {
        var expectation = GetExpectation(kind);
        return new JsonObject
        {
            ["case_id"] = $"DIRECT-{index:D3}",
            ["challenge_kind"] = kind,
            ["title"] = $"Direct runner test for {kind}",
            ["intent"] = "Exercise ShieldLiteChallengeRunner without routing through CLI, PowerShell, or challenge smoke scripts.",
            ["fixture"] = new JsonObject
            {
                ["summary"] = $"Bounded fixture for {kind}.",
                ["mutations"] = new JsonArray(
                    new JsonObject
                    {
                        ["artifact"] = "shield_evidence",
                        ["operation"] = $"set {kind}",
                        ["description"] = "The local challenge expects the configured decision posture.",
                    }),
            },
            ["expected_local_decision_posture"] = new JsonObject
            {
                ["decision"] = expectation.Decision,
                ["shield_lite_band_ceiling"] = expectation.BandCeiling,
                ["reason_codes"] = StringArray(kind),
                ["evidence_refs"] = StringArray($"evidence.{kind}"),
            },
            ["allowed_outputs"] = new JsonObject
            {
                ["local_result"] = true,
                ["certification"] = false,
                ["shareable_artifacts"] = StringArray("challenge_result_json", "redacted_evidence_summary"),
                ["forbidden_outputs"] = StringArray("certification", "source_code", "raw_git_diff"),
            },
            ["privacy"] = new JsonObject
            {
                ["source_included"] = false,
                ["raw_diff_included"] = false,
                ["prompt_included"] = false,
                ["model_response_included"] = false,
                ["secrets_included"] = false,
                ["credentials_included"] = false,
            },
        };
    }

    private static ChallengeExpectation GetExpectation(string kind)
    {
        return kind switch
        {
            "protected_path_violation" => new("block", "critical"),
            "deletion_without_credible_replacement" => new("block", "critical"),
            "fake_audit_evidence" => new("reject", "critical"),
            "stale_handoff_packet" => new("review", "critical"),
            "privacy_leakage_flag" => new("reject", "critical"),
            "missing_ci_evidence" => new("review", "basic"),
            "oversized_patch" => new("review", "basic"),
            _ => throw new InvalidOperationException($"Unknown Shield challenge kind: {kind}"),
        };
    }

    private static JsonArray StringArray(params string[] values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private JsonArray Cases()
    {
        return Pack["cases"]!.AsArray();
    }

    private sealed record ChallengeExpectation(string Decision, string BandCeiling);
}

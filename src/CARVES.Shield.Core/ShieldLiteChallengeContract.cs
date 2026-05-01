namespace Carves.Shield.Core;

public static class ShieldLiteChallengeContract
{
    public const string SchemaVersion = "shield-lite-challenge.v0";
    public const string ResultSchemaVersion = "shield-lite-challenge-result.v0";
    public const string SchemaPath = "docs/shield/schemas/shield-lite-challenge-v0.schema.json";
    public const string SuiteExamplePath = "docs/shield/examples/shield-lite-challenge-suite.example.json";
    public const string StarterPackPath = "docs/shield/examples/shield-lite-starter-challenge-pack.example.json";
    public const string ResultExamplePath = "docs/shield/examples/shield-lite-challenge-result.example.json";
    public const string StarterSmokeScriptPath = "scripts/shield/shield-lite-starter-challenge-smoke.ps1";
    public const string SummaryLabel = "local challenge result, not certified safe";

    private static readonly string[] ChallengeKinds =
    [
        "protected_path_violation",
        "deletion_without_credible_replacement",
        "fake_audit_evidence",
        "stale_handoff_packet",
        "privacy_leakage_flag",
        "missing_ci_evidence",
        "oversized_patch",
    ];

    public static IReadOnlyList<string> RequiredChallengeKinds => ChallengeKinds;
}

namespace Carves.Runtime.Domain.Execution;

public sealed class RuntimePackCommandAdmissionDecision
{
    public string SchemaVersion { get; init; } = "1.0";

    public string DecisionId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public string RunId { get; init; } = string.Empty;

    public string PackSelectionId { get; init; } = string.Empty;

    public string PackId { get; init; } = string.Empty;

    public string PackVersion { get; init; } = string.Empty;

    public string Channel { get; init; } = string.Empty;

    public RuntimePackCommandRef CommandRef { get; init; } = new();

    public string RequestedKind { get; init; } = string.Empty;

    public RuntimePackCommandDecisionOutcome Decision { get; init; } = new();

    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public RuntimePackCommandPayload Command { get; init; } = new();

    public RuntimePackCommandEffectivePermissions EffectivePermissions { get; init; } = new();

    public RuntimePackCommandEvidenceExpectation Evidence { get; init; } = new();

    public IReadOnlyList<string> SourcePolicyRefs { get; init; } = Array.Empty<string>();
}

public sealed class RuntimePackCommandRef
{
    public string RecipeId { get; init; } = string.Empty;

    public string CommandId { get; init; } = string.Empty;
}

public sealed class RuntimePackCommandDecisionOutcome
{
    public string Verdict { get; init; } = string.Empty;

    public string Basis { get; init; } = string.Empty;

    public string RiskLevel { get; init; } = string.Empty;

    public IReadOnlyList<string> StopReasons { get; init; } = Array.Empty<string>();
}

public sealed class RuntimePackCommandPayload
{
    public string Executable { get; init; } = string.Empty;

    public IReadOnlyList<string> Args { get; init; } = Array.Empty<string>();

    public string Cwd { get; init; } = string.Empty;

    public bool Required { get; init; }
}

public sealed class RuntimePackCommandEffectivePermissions
{
    public bool Network { get; init; }

    public RuntimePackCommandEnvironmentPermissions Env { get; init; } = new();

    public bool Secrets { get; init; }

    public RuntimePackCommandWritePermissions Writes { get; init; } = new();
}

public sealed class RuntimePackCommandEnvironmentPermissions
{
    public string Mode { get; init; } = "none";

    public IReadOnlyList<string> Allowed { get; init; } = Array.Empty<string>();
}

public sealed class RuntimePackCommandWritePermissions
{
    public IReadOnlyList<string> AllowedPaths { get; init; } = Array.Empty<string>();

    public string ProtectedRoots { get; init; } = "deny";
}

public sealed class RuntimePackCommandEvidenceExpectation
{
    public IReadOnlyList<string> ExpectedArtifacts { get; init; } = Array.Empty<string>();

    public bool FailureIsBlocking { get; init; }
}

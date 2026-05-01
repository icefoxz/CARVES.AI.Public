namespace Carves.Runtime.Domain.Execution;

public sealed class PacketEnforcementRecord
{
    public string SchemaVersion { get; init; } = "packet-enforcement.v1";

    public string TaskId { get; init; } = string.Empty;

    public string CardId { get; init; } = string.Empty;

    public string PacketId { get; init; } = string.Empty;

    public string PlannerIntent { get; init; } = "unknown";

    public bool PacketPresent { get; init; }

    public bool PacketPersisted { get; init; }

    public bool PacketContractValid { get; init; }

    public bool ResultPresent { get; init; }

    public bool WorkerArtifactPresent { get; init; }

    public string WorkerTerminalAction { get; init; } = "submit_result";

    public bool SubmitResultAllowed { get; init; }

    public string RequestedAction { get; init; } = "none";

    public string RequestedActionClass { get; init; } = "none";

    public bool PlannerOnlyActionAttempted { get; init; }

    public bool LifecycleWritebackAttempted { get; init; }

    public IReadOnlyList<string> WorkerAllowedActions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> PlannerOnlyActions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> EditableRoots { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RepoMirrorRoots { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ResultEnvelopeChangedFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> WorkerReportedChangedFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> WorkerObservedChangedFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> EvidenceChangedFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ChangedFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> OffPacketFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> TruthWriteFiles { get; init; } = Array.Empty<string>();

    public string Verdict { get; init; } = "not_applicable";

    public IReadOnlyList<string> ReasonCodes { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> EvidencePaths { get; init; } = Array.Empty<string>();

    public string Summary { get; init; } = string.Empty;

    public DateTimeOffset EvaluatedAt { get; init; } = DateTimeOffset.UtcNow;
}

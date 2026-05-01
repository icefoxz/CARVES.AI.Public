using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Execution;

public sealed class ExecutionEvidence
{
    public static ExecutionEvidence None { get; } = new()
    {
        EvidenceSource = ExecutionEvidenceSource.Synthetic,
        EvidenceCompleteness = ExecutionEvidenceCompleteness.Missing,
        EvidenceStrength = ExecutionEvidenceStrength.Missing,
    };

    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string RunId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public string WorkerId { get; init; } = string.Empty;

    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset EndedAt { get; init; } = DateTimeOffset.UtcNow;

    public ExecutionEvidenceSource EvidenceSource { get; init; } = ExecutionEvidenceSource.Host;

    public IReadOnlyList<string> DeclaredScopeFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> FilesRead { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> FilesWritten { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> CommandsExecuted { get; init; } = Array.Empty<string>();

    public string? RepoRoot { get; init; }

    public string? WorktreePath { get; init; }

    public string? BaseCommit { get; init; }

    public string? RequestedThreadId { get; init; }

    public string? ThreadId { get; init; }

    public WorkerThreadContinuity ThreadContinuity { get; init; } = WorkerThreadContinuity.None;

    public string? EvidencePath { get; init; }

    public string? BuildOutputRef { get; init; }

    public string? TestOutputRef { get; init; }

    public string? CommandLogRef { get; init; }

    public string? PatchRef { get; init; }

    public IReadOnlyList<string> Artifacts { get; init; } = Array.Empty<string>();

    public IReadOnlyDictionary<string, string> ArtifactHashes { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public int? ExitStatus { get; init; }

    public ExecutionEvidenceCompleteness EvidenceCompleteness { get; init; } = ExecutionEvidenceCompleteness.Missing;

    public ExecutionEvidenceStrength EvidenceStrength { get; init; } = ExecutionEvidenceStrength.Missing;

    public string? CommandTraceHash { get; init; }

    public string? PatchHash { get; init; }
}

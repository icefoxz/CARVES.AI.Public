using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Domain.Execution;

public sealed class ExecutionPacket
{
    public string SchemaVersion { get; init; } = "1.0";

    public string PacketId { get; init; } = string.Empty;

    public ExecutionPacketTaskRef TaskRef { get; init; } = new();

    public string Goal { get; init; } = string.Empty;

    public PlannerIntent PlannerIntent { get; init; } = PlannerIntent.Execution;

    public IReadOnlyList<string> Scope { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> NonGoals { get; init; } = Array.Empty<string>();

    public AcceptanceContract? AcceptanceContract { get; init; }

    public ExecutionPacketContext Context { get; init; } = new();

    public ExecutionPacketPermissions Permissions { get; init; } = new();

    public ExecutionPacketBudgets Budgets { get; init; } = new();

    public ExecutionPacketClosureContract ClosureContract { get; init; } = new();

    public WorkerExecutionPacket WorkerExecutionPacket { get; init; } = new();

    public IReadOnlyList<string> RequiredValidation { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> StableEvidenceSurfaces { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> WorkerAllowedActions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> PlannerOnlyActions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> StopConditions { get; init; } = Array.Empty<string>();
}

public sealed class ExecutionPacketTaskRef
{
    public string CardId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public int TaskRevision { get; init; } = 1;
}

public sealed class ExecutionPacketContext
{
    public IReadOnlyList<string> AssemblyOrder { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> MemoryBundleRefs { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> CodegraphQueries { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RelevantFiles { get; init; } = Array.Empty<string>();

    public string? ContextPackRef { get; init; }

    public IReadOnlyList<ExecutionPacketWindowedRead> WindowedReads { get; init; } = Array.Empty<ExecutionPacketWindowedRead>();

    public ExecutionPacketContextCompaction Compaction { get; init; } = new();
}

public sealed class ExecutionPacketWindowedRead
{
    public string Path { get; init; } = string.Empty;

    public int TotalLines { get; init; }

    public int StartLine { get; init; } = 1;

    public int EndLine { get; init; }

    public string Reason { get; init; } = string.Empty;

    public bool Truncated { get; init; }
}

public sealed class ExecutionPacketContextCompaction
{
    public string Strategy { get; init; } = "bounded_scope_projection";

    public int CandidateFileCount { get; init; }

    public int RelevantFileCount { get; init; }

    public int WindowedReadCount { get; init; }

    public int FullReadCount { get; init; }

    public int OmittedFileCount { get; init; }
}

public sealed class ExecutionPacketPermissions
{
    public IReadOnlyList<string> EditableRoots { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ReadOnlyRoots { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> TruthRoots { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RepoMirrorRoots { get; init; } = Array.Empty<string>();
}

public sealed class ExecutionPacketBudgets
{
    public int MaxFilesChanged { get; init; }

    public int MaxLinesChanged { get; init; }

    public int MaxShellCommands { get; init; }
}

public sealed class ExecutionPacketClosureContract
{
    public string SchemaVersion { get; init; } = "execution-closure-contract.v1";

    public string ProtocolId { get; init; } = "result_closure_protocol_v1";

    public string ContractMatrixProfileId { get; init; } = "generic_review_closure_v1";

    public string Summary { get; init; } = "Worker submits a candidate result; Host validates closure before review writeback.";

    public IReadOnlyList<string> RequiredContractChecks { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RequiredValidationGates { get; init; } = Array.Empty<string>();

    public bool CompletionClaimRequired { get; init; } = true;

    public IReadOnlyList<string> CompletionClaimFields { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ForbiddenVocabulary { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> EvidenceSurfaces { get; init; } = Array.Empty<string>();
}

public sealed class WorkerExecutionPacket
{
    public string SchemaVersion { get; init; } = "worker-execution-packet.v1";

    public string PacketId { get; init; } = string.Empty;

    public string SourceExecutionPacketId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public string Goal { get; init; } = string.Empty;

    public IReadOnlyList<string> AllowedFiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AllowedActions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RequiredContractMatrix { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RequiredValidation { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> RequiredValidationGates { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> EvidenceRequired { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ForbiddenVocabulary { get; init; } = Array.Empty<string>();

    public WorkerCompletionClaimSchema CompletionClaimSchema { get; init; } = new();

    public WorkerResultSubmissionContract ResultSubmission { get; init; } = new();

    public bool GrantsLifecycleTruthAuthority { get; init; }

    public bool GrantsTruthWriteAuthority { get; init; }

    public bool CreatesTaskQueue { get; init; }

    public bool WritesTruthRoots { get; init; }

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed class WorkerCompletionClaimSchema
{
    public string SchemaVersion { get; init; } = "worker-completion-claim-schema.v1";

    public bool Required { get; init; } = true;

    public IReadOnlyList<string> Fields { get; init; } = Array.Empty<string>();

    public bool ClaimIsTruth { get; init; }

    public bool HostValidationRequired { get; init; } = true;
}

public sealed class WorkerResultSubmissionContract
{
    public string SchemaVersion { get; init; } = "worker-result-submission.v1";

    public string CandidateResultChannel { get; init; } = string.Empty;

    public string HostIngestCommand { get; init; } = string.Empty;

    public bool CandidateOnly { get; init; } = true;

    public bool ReviewBundleRequired { get; init; } = true;

    public bool SubmittedByHostOrAdapter { get; init; } = true;

    public bool WorkerDirectTruthWriteAllowed { get; init; }
}

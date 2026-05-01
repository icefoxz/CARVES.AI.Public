using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class AuthoritativeTruthStoreSurface
{
    public string SurfaceId { get; init; } = "authoritative-truth-store";

    public string RepoRoot { get; init; } = string.Empty;

    public string AuthoritativeRoot { get; init; } = string.Empty;

    public string MirrorRoot { get; init; } = string.Empty;

    public bool ExternalToRepo { get; init; }

    public string Summary { get; init; } = string.Empty;

    public AuthoritativeTruthWriterLockSurface? WriterLock { get; init; }

    public IReadOnlyList<AuthoritativeTruthFamilyBinding> Families { get; init; } = Array.Empty<AuthoritativeTruthFamilyBinding>();
}

public sealed class AuthoritativeTruthWriterLockSurface
{
    public string Scope { get; init; } = string.Empty;

    public string LeasePath { get; init; } = string.Empty;

    public string State { get; init; } = "unknown";

    public string? Resource { get; init; }

    public string? Operation { get; init; }

    public string Mode { get; init; } = "write";

    public string OwnerId { get; init; } = string.Empty;

    public int? OwnerProcessId { get; init; }

    public string? OwnerProcessName { get; init; }

    public DateTimeOffset? AcquiredAt { get; init; }

    public DateTimeOffset? LastHeartbeat { get; init; }

    public double? TtlSeconds { get; init; }

    public string Summary { get; init; } = string.Empty;
}

public sealed class AuthoritativeTruthFamilyBinding
{
    public string FamilyId { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string ReadMode { get; init; } = "authoritative_first";

    public string WriteMode { get; init; } = "authoritative_then_mirror";

    public string AuthoritativePath { get; init; } = string.Empty;

    public string MirrorPath { get; init; } = string.Empty;

    public bool AuthoritativeExists { get; init; }

    public bool MirrorExists { get; init; }

    public bool MirrorDriftDetected { get; init; }

    public string MirrorState { get; init; } = "unknown";

    public string MirrorSummary { get; init; } = string.Empty;

    public AuthoritativeTruthMirrorSyncStatus MirrorSync { get; init; } = new();
}

public sealed class AuthoritativeTruthMirrorSyncStatus
{
    public string ReceiptPath { get; init; } = string.Empty;

    public string Outcome { get; init; } = "not_recorded";

    public string Summary { get; init; } = "No mirror synchronization receipt recorded for this family.";

    public string Resource { get; init; } = string.Empty;

    public DateTimeOffset? LastAuthoritativeWriteAt { get; init; }

    public DateTimeOffset? LastMirrorSyncAttemptAt { get; init; }

    public DateTimeOffset? LastSuccessfulMirrorSyncAt { get; init; }
}

namespace Carves.Runtime.Domain.Platform;

public sealed record AttachHandshakeRequestRecord
{
    public string RepoPath { get; init; } = string.Empty;

    public string GitRoot { get; init; } = string.Empty;

    public string Branch { get; init; } = string.Empty;

    public string ClientVersion { get; init; } = string.Empty;

    public string RuntimeVersion { get; init; } = string.Empty;

    public string ClientRepoRoot { get; init; } = string.Empty;

    public string RuntimeRoot { get; init; } = string.Empty;
}

public sealed record AttachHandshakeAcknowledgement
{
    public string RepoId { get; init; } = string.Empty;

    public string HostSessionId { get; init; } = string.Empty;

    public string Status { get; init; } = "attached";

    public DateTimeOffset AttachedAt { get; init; } = DateTimeOffset.UtcNow;

    public string RuntimeStatus { get; init; } = string.Empty;

    public string RepoSummary { get; init; } = string.Empty;

    public string AttachMode { get; init; } = string.Empty;
}

public sealed record AttachHandshakeRecord
{
    public string SchemaVersion { get; init; } = "attach-handshake.v1";

    public AttachHandshakeRequestRecord Request { get; init; } = new();

    public AttachHandshakeAcknowledgement Acknowledgement { get; init; } = new();
}

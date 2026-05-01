namespace Carves.Runtime.Application.AI;

public sealed class ProviderProtocolMetadata
{
    public string ProtocolId { get; init; } = string.Empty;

    public string ProtocolFamily { get; init; } = string.Empty;

    public string RequestFamily { get; init; } = string.Empty;

    public bool SupportsStreaming { get; init; }

    public bool SupportsToolCalls { get; init; }

    public bool SupportsJsonMode { get; init; }

    public bool SupportsSystemPrompt { get; init; }

    public bool SupportsFileUpload { get; init; }
}

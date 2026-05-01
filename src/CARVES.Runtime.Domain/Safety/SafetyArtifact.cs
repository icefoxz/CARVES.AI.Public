using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Safety;

public sealed class SafetyArtifact
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

    public SafetyDecision Decision { get; init; } = SafetyDecision.Allow(string.Empty);
}

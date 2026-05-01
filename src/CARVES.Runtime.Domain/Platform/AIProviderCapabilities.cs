namespace Carves.Runtime.Domain.Platform;

public sealed record AIProviderCapabilities(
    bool SupportsPlanning,
    bool SupportsCodeGeneration,
    bool SupportsLongContext,
    bool SupportsStructuredOutput,
    bool SupportsToolUse);

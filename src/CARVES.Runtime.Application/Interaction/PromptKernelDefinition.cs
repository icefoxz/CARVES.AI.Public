namespace Carves.Runtime.Application.Interaction;

public sealed record PromptKernelDefinition(
    string KernelId,
    string Version,
    string SourcePath,
    IReadOnlyList<string> SupportedRoles,
    string Summary,
    string Body);

namespace Carves.Runtime.Application.Interaction;

public sealed record PromptTemplateDefinition(
    string TemplateId,
    string Version,
    string Context,
    string SourcePath,
    IReadOnlyList<string> Sections,
    string Summary,
    string Body);

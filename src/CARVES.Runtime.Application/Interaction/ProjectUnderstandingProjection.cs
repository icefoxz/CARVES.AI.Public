namespace Carves.Runtime.Application.Interaction;

public sealed record ProjectUnderstandingProjection(
    ProjectUnderstandingState State,
    string Action,
    string Summary,
    string Rationale,
    DateTimeOffset? GeneratedAt,
    int ModuleCount,
    int FileCount,
    int CallableCount,
    int DependencyCount,
    IReadOnlyList<string> ModuleSummaries);

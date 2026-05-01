namespace Carves.Runtime.Application.CodeGraph;

public sealed class CodeGraphAuditReport
{
    public string SchemaVersion { get; init; } = "codegraph-audit.v1";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public bool StrictPassed { get; init; }

    public int ModuleCount { get; init; }

    public int FileCount { get; init; }

    public int CallableCount { get; init; }

    public int DependencyCount { get; init; }

    public IReadOnlyList<CodeGraphAuditFinding> Findings { get; init; } = Array.Empty<CodeGraphAuditFinding>();

    public IReadOnlyList<CodeGraphModulePurity> ModulePurity { get; init; } = Array.Empty<CodeGraphModulePurity>();

    public CodeGraphAuditDelta? DeltaFromPrevious { get; init; }
}

public sealed record CodeGraphAuditFinding(
    string Category,
    string Severity,
    string Path,
    string Message);

public sealed record CodeGraphModulePurity(
    string Module,
    int SourceFiles,
    int TotalFiles,
    double PurityRatio);

public sealed record CodeGraphAuditDelta(
    int ModuleDelta,
    int FileDelta,
    int CallableDelta,
    int DependencyDelta);

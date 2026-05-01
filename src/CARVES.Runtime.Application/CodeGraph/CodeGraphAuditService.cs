using System.Text.Json;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.CodeGraph;

namespace Carves.Runtime.Application.CodeGraph;

public sealed class CodeGraphAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly SystemConfig systemConfig;
    private readonly ICodeGraphQueryService codeGraphQueryService;

    public CodeGraphAuditService(
        string repoRoot,
        ControlPlanePaths paths,
        SystemConfig systemConfig,
        ICodeGraphQueryService codeGraphQueryService)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
        this.systemConfig = systemConfig;
        this.codeGraphQueryService = codeGraphQueryService;
    }

    public CodeGraphAuditReport Audit()
    {
        var previous = LoadPrevious();
        var index = codeGraphQueryService.LoadIndex();
        var findings = new List<CodeGraphAuditFinding>();

        foreach (var file in index.Files)
        {
            if (!CodeGraphSourceTruthPolicy.ShouldTrackRelativePath(file.Path, systemConfig))
            {
                findings.Add(new CodeGraphAuditFinding(
                    "forbidden_path_leakage",
                    "error",
                    file.Path,
                    "Indexed file is outside codegraph source-of-truth policy."));
            }

            if (!string.Equals(file.Language, "csharp", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(file.Language, "python", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new CodeGraphAuditFinding(
                    "non_source_language",
                    "error",
                    file.Path,
                    $"Indexed file language '{file.Language}' is not part of the source-truth whitelist."));
            }
        }

        var modulePurity = index.Modules
            .Select(module =>
            {
                var moduleFiles = index.Files.Where(file => string.Equals(file.Module, module.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
                var sourceFiles = moduleFiles.Count(file =>
                    string.Equals(file.Language, "csharp", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(file.Language, "python", StringComparison.OrdinalIgnoreCase));
                var purity = moduleFiles.Length == 0 ? 1d : (double)sourceFiles / moduleFiles.Length;
                return new CodeGraphModulePurity(module.Name, sourceFiles, moduleFiles.Length, purity);
            })
            .OrderBy(item => item.Module, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var module in modulePurity.Where(item => item.PurityRatio < 1d))
        {
            findings.Add(new CodeGraphAuditFinding(
                "module_purity",
                "error",
                module.Module,
                $"Module purity dropped below 1.0 ({module.SourceFiles}/{module.TotalFiles})."));
        }

        var report = new CodeGraphAuditReport
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            StrictPassed = findings.Count == 0,
            ModuleCount = index.Modules.Count,
            FileCount = index.Files.Count,
            CallableCount = index.Callables.Count,
            DependencyCount = index.Dependencies.Count,
            Findings = findings,
            ModulePurity = modulePurity,
            DeltaFromPrevious = previous is null
                ? null
                : new CodeGraphAuditDelta(
                    index.Modules.Count - previous.ModuleCount,
                    index.Files.Count - previous.FileCount,
                    index.Callables.Count - previous.CallableCount,
                    index.Dependencies.Count - previous.DependencyCount),
        };

        Save(report);
        return report;
    }

    private CodeGraphAuditReport? LoadPrevious()
    {
        var path = GetAuditPath();
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<CodeGraphAuditReport>(File.ReadAllText(path), JsonOptions);
    }

    private void Save(CodeGraphAuditReport report)
    {
        var path = GetAuditPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonOptions));
    }

    private string GetAuditPath()
    {
        return Path.Combine(paths.AiRoot, "codegraph", "audit.json");
    }
}

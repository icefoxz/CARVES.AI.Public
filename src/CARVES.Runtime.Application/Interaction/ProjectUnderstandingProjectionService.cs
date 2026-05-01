using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.CodeGraph;

namespace Carves.Runtime.Application.Interaction;

public sealed class ProjectUnderstandingProjectionService
{
    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly SystemConfig systemConfig;
    private readonly ICodeGraphBuilder codeGraphBuilder;
    private readonly ICodeGraphQueryService codeGraphQueryService;

    public ProjectUnderstandingProjectionService(
        string repoRoot,
        ControlPlanePaths paths,
        SystemConfig systemConfig,
        ICodeGraphBuilder codeGraphBuilder,
        ICodeGraphQueryService codeGraphQueryService)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
        this.systemConfig = systemConfig;
        this.codeGraphBuilder = codeGraphBuilder;
        this.codeGraphQueryService = codeGraphQueryService;
    }

    public ProjectUnderstandingProjection Evaluate(bool hydrateIfNeeded)
    {
        var manifestPath = Path.Combine(paths.AiRoot, "codegraph", "manifest.json");
        var summariesPath = Path.Combine(paths.AiRoot, "codegraph", "summaries");
        var state = ClassifyState(manifestPath);
        var action = "reused";
        var rationale = state switch
        {
            ProjectUnderstandingState.Missing => "codegraph truth is missing",
            ProjectUnderstandingState.Stale => "codegraph truth is older than tracked source files",
            ProjectUnderstandingState.Fresh => "existing codegraph truth is fresh",
            _ => "project understanding projection was deferred",
        };

        CodeGraphManifest? manifest = null;
        IReadOnlyList<CodeGraphModuleEntry>? moduleSummaries = null;
        if (state is ProjectUnderstandingState.Missing or ProjectUnderstandingState.Stale)
        {
            if (hydrateIfNeeded)
            {
                try
                {
                    codeGraphBuilder.Build();
                    state = ProjectUnderstandingState.Fresh;
                    action = "refreshed";
                    rationale = "attach refreshed the existing codegraph projection";
                }
                catch (Exception exception)
                {
                    state = ProjectUnderstandingState.Deferred;
                    action = "deferred";
                    rationale = $"project understanding refresh was deferred: {exception.Message}";
                }
            }
            else
            {
                action = "deferred";
            }
        }

        if (state == ProjectUnderstandingState.Fresh)
        {
            if (!File.Exists(manifestPath))
            {
                codeGraphBuilder.Build();
            }

            manifest = codeGraphQueryService.LoadManifest();
            moduleSummaries = codeGraphQueryService.LoadModuleSummaries();
        }

        if (state is ProjectUnderstandingState.Missing or ProjectUnderstandingState.Deferred || manifest is null || moduleSummaries is null)
        {
            return new ProjectUnderstandingProjection(
                state,
                action,
                "codegraph summary is not yet available",
                rationale,
                null,
                0,
                0,
                0,
                0,
                Array.Empty<string>());
        }

        var summaryLines = moduleSummaries
            .Take(3)
            .Select(module => BuildModuleSummary(module, summariesPath))
            .ToArray();

        return new ProjectUnderstandingProjection(
            state,
            action,
            $"modules={manifest.ModuleCount}; files={manifest.FileCount}; callables={manifest.CallableCount}; dependencies={manifest.DependencyCount}",
            rationale,
            manifest.GeneratedAt,
            manifest.ModuleCount,
            manifest.FileCount,
            manifest.CallableCount,
            manifest.DependencyCount,
            summaryLines);
    }

    private ProjectUnderstandingState ClassifyState(string indexPath)
    {
        if (!File.Exists(indexPath))
        {
            return ProjectUnderstandingState.Missing;
        }

        var latestSourceWrite = GetLatestSourceWrite();
        if (latestSourceWrite is null)
        {
            return ProjectUnderstandingState.Fresh;
        }

        return latestSourceWrite > File.GetLastWriteTimeUtc(indexPath)
            ? ProjectUnderstandingState.Stale
            : ProjectUnderstandingState.Fresh;
    }

    private DateTime? GetLatestSourceWrite()
    {
        DateTime? latest = null;
        foreach (var codeDirectory in CodeDirectoryDiscoveryPolicy.ResolveEffectiveDirectories(repoRoot, systemConfig))
        {
            var directory = codeDirectory == "."
                ? repoRoot
                : Path.Combine(repoRoot, codeDirectory.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (!CodeGraphSourceTruthPolicy.ShouldTrackFile(repoRoot, file, systemConfig))
                {
                    continue;
                }

                var writeTime = File.GetLastWriteTimeUtc(file);
                if (latest is null || writeTime > latest)
                {
                    latest = writeTime;
                }
            }
        }

        return latest;
    }

    private static string BuildModuleSummary(CodeGraphModuleEntry module, string summariesPath)
    {
        var summaryFile = Path.Combine(summariesPath, $"{module.Name}.md");
        if (!File.Exists(summaryFile))
        {
            return module.Summary;
        }

        var line = File.ReadLines(summaryFile)
            .FirstOrDefault(content => !string.IsNullOrWhiteSpace(content) && !content.StartsWith('#'));
        return string.IsNullOrWhiteSpace(line) ? module.Summary : line.Trim();
    }
}

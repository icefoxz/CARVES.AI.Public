using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Infrastructure.CodeGraph;

internal static class FileCodeGraphStorage
{
    public static string GetCodeGraphRoot(ControlPlanePaths paths) => Path.Combine(paths.AiRoot, "codegraph");

    public static string GetIndexPath(ControlPlanePaths paths) => Path.Combine(GetCodeGraphRoot(paths), "index.json");

    public static string GetManifestPath(ControlPlanePaths paths) => Path.Combine(GetCodeGraphRoot(paths), "manifest.json");

    public static string GetSearchRoot(ControlPlanePaths paths) => Path.Combine(GetCodeGraphRoot(paths), "search");

    public static string GetSearchIndexPath(ControlPlanePaths paths) => Path.Combine(GetSearchRoot(paths), "index.json");

    public static string GetModulesRoot(ControlPlanePaths paths) => Path.Combine(GetCodeGraphRoot(paths), "modules");

    public static string GetDependenciesRoot(ControlPlanePaths paths) => Path.Combine(GetCodeGraphRoot(paths), "dependencies");

    public static string GetDependencyShardPath(ControlPlanePaths paths) => Path.Combine(GetDependenciesRoot(paths), "module-deps.json");

    public static string GetSummariesRoot(ControlPlanePaths paths) => Path.Combine(GetCodeGraphRoot(paths), "summaries");

    public static string GetModuleShardPath(ControlPlanePaths paths, string moduleName)
    {
        return Path.Combine(GetModulesRoot(paths), $"{GetSafeModuleFileName(moduleName)}.json");
    }

    public static string GetModuleSummaryMarkdownPath(ControlPlanePaths paths, string moduleName)
    {
        return Path.Combine(GetSummariesRoot(paths), $"{moduleName}.md");
    }

    public static string GetRelativeCodeGraphPath(ControlPlanePaths paths, string path)
    {
        return Path.GetRelativePath(GetCodeGraphRoot(paths), path)
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    public static string GetSafeModuleFileName(string moduleName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var buffer = new char[moduleName.Length];
        for (var index = 0; index < moduleName.Length; index++)
        {
            var character = moduleName[index];
            buffer[index] = invalid.Contains(character) ? '_' : character switch
            {
                '/' or '\\' or ':' => '_',
                _ => character,
            };
        }

        return new string(buffer).Replace(' ', '_');
    }
}

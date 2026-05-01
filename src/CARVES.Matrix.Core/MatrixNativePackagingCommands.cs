using System.Runtime.InteropServices;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static MatrixNativePackageResult PackNativeTool(
        string runtimeRoot,
        string packageRoot,
        string configuration,
        string version,
        MatrixNativePackageSpec spec)
    {
        var packagePath = Path.Combine(packageRoot, $"{spec.PackageId}.{version}.nupkg");
        var projectPath = ResolveNativeRelativePath(runtimeRoot, spec.ProjectRelativePath);
        var result = InvokeProcess(
            "dotnet",
            [
                "pack",
                projectPath,
                "--configuration",
                configuration,
                "--output",
                packageRoot,
                "/p:PackageVersion=" + version,
                "--no-restore",
            ],
            runtimeRoot);
        var reasonCodes = BuildPackReasonCodes(result, packagePath);
        return new MatrixNativePackageResult(
            spec.ToolName,
            spec.PackageId,
            version,
            packagePath,
            result.ExitCode,
            reasonCodes.Count == 0,
            BuildPreview(result.Stdout, result.ExitCode == 0),
            BuildPreview(result.Stderr, result.ExitCode == 0),
            reasonCodes);
    }

    private static MatrixNativeToolInstallResult InstallNativeTool(
        string runtimeRoot,
        string packageRoot,
        string toolRoot,
        string version,
        MatrixNativePackageSpec spec)
    {
        var result = InvokeProcess(
            "dotnet",
            [
                "tool",
                "install",
                spec.PackageId,
                "--tool-path",
                toolRoot,
                "--add-source",
                packageRoot,
                "--version",
                version,
                "--ignore-failed-sources",
            ],
            runtimeRoot);
        var commandPath = ResolveInstalledCommandPath(toolRoot, spec.CommandName);
        var reasonCodes = BuildInstallReasonCodes(result, commandPath);
        return new MatrixNativeToolInstallResult(
            spec.ToolName,
            spec.PackageId,
            version,
            result.ExitCode,
            reasonCodes.Count == 0,
            File.Exists(commandPath) ? commandPath : null,
            BuildPreview(result.Stdout, result.ExitCode == 0),
            BuildPreview(result.Stderr, result.ExitCode == 0),
            reasonCodes);
    }

    private static List<string> BuildPackReasonCodes(ScriptResult result, string packagePath)
    {
        var reasonCodes = new List<string>();
        if (result.ExitCode != 0)
        {
            reasonCodes.Add("native_packaging_pack_failed");
        }

        if (!File.Exists(packagePath))
        {
            reasonCodes.Add("native_packaging_expected_package_missing");
        }

        return reasonCodes;
    }

    private static List<string> BuildInstallReasonCodes(ScriptResult result, string commandPath)
    {
        var reasonCodes = new List<string>();
        if (result.ExitCode != 0)
        {
            reasonCodes.Add("native_packaging_tool_install_failed");
        }

        if (!File.Exists(commandPath))
        {
            reasonCodes.Add("native_packaging_command_missing");
        }

        return reasonCodes;
    }

    private static string ResolveInstalledCommandPath(string toolRoot, string commandName)
    {
        var suffix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
        return Path.Combine(Path.GetFullPath(toolRoot), commandName + suffix);
    }

    private static string? BuildPreview(string value, bool commandPassed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return commandPassed ? null : TruncateForJson(value);
    }

    private static MatrixNativeInstalledCommands BuildInstalledCommands(
        IReadOnlyList<MatrixNativeToolInstallResult> installs)
    {
        return new MatrixNativeInstalledCommands(
            CarvesGuard: RequiredInstalledCommand(installs, "guard"),
            CarvesHandoff: RequiredInstalledCommand(installs, "handoff"),
            CarvesAudit: RequiredInstalledCommand(installs, "audit"),
            CarvesShield: RequiredInstalledCommand(installs, "shield"),
            CarvesMatrix: RequiredInstalledCommand(installs, "matrix"));
    }

    private static string RequiredInstalledCommand(
        IReadOnlyList<MatrixNativeToolInstallResult> installs,
        string toolName)
    {
        return installs.First(install => string.Equals(install.ToolName, toolName, StringComparison.Ordinal)).CommandPath
               ?? throw new InvalidOperationException($"Missing installed command path for {toolName}.");
    }
}

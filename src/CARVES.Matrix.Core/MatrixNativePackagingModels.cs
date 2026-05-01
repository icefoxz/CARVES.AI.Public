namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    internal sealed record MatrixNativePackagingHarnessOptions(
        string RuntimeRoot,
        string PackageRoot,
        string ToolRoot,
        string Configuration = "Release",
        string Version = "0.2.0-alpha.1",
        string? GuardVersion = null,
        string? HandoffVersion = null,
        string? AuditVersion = null,
        string? ShieldVersion = null,
        string? MatrixVersion = null);

    internal sealed record MatrixNativePackagingHarnessResult(
        int ExitCode,
        string Status,
        string PackageRoot,
        string ToolRoot,
        IReadOnlyList<MatrixNativePackageResult> Packages,
        IReadOnlyList<MatrixNativeToolInstallResult> ToolInstalls,
        MatrixNativeInstalledCommands? InstalledCommands,
        IReadOnlyList<string> ReasonCodes);

    internal sealed record MatrixNativePackageSpec(
        string ToolName,
        string PackageId,
        string ProjectRelativePath,
        string CommandName);

    internal sealed record MatrixNativePackageResult(
        string ToolName,
        string PackageId,
        string Version,
        string PackagePath,
        int ExitCode,
        bool Passed,
        string? StdoutPreview,
        string? StderrPreview,
        IReadOnlyList<string> ReasonCodes);

    internal sealed record MatrixNativeToolInstallResult(
        string ToolName,
        string PackageId,
        string Version,
        int ExitCode,
        bool Passed,
        string? CommandPath,
        string? StdoutPreview,
        string? StderrPreview,
        IReadOnlyList<string> ReasonCodes);

    internal sealed record MatrixNativeInstalledCommands(
        string CarvesGuard,
        string CarvesHandoff,
        string CarvesAudit,
        string CarvesShield,
        string CarvesMatrix);
}

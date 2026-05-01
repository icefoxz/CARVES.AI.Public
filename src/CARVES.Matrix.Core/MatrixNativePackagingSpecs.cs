namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static readonly MatrixNativePackageSpec[] NativePackagingSpecs =
    [
        new(
            ToolName: "guard",
            PackageId: "CARVES.Guard.Cli",
            ProjectRelativePath: "src/CARVES.Guard.Cli/Carves.Guard.Cli.csproj",
            CommandName: "carves-guard"),
        new(
            ToolName: "handoff",
            PackageId: "CARVES.Handoff.Cli",
            ProjectRelativePath: "src/CARVES.Handoff.Cli/Carves.Handoff.Cli.csproj",
            CommandName: "carves-handoff"),
        new(
            ToolName: "audit",
            PackageId: "CARVES.Audit.Cli",
            ProjectRelativePath: "src/CARVES.Audit.Cli/Carves.Audit.Cli.csproj",
            CommandName: "carves-audit"),
        new(
            ToolName: "shield",
            PackageId: "CARVES.Shield.Cli",
            ProjectRelativePath: "src/CARVES.Shield.Cli/Carves.Shield.Cli.csproj",
            CommandName: "carves-shield"),
        new(
            ToolName: "matrix",
            PackageId: "CARVES.Matrix.Cli",
            ProjectRelativePath: "src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj",
            CommandName: "carves-matrix"),
    ];
}

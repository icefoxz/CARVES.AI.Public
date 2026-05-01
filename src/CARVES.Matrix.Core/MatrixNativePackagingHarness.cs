namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    internal static MatrixNativePackagingHarnessResult RunNativePackagingHarness(
        MatrixNativePackagingHarnessOptions options)
    {
        var packageResults = new List<MatrixNativePackageResult>();
        var installResults = new List<MatrixNativeToolInstallResult>();
        var packageRoot = Path.GetFullPath(options.PackageRoot);
        var toolRoot = Path.GetFullPath(options.ToolRoot);

        if (!IsSupportedPackagingConfiguration(options.Configuration))
        {
            return FailedPackagingHarness(
                packageRoot,
                toolRoot,
                packageResults,
                installResults,
                ["native_packaging_configuration_invalid"]);
        }

        try
        {
            var runtimeRoot = Path.GetFullPath(options.RuntimeRoot);
            Directory.CreateDirectory(packageRoot);
            Directory.CreateDirectory(toolRoot);

            foreach (var spec in NativePackagingSpecs)
            {
                var version = ResolveNativePackageVersion(options, spec);
                var package = PackNativeTool(runtimeRoot, packageRoot, options.Configuration, version, spec);
                packageResults.Add(package);
                if (!package.Passed)
                {
                    return FailedPackagingHarness(
                        packageRoot,
                        toolRoot,
                        packageResults,
                        installResults,
                        package.ReasonCodes);
                }
            }

            foreach (var spec in NativePackagingSpecs)
            {
                var version = ResolveNativePackageVersion(options, spec);
                var install = InstallNativeTool(runtimeRoot, packageRoot, toolRoot, version, spec);
                installResults.Add(install);
                if (!install.Passed)
                {
                    return FailedPackagingHarness(
                        packageRoot,
                        toolRoot,
                        packageResults,
                        installResults,
                        install.ReasonCodes);
                }
            }

            return new MatrixNativePackagingHarnessResult(
                ExitCode: 0,
                Status: "passed",
                PackageRoot: packageRoot,
                ToolRoot: toolRoot,
                Packages: packageResults,
                ToolInstalls: installResults,
                InstalledCommands: BuildInstalledCommands(installResults),
                ReasonCodes: []);
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or ArgumentException
                                   or NotSupportedException
                                   or InvalidOperationException
                                   or System.ComponentModel.Win32Exception)
        {
            return FailedPackagingHarness(
                packageRoot,
                toolRoot,
                packageResults,
                installResults,
                ["native_packaging_harness_failed"]);
        }
    }

    private static bool IsSupportedPackagingConfiguration(string configuration)
    {
        return string.Equals(configuration, "Debug", StringComparison.Ordinal)
               || string.Equals(configuration, "Release", StringComparison.Ordinal);
    }

    private static string ResolveNativePackageVersion(
        MatrixNativePackagingHarnessOptions options,
        MatrixNativePackageSpec spec)
    {
        return spec.ToolName switch
        {
            "guard" => options.GuardVersion ?? options.Version,
            "handoff" => options.HandoffVersion ?? options.Version,
            "audit" => options.AuditVersion ?? options.Version,
            "shield" => options.ShieldVersion ?? options.Version,
            "matrix" => options.MatrixVersion ?? options.Version,
            _ => options.Version,
        };
    }

    private static MatrixNativePackagingHarnessResult FailedPackagingHarness(
        string packageRoot,
        string toolRoot,
        IReadOnlyList<MatrixNativePackageResult> packages,
        IReadOnlyList<MatrixNativeToolInstallResult> installs,
        IReadOnlyList<string> reasonCodes)
    {
        return new MatrixNativePackagingHarnessResult(
            ExitCode: 1,
            Status: "failed",
            PackageRoot: packageRoot,
            ToolRoot: toolRoot,
            Packages: packages,
            ToolInstalls: installs,
            InstalledCommands: null,
            ReasonCodes: reasonCodes);
    }
}

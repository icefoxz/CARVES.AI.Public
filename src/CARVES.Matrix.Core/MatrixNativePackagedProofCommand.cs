using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    internal static MatrixNativePackagedProofResult ProduceNativeFullReleasePackagedArtifacts(
        string artifactRoot,
        string? runtimeRoot = null,
        string? workRoot = null,
        string configuration = "Release",
        string version = "0.2.0-alpha.1",
        string? guardVersion = null,
        string? handoffVersion = null,
        string? auditVersion = null,
        string? shieldVersion = null,
        string? matrixVersion = null,
        bool keep = false)
    {
        var fullArtifactRoot = Path.GetFullPath(artifactRoot);
        var outputPath = ResolveNativeRelativePath(fullArtifactRoot, "packaged-matrix-output.json");
        var packagedRoot = ResolveNativeRelativePath(fullArtifactRoot, "packaged");
        var steps = new List<MatrixNativeProofStep>();
        string? fullWorkRoot = null;

        try
        {
            var fullRuntimeRoot = ResolveRuntimeRoot(runtimeRoot);
            if (fullRuntimeRoot is null)
            {
                return WriteNativeFullReleasePackagedFailure(fullArtifactRoot, outputPath, steps, "runtime_root", ["native_packaged_runtime_root_missing"], "Unable to locate CARVES.Runtime root.");
            }

            ResetNativeFullReleasePackagedArtifacts(fullArtifactRoot, packagedRoot);
            if (!IsSupportedPackagingConfiguration(configuration))
            {
                return WriteNativeFullReleasePackagedFailure(fullArtifactRoot, outputPath, steps, "configuration", ["native_packaged_configuration_invalid"], $"Invalid native packaged proof configuration: {configuration}. Expected Debug or Release.");
            }

            fullWorkRoot = string.IsNullOrWhiteSpace(workRoot)
                ? Path.Combine(Path.GetTempPath(), "carves-matrix-native-packaged-" + Guid.NewGuid().ToString("N"))
                : Path.GetFullPath(workRoot);
            Directory.CreateDirectory(fullWorkRoot);

            var harness = RunNativePackagingHarness(new MatrixNativePackagingHarnessOptions(
                RuntimeRoot: fullRuntimeRoot,
                PackageRoot: Path.Combine(fullWorkRoot, "packages"),
                ToolRoot: Path.Combine(fullWorkRoot, "tools"),
                Configuration: configuration,
                Version: version,
                GuardVersion: guardVersion,
                HandoffVersion: handoffVersion,
                AuditVersion: auditVersion,
                ShieldVersion: shieldVersion,
                MatrixVersion: matrixVersion));
            if (harness.ExitCode != 0 || harness.InstalledCommands is null)
            {
                return WriteNativeFullReleasePackagedFailure(fullArtifactRoot, outputPath, steps, "packaging", harness.ReasonCodes, "Native packaged proof package build or tool install failed.");
            }

            var matrixJson = RunNativeInstalledMatrixChain(
                fullRuntimeRoot,
                fullWorkRoot,
                packagedRoot,
                harness.InstalledCommands,
                steps,
                out var matrixFailure);
            if (matrixJson is null)
            {
                return WriteNativeFullReleasePackagedFailure(fullArtifactRoot, outputPath, steps, matrixFailure.FailedStepId, matrixFailure.ReasonCodes, matrixFailure.Message);
            }

            var summaryJson = WriteNativeFullReleasePackagedArtifacts(
                fullArtifactRoot,
                matrixJson.Value,
                harness);
            File.WriteAllText(outputPath, summaryJson);
            return new MatrixNativePackagedProofResult(
                ExitCode: 0,
                Status: "passed",
                ArtifactRoot: ToPublicArtifactRootMarker(),
                PackagedMatrixOutputPath: "packaged-matrix-output.json",
                ReasonCodes: []);
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or ArgumentException
                                   or NotSupportedException
                                   or InvalidOperationException
                                   or JsonException
                                   or System.ComponentModel.Win32Exception)
        {
            return WriteNativeFullReleasePackagedFailure(fullArtifactRoot, outputPath, steps, "native_packaged_proof", ["native_packaged_proof_failed"], "Native packaged proof failed before packaged summary output was produced.");
        }
        finally
        {
            if (!keep && !string.IsNullOrWhiteSpace(fullWorkRoot))
            {
                TryDeleteDirectory(fullWorkRoot);
            }
        }
    }
}

using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    internal static MatrixNativeProjectProofResult ProduceNativeFullReleaseProjectArtifacts(
        string artifactRoot,
        string? workRoot = null,
        string configuration = "Release",
        bool keep = false)
    {
        var fullArtifactRoot = Path.GetFullPath(artifactRoot);
        var outputPath = ResolveNativeRelativePath(fullArtifactRoot, "project-matrix-output.json");
        var steps = new List<MatrixNativeProofStep>();
        string? workRepoRoot = null;

        try
        {
            Directory.CreateDirectory(fullArtifactRoot);
            ResetNativeFullReleaseProjectArtifacts(fullArtifactRoot);

            if (!string.Equals(configuration, "Debug", StringComparison.Ordinal)
                && !string.Equals(configuration, "Release", StringComparison.Ordinal))
            {
                return WriteNativeFullReleaseProjectFailure(
                    fullArtifactRoot,
                    outputPath,
                    workRepoRoot,
                    steps,
                    "configuration",
                    ["native_project_configuration_invalid"],
                    $"Invalid native project proof configuration: {configuration}. Expected Debug or Release.");
            }

            workRepoRoot = CreateNativeWorkRepoRoot(workRoot);
            Directory.CreateDirectory(workRepoRoot);

            if (!TryPrepareNativeFullReleaseProjectRepository(fullArtifactRoot, outputPath, workRepoRoot, steps, out var setupFailure))
            {
                return setupFailure;
            }

            if (!TryRunNativeFullReleaseProjectChain(fullArtifactRoot, outputPath, workRepoRoot, steps, out var chain, out var chainFailure))
            {
                return chainFailure;
            }

            var summaryJson = WriteNativeFullReleaseProjectArtifacts(fullArtifactRoot, chain);
            File.WriteAllText(outputPath, summaryJson);
            return new MatrixNativeProjectProofResult(
                ExitCode: 0,
                Status: "passed",
                ArtifactRoot: ToPublicArtifactRootMarker(),
                ProjectMatrixOutputPath: "project-matrix-output.json",
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
            return WriteNativeFullReleaseProjectFailure(
                fullArtifactRoot,
                outputPath,
                workRepoRoot,
                steps,
                "native_project_proof",
                ["native_project_proof_failed"],
                $"Native full-release project proof failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            if (!keep && !string.IsNullOrWhiteSpace(workRepoRoot))
            {
                TryDeleteDirectory(workRepoRoot);
            }
        }
    }

    private static MatrixNativeProjectProofResult WriteNativeFullReleaseProjectFailure(
        string artifactRoot,
        string outputPath,
        string? workRepoRoot,
        IReadOnlyList<MatrixNativeProofStep> steps,
        string failedStepId,
        IReadOnlyList<string> reasonCodes,
        string message)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? artifactRoot);
        var output = new
        {
            schema_version = "matrix-native-project-proof.v0",
            status = "failed",
            proof_mode = MatrixProofSummaryPublicContract.FullReleaseProofMode,
            producer = "native_full_release_project",
            artifact_root = ToPublicArtifactRootMarker(),
            work_repo = BuildNativeWorkRepoOutput(workRepoRoot, keep: false),
            failed_step = failedStepId,
            message,
            steps,
            reason_codes = reasonCodes,
        };
        File.WriteAllText(outputPath, JsonSerializer.Serialize(output, JsonOptions));
        return new MatrixNativeProjectProofResult(
            ExitCode: 1,
            Status: "failed",
            ArtifactRoot: ToPublicArtifactRootMarker(),
            ProjectMatrixOutputPath: "project-matrix-output.json",
            ReasonCodes: reasonCodes);
    }
}

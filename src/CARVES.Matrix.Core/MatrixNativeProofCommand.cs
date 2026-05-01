using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static int RunNativeMinimalProof(MatrixOptions options)
    {
        var artifactRoot = ResolveNativeArtifactRoot(options.ArtifactRoot);
        var steps = new List<MatrixNativeProofStep>();
        string? workRepoRoot = null;

        try
        {
            Directory.CreateDirectory(artifactRoot);
            ResetNativeProofArtifacts(artifactRoot);

            if (!TryPrepareNativeProofRepository(artifactRoot, options, steps, out workRepoRoot, out var setupExitCode))
            {
                return setupExitCode;
            }

            var preparedWorkRepoRoot = workRepoRoot ?? throw new InvalidOperationException("Native proof repository setup did not return a repository path.");
            if (!TryRunNativeProofProductChain(artifactRoot, options, preparedWorkRepoRoot, steps, out var productChain, out var productChainExitCode))
            {
                return productChainExitCode;
            }

            return CompleteNativeMinimalProof(artifactRoot, options, preparedWorkRepoRoot, steps, productChain);
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or ArgumentException
                                   or NotSupportedException
                                   or InvalidOperationException
                                   or JsonException
                                   or System.ComponentModel.Win32Exception)
        {
            return WriteNativeProofFailure(
                artifactRoot,
                options,
                workRepoRoot,
                steps,
                "native_proof",
                ["native_proof_failed"],
                $"Native minimal proof failed before verification: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            if (!options.Keep && !string.IsNullOrWhiteSpace(workRepoRoot))
            {
                TryDeleteDirectory(workRepoRoot);
            }
        }
    }
}

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static int RunNativeFullReleaseProof(MatrixOptions options)
    {
        var runtimeRoot = ResolveRuntimeRoot(options.RuntimeRoot);
        if (runtimeRoot is null)
        {
            Console.Error.WriteLine("Unable to locate CARVES.Runtime root. Run from the source repository or pass --runtime-root <path>.");
            return 2;
        }

        var artifactRoot = ResolveArtifactRoot(runtimeRoot, options.ArtifactRoot);
        var result = ProduceNativeFullReleaseProofArtifacts(
            artifactRoot,
            runtimeRoot,
            configuration: options.Configuration);
        if (result.ExitCode == 0)
        {
            Console.WriteLine(result.ProofSummaryJson);
            return 0;
        }

        Console.Error.WriteLine("Native full-release proof failed before publishing a complete bundle.");
        if (!string.IsNullOrWhiteSpace(result.FailureEvidencePath))
        {
            Console.Error.WriteLine($"Failure evidence: {result.FailureEvidencePath}");
        }

        return result.ExitCode;
    }
}

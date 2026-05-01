using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static object BuildNativeWorkRepoOutput(string? workRepoRoot, bool keep)
    {
        return new
        {
            retained = keep,
            path = keep ? workRepoRoot : null,
        };
    }

    private static int WriteNativeProofFailure(
        string artifactRoot,
        MatrixOptions options,
        string? workRepoRoot,
        IReadOnlyList<MatrixNativeProofStep> steps,
        string failedStepId,
        IEnumerable<string> reasonCodes,
        string message)
    {
        var result = new
        {
            schema_version = "matrix-native-proof.v0",
            status = "failed",
            proof_mode = "native_minimal",
            proof_capabilities = BuildNativeMinimalProofCapabilities(),
            configuration = options.Configuration,
            artifact_root = ToPublicArtifactRootMarker(),
            work_repo = BuildNativeWorkRepoOutput(workRepoRoot, options.Keep),
            failed_step_id = failedStepId,
            message,
            steps,
            exit_code = 1,
            reason_codes = DistinctNativeReasonCodes(reasonCodes),
        };
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        return 1;
    }

    private static IReadOnlyList<string> DistinctNativeReasonCodes(IEnumerable<string> codes)
    {
        return codes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
    }
}

using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static string? WriteNativeProofArtifacts(
        string artifactRoot,
        string workRepoRoot,
        string shieldEvaluateJson,
        string shieldBadgeJson,
        out JsonElement matrixSummaryRoot)
    {
        matrixSummaryRoot = default;
        try
        {
            var projectRoot = Path.Combine(artifactRoot, "project");
            Directory.CreateDirectory(projectRoot);

            CopyNativeArtifact(workRepoRoot, ".ai/runtime/guard/decisions.jsonl", artifactRoot, "project/decisions.jsonl");
            CopyNativeArtifact(workRepoRoot, ".ai/handoff/handoff.json", artifactRoot, "project/handoff.json");
            CopyNativeArtifact(workRepoRoot, ".carves/shield-evidence.json", artifactRoot, "project/shield-evidence.json");
            CopyNativeArtifact(workRepoRoot, "docs/shield-badge.svg", artifactRoot, "project/shield-badge.svg");

            var shieldEvaluatePayload = NormalizeJsonStdout(shieldEvaluateJson, "Shield evaluate");
            var shieldBadgePayload = NormalizeJsonStdout(shieldBadgeJson, "Shield badge");
            File.WriteAllText(Path.Combine(projectRoot, "shield-evaluate.json"), shieldEvaluatePayload);
            File.WriteAllText(Path.Combine(projectRoot, "shield-badge.json"), shieldBadgePayload);

            var matrixSummary = BuildNativeMatrixSummary(artifactRoot, shieldEvaluatePayload);
            var matrixSummaryJson = JsonSerializer.Serialize(matrixSummary, JsonOptions);
            File.WriteAllText(Path.Combine(projectRoot, "matrix-summary.json"), matrixSummaryJson);
            using var document = JsonDocument.Parse(matrixSummaryJson);
            matrixSummaryRoot = document.RootElement.Clone();
            return null;
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or ArgumentException
                                   or InvalidOperationException
                                   or NotSupportedException
                                   or JsonException)
        {
            return $"Failed to materialize native Matrix artifacts: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private static void CopyNativeArtifact(
        string sourceRoot,
        string sourceRelativePath,
        string destinationRoot,
        string destinationRelativePath)
    {
        var sourcePath = ResolveNativeRelativePath(sourceRoot, sourceRelativePath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Native proof artifact was not found: {sourceRelativePath}", sourcePath);
        }

        if (MatrixArtifactManifestWriter.IsReparsePointOrSymbolicLink(sourcePath))
        {
            throw new InvalidOperationException($"Native proof artifact must be a regular file, not a symbolic link or reparse point: {sourceRelativePath}");
        }

        var destinationPath = ResolveNativeRelativePath(destinationRoot, destinationRelativePath);
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static string NormalizeJsonStdout(string stdout, string stepName)
    {
        var text = stdout.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new JsonException($"{stepName} emitted empty JSON output.");
        }

        using var document = JsonDocument.Parse(text);
        return text + Environment.NewLine;
    }

    private static object BuildNativeProofArtifactIndex()
    {
        return new
        {
            guard_decision = "project/decisions.jsonl",
            handoff_packet = "project/handoff.json",
            audit_evidence = "project/shield-evidence.json",
            shield_evaluation = "project/shield-evaluate.json",
            shield_badge_json = "project/shield-badge.json",
            shield_badge_svg = "project/shield-badge.svg",
            matrix_summary = "project/matrix-summary.json",
            matrix_artifact_manifest = MatrixArtifactManifestWriter.DefaultManifestFileName,
            matrix_proof_summary = "matrix-proof-summary.json",
        };
    }
}

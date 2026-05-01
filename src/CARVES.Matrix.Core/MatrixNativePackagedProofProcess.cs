using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static JsonElement? RunNativeInstalledMatrixChain(
        string runtimeRoot,
        string workRoot,
        string packagedRoot,
        MatrixNativeInstalledCommands commands,
        List<MatrixNativeProofStep> steps,
        out MatrixNativePackagedFailure failure)
    {
        failure = null!;
        if (!TryRunNativePackagedProcessStep(
                packagedRoot,
                runtimeRoot,
                steps,
                "matrix_command_help",
                "carves-matrix --help",
                commands.CarvesMatrix,
                ["--help"],
                "Installed Matrix command failed before native packaged proof orchestration.",
                out failure))
        {
            return null;
        }

        var workRepoRoot = Path.Combine(workRoot, "matrix-target");
        Directory.CreateDirectory(workRepoRoot);
        if (!TryPrepareNativePackagedRepository(packagedRoot, workRepoRoot, commands, steps, out failure)
            || !TryRunNativePackagedProductChain(packagedRoot, workRepoRoot, commands, steps, out var chain, out failure))
        {
            return null;
        }

        var summary = BuildNativeFullReleaseProjectSummary(
            chain.GuardRunId,
            ParseNativeFullReleaseProjectJson(chain.GuardCheckJson, "guard check"),
            ParseNativeFullReleaseProjectJson(chain.HandoffInspectJson, "handoff inspect"),
            ParseNativeFullReleaseProjectJson(chain.AuditSummaryJson, "audit summary"),
            ParseNativeFullReleaseProjectJson(chain.AuditEvidenceJson, "audit evidence"),
            ParseNativeFullReleaseProjectJson(chain.ShieldEvaluateJson, "shield evaluate"),
            ParseNativeFullReleaseProjectJson(chain.ShieldBadgeJson, "shield badge"),
            toolMode: "installed");
        var summaryJson = JsonSerializer.Serialize(summary, JsonOptions);
        File.WriteAllText(ResolveNativeRelativePath(packagedRoot, "matrix-e2e-output.json"), summaryJson);
        using var document = JsonDocument.Parse(summaryJson);
        return document.RootElement.Clone();
    }

    private static bool TryRunNativePackagedProcessStep(
        string packagedRoot,
        string workingDirectory,
        List<MatrixNativeProofStep> steps,
        string stepId,
        string command,
        string fileName,
        IReadOnlyList<string> arguments,
        string failureMessage,
        out MatrixNativePackagedFailure failure)
    {
        failure = null!;
        var capture = RunNativeProcessStep(
            stepId,
            command,
            workingDirectory,
            fileName,
            arguments);
        if (AppendNativeStep(steps, capture, out var failedStep))
        {
            return true;
        }

        failure = new MatrixNativePackagedFailure(
            failedStep.StepId,
            failedStep.ReasonCodes,
            failureMessage);
        return false;
    }

    private static bool TryRunNativePackagedJsonStep(
        string packagedRoot,
        string workingDirectory,
        List<MatrixNativeProofStep> steps,
        string stepId,
        string command,
        string fileName,
        IReadOnlyList<string> arguments,
        string outputRelativePath,
        string failureMessage,
        out string json,
        out MatrixNativePackagedFailure failure)
    {
        json = string.Empty;
        failure = null!;
        var capture = RunNativeProcessStep(
            stepId,
            command,
            workingDirectory,
            fileName,
            arguments);
        if (!AppendNativeStep(steps, capture, out var failedStep))
        {
            failure = new MatrixNativePackagedFailure(
                failedStep.StepId,
                failedStep.ReasonCodes,
                failureMessage);
            return false;
        }

        try
        {
            json = WriteNativePackagedStepJson(packagedRoot, outputRelativePath, capture, stepId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            failure = new MatrixNativePackagedFailure(
                stepId,
                [$"native_packaged_{stepId}_invalid_json", "native_packaged_proof_step_failed"],
                $"{failureMessage} {stepId} emitted invalid JSON.");
            return false;
        }
        return true;
    }

    private static string WriteNativePackagedStepJson(
        string packagedRoot,
        string outputRelativePath,
        MatrixNativeProofStepCapture capture,
        string stepName)
    {
        var payload = NormalizeJsonStdout(capture.Stdout, stepName);
        File.WriteAllText(ResolveNativeRelativePath(packagedRoot, outputRelativePath), payload);
        return payload;
    }

    private static MatrixNativePackagedProofResult WriteNativeFullReleasePackagedFailure(
        string artifactRoot,
        string outputPath,
        IReadOnlyList<MatrixNativeProofStep> steps,
        string failedStepId,
        IEnumerable<string> reasonCodes,
        string message)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? artifactRoot);
        var distinctReasonCodes = DistinctNativeReasonCodes(reasonCodes);
        var output = new
        {
            schema_version = "matrix-native-packaged-proof.v0",
            status = "failed",
            proof_mode = MatrixProofSummaryPublicContract.FullReleaseProofMode,
            producer = "native_full_release_packaged",
            artifact_root = ToPublicArtifactRootMarker(),
            failed_step = failedStepId,
            message,
            steps = steps.Select(RedactPackagedFailureStep).ToArray(),
            reason_codes = distinctReasonCodes,
        };
        File.WriteAllText(outputPath, JsonSerializer.Serialize(output, JsonOptions));
        return new MatrixNativePackagedProofResult(
            ExitCode: 1,
            Status: "failed",
            ArtifactRoot: ToPublicArtifactRootMarker(),
            PackagedMatrixOutputPath: "packaged-matrix-output.json",
            ReasonCodes: distinctReasonCodes);
    }

    private static MatrixNativeProofStep RedactPackagedFailureStep(MatrixNativeProofStep step)
    {
        return step with
        {
            StdoutPreview = null,
            StderrPreview = null,
        };
    }
}

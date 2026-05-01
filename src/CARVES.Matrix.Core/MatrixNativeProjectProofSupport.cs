using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static bool TryRunProjectCliJson(
        string artifactRoot,
        string outputPath,
        string workRepoRoot,
        List<MatrixNativeProofStep> steps,
        string stepId,
        string command,
        string outputRelativePath,
        Func<int> action,
        string failureMessage,
        out string json,
        out MatrixNativeProjectProofResult failure,
        IReadOnlyCollection<int>? acceptedExitCodes = null)
    {
        json = string.Empty;
        failure = null!;
        var capture = RunNativeCliStep(stepId, command, action, acceptedExitCodes);
        try
        {
            json = WriteNativeFullReleaseProjectStepJson(artifactRoot, outputRelativePath, capture, stepId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            AppendNativeStep(steps, capture, out _);
            failure = WriteNativeFullReleaseProjectFailure(
                artifactRoot,
                outputPath,
                workRepoRoot,
                steps,
                stepId,
                [$"native_project_{stepId}_invalid_json", "native_project_proof_step_failed"],
                $"{failureMessage} {stepId} emitted invalid JSON: {ex.Message}");
            return false;
        }

        if (AppendNativeStep(steps, capture, out var failedStep))
        {
            return true;
        }

        failure = FailedProjectStep(artifactRoot, outputPath, workRepoRoot, steps, failedStep, failureMessage);
        return false;
    }

    private static string WriteNativeFullReleaseProjectStepJson(
        string artifactRoot,
        string outputRelativePath,
        MatrixNativeProofStepCapture capture,
        string stepName)
    {
        var payload = NormalizeJsonStdout(capture.Stdout, stepName);
        var path = ResolveNativeRelativePath(artifactRoot, outputRelativePath);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, payload);
        return payload;
    }

    private static JsonElement ParseNativeFullReleaseProjectJson(string json, string stepName)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new JsonException($"{stepName} emitted invalid JSON.", ex);
        }
    }

    private static MatrixNativeProjectProofResult FailedProjectStep(
        string artifactRoot,
        string outputPath,
        string workRepoRoot,
        IReadOnlyList<MatrixNativeProofStep> steps,
        MatrixNativeProofStep failedStep,
        string failureMessage)
    {
        return WriteNativeFullReleaseProjectFailure(
            artifactRoot,
            outputPath,
            workRepoRoot,
            steps,
            failedStep.StepId,
            failedStep.ReasonCodes,
            failureMessage);
    }

    private static object BuildNativeFullReleaseTrustChainEvidence()
    {
        return new
        {
            audit_evidence_integrity = "complete_card_796",
            guard_deletion_replacement_honesty = "complete_card_797",
            shield_evidence_contract_alignment = "complete_card_798",
            guard_audit_store_multiprocess_durability = "complete_card_799",
            handoff_completed_state_semantics = "complete_card_800",
            matrix_shield_proof_bridge_claim_boundary = "complete_card_801",
            large_log_streaming_output_boundaries = "complete_card_802",
            handoff_reference_freshness_portability = "complete_card_803",
            usability_coverage_cleanup = "complete_card_804",
            release_checkpoint = "complete_card_805",
            public_rating_claim = "local_self_check_only",
            public_rating_claims_allowed = "limited_to_local_self_check",
        };
    }
}

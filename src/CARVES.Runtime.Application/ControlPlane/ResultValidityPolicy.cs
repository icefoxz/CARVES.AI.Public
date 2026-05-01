using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.ControlPlane;

public sealed class ResultValidityPolicy
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ControlPlanePaths? paths;
    private readonly AuthoritativeTruthStoreService? authoritativeTruthStoreService;

    public ResultValidityPolicy(ControlPlanePaths? paths = null)
    {
        this.paths = paths;
        authoritativeTruthStoreService = paths is null ? null : new AuthoritativeTruthStoreService(paths);
    }

    public ResultValidityDecision Evaluate(
        string taskId,
        ResultEnvelope envelope,
        WorkerExecutionArtifact? workerArtifact,
        string? executionRunId = null)
    {
        if (workerArtifact is null)
        {
            return new ResultValidityDecision(false, "no_evidence", $"Result envelope for '{taskId}' cannot be written back without worker execution evidence.");
        }

        if (!string.IsNullOrWhiteSpace(envelope.ExecutionRunId)
            && !string.Equals(workerArtifact.Result.RunId, envelope.ExecutionRunId, StringComparison.Ordinal)
            && !string.Equals(executionRunId, envelope.ExecutionRunId, StringComparison.Ordinal))
        {
            return new ResultValidityDecision(false, "evidence_run_mismatch", $"Result envelope run '{envelope.ExecutionRunId}' does not match execution run '{executionRunId ?? "(none)"}' or evidence run '{workerArtifact.Result.RunId}'.");
        }

        var evidence = workerArtifact.Evidence ?? ExecutionEvidence.None;
        if (evidence.EvidenceCompleteness == ExecutionEvidenceCompleteness.Missing)
        {
            return new ResultValidityDecision(false, "no_evidence", $"Result envelope for '{taskId}' is missing execution evidence.");
        }

        var effectiveStrength = ResolveEffectiveStrength(evidence);
        if (effectiveStrength < ExecutionEvidenceStrength.Verifiable)
        {
            return new ResultValidityDecision(false, "evidence_not_admissible", $"Result envelope for '{taskId}' only has {effectiveStrength.ToString().ToLowerInvariant()} evidence and cannot be written back.");
        }

        if (!string.IsNullOrWhiteSpace(envelope.ExecutionEvidencePath)
            && !string.Equals(NormalizePath(envelope.ExecutionEvidencePath), NormalizePath(evidence.EvidencePath), StringComparison.Ordinal))
        {
            return new ResultValidityDecision(false, "evidence_reference_mismatch", $"Result envelope evidence path '{envelope.ExecutionEvidencePath}' does not match recorded evidence '{evidence.EvidencePath}'.");
        }

        if (evidence.EndedAt < evidence.StartedAt)
        {
            return new ResultValidityDecision(false, "evidence_timestamp_invalid", $"Execution evidence for '{taskId}' ended before it started.");
        }

        if (evidence.CommandsExecuted.Count > 0 && evidence.ExitStatus is null)
        {
            return new ResultValidityDecision(false, "missing_exit_status", $"Execution evidence for '{taskId}' is missing an exit status for executed commands.");
        }

        if (!ArtifactExists(evidence.EvidencePath))
        {
            return new ResultValidityDecision(false, "evidence_artifact_missing", $"Execution evidence artifact for '{taskId}' was not found at '{evidence.EvidencePath}'.");
        }

        if (!ArtifactExists(evidence.CommandLogRef))
        {
            return new ResultValidityDecision(false, "command_log_missing", $"Execution evidence for '{taskId}' is missing command.log.");
        }

        if (RequiresBuildArtifact(envelope) && !ArtifactExists(evidence.BuildOutputRef))
        {
            return new ResultValidityDecision(false, "build_log_missing", $"Execution evidence for '{taskId}' is missing build output needed for writeback admission.");
        }

        if (RequiresTestArtifact(envelope) && !ArtifactExists(evidence.TestOutputRef))
        {
            return new ResultValidityDecision(false, "test_log_missing", $"Execution evidence for '{taskId}' is missing test output needed for writeback admission.");
        }

        if (evidence.FilesWritten.Count > 0 && !ArtifactExists(evidence.PatchRef))
        {
            return new ResultValidityDecision(false, "patch_artifact_missing", $"Execution evidence for '{taskId}' is missing the reviewable patch artifact.");
        }

        var hostValidationDecision = ValidateHostResultClosure(taskId, envelope, workerArtifact, evidence);
        if (hostValidationDecision is not null)
        {
            return hostValidationDecision;
        }

        return new ResultValidityDecision(true, "valid", "Execution evidence is admissible for result writeback.", evidence);
    }

    private ResultValidityDecision? ValidateHostResultClosure(
        string taskId,
        ResultEnvelope envelope,
        WorkerExecutionArtifact workerArtifact,
        ExecutionEvidence evidence)
    {
        var packet = TryLoadExecutionPacket(taskId);
        if (packet is null || string.IsNullOrWhiteSpace(packet.WorkerExecutionPacket.PacketId))
        {
            return null;
        }

        var workerPacket = packet.WorkerExecutionPacket;
        var blockers = new List<string>();
        if (packet.PlannerIntent == PlannerIntent.Execution
            && !workerPacket.AllowedActions.Any(action => ActionMatches(action, "carves.submit_result")))
        {
            blockers.Add("worker_packet_missing_submit_result");
        }

        if (workerPacket.GrantsLifecycleTruthAuthority)
        {
            blockers.Add("worker_packet_grants_lifecycle_truth_authority");
        }

        if (workerPacket.GrantsTruthWriteAuthority || workerPacket.WritesTruthRoots)
        {
            blockers.Add("worker_packet_grants_truth_write_authority");
        }

        if (workerPacket.CreatesTaskQueue)
        {
            blockers.Add("worker_packet_creates_task_queue");
        }

        if (!workerPacket.ResultSubmission.CandidateOnly)
        {
            blockers.Add("worker_result_not_candidate_only");
        }

        if (workerPacket.ResultSubmission.WorkerDirectTruthWriteAllowed)
        {
            blockers.Add("worker_direct_truth_write_allowed");
        }

        if (!string.IsNullOrWhiteSpace(workerPacket.ResultSubmission.CandidateResultChannel)
            && !string.Equals(
                NormalizePath(workerPacket.ResultSubmission.CandidateResultChannel),
                $".ai/execution/{taskId}/result.json",
                StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add("worker_result_channel_mismatch");
        }

        var claim = workerArtifact.Result.CompletionClaim;
        if (workerPacket.CompletionClaimSchema.Required)
        {
            if (!claim.Required || string.Equals(claim.Status, "not_required", StringComparison.OrdinalIgnoreCase))
            {
                blockers.Add("completion_claim_missing");
            }
            else if (!string.Equals(claim.Status, "present", StringComparison.OrdinalIgnoreCase))
            {
                blockers.Add($"completion_claim_not_present:{claim.Status}");
            }

            if (string.IsNullOrWhiteSpace(claim.PacketId)
                || !string.Equals(claim.PacketId, workerPacket.PacketId, StringComparison.Ordinal))
            {
                blockers.Add("completion_claim_packet_id_mismatch");
            }

            if (string.IsNullOrWhiteSpace(claim.SourceExecutionPacketId)
                || !string.Equals(claim.SourceExecutionPacketId, packet.PacketId, StringComparison.Ordinal))
            {
                blockers.Add("completion_claim_source_packet_mismatch");
            }

            if (claim.ClaimIsTruth)
            {
                blockers.Add("completion_claim_claims_truth_authority");
            }

            if (!claim.HostValidationRequired)
            {
                blockers.Add("completion_claim_host_validation_not_required");
            }

            if (!string.Equals(claim.PacketValidationStatus, "passed", StringComparison.OrdinalIgnoreCase))
            {
                blockers.Add($"completion_claim_packet_validation_not_passed:{claim.PacketValidationStatus}");
            }

            blockers.AddRange(claim.PacketValidationBlockers.Select(blocker => $"completion_claim_packet:{blocker}"));
            blockers.AddRange(claim.MissingFields.Select(field => $"completion_claim_missing_field:{field}"));
            blockers.AddRange(claim.MissingContractItems.Select(item => $"completion_claim_missing_contract_item:{item}"));
            blockers.AddRange(claim.DisallowedChangedFiles.Select(path => $"completion_claim_disallowed_changed_file:{path}"));
            blockers.AddRange(claim.ForbiddenVocabularyHits.Select(term => $"completion_claim_forbidden_vocabulary:{term}"));
        }

        var effectiveFiles = BuildEffectiveChangedFiles(envelope, workerArtifact, evidence);
        var disallowedFiles = effectiveFiles
            .Where(path => !IsUnderRoots(path, workerPacket.AllowedFiles))
            .ToArray();
        blockers.AddRange(disallowedFiles.Select(path => $"host_validator_disallowed_changed_file:{path}"));

        var claimMissingFiles = effectiveFiles
            .Where(path => !claim.ChangedFiles.Any(claimPath => PathsMatch(path, claimPath)))
            .ToArray();
        if (workerPacket.CompletionClaimSchema.Required)
        {
            blockers.AddRange(claimMissingFiles.Select(path => $"completion_claim_missing_changed_file:{path}"));
        }

        if (blockers.Count == 0)
        {
            return null;
        }

        var distinctBlockers = blockers
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new ResultValidityDecision(
            false,
            ResolveHostValidationReasonCode(distinctBlockers),
            $"Host result validation failed for '{taskId}': {string.Join(", ", distinctBlockers)}.",
            evidence);
    }

    private ExecutionPacket? TryLoadExecutionPacket(string taskId)
    {
        if (paths is null || authoritativeTruthStoreService is null)
        {
            return null;
        }

        var mirrorPath = Path.Combine(paths.RuntimeRoot, "execution-packets", $"{taskId}.json");
        var authoritativePath = authoritativeTruthStoreService.GetExecutionPacketPath(taskId);
        var payload = authoritativeTruthStoreService.ReadAuthoritativeFirst(authoritativePath, mirrorPath);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ExecutionPacket>(payload, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> BuildEffectiveChangedFiles(
        ResultEnvelope envelope,
        WorkerExecutionArtifact workerArtifact,
        ExecutionEvidence evidence)
    {
        return envelope.Changes.FilesModified
            .Concat(envelope.Changes.FilesAdded)
            .Concat(workerArtifact.Result.ChangedFiles)
            .Concat(workerArtifact.Result.ObservedChangedFiles)
            .Concat(evidence.FilesWritten)
            .Select(NormalizePath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveHostValidationReasonCode(IReadOnlyList<string> blockers)
    {
        var first = blockers.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(first))
        {
            return "host_validator_failed";
        }

        var separator = first.IndexOf(':', StringComparison.Ordinal);
        return separator > 0 ? first[..separator] : first;
    }

    private static string? NormalizePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? null
            : path.Trim().Trim('`').Replace('\\', '/');
    }

    private bool ArtifactExists(string? relativeOrAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
        {
            return false;
        }

        var path = relativeOrAbsolutePath;
        if (!Path.IsPathRooted(path))
        {
            if (paths is null)
            {
                return true;
            }

            path = Path.GetFullPath(Path.Combine(paths.RepoRoot, relativeOrAbsolutePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        return File.Exists(path);
    }

    private static bool RequiresBuildArtifact(ResultEnvelope envelope)
    {
        return !string.Equals(envelope.Validation.Build, "not_run", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresTestArtifact(ResultEnvelope envelope)
    {
        return !string.Equals(envelope.Validation.Tests, "not_run", StringComparison.OrdinalIgnoreCase);
    }

    private static ExecutionEvidenceStrength ResolveEffectiveStrength(ExecutionEvidence evidence)
    {
        if (evidence.EvidenceStrength != ExecutionEvidenceStrength.Missing || evidence.EvidenceCompleteness == ExecutionEvidenceCompleteness.Missing)
        {
            return evidence.EvidenceStrength;
        }

        return evidence.EvidenceCompleteness switch
        {
            ExecutionEvidenceCompleteness.Complete => ExecutionEvidenceStrength.Verifiable,
            ExecutionEvidenceCompleteness.Partial => ExecutionEvidenceStrength.Observed,
            _ => ExecutionEvidenceStrength.Missing,
        };
    }

    private static bool IsUnderRoots(string path, IReadOnlyList<string> roots)
    {
        if (roots.Count == 0)
        {
            return true;
        }

        var normalizedPath = NormalizePath(path);
        return roots
            .Select(NormalizePath)
            .Where(static root => !string.IsNullOrWhiteSpace(root))
            .Any(root =>
                string.Equals(normalizedPath, root, StringComparison.OrdinalIgnoreCase)
                || (root!.EndsWith("/", StringComparison.Ordinal)
                    && normalizedPath!.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                || normalizedPath!.StartsWith($"{root}/", StringComparison.OrdinalIgnoreCase));
    }

    private static bool PathsMatch(string first, string second)
    {
        return string.Equals(NormalizePath(first), NormalizePath(second), StringComparison.OrdinalIgnoreCase);
    }

    private static bool ActionMatches(string? value, string candidate)
    {
        return !string.IsNullOrWhiteSpace(value)
               && !string.IsNullOrWhiteSpace(candidate)
               && NormalizeAction(value).Contains(NormalizeAction(candidate), StringComparison.Ordinal);
    }

    private static string NormalizeAction(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}

public sealed record ResultValidityDecision(
    bool Valid,
    string ReasonCode,
    string Message,
    ExecutionEvidence? Evidence = null);

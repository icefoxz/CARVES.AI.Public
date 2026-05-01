using System.Text.Json;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Platform.SurfaceModels;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed class ModeEReviewPreflightService
{
    private static readonly IReadOnlyList<string> HostOnlyRoots =
    [
        ".ai/tasks",
        ".ai/memory",
        ".ai/artifacts/reviews",
        ".carves-platform",
    ];

    private static readonly IReadOnlyList<string> DenyRoots =
    [
        ".git",
        ".vs",
        ".idea",
    ];

    private readonly TaskGraphService taskGraphService;
    private readonly IRuntimeArtifactRepository artifactRepository;
    private readonly PacketEnforcementService packetEnforcementService;
    private readonly ReviewEvidenceGateService reviewEvidenceGateService;
    private readonly MemoryPatternWritebackRouteAuthorizationService? memoryPatternWritebackRouteAuthorizationService;
    private readonly ControlPlanePaths paths;

    public ModeEReviewPreflightService(
        ControlPlanePaths paths,
        TaskGraphService taskGraphService,
        IRuntimeArtifactRepository artifactRepository,
        PacketEnforcementService packetEnforcementService,
        ReviewEvidenceGateService reviewEvidenceGateService,
        MemoryPatternWritebackRouteAuthorizationService? memoryPatternWritebackRouteAuthorizationService = null)
    {
        this.paths = paths;
        this.taskGraphService = taskGraphService;
        this.artifactRepository = artifactRepository;
        this.packetEnforcementService = packetEnforcementService;
        this.reviewEvidenceGateService = reviewEvidenceGateService;
        this.memoryPatternWritebackRouteAuthorizationService = memoryPatternWritebackRouteAuthorizationService;
    }

    public RuntimeBrokeredReviewPreflightSurface Build(string taskId, PacketEnforcementSurfaceSnapshot? packetEnforcement = null)
    {
        var task = taskGraphService.GetTask(taskId);
        var snapshot = packetEnforcement ?? BuildPacketEnforcementSnapshot(taskId);
        var record = snapshot.Record;
        if (!Applies(record))
        {
            return new RuntimeBrokeredReviewPreflightSurface
            {
                PacketEnforcementVerdict = record.Verdict,
            };
        }

        var reviewArtifact = artifactRepository.TryLoadPlannerReviewArtifact(taskId);
        var workerArtifact = artifactRepository.TryLoadWorkerExecutionArtifact(taskId);
        var patternWritebackAuthorization = memoryPatternWritebackRouteAuthorizationService?.Evaluate(
                record.ChangedFiles.Concat(record.TruthWriteFiles),
                workerArtifact)
            ?? MemoryPatternWritebackRouteAuthorizationAssessment.NotApplicable;
        var blockers = new List<RuntimeBrokeredReviewPreflightBlockerSurface>();
        var packetScopeStatus = EvaluatePacketScope(record, patternWritebackAuthorization, blockers);
        var acceptanceEvidenceStatus = EvaluateAcceptanceEvidence(task, reviewArtifact, workerArtifact, blockers, out var missingAcceptanceEvidence);
        var pathPolicyStatus = EvaluateProtectedPaths(record, patternWritebackAuthorization, blockers, out var hostOnlyPaths, out var deniedPaths, out var protectedPathDetails);
        var mutationAuditStatus = EvaluateMutationAudit(record, patternWritebackAuthorization, hostOnlyPaths, deniedPaths, blockers, out var mutationAuditViolationPaths, out var mutationAuditFirstViolationClass, out var mutationAuditRecommendedAction);
        var canProceedToReviewApproval = blockers.Count == 0;
        var canProceedToProvisionalApproval = blockers.All(static blocker => string.Equals(blocker.Category, "acceptance_evidence", StringComparison.Ordinal))
            && task.AcceptanceContract?.HumanReview.ProvisionalAllowed == true;

        return new RuntimeBrokeredReviewPreflightSurface
        {
            Status = canProceedToReviewApproval ? "ready_for_review_approval" : "blocked",
            Applies = true,
            CanProceedToReviewApproval = canProceedToReviewApproval,
            CanProceedToProvisionalApproval = canProceedToReviewApproval || canProceedToProvisionalApproval,
            Summary = BuildSummary(canProceedToReviewApproval, blockers, record),
            PacketScopeStatus = packetScopeStatus,
            PacketEnforcementVerdict = record.Verdict,
            PacketScopeMismatchFiles = record.OffPacketFiles,
            PacketScopeReasonCodes = record.ReasonCodes,
            AcceptanceEvidenceStatus = acceptanceEvidenceStatus,
            MissingAcceptanceEvidence = missingAcceptanceEvidence,
            PathPolicyStatus = pathPolicyStatus,
            ProtectedPathViolations = hostOnlyPaths.Concat(deniedPaths).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            ProtectedPathViolationDetails = protectedPathDetails,
            HostOnlyPathViolations = hostOnlyPaths,
            DeniedPathViolations = deniedPaths,
            MutationAuditStatus = mutationAuditStatus,
            MutationAuditChangedPathCount = record.ChangedFiles.Count,
            MutationAuditViolationPaths = mutationAuditViolationPaths,
            MutationAuditFirstViolationPath = mutationAuditViolationPaths.FirstOrDefault(),
            MutationAuditFirstViolationClass = mutationAuditFirstViolationClass,
            MutationAuditRecommendedAction = mutationAuditRecommendedAction,
            Blockers = blockers,
        };
    }

    private PacketEnforcementSurfaceSnapshot BuildPacketEnforcementSnapshot(string taskId)
    {
        try
        {
            return packetEnforcementService.BuildSnapshot(taskId);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            if (!File.Exists(Path.Combine(paths.RuntimeRoot, "execution-packets", $"{taskId}.json")))
            {
                return new PacketEnforcementSurfaceSnapshot
                {
                    TaskId = taskId,
                    Summary = "Mode E review preflight is not applicable because no persisted execution packet exists for this task.",
                    Record = new PacketEnforcementRecord
                    {
                        TaskId = taskId,
                        Verdict = "not_applicable",
                        ReasonCodes = ["packet_missing"],
                        Summary = "Mode E review preflight is not applicable because no persisted execution packet exists for this task.",
                    },
                };
            }

            return new PacketEnforcementSurfaceSnapshot
            {
                TaskId = taskId,
                Summary = $"Packet enforcement could not evaluate Mode E review preflight: {exception.Message}",
                Record = new PacketEnforcementRecord
                {
                    TaskId = taskId,
                    PacketPresent = true,
                    PacketPersisted = true,
                    Verdict = "result_return_payload_invalid",
                    ReasonCodes = ["packet_enforcement_unavailable", "result_return_payload_invalid"],
                    Summary = $"Packet enforcement could not evaluate Mode E review preflight: {exception.Message}",
                },
            };
        }
    }

    private static bool Applies(PacketEnforcementRecord record)
    {
        return record.PacketPresent
            || record.PacketPersisted
            || !string.IsNullOrWhiteSpace(record.PacketId);
    }

    private static string EvaluatePacketScope(
        PacketEnforcementRecord record,
        MemoryPatternWritebackRouteAuthorizationAssessment patternWritebackAuthorization,
        List<RuntimeBrokeredReviewPreflightBlockerSurface> blockers)
    {
        var unauthorizedOffPacketFiles = record.OffPacketFiles
            .Where(path => !patternWritebackAuthorization.IsAuthorized(path))
            .ToArray();
        var unauthorizedTruthWriteFiles = record.TruthWriteFiles
            .Where(path => !patternWritebackAuthorization.IsAuthorized(path))
            .ToArray();
        var governedPatternTruthWrite =
            patternWritebackAuthorization.Applies
            && patternWritebackAuthorization.AllTouchedPathsAuthorized
            && unauthorizedOffPacketFiles.Length == 0
            && unauthorizedTruthWriteFiles.Length == 0
            && patternWritebackAuthorization.AuthorizedPaths.Count > 0
            && record.ReasonCodes.All(static code =>
                string.Equals(code, "truth_write_attempt_detected", StringComparison.Ordinal)
                || string.Equals(code, "off_packet_edit_detected", StringComparison.Ordinal));

        if (record.Verdict is "reject" or "quarantine" or "result_return_payload_invalid"
            && !governedPatternTruthWrite)
        {
            blockers.Add(new RuntimeBrokeredReviewPreflightBlockerSurface
            {
                BlockerId = "packet_enforcement_blocked",
                Category = "packet_scope",
                Summary = $"Packet enforcement verdict is {record.Verdict}; reason codes: {FormatList(record.ReasonCodes)}.",
                RequiredAction = "Correct the returned result so it stays inside the issued execution packet before review approval.",
            });
        }

        if (unauthorizedOffPacketFiles.Length > 0)
        {
            blockers.Add(new RuntimeBrokeredReviewPreflightBlockerSurface
            {
                BlockerId = "packet_scope_mismatch",
                Category = "packet_scope",
                Summary = $"Returned files outside packet scope: {FormatList(unauthorizedOffPacketFiles)}.",
                RequiredAction = "Replan or narrow the returned patch to the packet editable roots before writeback.",
            });
        }

        if (record.Verdict == "pending_execution")
        {
            blockers.Add(new RuntimeBrokeredReviewPreflightBlockerSurface
            {
                BlockerId = "brokered_result_missing",
                Category = "packet_scope",
                Summary = "Packet enforcement is pending because no brokered result envelope or worker artifact has returned.",
                RequiredAction = "Return Mode E result material before attempting review approval.",
            });
        }

        if (governedPatternTruthWrite)
        {
            return "governed_truth_route";
        }

        return blockers.Any(static blocker => string.Equals(blocker.Category, "packet_scope", StringComparison.Ordinal))
            ? "mismatch"
            : "clear";
    }

    private string EvaluateAcceptanceEvidence(
        TaskNode task,
        PlannerReviewArtifact? reviewArtifact,
        WorkerExecutionArtifact? workerArtifact,
        List<RuntimeBrokeredReviewPreflightBlockerSurface> blockers,
        out IReadOnlyList<string> missingAcceptanceEvidence)
    {
        var requirements = task.AcceptanceContract?.EvidenceRequired ?? Array.Empty<AcceptanceContractEvidenceRequirement>();
        if (reviewArtifact is null)
        {
            if (task.Status != DomainTaskStatus.Review)
            {
                missingAcceptanceEvidence = Array.Empty<string>();
                return requirements.Count == 0 ? "no_requirements" : "awaiting_review_artifact";
            }

            missingAcceptanceEvidence = requirements.Count == 0
                ? ["worker_execution_evidence"]
                : requirements
                    .Select(FormatRequirement)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            blockers.Add(new RuntimeBrokeredReviewPreflightBlockerSurface
            {
                BlockerId = "review_artifact_missing_for_acceptance_evidence",
                Category = "acceptance_evidence",
                Summary = $"Review artifact is missing; required evidence cannot be evaluated: {FormatList(missingAcceptanceEvidence)}.",
                RequiredAction = "Record planner review evidence before approving the Mode E returned result.",
            });
            return "missing";
        }

        var assessment = reviewEvidenceGateService.EvaluateBeforeWriteback(task, reviewArtifact, workerArtifact);
        missingAcceptanceEvidence = assessment.MissingRequirements
            .Select(static gap => gap.DisplayLabel)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (assessment.IsSatisfied)
        {
            return requirements.Count == 0 ? "no_requirements" : "satisfied";
        }

        blockers.Add(new RuntimeBrokeredReviewPreflightBlockerSurface
        {
            BlockerId = "acceptance_evidence_missing",
            Category = "acceptance_evidence",
            Summary = $"Acceptance contract evidence is missing: {FormatList(missingAcceptanceEvidence)}.",
            RequiredAction = "Capture the missing acceptance evidence or use provisional acceptance only if the contract explicitly allows it.",
        });
        return "missing";
    }

    private static string EvaluateProtectedPaths(
        PacketEnforcementRecord record,
        MemoryPatternWritebackRouteAuthorizationAssessment patternWritebackAuthorization,
        List<RuntimeBrokeredReviewPreflightBlockerSurface> blockers,
        out IReadOnlyList<string> hostOnlyPaths,
        out IReadOnlyList<string> deniedPaths,
        out IReadOnlyList<RuntimeProtectedPathViolationSurface> protectedPathDetails)
    {
        var changedFiles = record.ChangedFiles
            .Concat(record.TruthWriteFiles)
            .Select(NormalizePath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        hostOnlyPaths = changedFiles
            .Where(static path => IsUnderRoots(path, HostOnlyRoots))
            .Where(path => !patternWritebackAuthorization.IsAuthorized(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        deniedPaths = changedFiles
            .Where(static path => IsUnderRoots(path, DenyRoots) || IsSecretLikePath(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        protectedPathDetails = hostOnlyPaths
            .Concat(deniedPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(RuntimeProtectedTruthRootPolicyService.ClassifyViolation)
            .ToArray();
        if (hostOnlyPaths.Count == 0 && deniedPaths.Count == 0)
        {
            return "clear";
        }

        blockers.Add(new RuntimeBrokeredReviewPreflightBlockerSurface
        {
            BlockerId = "protected_path_policy_violation",
            Category = "path_policy",
            Summary = $"Returned material touched protected paths: {FormatList(protectedPathDetails.Select(static detail => $"{detail.Path} ({detail.ProtectedClassification})").ToArray())}.",
            RequiredAction = protectedPathDetails.Count == 0
                ? "Remove protected-path writes from the returned material; official truth roots must be changed only through host-owned writeback."
                : protectedPathDetails[0].RemediationAction,
        });
        return "protected_path_violation";
    }

    private static string EvaluateMutationAudit(
        PacketEnforcementRecord record,
        MemoryPatternWritebackRouteAuthorizationAssessment patternWritebackAuthorization,
        IReadOnlyList<string> hostOnlyPaths,
        IReadOnlyList<string> deniedPaths,
        List<RuntimeBrokeredReviewPreflightBlockerSurface> blockers,
        out IReadOnlyList<string> violationPaths,
        out string? firstViolationClass,
        out string recommendedAction)
    {
        var violations = deniedPaths
            .Select(static path => (Path: path, PolicyClass: "deny"))
            .Concat(hostOnlyPaths.Select(static path => (Path: path, PolicyClass: "host_only")))
            .Concat(record.OffPacketFiles
                .Select(NormalizePath)
                .Where(path => !patternWritebackAuthorization.IsAuthorized(path))
                .Select(static path => (Path: path, PolicyClass: "scope_escape")))
            .Where(static item => !string.IsNullOrWhiteSpace(item.Path))
            .DistinctBy(static item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        violationPaths = violations.Select(static item => item.Path).ToArray();
        firstViolationClass = violations.FirstOrDefault().PolicyClass;
        recommendedAction = firstViolationClass switch
        {
            "deny" => "Remove denied-root or secret-like writes before any review or writeback.",
            "host_only" => "Route governed truth mutations through host-owned Runtime writeback.",
            "scope_escape" => "Replan or narrow returned changes before writeback.",
            _ => "none",
        };

        if (violations.Length == 0)
        {
            return record.ChangedFiles.Count == 0 ? "no_changed_paths" : "clear";
        }

        blockers.Add(new RuntimeBrokeredReviewPreflightBlockerSurface
        {
            BlockerId = "mutation_audit_policy_violation",
            Category = "mutation_audit",
            Summary = $"Mutation audit found result paths that cannot proceed to writeback: {FormatList(violationPaths)}.",
            RequiredAction = recommendedAction,
        });
        return firstViolationClass ?? "blocked";
    }

    private static string BuildSummary(
        bool canProceedToReviewApproval,
        IReadOnlyList<RuntimeBrokeredReviewPreflightBlockerSurface> blockers,
        PacketEnforcementRecord record)
    {
        if (canProceedToReviewApproval)
        {
            return $"Mode E review preflight is ready; packet enforcement verdict={record.Verdict}; approval still requires the existing review lifecycle.";
        }

        return $"Mode E review preflight blocked approval before writeback: {FormatList(blockers.Select(static blocker => blocker.BlockerId).ToArray())}.";
    }

    private static string FormatRequirement(AcceptanceContractEvidenceRequirement requirement)
    {
        return string.IsNullOrWhiteSpace(requirement.Description)
            ? requirement.Type.Trim()
            : $"{requirement.Type.Trim()} ({requirement.Description.Trim()})";
    }

    private static bool IsUnderRoots(string normalizedPath, IReadOnlyList<string> roots)
    {
        return roots.Any(root => PathMatchesRoot(root, normalizedPath));
    }

    private static bool PathMatchesRoot(string root, string path)
    {
        var normalizedRoot = NormalizePath(root).TrimEnd('/');
        return string.Equals(path, normalizedRoot, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith($"{normalizedRoot}/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSecretLikePath(string normalizedPath)
    {
        var fileName = Path.GetFileName(normalizedPath);
        return fileName.Equals(".env", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".snk", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.Contains("/secrets/", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("secrets.json", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').Trim();
    }

    private static string FormatList(IReadOnlyList<string> values)
    {
        return values.Count == 0 ? "(none)" : string.Join(", ", values);
    }
}

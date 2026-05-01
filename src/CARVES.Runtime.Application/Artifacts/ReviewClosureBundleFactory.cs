using System.Text.Json;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Artifacts;

internal static class ReviewClosureBundleFactory
{
    private const string GenericClosureProfileId = "generic_review_closure_v1";
    private const string RuntimeRecoverableResidueProfileId = "runtime_recoverable_residue_contract_v1";

    public static ReviewClosureBundle Build(
        TaskNode? task,
        IReadOnlyList<string> patchPaths,
        bool validationPassed,
        IReadOnlyList<string> validationEvidence,
        IReadOnlyList<CommandExecutionRecord> validationCommandResults,
        int validationEvidenceCount,
        SafetyOutcome safetyOutcome,
        IReadOnlyList<string> safetyIssues,
        PlannerReview review,
        DomainTaskStatus resultingStatus,
        bool writebackApplied,
        WorkerCompletionClaim? workerCompletionClaim,
        WorkerFailureKind? historicalWorkerFailureKind,
        ReviewClosureBundle? existingBundle,
        ReviewClosureHostValidationSummary? hostValidationOverride = null)
    {
        var manualFallback = IsManualFallback(task);
        var effectiveDecisionStatus = ResolveEffectiveDecisionStatus(review, resultingStatus);
        var validationProfile = BuildValidationProfile(
            task,
            patchPaths,
            validationPassed,
            validationEvidence,
            validationCommandResults,
            existingBundle);
        var contractMatrix = BuildContractMatrix(
            task,
            patchPaths,
            validationPassed,
            validationEvidence,
            safetyOutcome,
            safetyIssues,
            existingBundle);
        var candidateResultSource = ResolveCandidateResultSource(task, existingBundle, manualFallback);
        var resolvedHistoricalWorkerFailureKind = historicalWorkerFailureKind ?? ResolveHistoricalWorkerFailureKind(task);
        var workerResultVerdict = ResolveWorkerResultVerdict(
            validationPassed,
            safetyOutcome,
            effectiveDecisionStatus,
            resultingStatus,
            manualFallback,
            resolvedHistoricalWorkerFailureKind,
            existingBundle);
        var acceptedPatchSource = ResolveAcceptedPatchSource(review, manualFallback);
        var completionMode = ResolveCompletionMode(effectiveDecisionStatus, resultingStatus, manualFallback);
        var reviewerDecision = ResolveReviewerDecision(effectiveDecisionStatus);
        var validationSummary = new ReviewClosureValidationSummary
        {
            RequiredGateStatus = ResolveRequiredGateStatus(validationProfile, validationPassed),
            AdvisoryGateStatus = ResolveAdvisoryGateStatus(validationProfile),
            KnownRedBaselineStatus = ResolveKnownRedBaselineStatus(validationProfile),
            EvidenceCount = validationEvidenceCount,
            Profile = validationProfile,
        };
        var completionClaim = ResolveCompletionClaim(workerCompletionClaim, existingBundle);
        var hostValidation = hostValidationOverride ?? BuildHostValidationSummary(completionClaim);
        var fallbackRunPacket = BuildFallbackRunPacket(task, manualFallback, completionClaim, existingBundle);
        var closureDecision = BuildClosureDecision(
            candidateResultSource,
            workerResultVerdict,
            acceptedPatchSource,
            completionMode,
            reviewerDecision,
            validationSummary,
            fallbackRunPacket,
            contractMatrix,
            completionClaim,
            hostValidation,
            safetyOutcome,
            effectiveDecisionStatus,
            writebackApplied);
        return new ReviewClosureBundle
        {
            SchemaVersion = existingBundle?.SchemaVersion ?? "review-closure-bundle.v1",
            CandidateResultSource = candidateResultSource,
            WorkerResultVerdict = workerResultVerdict,
            AcceptedPatchSource = acceptedPatchSource,
            CompletionMode = completionMode,
            ReviewerDecision = reviewerDecision,
            Validation = validationSummary,
            CompletionClaim = completionClaim,
            HostValidation = hostValidation,
            FallbackRunPacket = fallbackRunPacket,
            ContractMatrix = contractMatrix,
            ClosureDecision = closureDecision,
            WritebackRecommendation = ResolveWritebackRecommendation(effectiveDecisionStatus, manualFallback, writebackApplied),
        };
    }

    private static ReviewClosureHostValidationSummary BuildHostValidationSummary(
        ReviewClosureCompletionClaimSummary completionClaim)
    {
        var packetBoundClaim = completionClaim.Required
                               || !string.IsNullOrWhiteSpace(completionClaim.PacketId)
                               || !string.IsNullOrWhiteSpace(completionClaim.SourceExecutionPacketId)
                               || !string.Equals(completionClaim.PacketValidationStatus, "not_evaluated", StringComparison.OrdinalIgnoreCase)
                               || completionClaim.PacketValidationBlockers.Count > 0;
        if (!packetBoundClaim)
        {
            return new ReviewClosureHostValidationSummary
            {
                Status = "not_required",
                Required = false,
                ReasonCode = "not_required",
                Message = "No worker packet-bound completion claim was present; Review closure keeps the existing evidence gates authoritative.",
                CompletionClaimStatus = completionClaim.Status,
                CompletionClaimPacketValidationStatus = completionClaim.PacketValidationStatus,
                CompletionClaimHostValidationRequired = completionClaim.HostValidationRequired,
                CompletionClaimClaimsTruthAuthority = completionClaim.ClaimIsTruth,
                Notes =
                [
                    "Host validation summary is review-local evidence and does not write lifecycle truth.",
                ],
            };
        }

        var blockers = new List<string>();
        if (completionClaim.Status != "present")
        {
            blockers.Add($"completion_claim_not_present:{completionClaim.Status}");
        }

        if (string.IsNullOrWhiteSpace(completionClaim.PacketId))
        {
            blockers.Add("completion_claim_packet_id_missing");
        }

        if (string.IsNullOrWhiteSpace(completionClaim.SourceExecutionPacketId))
        {
            blockers.Add("completion_claim_source_execution_packet_id_missing");
        }

        if (completionClaim.ClaimIsTruth)
        {
            blockers.Add("completion_claim_claims_truth_authority");
        }

        if (!completionClaim.HostValidationRequired)
        {
            blockers.Add("completion_claim_host_validation_not_required");
        }

        if (!string.Equals(completionClaim.PacketValidationStatus, "passed", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add($"completion_claim_packet_validation_not_passed:{completionClaim.PacketValidationStatus}");
        }

        blockers.AddRange(completionClaim.PacketValidationBlockers.Select(blocker => $"completion_claim_packet:{blocker}"));
        blockers.AddRange(completionClaim.MissingFields.Select(field => $"completion_claim_missing_field:{field}"));
        blockers.AddRange(completionClaim.MissingContractItems.Select(item => $"completion_claim_missing_contract_item:{item}"));
        blockers.AddRange(completionClaim.DisallowedChangedFiles.Select(path => $"completion_claim_disallowed_changed_file:{path}"));
        blockers.AddRange(completionClaim.ForbiddenVocabularyHits.Select(term => $"completion_claim_forbidden_vocabulary:{term}"));

        var distinctBlockers = blockers
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var passed = distinctBlockers.Length == 0;
        return new ReviewClosureHostValidationSummary
        {
            Status = passed ? "passed" : "failed",
            Required = true,
            ReasonCode = passed ? "valid" : ResolveHostValidationReasonCode(distinctBlockers),
            Message = passed
                ? "Host validation can accept the worker completion claim as a candidate result declaration."
                : $"Host validation blocks Review closure: {string.Join(", ", distinctBlockers)}.",
            WorkerPacketId = completionClaim.PacketId,
            SourceExecutionPacketId = completionClaim.SourceExecutionPacketId,
            CompletionClaimStatus = completionClaim.Status,
            CompletionClaimPacketValidationStatus = completionClaim.PacketValidationStatus,
            CompletionClaimHostValidationRequired = completionClaim.HostValidationRequired,
            CompletionClaimClaimsTruthAuthority = completionClaim.ClaimIsTruth,
            Blockers = distinctBlockers,
            EvidenceRefs = BuildHostValidationEvidenceRefs(completionClaim),
            Notes =
            [
                "Worker completion claim is only a candidate declaration; Host validation remains the acceptance boundary.",
                "This summary is part of the ReviewBundle evidence and does not grant worker truth-write authority.",
            ],
        };
    }

    private static ReviewClosureCompletionClaimSummary ResolveCompletionClaim(
        WorkerCompletionClaim? workerCompletionClaim,
        ReviewClosureBundle? existingBundle)
    {
        if (workerCompletionClaim is not null && (workerCompletionClaim.Required || workerCompletionClaim.Status != "not_required"))
        {
            return ReviewClosureCompletionClaimSummary.FromWorkerClaim(workerCompletionClaim);
        }

        return existingBundle?.CompletionClaim ?? new ReviewClosureCompletionClaimSummary
        {
            Status = "not_recorded",
            Notes = ["No worker completion claim was attached to this review closure bundle."],
        };
    }

    private static ReviewClosureFallbackRunPacketSummary BuildFallbackRunPacket(
        TaskNode? task,
        bool manualFallback,
        ReviewClosureCompletionClaimSummary completionClaim,
        ReviewClosureBundle? existingBundle)
    {
        if (!manualFallback && existingBundle?.FallbackRunPacket.Required != true)
        {
            return new ReviewClosureFallbackRunPacketSummary
            {
                Required = false,
                Status = "not_required",
                StrictlyRequired = false,
                ClosureBlockerWhenIncomplete = false,
                Notes = ["Fallback run packet is not required for non-fallback worker review closure."],
            };
        }

        var metadata = task?.Metadata ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var roleSwitchReceiptRef = FirstNonEmpty(
            metadata.GetValueOrDefault("fallback_run_packet_role_switch_receipt"),
            existingBundle?.FallbackRunPacket.RoleSwitchReceiptRef);
        var contextReceiptRef = FirstNonEmpty(
            metadata.GetValueOrDefault("fallback_run_packet_context_receipt"),
            existingBundle?.FallbackRunPacket.ContextReceiptRef);
        var executionClaimRef = FirstNonEmpty(
            metadata.GetValueOrDefault("fallback_run_packet_execution_claim"),
            completionClaim.Status is "present" or "partial" ? $"worker_completion_claim:{completionClaim.Status}" : null,
            existingBundle?.FallbackRunPacket.ExecutionClaimRef);
        var reviewBundleRef = FirstNonEmpty(
            metadata.GetValueOrDefault("fallback_run_packet_review_bundle"),
            task is null ? null : $".ai/artifacts/reviews/{task.TaskId}.json#closure_bundle",
            existingBundle?.FallbackRunPacket.ReviewBundleRef);
        var receipts = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["role_switch_receipt"] = roleSwitchReceiptRef,
            ["context_receipt"] = contextReceiptRef,
            ["execution_claim"] = executionClaimRef,
            ["review_bundle"] = reviewBundleRef,
        };
        var present = receipts
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => item.Key)
            .ToArray();
        var missing = receipts
            .Where(item => string.IsNullOrWhiteSpace(item.Value))
            .Select(item => item.Key)
            .ToArray();

        return new ReviewClosureFallbackRunPacketSummary
        {
            Required = true,
            Status = missing.Length == 0 ? "complete" : "incomplete",
            StrictlyRequired = true,
            ClosureBlockerWhenIncomplete = true,
            RequiredReceipts = receipts.Keys.ToArray(),
            PresentReceipts = present,
            MissingReceipts = missing,
            RoleSwitchReceiptRef = roleSwitchReceiptRef,
            ContextReceiptRef = contextReceiptRef,
            ExecutionClaimRef = executionClaimRef,
            ReviewBundleRef = reviewBundleRef,
            GrantsExecutionAuthority = false,
            GrantsTruthWriteAuthority = false,
            CreatesTaskQueue = false,
            Notes =
            [
                "Fallback run packet is review-local evidence; it does not grant execution authority or truth-write authority.",
                "All required receipts must be present before fallback closure can allow writeback.",
            ],
        };
    }

    private static ReviewClosureDecision BuildClosureDecision(
        string candidateResultSource,
        string workerResultVerdict,
        string acceptedPatchSource,
        string completionMode,
        string reviewerDecision,
        ReviewClosureValidationSummary validation,
        ReviewClosureFallbackRunPacketSummary fallbackRunPacket,
        ReviewClosureContractMatrix contractMatrix,
        ReviewClosureCompletionClaimSummary completionClaim,
        ReviewClosureHostValidationSummary hostValidation,
        SafetyOutcome safetyOutcome,
        ReviewDecisionStatus effectiveDecisionStatus,
        bool writebackApplied)
    {
        if (writebackApplied)
        {
            return new ReviewClosureDecision
            {
                Status = "writeback_applied",
                Decision = "writeback_already_applied",
                WritebackAllowed = false,
                ResultSource = candidateResultSource,
                AcceptedPatchSource = acceptedPatchSource,
                WorkerResultVerdict = workerResultVerdict,
                ReviewerDecision = reviewerDecision,
                RequiredGateStatus = validation.RequiredGateStatus,
                ContractMatrixStatus = contractMatrix.Status,
                SafetyStatus = safetyOutcome == SafetyOutcome.Allow ? "passed" : "failed",
                HostValidationStatus = hostValidation.Status,
                Notes = ["Writeback has already been applied for this review artifact."],
            };
        }

        var blockers = new List<string>();
        if (effectiveDecisionStatus != ReviewDecisionStatus.Approved)
        {
            blockers.Add($"reviewer_decision_not_approved:{reviewerDecision}");
        }

        if (validation.RequiredGateStatus != "passed")
        {
            blockers.Add($"required_validation_not_passed:{validation.RequiredGateStatus}");
        }

        blockers.AddRange(validation.Profile.Blockers.Select(blocker => $"validation_profile:{blocker}"));

        if (fallbackRunPacket.Required && fallbackRunPacket.Status != "complete")
        {
            blockers.Add("fallback_run_packet_incomplete");
            blockers.AddRange(fallbackRunPacket.MissingReceipts.Select(receipt => $"fallback_run_packet_missing:{receipt}"));
        }

        if (contractMatrix.Status != "passed")
        {
            blockers.Add($"contract_matrix_not_passed:{contractMatrix.Status}");
        }

        blockers.AddRange(contractMatrix.Blockers.Select(blocker => $"contract_matrix:{blocker}"));

        if (completionClaim.Required && completionClaim.Status != "present")
        {
            blockers.Add($"completion_claim_not_present:{completionClaim.Status}");
            blockers.AddRange(completionClaim.MissingFields.Select(field => $"completion_claim_missing_field:{field}"));
        }

        if (completionClaim.PacketValidationStatus == "failed")
        {
            blockers.Add("completion_claim_packet_validation_failed");
            blockers.AddRange(completionClaim.PacketValidationBlockers.Select(blocker => $"completion_claim_packet:{blocker}"));
        }

        if (hostValidation.Required && hostValidation.Status != "passed")
        {
            blockers.Add($"host_validation_not_passed:{hostValidation.Status}");
            blockers.AddRange(hostValidation.Blockers.Select(blocker => $"host_validation:{blocker}"));
        }

        if (safetyOutcome != SafetyOutcome.Allow)
        {
            blockers.Add($"safety_not_allowed:{safetyOutcome}");
        }

        if (acceptedPatchSource == "none")
        {
            blockers.Add("accepted_patch_source_missing");
        }

        if (acceptedPatchSource == "worker_patch" && workerResultVerdict != "accepted")
        {
            blockers.Add($"worker_patch_not_accepted:{workerResultVerdict}");
        }

        var writebackAllowed = blockers.Count == 0;
        return new ReviewClosureDecision
        {
            Status = writebackAllowed ? "writeback_allowed" : "writeback_blocked",
            Decision = writebackAllowed ? "allow_writeback" : "block_writeback",
            WritebackAllowed = writebackAllowed,
            ResultSource = candidateResultSource,
            AcceptedPatchSource = acceptedPatchSource,
            WorkerResultVerdict = workerResultVerdict,
            ReviewerDecision = reviewerDecision,
            RequiredGateStatus = validation.RequiredGateStatus,
            ContractMatrixStatus = contractMatrix.Status,
            SafetyStatus = safetyOutcome == SafetyOutcome.Allow ? "passed" : "failed",
            HostValidationStatus = hostValidation.Status,
            Blockers = blockers,
            Notes =
            [
                "Closure arbiter is review-local evidence; it does not write task truth or bypass Host writeback.",
                $"completion_mode={completionMode}",
                fallbackRunPacket.Required ? $"fallback_run_packet={fallbackRunPacket.Status}" : "fallback_run_packet=not_required",
                $"host_validation={hostValidation.Status}",
            ],
        };
    }

    private static ReviewClosureValidationProfile BuildValidationProfile(
        TaskNode? task,
        IReadOnlyList<string> patchPaths,
        bool validationPassed,
        IReadOnlyList<string> validationEvidence,
        IReadOnlyList<CommandExecutionRecord> validationCommandResults,
        ReviewClosureBundle? existingBundle)
    {
        if (task is null
            && existingBundle?.Validation.Profile.RequiredGates.Count > 0)
        {
            return existingBundle.Validation.Profile;
        }

        var normalizedPatchPaths = NormalizePaths(patchPaths);
        var touchedScope = ResolveTouchedScope(task, normalizedPatchPaths);
        var knownRedBaseline = ExtractKnownRedBaseline(validationEvidence, validationCommandResults);
        var requiredGates = BuildRequiredValidationGates(
            task,
            validationPassed,
            validationEvidence,
            validationCommandResults);
        var advisoryGates = BuildAdvisoryValidationGates(validationEvidence, validationCommandResults, knownRedBaseline);
        var blockers = requiredGates
            .Where(gate => gate.Blocking && gate.Status is "failed" or "missing_evidence")
            .Select(gate => $"{gate.GateId}:{gate.Status}")
            .ToArray();

        return new ReviewClosureValidationProfile
        {
            ProfileId = ResolveValidationProfileId(task),
            Status = blockers.Length == 0 ? "passed" : "failed",
            TouchedScope = touchedScope,
            FailureAttribution = ResolveValidationFailureAttribution(requiredGates, advisoryGates, knownRedBaseline),
            RequiredGates = requiredGates,
            AdvisoryGates = advisoryGates,
            KnownRedBaseline = knownRedBaseline,
            Blockers = blockers,
            Notes =
            [
                "Required gates are blocking for review closure; advisory gates record broader validation without blocking when known-red baseline is explicit.",
            ],
        };
    }

    private static IReadOnlyList<ReviewClosureValidationGate> BuildRequiredValidationGates(
        TaskNode? task,
        bool validationPassed,
        IReadOnlyList<string> validationEvidence,
        IReadOnlyList<CommandExecutionRecord> validationCommandResults)
    {
        var requiredEvidence = validationEvidence
            .Where(IsFocusedValidationEvidence)
            .Concat(validationCommandResults
                .Where(IsFocusedValidationCommand)
                .Select(DescribeCommandResult))
            .ToArray();
        var hasAnyValidationEvidence = validationEvidence.Count > 0 || validationCommandResults.Count > 0;
        var gates = new List<ReviewClosureValidationGate>
        {
            ValidationGate(
                "focused_required_validation",
                "Focused required validation",
                !hasAnyValidationEvidence ? "missing_evidence" : validationPassed ? "passed" : "failed",
                blocking: true,
                requiredEvidence.Length == 0 ? validationEvidence : requiredEvidence),
        };

        if (IsRuntimeRecoverableResidueTask(task))
        {
            var roundtripEvidence = validationEvidence
                .Where(IsRoundtripValidationEvidence)
                .Concat(validationCommandResults
                    .Where(command => IsFocusedValidationCommand(command) && CommandText(command).Contains("ControlPlaneContentionTests", StringComparison.OrdinalIgnoreCase))
                    .Select(DescribeCommandResult))
                .ToArray();
            gates.Add(ValidationGate(
                "contract_roundtrip_required",
                "Contract roundtrip validation",
                validationPassed && roundtripEvidence.Length > 0 ? "passed" : "missing_evidence",
                blocking: true,
                roundtripEvidence));
        }

        var expectedEvidence = task?.Validation.ExpectedEvidence ?? Array.Empty<string>();
        if (expectedEvidence.Count > 0)
        {
            var matchedEvidence = expectedEvidence
                .Where(expected => validationEvidence.Any(evidence => evidence.Contains(expected, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            gates.Add(ValidationGate(
                "task_expected_evidence_required",
                "Task expected evidence",
                matchedEvidence.Length == expectedEvidence.Count ? "passed" : "missing_evidence",
                blocking: true,
                matchedEvidence.Length == 0 ? expectedEvidence : matchedEvidence));
        }

        return gates;
    }

    private static IReadOnlyList<ReviewClosureValidationGate> BuildAdvisoryValidationGates(
        IReadOnlyList<string> validationEvidence,
        IReadOnlyList<CommandExecutionRecord> validationCommandResults,
        IReadOnlyList<string> knownRedBaseline)
    {
        var fullSuiteCommandEvidence = validationCommandResults
            .Where(IsFullSuiteValidationCommand)
            .Select(DescribeCommandResult)
            .ToArray();
        var fullSuiteTextEvidence = validationEvidence
            .Where(IsFullSuiteValidationEvidence)
            .ToArray();
        var fullSuiteEvidence = fullSuiteCommandEvidence.Concat(fullSuiteTextEvidence).ToArray();
        if (fullSuiteEvidence.Length == 0)
        {
            return
            [
                ValidationGate(
                    "full_suite_advisory",
                    "Full suite advisory validation",
                    "not_recorded",
                    blocking: false,
                    Array.Empty<string>()),
            ];
        }

        var failedFullSuite = validationCommandResults.Any(command => IsFullSuiteValidationCommand(command) && command.ExitCode != 0)
                              || fullSuiteTextEvidence.Any(IsFailureEvidence);
        var status = failedFullSuite
            ? knownRedBaseline.Count > 0 ? "known_red_advisory_failure" : "failed_advisory"
            : "passed";

        return
        [
            ValidationGate(
                "full_suite_advisory",
                "Full suite advisory validation",
                status,
                blocking: false,
                fullSuiteEvidence),
        ];
    }

    private static ReviewClosureValidationGate ValidationGate(
        string gateId,
        string title,
        string status,
        bool blocking,
        IReadOnlyList<string> evidence)
    {
        return new ReviewClosureValidationGate
        {
            GateId = gateId,
            Title = title,
            Status = status,
            Blocking = blocking,
            Evidence = evidence,
        };
    }

    private static string ResolveRequiredGateStatus(ReviewClosureValidationProfile profile, bool validationPassed)
    {
        if (profile.RequiredGates.Count == 0)
        {
            return validationPassed ? "passed" : "failed";
        }

        if (profile.RequiredGates.Any(gate => gate.Status == "failed"))
        {
            return "failed";
        }

        if (profile.RequiredGates.Any(gate => gate.Status == "missing_evidence"))
        {
            return "missing_evidence";
        }

        return "passed";
    }

    private static string ResolveAdvisoryGateStatus(ReviewClosureValidationProfile profile)
    {
        if (profile.AdvisoryGates.Count == 0
            || profile.AdvisoryGates.All(gate => gate.Status == "not_recorded"))
        {
            return "not_recorded";
        }

        if (profile.AdvisoryGates.Any(gate => gate.Status == "failed_advisory"))
        {
            return "failed_advisory";
        }

        if (profile.AdvisoryGates.Any(gate => gate.Status == "known_red_advisory_failure"))
        {
            return "known_red_advisory_failure";
        }

        return "passed";
    }

    private static string ResolveKnownRedBaselineStatus(ReviewClosureValidationProfile profile)
    {
        return profile.KnownRedBaseline.Count == 0 ? "not_recorded" : "recorded";
    }

    private static string ResolveValidationFailureAttribution(
        IReadOnlyList<ReviewClosureValidationGate> requiredGates,
        IReadOnlyList<ReviewClosureValidationGate> advisoryGates,
        IReadOnlyList<string> knownRedBaseline)
    {
        if (requiredGates.Any(gate => gate.Status is "failed" or "missing_evidence"))
        {
            return "required_gate_failed";
        }

        if (advisoryGates.Any(gate => gate.Status == "failed_advisory"))
        {
            return "advisory_failure_unattributed";
        }

        if (advisoryGates.Any(gate => gate.Status == "known_red_advisory_failure") && knownRedBaseline.Count > 0)
        {
            return "known_red_advisory_only";
        }

        return "no_blocking_failure";
    }

    private static string ResolveValidationProfileId(TaskNode? task)
    {
        return IsRuntimeRecoverableResidueTask(task)
            ? "runtime_recoverable_residue_validation_profile_v1"
            : "task_validation_profile_v1";
    }

    private static string ResolveTouchedScope(TaskNode? task, IReadOnlyList<string> patchPaths)
    {
        if (patchPaths.Count > 0)
        {
            return string.Join(", ", patchPaths);
        }

        return task?.Scope.Count > 0
            ? string.Join(", ", task.Scope)
            : "not_recorded";
    }

    private static IReadOnlyList<string> ExtractKnownRedBaseline(
        IReadOnlyList<string> validationEvidence,
        IReadOnlyList<CommandExecutionRecord> validationCommandResults)
    {
        return validationEvidence
            .Where(IsKnownRedEvidence)
            .Concat(validationCommandResults
                .Where(command => IsKnownRedEvidence(command.StandardOutput) || IsKnownRedEvidence(command.StandardError))
                .Select(DescribeCommandResult))
            .ToArray();
    }

    private static bool IsRuntimeRecoverableResidueTask(TaskNode? task)
    {
        return task is not null
               && (string.Equals(task.CardId, "CARD-972", StringComparison.Ordinal)
                   || task.TaskId.StartsWith("T-CARD-972", StringComparison.Ordinal)
                   || ContainsAny($"{task.Title} {task.Description}", "recoverable residue", "residue contract"));
    }

    private static bool IsFocusedValidationEvidence(string evidence)
    {
        return ContainsAny(evidence, "focused", "targeted", "--filter", "FullyQualifiedName~", "roundtrip", "passed");
    }

    private static bool IsRoundtripValidationEvidence(string evidence)
    {
        return ContainsAny(evidence, "roundtrip", "persist -> read", "write -> persist -> read", "ControlPlaneContentionTests", "ManagedWorkspaceLeaseServiceTests");
    }

    private static bool IsFullSuiteValidationEvidence(string evidence)
    {
        return ContainsAny(evidence, "full suite", "full dotnet test", "full validation", "advisory full");
    }

    private static bool IsKnownRedEvidence(string text)
    {
        return ContainsAny(text, "known red", "known-red", "known_red", "existing failure", "existing failures", "not caused by this patch", "baseline red");
    }

    private static bool IsFailureEvidence(string text)
    {
        return ContainsAny(text, "failed", "failure", "red");
    }

    private static bool IsFocusedValidationCommand(CommandExecutionRecord record)
    {
        if (!string.Equals(record.Category, "validation", StringComparison.OrdinalIgnoreCase)
            && !record.Category.Contains("validation", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var command = CommandText(record);
        return !IsFullSuiteValidationCommand(record)
               || ContainsAny(command, "--filter", "FullyQualifiedName~", "ControlPlaneContentionTests", "ManagedWorkspaceLeaseServiceTests", "PlannerReviewRealityProjectionTests");
    }

    private static bool IsFullSuiteValidationCommand(CommandExecutionRecord record)
    {
        var command = CommandText(record);
        if (!command.Contains("dotnet test", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !ContainsAny(command, "--filter", "FullyQualifiedName~");
    }

    private static string CommandText(CommandExecutionRecord record)
    {
        return string.Join(' ', record.Command);
    }

    private static string DescribeCommandResult(CommandExecutionRecord record)
    {
        var status = record.Skipped ? "skipped" : record.ExitCode == 0 ? "passed" : "failed";
        return $"{status}: {CommandText(record)}";
    }

    private static ReviewClosureContractMatrix BuildContractMatrix(
        TaskNode? task,
        IReadOnlyList<string> patchPaths,
        bool validationPassed,
        IReadOnlyList<string> validationEvidence,
        SafetyOutcome safetyOutcome,
        IReadOnlyList<string> safetyIssues,
        ReviewClosureBundle? existingBundle)
    {
        if (task is null && existingBundle?.ContractMatrix.Checks.Count > 0)
        {
            return existingBundle.ContractMatrix;
        }

        var normalizedPatchPaths = NormalizePaths(patchPaths);
        var candidatePaths = NormalizePaths(normalizedPatchPaths.Concat(task?.Scope ?? Array.Empty<string>()));
        var profileId = ResolveContractProfileId(task, candidatePaths, existingBundle);
        var checks = profileId == RuntimeRecoverableResidueProfileId
            ? BuildRuntimeRecoverableResidueChecks(
                candidatePaths,
                normalizedPatchPaths,
                validationPassed,
                validationEvidence,
                safetyIssues,
                task)
            : BuildGenericReviewClosureChecks(
                normalizedPatchPaths,
                validationPassed,
                validationEvidence,
                safetyOutcome,
                safetyIssues);
        var blockers = checks
            .Where(check => check.Status is "failed" or "missing_evidence")
            .Select(check => $"{check.CheckId}:{check.Status}")
            .ToArray();

        return new ReviewClosureContractMatrix
        {
            ProfileId = profileId,
            Status = ResolveContractMatrixStatus(checks),
            Checks = checks,
            Blockers = blockers,
            Notes =
            [
                profileId == RuntimeRecoverableResidueProfileId
                    ? "CARD-972 residue closure profile: schema, persistence, readback, severity, roundtrip, and scope hygiene are checked inside Review."
                    : "Generic closure profile: Review records validation, safety, patch scope, and scope hygiene without changing public task status.",
            ],
        };
    }

    private static IReadOnlyList<ReviewClosureContractCheck> BuildGenericReviewClosureChecks(
        IReadOnlyList<string> patchPaths,
        bool validationPassed,
        IReadOnlyList<string> validationEvidence,
        SafetyOutcome safetyOutcome,
        IReadOnlyList<string> safetyIssues)
    {
        return
        [
            ContractCheck(
                "review_artifact_present",
                "Review artifact present",
                "passed",
                "Review generated a structured closure bundle.",
                ["review_closure_bundle"]),
            ContractCheck(
                "validation_recorded",
                "Validation evidence recorded",
                validationEvidence.Count == 0 ? "missing_evidence" : validationPassed ? "passed" : "failed",
                validationEvidence.Count == 0
                    ? "No validation evidence was recorded for this candidate result."
                    : validationPassed
                        ? "Required validation evidence was recorded as passed."
                        : "Validation evidence was recorded but did not pass.",
                validationEvidence.Count == 0 ? Array.Empty<string>() : validationEvidence),
            ContractCheck(
                "safety_recorded",
                "Safety outcome recorded",
                safetyOutcome == SafetyOutcome.Allow ? "passed" : "failed",
                safetyOutcome == SafetyOutcome.Allow
                    ? "Safety allowed the candidate result."
                    : "Safety did not allow the candidate result.",
                safetyIssues.Count == 0 ? [$"safety={safetyOutcome}"] : safetyIssues),
            ContractCheck(
                "patch_scope_recorded",
                "Patch scope recorded",
                patchPaths.Count == 0 ? "missing_evidence" : "passed",
                patchPaths.Count == 0
                    ? "No changed paths were recorded for this candidate result."
                    : "Changed paths were recorded for review.",
                patchPaths),
            BuildScopeHygieneCheck(patchPaths),
        ];
    }

    private static IReadOnlyList<ReviewClosureContractCheck> BuildRuntimeRecoverableResidueChecks(
        IReadOnlyList<string> candidatePaths,
        IReadOnlyList<string> patchPaths,
        bool validationPassed,
        IReadOnlyList<string> validationEvidence,
        IReadOnlyList<string> safetyIssues,
        TaskNode? task)
    {
        var allReviewText = FlattenReviewText(validationEvidence, safetyIssues, task);
        var schemaEvidence = MatchingPaths(
            candidatePaths,
            "ControlPlaneResidueContract.cs",
            "ControlPlaneLockHandle.cs",
            "ControlPlaneLockLeaseSnapshot.cs",
            "ControlPlaneLockOptions.cs");
        var persistenceEvidence = MatchingPaths(
            candidatePaths,
            "ControlPlaneLockService.cs",
            "ManagedWorkspaceLeaseService.cs");
        var projectionEvidence = MatchingPaths(
            candidatePaths,
            "ManagedWorkspaceLeaseService.cs",
            "RuntimeProductClosurePilotStatusService.cs");
        var roundtripEvidence = validationEvidence
            .Where(evidence => ContainsAny(
                evidence,
                "ControlPlaneContentionTests",
                "ManagedWorkspaceLeaseServiceTests",
                "roundtrip",
                "persist -> read",
                "write -> persist -> read"))
            .ToArray();
        var forbiddenSeverityEvidence = allReviewText
            .Where(ContainsForbiddenSeverityVocabulary)
            .ToArray();

        return
        [
            ContractCheck(
                "residue_contract_schema_presence",
                "Residue contract schema presence",
                schemaEvidence.Count == 0 ? "missing_evidence" : "passed",
                schemaEvidence.Count == 0
                    ? "No residue/lock schema path evidence was recorded."
                    : "Residue and lock schema paths are present in the candidate scope.",
                schemaEvidence),
            ContractCheck(
                "persistence_readback_wiring",
                "Persistence/readback wiring",
                persistenceEvidence.Count == 0 ? "missing_evidence" : "passed",
                persistenceEvidence.Count == 0
                    ? "No persistence or lease readback wiring path evidence was recorded."
                    : "Persistence or lease readback wiring paths are present.",
                persistenceEvidence),
            ContractCheck(
                "review_surface_projection",
                "Review/readback projection",
                projectionEvidence.Count == 0 ? "missing_evidence" : "passed",
                projectionEvidence.Count == 0
                    ? "No inspect/status projection path evidence was recorded."
                    : "Review/readback projection paths are present.",
                projectionEvidence),
            ContractCheck(
                "severity_vocabulary_invariant",
                "Severity vocabulary invariant",
                forbiddenSeverityEvidence.Length == 0 ? "passed" : "failed",
                forbiddenSeverityEvidence.Length == 0
                    ? "No forbidden low/medium/high severity vocabulary was recorded in review evidence."
                    : "Forbidden low/medium/high severity vocabulary was recorded.",
                forbiddenSeverityEvidence.Length == 0
                    ? ["allowed_severity_values=warning,error"]
                    : forbiddenSeverityEvidence),
            ContractCheck(
                "roundtrip_validation_evidence",
                "Roundtrip validation evidence",
                validationPassed && roundtripEvidence.Length > 0 ? "passed" : "missing_evidence",
                validationPassed && roundtripEvidence.Length > 0
                    ? "Roundtrip-focused validation evidence was recorded as passed."
                    : "No passing roundtrip-focused validation evidence was recorded.",
                roundtripEvidence),
            BuildScopeHygieneCheck(patchPaths),
        ];
    }

    private static ReviewClosureContractCheck BuildScopeHygieneCheck(IReadOnlyList<string> patchPaths)
    {
        var protectedPathEvidence = patchPaths
            .Where(IsProtectedTruthRootPath)
            .ToArray();
        return ContractCheck(
            "scope_hygiene",
            "Scope hygiene",
            protectedPathEvidence.Length == 0 ? "passed" : "failed",
            protectedPathEvidence.Length == 0
                ? "No protected truth-root path was recorded in the candidate patch."
                : "Protected truth-root paths were recorded in the candidate patch.",
            protectedPathEvidence.Length == 0 ? ["no_protected_truth_root_patch_paths"] : protectedPathEvidence);
    }

    private static ReviewClosureContractCheck ContractCheck(
        string checkId,
        string title,
        string status,
        string summary,
        IReadOnlyList<string> evidence)
    {
        return new ReviewClosureContractCheck
        {
            CheckId = checkId,
            Title = title,
            Status = status,
            Summary = summary,
            Evidence = evidence,
        };
    }

    private static string ResolveContractProfileId(
        TaskNode? task,
        IReadOnlyList<string> candidatePaths,
        ReviewClosureBundle? existingBundle)
    {
        if (task is null)
        {
            return string.IsNullOrWhiteSpace(existingBundle?.ContractMatrix.ProfileId)
                ? GenericClosureProfileId
                : existingBundle.ContractMatrix.ProfileId;
        }

        if (string.Equals(task.CardId, "CARD-972", StringComparison.Ordinal)
            || task.TaskId.StartsWith("T-CARD-972", StringComparison.Ordinal)
            || candidatePaths.Any(path => ContainsAny(
                path,
                "ControlPlaneResidueContract",
                "ControlPlaneLock",
                "ManagedWorkspaceLeaseService",
                "RuntimeProductClosurePilotStatusService"))
            || ContainsAny($"{task.Title} {task.Description}", "recoverable residue", "residue contract"))
        {
            return RuntimeRecoverableResidueProfileId;
        }

        return GenericClosureProfileId;
    }

    private static string ResolveContractMatrixStatus(IReadOnlyList<ReviewClosureContractCheck> checks)
    {
        if (checks.Count == 0)
        {
            return "not_evaluated";
        }

        if (checks.Any(check => check.Status == "failed"))
        {
            return "failed";
        }

        if (checks.Any(check => check.Status is "missing_evidence" or "not_evaluated"))
        {
            return "partial";
        }

        return "passed";
    }

    private static IReadOnlyList<string> NormalizePaths(IEnumerable<string> paths)
    {
        return paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Where(path => !string.Equals(path, "(none)", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> MatchingPaths(IReadOnlyList<string> paths, params string[] needles)
    {
        return paths
            .Where(path => ContainsAny(path, needles))
            .ToArray();
    }

    private static IReadOnlyList<string> FlattenReviewText(
        IReadOnlyList<string> validationEvidence,
        IReadOnlyList<string> safetyIssues,
        TaskNode? task)
    {
        return validationEvidence
            .Concat(safetyIssues)
            .Concat(task?.Acceptance ?? Array.Empty<string>())
            .Concat(task?.Constraints ?? Array.Empty<string>())
            .Concat(new[] { task?.Title ?? string.Empty, task?.Description ?? string.Empty })
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        return needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsForbiddenSeverityVocabulary(string text)
    {
        return ContainsAny(
            text,
            "severity=low",
            "severity: low",
            "severity low",
            "low severity",
            "severity=medium",
            "severity: medium",
            "severity medium",
            "medium severity",
            "severity=high",
            "severity: high",
            "severity high",
            "high severity",
            "low/medium/high",
            "low / medium / high");
    }

    private static bool IsProtectedTruthRootPath(string path)
    {
        var normalized = path.TrimStart('.', '/', '\\');
        return normalized.StartsWith("ai/", StringComparison.Ordinal)
               || normalized.StartsWith("ai\\", StringComparison.Ordinal)
               || normalized.StartsWith("carves-platform/", StringComparison.Ordinal)
               || normalized.StartsWith("carves-platform\\", StringComparison.Ordinal);
    }

    public static IReadOnlyList<string> ParsePatchPaths(string patchSummary)
    {
        const string marker = "paths=";
        var markerIndex = patchSummary.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return Array.Empty<string>();
        }

        return NormalizePaths(patchSummary[(markerIndex + marker.Length)..].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
    }

    private static string ResolveCandidateResultSource(
        TaskNode? task,
        ReviewClosureBundle? existingBundle,
        bool manualFallback)
    {
        if (existingBundle is not null && !string.IsNullOrWhiteSpace(existingBundle.CandidateResultSource))
        {
            return existingBundle.CandidateResultSource;
        }

        var hasWorkerHistory = task?.LastWorkerFailureKind != WorkerFailureKind.None
                               || !string.IsNullOrWhiteSpace(task?.LastWorkerRunId)
                               || !string.IsNullOrWhiteSpace(task?.Metadata.GetValueOrDefault("completion_historical_worker_run_id"))
                               || !string.IsNullOrWhiteSpace(task?.Metadata.GetValueOrDefault("completion_historical_worker_backend"));
        if (hasWorkerHistory)
        {
            return "worker";
        }

        return manualFallback
            ? "manual_fallback"
            : "worker";
    }

    private static string ResolveWorkerResultVerdict(
        bool validationPassed,
        SafetyOutcome safetyOutcome,
        ReviewDecisionStatus effectiveDecisionStatus,
        DomainTaskStatus resultingStatus,
        bool manualFallback,
        WorkerFailureKind? historicalWorkerFailureKind,
        ReviewClosureBundle? existingBundle)
    {
        if (manualFallback && historicalWorkerFailureKind is { } failureKind && failureKind != WorkerFailureKind.None)
        {
            return failureKind switch
            {
                WorkerFailureKind.TaskLogicFailed or WorkerFailureKind.InvalidOutput or WorkerFailureKind.PatchFailure or WorkerFailureKind.ApprovalRequired
                    => "rejected_semantic_incomplete",
                WorkerFailureKind.ContractFailure or WorkerFailureKind.PolicyDenied
                    => "rejected_contract_drift",
                WorkerFailureKind.BuildFailure or WorkerFailureKind.TestFailure
                    => "rejected_validation_failed",
                _ => "rejected_worker_result",
            };
        }

        if (!validationPassed)
        {
            return "rejected_validation_failed";
        }

        if (safetyOutcome != SafetyOutcome.Allow)
        {
            return "rejected_safety_gate";
        }

        if (effectiveDecisionStatus == ReviewDecisionStatus.Rejected)
        {
            return "rejected_by_review";
        }

        if (effectiveDecisionStatus is ReviewDecisionStatus.Approved or ReviewDecisionStatus.ProvisionalAccepted
            && !manualFallback
            && resultingStatus is DomainTaskStatus.Completed or DomainTaskStatus.Merged or DomainTaskStatus.Review)
        {
            return "accepted";
        }

        return existingBundle?.WorkerResultVerdict is { Length: > 0 } existingVerdict
               && !string.Equals(existingVerdict, "review_pending", StringComparison.Ordinal)
            ? existingVerdict
            : "review_pending";
    }

    private static string ResolveAcceptedPatchSource(PlannerReview review, bool manualFallback)
    {
        if (manualFallback)
        {
            return "manual_fallback_patch";
        }

        return review.DecisionStatus is ReviewDecisionStatus.Approved or ReviewDecisionStatus.ProvisionalAccepted
            ? "worker_patch"
            : "none";
    }

    private static string ResolveCompletionMode(
        ReviewDecisionStatus effectiveDecisionStatus,
        DomainTaskStatus resultingStatus,
        bool manualFallback)
    {
        if (manualFallback)
        {
            return "manual_fallback_after_worker_review";
        }

        return effectiveDecisionStatus switch
        {
            ReviewDecisionStatus.PendingReview when resultingStatus == DomainTaskStatus.Review => "worker_review_pending",
            ReviewDecisionStatus.Approved => "worker_review_approved",
            ReviewDecisionStatus.ProvisionalAccepted => "provisional_acceptance",
            ReviewDecisionStatus.Rejected => "review_rejected",
            ReviewDecisionStatus.Reopened => "review_reopened",
            ReviewDecisionStatus.Blocked => "review_blocked",
            ReviewDecisionStatus.Superseded => "review_superseded",
            _ when resultingStatus == DomainTaskStatus.Review => "review_pending",
            _ => "review_recorded",
        };
    }

    private static string ResolveReviewerDecision(ReviewDecisionStatus effectiveDecisionStatus)
    {
        return JsonNamingPolicy.SnakeCaseLower.ConvertName(effectiveDecisionStatus.ToString());
    }

    private static string ResolveWritebackRecommendation(
        ReviewDecisionStatus effectiveDecisionStatus,
        bool manualFallback,
        bool writebackApplied)
    {
        if (writebackApplied)
        {
            return "writeback_applied";
        }

        if (manualFallback)
        {
            return "manual_fallback_recorded";
        }

        return effectiveDecisionStatus switch
        {
            ReviewDecisionStatus.PendingReview or ReviewDecisionStatus.NeedsAttention => "review_required_before_writeback",
            ReviewDecisionStatus.Approved or ReviewDecisionStatus.ProvisionalAccepted => "writeback_pending_materialization",
            _ => "writeback_blocked",
        };
    }

    private static bool IsManualFallback(TaskNode? task)
    {
        return string.Equals(
            task?.Metadata.GetValueOrDefault("completion_provenance"),
            "manual_fallback",
            StringComparison.Ordinal);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }

    private static IReadOnlyList<string> BuildHostValidationEvidenceRefs(
        ReviewClosureCompletionClaimSummary completionClaim)
    {
        return completionClaim.EvidencePaths
            .Concat(new[]
            {
                string.IsNullOrWhiteSpace(completionClaim.PacketId) ? null : $"worker_packet:{completionClaim.PacketId}",
                string.IsNullOrWhiteSpace(completionClaim.SourceExecutionPacketId) ? null : $"execution_packet:{completionClaim.SourceExecutionPacketId}",
                string.IsNullOrWhiteSpace(completionClaim.RawClaimHash) ? null : $"completion_claim_hash:{completionClaim.RawClaimHash}",
            })
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string ResolveHostValidationReasonCode(IReadOnlyList<string> blockers)
    {
        var first = blockers.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(first))
        {
            return "host_validation_failed";
        }

        var separator = first.IndexOf(':', StringComparison.Ordinal);
        return separator > 0 ? first[..separator] : first;
    }

    private static WorkerFailureKind? ResolveHistoricalWorkerFailureKind(TaskNode? task)
    {
        if (task is null)
        {
            return null;
        }

        if (task.LastWorkerFailureKind != WorkerFailureKind.None)
        {
            return task.LastWorkerFailureKind;
        }

        var historical = task.Metadata.GetValueOrDefault("completion_historical_worker_failure_kind");
        return Enum.TryParse<WorkerFailureKind>(historical, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    private static ReviewDecisionStatus ResolveEffectiveDecisionStatus(PlannerReview review, DomainTaskStatus resultingStatus)
    {
        if (review.DecisionStatus != ReviewDecisionStatus.NeedsAttention)
        {
            return review.DecisionStatus;
        }

        return resultingStatus switch
        {
            DomainTaskStatus.Review => ReviewDecisionStatus.PendingReview,
            DomainTaskStatus.Blocked => ReviewDecisionStatus.Blocked,
            DomainTaskStatus.Superseded => ReviewDecisionStatus.Superseded,
            _ => ReviewDecisionStatus.NeedsAttention,
        };
    }
}

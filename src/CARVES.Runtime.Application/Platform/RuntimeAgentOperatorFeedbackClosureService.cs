using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAgentOperatorFeedbackClosureService
{
    private readonly string repoRoot;

    public RuntimeAgentOperatorFeedbackClosureService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
    }

    public RuntimeAgentOperatorFeedbackClosureSurface Build()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        const string boundaryDocumentPath = "docs/runtime/runtime-agent-governed-operator-feedback-closure-contract.md";
        const string guidePath = "docs/guides/RUNTIME_AGENT_V1_OPERATOR_FEEDBACK_GUIDE.md";
        const string deliveryReadinessGuidePath = "docs/guides/RUNTIME_AGENT_V1_DELIVERY_READINESS.md";
        const string firstRunPacketPath = "docs/runtime/runtime-first-run-operator-packet.md";
        const string validationBundleGuidePath = "docs/guides/RUNTIME_AGENT_V1_VALIDATION_BUNDLE.md";
        const string failureRecoveryPath = "docs/runtime/runtime-agent-governed-failure-classification-recovery-closure-contract.md";

        ValidatePath(boundaryDocumentPath, "Stage 6 operator feedback contract", errors);
        ValidatePath(guidePath, "Runtime Agent v1 operator feedback guide", errors);
        ValidatePath(deliveryReadinessGuidePath, "Runtime Agent v1 delivery readiness guide", errors);
        ValidatePath(firstRunPacketPath, "Runtime first-run packet", errors);
        ValidatePath(validationBundleGuidePath, "Runtime validation bundle guide", errors);
        ValidatePath(failureRecoveryPath, "Runtime failure recovery contract", errors);

        return new RuntimeAgentOperatorFeedbackClosureSurface
        {
            BoundaryDocumentPath = boundaryDocumentPath,
            GuidePath = guidePath,
            DeliveryReadinessGuidePath = deliveryReadinessGuidePath,
            FirstRunPacketPath = firstRunPacketPath,
            ValidationBundleGuidePath = validationBundleGuidePath,
            FailureRecoveryPath = failureRecoveryPath,
            OverallPosture = errors.Count == 0
                ? "bounded_operator_feedback_ready"
                : "blocked_by_operator_feedback_gaps",
            FeedbackBundles = GetFeedbackBundles(),
            RecommendedNextAction = errors.Count == 0
                ? "Project the matching feedback bundle into attach/status/help surfaces instead of relying on author memory for host ensure, bootstrap, validation, or repair guidance."
                : "Restore the missing Stage 6 feedback-closure anchors before treating operator guidance as bounded and supportable.",
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            NonClaims =
            [
                "This surface does not create a second planner, second control plane, or hidden operator state.",
                "This surface does not replace first-run packet, delivery readiness, validation bundle, or failure recovery truth.",
                "This surface does not reopen Stage 0 through Stage 5 boundaries in the name of polish.",
            ],
        };
    }

    public static string SelectBundleId(bool hostConnected, bool runtimePresent, RepoRuntimeHealthState healthState, string dispatchState)
    {
        if (!hostConnected)
        {
            return "host_start_and_attach";
        }

        if (!runtimePresent)
        {
            return "attach_and_bootstrap";
        }

        if (healthState is RepoRuntimeHealthState.Broken or RepoRuntimeHealthState.Dirty)
        {
            return "repair_and_recovery";
        }

        if (string.Equals(dispatchState, "dispatchable", StringComparison.OrdinalIgnoreCase))
        {
            return "dispatchable_run";
        }

        return "delivery_and_validation_readback";
    }

    public static IReadOnlyList<RuntimeAgentOperatorFeedbackBundleSurface> GetFeedbackBundles()
    {
        return
        [
            new RuntimeAgentOperatorFeedbackBundleSurface
            {
                BundleId = "host_start_and_attach",
                TriggerState = "host_not_running",
                Summary = "Guide the operator back onto the single Runtime-owned entry lane before any bootstrap or delivery claim.",
                Commands =
                [
                    "carves host ensure --json",
                    "carves attach",
                    "carves inspect runtime-first-run-operator-packet",
                ],
                Notes =
                [
                    "Do not invent local bootstrap state while the resident host is unavailable.",
                ],
            },
            new RuntimeAgentOperatorFeedbackBundleSurface
            {
                BundleId = "attach_and_bootstrap",
                TriggerState = "runtime_not_initialized",
                Summary = "Guide the operator through attach and the bounded bootstrap readback without creating a second onboarding flow.",
                Commands =
                [
                    "carves attach",
                    "carves inspect runtime-first-run-operator-packet",
                    "carves inspect runtime-agent-delivery-readiness",
                ],
                Notes =
                [
                    "Attach remains the only Runtime-owned bootstrap write path.",
                ],
            },
            new RuntimeAgentOperatorFeedbackBundleSurface
            {
                BundleId = "delivery_and_validation_readback",
                TriggerState = "runtime_initialized_idle",
                Summary = "Guide the operator through the bounded Stage 6 readback before claiming friend-trial delivery readiness.",
                Commands =
                [
                    "carves inspect runtime-agent-delivery-readiness",
                    "carves inspect runtime-agent-validation-bundle",
                    "carves status",
                ],
                Notes =
                [
                    "Delivery readiness and validation bundle stay on the same Runtime-owned lane.",
                ],
            },
            new RuntimeAgentOperatorFeedbackBundleSurface
            {
                BundleId = "repair_and_recovery",
                TriggerState = "runtime_dirty_or_broken",
                Summary = "Guide the operator through bounded repair and recovery without jumping to hidden fallback.",
                Commands =
                [
                    "carves maintain repair",
                    "carves inspect runtime-agent-failure-recovery-closure",
                    "carves status",
                ],
                Notes =
                [
                    "Use the existing repair and failure-recovery lane before broader intervention.",
                ],
            },
            new RuntimeAgentOperatorFeedbackBundleSurface
            {
                BundleId = "dispatchable_run",
                TriggerState = "runtime_dispatchable",
                Summary = "Guide the operator into the existing governed run lane once the repo is healthy and dispatchable.",
                Commands =
                [
                    "carves run",
                    "carves status",
                    "carves inspect runtime-agent-validation-bundle",
                ],
                Notes =
                [
                    "Keep execution on the existing Host-routed lane.",
                ],
            },
        ];
    }

    private void ValidatePath(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(repoRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }
}

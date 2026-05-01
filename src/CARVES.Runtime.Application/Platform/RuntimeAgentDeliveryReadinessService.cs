namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAgentDeliveryReadinessService
{
    private readonly string repoRoot;

    public RuntimeAgentDeliveryReadinessService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
    }

    public RuntimeAgentDeliveryReadinessSurface Build()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        const string boundaryDocumentPath = "docs/runtime/runtime-agent-governed-packaging-closure-delivery-readiness-contract.md";
        const string guidePath = "docs/guides/RUNTIME_AGENT_V1_DELIVERY_READINESS.md";
        const string packagingMaturityPath = "docs/runtime/runtime-packaging-proof-federation-maturity.md";
        const string firstRunPacketPath = "docs/runtime/runtime-first-run-operator-packet.md";
        const string validationBundleGuidePath = "docs/guides/RUNTIME_AGENT_V1_VALIDATION_BUNDLE.md";
        const string trialWrapperPath = "scripts/carves-host.ps1";

        ValidatePath(boundaryDocumentPath, "Stage 6 packaging closure contract", errors);
        ValidatePath(guidePath, "Runtime Agent v1 delivery readiness guide", errors);
        ValidatePath(packagingMaturityPath, "Runtime packaging maturity contract", errors);
        ValidatePath(firstRunPacketPath, "Runtime first-run packet", errors);
        ValidatePath(validationBundleGuidePath, "Runtime validation bundle guide", errors);
        ValidatePath(trialWrapperPath, "Source-tree trial wrapper", errors);

        return new RuntimeAgentDeliveryReadinessSurface
        {
            BoundaryDocumentPath = boundaryDocumentPath,
            GuidePath = guidePath,
            PackagingMaturityPath = packagingMaturityPath,
            FirstRunPacketPath = firstRunPacketPath,
            ValidationBundleGuidePath = validationBundleGuidePath,
            TrialWrapperPath = trialWrapperPath,
            OverallPosture = errors.Count == 0
                ? "bounded_delivery_readiness_ready"
                : "blocked_by_delivery_readiness_gaps",
            EntryCommands =
            [
                RuntimeHostCommandLauncher.Cold("host", "start", "--interval-ms", "200"),
                RuntimeHostCommandLauncher.Cold("attach"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-first-run-operator-packet"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-agent-delivery-readiness"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-agent-validation-bundle"),
            ],
            RuntimeTruthFiles =
            [
                ".ai/config/system.json",
                ".ai/runtime.json",
                ".ai/runtime/attach-handshake.json",
            ],
            DerivedPackagingArtifacts =
            [
                "source_tree_cold_launcher_generation",
                "temp_deployment_output",
                "host_runtime_dir",
            ],
            RelatedSurfaceRefs =
            [
                "runtime-first-run-operator-packet",
                "runtime-agent-validation-bundle",
                "runtime-packaging-proof-federation-maturity",
            ],
            DeliveryClaims =
            [
                "friend_trial_delivery_readback_is_repeatable",
                "source_tree_wrapper_is_supported_without_becoming_truth_owner",
                "delivery_readiness_stays_on_one_runtime_owned_entry_lane",
            ],
            BlockedClaims =
            [
                "installer_owned_bootstrap_truth",
                "runtime_owned_second_product_shell",
                "operator_scope_repatriated_from_carves_operator",
                "package_side_hidden_install_metadata_as_truth",
            ],
            RecommendedNextAction = errors.Count == 0
                ? "Use attach, first-run packet, delivery-readiness readback, and the validation bundle on the same Runtime-owned lane before claiming friend-trial delivery readiness."
                : "Restore the missing Stage 6 delivery-readiness anchors before treating packaging closure as bounded and repeatable.",
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            NonClaims =
            [
                "This surface does not claim installable operator packaging now belongs inside Runtime.",
                "This surface keeps installable operator entry and product-shell scope in sibling repo CARVES.Operator.",
                "This surface does not replace repo-local bootstrap truth with wrapper or installer state.",
                "This surface does not create a second entry lane outside attach, first-run packet, and validation bundle readback.",
            ],
        };
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

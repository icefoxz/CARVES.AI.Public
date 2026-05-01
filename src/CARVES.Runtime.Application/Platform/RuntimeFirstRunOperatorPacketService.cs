using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeFirstRunOperatorPacketService
{
    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;
    private readonly Func<RuntimeSessionGatewayInternalBetaGateSurface> internalBetaGateFactory;

    public RuntimeFirstRunOperatorPacketService(
        string repoRoot,
        Func<RuntimeSessionGatewayInternalBetaGateSurface> internalBetaGateFactory)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
        this.internalBetaGateFactory = internalBetaGateFactory;
    }

    public RuntimeFirstRunOperatorPacketSurface Build()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        const string packetPath = "docs/runtime/runtime-first-run-operator-packet.md";
        const string internalBetaGatePath = "docs/session-gateway/internal-beta-gate.md";
        const string trustedBootstrapTruthPath = "docs/runtime/runtime-trusted-bootstrap-truth-schema.md";
        const string onboardingAccelerationContractPath = "docs/runtime/runtime-onboarding-acceleration-contract.md";
        const string alphaSetupPath = "docs/session-gateway/ALPHA_SETUP.md";
        const string alphaQuickstartPath = "docs/session-gateway/ALPHA_QUICKSTART.md";

        ValidateDocument(packetPath, "First-run operator packet", errors);
        ValidateDocument(internalBetaGatePath, "Internal beta gate", errors);
        ValidateDocument(trustedBootstrapTruthPath, "Trusted bootstrap truth schema", errors);
        ValidateDocument(onboardingAccelerationContractPath, "Onboarding acceleration contract", errors);
        ValidateDocument(alphaSetupPath, "Alpha setup", errors);
        ValidateDocument(alphaQuickstartPath, "Alpha quickstart", errors);

        var gate = internalBetaGateFactory();
        var attachedTargetDocumentProjection = !string.Equals(documentRoot.Mode, "repo_local", StringComparison.Ordinal)
            && !string.Equals(documentRoot.Mode, "repo_local_missing_runtime_docs", StringComparison.Ordinal);
        if (attachedTargetDocumentProjection)
        {
            if (!gate.IsValid || gate.Errors.Count > 0 || gate.Warnings.Count > 0)
            {
                warnings.Add("Internal beta gate target-scope readback is summarized for attached targets; Runtime internal beta closure is not recomputed from target repo task truth.");
            }
        }
        else
        {
            errors.AddRange(gate.Errors.Select(error => $"Internal beta gate surface: {error}"));
            warnings.AddRange(gate.Warnings.Select(warning => $"Internal beta gate surface: {warning}"));
        }

        var packetReady = errors.Count == 0;

        if (!string.Equals(gate.OverallPosture, "internal_beta_gated_ready", StringComparison.Ordinal))
        {
            warnings.Add($"Internal beta gate posture is {gate.OverallPosture}, not internal_beta_gated_ready.");
        }

        return new RuntimeFirstRunOperatorPacketSurface
        {
            PacketPath = packetPath,
            InternalBetaGatePath = internalBetaGatePath,
            TrustedBootstrapTruthPath = trustedBootstrapTruthPath,
            OnboardingAccelerationContractPath = onboardingAccelerationContractPath,
            AlphaSetupPath = alphaSetupPath,
            AlphaQuickstartPath = alphaQuickstartPath,
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            OverallPosture = packetReady ? "first_run_packet_ready" : "blocked_by_first_run_packet_gaps",
            InternalBetaGatePosture = attachedTargetDocumentProjection && !gate.IsValid
                ? "runtime_document_root_reused_for_attached_target"
                : gate.OverallPosture,
            CurrentProofSource = gate.OperatorProofContract.CurrentProofSource,
            CurrentOperatorState = gate.OperatorProofContract.CurrentOperatorState,
            ProjectIdentification =
            [
                "real_repo_path",
                "entry_source",
                "runtime_host_lane_selection",
            ],
            BootstrapTruthFamilies =
            [
                "project_identity",
                "boundary_and_ownership",
                "runtime_entry_posture",
                "initial_working_assumptions",
                "proof_posture",
            ],
            RequiredOperatorActions =
            [
                "identify project and entry source before treating the lane as a real first-run",
                "establish trusted bootstrap truth instead of letting AI inference become official truth",
                "review import/config mapping and keep unknowns visible before acceleration",
                "confirm required operator actions and proof obligations on the bounded Runtime-owned lane",
                "continue only with explicit real-world evidence or stop without claiming bootstrap completion",
            ],
            AllowedAiAssistance =
            [
                "summarize the current project shape after trusted bootstrap truth is explicit",
                "suggest initial tasks or next questions on the same Runtime-owned lane",
                "propose proof obligations and evidence gaps without self-certifying completion",
                "assemble the bounded first-run reading bundle and entry commands",
            ],
            ExitCriteria =
            [
                "trusted bootstrap truth is explicit, inspectable, versioned, and operator-reviewable",
                "current unknowns remain visible before onboarding acceleration begins",
                "internal beta gate posture and operator-proof obligations have been read on the same lane",
                "the operator can either continue with real-world evidence collection or stop without synthetic completion claims",
            ],
            RequiredEvidenceBundle = gate.RequiredEvidenceBundle,
            EntryCommands =
            [
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-first-run-operator-packet"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-session-gateway-internal-beta-gate"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-session-gateway-private-alpha-handoff"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-session-gateway-repeatability"),
                RuntimeHostCommandLauncher.Cold("host", "status"),
            ],
            MinimumOnboardingReads = RuntimeMinimumOnboardingGuidance.Reads,
            MinimumOnboardingNextSteps = RuntimeMinimumOnboardingGuidance.NextSteps,
            BlockedClaims =
            [
                "the first-run packet does not imply full project-creation init is implemented",
                "the first-run packet does not imply init --import or init --config are implemented",
                "the first-run packet does not imply onboard can establish trusted bootstrap truth by itself",
                "the first-run packet does not remove operator-proof obligations or upgrade proof source beyond repo_local_proof",
                "the first-run packet does not create a second onboarding root, second control plane, or client-owned truth lane",
                "attached target first-run packet readback does not recompute Runtime internal beta closure from target repo task truth",
            ],
            RecommendedNextAction = packetReady
                ? "Read README.md and AGENTS.md, inspect runtime-first-run-operator-packet on the same lane, then capture the first initialization card before broader execution claims."
                : "Restore the missing packet inputs before treating Runtime as ready to guide bounded first-run internal beta entry.",
            IsValid = packetReady,
            Errors = errors,
            Warnings = warnings,
            NonClaims =
            [
                "The first-run packet is a Runtime-owned read model, not a project-creation init or onboard workflow.",
                "Trusted bootstrap truth must exist before onboarding acceleration is treated as valid.",
                "The first-run packet does not replace the internal beta gate; it packages that gate with bootstrap and acceleration boundaries for bounded operator entry.",
            ],
        };
    }

    private void ValidateDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }
}

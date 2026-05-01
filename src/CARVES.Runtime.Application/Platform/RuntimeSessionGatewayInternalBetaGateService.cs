using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeSessionGatewayInternalBetaGateService
{
    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;
    private readonly Func<RuntimeSessionGatewayPrivateAlphaHandoffSurface> privateAlphaHandoffFactory;
    private readonly Func<RuntimeSessionGatewayRepeatabilitySurface> repeatabilityFactory;

    public RuntimeSessionGatewayInternalBetaGateService(
        string repoRoot,
        Func<RuntimeSessionGatewayPrivateAlphaHandoffSurface> privateAlphaHandoffFactory,
        Func<RuntimeSessionGatewayRepeatabilitySurface> repeatabilityFactory)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
        this.privateAlphaHandoffFactory = privateAlphaHandoffFactory;
        this.repeatabilityFactory = repeatabilityFactory;
    }

    public RuntimeSessionGatewayInternalBetaGateSurface Build()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        const string executionPlanPath = "docs/session-gateway/session-gateway-v1-post-closure-execution-plan.md";
        const string releaseSurfacePath = "docs/session-gateway/release-surface.md";
        const string internalBetaGatePath = "docs/session-gateway/internal-beta-gate.md";
        const string repeatabilityReadinessPath = "docs/session-gateway/repeatability-readiness.md";
        const string operatorProofContractPath = "docs/session-gateway/operator-proof-contract.md";
        const string alphaSetupPath = "docs/session-gateway/ALPHA_SETUP.md";
        const string alphaQuickstartPath = "docs/session-gateway/ALPHA_QUICKSTART.md";
        const string successfulProofPacketPath = "docs/session-gateway/internal-operator-proof-packet-2026-04-06-carves-site-scope-repair.md";
        const string successfulProofEvidencePath = ".ai/runtime/internal-operator-proof/2026-04-06-carves-site-session-gateway-scope-repair.json";
        const string previousFailurePacketPath = "docs/session-gateway/internal-operator-proof-packet-2026-04-06-carves-site.md";
        const string previousFailureEvidencePath = ".ai/runtime/internal-operator-proof/2026-04-06-carves-site-session-gateway-failure.json";

        ValidateDocument(executionPlanPath, "Execution plan", errors);
        ValidateDocument(releaseSurfacePath, "Release surface", errors);
        ValidateDocument(internalBetaGatePath, "Internal beta gate", errors);
        ValidateDocument(repeatabilityReadinessPath, "Repeatability readiness", errors);
        ValidateDocument(operatorProofContractPath, "Operator proof contract", errors);
        ValidateDocument(alphaSetupPath, "Alpha setup", errors);
        ValidateDocument(alphaQuickstartPath, "Alpha quickstart", errors);
        ValidateDocument(successfulProofPacketPath, "Successful internal proof packet", errors);
        ValidateDocument(successfulProofEvidencePath, "Successful internal proof evidence", errors);
        ValidateOptionalDocument(previousFailurePacketPath, "Previous internal failure packet", warnings);
        ValidateOptionalDocument(previousFailureEvidencePath, "Previous internal failure evidence", warnings);

        var handoff = privateAlphaHandoffFactory();
        var repeatability = repeatabilityFactory();
        errors.AddRange(handoff.Errors.Select(error => $"Private alpha handoff surface: {error}"));
        warnings.AddRange(handoff.Warnings.Select(warning => $"Private alpha handoff surface: {warning}"));
        errors.AddRange(repeatability.Errors.Select(error => $"Repeatability surface: {error}"));
        warnings.AddRange(repeatability.Warnings.Select(warning => $"Repeatability surface: {warning}"));

        var gateReady =
            handoff.IsValid
            && repeatability.IsValid
            && string.Equals(handoff.OverallPosture, "private_alpha_deliverable_ready", StringComparison.Ordinal)
            && string.Equals(repeatability.OverallPosture, "repeatable_private_alpha_ready", StringComparison.Ordinal)
            && errors.Count == 0;

        if (!string.Equals(handoff.OverallPosture, "private_alpha_deliverable_ready", StringComparison.Ordinal))
        {
            warnings.Add($"Private alpha handoff posture is {handoff.OverallPosture}, not private_alpha_deliverable_ready.");
        }

        if (!string.Equals(repeatability.OverallPosture, "repeatable_private_alpha_ready", StringComparison.Ordinal))
        {
            warnings.Add($"Repeatability posture is {repeatability.OverallPosture}, not repeatable_private_alpha_ready.");
        }

        return new RuntimeSessionGatewayInternalBetaGateSurface
        {
            ExecutionPlanPath = executionPlanPath,
            ReleaseSurfacePath = releaseSurfacePath,
            InternalBetaGatePath = internalBetaGatePath,
            RepeatabilityReadinessPath = repeatabilityReadinessPath,
            OperatorProofContractPath = operatorProofContractPath,
            AlphaSetupPath = alphaSetupPath,
            AlphaQuickstartPath = alphaQuickstartPath,
            SuccessfulProofPacketPath = successfulProofPacketPath,
            SuccessfulProofEvidencePath = successfulProofEvidencePath,
            PreviousFailurePacketPath = previousFailurePacketPath,
            PreviousFailureEvidencePath = previousFailureEvidencePath,
            OverallPosture = gateReady ? "internal_beta_gated_ready" : "blocked_by_internal_beta_gate_gaps",
            PrivateAlphaHandoffPosture = handoff.OverallPosture,
            RepeatabilityPosture = repeatability.OverallPosture,
            ThinShellRoute = handoff.ThinShellRoute,
            SessionCollectionRoute = handoff.SessionCollectionRoute,
            MessageRouteTemplate = handoff.MessageRouteTemplate,
            EventsRouteTemplate = handoff.EventsRouteTemplate,
            AcceptedOperationRouteTemplate = handoff.AcceptedOperationRouteTemplate,
            SupportedIntents = handoff.SupportedIntents,
            IncludedScope =
            [
                "internal operators on the existing Runtime-owned Session Gateway lane",
                "discuss, plan, and governed_run over Strict Broker-only semantics",
                "attached real repos on the same host-owned lane after the CARD-633 scoping repair",
                "Runtime-owned accepted-operation, review, replan, and evidence projection",
                "thin-shell projection without front-end-owned mutation, git, shell, or provider authority",
            ],
            BlockedClaims =
            [
                "internal beta gate does not auto-upgrade current proof source to operator_run_proof",
                "internal beta gate does not imply external_user_proof or public release readiness",
                "internal beta gate does not imply multi-user scale, team workflow authority, or beta-scale product expansion",
                "internal beta gate does not permit a second control plane, second host lane, or client-owned truth root",
                "internal beta gate does not widen Session Gateway v1 beyond the existing Runtime-owned Strict Broker-only surface",
            ],
            RequiredEvidenceBundle = handoff.OperatorProofContract.SharedRequiredEvidence,
            EntryCommands =
            [
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-session-gateway-internal-beta-gate"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-session-gateway-private-alpha-handoff"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-session-gateway-repeatability"),
                RuntimeHostCommandLauncher.Cold("host", "status"),
            ],
            OperatorProofContract = handoff.OperatorProofContract,
            RecommendedNextAction = gateReady
                ? "Use the internal beta gate to admit bounded internal operators on the existing Runtime-owned lane, but keep operator-proof obligations explicit and refuse broader completion claims."
                : "Restore the missing internal beta gate inputs before treating Session Gateway as bounded-ready for internal beta.",
            IsValid = gateReady,
            Errors = errors,
            Warnings = warnings,
            NonClaims =
            [
                "A successful attached-repo scoping rerun does not erase the operator proof contract or its WAITING_OPERATOR_* obligations.",
                "The internal beta gate is a Runtime-owned read model, not a second planning or release authority.",
                "The internal beta gate does not replace private alpha docs, repeatability truth, or the operator proof contract; it links them into one bounded entry contract.",
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

    private void ValidateOptionalDocument(string repoRelativePath, string label, List<string> warnings)
    {
        var fullPath = Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            warnings.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }
}

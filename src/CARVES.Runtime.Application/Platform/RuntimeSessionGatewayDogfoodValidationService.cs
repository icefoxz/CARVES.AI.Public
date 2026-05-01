using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeSessionGatewayDogfoodValidationService
{
    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;
    private readonly Func<RuntimeGovernanceProgramReauditSurface> programReauditFactory;

    public RuntimeSessionGatewayDogfoodValidationService(
        string repoRoot,
        Func<RuntimeGovernanceProgramReauditSurface> programReauditFactory)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
        this.programReauditFactory = programReauditFactory;
    }

    public RuntimeSessionGatewayDogfoodValidationSurface Build()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var boundaryDocumentPath = "docs/session-gateway/dogfood-validation.md";
        var executionPlanPath = "docs/session-gateway/session-gateway-v1-post-closure-execution-plan.md";
        var releaseSurfacePath = "docs/session-gateway/release-surface.md";
        var operatorProofContractPath = "docs/session-gateway/operator-proof-contract.md";
        ValidateDocument(boundaryDocumentPath, "Boundary document", errors);
        ValidateDocument(executionPlanPath, "Execution plan", errors);
        ValidateDocument(releaseSurfacePath, "Release surface", errors);
        ValidateDocument(operatorProofContractPath, "Operator proof contract", errors);

        var programReaudit = programReauditFactory();
        errors.AddRange(programReaudit.Errors.Select(error => $"Program re-audit surface: {error}"));
        warnings.AddRange(programReaudit.Warnings.Select(warning => $"Program re-audit surface: {warning}"));
        var operatorProofContract = RuntimeSessionGatewayOperatorProofContractCatalog.BuildPrivateAlphaContract();

        var dogfoodValidated = string.Equals(programReaudit.OverallVerdict, "program_closure_complete", StringComparison.Ordinal);
        return new RuntimeSessionGatewayDogfoodValidationSurface
        {
            BoundaryDocumentPath = boundaryDocumentPath,
            ExecutionPlanPath = executionPlanPath,
            ReleaseSurfacePath = releaseSurfacePath,
            OperatorProofContractPath = operatorProofContractPath,
            OverallPosture = dogfoodValidated ? "narrow_private_alpha_ready" : "blocked_by_runtime_governance",
            ProgramClosureVerdict = programReaudit.OverallVerdict,
            ContinuationGateOutcome = programReaudit.ContinuationGateOutcome,
            ThinShellRoute = "/session-gateway/v1/shell",
            SessionCollectionRoute = "/api/session-gateway/v1/sessions",
            MessageRouteTemplate = "/api/session-gateway/v1/sessions/{session_id}/messages",
            EventsRouteTemplate = "/api/session-gateway/v1/sessions/{session_id}/events",
            AcceptedOperationRouteTemplate = "/api/session-gateway/v1/operations/{operation_id}",
            SupportedIntents =
            [
                "discuss",
                "plan",
                "governed_run",
            ],
            ValidatedScenarios =
            [
                "create_or_resume_session",
                "discuss_plan_governed_run_classification",
                "ordered_event_projection",
                "runtime_hosted_thin_shell_projection",
                "accepted_operation_lookup_under_gateway_namespace",
                "approve_reject_replan_mutation_forwarding",
                "same_lane_private_alpha_readiness",
                "operator_proof_contract_projection",
            ],
            DeferredFollowOns = [],
            OperatorProofContract = operatorProofContract,
            RecommendedNextAction = dogfoodValidated
                ? "Proceed with narrow private alpha under bounded operator oversight and stop at WAITING_OPERATOR_SETUP until operator-run proof exists."
                : "Restore program closure before claiming Session Gateway dogfood validation.",
            IsValid = errors.Count == 0 && programReaudit.IsValid,
            Errors = errors,
            Warnings = warnings,
            MutationForwardingPosture = "runtime_owned_forwarding_landed",
            PrivateAlphaPosture = "narrow_private_alpha_ready",
            NonClaims =
            [
                "Dogfood validation does not authorize a second control plane or a front-end-owned execution lane.",
                "Narrow private alpha readiness does not authorize a second planner, second scheduler, or client-owned mutation truth.",
                "Private alpha readiness does not widen Session Gateway v1 beyond Strict Broker-only semantics.",
                "Repo-local readiness does not count as operator-run or external-user proof.",
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

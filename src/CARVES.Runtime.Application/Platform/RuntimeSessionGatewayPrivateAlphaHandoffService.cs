using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeSessionGatewayPrivateAlphaHandoffService
{
    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;
    private readonly Func<RuntimeSessionGatewayDogfoodValidationSurface> dogfoodValidationFactory;
    private readonly Func<OperationalSummary> operationalSummaryFactory;
    private readonly Func<RepoRuntimeHealthCheckResult> runtimeHealthFactory;

    public RuntimeSessionGatewayPrivateAlphaHandoffService(
        string repoRoot,
        Func<RuntimeSessionGatewayDogfoodValidationSurface> dogfoodValidationFactory,
        Func<OperationalSummary> operationalSummaryFactory,
        Func<RepoRuntimeHealthCheckResult> runtimeHealthFactory)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
        this.dogfoodValidationFactory = dogfoodValidationFactory;
        this.operationalSummaryFactory = operationalSummaryFactory;
        this.runtimeHealthFactory = runtimeHealthFactory;
    }

    public RuntimeSessionGatewayPrivateAlphaHandoffSurface Build()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        const string executionPlanPath = "docs/session-gateway/session-gateway-v1-post-closure-execution-plan.md";
        const string releaseSurfacePath = "docs/session-gateway/release-surface.md";
        const string dogfoodValidationPath = "docs/session-gateway/dogfood-validation.md";
        const string operatorProofContractPath = "docs/session-gateway/operator-proof-contract.md";
        const string alphaSetupPath = "docs/session-gateway/ALPHA_SETUP.md";
        const string alphaQuickstartPath = "docs/session-gateway/ALPHA_QUICKSTART.md";
        const string knownLimitationsPath = "docs/session-gateway/KNOWN_LIMITATIONS.md";
        const string bugReportBundlePath = "docs/session-gateway/BUG_REPORT_BUNDLE.md";

        ValidateDocument(executionPlanPath, "Execution plan", errors);
        ValidateDocument(releaseSurfacePath, "Release surface", errors);
        ValidateDocument(dogfoodValidationPath, "Dogfood validation", errors);
        ValidateDocument(operatorProofContractPath, "Operator proof contract", errors);
        ValidateDocument(alphaSetupPath, "Alpha setup", errors);
        ValidateDocument(alphaQuickstartPath, "Alpha quickstart", errors);
        ValidateDocument(knownLimitationsPath, "Known limitations", errors);
        ValidateDocument(bugReportBundlePath, "Bug report bundle", errors);

        var dogfoodValidation = dogfoodValidationFactory();
        errors.AddRange(dogfoodValidation.Errors.Select(error => $"Dogfood validation surface: {error}"));
        warnings.AddRange(dogfoodValidation.Warnings.Select(warning => $"Dogfood validation surface: {warning}"));

        var operationalSummary = operationalSummaryFactory();
        var runtimeHealth = runtimeHealthFactory();
        var operatorProofContract = RuntimeSessionGatewayOperatorProofContractCatalog.BuildPrivateAlphaContract();

        if (runtimeHealth.State != RepoRuntimeHealthState.Healthy)
        {
            warnings.Add($"Runtime health posture is {runtimeHealth.State.ToString().ToLowerInvariant()}: {runtimeHealth.Summary}");
        }

        if (operationalSummary.ProviderHealthIssueCount > 0)
        {
            warnings.Add($"Operational summary reports {operationalSummary.ProviderHealthIssueCount} provider health issues.");
        }

        var readinessSatisfied =
            dogfoodValidation.IsValid
            && string.Equals(dogfoodValidation.OverallPosture, "narrow_private_alpha_ready", StringComparison.Ordinal)
            && string.Equals(dogfoodValidation.ProgramClosureVerdict, "program_closure_complete", StringComparison.Ordinal)
            && errors.Count == 0;

        var providerStatuses = operationalSummary.Providers
            .Select(provider => $"{provider.ProviderId}/{provider.BackendId}:{provider.State}; next={provider.RecommendedNextAction}")
            .ToArray();
        var providerVisibilitySummary =
            $"actionability_issues={operationalSummary.ProviderHealthIssueCount}; optional={operationalSummary.OptionalProviderHealthIssueCount}; disabled={operationalSummary.DisabledProviderCount}";
        var runtimeIssueSummaries = runtimeHealth.Issues
            .Select(issue => $"{issue.Code}:{issue.Summary}")
            .ToArray();

        return new RuntimeSessionGatewayPrivateAlphaHandoffSurface
        {
            ExecutionPlanPath = executionPlanPath,
            ReleaseSurfacePath = releaseSurfacePath,
            DogfoodValidationPath = dogfoodValidationPath,
            OperatorProofContractPath = operatorProofContractPath,
            AlphaSetupPath = alphaSetupPath,
            AlphaQuickstartPath = alphaQuickstartPath,
            KnownLimitationsPath = knownLimitationsPath,
            BugReportBundlePath = bugReportBundlePath,
            OverallPosture = readinessSatisfied ? "private_alpha_deliverable_ready" : "blocked_by_gateway_readiness",
            DogfoodValidationPosture = dogfoodValidation.OverallPosture,
            ProgramClosureVerdict = dogfoodValidation.ProgramClosureVerdict,
            ContinuationGateOutcome = dogfoodValidation.ContinuationGateOutcome,
            ThinShellRoute = dogfoodValidation.ThinShellRoute,
            SessionCollectionRoute = dogfoodValidation.SessionCollectionRoute,
            MessageRouteTemplate = dogfoodValidation.MessageRouteTemplate,
            EventsRouteTemplate = dogfoodValidation.EventsRouteTemplate,
            AcceptedOperationRouteTemplate = dogfoodValidation.AcceptedOperationRouteTemplate,
            RuntimeHealthState = runtimeHealth.State.ToString().ToLowerInvariant(),
            RuntimeHealthSummary = runtimeHealth.Summary,
            RuntimeHealthSuggestedAction = runtimeHealth.SuggestedAction,
            RuntimeHealthIssueCount = runtimeHealth.Issues.Count,
            ProviderHealthIssueCount = operationalSummary.ProviderHealthIssueCount,
            OptionalProviderHealthIssueCount = operationalSummary.OptionalProviderHealthIssueCount,
            DisabledProviderCount = operationalSummary.DisabledProviderCount,
            ProviderVisibilitySummary = providerVisibilitySummary,
            OperationalRecommendedNextAction = operationalSummary.RecommendedNextAction,
            ProviderStatuses = providerStatuses,
            RuntimeIssueSummaries = runtimeIssueSummaries,
            StartupCommands =
            [
                RuntimeHostCommandLauncher.Cold("host", "start", "--interval-ms", "200"),
                RuntimeHostCommandLauncher.Cold("host", "status"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-session-gateway-private-alpha-handoff"),
            ],
            ProviderStatusCommands =
            [
                RuntimeHostCommandLauncher.Cold("api", "operational-summary"),
                RuntimeHostCommandLauncher.Cold("worker", "health", "--no-refresh"),
                RuntimeHostCommandLauncher.Cold("worker", "providers"),
            ],
            RuntimeHealthCommands =
            [
                RuntimeHostCommandLauncher.Cold("verify", "runtime"),
                RuntimeHostCommandLauncher.Cold("status", "--summary"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-governance-program-reaudit"),
            ],
            MaintenanceCommands =
            [
                RuntimeHostCommandLauncher.Cold("repair"),
                RuntimeHostCommandLauncher.Cold("rebuild"),
                RuntimeHostCommandLauncher.Cold("reset", "--derived"),
            ],
            BugReportBundleCommands =
            [
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-session-gateway-private-alpha-handoff"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-session-gateway-dogfood-validation"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-governance-program-reaudit"),
                RuntimeHostCommandLauncher.Cold("api", "operational-summary"),
                RuntimeHostCommandLauncher.Cold("status", "--summary"),
            ],
            SupportedIntents = dogfoodValidation.SupportedIntents,
            OperatorProofContract = operatorProofContract,
            RecommendedNextAction = readinessSatisfied
                ? "Proceed with bounded private alpha handoff, but stop at WAITING_OPERATOR_SETUP until a real project, run, evidence bundle, and human verdict exist."
                : "Restore Session Gateway dogfood readiness before treating Phase B as deliverable.",
            IsValid = readinessSatisfied,
            Errors = errors,
            Warnings = warnings,
            NonClaims =
            [
                "Private alpha handoff does not create a second planner, second executor, or client-owned truth root.",
                "Provider visibility does not grant front-end-owned provider authority or key ownership.",
                "Private alpha deliverable readiness does not widen Session Gateway v1 beyond Strict Broker-only and local-first single-user posture.",
                "Private alpha deliverable readiness does not mean operator-run proof or external-user proof already exists.",
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

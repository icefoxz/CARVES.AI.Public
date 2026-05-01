using Carves.Runtime.Application.Processes;

namespace Carves.Runtime.Application.Guard;

public sealed class GuardCheckService
{
    private readonly GuardPolicyService policyService;
    private readonly GuardDiffAdapter diffAdapter;
    private readonly GuardPolicyEvaluator evaluator;

    public GuardCheckService(IProcessRunner processRunner)
        : this(new GuardPolicyService(), new GuardDiffAdapter(processRunner), new GuardPolicyEvaluator())
    {
    }

    public GuardCheckService(
        GuardPolicyService policyService,
        GuardDiffAdapter diffAdapter,
        GuardPolicyEvaluator evaluator)
    {
        this.policyService = policyService;
        this.diffAdapter = diffAdapter;
        this.evaluator = evaluator;
    }

    public GuardCheckResult Check(
        string repositoryRoot,
        string policyPath = ".ai/guard-policy.json",
        string baseRef = "HEAD",
        string? headRef = null,
        string sourceTool = "cli")
    {
        var runId = CreateRunId();
        var policyLoad = policyService.Load(repositoryRoot, policyPath);
        if (!policyLoad.IsValid || policyLoad.Policy is null)
        {
            return new GuardCheckResult(BuildPolicyFailureDecision(runId, policyLoad), Context: null);
        }

        var input = new GuardDiffInput(
            repositoryRoot,
            baseRef,
            headRef,
            policyPath,
            DiffText: null,
            ChangedFiles: Array.Empty<GuardChangedFileInput>(),
            runId,
            sourceTool);
        var context = diffAdapter.BuildContext(input, policyLoad.Policy);
        var decision = evaluator.Evaluate(context, runId);
        return new GuardCheckResult(decision, context);
    }

    public GuardCheckResult CheckChangedFiles(
        string repositoryRoot,
        IReadOnlyList<string> changedFiles,
        string policyPath = ".ai/guard-policy.json",
        string sourceTool = "guard-run")
    {
        var runId = CreateRunId();
        var policyLoad = policyService.Load(repositoryRoot, policyPath);
        if (!policyLoad.IsValid || policyLoad.Policy is null)
        {
            return new GuardCheckResult(BuildPolicyFailureDecision(runId, policyLoad), Context: null);
        }

        var inputs = changedFiles
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim().Replace('\\', '/'))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new GuardChangedFileInput(
                path,
                OldPath: null,
                GuardFileChangeStatus.Modified,
                Additions: 0,
                Deletions: 0,
                IsBinary: false,
                WasUntracked: false))
            .ToArray();
        var input = new GuardDiffInput(
            repositoryRoot,
            BaseRef: "task-run-envelope",
            HeadRef: null,
            policyPath,
            DiffText: null,
            inputs,
            runId,
            sourceTool);
        var context = diffAdapter.BuildContext(input, policyLoad.Policy);
        var decision = evaluator.Evaluate(context, runId);
        return new GuardCheckResult(decision, context);
    }

    private static GuardDecision BuildPolicyFailureDecision(string runId, GuardPolicyLoadResult policyLoad)
    {
        var ruleId = policyLoad.ErrorCode switch
        {
            "policy.unsupported_schema_version" => "policy.unsupported_schema_version",
            "policy.missing" => "policy.invalid",
            "policy.invalid_json" => "policy.invalid",
            "policy.unknown_field" => "policy.invalid",
            _ => "policy.invalid",
        };
        var violation = new GuardViolation(
            ruleId,
            GuardSeverity.Block,
            "Guard policy could not be loaded.",
            FilePath: null,
            policyLoad.ErrorMessage ?? "policy load failed",
            $"guard-rule:{runId}:{ruleId}:0");
        return new GuardDecision(
            runId,
            GuardDecisionOutcome.Block,
            PolicyId: "unknown",
            Summary: "Patch blocked because Alpha Guard policy could not be loaded.",
            Violations: [violation],
            Warnings: [],
            EvidenceRefs: [$"guard-run:{runId}", violation.EvidenceRef],
            RequiresRuntimeTaskTruth: false);
    }

    private static string CreateRunId()
    {
        return $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssZ}-{Guid.NewGuid():N}"[..34];
    }
}

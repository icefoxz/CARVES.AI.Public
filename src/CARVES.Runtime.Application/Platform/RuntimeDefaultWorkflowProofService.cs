namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeDefaultWorkflowProofService
{
    private const string FirstThreadCommand = "carves agent start --json";
    private const string WarmContextCommand = "carves agent context --json";
    private const string MarkdownBudgetCommand = "carves api runtime-markdown-read-path-budget";
    private const string GovernanceCoverageCommand = "carves api runtime-governance-surface-coverage-audit";
    private const string WorkflowProofCommand = "carves api runtime-default-workflow-proof";
    private const string ProblemReportCommand = "carves pilot report-problem <json-path> --json";
    private const string QuickstartPath = "docs/guides/CARVES_EXTERNAL_AGENT_QUICKSTART.md";
    private const string ConsumerResourcePackPath = "docs/guides/CARVES_EXTERNAL_CONSUMER_RESOURCE_PACK.md";

    private readonly string repoRoot;

    public RuntimeDefaultWorkflowProofService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
    }

    public RuntimeDefaultWorkflowProofSurface Build(
        RuntimeAgentThreadStartSurface threadStart,
        RuntimeAgentShortContextSurface shortContext,
        RuntimeMarkdownReadPathBudgetSurface markdownBudget,
        RuntimeGovernanceSurfaceCoverageAuditSurface governanceCoverage,
        RuntimeExternalConsumerResourcePackSurface resourcePack)
    {
        var quickstart = ReadRepoFile(QuickstartPath);
        var consumerPack = ReadRepoFile(ConsumerResourcePackPath);
        var checks = BuildChecks(threadStart, shortContext, markdownBudget, governanceCoverage, resourcePack, quickstart, consumerPack).ToArray();
        var structuralGaps = checks
            .Where(static check => check.Blocking && !check.Passed)
            .Select(static check => $"default_workflow_check_failed:{check.CheckId}")
            .ToArray();
        var proofComplete = structuralGaps.Length == 0;
        var currentRuntimeBlockers = threadStart.ThreadStartReady ? [] : threadStart.Gaps.ToArray();
        var posture = ResolvePosture(proofComplete, threadStart.ThreadStartReady);

        return new RuntimeDefaultWorkflowProofSurface
        {
            RepoRoot = repoRoot,
            OverallPosture = posture,
            WorkflowProofComplete = proofComplete,
            CurrentRuntimeReady = threadStart.ThreadStartReady,
            CurrentRuntimePosture = threadStart.OverallPosture,
            CurrentStageId = threadStart.CurrentStageId,
            CurrentStageStatus = threadStart.CurrentStageStatus,
            NextGovernedCommand = threadStart.NextGovernedCommand,
            NextCommandSource = threadStart.NextCommandSource,
            DefaultFirstThreadCommandCount = 2,
            DefaultWarmReorientationCommandCount = 2,
            OptionalTroubleshootingCommandCount = 4,
            PostInitializationMarkdownTokens = markdownBudget.PostInitializationDefault.EstimatedDefaultMarkdownTokens,
            PostInitializationMarkdownTokenBudget = markdownBudget.PostInitializationDefault.MaxDefaultMarkdownTokens,
            DeferredGeneratedMarkdownTokens = markdownBudget.GeneratedMarkdownViews.DeferredMarkdownTokens,
            ShortContextReady = shortContext.ShortContextReady,
            MarkdownReadPathWithinBudget = markdownBudget.PostInitializationDefault.WithinBudget,
            GovernanceSurfaceCoverageComplete = governanceCoverage.CoverageComplete,
            ResourcePackCoversDefaultCommands = ResourcePackContains(resourcePack, FirstThreadCommand)
                                                  && ResourcePackContains(resourcePack, WarmContextCommand)
                                                  && ResourcePackContains(resourcePack, MarkdownBudgetCommand)
                                                  && ResourcePackContains(resourcePack, GovernanceCoverageCommand)
                                                  && ResourcePackContains(resourcePack, WorkflowProofCommand),
            DefaultPath = BuildDefaultPath(threadStart).ToArray(),
            WarmPath = BuildWarmPath(threadStart).ToArray(),
            OptionalProofAndTroubleshootingPath = BuildOptionalPath().ToArray(),
            Checks = checks,
            StructuralGaps = structuralGaps,
            CurrentRuntimeBlockers = currentRuntimeBlockers,
            EvidenceSourcePaths =
            [
                QuickstartPath,
                ConsumerResourcePackPath,
                "runtime-agent-thread-start",
                "runtime-agent-short-context",
                "runtime-markdown-read-path-budget",
                "runtime-governance-surface-coverage-audit",
                "runtime-external-consumer-resource-pack",
            ],
            Summary = BuildSummary(proofComplete, threadStart, markdownBudget),
            RecommendedNextAction = proofComplete
                ? BuildRecommendedNextAction(threadStart)
                : "Restore the listed structural gaps before claiming the default workflow proof.",
            NonClaims =
            [
                "This surface is read-only and does not initialize, plan, approve, execute, write back, stage, commit, push, tag, release, or retarget anything.",
                "This surface does not claim the current repo or target is ready; current Runtime blockers are reported separately from structural workflow proof gaps.",
                "This surface does not replace the required first-session initialization report, task overlay, execution packet, review gates, or problem-report flow.",
                "This surface does not turn troubleshooting readbacks into the default path and does not grant planning or continuation authority.",
            ],
        };
    }

    private static IEnumerable<RuntimeDefaultWorkflowProofStep> BuildDefaultPath(RuntimeAgentThreadStartSurface threadStart)
    {
        yield return BuildStep(
            "first_thread_start",
            FirstThreadCommand,
            "runtime-agent-thread-start",
            "first_thread",
            required: true,
            "covered",
            "single new-thread entry command");
        yield return BuildStep(
            "follow_next_governed_command",
            NormalizeCommand(threadStart.NextGovernedCommand),
            threadStart.NextCommandSource,
            "first_thread",
            required: true,
            string.IsNullOrWhiteSpace(threadStart.NextGovernedCommand) ? "missing" : "selected",
            "next command selected by runtime-agent-thread-start");
    }

    private static IEnumerable<RuntimeDefaultWorkflowProofStep> BuildWarmPath(RuntimeAgentThreadStartSurface threadStart)
    {
        yield return BuildStep(
            "warm_reorientation",
            WarmContextCommand,
            "runtime-agent-short-context",
            "warm_reorientation",
            required: true,
            "covered",
            "single compact readback for warm orientation");
        yield return BuildStep(
            "warm_follow_next_governed_command",
            NormalizeCommand(threadStart.NextGovernedCommand),
            threadStart.NextCommandSource,
            "warm_reorientation",
            required: true,
            string.IsNullOrWhiteSpace(threadStart.NextGovernedCommand) ? "missing" : "selected",
            "same next governed command remains authoritative after short-context orientation");
    }

    private static IEnumerable<RuntimeDefaultWorkflowProofStep> BuildOptionalPath()
    {
        yield return BuildStep(
            "markdown_read_decision",
            MarkdownBudgetCommand,
            "runtime-markdown-read-path-budget",
            "decision_support",
            required: false,
            "covered",
            "use only when deciding whether Markdown needs to be opened");
        yield return BuildStep(
            "governance_surface_coverage",
            GovernanceCoverageCommand,
            "runtime-governance-surface-coverage-audit",
            "handoff_audit",
            required: false,
            "covered",
            "use before alpha handoff or when a governance readback appears missing");
        yield return BuildStep(
            "default_workflow_proof",
            WorkflowProofCommand,
            "runtime-default-workflow-proof",
            "handoff_audit",
            required: false,
            "covered",
            "use to prove the default path remains short and wired");
        yield return BuildStep(
            "blocked_problem_report",
            ProblemReportCommand,
            "runtime-agent-problem-intake",
            "blocked_path",
            required: false,
            "covered",
            "use when the selected next command fails or reports a blocker");
    }

    private IEnumerable<RuntimeDefaultWorkflowProofCheck> BuildChecks(
        RuntimeAgentThreadStartSurface threadStart,
        RuntimeAgentShortContextSurface shortContext,
        RuntimeMarkdownReadPathBudgetSurface markdownBudget,
        RuntimeGovernanceSurfaceCoverageAuditSurface governanceCoverage,
        RuntimeExternalConsumerResourcePackSurface resourcePack,
        string quickstart,
        string consumerPack)
    {
        yield return BuildCheck(
            "first_thread_command_is_single_entry",
            string.Equals(threadStart.OneCommandForNewThread, FirstThreadCommand, StringComparison.Ordinal),
            blocking: true,
            $"first command is {threadStart.OneCommandForNewThread}");
        yield return BuildCheck(
            "next_governed_command_selected",
            !string.IsNullOrWhiteSpace(threadStart.NextGovernedCommand),
            blocking: true,
            $"next_governed_command={NormalizeCommand(threadStart.NextGovernedCommand)}");
        yield return BuildCheck(
            "short_context_ready",
            shortContext.ShortContextReady,
            blocking: true,
            $"short_context_ready={shortContext.ShortContextReady}");
        yield return BuildCheck(
            "short_context_contains_warm_command",
            shortContext.PrimaryCommands.Any(command => string.Equals(command.Command, WarmContextCommand, StringComparison.Ordinal)),
            blocking: true,
            "short-context primary commands include carves agent context --json");
        yield return BuildCheck(
            "markdown_default_read_path_zero_tokens",
            markdownBudget.PostInitializationDefault.EstimatedDefaultMarkdownTokens == 0 && markdownBudget.PostInitializationDefault.WithinBudget,
            blocking: true,
            $"post-init markdown tokens={markdownBudget.PostInitializationDefault.EstimatedDefaultMarkdownTokens}/{markdownBudget.PostInitializationDefault.MaxDefaultMarkdownTokens}");
        yield return BuildCheck(
            "generated_markdown_views_deferred",
            markdownBudget.GeneratedMarkdownViews.DeferredMarkdownTokens > 0
            || markdownBudget.DeferredMarkdownSources.Count > 0
            || markdownBudget.DefaultMachineReadPath.Contains(WarmContextCommand, StringComparer.Ordinal),
            blocking: true,
            $"deferred generated markdown tokens={markdownBudget.GeneratedMarkdownViews.DeferredMarkdownTokens}");
        yield return BuildCheck(
            "governance_surface_coverage_complete",
            governanceCoverage.CoverageComplete,
            blocking: true,
            $"coverage={governanceCoverage.CoveredSurfaceCount}/{governanceCoverage.RequiredSurfaceCount}; gaps={governanceCoverage.BlockingGapCount}");
        yield return BuildCheck(
            "resource_pack_covers_default_commands",
            ResourcePackContains(resourcePack, FirstThreadCommand)
            && ResourcePackContains(resourcePack, WarmContextCommand)
            && ResourcePackContains(resourcePack, MarkdownBudgetCommand)
            && ResourcePackContains(resourcePack, GovernanceCoverageCommand)
            && ResourcePackContains(resourcePack, WorkflowProofCommand),
            blocking: true,
            "resource-pack command entries include start, context, Markdown budget, coverage audit, and default workflow proof");
        yield return BuildCheck(
            "quickstart_documents_default_workflow",
            TextContainsAll(quickstart, [FirstThreadCommand, WarmContextCommand, "runtime-default-workflow-proof"]),
            blocking: true,
            "external quickstart documents first command, short context, and default workflow proof");
        yield return BuildCheck(
            "consumer_pack_documents_default_workflow",
            TextContainsAll(consumerPack, [FirstThreadCommand, WarmContextCommand, "runtime-default-workflow-proof"]),
            blocking: true,
            "external consumer resource pack documents the default workflow proof");
        yield return BuildCheck(
            "troubleshooting_not_default_path",
            threadStart.TroubleshootingReadbacks.Count > 0 && threadStart.MinimalAgentRules.Any(rule => rule.Contains("troubleshooting readbacks only", StringComparison.OrdinalIgnoreCase)),
            blocking: true,
            "troubleshooting readbacks remain conditional rather than the default command chain");
        yield return BuildCheck(
            "current_runtime_ready_status_reported_separately",
            true,
            blocking: false,
            $"current_runtime_ready={threadStart.ThreadStartReady}; current gaps={threadStart.Gaps.Count}");
    }

    private static RuntimeDefaultWorkflowProofStep BuildStep(
        string stepId,
        string command,
        string surfaceId,
        string lane,
        bool required,
        string status,
        string evidence)
    {
        return new RuntimeDefaultWorkflowProofStep
        {
            StepId = stepId,
            Command = command,
            SurfaceId = string.IsNullOrWhiteSpace(surfaceId) ? "N/A" : surfaceId,
            Lane = lane,
            RequiredInDefaultPath = required,
            Status = status,
            Evidence = evidence,
        };
    }

    private static RuntimeDefaultWorkflowProofCheck BuildCheck(string checkId, bool passed, bool blocking, string summary)
    {
        return new RuntimeDefaultWorkflowProofCheck
        {
            CheckId = checkId,
            Passed = passed,
            Blocking = blocking,
            Summary = summary,
        };
    }

    private static string ResolvePosture(bool proofComplete, bool currentRuntimeReady)
    {
        if (!proofComplete)
        {
            return "default_workflow_proof_blocked_by_structural_gaps";
        }

        return currentRuntimeReady
            ? "default_workflow_proof_ready"
            : "default_workflow_proof_ready_current_repo_blocked";
    }

    private static string BuildSummary(
        bool proofComplete,
        RuntimeAgentThreadStartSurface threadStart,
        RuntimeMarkdownReadPathBudgetSurface markdownBudget)
    {
        if (!proofComplete)
        {
            return "Default workflow proof has structural gaps in command wiring, compact readback, documentation, resource pack, or coverage evidence.";
        }

        return $"Default workflow proof is complete: first-thread path is `{FirstThreadCommand}` -> `{NormalizeCommand(threadStart.NextGovernedCommand)}`, warm path is `{WarmContextCommand}` -> the same next governed command, and post-init Markdown default remains {markdownBudget.PostInitializationDefault.EstimatedDefaultMarkdownTokens} tokens.";
    }

    private static string BuildRecommendedNextAction(RuntimeAgentThreadStartSurface threadStart)
    {
        if (threadStart.ThreadStartReady)
        {
            return $"Run `{FirstThreadCommand}` in a new thread, then follow next_governed_command exactly: {NormalizeCommand(threadStart.NextGovernedCommand)}.";
        }

        return $"Workflow proof is structurally complete, but the current repo state is not ready; follow the current next governed command or blockers from `carves agent start --json`: {NormalizeCommand(threadStart.NextGovernedCommand)}.";
    }

    private static bool ResourcePackContains(RuntimeExternalConsumerResourcePackSurface resourcePack, string command)
    {
        return resourcePack.CommandEntries.Any(entry =>
            entry.Command.Contains(command, StringComparison.Ordinal)
            || command.Contains(entry.Command.Replace(" <query>", string.Empty), StringComparison.Ordinal));
    }

    private static bool TextContainsAll(string text, IReadOnlyList<string> needles)
    {
        return needles.All(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private string ReadRepoFile(string repoRelativePath)
    {
        var path = Path.Combine(repoRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static string NormalizeCommand(string? command)
    {
        return string.IsNullOrWhiteSpace(command) ? "N/A" : command;
    }
}

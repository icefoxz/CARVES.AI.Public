using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAgentProblemIntakeService
{
    public const string ProblemIntakeGuideDocumentPath = "docs/guides/CARVES_AGENT_PROBLEM_INTAKE.md";

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;
    private readonly Func<RuntimeExternalTargetPilotStartSurface> pilotStartFactory;
    private readonly Func<RuntimeExternalTargetPilotNextSurface> pilotNextFactory;
    private readonly Func<IReadOnlyList<PilotProblemIntakeRecord>> recentProblemsFactory;

    public RuntimeAgentProblemIntakeService(
        string repoRoot,
        Func<RuntimeExternalTargetPilotStartSurface> pilotStartFactory,
        Func<RuntimeExternalTargetPilotNextSurface> pilotNextFactory,
        Func<IReadOnlyList<PilotProblemIntakeRecord>> recentProblemsFactory)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
        this.pilotStartFactory = pilotStartFactory;
        this.pilotNextFactory = pilotNextFactory;
        this.recentProblemsFactory = recentProblemsFactory;
    }

    public RuntimeAgentProblemIntakeSurface Build()
    {
        var errors = ValidateBaseDocuments();
        var pilotStart = pilotStartFactory();
        var pilotNext = pilotNextFactory();
        var recentProblems = recentProblemsFactory()
            .OrderByDescending(record => record.RecordedAtUtc)
            .Take(10)
            .Select(RuntimeAgentProblemIntakeRecentProblemSurface.FromRecord)
            .ToArray();
        var dependencyErrors = pilotStart.Errors
            .Select(static error => $"runtime-external-target-pilot-start:{error}")
            .Concat(pilotNext.Errors.Select(static error => $"runtime-external-target-pilot-next:{error}"))
            .ToArray();
        var contractAvailable = errors.Count == 0;
        var ready = contractAvailable && dependencyErrors.Length == 0 && pilotStart.IsValid && pilotNext.IsValid;
        var gaps = errors
            .Select(static error => $"agent_problem_intake_resource:{error}")
            .Concat(dependencyErrors.Select(static error => $"agent_problem_intake_dependency:{error}"))
            .ToArray();

        return new RuntimeAgentProblemIntakeSurface
        {
            ProblemIntakeGuideDocumentPath = ProblemIntakeGuideDocumentPath,
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = ready
                ? "agent_problem_intake_ready"
                : "agent_problem_intake_blocked",
            ProblemIntakeReady = ready,
            PilotStartBundleReady = pilotStart.PilotStartBundleReady,
            ReadyToRunNextCommand = pilotNext.ReadyToRunNextCommand,
            CurrentStageId = pilotNext.CurrentStageId,
            CurrentStageOrder = pilotNext.CurrentStageOrder,
            CurrentStageStatus = pilotNext.CurrentStageStatus,
            NextGovernedCommand = pilotNext.NextGovernedCommand,
            AcceptedProblemKinds = BuildAcceptedProblemKinds(),
            RequiredPayloadFields = BuildRequiredPayloadFields(),
            OptionalPayloadFields = BuildOptionalPayloadFields(),
            PayloadRules = BuildPayloadRules(),
            StopAndReportTriggers = BuildStopAndReportTriggers(),
            CommandExamples = BuildCommandExamples(),
            RecentProblemCount = recentProblems.Length,
            RecentProblems = recentProblems,
            Gaps = gaps,
            Summary = ready
                ? "Agent problem intake is ready: an external agent can stop on a surfaced CARVES blocker and submit a structured report without mutating planning, task, review, or protected truth roots."
                : "Agent problem intake is blocked until its Runtime-owned docs and start/next dependencies are valid.",
            RecommendedNextAction = ready
                ? "When a stop-and-report trigger fires, write a problem payload outside protected truth roots, then run carves pilot report-problem <json-path> --json."
                : "Restore the listed problem-intake gaps, then rerun carves pilot problem-intake --json.",
            IsValid = contractAvailable,
            Errors = [.. errors, .. dependencyErrors],
            NonClaims = BuildNonClaims(),
        };
    }

    private List<string> ValidateBaseDocuments()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(RuntimeProductClosureMetadata.CurrentDocumentPath, RuntimeProductClosureMetadata.CurrentDocumentLabel, errors);
        ValidateRuntimeDocument(RuntimeProductClosureMetadata.PreviousDocumentPath, "Product closure previous phase document", errors);
        ValidateRuntimeDocument(ProblemIntakeGuideDocumentPath, "Agent problem intake guide document", errors);
        ValidateRuntimeDocument(RuntimeExternalTargetPilotStartService.QuickstartGuideDocumentPath, "External agent quickstart guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosurePilotGuideService.GuideDocumentPath, "Productized pilot guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosurePilotStatusService.GuideDocumentPath, "Productized pilot status document", errors);
        return errors;
    }

    private static string[] BuildAcceptedProblemKinds()
    {
        return
        [
            "command_failed",
            "blocked_posture",
            "protected_truth_root_requested",
            "missing_acceptance_contract",
            "workspace_scope_ambiguous",
            "next_command_ambiguous",
            "runtime_binding_ambiguous",
            "agent_policy_conflict",
            "other",
        ];
    }

    private static string[] BuildRequiredPayloadFields()
    {
        return
        [
            "summary",
            "problem_kind",
        ];
    }

    private static string[] BuildOptionalPayloadFields()
    {
        return
        [
            "repo_id",
            "task_id",
            "card_id",
            "current_stage_id",
            "next_governed_command",
            "severity",
            "blocked_command",
            "command_exit_code",
            "command_output",
            "stop_trigger",
            "observations",
            "affected_paths",
            "recommended_follow_up",
        ];
    }

    private static string[] BuildPayloadRules()
    {
        return
        [
            "Payloads are JSON and are submitted by path; do not paste unbounded logs into chat when a file can hold the evidence.",
            "Use problem_kind to classify the blocker; use summary for the human-readable explanation.",
            "Put command output in command_output only when it is relevant and bounded.",
            "Use affected_paths for paths the agent wanted to touch or could not safely classify.",
            "Do not edit .ai/tasks/, .ai/memory/, .ai/artifacts/reviews/, or .carves-platform/ directly to create a problem report.",
        ];
    }

    private static string[] BuildStopAndReportTriggers()
    {
        return
        [
            "next_governed_command is empty, contradictory, or conflicts with the user's requested scope",
            "a CARVES command fails or returns a blocked posture",
            "the agent wants to modify a protected truth root directly",
            "the agent cannot find an acceptance contract for executable work",
            "the agent is about to edit files outside a managed workspace lease or declared writable path",
            "runtime root, dist binding, or target attach state is ambiguous",
            "the agent is tempted to rationalize a CARVES warning instead of following the surfaced next action",
        ];
    }

    private static string[] BuildCommandExamples()
    {
        return
        [
            "carves pilot problem-intake --json",
            "carves pilot report-problem .carves-agent/problem-intake.json --json",
            "carves pilot list-problems",
            "carves pilot inspect-problem <problem-id>",
            "carves pilot triage --json",
            "carves pilot follow-up --json",
            "carves pilot follow-up-plan --json",
            "carves pilot follow-up-record --json",
        ];
    }

    private static string[] BuildNonClaims()
    {
        return
        [
            "This surface is not a planner and does not create cards, tasks, acceptance contracts, reviews, writebacks, commits, or ignore decisions.",
            "The report-problem command records problem intake and pilot evidence; it does not authorize the blocked change.",
            "Problem intake records are target runtime evidence, not Runtime-owned doctrine or product truth.",
            "This surface does not claim OS sandboxing, full ACP, full MCP, remote worker orchestration, or automatic triage.",
        ];
    }

    private void ValidateRuntimeDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }
}

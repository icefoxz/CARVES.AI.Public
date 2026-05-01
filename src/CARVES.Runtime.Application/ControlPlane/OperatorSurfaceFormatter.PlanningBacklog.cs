using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.Refactoring;
using Carves.Runtime.Domain.Cards;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Safety;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult PlanPreview(
        CardDefinition card,
        CodeGraphScopeAnalysis scopeAnalysis,
        CodeGraphImpactAnalysis impactAnalysis)
    {
        var lines = new List<string>
        {
            $"Card: {card.CardId}",
            $"Title: {card.Title}",
            $"Priority: {card.Priority}",
            $"Scope entries: {card.Scope.Count}",
            $"Acceptance entries: {card.Acceptance.Count}",
        };

        if (scopeAnalysis.HasMatches)
        {
            lines.Add($"CodeGraph modules: {scopeAnalysis.Modules.Count}");
            lines.Add($"CodeGraph files: {scopeAnalysis.Files.Count}");
            lines.Add($"CodeGraph callables: {scopeAnalysis.Callables.Count}");
        }

        if (impactAnalysis.HasMatches)
        {
            lines.Add($"CodeGraph impacted modules: {impactAnalysis.ImpactedModules.Count}");
            lines.Add($"CodeGraph impacted files: {impactAnalysis.ImpactedFiles.Count}");
        }

        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult RunNext(CycleResult result)
    {
        var lines = new List<string> { result.Message };
        if (result.ScheduleDecision is not null)
        {
            lines.Add($"Scheduler: {result.ScheduleDecision.Kind} ({result.ScheduleDecision.Reason})");
            if (result.ScheduleDecision.BlockedTasks.Count > 0)
            {
                lines.AddRange(result.ScheduleDecision.BlockedTasks.Take(5).Select(block => $"Blocked: {block.TaskId} ({block.Reason})"));
            }
        }

        if (result.PlannerReentry is not null)
        {
            lines.Add($"Planner re-entry: {result.PlannerReentry.Outcome} ({result.PlannerReentry.Reason})");
            lines.Add($"Planner round: {result.PlannerReentry.PlannerRound}");
            lines.Add($"Opportunities detected/evaluated: {result.PlannerReentry.DetectedOpportunityCount}/{result.PlannerReentry.EvaluatedOpportunityCount}");
            lines.Add($"Opportunity sources: {result.PlannerReentry.OpportunitySourceSummary}");
            if (result.PlannerReentry.ProposedTaskIds.Count > 0)
            {
                lines.Add($"Planner re-entry tasks: {string.Join(", ", result.PlannerReentry.ProposedTaskIds)}");
            }
        }

        if (result.Reports.Count > 0)
        {
            lines.Add($"Tasks: {string.Join(", ", result.Reports.Select(report => report.TaskId))}");
            lines.AddRange(result.Reports.Select(report => $"Task {report.TaskId}: validation={report.Validation.Passed}; safety={report.SafetyDecision.Allowed}; mode={report.SafetyDecision.ValidationMode}"));
        }

        if (result.Session is not null)
        {
            lines.Add($"Session status: {result.Session.Status}");
            lines.Add($"Planner state: {PlannerLifecycleSemantics.DescribeState(result.Session.PlannerLifecycleState)}");
            lines.Add($"Planner lifecycle reason: {result.Session.PlannerLifecycleReason ?? "(none)"}");
            if (result.Session.Status == RuntimeSessionStatus.ApprovalWait)
            {
                lines.Add($"Pending permissions: {(result.Session.PendingPermissionRequestIds.Count == 0 ? "(none)" : string.Join(", ", result.Session.PendingPermissionRequestIds))}");
                lines.Add($"Permission summary: {result.Session.LastPermissionSummary ?? "(none)"}");
            }
        }

        var hasActionableWorkerFailure = result.Reports.Any(report =>
            report.WorkerExecution.Status is Carves.Runtime.Domain.Execution.WorkerExecutionStatus.Failed
                or Carves.Runtime.Domain.Execution.WorkerExecutionStatus.Blocked
                or Carves.Runtime.Domain.Execution.WorkerExecutionStatus.TimedOut
                or Carves.Runtime.Domain.Execution.WorkerExecutionStatus.Cancelled
                or Carves.Runtime.Domain.Execution.WorkerExecutionStatus.Aborted
                or Carves.Runtime.Domain.Execution.WorkerExecutionStatus.ApprovalWait);
        var exitCode = result.Session?.Status == RuntimeSessionStatus.Failed || hasActionableWorkerFailure ? 1 : 0;
        return new OperatorCommandResult(exitCode, lines);
    }

    public static OperatorCommandResult SessionStatus(RuntimeSessionState? session)
    {
        return OperatorRuntimeStatusFormatter.SessionStatus(session);
    }

    public static OperatorCommandResult SessionChanged(string action, RuntimeSessionState session)
    {
        return OperatorRuntimeStatusFormatter.SessionChanged(action, session);
    }

    public static OperatorCommandResult ContinuousLoop(ContinuousLoopResult result)
    {
        return OperatorRuntimeStatusFormatter.ContinuousLoop(result);
    }

    public static OperatorCommandResult PlannerStatus(RuntimeSessionState? session)
    {
        return OperatorRuntimeStatusFormatter.PlannerStatus(session);
    }

    public static OperatorCommandResult PlannerRun(PlannerHostResult result)
    {
        return OperatorRuntimeStatusFormatter.PlannerRun(result);
    }

    public static OperatorCommandResult PlannerLoop(PlannerHostLoopResult result)
    {
        return OperatorRuntimeStatusFormatter.PlannerLoop(result);
    }

    public static OperatorCommandResult PlannerLifecycleChanged(string action, RuntimeSessionState session)
    {
        return OperatorRuntimeStatusFormatter.PlannerLifecycleChanged(action, session);
    }

    public static OperatorCommandResult ScanCode(CodeGraphBuildResult result)
    {
        return OperatorCommandResult.Success(
            $"CodeGraph built: {result.Graph.Nodes.Count} nodes",
            $"Modules indexed: {result.Index.Modules.Count}",
            $"Files indexed: {result.Index.Files.Count}",
            $"Callables indexed: {result.Index.Callables.Count}",
            $"Manifest: {result.OutputPath}",
            $"Index: {result.IndexPath}");
    }

    public static OperatorCommandResult SafetyCheck(CarvesCodeStandard carvesCodeStandard, SafetyRules rules, IReadOnlyList<SafetyViolation> violations)
    {
        var lines = new List<string>
        {
            "CARVES code standard:",
            $"- version: {carvesCodeStandard.Version}",
            $"- core loop: {carvesCodeStandard.CoreLoop}",
            $"- interaction loop: {carvesCodeStandard.InteractionLoop}",
            $"- recorder writable by: {string.Join(", ", carvesCodeStandard.Authority.RecorderWritableBy)}",
            $"- domain events emitted by: {string.Join(", ", carvesCodeStandard.Authority.DomainEventsEmittedBy)}",
            $"- forbidden edges tracked: {carvesCodeStandard.ForbiddenEdges.Count}",
            $"- directory mirroring required: {carvesCodeStandard.Applicability.DirectoryLayoutRequired}",
            $"- refactor for purity alone forbidden: {carvesCodeStandard.Applicability.RefactorForPurityAloneForbidden}",
            $"- AI-friendly line target: {carvesCodeStandard.AiFriendlyArchitecture.RecommendedFileLinesLowerBound}-{carvesCodeStandard.AiFriendlyArchitecture.RecommendedFileLinesUpperBound}",
            $"- AI-friendly refactor threshold: {carvesCodeStandard.AiFriendlyArchitecture.RefactorFileLinesThreshold}",
            $"- physical split heuristic: can split at {carvesCodeStandard.PhysicalSplitting.SplitScoreCanSplit}, should split at {carvesCodeStandard.PhysicalSplitting.SplitScoreShouldSplit}",
            $"- naming grammar: {carvesCodeStandard.ExtremeNaming.NamingGrammar}",
            $"- one concept one headword: {carvesCodeStandard.ExtremeNaming.OneConceptOneHeadword}",
            $"- canonical vocabulary required: {carvesCodeStandard.ExtremeNaming.CanonicalVocabularyRequired}",
            $"- forbidden generic words: {carvesCodeStandard.ExtremeNaming.ForbiddenGenericWords.Count}",
            $"- suggested analyzer rules: {carvesCodeStandard.ExtremeNaming.SuggestedAnalyzerRules.Count}",
            $"- dependency one-way: {carvesCodeStandard.DependencyContract.DependencyDirectionOneWay}",
            $"- recorder access model: {carvesCodeStandard.DependencyContract.RecorderAccessModel}",
            $"- dependency diagnostics: forbidden={carvesCodeStandard.DependencyContract.ForbiddenDiagnosticRules.Count}; restricted={carvesCodeStandard.DependencyContract.RestrictedDiagnosticRules.Count}; advisory={carvesCodeStandard.DependencyContract.AdvisoryDiagnosticRules.Count}",
            "Safety baseline:",
            $"- Restricted paths: {string.Join(", ", rules.RestrictedPaths)}",
            $"- Worker writable paths: {string.Join(", ", rules.WorkerWritablePaths)}",
            $"- Tests required for source changes: {rules.RequireTestsForSourceChanges}",
        };

        lines.AddRange(violations.Count == 0
            ? ["No baseline issues detected."]
            : violations.Select(violation => $"- {violation.Code}: {violation.Message}"));

        lines.Add("CARVES review questions:");
        lines.AddRange(carvesCodeStandard.ReviewQuestions.Take(3).Select(question => $"- {question}"));
        lines.Add("CARVES moderation rules:");
        lines.AddRange(carvesCodeStandard.ModerationRules.Take(3).Select(rule => $"- {rule}"));
        lines.Add("CARVES runtime questions:");
        lines.AddRange(carvesCodeStandard.RuntimeQuestions.Take(2).Select(question => $"- {question}"));

        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult DetectRefactors(RefactoringBacklogSnapshot backlog)
    {
        var lines = new List<string> { $"Refactoring backlog items: {backlog.Items.Count}" };
        lines.AddRange(backlog.Items.Select(item => $"- {item.ItemId}: {item.Kind} in {item.Path} [{item.Status}]"));
        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult MaterializeRefactors(RefactoringTaskMaterializationResult result)
    {
        if (result.DeferredForHigherPriorityWork)
        {
            var deferredLines = new List<string>
            {
                $"Refactoring task materialization deferred for {result.DeferredBacklogItemIds.Count} backlog items because higher-priority work is active.",
                $"Governed hotspot queues: {result.QueueIds.Count}",
            };
            deferredLines.AddRange(result.QueuePaths.Select(path => $"- {path}"));
            return new OperatorCommandResult(0, deferredLines);
        }

        var lines = new List<string>
        {
            $"Suggested refactoring tasks: {result.SuggestedTaskIds.Count}",
            $"Governed hotspot queues: {result.QueueIds.Count}",
        };
        lines.AddRange(result.SuggestedTaskIds.Select(taskId => $"- {taskId}"));
        lines.AddRange(result.QueuePaths.Select(path => $"- queue: {path}"));
        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult DetectOpportunities(OpportunityDetectionResult result)
    {
        var lines = new List<string>
        {
            $"Detected opportunities: {result.TotalDetected}",
            $"Open opportunities: {result.OpenCount}",
        };
        lines.AddRange(result.Contributions.Select(contribution =>
            $"- {contribution.DetectorName}: {(contribution.OpportunityIds.Count == 0 ? "(none)" : string.Join(", ", contribution.OpportunityIds))}"));
        return new OperatorCommandResult(0, lines);
    }
}

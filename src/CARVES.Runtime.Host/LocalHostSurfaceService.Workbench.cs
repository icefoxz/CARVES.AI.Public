using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostSurfaceService
{
    public JsonObject BuildWorkbenchOverview()
    {
        return ToJsonObject(services.WorkbenchSurfaceService.BuildOverview());
    }

    public JsonObject BuildWorkbenchCard(string cardId)
    {
        return ToJsonObject(services.WorkbenchSurfaceService.BuildCard(cardId));
    }

    public JsonObject BuildWorkbenchTask(string taskId)
    {
        return ToJsonObject(services.WorkbenchSurfaceService.BuildTask(taskId));
    }

    public JsonObject BuildWorkbenchReview()
    {
        return ToJsonObject(services.WorkbenchSurfaceService.BuildReview());
    }

    public IReadOnlyList<string> RenderWorkbenchTextOverview()
    {
        var overview = services.WorkbenchSurfaceService.BuildOverview();
        var lines = new List<string>
        {
            "CARVES Workbench",
            "Surface: overview",
            $"Repo: {overview.RepoId}",
            $"Summary: {overview.Summary}",
            $"Session: {overview.SessionStatus}",
            $"Host control: {overview.HostControlState}",
            $"Actionability: {overview.Actionability}",
            $"Agent working mode: {overview.CurrentMode}",
            $"External agent recommended mode: {overview.ExternalAgentRecommendedMode}",
            $"External agent recommendation posture: {overview.ExternalAgentRecommendationPosture}",
            $"External agent constraint tier: {overview.ExternalAgentConstraintTier}",
            $"External agent stronger-mode blockers: {overview.ExternalAgentStrongerModeBlockerCount}",
            $"External agent first stronger-mode blocker: {overview.ExternalAgentFirstStrongerModeBlockerId ?? "(none)"}",
            $"External agent first stronger-mode blocker action: {overview.ExternalAgentFirstStrongerModeBlockerRequiredAction ?? "(none)"}",
            $"Mode E activation: {overview.ModeEOperationalActivationState}",
            $"Mode E activation task: {overview.ModeEActivationTaskId ?? "(none)"}",
            $"Mode E activation command: {overview.ModeEActivationCommands.FirstOrDefault() ?? "(none)"}",
            $"Mode E result return channel: {overview.ModeEActivationResultReturnChannel ?? "(none)"}",
            $"Mode E activation next action: {overview.ModeEActivationRecommendedNextAction}",
            $"Mode E activation blocking checks: {overview.ModeEActivationBlockingCheckCount}",
            $"Mode E activation first blocker: {overview.ModeEActivationFirstBlockingCheckId ?? "(none)"}",
            $"Mode E activation first blocker action: {overview.ModeEActivationFirstBlockingCheckRequiredAction ?? "(none)"}",
            $"Mode E activation playbook: {overview.ModeEActivationPlaybookSummary}",
            $"Mode E activation playbook steps: {overview.ModeEActivationPlaybookStepCount}",
            $"Mode E activation first playbook command: {overview.ModeEActivationFirstPlaybookStepCommand ?? "(none)"}",
            $"Planning coupling: {overview.PlanningCouplingPosture}",
            $"Formal planning posture: {overview.FormalPlanningPosture}",
            $"Formal planning entry trigger: {overview.FormalPlanningEntryTriggerState}",
            $"Formal planning entry command: {overview.FormalPlanningEntryCommand}",
            $"Formal planning entry next action: {overview.FormalPlanningEntryRecommendedNextAction}",
            $"Formal planning entry summary: {overview.FormalPlanningEntrySummary}",
            $"Active planning slot state: {overview.ActivePlanningSlotState}",
            $"Active planning slot conflict reason: {(string.IsNullOrWhiteSpace(overview.ActivePlanningSlotConflictReason) ? "(none)" : overview.ActivePlanningSlotConflictReason)}",
            $"Active planning slot remediation: {overview.ActivePlanningSlotRemediationAction}",
            $"Planning card invariant state: {overview.PlanningCardInvariantState}",
            $"Planning card invariant can export: {overview.PlanningCardInvariantCanExportGovernedTruth}",
            $"Planning card invariant violations: {overview.PlanningCardInvariantViolationCount}",
            $"Planning card invariant remediation: {overview.PlanningCardInvariantRemediationAction}",
            $"Active planning card fill state: {overview.ActivePlanningCardFillState}",
            $"Active planning card fill completion: {overview.ActivePlanningCardFillCompletionPosture}",
            $"Active planning card fill missing required fields: {overview.ActivePlanningCardFillMissingRequiredFieldCount}",
            $"Active planning card fill next missing field: {overview.ActivePlanningCardFillNextMissingFieldPath ?? "(none)"}",
            $"Active planning card fill next action: {overview.ActivePlanningCardFillRecommendedNextAction}",
            $"Active plan handle: {overview.PlanHandle ?? "(none)"}",
            $"Active planning card: {overview.PlanningCardId ?? "(none)"}",
            $"Managed workspace posture: {overview.ManagedWorkspacePosture}",
            $"Vendor-native acceleration: {overview.VendorNativeAccelerationPosture}",
            $"Codex reinforcement: {overview.CodexReinforcementState}",
            $"Claude reinforcement: {overview.ClaudeReinforcementState}",
            $"Current card: {overview.CurrentCardId ?? "(none)"}",
            $"Current task: {overview.CurrentTaskId ?? "(none)"}",
            $"Next task: {overview.NextTaskId ?? "(none)"}",
            $"Ready/Running/Review/Blocked: {overview.ReadyTaskCount}/{overview.RunningTaskCount}/{overview.ReviewTaskCount}/{overview.BlockedTaskCount}",
            $"Acceptance contract gaps: {overview.AcceptanceContractGapCount}",
            $"Plan-required execution gaps: {overview.PlanRequiredBlockCount}",
            $"Managed workspace gaps: {overview.WorkspaceRequiredBlockCount}",
            $"Mode C/D entry first blocker: {overview.ModeExecutionEntryFirstBlockingCheckId ?? "(none)"}",
            $"Mode C/D entry first blocker command: {overview.ModeExecutionEntryFirstBlockingCheckRequiredCommand ?? "(none)"}",
            $"Mode C/D entry next command: {overview.ModeExecutionEntryRecommendedNextCommand ?? "(none)"}",
            $"Pending approvals: {overview.PendingApprovalCount}",
        };

        if (!string.IsNullOrWhiteSpace(overview.WaitingReason))
        {
            lines.Add($"Waiting reason: {overview.WaitingReason}");
        }

        lines.Add("Focus tasks:");
        lines.AddRange(overview.FocusTasks.Count == 0
            ? ["  - (none)"]
            : overview.FocusTasks.Select(item => $"  - {item.TaskId} [{item.Status}] {item.Title} | next={item.NextAction} | blocked={item.BlockedReason}"));
        lines.Add("Available actions:");
        lines.AddRange(overview.AvailableActions.Count == 0
            ? ["  - (none)"]
            : overview.AvailableActions.Select(FormatActionLine));
        var acceptedOperations = ListRecentAcceptedOperations();
        lines.Add("Accepted operations:");
        lines.AddRange(acceptedOperations.Count == 0
            ? ["  - (none)"]
            : acceptedOperations.Select(item => $"  - {item.Command} [{item.ProgressMarker}] completed={item.Completed} | state={item.OperationState} | updated={item.UpdatedAt:O}"));
        return lines;
    }

    public IReadOnlyList<string> RenderWorkbenchTextCard(string cardId)
    {
        var card = services.WorkbenchSurfaceService.BuildCard(cardId);
        var lines = new List<string>
        {
            "CARVES Workbench",
            $"Surface: card {card.CardId}",
            $"Title: {card.Title}",
            $"Goal: {card.Goal}",
            $"Status: {card.Status}",
            $"Lifecycle: {card.LifecycleState}",
            $"Summary: {card.Summary}",
            $"Reality: {card.Reality.Status} | {card.Reality.Summary}",
            $"Blocked reason: {card.BlockedReason}",
            $"Next action: {card.NextAction}",
            "Tasks:",
        };
        lines.AddRange(card.Tasks.Count == 0
            ? ["  - (none)"]
            : card.Tasks.Select(item => $"  - {item.TaskId} [{item.Status}] {item.Title} | next={item.NextAction} | blocked={item.BlockedReason}"));
        lines.Add("Available actions:");
        lines.AddRange(card.AvailableActions.Count == 0
            ? ["  - (none)"]
            : card.AvailableActions.Select(FormatActionLine));
        return lines;
    }

    public IReadOnlyList<string> RenderWorkbenchTextTask(string taskId)
    {
        var task = services.WorkbenchSurfaceService.BuildTask(taskId);
        var lines = new List<string>
        {
            "CARVES Workbench",
            $"Surface: task {task.TaskId}",
            $"Card: {task.CardId}",
            $"Title: {task.Title}",
            $"Status: {task.Status}",
            $"Summary: {task.Summary}",
            $"Reality: {task.Reality.Status} | {task.Reality.Summary}",
            $"Blocked reason: {task.BlockedReason}",
            $"Next action: {task.NextAction}",
            $"Dependencies: {(task.Dependencies.Count == 0 ? "(none)" : string.Join(", ", task.Dependencies))}",
            $"Unresolved dependencies: {(task.UnresolvedDependencies.Count == 0 ? "(none)" : string.Join(", ", task.UnresolvedDependencies))}",
        };

        if (!string.Equals(task.ReviewEvidence.Status, "unavailable", StringComparison.Ordinal))
        {
            AddReviewEvidenceTextLines(lines, task.ReviewEvidence);
        }

        if (task.ExecutionRun is not null)
        {
            lines.Add($"Execution run: {task.ExecutionRun.RunId} [{task.ExecutionRun.Status}] step={task.ExecutionRun.CurrentStepTitle}");
        }

        lines.Add("Artifacts:");
        lines.AddRange(task.Artifacts.Count == 0
            ? ["  - (none)"]
            : task.Artifacts.Select(item => $"  - {item.Label}: {item.Path}"));
        lines.Add("Related tasks:");
        lines.AddRange(task.RelatedTasks.Count == 0
            ? ["  - (none)"]
            : task.RelatedTasks.Select(item => $"  - {item.TaskId} [{item.Status}] {item.Title}"));
        lines.Add("Available actions:");
        lines.AddRange(task.AvailableActions.Count == 0
            ? ["  - (none)"]
            : task.AvailableActions.Select(FormatActionLine));
        return lines;
    }

    public IReadOnlyList<string> RenderWorkbenchTextReview()
    {
        var review = services.WorkbenchSurfaceService.BuildReview();
        var lines = new List<string>
        {
            "CARVES Workbench",
            "Surface: review",
            $"Summary: {review.Summary}",
            "Review queue:",
        };
        lines.AddRange(review.ReviewQueue.Count == 0
            ? ["  - (none)"]
            : review.ReviewQueue.SelectMany(FormatReviewQueueText));
        lines.Add("Task actions:");
        lines.AddRange(review.TaskActionQueue.Count == 0
            ? ["  - (none)"]
            : review.TaskActionQueue.SelectMany(FormatReviewQueueText));
        lines.Add("Global actions:");
        lines.AddRange(review.GlobalActions.Count == 0
            ? ["  - (none)"]
            : review.GlobalActions.Select(FormatActionLine));
        var acceptedOperations = ListRecentAcceptedOperations();
        lines.Add("Accepted operations:");
        lines.AddRange(acceptedOperations.Count == 0
            ? ["  - (none)"]
            : acceptedOperations.Select(item => $"  - {item.Command} [{item.ProgressMarker}] completed={item.Completed} | state={item.OperationState} | updated={item.UpdatedAt:O}"));
        return lines;
    }

    public string RenderWorkbenchOverviewHtml(LocalHostState hostState)
    {
        var overview = services.WorkbenchSurfaceService.BuildOverview();
        var review = services.WorkbenchSurfaceService.BuildReview();
        var card = !string.IsNullOrWhiteSpace(overview.CurrentCardId)
            ? services.WorkbenchSurfaceService.BuildCard(overview.CurrentCardId!)
            : null;
        return RenderWorkbenchHtml(
            hostState,
            "Overview",
            $"""
             <section class="hero">
               <div class="metric"><span class="label">Repo</span><span class="value">{Encode(overview.RepoId)}</span></div>
               <div class="metric"><span class="label">Current card</span><span class="value">{Encode(overview.CurrentCardId ?? "(none)")}</span></div>
               <div class="metric"><span class="label">Current task</span><span class="value">{Encode(overview.CurrentTaskId ?? "(none)")}</span></div>
               <div class="metric"><span class="label">Next task</span><span class="value">{Encode(overview.NextTaskId ?? "(none)")}</span></div>
               <div class="metric"><span class="label">Mode</span><span class="value">{Encode(overview.CurrentMode)}</span></div>
               <div class="metric"><span class="label">Recommended mode</span><span class="value">{Encode(overview.ExternalAgentRecommendedMode)}</span></div>
               <div class="metric"><span class="label">Actionability</span><span class="value">{Encode(overview.Actionability)}</span></div>
             </section>
             <section class="panel">
               <h2>Overview</h2>
               <p>{Encode(overview.Summary)}</p>
               <ul class="facts">
                 <li>Session: {Encode(overview.SessionStatus)}</li>
                 <li>Host control: {Encode(overview.HostControlState)}</li>
                 <li>External agent recommendation posture: {Encode(overview.ExternalAgentRecommendationPosture)}</li>
                 <li>External agent constraint tier: {Encode(overview.ExternalAgentConstraintTier)}</li>
                 <li>External agent stronger-mode blockers: {overview.ExternalAgentStrongerModeBlockerCount}</li>
                 <li>External agent first stronger-mode blocker: {Encode(overview.ExternalAgentFirstStrongerModeBlockerId ?? "(none)")}</li>
                 <li>External agent first stronger-mode blocker action: {Encode(overview.ExternalAgentFirstStrongerModeBlockerRequiredAction ?? "(none)")}</li>
                 <li>Mode E activation: {Encode(overview.ModeEOperationalActivationState)}</li>
                 <li>Mode E activation task: {Encode(overview.ModeEActivationTaskId ?? "(none)")}</li>
                 <li>Mode E activation command: {Encode(overview.ModeEActivationCommands.FirstOrDefault() ?? "(none)")}</li>
                 <li>Mode E result return channel: {Encode(overview.ModeEActivationResultReturnChannel ?? "(none)")}</li>
                 <li>Mode E activation next action: {Encode(overview.ModeEActivationRecommendedNextAction)}</li>
                 <li>Mode E activation blocking checks: {overview.ModeEActivationBlockingCheckCount}</li>
                 <li>Mode E activation first blocker: {Encode(overview.ModeEActivationFirstBlockingCheckId ?? "(none)")}</li>
                 <li>Mode E activation first blocker action: {Encode(overview.ModeEActivationFirstBlockingCheckRequiredAction ?? "(none)")}</li>
                 <li>Mode E activation playbook: {Encode(overview.ModeEActivationPlaybookSummary)}</li>
                 <li>Mode E activation playbook steps: {overview.ModeEActivationPlaybookStepCount}</li>
                 <li>Mode E activation first playbook command: {Encode(overview.ModeEActivationFirstPlaybookStepCommand ?? "(none)")}</li>
                 <li>Planning coupling: {Encode(overview.PlanningCouplingPosture)}</li>
                 <li>Formal planning posture: {Encode(overview.FormalPlanningPosture)}</li>
                 <li>Formal planning entry trigger: {Encode(overview.FormalPlanningEntryTriggerState)}</li>
                 <li>Formal planning entry command: {Encode(overview.FormalPlanningEntryCommand)}</li>
                 <li>Formal planning entry next action: {Encode(overview.FormalPlanningEntryRecommendedNextAction)}</li>
                 <li>Formal planning entry summary: {Encode(overview.FormalPlanningEntrySummary)}</li>
                 <li>Active planning slot state: {Encode(overview.ActivePlanningSlotState)}</li>
                 <li>Active planning slot conflict reason: {Encode(string.IsNullOrWhiteSpace(overview.ActivePlanningSlotConflictReason) ? "(none)" : overview.ActivePlanningSlotConflictReason)}</li>
                 <li>Active planning slot remediation: {Encode(overview.ActivePlanningSlotRemediationAction)}</li>
                 <li>Planning card invariant state: {Encode(overview.PlanningCardInvariantState)}</li>
                 <li>Planning card invariant can export: {Encode(overview.PlanningCardInvariantCanExportGovernedTruth.ToString())}</li>
                 <li>Planning card invariant violations: {overview.PlanningCardInvariantViolationCount}</li>
                 <li>Planning card invariant remediation: {Encode(overview.PlanningCardInvariantRemediationAction)}</li>
                 <li>Active planning card fill state: {Encode(overview.ActivePlanningCardFillState)}</li>
                 <li>Active planning card fill completion: {Encode(overview.ActivePlanningCardFillCompletionPosture)}</li>
                 <li>Active planning card fill missing required fields: {overview.ActivePlanningCardFillMissingRequiredFieldCount}</li>
                 <li>Active planning card fill next missing field: {Encode(overview.ActivePlanningCardFillNextMissingFieldPath ?? "(none)")}</li>
                 <li>Active planning card fill next action: {Encode(overview.ActivePlanningCardFillRecommendedNextAction)}</li>
                 <li>Active plan handle: {Encode(overview.PlanHandle ?? "(none)")}</li>
                 <li>Active planning card: {Encode(overview.PlanningCardId ?? "(none)")}</li>
                 <li>Managed workspace posture: {Encode(overview.ManagedWorkspacePosture)}</li>
                 <li>Vendor-native acceleration: {Encode(overview.VendorNativeAccelerationPosture)}</li>
                 <li>Codex reinforcement: {Encode(overview.CodexReinforcementState)}</li>
                 <li>Claude reinforcement: {Encode(overview.ClaudeReinforcementState)}</li>
                 <li>Ready / Running / Review / Blocked: {overview.ReadyTaskCount} / {overview.RunningTaskCount} / {overview.ReviewTaskCount} / {overview.BlockedTaskCount}</li>
                 <li>Acceptance contract gaps: {overview.AcceptanceContractGapCount}</li>
                 <li>Plan-required execution gaps: {overview.PlanRequiredBlockCount}</li>
                 <li>Managed workspace gaps: {overview.WorkspaceRequiredBlockCount}</li>
                 <li>Mode C/D entry first blocker: {Encode(overview.ModeExecutionEntryFirstBlockingCheckId ?? "(none)")}</li>
                 <li>Mode C/D entry first blocker command: {Encode(overview.ModeExecutionEntryFirstBlockingCheckRequiredCommand ?? "(none)")}</li>
                 <li>Mode C/D entry next command: {Encode(overview.ModeExecutionEntryRecommendedNextCommand ?? "(none)")}</li>
                 <li>Pending approvals: {overview.PendingApprovalCount}</li>
                 <li>Waiting reason: {Encode(overview.WaitingReason ?? "(none)")}</li>
               </ul>
               {RenderActionForms(overview.AvailableActions, "/workbench")}
             </section>
             <section class="panel">
               <h2>Focus Tasks</h2>
               {RenderTaskList(overview.FocusTasks)}
             </section>
             <section class="panel">
               <h2>Current Card</h2>
               {RenderCurrentCardSummary(card)}
             </section>
             <section class="panel">
               <h2>Review Queue</h2>
               {RenderReviewQueue(review.ReviewQueue, "/workbench/review")}
             </section>
             <section class="panel">
               <h2>Accepted Operations</h2>
               {RenderAcceptedOperations()}
             </section>
             """);
    }

    public string RenderWorkbenchCardHtml(LocalHostState hostState, string cardId)
    {
        var card = services.WorkbenchSurfaceService.BuildCard(cardId);
        return RenderWorkbenchHtml(
            hostState,
            $"Card {card.CardId}",
            $"""
             <section class="panel">
               <h2>{Encode(card.Title)}</h2>
               <p>{Encode(card.Goal)}</p>
               <ul class="facts">
                 <li>Status: {Encode(card.Status)}</li>
                 <li>Lifecycle: {Encode(card.LifecycleState)}</li>
                 <li>Summary: {Encode(card.Summary)}</li>
                 <li>Reality: {Encode(card.Reality.Status)} | {Encode(card.Reality.Summary)}</li>
                 <li>Blocked reason: {Encode(card.BlockedReason)}</li>
                 <li>Next action: {Encode(card.NextAction)}</li>
               </ul>
               {RenderActionForms(card.AvailableActions, $"/workbench/card/{Uri.EscapeDataString(card.CardId)}")}
             </section>
             <section class="panel">
               <h2>Tasks</h2>
               {RenderTaskList(card.Tasks)}
             </section>
             """);
    }

    public string RenderWorkbenchTaskHtml(LocalHostState hostState, string taskId)
    {
        var task = services.WorkbenchSurfaceService.BuildTask(taskId);
        return RenderWorkbenchHtml(
            hostState,
            $"Task {task.TaskId}",
            $"""
             <section class="panel">
               <h2>{Encode(task.Title)}</h2>
               <ul class="facts">
                 <li>Card: <a href="/workbench/card/{Uri.EscapeDataString(task.CardId)}">{Encode(task.CardId)}</a></li>
                 <li>Status: {Encode(task.Status)}</li>
                 <li>Summary: {Encode(task.Summary)}</li>
                 <li>Reality: {Encode(task.Reality.Status)} | {Encode(task.Reality.Summary)}</li>
                 <li>Blocked reason: {Encode(task.BlockedReason)}</li>
                 <li>Next action: {Encode(task.NextAction)}</li>
                 {RenderTaskReviewEvidenceFact(task)}
                 <li>Dependencies: {Encode(task.Dependencies.Count == 0 ? "(none)" : string.Join(", ", task.Dependencies))}</li>
                 <li>Unresolved dependencies: {Encode(task.UnresolvedDependencies.Count == 0 ? "(none)" : string.Join(", ", task.UnresolvedDependencies))}</li>
               </ul>
               {RenderRunSummary(task.ExecutionRun)}
               {RenderActionForms(task.AvailableActions, $"/workbench/task/{Uri.EscapeDataString(task.TaskId)}")}
             </section>
             <section class="panel">
               <h2>Artifacts</h2>
               {RenderArtifacts(task.Artifacts)}
             </section>
             <section class="panel">
               <h2>Related Tasks</h2>
               {RenderTaskList(task.RelatedTasks)}
             </section>
             """);
    }

    public string RenderWorkbenchReviewHtml(LocalHostState hostState)
    {
        var review = services.WorkbenchSurfaceService.BuildReview();
        return RenderWorkbenchHtml(
            hostState,
            "Review",
            $"""
             <section class="panel">
               <h2>Review Queue</h2>
               <p>{Encode(review.Summary)}</p>
               {RenderReviewQueue(review.ReviewQueue, "/workbench/review")}
               {RenderActionForms(review.GlobalActions, "/workbench/review")}
             </section>
             <section class="panel">
               <h2>Task Actions</h2>
               {RenderReviewQueue(review.TaskActionQueue, "/workbench/review")}
             </section>
             <section class="panel">
               <h2>Accepted Operations</h2>
               {RenderAcceptedOperations()}
             </section>
             """);
    }

    public string RenderWorkbenchActionResultHtml(LocalHostState hostState, string actionId, string? targetId, string? reason, OperatorCommandResult result, string returnPath)
    {
        var title = result.ExitCode == 0 ? "Action completed" : "Action failed";
        var message = new StringBuilder();
        message.Append("<section class=\"panel\">");
        message.Append($"<h2>{Encode(title)}</h2>");
        message.Append("<ul class=\"facts\">");
        message.Append($"<li>Action: {Encode(actionId)}</li>");
        message.Append($"<li>Target: {Encode(targetId ?? "(none)")}</li>");
        message.Append($"<li>Reason: {Encode(string.IsNullOrWhiteSpace(reason) ? "(none)" : reason)}</li>");
        message.Append("</ul>");
        message.Append("<pre>");
        foreach (var line in result.Lines)
        {
            message.Append(Encode(line));
            message.Append('\n');
        }

        message.Append("</pre>");
        message.Append($"<p><a href=\"{Encode(returnPath)}\">Back to Workbench</a></p>");
        message.Append("</section>");
        return RenderWorkbenchHtml(hostState, title, message.ToString());
    }

    public OperatorCommandResult ExecuteWorkbenchAction(string actionId, string? targetId, string? reason)
    {
        var normalizedAction = actionId.Trim().ToLowerInvariant();
        return normalizedAction switch
        {
            "approve" => string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(reason)
                ? OperatorCommandResult.Failure("Workbench action approve requires a task id and reason.")
                : services.OperatorSurfaceService.ApproveReview(targetId, reason),
            "provisional_approve" => string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(reason)
                ? OperatorCommandResult.Failure("Workbench action provisional_approve requires a task id and reason.")
                : services.OperatorSurfaceService.ApproveReview(targetId, reason, provisional: true),
            "reject" => string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(reason)
                ? OperatorCommandResult.Failure("Workbench action reject requires a task id and reason.")
                : services.OperatorSurfaceService.RejectReview(targetId, reason),
            "reopen" => string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(reason)
                ? OperatorCommandResult.Failure("Workbench action reopen requires a task id and reason.")
                : services.OperatorSurfaceService.ReopenReview(targetId, reason),
            "done" => string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(reason)
                ? OperatorCommandResult.Failure("Workbench action done requires a task id and reason.")
                : services.OperatorSurfaceService.ReviewTask(targetId, "done", reason),
            "fail" => string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(reason)
                ? OperatorCommandResult.Failure("Workbench action fail requires a task id and reason.")
                : services.OperatorSurfaceService.ReviewTask(targetId, "fail", reason),
            "block" => string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(reason)
                ? OperatorCommandResult.Failure("Workbench action block requires a task id and reason.")
                : services.OperatorSurfaceService.ReviewTask(targetId, "block", reason),
            "sync" => services.OperatorSurfaceService.SyncState(),
            _ => OperatorCommandResult.Failure($"Unknown workbench action '{actionId}'."),
        };
    }

    private string RenderWorkbenchHtml(LocalHostState hostState, string pageTitle, string body)
    {
        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>CARVES Workbench</title>
  <meta http-equiv="refresh" content="4">
  <style>
    body { font-family: Consolas, "Courier New", monospace; margin: 20px; background: #f4f1ea; color: #1e1b16; }
    a { color: #0f5f73; text-decoration: none; }
    .nav { display: flex; gap: 16px; margin-bottom: 16px; }
    .nav a { font-weight: 700; }
    .hero { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 12px; margin-bottom: 16px; }
    .metric, .panel { background: #fffdf8; border: 1px solid #d6cfbf; border-radius: 8px; padding: 14px; }
    .metric .label { display: block; font-size: 12px; color: #756a58; text-transform: uppercase; }
    .metric .value { display: block; margin-top: 6px; font-size: 18px; }
    .panel { margin-bottom: 16px; }
    h1, h2 { margin-top: 0; }
    ul { padding-left: 20px; }
    .facts { list-style: none; padding-left: 0; }
    .facts li { margin-bottom: 6px; }
    form { display: grid; gap: 8px; margin: 12px 0; padding: 12px; background: #f7f4ed; border-radius: 6px; }
    input[type=text] { width: 100%; padding: 8px; font-family: inherit; }
    button { width: fit-content; padding: 6px 10px; font-family: inherit; cursor: pointer; }
    pre { white-space: pre-wrap; }
  </style>
</head>
<body>
  <h1>CARVES Workbench</h1>
  <p>Page: {{Encode(pageTitle)}} | Host: <a href="{{Encode(hostState.WorkbenchUrl)}}">{{Encode(hostState.WorkbenchUrl)}}</a></p>
  <nav class="nav">
    <a href="/workbench">Overview</a>
    <a href="/workbench/review">Review</a>
    <a href="{{Encode(hostState.DashboardUrl)}}">Legacy Dashboard</a>
  </nav>
  {{body}}
</body>
</html>
""";
    }

    private static string RenderCurrentCardSummary(CardWorkbenchReadModel? card)
    {
        if (card is null)
        {
            return "<p>(none)</p>";
        }

        return $"""
                <p><a href="/workbench/card/{Uri.EscapeDataString(card.CardId)}">{Encode(card.CardId)}</a> — {Encode(card.Title)}</p>
                <ul class="facts">
                  <li>Status: {Encode(card.Status)}</li>
                  <li>Lifecycle: {Encode(card.LifecycleState)}</li>
                  <li>Reality: {Encode(card.Reality.Status)} | {Encode(card.Reality.Summary)}</li>
                  <li>Next action: {Encode(card.NextAction)}</li>
                </ul>
                """;
    }

    private static string RenderTaskList(IReadOnlyList<WorkbenchTaskListItem> tasks)
    {
        if (tasks.Count == 0)
        {
            return "<p>(none)</p>";
        }

        var builder = new StringBuilder("<ul>");
        foreach (var task in tasks)
        {
            builder.Append($"<li><a href=\"/workbench/task/{Uri.EscapeDataString(task.TaskId)}\">{Encode(task.TaskId)}</a> [{Encode(task.Status)}] {Encode(task.Title)}");
            builder.Append($"<div>summary={Encode(task.Summary)} | reality={Encode(task.Reality.Status)} | next={Encode(task.NextAction)} | blocked={Encode(task.BlockedReason)}</div>");
            builder.Append("</li>");
        }

        builder.Append("</ul>");
        return builder.ToString();
    }

    private static string RenderReviewQueue(IReadOnlyList<WorkbenchReviewQueueItem> items, string returnPath)
    {
        if (items.Count == 0)
        {
            return "<p>(none)</p>";
        }

        var builder = new StringBuilder();
        foreach (var item in items)
        {
            builder.Append("<article class=\"metric\">");
            builder.Append($"<h3><a href=\"/workbench/task/{Uri.EscapeDataString(item.TaskId)}\">{Encode(item.TaskId)}</a></h3>");
            builder.Append($"<p>{Encode(item.Title)}</p>");
            builder.Append("<ul class=\"facts\">");
            builder.Append($"<li>Card: {Encode(item.CardId)}</li>");
            builder.Append($"<li>Status: {Encode(item.Status)}</li>");
            builder.Append($"<li>Summary: {Encode(item.Summary)}</li>");
            builder.Append($"<li>Reality: {Encode(item.Reality.Status)} | {Encode(item.Reality.Summary)}</li>");
            if (!string.Equals(item.ReviewEvidence.Status, "unavailable", StringComparison.Ordinal))
            {
                AppendReviewEvidenceFacts(builder, item.ReviewEvidence);
            }
            builder.Append("</ul>");
            builder.Append(RenderActionForms(item.AvailableActions, returnPath));
            builder.Append("</article>");
        }

        return builder.ToString();
    }

    private string RenderAcceptedOperations()
    {
        var operations = ListRecentAcceptedOperations();
        if (operations.Count == 0)
        {
            return "<p>(none)</p>";
        }

        var builder = new StringBuilder("<ul>");
        foreach (var operation in operations)
        {
            builder.Append("<li>");
            builder.Append($"{Encode(operation.Command)} [{Encode(operation.ProgressMarker)}] completed={Encode(operation.Completed.ToString())}");
            builder.Append($"<div>operation={Encode(operation.OperationId)} | state={Encode(operation.OperationState)} | updated={Encode(operation.UpdatedAt.ToString("O"))}</div>");
            builder.Append("</li>");
        }

        builder.Append("</ul>");
        return builder.ToString();
    }

    private static string RenderArtifacts(IReadOnlyList<WorkbenchArtifactReference> items)
    {
        if (items.Count == 0)
        {
            return "<p>(none)</p>";
        }

        var builder = new StringBuilder("<ul>");
        foreach (var item in items)
        {
            builder.Append($"<li>{Encode(item.Label)}: {Encode(item.Path)}</li>");
        }

        builder.Append("</ul>");
        return builder.ToString();
    }

    private static string RenderRunSummary(WorkbenchRunSummary? run)
    {
        if (run is null)
        {
            return "<p>No execution run recorded.</p>";
        }

        return $"""
                <ul class="facts">
                  <li>Run id: {Encode(run.RunId)}</li>
                  <li>Status: {Encode(run.Status)}</li>
                  <li>Run count: {run.RunCount}</li>
                  <li>Current step: {Encode(run.CurrentStepTitle)}</li>
                </ul>
                """;
    }

    private static IReadOnlyList<string> FormatReviewQueueText(WorkbenchReviewQueueItem item)
    {
        var lines = new List<string>
        {
            $"  - {item.TaskId} [{item.Status}] {item.Title} | reality={item.Reality.Status} | actions={string.Join(", ", item.AvailableActions.Select(action => action.ActionId))}",
        };
        if (!string.Equals(item.ReviewEvidence.Status, "unavailable", StringComparison.Ordinal))
        {
            AddReviewEvidenceTextLines(lines, item.ReviewEvidence, "    ", compact: true);
        }

        return lines;
    }

    private static string RenderTaskReviewEvidenceFact(TaskWorkbenchReadModel task)
    {
        if (string.Equals(task.ReviewEvidence.Status, "unavailable", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        AppendReviewEvidenceFacts(builder, task.ReviewEvidence);
        return builder.ToString();
    }

    private static void AddReviewEvidenceTextLines(
        List<string> lines,
        WorkbenchReviewEvidenceReadModel reviewEvidence,
        string prefix = "",
        bool compact = false)
    {
        if (compact)
        {
            lines.Add($"{prefix}evidence={reviewEvidence.Status}; can_final_approve={reviewEvidence.CanFinalApprove}; summary={reviewEvidence.Summary}");
            if (reviewEvidence.MissingEvidence.Count > 0)
            {
                lines.Add($"{prefix}missing={string.Join(", ", reviewEvidence.MissingEvidence)}");
            }
        }
        else
        {
            lines.Add($"{prefix}Review evidence: {reviewEvidence.Status} | can_final_approve={reviewEvidence.CanFinalApprove}");
            lines.Add($"{prefix}Review evidence summary: {reviewEvidence.Summary}");
            if (reviewEvidence.MissingEvidence.Count > 0)
            {
                lines.Add($"{prefix}Missing evidence: {string.Join(", ", reviewEvidence.MissingEvidence)}");
            }
        }

        lines.Add($"{prefix}Worker completion claim: status={reviewEvidence.CompletionClaimStatus}; required={reviewEvidence.CompletionClaimRequired}");
        if (reviewEvidence.CompletionClaimMissingFields.Count > 0)
        {
            lines.Add($"{prefix}Completion claim missing fields: {string.Join(", ", reviewEvidence.CompletionClaimMissingFields)}");
        }

        if (reviewEvidence.CompletionClaimEvidencePaths.Count > 0)
        {
            lines.Add($"{prefix}Completion claim evidence paths: {string.Join(", ", reviewEvidence.CompletionClaimEvidencePaths)}");
        }

        lines.Add($"{prefix}Completion claim summary: {reviewEvidence.CompletionClaimSummary}");
        lines.Add($"{prefix}Host validation: status={reviewEvidence.HostValidationStatus}; required={reviewEvidence.HostValidationRequired}");
        if (reviewEvidence.HostValidationBlockers.Count > 0)
        {
            lines.Add($"{prefix}Host validation blockers: {string.Join(", ", reviewEvidence.HostValidationBlockers)}");
        }

        lines.Add($"{prefix}Host validation summary: {reviewEvidence.HostValidationSummary}");
    }

    private static void AppendReviewEvidenceFacts(StringBuilder builder, WorkbenchReviewEvidenceReadModel reviewEvidence)
    {
        builder.Append($"<li>Review evidence: {Encode(reviewEvidence.Status)} | can_final_approve={Encode(reviewEvidence.CanFinalApprove.ToString())}</li>");
        builder.Append($"<li>Review evidence summary: {Encode(reviewEvidence.Summary)}</li>");
        if (reviewEvidence.MissingEvidence.Count > 0)
        {
            builder.Append($"<li>Missing evidence: {Encode(string.Join(", ", reviewEvidence.MissingEvidence))}</li>");
        }

        builder.Append($"<li>Worker completion claim: status={Encode(reviewEvidence.CompletionClaimStatus)} | required={Encode(reviewEvidence.CompletionClaimRequired.ToString())}</li>");
        if (reviewEvidence.CompletionClaimMissingFields.Count > 0)
        {
            builder.Append($"<li>Completion claim missing fields: {Encode(string.Join(", ", reviewEvidence.CompletionClaimMissingFields))}</li>");
        }

        if (reviewEvidence.CompletionClaimEvidencePaths.Count > 0)
        {
            builder.Append($"<li>Completion claim evidence paths: {Encode(string.Join(", ", reviewEvidence.CompletionClaimEvidencePaths))}</li>");
        }

        builder.Append($"<li>Completion claim summary: {Encode(reviewEvidence.CompletionClaimSummary)}</li>");
        builder.Append($"<li>Host validation: status={Encode(reviewEvidence.HostValidationStatus)} | required={Encode(reviewEvidence.HostValidationRequired.ToString())}</li>");
        if (reviewEvidence.HostValidationBlockers.Count > 0)
        {
            builder.Append($"<li>Host validation blockers: {Encode(string.Join(", ", reviewEvidence.HostValidationBlockers))}</li>");
        }

        builder.Append($"<li>Host validation summary: {Encode(reviewEvidence.HostValidationSummary)}</li>");
    }

    private static string RenderActionForms(IReadOnlyList<WorkbenchActionDescriptor> actions, string returnPath)
    {
        if (actions.Count == 0)
        {
            return "<p>No actions.</p>";
        }

        var builder = new StringBuilder();
        foreach (var action in actions)
        {
            builder.Append("<form method=\"post\" action=\"/workbench/action\">");
            builder.Append($"<input type=\"hidden\" name=\"action\" value=\"{Encode(action.ActionId)}\" />");
            builder.Append($"<input type=\"hidden\" name=\"target_id\" value=\"{Encode(action.TargetId)}\" />");
            builder.Append($"<input type=\"hidden\" name=\"return_path\" value=\"{Encode(returnPath)}\" />");
            builder.Append($"<strong>{Encode(action.Label)}</strong><div>{Encode(action.Summary)}</div>");
            if (action.RequiresReason)
            {
                builder.Append("<label>Reason <input type=\"text\" name=\"reason\" required /></label>");
            }
            else
            {
                builder.Append("<input type=\"hidden\" name=\"reason\" value=\"\" />");
            }

            builder.Append($"<div><code>{Encode(action.Command)}</code></div>");
            builder.Append($"<button type=\"submit\">{Encode(action.Label)}</button>");
            builder.Append("</form>");
        }

        return builder.ToString();
    }

    private static string FormatActionLine(WorkbenchActionDescriptor action)
    {
        var suffix = action.RequiresReason ? " (reason required)" : string.Empty;
        return $"  - {action.Label}: {action.Command}{suffix}";
    }

    private JsonObject ToJsonObject<T>(T payload)
    {
        return JsonSerializer.SerializeToNode(payload, JsonOptions)?.AsObject()
            ?? new JsonObject();
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}

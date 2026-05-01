using System.Text.Json;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult PauseHost(string reason)
    {
        var session = devLoopService.GetSession();
        if (session is not null && session.Status != Carves.Runtime.Domain.Runtime.RuntimeSessionStatus.Paused)
        {
            devLoopService.PauseSession(reason);
        }

        var hostSession = new HostSessionService(paths).Pause(reason);
        markdownSyncService.Sync(taskGraphService.Load(), session: devLoopService.GetSession());
        return OperatorCommandResult.Success(
            $"Paused host control for session {hostSession.SessionId}.",
            $"Host control state: {hostSession.ControlState}",
            $"Reason: {hostSession.LastControlReason ?? reason}");
    }

    public OperatorCommandResult ResumeHost(string reason)
    {
        var session = devLoopService.GetSession();
        if (session is not null && session.Status == Carves.Runtime.Domain.Runtime.RuntimeSessionStatus.Paused)
        {
            devLoopService.ResumeSession(reason);
        }

        var hostSession = new HostSessionService(paths).Resume(reason);
        markdownSyncService.Sync(taskGraphService.Load(), session: devLoopService.GetSession());
        return OperatorCommandResult.Success(
            $"Resumed host control for session {hostSession.SessionId}.",
            $"Host control state: {hostSession.ControlState}",
            $"Reason: {hostSession.LastControlReason ?? reason}");
    }

    public OperatorCommandResult InspectMethodologyGate(string subject)
    {
        var methodology = new RuntimeMethodologyComplianceService(paths);
        RuntimeMethodologyAssessment assessment;
        if (subject.StartsWith("CARD-", StringComparison.OrdinalIgnoreCase))
        {
            var draft = planningDraftService.TryGetCardDraft(subject);
            if (draft is not null)
            {
                assessment = methodology.AssessDraft(draft);
            }
            else
            {
                var cardPath = Path.Combine(paths.CardsRoot, $"{subject}.md");
                if (!File.Exists(cardPath))
                {
                    throw new InvalidOperationException($"Card '{subject}' was not found.");
                }

                assessment = methodology.AssessCard(plannerService.ParseCard(cardPath), cardPath);
            }
        }
        else
        {
            var draft = planningDraftService.TryGetCardDraft(subject)
                ?? throw new InvalidOperationException($"Card or draft '{subject}' was not found.");
            assessment = methodology.AssessDraft(draft);
        }

        var lines = new List<string>
        {
            $"Methodology applies: {assessment.Applies}",
            $"Methodology acknowledged: {assessment.Acknowledged}",
            $"Reference: {assessment.ReferencePath}",
            $"Coverage: {RuntimeMethodologyComplianceService.DescribeCoverage(assessment.CoverageStatus)}",
            $"Summary: {assessment.Summary}",
            $"Recommended action: {assessment.RecommendedAction}",
            $"Related cards: {(assessment.RelatedCardIds.Count == 0 ? "(none)" : string.Join(", ", assessment.RelatedCardIds))}",
        };
        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult InspectAsyncResumeGate()
    {
        var gate = new RuntimeMethodologyComplianceService(paths).EnsureAsyncResumeGate();
        var lines = new List<string>
        {
            $"Resume gate schema: {gate.SchemaVersion}",
            $"Reference: {gate.ReferencePath}",
            "Preconditions:",
            "Resume order:",
            "Anti-duplication rules:",
        };
        lines.InsertRange(3, gate.Preconditions.Select(item => $"- {item}"));
        var resumeOrderIndex = lines.IndexOf("Resume order:") + 1;
        lines.InsertRange(resumeOrderIndex, gate.ResumeOrder.Select(item => $"- {item.Order}. {item.Summary} => {string.Join(", ", item.CardIds)} ({item.ResumeReason})"));
        var antiDuplicationIndex = lines.IndexOf("Anti-duplication rules:") + 1;
        lines.InsertRange(antiDuplicationIndex, gate.AntiDuplicationRules.Select(item => $"- {item}"));
        return new OperatorCommandResult(0, lines);
    }
}

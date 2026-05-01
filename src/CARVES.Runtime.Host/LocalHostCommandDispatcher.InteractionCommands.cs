using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Host;

internal static partial class LocalHostCommandDispatcher
{
    private static OperatorCommandResult RunApiCommand(RuntimeServices services, IReadOnlyList<string> arguments) => RunApiCommandCore(services, arguments);

    private const string ActorUsage = "Usage: actor <sessions|ownership|events|agent-trace|repair-sessions|clear-sessions|fallback-policy|register|heartbeat> [...]";
    private const string ActorRegisterUsage = "Usage: actor register --kind <operator|agent|planner|worker> --identity <id> [--repo-id <id>] [--actor-session-id <id>] [--provider-profile <id>] [--capability-profile <id>] [--scope <scope>] [--budget-profile <id>] [--schedule-binding <id>] [--context-receipt <id>] [--health <posture>] [--process-id <pid>] [--registration-mode <manual|supervised>] [--worker-instance-id <id>] [--launch-token <token>] [--reason <text>] [--dry-run]";
    private const string ApiActorSessionRegisterUsage = "Usage: api actor-session-register --kind <operator|agent|planner|worker> --identity <id> [--repo-id <id>] [--actor-session-id <id>] [--provider-profile <id>] [--capability-profile <id>] [--scope <scope>] [--budget-profile <id>] [--schedule-binding <id>] [--context-receipt <id>] [--health <posture>] [--process-id <pid>] [--registration-mode <manual|supervised>] [--worker-instance-id <id>] [--launch-token <token>] [--reason <text>] [--dry-run]";

    private static OperatorCommandResult RunActorCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure(ActorUsage);
        }

        return arguments[0] switch
        {
            "sessions" => services.OperatorSurfaceService.ActorSessions(
                ParseActorSessionKind(ResolveOption(arguments, "--kind")),
                ResolveOption(arguments, "--repo-id")),
            "ownership" => services.OperatorSurfaceService.ActorOwnership(ParseOwnershipScope(ResolveOption(arguments, "--scope")), ResolveOption(arguments, "--target-id")),
            "events" => services.OperatorSurfaceService.OperatorOsEvents(ResolveOption(arguments, "--task-id"), ResolveOption(arguments, "--actor-session-id"), ParseOperatorOsEventKind(ResolveOption(arguments, "--kind"))),
            "agent-trace" or "agent-gateway-trace" => services.OperatorSurfaceService.AgentGatewayTrace(),
            "repair-sessions" => services.OperatorSurfaceService.RepairActorSessions(arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase))),
            "clear-sessions" => services.OperatorSurfaceService.ClearActorSessions(
                ResolveOption(arguments, "--actor-session-id"),
                ResolveOption(arguments, "--repo-id"),
                ParseActorSessionKind(ResolveOption(arguments, "--kind")),
                arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase)),
                ResolveOption(arguments, "--reason")),
            "fallback-policy" => services.OperatorSurfaceService.ActorSessionFallbackPolicy(ResolveOption(arguments, "--repo-id")),
            "heartbeat" => services.OperatorSurfaceService.ActorSessionHeartbeat(
                ResolveOption(arguments, "--actor-session-id"),
                ResolveOption(arguments, "--health"),
                ResolveOption(arguments, "--context-receipt"),
                arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase)),
                ResolveOption(arguments, "--reason")),
            "register" => arguments.Count == 1
                ? OperatorCommandResult.Failure(ActorRegisterUsage)
                : services.OperatorSurfaceService.RegisterActorSession(
                    ParseActorSessionKind(ResolveOption(arguments, "--kind")),
                    ResolveOption(arguments, "--identity"),
                    ResolveOption(arguments, "--repo-id"),
                    ResolveOption(arguments, "--actor-session-id"),
                    ResolveOption(arguments, "--provider-profile"),
                    ResolveOption(arguments, "--capability-profile"),
                    ResolveOption(arguments, "--scope"),
                    ResolveOption(arguments, "--budget-profile"),
                    ResolveOption(arguments, "--schedule-binding"),
                    ResolveOption(arguments, "--context-receipt"),
                    ResolveOption(arguments, "--health"),
                    ResolveOptionalInt(arguments, "--process-id"),
                    ResolveOption(arguments, "--registration-mode"),
                    ResolveOption(arguments, "--worker-instance-id"),
                    ResolveOption(arguments, "--launch-token"),
                    ResolveOption(arguments, "--reason"),
                    arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase))),
            _ => OperatorCommandResult.Failure(ActorUsage),
        };
    }

    private static OperatorCommandResult RunDashboardCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var text = arguments.Any(argument => string.Equals(argument, "--text", StringComparison.OrdinalIgnoreCase));
        var repoId = ResolvePrimaryArgument(arguments, [], ["--text"]);
        if (text)
        {
            var surface = new LocalHostSurfaceService(services);
            return OperatorCommandResult.Success(surface.RenderDashboardText(repoId).ToArray());
        }

        return services.OperatorSurfaceService.Dashboard(repoId);
    }

    private static OperatorCommandResult RunWorkbenchCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var surface = new LocalHostSurfaceService(services);
        var filtered = arguments.Where(argument => !string.Equals(argument, "--text", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (filtered.Length == 0)
        {
            return OperatorCommandResult.Success(surface.RenderWorkbenchTextOverview().ToArray());
        }

        return filtered[0].ToLowerInvariant() switch
        {
            "overview" => OperatorCommandResult.Success(surface.RenderWorkbenchTextOverview().ToArray()),
            "review" => OperatorCommandResult.Success(surface.RenderWorkbenchTextReview().ToArray()),
            "card" when filtered.Length >= 2 => OperatorCommandResult.Success(surface.RenderWorkbenchTextCard(filtered[1]).ToArray()),
            "task" when filtered.Length >= 2 => OperatorCommandResult.Success(surface.RenderWorkbenchTextTask(filtered[1]).ToArray()),
            _ => OperatorCommandResult.Failure("Usage: workbench [overview|card <card-id>|task <task-id>|review] [--text]"),
        };
    }

    private static OperatorCommandResult RunAttachCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var dryRun = arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase));
        var startRuntime = arguments.Any(argument => string.Equals(argument, "--start-runtime", StringComparison.OrdinalIgnoreCase));
        var force = arguments.Any(argument => string.Equals(argument, "--force", StringComparison.OrdinalIgnoreCase));
        var repoPath = ResolvePrimaryArgument(arguments, ["--repo-id", "--provider-profile", "--policy-profile", "--client-repo-root"], ["--dry-run", "--start-runtime", "--force"]);
        repoPath ??= services.Paths.RepoRoot;

        return new TargetRepoAttachService(services).Attach(
            ResolvePath(services.Paths.RepoRoot, repoPath),
            ResolveOption(arguments, "--repo-id"),
            ResolveOption(arguments, "--provider-profile"),
            ResolveOption(arguments, "--policy-profile"),
            startRuntime,
            dryRun,
            force,
            ResolveOption(arguments, "--client-repo-root"));
    }

    private static OperatorCommandResult RunCardCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: card <create-draft|list|inspect|update|status> [...]");
        }

        var scoped = ResolveRepoScopedServices(services, arguments, out var filteredArguments);
        if (filteredArguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: card <create-draft|list|inspect|update|status> [...]");
        }

        return filteredArguments[0].ToLowerInvariant() switch
        {
            "create-draft" => filteredArguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: card create-draft <json-path> [--repo-id <id>]")
                : scoped.OperatorSurfaceService.CreateCardDraft(ResolvePath(services.Paths.RepoRoot, filteredArguments[1])),
            "list" => OperatorCommandResult.Success(new LocalHostSurfaceService(scoped).ToPrettyJson(new LocalHostSurfaceService(scoped).BuildCardList(ResolveOption(filteredArguments, "--state")))),
            "inspect" => filteredArguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: card inspect <card-id> [--repo-id <id>]")
                : RunInspectCardWithServices(scoped, filteredArguments[1]),
            "update" => filteredArguments.Count < 3
                ? OperatorCommandResult.Failure("Usage: card update <card-id> <json-path> [--repo-id <id>]")
                : scoped.OperatorSurfaceService.UpdateCardDraft(filteredArguments[1], ResolvePath(services.Paths.RepoRoot, filteredArguments[2])),
            "status" => filteredArguments.Count < 3
                ? OperatorCommandResult.Failure("Usage: card status <card-id> <draft|reviewed|approved|rejected|archived> [reason...] [--repo-id <id>]")
                : scoped.OperatorSurfaceService.SetCardStatus(
                    filteredArguments[1],
                    ParseCardLifecycleState(filteredArguments[2]),
                    filteredArguments.Count > 3 ? string.Join(' ', filteredArguments.Skip(3)) : null),
            _ => OperatorCommandResult.Failure("Usage: card <create-draft|list|inspect|update|status> [...]"),
        };
    }

    private static OperatorCommandResult RunIntentCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: intent <status|draft [--persist]|focus|decision|candidate|accept|discard>");
        }

        var surface = new LocalHostSurfaceService(services);
        return arguments[0].ToLowerInvariant() switch
        {
            "status" => OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildIntentStatus())),
            "draft" =>
                arguments.Any(argument => string.Equals(argument, "--persist", StringComparison.OrdinalIgnoreCase))
                    ? OperatorCommandResult.Success(surface.ToPrettyJson(LocalHostSurfaceService.BuildIntentStatusFrom(services.IntentDiscoveryService.GenerateDraft())))
                    : OperatorCommandResult.Success(surface.ToPrettyJson(BuildIntentDraftPreviewNode(services))),
            "focus" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: intent focus <candidate-card-id|none|clear>")
                : OperatorCommandResult.Success(surface.ToPrettyJson(LocalHostSurfaceService.BuildIntentStatusFrom(services.IntentDiscoveryService.SetFocusCard(arguments[1])))),
            "decision" => arguments.Count < 3
                ? OperatorCommandResult.Failure("Usage: intent decision <decision-id> <open|resolved|paused|forbidden>")
                : OperatorCommandResult.Success(surface.ToPrettyJson(LocalHostSurfaceService.BuildIntentStatusFrom(services.IntentDiscoveryService.SetPendingDecisionStatus(arguments[1], ParseGuidedPlanningDecisionStatus(arguments[2]))))),
            "candidate" => arguments.Count < 3
                ? OperatorCommandResult.Failure("Usage: intent candidate <candidate-card-id> <emerging|needs_confirmation|wobbling|grounded|paused|forbidden|ready_to_plan>")
                : OperatorCommandResult.Success(surface.ToPrettyJson(LocalHostSurfaceService.BuildIntentStatusFrom(services.IntentDiscoveryService.SetCandidateCardPosture(arguments[1], ParseGuidedPlanningPosture(arguments[2]))))),
            "accept" => OperatorCommandResult.Success(surface.ToPrettyJson(LocalHostSurfaceService.BuildIntentStatusFrom(services.IntentDiscoveryService.AcceptDraft()))),
            "discard" => OperatorCommandResult.Success(surface.ToPrettyJson(LocalHostSurfaceService.BuildIntentStatusFrom(services.IntentDiscoveryService.DiscardDraft()))),
            _ => OperatorCommandResult.Failure("Usage: intent <status|draft [--persist]|focus|decision|candidate|accept|discard>"),
        };
    }

    private static JsonNode BuildIntentDraftPreviewNode(RuntimeServices services)
    {
        return LocalHostSurfaceService.BuildIntentPreviewFrom(services.IntentDiscoveryService.PreviewDraft());
    }

    private static OperatorCommandResult RunProtocolCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: protocol <status|check <phase>>");
        }

        var surface = new LocalHostSurfaceService(services);
        return arguments[0].ToLowerInvariant() switch
        {
            "status" => OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildProtocolStatus())),
            "check" when arguments.Count >= 2 => OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildProtocolCheck(arguments[1]))),
            _ => OperatorCommandResult.Failure("Usage: protocol <status|check <phase>>"),
        };
    }

    private static OperatorCommandResult RunPromptCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: prompt <kernel|templates|template <id>>");
        }

        var surface = new LocalHostSurfaceService(services);
        return arguments[0].ToLowerInvariant() switch
        {
            "kernel" => OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildPromptKernel())),
            "templates" => OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildPromptTemplates())),
            "template" when arguments.Count >= 2 => OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildPromptTemplate(arguments[1]))),
            _ => OperatorCommandResult.Failure("Usage: prompt <kernel|templates|template <id>>"),
        };
    }

    private static OperatorCommandResult RunInspectCommand(RuntimeServices services, IReadOnlyList<string> arguments) => RunInspectCommandCore(services, arguments);

    private static RoutingValidationMode ParseRoutingValidationMode(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "baseline" => RoutingValidationMode.Baseline,
            "forced-fallback" or "forced_fallback" => RoutingValidationMode.ForcedFallback,
            _ => RoutingValidationMode.Routing,
        };
    }

    private static OperatorCommandResult RunPilotCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: pilot <boot|agent-start|start|next|problem-intake|triage|problem-triage|friction-ledger|follow-up|problem-follow-up|triage-follow-up|follow-up-plan|problem-follow-up-plan|triage-follow-up-plan|follow-up-record|follow-up-decision-record|problem-follow-up-record|follow-up-intake|follow-up-planning|problem-follow-up-intake|follow-up-gate|follow-up-planning-gate|problem-follow-up-gate|record-follow-up-decision|report-problem|list-problems|inspect-problem|guide|status|preflight|readiness|alpha|invocation|activation|alias|dist-smoke|dist-freshness|dist-binding|bind-dist|target-proof|external-proof|resources|commit-hygiene|commit-plan|closure|residue|ignore-plan|ignore-record|record-ignore-decision|dist|proof|close-loop|record-evidence|list-evidence|inspect-evidence> [...]");
        }

        return arguments[0] switch
        {
            "boot" or "agent-start" or "thread-start" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeAgentThreadStart()
                : services.OperatorSurfaceService.InspectRuntimeAgentThreadStart(),
            "start" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeExternalTargetPilotStart()
                : services.OperatorSurfaceService.InspectRuntimeExternalTargetPilotStart(),
            "next" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeExternalTargetPilotNext()
                : services.OperatorSurfaceService.InspectRuntimeExternalTargetPilotNext(),
            "problem-intake" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeAgentProblemIntake()
                : services.OperatorSurfaceService.InspectRuntimeAgentProblemIntake(),
            "triage" or "problem-triage" or "friction-ledger" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeAgentProblemTriageLedger()
                : services.OperatorSurfaceService.InspectRuntimeAgentProblemTriageLedger(),
            "follow-up" or "problem-follow-up" or "triage-follow-up" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeAgentProblemFollowUpCandidates()
                : services.OperatorSurfaceService.InspectRuntimeAgentProblemFollowUpCandidates(),
            "follow-up-plan" or "problem-follow-up-plan" or "triage-follow-up-plan" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeAgentProblemFollowUpDecisionPlan()
                : services.OperatorSurfaceService.InspectRuntimeAgentProblemFollowUpDecisionPlan(),
            "follow-up-record" or "follow-up-decision-record" or "problem-follow-up-record" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeAgentProblemFollowUpDecisionRecord()
                : services.OperatorSurfaceService.InspectRuntimeAgentProblemFollowUpDecisionRecord(),
            "follow-up-intake" or "follow-up-planning" or "problem-follow-up-intake" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeAgentProblemFollowUpPlanningIntake()
                : services.OperatorSurfaceService.InspectRuntimeAgentProblemFollowUpPlanningIntake(),
            "follow-up-gate" or "follow-up-planning-gate" or "problem-follow-up-gate" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeAgentProblemFollowUpPlanningGate()
                : services.OperatorSurfaceService.InspectRuntimeAgentProblemFollowUpPlanningGate(),
            "record-follow-up-decision" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: pilot record-follow-up-decision <accept_as_governed_planning_input|accept_as_governed_planning_input_after_operator_override|reject_as_target_project_only|reject_as_noise_or_target_only|wait_for_more_evidence|accept|reject|wait> [--all] [--candidate <candidate-id>...] --reason <text> [--operator <name>] [--plan-id <id>] [--acceptance-evidence <text>] [--readback <command>] [--json]")
                : services.OperatorSurfaceService.RecordRuntimeAgentProblemFollowUpDecision(
                    new Carves.Runtime.Domain.Planning.AgentProblemFollowUpDecisionRecordRequest
                    {
                        Decision = arguments[1],
                        AllCandidates = arguments.Any(argument => string.Equals(argument, "--all", StringComparison.OrdinalIgnoreCase)),
                        CandidateIds = ResolveMultiOption(arguments, "--candidate"),
                        Reason = ResolveOption(arguments, "--reason") ?? string.Empty,
                        Operator = ResolveOption(arguments, "--operator") ?? "operator",
                        PlanId = ResolveOption(arguments, "--plan-id"),
                        AcceptanceEvidence = ResolveOption(arguments, "--acceptance-evidence") ?? string.Empty,
                        ReadbackCommand = ResolveOption(arguments, "--readback") ?? string.Empty,
                    },
                    arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))),
            "report-problem" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: pilot report-problem <json-path> [--json]")
                : services.OperatorSurfaceService.ReportPilotProblem(
                    ResolvePath(services.Paths.RepoRoot, arguments[1]),
                    arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))),
            "list-problems" => services.OperatorSurfaceService.ListPilotProblems(ResolveOption(arguments, "--repo-id")),
            "inspect-problem" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: pilot inspect-problem <problem-id>")
                : services.OperatorSurfaceService.InspectPilotProblem(arguments[1]),
            "guide" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeProductClosurePilotGuide()
                : services.OperatorSurfaceService.InspectRuntimeProductClosurePilotGuide(),
            "status" or "preflight" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? DecoratePilotStatusJson(services, services.OperatorSurfaceService.ApiRuntimeProductClosurePilotStatus())
                : DecoratePilotStatusText(services, services.OperatorSurfaceService.InspectRuntimeProductClosurePilotStatus()),
            "readiness" or "alpha" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeAlphaExternalUseReadiness()
                : services.OperatorSurfaceService.InspectRuntimeAlphaExternalUseReadiness(),
            "resources" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeExternalConsumerResourcePack()
                : services.OperatorSurfaceService.InspectRuntimeExternalConsumerResourcePack(),
            "invocation" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeCliInvocationContract()
                : services.OperatorSurfaceService.InspectRuntimeCliInvocationContract(),
            "activation" or "alias" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeCliActivationPlan()
                : services.OperatorSurfaceService.InspectRuntimeCliActivationPlan(),
            "dist-binding" or "bind-dist" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeTargetDistBindingPlan()
                : services.OperatorSurfaceService.InspectRuntimeTargetDistBindingPlan(),
            "dist-smoke" or "dist-freshness" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeLocalDistFreshnessSmoke()
                : services.OperatorSurfaceService.InspectRuntimeLocalDistFreshnessSmoke(),
            "target-proof" or "external-proof" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeFrozenDistTargetReadbackProof()
                : services.OperatorSurfaceService.InspectRuntimeFrozenDistTargetReadbackProof(),
            "commit-hygiene" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeTargetCommitHygiene()
                : services.OperatorSurfaceService.InspectRuntimeTargetCommitHygiene(),
            "commit-plan" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeTargetCommitPlan()
                : services.OperatorSurfaceService.InspectRuntimeTargetCommitPlan(),
            "closure" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeTargetCommitClosure()
                : services.OperatorSurfaceService.InspectRuntimeTargetCommitClosure(),
            "residue" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeTargetResiduePolicy()
                : services.OperatorSurfaceService.InspectRuntimeTargetResiduePolicy(),
            "ignore-plan" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeTargetIgnoreDecisionPlan()
                : services.OperatorSurfaceService.InspectRuntimeTargetIgnoreDecisionPlan(),
            "ignore-record" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeTargetIgnoreDecisionRecord()
                : services.OperatorSurfaceService.InspectRuntimeTargetIgnoreDecisionRecord(),
            "record-ignore-decision" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: pilot record-ignore-decision <keep_local|add_to_gitignore_after_review|manual_cleanup_after_review> [--all] [--entry <entry>...] --reason <text> [--operator <name>] [--plan-id <id>] [--json]")
                : services.OperatorSurfaceService.RecordRuntimeTargetIgnoreDecision(
                    new Carves.Runtime.Domain.Planning.TargetIgnoreDecisionRecordRequest
                    {
                        Decision = arguments[1],
                        AllEntries = arguments.Any(argument => string.Equals(argument, "--all", StringComparison.OrdinalIgnoreCase)),
                        Entries = ResolveMultiOption(arguments, "--entry"),
                        Reason = ResolveOption(arguments, "--reason") ?? string.Empty,
                        Operator = ResolveOption(arguments, "--operator") ?? "operator",
                        PlanId = ResolveOption(arguments, "--plan-id"),
                    },
                    arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))),
            "dist" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeLocalDistHandoff()
                : services.OperatorSurfaceService.InspectRuntimeLocalDistHandoff(),
            "proof" => arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                ? services.OperatorSurfaceService.ApiRuntimeProductPilotProof()
                : services.OperatorSurfaceService.InspectRuntimeProductPilotProof(),
            "close-loop" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: pilot close-loop <task-id> [--changed-file <path>] [--summary <text>] [--failure-message <text>] [--review-reason <text>] [--record-evidence <json-path>]")
                : services.OperatorSurfaceService.ClosePilotLoop(
                    new Carves.Runtime.Domain.Planning.PilotCloseLoopRequest
                    {
                        TaskId = arguments[1],
                        ChangedFile = ResolveOption(arguments, "--changed-file"),
                        Summary = ResolveOption(arguments, "--summary"),
                        FailureMessage = ResolveOption(arguments, "--failure-message"),
                        ReviewReason = ResolveOption(arguments, "--review-reason"),
                        PilotEvidencePath = ResolveOptionalPath(services.Paths.RepoRoot, ResolveOption(arguments, "--record-evidence")),
                    }),
            "record-evidence" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: pilot record-evidence <json-path>")
                : services.OperatorSurfaceService.RecordPilotEvidence(ResolvePath(services.Paths.RepoRoot, arguments[1])),
            "list-evidence" => services.OperatorSurfaceService.ListPilotEvidence(ResolveOption(arguments, "--repo-id")),
            "inspect-evidence" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: pilot inspect-evidence <evidence-id>")
                : services.OperatorSurfaceService.InspectPilotEvidence(arguments[1]),
            _ => OperatorCommandResult.Failure("Usage: pilot <boot|agent-start|start|next|problem-intake|triage|problem-triage|friction-ledger|follow-up|problem-follow-up|triage-follow-up|follow-up-plan|problem-follow-up-plan|triage-follow-up-plan|follow-up-record|follow-up-decision-record|problem-follow-up-record|follow-up-intake|follow-up-planning|problem-follow-up-intake|follow-up-gate|follow-up-planning-gate|problem-follow-up-gate|record-follow-up-decision|report-problem|list-problems|inspect-problem|guide|status|preflight|readiness|alpha|invocation|activation|alias|dist-smoke|dist-freshness|dist-binding|bind-dist|target-proof|external-proof|resources|commit-hygiene|commit-plan|closure|residue|ignore-plan|ignore-record|record-ignore-decision|dist|proof|close-loop|record-evidence|list-evidence|inspect-evidence> [...]"),
        };
    }

    private static OperatorCommandResult DecoratePilotStatusJson(RuntimeServices services, OperatorCommandResult result)
    {
        if (result.ExitCode != 0 || result.Lines.Count == 0)
        {
            return result;
        }

        try
        {
            var node = JsonNode.Parse(string.Join(Environment.NewLine, result.Lines))?.AsObject();
            if (node is null)
            {
                return result;
            }

            var honesty = LocalHostSurfaceHonesty.Describe(new LocalHostDiscoveryService().Discover(services.Paths.RepoRoot));
            var hostAwareSafeToStart = (node["safe_to_start_new_execution"]?.GetValue<bool>() ?? false) && honesty.HostRunning;
            node["safe_to_start_new_execution"] = hostAwareSafeToStart;
            node["host_readiness"] = honesty.HostReadiness;
            node["host_operational_state"] = honesty.OperationalState;
            node["host_conflict_present"] = honesty.ConflictPresent;
            node["host_safe_to_start_new_host"] = honesty.SafeToStartNewHost;
            node["host_pointer_repair_applied"] = honesty.PointerRepairApplied;
            node["host_recommended_action_kind"] = honesty.RecommendedActionKind;
            node["host_recommended_action"] = honesty.RecommendedAction;
            node["host_lifecycle"] = new JsonObject
            {
                ["state"] = honesty.Lifecycle.State,
                ["reason"] = honesty.Lifecycle.Reason,
                ["action_kind"] = honesty.Lifecycle.ActionKind,
                ["action"] = honesty.Lifecycle.Action,
                ["ready"] = honesty.Lifecycle.Ready,
                ["blocks_automation"] = honesty.Lifecycle.BlocksAutomation,
            };
            node["host_summary_message"] = honesty.SummaryMessage;
            if (node["summary"] is JsonValue summaryValue)
            {
                var summary = summaryValue.GetValue<string>();
                if (!honesty.HostRunning)
                {
                    node["summary"] = $"{summary} Resident host is not currently ready for new execution.";
                }
                else if (honesty.PointerRepairApplied)
                {
                    node["summary"] = $"{summary} Active host pointer was repaired to a healthy existing generation.";
                }
            }

            return OperatorCommandResult.Success(new LocalHostSurfaceService(services).ToPrettyJson(node));
        }
        catch
        {
            return result;
        }
    }

    private static OperatorCommandResult DecoratePilotStatusText(RuntimeServices services, OperatorCommandResult result)
    {
        var honesty = LocalHostSurfaceHonesty.Describe(new LocalHostDiscoveryService().Discover(services.Paths.RepoRoot));
        var lines = result.Lines.ToList();
        var safeIndex = lines.FindIndex(static line => line.StartsWith("Safe to start new execution:", StringComparison.Ordinal));
        if (safeIndex >= 0)
        {
            var current = ExtractBooleanValue(lines[safeIndex]);
            lines[safeIndex] = $"Safe to start new execution: {current && honesty.HostRunning}";
        }

        var summaryIndex = lines.FindIndex(static line => line.StartsWith("Summary:", StringComparison.Ordinal));
        if (summaryIndex >= 0)
        {
            if (!honesty.HostRunning)
            {
                lines[summaryIndex] = $"{lines[summaryIndex]} Resident host is not currently ready for new execution.";
            }
            else if (honesty.PointerRepairApplied)
            {
                lines[summaryIndex] = $"{lines[summaryIndex]} Active host pointer was repaired to a healthy existing generation.";
            }
        }

        lines.Add($"Host readiness: {honesty.HostReadiness}");
        lines.Add($"Host operational state: {honesty.OperationalState}");
        lines.Add($"Host conflict present: {honesty.ConflictPresent}");
        lines.Add($"Host safe to start new host: {honesty.SafeToStartNewHost}");
        lines.Add($"Host pointer repair applied: {honesty.PointerRepairApplied}");
        lines.Add($"Host recommended action kind: {honesty.RecommendedActionKind}");
        lines.Add($"Host recommended action: {honesty.RecommendedAction}");
        lines.Add($"Host lifecycle state: {honesty.Lifecycle.State}");
        lines.Add($"Host lifecycle reason: {honesty.Lifecycle.Reason}");
        return new OperatorCommandResult(result.ExitCode, lines);
    }

    private static bool ExtractBooleanValue(string line)
    {
        var separator = line.IndexOf(':');
        if (separator < 0)
        {
            return false;
        }

        return bool.TryParse(line[(separator + 1)..].Trim(), out var value) && value;
    }

    private static OperatorCommandResult RunTaskCommand(RuntimeServices services, IReadOnlyList<string> arguments) => RunTaskCommandCore(services, arguments);

    private static OperatorCommandResult RunDiscussCommand(RuntimeServices services, IReadOnlyList<string> arguments) => RunDiscussCommandCore(services, arguments);
}

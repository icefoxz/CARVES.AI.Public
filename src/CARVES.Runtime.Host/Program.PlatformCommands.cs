using System.Collections.Generic;
using System.Linq;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Host;

public static partial class Program
{
    private static OperatorCommandResult RunRepoCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: repo <list|register|inspect> [...]");
        }

        return arguments[0] switch
        {
            "list" => services.OperatorSurfaceService.RepoList(),
            "register" => RunRepoRegister(services, arguments.Skip(1).ToArray()),
            "inspect" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: repo inspect <repo-id>")
                : services.OperatorSurfaceService.RepoInspect(arguments[1]),
            _ => OperatorCommandResult.Failure("Usage: repo <list|register|inspect> [...]"),
        };
    }

    private static OperatorCommandResult RunRepoRegister(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: repo register <repo-path> [--repo-id <id>] [--provider-profile <profile>] [--policy-profile <profile>]");
        }

        var repoPath = arguments[0];
        return services.OperatorSurfaceService.RepoRegister(
            ResolvePath(services.Paths.RepoRoot, repoPath),
            ResolveOption(arguments, "--repo-id"),
            ResolveOption(arguments, "--provider-profile"),
            ResolveOption(arguments, "--policy-profile"));
    }

    private static OperatorCommandResult RunRuntimeCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: runtime <list|inspect|start|resume|pause|stop|schedule|token-baseline|admit-pack|admit-pack-v1|assign-pack|rollback-pack|pin-current-pack|clear-pack-pin|export-pack-policy|preview-pack-policy|import-pack-policy> [...]");
        }

        var dryRun = arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase));
        var filtered = arguments.Where(argument => !string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase)).ToArray();
        return filtered[0] switch
        {
            "list" => services.OperatorSurfaceService.RuntimeList(),
            "inspect" => filtered.Length < 2
                ? OperatorCommandResult.Failure("Usage: runtime inspect <repo-id>")
                : services.OperatorSurfaceService.RuntimeInspect(filtered[1]),
            "start" => filtered.Length < 2
                ? OperatorCommandResult.Failure("Usage: runtime start <repo-id> [--dry-run]")
                : services.OperatorSurfaceService.RuntimeStart(filtered[1], dryRun),
            "resume" => filtered.Length < 3
                ? OperatorCommandResult.Failure("Usage: runtime resume <repo-id> <reason...>")
                : services.OperatorSurfaceService.RuntimeResume(filtered[1], string.Join(' ', filtered.Skip(2))),
            "pause" => filtered.Length < 3
                ? OperatorCommandResult.Failure("Usage: runtime pause <repo-id> <reason...>")
                : services.OperatorSurfaceService.RuntimePause(filtered[1], string.Join(' ', filtered.Skip(2))),
            "stop" => filtered.Length < 3
                ? OperatorCommandResult.Failure("Usage: runtime stop <repo-id> <reason...>")
                : services.OperatorSurfaceService.RuntimeStop(filtered[1], string.Join(' ', filtered.Skip(2))),
            "token-baseline" => RuntimeTokenBaselineCommandSupport.Run(services, filtered.Skip(1).ToArray()),
            "admit-pack" => RunRuntimeAdmitPack(services, filtered.Skip(1).ToArray()),
            "admit-pack-v1" => RunRuntimeAdmitPackV1(services, filtered.Skip(1).ToArray()),
            "assign-pack" => RunRuntimeAssignPack(services, filtered.Skip(1).ToArray()),
            "rollback-pack" => RunRuntimeRollbackPack(services, filtered.Skip(1).ToArray()),
            "pin-current-pack" => services.OperatorSurfaceService.RuntimePinCurrentPack(ResolveOption(filtered, "--reason")),
            "clear-pack-pin" => services.OperatorSurfaceService.RuntimeClearPackPin(ResolveOption(filtered, "--reason")),
            "export-pack-policy" => RunRuntimeExportPackPolicy(services, filtered.Skip(1).ToArray()),
            "preview-pack-policy" => RunRuntimePreviewPackPolicy(services, filtered.Skip(1).ToArray()),
            "import-pack-policy" => RunRuntimeImportPackPolicy(services, filtered.Skip(1).ToArray()),
            "schedule" => services.OperatorSurfaceService.RuntimeSchedule(ResolveSlots(filtered)),
            _ => OperatorCommandResult.Failure("Usage: runtime <list|inspect|start|resume|pause|stop|schedule|token-baseline|admit-pack|admit-pack-v1|assign-pack|rollback-pack|pin-current-pack|clear-pack-pin|export-pack-policy|preview-pack-policy|import-pack-policy> [...]"),
        };
    }

    private static OperatorCommandResult RunPackCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: pack <validate|admit|assign|pin|unpin|rollback|inspect|explain|audit|mismatch> [...]");
        }

        return arguments[0] switch
        {
            "validate" => RunValidateRuntimePackV1(services, arguments.Skip(1).ToArray()),
            "admit" => RunPackAdmit(services, arguments.Skip(1).ToArray()),
            "assign" => RunRuntimeAssignPack(services, arguments.Skip(1).ToArray()),
            "pin" => RunPackPin(services, arguments.Skip(1).ToArray()),
            "unpin" => RunPackUnpin(services, arguments.Skip(1).ToArray()),
            "rollback" => RunRuntimeRollbackPack(services, arguments.Skip(1).ToArray()),
            "inspect" => RunPackInspect(services, arguments.Skip(1).ToArray()),
            "explain" => RunPackExplain(services, arguments.Skip(1).ToArray()),
            "audit" => RunPackAudit(services, arguments.Skip(1).ToArray()),
            "mismatch" => RunPackMismatch(services, arguments.Skip(1).ToArray()),
            _ => OperatorCommandResult.Failure("Usage: pack <validate|admit|assign|pin|unpin|rollback|inspect|explain|audit|mismatch> [...]"),
        };
    }

    private static OperatorCommandResult RunPackPin(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count > 0 && ResolvePrimaryArgument(arguments, ["--reason"], []) is not null)
        {
            return OperatorCommandResult.Failure("Usage: pack pin [--reason <text>]");
        }

        return services.OperatorSurfaceService.RuntimePinCurrentPack(ResolveOption(arguments, "--reason"));
    }

    private static OperatorCommandResult RunPackAdmit(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (!string.IsNullOrWhiteSpace(ResolveOption(arguments, "--attribution")))
        {
            return RunRuntimeAdmitPack(services, arguments);
        }

        return RunRuntimeAdmitPackV1(services, arguments);
    }

    private static OperatorCommandResult RunPackUnpin(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count > 0 && ResolvePrimaryArgument(arguments, ["--reason"], []) is not null)
        {
            return OperatorCommandResult.Failure("Usage: pack unpin [--reason <text>]");
        }

        return services.OperatorSurfaceService.RuntimeClearPackPin(ResolveOption(arguments, "--reason"));
    }

    private static OperatorCommandResult RunPackInspect(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: pack inspect <admission|admission-policy|selection|switch-policy|policy-audit|policy-preview|policy-transfer|distribution-boundary>");
        }

        return arguments[0] switch
        {
            "admission" => services.OperatorSurfaceService.InspectRuntimePackAdmission(),
            "admission-policy" => services.OperatorSurfaceService.InspectRuntimePackAdmissionPolicy(),
            "selection" => services.OperatorSurfaceService.InspectRuntimePackSelection(),
            "switch-policy" => services.OperatorSurfaceService.InspectRuntimePackSwitchPolicy(),
            "policy-audit" => services.OperatorSurfaceService.InspectRuntimePackPolicyAudit(),
            "policy-preview" => services.OperatorSurfaceService.InspectRuntimePackPolicyPreview(),
            "policy-transfer" => services.OperatorSurfaceService.InspectRuntimePackPolicyTransfer(),
            "distribution-boundary" => services.OperatorSurfaceService.InspectRuntimePackDistributionBoundary(),
            _ => OperatorCommandResult.Failure("Usage: pack inspect <admission|admission-policy|selection|switch-policy|policy-audit|policy-preview|policy-transfer|distribution-boundary>"),
        };
    }

    private static OperatorCommandResult RunPackExplain(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var taskId = ResolveOption(arguments, "--task");
        if (string.IsNullOrWhiteSpace(taskId) || arguments.Count != 2)
        {
            return OperatorCommandResult.Failure("Usage: pack explain --task <task-id>");
        }

        return services.OperatorSurfaceService.InspectRuntimePackTaskExplainability(taskId);
    }

    private static OperatorCommandResult RunPackAudit(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 0)
        {
            return OperatorCommandResult.Failure("Usage: pack audit");
        }

        return services.OperatorSurfaceService.InspectRuntimePackExecutionAudit();
    }

    private static OperatorCommandResult RunPackMismatch(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 0)
        {
            return OperatorCommandResult.Failure("Usage: pack mismatch");
        }

        return services.OperatorSurfaceService.InspectRuntimePackMismatchDiagnostics();
    }

    private static OperatorCommandResult RunRuntimeAdmitPack(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: runtime admit-pack <pack-artifact-path> --attribution <runtime-pack-attribution-path>");
        }

        var packArgument = ResolvePrimaryArgument(arguments, [], ["--attribution"]);
        var attributionArgument = ResolveOption(arguments, "--attribution");
        if (string.IsNullOrWhiteSpace(packArgument) || string.IsNullOrWhiteSpace(attributionArgument))
        {
            return OperatorCommandResult.Failure("Usage: runtime admit-pack <pack-artifact-path> --attribution <runtime-pack-attribution-path>");
        }

        var packArtifactPath = ResolvePath(services.Paths.RepoRoot, packArgument);
        var attributionPath = ResolvePath(services.Paths.RepoRoot, attributionArgument);
        return services.OperatorSurfaceService.RuntimeAdmitPack(packArtifactPath, attributionPath);
    }

    private static OperatorCommandResult RunRuntimeAdmitPackV1(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: runtime admit-pack-v1 <runtime-pack-v1-manifest-path> [--channel <channel>] [--published-by <principal>] [--source-line <line>]");
        }

        var manifestArgument = ResolvePrimaryArgument(arguments, ["--channel", "--published-by", "--source-line"], []);
        if (string.IsNullOrWhiteSpace(manifestArgument))
        {
            return OperatorCommandResult.Failure("Usage: runtime admit-pack-v1 <runtime-pack-v1-manifest-path> [--channel <channel>] [--published-by <principal>] [--source-line <line>]");
        }

        return services.OperatorSurfaceService.RuntimeAdmitPackV1(
            ResolvePath(services.Paths.RepoRoot, manifestArgument),
            ResolveOption(arguments, "--channel"),
            ResolveOption(arguments, "--published-by"),
            ResolveOption(arguments, "--source-line"));
    }

    private static OperatorCommandResult RunRuntimeAssignPack(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: runtime assign-pack <pack-id> [--pack-version <version>] [--channel <channel>] [--reason <text>]");
        }

        var packId = ResolvePrimaryArgument(arguments, ["--pack-version", "--channel", "--reason"], []);
        if (string.IsNullOrWhiteSpace(packId))
        {
            return OperatorCommandResult.Failure("Usage: runtime assign-pack <pack-id> [--pack-version <version>] [--channel <channel>] [--reason <text>]");
        }

        return services.OperatorSurfaceService.RuntimeAssignPack(
            packId,
            ResolveOption(arguments, "--pack-version"),
            ResolveOption(arguments, "--channel"),
            ResolveOption(arguments, "--reason"));
    }

    private static OperatorCommandResult RunRuntimeRollbackPack(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: runtime rollback-pack <selection-id> [--reason <text>]");
        }

        var selectionId = ResolvePrimaryArgument(arguments, ["--reason"], []);
        if (string.IsNullOrWhiteSpace(selectionId))
        {
            return OperatorCommandResult.Failure("Usage: runtime rollback-pack <selection-id> [--reason <text>]");
        }

        return services.OperatorSurfaceService.RuntimeRollbackPack(selectionId, ResolveOption(arguments, "--reason"));
    }

    private static OperatorCommandResult RunRuntimeExportPackPolicy(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 1)
        {
            return OperatorCommandResult.Failure("Usage: runtime export-pack-policy <output-path>");
        }

        return services.OperatorSurfaceService.RuntimeExportPackPolicy(ResolvePath(services.Paths.RepoRoot, arguments[0]));
    }

    private static OperatorCommandResult RunRuntimePreviewPackPolicy(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 1)
        {
            return OperatorCommandResult.Failure("Usage: runtime preview-pack-policy <input-path>");
        }

        return services.OperatorSurfaceService.RuntimePreviewPackPolicy(ResolvePath(services.Paths.RepoRoot, arguments[0]));
    }

    private static OperatorCommandResult RunRuntimeImportPackPolicy(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 1)
        {
            return OperatorCommandResult.Failure("Usage: runtime import-pack-policy <input-path>");
        }

        return services.OperatorSurfaceService.RuntimeImportPackPolicy(ResolvePath(services.Paths.RepoRoot, arguments[0]));
    }

    private static OperatorCommandResult RunProviderCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: provider <list|inspect|bind|quota|route> [...]");
        }

        return arguments[0] switch
        {
            "list" => services.OperatorSurfaceService.ProviderList(),
            "inspect" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: provider inspect <provider-id>")
                : services.OperatorSurfaceService.ProviderInspect(arguments[1]),
            "bind" => arguments.Count < 3
                ? OperatorCommandResult.Failure("Usage: provider bind <repo-id> <profile-id>")
                : services.OperatorSurfaceService.ProviderBind(arguments[1], arguments[2]),
            "quota" => services.OperatorSurfaceService.ProviderQuota(),
            "route" => arguments.Count < 3
                ? OperatorCommandResult.Failure("Usage: provider route <repo-id> <role> [--no-fallback]")
                : services.OperatorSurfaceService.ProviderRoute(arguments[1], arguments[2], !arguments.Any(argument => string.Equals(argument, "--no-fallback", StringComparison.OrdinalIgnoreCase))),
            _ => OperatorCommandResult.Failure("Usage: provider <list|inspect|bind|quota|route> [...]"),
        };
    }

    private static OperatorCommandResult RunGovernanceCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: governance <show|inspect> [...]");
        }

        return arguments[0] switch
        {
            "show" => services.OperatorSurfaceService.GovernanceShow(),
            "report" => services.OperatorSurfaceService.GovernanceReport(ResolveOptionalPositiveInt(arguments, "--hours", defaultValue: 24)),
            "inspect" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: governance inspect <repo-id>")
                : services.OperatorSurfaceService.GovernanceInspect(arguments[1]),
            _ => OperatorCommandResult.Failure("Usage: governance <show|report|inspect> [...]"),
        };
    }

    private static OperatorCommandResult RunWorkerCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: worker <list|providers|profiles|select|health|policy|activate-external-app-cli|activate-external-codex|supervisor-launch|supervisor-instances|supervisor-events|supervisor-archive|approvals|approve|deny|timeout|audit|incidents|leases|heartbeat|quarantine|expire> [...]");
        }

        return arguments[0] switch
        {
            "list" => services.OperatorSurfaceService.WorkerList(),
            "providers" => services.OperatorSurfaceService.WorkerProviders(),
            "profiles" => services.OperatorSurfaceService.WorkerProfiles(arguments.Count >= 2 ? arguments[1] : null),
            "summary" => services.OperatorSurfaceService.WorkerSummary(),
            "select" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: worker select <repo-id> [--task-id <task-id>]")
                : services.OperatorSurfaceService.WorkerSelection(arguments[1], ResolveOption(arguments, "--task-id")),
            "health" => services.OperatorSurfaceService.WorkerHealth(ResolveOption(arguments, "--backend"), !arguments.Any(argument => string.Equals(argument, "--no-refresh", StringComparison.OrdinalIgnoreCase))),
            "policy" => services.OperatorSurfaceService.WorkerApprovalPolicy(arguments.Count >= 2 ? arguments[1] : null),
            "activate-external-codex" => services.OperatorSurfaceService.WorkerActivateExternalCodex(
                arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase)),
                ResolveOption(arguments, "--reason")),
            "activate-external-app-cli" => services.OperatorSurfaceService.WorkerActivateExternalAppCli(
                arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase)),
                ResolveOption(arguments, "--reason")),
            "supervisor-launch" => services.OperatorSurfaceService.WorkerSupervisorLaunch(
                ResolveOption(arguments, "--repo-id"),
                ResolveOption(arguments, "--identity"),
                ResolveOption(arguments, "--worker-instance-id"),
                ResolveOption(arguments, "--actor-session-id"),
                ResolveOption(arguments, "--provider-profile"),
                ResolveOption(arguments, "--capability-profile"),
                ResolveOption(arguments, "--schedule-binding"),
                arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase)),
                ResolveOption(arguments, "--reason")),
            "supervisor-instances" => services.OperatorSurfaceService.WorkerSupervisorInstances(ResolveOption(arguments, "--repo-id")),
            "supervisor-events" => services.OperatorSurfaceService.WorkerSupervisorEvents(
                ResolveOption(arguments, "--repo-id"),
                ResolveOption(arguments, "--worker-instance-id"),
                ResolveOption(arguments, "--actor-session-id")),
            "supervisor-archive" => services.OperatorSurfaceService.WorkerSupervisorArchive(
                ResolveOption(arguments, "--worker-instance-id"),
                arguments.Any(argument => string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase)),
                ResolveOption(arguments, "--reason")),
            "approvals" => services.OperatorSurfaceService.WorkerApprovals(),
            "approve" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: worker approve <permission-request-id> [actor-identity]")
                : services.OperatorSurfaceService.WorkerApprove(arguments[1], arguments.Count >= 3 ? arguments[2] : "operator"),
            "deny" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: worker deny <permission-request-id> [actor-identity]")
                : services.OperatorSurfaceService.WorkerDeny(arguments[1], arguments.Count >= 3 ? arguments[2] : "operator"),
            "timeout" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: worker timeout <permission-request-id> [actor-identity]")
                : services.OperatorSurfaceService.WorkerTimeout(arguments[1], arguments.Count >= 3 ? arguments[2] : "operator"),
            "audit" => services.OperatorSurfaceService.WorkerPermissionAudit(ResolveOption(arguments, "--task-id"), ResolveOption(arguments, "--request-id")),
            "incidents" => services.OperatorSurfaceService.WorkerIncidents(ResolveOption(arguments, "--task-id"), ResolveOption(arguments, "--run-id")),
            "leases" => services.OperatorSurfaceService.WorkerLeases(),
            "heartbeat" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: worker heartbeat <node-id>")
                : services.OperatorSurfaceService.WorkerHeartbeat(arguments[1]),
            "quarantine" => arguments.Count < 3
                ? OperatorCommandResult.Failure("Usage: worker quarantine <node-id> <reason...>")
                : services.OperatorSurfaceService.WorkerQuarantine(arguments[1], string.Join(' ', arguments.Skip(2))),
            "expire" => arguments.Count < 3
                ? OperatorCommandResult.Failure("Usage: worker expire <lease-id> <reason...>")
                : services.OperatorSurfaceService.WorkerExpireLease(arguments[1], string.Join(' ', arguments.Skip(2))),
            _ => OperatorCommandResult.Failure("Usage: worker <list|providers|profiles|summary|select|health|policy|activate-external-codex|supervisor-launch|supervisor-instances|supervisor-events|supervisor-archive|approvals|approve|deny|timeout|audit|incidents|leases|heartbeat|quarantine|expire> [...]"),
        };
    }

    private static OperatorCommandResult RunQualificationCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: qualification <run|materialize-candidate|promote-candidate> [...]");
        }

        return arguments[0] switch
        {
            "run" => services.OperatorSurfaceService.RunQualification(ResolveOptionalInt(arguments, "--attempts")),
            "materialize-candidate" => services.OperatorSurfaceService.MaterializeQualificationCandidate(),
            "promote-candidate" => services.OperatorSurfaceService.PromoteQualificationCandidate(arguments.Count >= 2 ? arguments[1] : null),
            _ => OperatorCommandResult.Failure("Usage: qualification <run|materialize-candidate|promote-candidate> [...]"),
        };
    }

    private static OperatorCommandResult RunValidationCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: validation <run|run-suite|summary> [...]");
        }

        return arguments[0] switch
        {
            "run" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: validation run <task-id> [--mode <baseline|routing|forced-fallback>] [--repo-id <repo-id>]")
                : services.OperatorSurfaceService.RunValidationTask(arguments[1], ParseRoutingValidationMode(ResolveOption(arguments, "--mode")), ResolveOption(arguments, "--repo-id")),
            "run-suite" => services.OperatorSurfaceService.RunValidationSuite(
                ParseRoutingValidationMode(ResolveOption(arguments, "--mode")),
                ResolveOptionalInt(arguments, "--limit"),
                ResolveOption(arguments, "--repo-id")),
            "summary" => services.OperatorSurfaceService.InspectValidationSummary(ResolveOption(arguments, "--run-id")),
            _ => OperatorCommandResult.Failure("Usage: validation <run|run-suite|summary> [...]"),
        };
    }

    private static OperatorCommandResult RunValidateCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: validate <card|task|safety|memory|pack-artifact|runtime-pack-v1|runtime-pack-attribution|runtime-pack-policy-package> [...]");
        }

        return arguments[0].ToLowerInvariant() switch
        {
            "card" => RunValidateCard(services, arguments.Skip(1).ToArray()),
            "task" => RunValidateTask(services, arguments.Skip(1).ToArray()),
            "safety" => RunValidateSafety(services, arguments.Skip(1).ToArray()),
            "memory" => RunValidateMemory(services, arguments.Skip(1).ToArray()),
            "pack-artifact" => RunValidatePackArtifact(services, arguments.Skip(1).ToArray()),
            "runtime-pack-v1" => RunValidateRuntimePackV1(services, arguments.Skip(1).ToArray()),
            "runtime-pack-attribution" => RunValidateRuntimePackAttribution(services, arguments.Skip(1).ToArray()),
            "runtime-pack-policy-package" => RunValidateRuntimePackPolicyPackage(services, arguments.Skip(1).ToArray()),
            _ => OperatorCommandResult.Failure("Usage: validate <card|task|safety|memory|pack-artifact|runtime-pack-v1|runtime-pack-attribution|runtime-pack-policy-package> [...]"),
        };
    }

    private static OperatorCommandResult RunValidateCard(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: validate card <card-path> [--strict]");
        }

        var strict = arguments.Any(argument => string.Equals(argument, "--strict", StringComparison.OrdinalIgnoreCase));
        var cardArgument = ResolvePrimaryArgument(arguments, [], ["--strict"]);
        if (string.IsNullOrWhiteSpace(cardArgument))
        {
            return OperatorCommandResult.Failure("Usage: validate card <card-path> [--strict]");
        }

        var cardPath = ResolvePath(services.Paths.RepoRoot, cardArgument);
        return services.OperatorSurfaceService.ValidateCard(cardPath, strict);
    }

    private static OperatorCommandResult RunValidateTask(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: validate task <task-path>");
        }

        var taskArgument = ResolvePrimaryArgument(arguments, [], []);
        if (string.IsNullOrWhiteSpace(taskArgument))
        {
            return OperatorCommandResult.Failure("Usage: validate task <task-path>");
        }

        var taskPath = ResolvePath(services.Paths.RepoRoot, taskArgument);
        return services.OperatorSurfaceService.ValidateTask(taskPath);
    }

    private static OperatorCommandResult RunValidateMemory(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: validate memory <memory-meta-path> [--strict]");
        }

        var strict = arguments.Any(argument => string.Equals(argument, "--strict", StringComparison.OrdinalIgnoreCase));
        var memoryArgument = ResolvePrimaryArgument(arguments, [], ["--strict"]);
        if (string.IsNullOrWhiteSpace(memoryArgument))
        {
            return OperatorCommandResult.Failure("Usage: validate memory <memory-meta-path> [--strict]");
        }

        var memoryPath = ResolvePath(services.Paths.RepoRoot, memoryArgument);
        return services.OperatorSurfaceService.ValidateMemory(memoryPath, strict);
    }

    private static OperatorCommandResult RunValidatePackArtifact(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: validate pack-artifact <json-path>");
        }

        var pathArgument = ResolvePrimaryArgument(arguments, [], []);
        if (string.IsNullOrWhiteSpace(pathArgument))
        {
            return OperatorCommandResult.Failure("Usage: validate pack-artifact <json-path>");
        }

        var jsonPath = ResolvePath(services.Paths.RepoRoot, pathArgument);
        return services.OperatorSurfaceService.ValidatePackArtifact(jsonPath);
    }

    private static OperatorCommandResult RunValidateRuntimePackV1(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: validate runtime-pack-v1 <json-path>");
        }

        var pathArgument = ResolvePrimaryArgument(arguments, [], []);
        if (string.IsNullOrWhiteSpace(pathArgument))
        {
            return OperatorCommandResult.Failure("Usage: validate runtime-pack-v1 <json-path>");
        }

        var jsonPath = ResolvePath(services.Paths.RepoRoot, pathArgument);
        return services.OperatorSurfaceService.ValidateRuntimePackV1(jsonPath);
    }

    private static OperatorCommandResult RunValidateRuntimePackAttribution(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: validate runtime-pack-attribution <json-path>");
        }

        var pathArgument = ResolvePrimaryArgument(arguments, [], []);
        if (string.IsNullOrWhiteSpace(pathArgument))
        {
            return OperatorCommandResult.Failure("Usage: validate runtime-pack-attribution <json-path>");
        }

        var jsonPath = ResolvePath(services.Paths.RepoRoot, pathArgument);
        return services.OperatorSurfaceService.ValidateRuntimePackAttribution(jsonPath);
    }

    private static OperatorCommandResult RunValidateRuntimePackPolicyPackage(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: validate runtime-pack-policy-package <json-path>");
        }

        var pathArgument = ResolvePrimaryArgument(arguments, [], []);
        if (string.IsNullOrWhiteSpace(pathArgument))
        {
            return OperatorCommandResult.Failure("Usage: validate runtime-pack-policy-package <json-path>");
        }

        var jsonPath = ResolvePath(services.Paths.RepoRoot, pathArgument);
        return services.OperatorSurfaceService.ValidateRuntimePackPolicyPackage(jsonPath);
    }

    private static OperatorCommandResult RunValidateSafety(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var actor = ResolveOption(arguments, "--actor");
        var operation = ResolveValidateSafetyOperation(arguments);
        var paths = ResolveValidateSafetyPaths(arguments);
        if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(operation) || paths.Count == 0)
        {
            return OperatorCommandResult.Failure(
                "Usage: validate safety --actor <planner|worker|operator> --op <read|write|delete|execute> [--path <target-path>] [<target-path> ...]");
        }

        var resolvedPaths = paths
            .Select(path => ResolvePath(services.Paths.RepoRoot, path))
            .ToArray();
        return services.OperatorSurfaceService.ValidateSafety(actor, operation, resolvedPaths);
    }

    private static OperatorCommandResult RunApiCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        return RunApiCommandCore(services, arguments);
    }

    private static OperatorCommandResult RunDashboardCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        return services.OperatorSurfaceService.Dashboard(arguments.Count == 0 ? null : arguments[0]);
    }

    private static OperatorCommandResult RunWorkbenchCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        return LocalHostCommandDispatcher.Dispatch(services, "workbench", arguments);
    }
}

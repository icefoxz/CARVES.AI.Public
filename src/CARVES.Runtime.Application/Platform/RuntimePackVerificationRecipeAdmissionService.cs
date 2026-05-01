using System.Text.Json;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimePackVerificationRecipeAdmissionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private static readonly string[] SourcePolicyRefs =
    [
        "docs/product/runtime-pack-command-admission-v1.md",
        "docs/contracts/runtime-pack-command-admission.schema.json",
    ];

    private static readonly HashSet<string> ShellExecutables = new(StringComparer.OrdinalIgnoreCase)
    {
        "bash",
        "sh",
        "zsh",
        "pwsh",
        "powershell",
        "cmd",
        "/bin/bash",
        "/bin/sh",
    };

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly IRuntimeArtifactRepository artifactRepository;

    public RuntimePackVerificationRecipeAdmissionService(
        string repoRoot,
        ControlPlanePaths paths,
        IRuntimeArtifactRepository artifactRepository)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.paths = paths;
        this.artifactRepository = artifactRepository;
    }

    public RuntimePackVerificationRecipeAdmissionResult Resolve(
        TaskNode task,
        IReadOnlyList<IReadOnlyList<string>> baseValidationCommands)
    {
        var normalizedBaseCommands = NormalizeCommands(baseValidationCommands);
        if (!task.Metadata.TryGetValue("execution_run_active_id", out var runId)
            || string.IsNullOrWhiteSpace(runId))
        {
            return RuntimePackVerificationRecipeAdmissionResult.None(
                normalizedBaseCommands,
                "Runtime Pack verification recipe admission skipped because no active execution run id was present on task metadata.");
        }

        var selection = artifactRepository.TryLoadCurrentRuntimePackSelectionArtifact();
        if (selection is null
            || !string.Equals(selection.AdmissionSource.AssignmentMode, "overlay_assignment", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(selection.AdmissionSource.AssignmentRef))
        {
            return RuntimePackVerificationRecipeAdmissionResult.None(
                normalizedBaseCommands,
                "Runtime Pack verification recipe admission skipped because no selected declarative pack overlay assignment is active.");
        }

        var manifestPath = ResolveManifestPath(selection.AdmissionSource.AssignmentRef!);
        if (manifestPath is null || !File.Exists(manifestPath))
        {
            return RuntimePackVerificationRecipeAdmissionResult.None(
                normalizedBaseCommands,
                "Runtime Pack verification recipe admission skipped because the selected declarative pack manifest could not be resolved on disk.");
        }

        RuntimePackV1VerificationManifestDocument? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<RuntimePackV1VerificationManifestDocument>(File.ReadAllText(manifestPath), JsonOptions);
        }
        catch (JsonException)
        {
            return RuntimePackVerificationRecipeAdmissionResult.None(
                normalizedBaseCommands,
                "Runtime Pack verification recipe admission skipped because the selected declarative pack manifest could not be deserialized.");
        }

        if (manifest is null
            || !manifest.CapabilityKinds.Contains("verification_recipe", StringComparer.Ordinal)
            || manifest.Recipes.VerificationRecipes.Length == 0)
        {
            return RuntimePackVerificationRecipeAdmissionResult.None(
                normalizedBaseCommands,
                "Runtime Pack verification recipe admission skipped because the selected declarative pack does not contribute verification recipes.");
        }

        var admittedCommands = new List<IReadOnlyList<string>>();
        var recipeIds = new List<string>();
        var decisionIds = new List<string>();
        var decisionPaths = new List<string>();
        var admittedCount = 0;
        var elevatedRiskCount = 0;
        var blockedCount = 0;
        var rejectedCount = 0;

        foreach (var recipe in manifest.Recipes.VerificationRecipes)
        {
            recipeIds.Add(recipe.Id);
            foreach (var command in recipe.Commands)
            {
                var decision = BuildDecision(task.TaskId, runId.Trim(), selection, recipe, command);
                var decisionPath = PersistDecision(task.TaskId, runId.Trim(), recipe.Id, command.Id, decision);
                decisionIds.Add(decision.DecisionId);
                decisionPaths.Add(decisionPath);

                switch (decision.Decision.Verdict)
                {
                    case "admitted":
                        admittedCount += 1;
                        admittedCommands.Add(ToCommand(command));
                        break;
                    case "admitted_with_elevated_risk":
                        elevatedRiskCount += 1;
                        admittedCommands.Add(ToCommand(command));
                        break;
                    case "blocked":
                        blockedCount += 1;
                        break;
                    default:
                        rejectedCount += 1;
                        break;
                }
            }
        }

        var effectiveCommands = MergeCommands(normalizedBaseCommands, admittedCommands);
        return new RuntimePackVerificationRecipeAdmissionResult(
            effectiveCommands,
            recipeIds.Distinct(StringComparer.Ordinal).ToArray(),
            decisionIds,
            decisionPaths,
            admittedCount,
            elevatedRiskCount,
            blockedCount,
            rejectedCount,
            decisionIds.Count > 0,
            $"Runtime Pack verification recipe admission evaluated {decisionIds.Count} command(s) for {selection.PackId}@{selection.PackVersion} ({selection.Channel}); admitted={admittedCount}, elevated={elevatedRiskCount}, blocked={blockedCount}, rejected={rejectedCount}.");
    }

    private RuntimePackCommandAdmissionDecision BuildDecision(
        string taskId,
        string runId,
        SurfaceModels.RuntimePackSelectionArtifact selection,
        RuntimePackV1VerificationRecipeDocument recipe,
        RuntimePackV1VerificationCommandDocument command)
    {
        var effectivePermissions = BuildEffectivePermissions(command);
        var evidence = BuildEvidenceExpectation(command);
        var outcome = BuildDecisionOutcome(command);

        return new RuntimePackCommandAdmissionDecision
        {
            DecisionId = $"packcmdadm-{Guid.NewGuid():N}",
            TaskId = taskId,
            RunId = runId,
            PackSelectionId = selection.SelectionId,
            PackId = selection.PackId,
            PackVersion = selection.PackVersion,
            Channel = selection.Channel,
            CommandRef = new RuntimePackCommandRef
            {
                RecipeId = recipe.Id,
                CommandId = command.Id,
            },
            RequestedKind = command.Kind,
            Decision = outcome,
            Command = new RuntimePackCommandPayload
            {
                Executable = command.Executable,
                Args = command.Args,
                Cwd = command.Cwd,
                Required = command.Required,
            },
            EffectivePermissions = effectivePermissions,
            Evidence = evidence,
            SourcePolicyRefs = SourcePolicyRefs,
        };
    }

    private static RuntimePackCommandDecisionOutcome BuildDecisionOutcome(RuntimePackV1VerificationCommandDocument command)
    {
        if (IsShellRejected(command))
        {
            return new RuntimePackCommandDecisionOutcome
            {
                Verdict = "rejected",
                Basis = "hard_deny",
                RiskLevel = "rejected_shell",
                StopReasons = ["shell_wrapper_forbidden"],
            };
        }

        if (ContainsFreeFormShellTokens(command.Args))
        {
            return new RuntimePackCommandDecisionOutcome
            {
                Verdict = "rejected",
                Basis = "hard_deny",
                RiskLevel = "rejected_shell",
                StopReasons = ["free_form_shell_tokens_forbidden"],
            };
        }

        return command.Kind switch
        {
            "known_tool_command" => new RuntimePackCommandDecisionOutcome
            {
                Verdict = "admitted",
                Basis = "default_policy",
                RiskLevel = "l3_known_tool",
            },
            "package_manager_script" => new RuntimePackCommandDecisionOutcome
            {
                Verdict = "admitted_with_elevated_risk",
                Basis = "default_policy",
                RiskLevel = "l3_package_script",
                StopReasons = ["package_manager_script_elevated_risk"],
            },
            "repo_script" => new RuntimePackCommandDecisionOutcome
            {
                Verdict = "blocked",
                Basis = "default_policy",
                RiskLevel = "blocked_repo_script",
                StopReasons = ["repo_script_requires_repo_policy"],
            },
            _ => new RuntimePackCommandDecisionOutcome
            {
                Verdict = "rejected",
                Basis = "hard_deny",
                RiskLevel = "rejected_shell",
                StopReasons = ["shell_command_forbidden"],
            },
        };
    }

    private static RuntimePackCommandEffectivePermissions BuildEffectivePermissions(RuntimePackV1VerificationCommandDocument command)
    {
        return new RuntimePackCommandEffectivePermissions
        {
            Network = false,
            Env = new RuntimePackCommandEnvironmentPermissions
            {
                Mode = "none",
                Allowed = Array.Empty<string>(),
            },
            Secrets = false,
            Writes = new RuntimePackCommandWritePermissions
            {
                AllowedPaths = InferAllowedWritePaths(command),
                ProtectedRoots = "deny",
            },
        };
    }

    private static RuntimePackCommandEvidenceExpectation BuildEvidenceExpectation(RuntimePackV1VerificationCommandDocument command)
    {
        return new RuntimePackCommandEvidenceExpectation
        {
            ExpectedArtifacts = InferExpectedArtifacts(command),
            FailureIsBlocking = command.Required,
        };
    }

    private string PersistDecision(
        string taskId,
        string runId,
        string recipeId,
        string commandId,
        RuntimePackCommandAdmissionDecision decision)
    {
        var taskRoot = Path.Combine(paths.AiRoot, "runtime", "runs", taskId);
        Directory.CreateDirectory(taskRoot);
        var fileName = $"{runId}.{Sanitize(recipeId)}.{Sanitize(commandId)}.pack-command-admission.json";
        var fullPath = Path.Combine(taskRoot, fileName);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(decision, JsonOptions));
        return ToRepoRelativeOrAbsolute(fullPath);
    }

    private string? ResolveManifestPath(string assignmentRef)
    {
        var normalized = assignmentRef.Replace('/', Path.DirectorySeparatorChar);
        var rooted = Path.IsPathRooted(normalized)
            ? Path.GetFullPath(normalized)
            : Path.GetFullPath(Path.Combine(paths.RepoRoot, normalized));
        if (!rooted.StartsWith(repoRoot, StringComparison.Ordinal))
        {
            return null;
        }

        return rooted;
    }

    private static bool IsShellRejected(RuntimePackV1VerificationCommandDocument command)
    {
        if (string.Equals(command.Kind, "shell_command", StringComparison.Ordinal))
        {
            return true;
        }

        return ShellExecutables.Contains(command.Executable);
    }

    private static bool ContainsFreeFormShellTokens(IReadOnlyList<string> args)
    {
        foreach (var arg in args)
        {
            if (arg.Contains('|', StringComparison.Ordinal)
                || arg.Contains("&&", StringComparison.Ordinal)
                || arg.Contains("||", StringComparison.Ordinal)
                || arg.Contains(';', StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> InferAllowedWritePaths(RuntimePackV1VerificationCommandDocument command)
    {
        if (LooksLikeNoEmitCommand(command) || LooksLikeLintCommand(command))
        {
            return Array.Empty<string>();
        }

        if (LooksLikeTestCommand(command))
        {
            return ["TestResults/**", "coverage/**", "test-results/**"];
        }

        if (LooksLikeBuildCommand(command))
        {
            return ["bin/**", "obj/**", "dist/**", "build/**"];
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyList<string> InferExpectedArtifacts(RuntimePackV1VerificationCommandDocument command)
    {
        if (LooksLikeTestCommand(command))
        {
            return ["test-log"];
        }

        if (LooksLikeLintCommand(command))
        {
            return ["lint-log"];
        }

        if (LooksLikeNoEmitCommand(command))
        {
            return ["typecheck-log"];
        }

        if (LooksLikeBuildCommand(command))
        {
            return ["build-log"];
        }

        return ["verification-log"];
    }

    private static bool LooksLikeBuildCommand(RuntimePackV1VerificationCommandDocument command)
    {
        return CommandParts(command).Any(part => string.Equals(part, "build", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeTestCommand(RuntimePackV1VerificationCommandDocument command)
    {
        return CommandParts(command).Any(part =>
            string.Equals(part, "test", StringComparison.OrdinalIgnoreCase)
            || part.Contains("test", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeLintCommand(RuntimePackV1VerificationCommandDocument command)
    {
        return CommandParts(command).Any(part =>
            string.Equals(part, "lint", StringComparison.OrdinalIgnoreCase)
            || part.Contains("lint", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeNoEmitCommand(RuntimePackV1VerificationCommandDocument command)
    {
        return CommandParts(command).Any(part =>
            string.Equals(part, "tsc", StringComparison.OrdinalIgnoreCase)
            || string.Equals(part, "--noEmit", StringComparison.OrdinalIgnoreCase)
            || part.Contains("typecheck", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> CommandParts(RuntimePackV1VerificationCommandDocument command)
    {
        yield return command.Executable;
        foreach (var arg in command.Args)
        {
            yield return arg;
        }
    }

    private static IReadOnlyList<IReadOnlyList<string>> NormalizeCommands(IReadOnlyList<IReadOnlyList<string>> commands)
    {
        return commands
            .Select(command => (IReadOnlyList<string>)command
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part.Trim())
                .ToArray())
            .Where(command => command.Count > 0)
            .ToArray();
    }

    private static IReadOnlyList<IReadOnlyList<string>> MergeCommands(
        IReadOnlyList<IReadOnlyList<string>> baseCommands,
        IReadOnlyList<IReadOnlyList<string>> additionalCommands)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = new List<IReadOnlyList<string>>();

        foreach (var command in baseCommands.Concat(additionalCommands))
        {
            var normalized = NormalizeCommandKey(command);
            if (!seen.Add(normalized))
            {
                continue;
            }

            merged.Add(command);
        }

        return merged;
    }

    private static string NormalizeCommandKey(IReadOnlyList<string> command)
    {
        return string.Join('\u001f', command.Select(part => part.Trim()));
    }

    private static IReadOnlyList<string> ToCommand(RuntimePackV1VerificationCommandDocument command)
    {
        return [command.Executable, .. command.Args];
    }

    private static string Sanitize(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '.' or '_' or '-'
                ? character
                : '-');
        }

        return builder.ToString().Trim('-');
    }

    private string ToRepoRelativeOrAbsolute(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(repoRoot, fullPath);
        if (relative.StartsWith("..", StringComparison.Ordinal))
        {
            return fullPath.Replace('\\', '/');
        }

        return relative.Replace('\\', '/');
    }

    private sealed class RuntimePackV1VerificationManifestDocument
    {
        public string[] CapabilityKinds { get; init; } = [];

        public RuntimePackV1VerificationRecipesDocument Recipes { get; init; } = new();
    }

    private sealed class RuntimePackV1VerificationRecipesDocument
    {
        public RuntimePackV1VerificationRecipeDocument[] VerificationRecipes { get; init; } = [];
    }

    private sealed class RuntimePackV1VerificationRecipeDocument
    {
        public string Id { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public RuntimePackV1VerificationCommandDocument[] Commands { get; init; } = [];
    }

    private sealed class RuntimePackV1VerificationCommandDocument
    {
        public string Id { get; init; } = string.Empty;

        public string Kind { get; init; } = string.Empty;

        public string Executable { get; init; } = string.Empty;

        public string[] Args { get; init; } = [];

        public string Cwd { get; init; } = ".";

        public bool Required { get; init; }
    }
}

public sealed class RuntimePackVerificationRecipeAdmissionResult
{
    public RuntimePackVerificationRecipeAdmissionResult(
        IReadOnlyList<IReadOnlyList<string>> effectiveValidationCommands,
        IReadOnlyList<string> recipeIds,
        IReadOnlyList<string> decisionIds,
        IReadOnlyList<string> decisionPaths,
        int admittedCommandCount,
        int elevatedRiskCommandCount,
        int blockedCommandCount,
        int rejectedCommandCount,
        bool hasRuntimePackContribution,
        string summary)
    {
        EffectiveValidationCommands = effectiveValidationCommands;
        RecipeIds = recipeIds;
        DecisionIds = decisionIds;
        DecisionPaths = decisionPaths;
        AdmittedCommandCount = admittedCommandCount;
        ElevatedRiskCommandCount = elevatedRiskCommandCount;
        BlockedCommandCount = blockedCommandCount;
        RejectedCommandCount = rejectedCommandCount;
        HasRuntimePackContribution = hasRuntimePackContribution;
        Summary = summary;
    }

    public IReadOnlyList<IReadOnlyList<string>> EffectiveValidationCommands { get; }

    public IReadOnlyList<string> RecipeIds { get; }

    public IReadOnlyList<string> DecisionIds { get; }

    public IReadOnlyList<string> DecisionPaths { get; }

    public int AdmittedCommandCount { get; }

    public int ElevatedRiskCommandCount { get; }

    public int BlockedCommandCount { get; }

    public int RejectedCommandCount { get; }

    public bool HasRuntimePackContribution { get; }

    public string Summary { get; }

    public static RuntimePackVerificationRecipeAdmissionResult None(
        IReadOnlyList<IReadOnlyList<string>> effectiveValidationCommands,
        string summary)
    {
        return new RuntimePackVerificationRecipeAdmissionResult(
            effectiveValidationCommands,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            0,
            0,
            0,
            0,
            hasRuntimePackContribution: false,
            summary);
    }
}

using System.Text.Json;

namespace Carves.Runtime.Cli;

internal static partial class FriendlyCliApplication
{
    private static readonly JsonSerializerOptions DoctorJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private static int RunDoctor(string? explicitRepoRoot, IReadOnlyList<string> arguments)
    {
        var wantsJson = arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase));
        var workingDirectory = Directory.GetCurrentDirectory();
        var resolvedRepoRoot = RepoLocator.Resolve(explicitRepoRoot, workingDirectory);
        var repoExists = !string.IsNullOrWhiteSpace(resolvedRepoRoot) && Directory.Exists(resolvedRepoRoot);
        var isWorkspace = repoExists && RepoLocator.IsRepositoryWorkspace(resolvedRepoRoot!);
        var runtimeJsonPresent = isWorkspace && File.Exists(Path.Combine(resolvedRepoRoot!, ".ai", "runtime.json"));
        var aiDirectoryPresent = isWorkspace && Directory.Exists(Path.Combine(resolvedRepoRoot!, ".ai"));
        var hostReadiness = "not_checked";
        var hostOperationalState = "not_checked";
        var hostConflictPresent = false;
        var hostSafeToStartNewHost = false;
        var hostRecommendedActionKind = string.Empty;
        var hostRecommendedAction = string.Empty;
        var hostLifecycleState = "not_checked";
        var hostLifecycleReason = "not_checked";
        if (isWorkspace)
        {
            var hostProjection = ResolveFriendlyHostProjection(resolvedRepoRoot!);
            hostReadiness = hostProjection.HostReadiness;
            hostOperationalState = hostProjection.HostOperationalState;
            hostConflictPresent = hostProjection.ConflictPresent;
            hostSafeToStartNewHost = hostProjection.SafeToStartNewHost;
            hostRecommendedActionKind = hostProjection.RecommendedActionKind;
            hostRecommendedAction = hostProjection.RecommendedAction;
            hostLifecycleState = string.IsNullOrWhiteSpace(hostProjection.LifecycleState) ? "unknown" : hostProjection.LifecycleState;
            hostLifecycleReason = string.IsNullOrWhiteSpace(hostProjection.LifecycleReason) ? hostProjection.HostReadiness : hostProjection.LifecycleReason;
        }

        var targetRepo = ResolveTargetRepoState(repoExists, isWorkspace);
        var targetRepoReadiness = ResolveTargetRepoReadiness(repoExists, isWorkspace, aiDirectoryPresent, runtimeJsonPresent);
        var gaps = ResolveDoctorGaps(targetRepo, targetRepoReadiness, hostReadiness, hostConflictPresent);
        var nextAction = ResolveDoctorNextAction(targetRepo, targetRepoReadiness, hostReadiness, hostConflictPresent, hostRecommendedAction);
        var readiness = new DoctorReadiness(
            SchemaVersion: "carves-doctor.v1",
            ToolReadiness: "available",
            CommandEntry: "carves",
            CliVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
            WorkingDirectory: workingDirectory,
            TargetRepo: targetRepo,
            TargetRepoPath: isWorkspace ? resolvedRepoRoot : null,
            TargetRepoReadiness: targetRepoReadiness,
            RuntimeReadiness: runtimeJsonPresent ? "initialized" : isWorkspace ? "missing" : "not_checked",
            HostReadiness: hostReadiness,
            HostOperationalState: hostOperationalState,
            HostConflictPresent: hostConflictPresent,
            HostSafeToStartNewHost: hostSafeToStartNewHost,
            HostRecommendedActionKind: hostRecommendedActionKind,
            HostRecommendedAction: hostRecommendedAction,
            HostLifecycleState: hostLifecycleState,
            HostLifecycleReason: hostLifecycleReason,
            StatusScope: "target_repo_status_only",
            AgentHandoffCommand: "carves agent handoff",
            NextAction: nextAction,
            IsReady: gaps.Count == 0,
            Gaps: gaps);

        if (wantsJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(readiness, DoctorJsonOptions));
            return readiness.IsReady ? 0 : 1;
        }

        RenderDoctor(readiness);
        return readiness.IsReady ? 0 : 1;
    }

    private static string ResolveTargetRepoState(bool repoExists, bool isWorkspace)
    {
        if (!repoExists)
        {
            return "not_found";
        }

        return isWorkspace ? "found" : "not_repository_workspace";
    }

    private static string ResolveTargetRepoReadiness(bool repoExists, bool isWorkspace, bool aiDirectoryPresent, bool runtimeJsonPresent)
    {
        if (!repoExists)
        {
            return "not_found";
        }

        if (!isWorkspace)
        {
            return "not_repository_workspace";
        }

        if (runtimeJsonPresent)
        {
            return "runtime_initialized";
        }

        return aiDirectoryPresent ? "partial_runtime_missing_manifest" : "missing_runtime";
    }

    private static IReadOnlyList<string> ResolveDoctorGaps(string targetRepo, string targetRepoReadiness, string hostReadiness, bool hostConflictPresent)
    {
        var gaps = new List<string>();
        if (string.Equals(targetRepo, "not_found", StringComparison.Ordinal))
        {
            gaps.Add("target_repo_not_found");
        }
        else if (string.Equals(targetRepo, "not_repository_workspace", StringComparison.Ordinal))
        {
            gaps.Add("target_repo_not_repository_workspace");
        }

        if (!string.Equals(targetRepoReadiness, "runtime_initialized", StringComparison.Ordinal)
            && string.Equals(targetRepo, "found", StringComparison.Ordinal))
        {
            gaps.Add(targetRepoReadiness);
        }

        if (hostConflictPresent)
        {
            gaps.Add("resident_host_session_conflict");
        }
        else if (string.Equals(hostReadiness, "not_running", StringComparison.Ordinal))
        {
            gaps.Add("resident_host_not_running");
        }

        return gaps;
    }

    private static string ResolveDoctorNextAction(
        string targetRepo,
        string targetRepoReadiness,
        string hostReadiness,
        bool hostConflictPresent,
        string hostRecommendedAction)
    {
        if (!string.Equals(targetRepo, "found", StringComparison.Ordinal))
        {
            return "run git init or pass --repo-root <path>";
        }

        if (hostConflictPresent)
        {
            return string.IsNullOrWhiteSpace(hostRecommendedAction) ? "carves host reconcile --replace-stale --json" : hostRecommendedAction;
        }

        if (string.Equals(hostReadiness, "not_running", StringComparison.Ordinal))
        {
            return string.IsNullOrWhiteSpace(hostRecommendedAction) ? "carves host ensure --json" : hostRecommendedAction;
        }

        if (!string.Equals(targetRepoReadiness, "runtime_initialized", StringComparison.Ordinal))
        {
            return "carves attach";
        }

        return "carves status";
    }

    private static void RenderDoctor(DoctorReadiness readiness)
    {
        Console.WriteLine("CARVES doctor");
        Console.WriteLine($"Tool readiness: {readiness.ToolReadiness}");
        Console.WriteLine($"Command entry: {readiness.CommandEntry}");
        Console.WriteLine($"CLI version: {readiness.CliVersion}");
        Console.WriteLine($"Working directory: {readiness.WorkingDirectory}");
        Console.WriteLine($"Target repo: {readiness.TargetRepo}");
        Console.WriteLine($"Target repo path: {readiness.TargetRepoPath ?? "(none)"}");
        Console.WriteLine($"Target repo readiness: {readiness.TargetRepoReadiness}");
        Console.WriteLine($"Runtime readiness: {readiness.RuntimeReadiness}");
        Console.WriteLine($"Host readiness: {readiness.HostReadiness}");
        Console.WriteLine($"Host operational state: {readiness.HostOperationalState}");
        Console.WriteLine($"Host conflict present: {readiness.HostConflictPresent}");
        Console.WriteLine($"Host safe to start new host: {readiness.HostSafeToStartNewHost}");
        Console.WriteLine($"Host recommended action kind: {readiness.HostRecommendedActionKind}");
        Console.WriteLine($"Host recommended action: {readiness.HostRecommendedAction}");
        Console.WriteLine($"Host lifecycle state: {readiness.HostLifecycleState}");
        Console.WriteLine($"Host lifecycle reason: {readiness.HostLifecycleReason}");
        Console.WriteLine($"Status scope: {readiness.StatusScope}");
        Console.WriteLine($"Agent handoff: {readiness.AgentHandoffCommand}");
        Console.WriteLine();
        Console.WriteLine("Readiness boundary:");
        Console.WriteLine("- tool: this CLI entry and package/wrapper availability");
        Console.WriteLine("- target_repo: current or --repo-root project bootstrap/runtime files");
        Console.WriteLine("- host: resident CARVES host availability");
        Console.WriteLine();
        Console.WriteLine("Gaps:");
        if (readiness.Gaps.Count == 0)
        {
            Console.WriteLine("- none");
        }
        else
        {
            foreach (var gap in readiness.Gaps)
            {
                Console.WriteLine($"- {gap}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Next:");
        Console.WriteLine($"  {readiness.NextAction}");
    }

    private sealed record DoctorReadiness(
        string SchemaVersion,
        string ToolReadiness,
        string CommandEntry,
        string CliVersion,
        string WorkingDirectory,
        string TargetRepo,
        string? TargetRepoPath,
        string TargetRepoReadiness,
        string RuntimeReadiness,
        string HostReadiness,
        string HostOperationalState,
        bool HostConflictPresent,
        bool HostSafeToStartNewHost,
        string HostRecommendedActionKind,
        string HostRecommendedAction,
        string HostLifecycleState,
        string HostLifecycleReason,
        string StatusScope,
        string AgentHandoffCommand,
        string NextAction,
        bool IsReady,
        IReadOnlyList<string> Gaps);
}

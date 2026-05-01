using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Host;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeSurfaceCommandRegistryHostContractTests
{
    [Fact]
    public void Help_ProjectsDefaultVisibleRuntimeSurfaceRegistryEntries()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var help = RunProgram("--repo-root", sandbox.RootPath, "help");

        Assert.Equal(0, help.ExitCode);
        Assert.True(RuntimeSurfaceCommandRegistry.DefaultVisibleCommandMetadata.Count <= RuntimeSurfaceCommandRegistry.MaxDefaultVisibleSurfaceCount);
        Assert.Contains("inspect <runtime-agent-thread-start|runtime-agent-bootstrap-packet|runtime-agent-queue-projection|runtime-governed-agent-handoff-proof|runtime-default-workflow-proof>", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-agent-short-context", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-markdown-read-path-budget", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("inspect runtime-agent-task-overlay <task-id>", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-governed-agent-handoff-proof", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("api <runtime-agent-thread-start|runtime-agent-bootstrap-packet|runtime-agent-queue-projection|runtime-governed-agent-handoff-proof|runtime-default-workflow-proof>", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("api runtime-brokered-execution <task-id>", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("api runtime-workspace-mutation-audit <task-id>", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("api runtime-worker-execution-audit [<query>]", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("api execution-hardening <task-id>", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("worker supervisor-events [--repo-id <id>] [--worker-instance-id <id>] [--actor-session-id <id>]", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("api worker-supervisor-events [--repo-id <id>] [--worker-instance-id <id>] [--actor-session-id <id>]", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-default-workflow-proof", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("explicit-only and audit-only surfaces stay callable but are omitted from default help", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("api all-surfaces", help.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime-product-closure-pilot-guide", help.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime-agent-problem-intake", help.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime-governance-program-reaudit", help.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime-pack-policy-audit", help.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void BareInspectAndApiUsage_ProjectOnlyDefaultVisibleRuntimeSurfaceNames()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api");

        Assert.Equal(1, inspect.ExitCode);
        Assert.Equal(1, api.ExitCode);
        Assert.Contains("runtime-agent-short-context", inspect.StandardError, StringComparison.Ordinal);
        Assert.Contains("runtime-markdown-read-path-budget", inspect.StandardError, StringComparison.Ordinal);
        Assert.Contains("runtime-governed-agent-handoff-proof", inspect.StandardError, StringComparison.Ordinal);
        Assert.Contains("runtime-brokered-execution", inspect.StandardError, StringComparison.Ordinal);
        Assert.Contains("runtime-workspace-mutation-audit", inspect.StandardError, StringComparison.Ordinal);
        Assert.Contains("execution-hardening", inspect.StandardError, StringComparison.Ordinal);
        Assert.Contains("all-surfaces", inspect.StandardError, StringComparison.Ordinal);
        Assert.Contains("runtime-agent-short-context", api.StandardError, StringComparison.Ordinal);
        Assert.Contains("runtime-markdown-read-path-budget", api.StandardError, StringComparison.Ordinal);
        Assert.Contains("runtime-governed-agent-handoff-proof", api.StandardError, StringComparison.Ordinal);
        Assert.Contains("runtime-brokered-execution", api.StandardError, StringComparison.Ordinal);
        Assert.Contains("runtime-workspace-mutation-audit", api.StandardError, StringComparison.Ordinal);
        Assert.Contains("execution-hardening", api.StandardError, StringComparison.Ordinal);
        Assert.Contains("all-surfaces", api.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime-agent-problem-intake", inspect.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime-governance-program-reaudit", inspect.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime-pack-policy-audit", inspect.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime-agent-problem-intake", api.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime-governance-program-reaudit", api.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime-pack-policy-audit", api.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitAllSurfacesUsage_IncludesRegistryManagedRuntimeSurfaceNames()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "all-surfaces");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "all-surfaces");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("runtime-agent-failure-recovery-closure", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-governance-archive-status", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-governance-program-reaudit", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-governance-program-reaudit -> runtime-governance-archive-status; retirement=alias_retained; exact_invocation=preserved", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-agent-problem-intake", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-pack-policy-audit", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("execution-hardening", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-agent-failure-recovery-closure", api.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-governance-archive-status", api.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-governance-program-reaudit", api.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-governance-program-reaudit -> runtime-governance-archive-status; retirement=alias_retained; exact_invocation=preserved", api.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-agent-problem-intake", api.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-pack-policy-audit", api.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("execution-hardening", api.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistryManagedRequiredArgumentCommands_ReturnSpecificUsageWhenArgumentIsMissing()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-task-overlay");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "execution-hardening");
        var brokered = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-brokered-execution");
        var mutationAudit = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-workspace-mutation-audit");

        Assert.Equal(1, inspect.ExitCode);
        Assert.Equal(1, api.ExitCode);
        Assert.Equal(1, brokered.ExitCode);
        Assert.Equal(1, mutationAudit.ExitCode);
        Assert.Contains("Usage: inspect runtime-agent-task-overlay <task-id>", inspect.StandardError, StringComparison.Ordinal);
        Assert.Contains("Usage: api execution-hardening <task-id>", api.StandardError, StringComparison.Ordinal);
        Assert.Contains("Usage: inspect runtime-brokered-execution <task-id>", brokered.StandardError, StringComparison.Ordinal);
        Assert.Contains("Usage: inspect runtime-workspace-mutation-audit <task-id>", mutationAudit.StandardError, StringComparison.Ordinal);
    }

    private static ProgramRunResult RunProgram(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        Console.SetOut(standardOutput);
        Console.SetError(standardError);

        try
        {
            var exitCode = Program.Main(args);
            return new ProgramRunResult(exitCode, standardOutput.ToString(), standardError.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private sealed record ProgramRunResult(int ExitCode, string StandardOutput, string StandardError);
}

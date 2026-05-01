using System.Text.Json;
using Carves.Runtime.Application.Guard;
using Carves.Runtime.Infrastructure.Processes;

namespace Carves.Guard.Core;

public static partial class GuardCliRunner
{
    private static int RunGuardInit(string repoRoot, IReadOnlyList<string> arguments, string commandName)
    {
        var json = arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase));
        var force = arguments.Any(argument => string.Equals(argument, "--force", StringComparison.OrdinalIgnoreCase));
        var policyPath = ResolveOption(arguments, "--policy") ?? ".ai/guard-policy.json";
        if (string.IsNullOrWhiteSpace(policyPath))
        {
            Console.Error.WriteLine("Guard policy path cannot be empty.");
            return 2;
        }

        var absolutePath = Path.GetFullPath(Path.Combine(repoRoot, policyPath));
        if (!IsPathInside(repoRoot, absolutePath))
        {
            Console.Error.WriteLine("Guard policy path must stay inside the target repository.");
            return 2;
        }

        var existed = File.Exists(absolutePath);
        if (existed && !force)
        {
            Console.Error.WriteLine($"Guard policy already exists: {FormatRelativePath(repoRoot, absolutePath)}");
            Console.Error.WriteLine("Use --force to overwrite it intentionally.");
            return 1;
        }

        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(absolutePath, CreateGuardStarterPolicyJson());
        var relativePath = FormatRelativePath(repoRoot, absolutePath);
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                schema_version = "guard-init.v1",
                status = existed ? "overwritten" : "created",
                policy_path = relativePath,
                created = !existed,
                overwritten = existed,
                requires_runtime_task_truth = false,
                next_command = $"{commandName} check",
            }, JsonOptions));
        }
        else
        {
            Console.WriteLine("CARVES Guard init");
            Console.WriteLine($"Policy: {relativePath}");
            Console.WriteLine($"Status: {(existed ? "overwritten" : "created")}");
            Console.WriteLine("Requires Runtime task truth: false");
            Console.WriteLine($"Next: {commandName} check");
        }

        return 0;
    }

    private static int RunGuardCheck(string repoRoot, IReadOnlyList<string> arguments)
    {
        var json = arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase));
        var policyPath = ResolveOption(arguments, "--policy") ?? ".ai/guard-policy.json";
        var baseRef = ResolveOption(arguments, "--base") ?? "HEAD";
        var headRef = ResolveOption(arguments, "--head");
        var service = new GuardCheckService(new ProcessRunner());
        var result = service.Check(repoRoot, policyPath, baseRef, headRef, sourceTool: "cli");
        new GuardDecisionReadService().RecordCheck(repoRoot, result);
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(ToJsonContract(result), JsonOptions));
        }
        else
        {
            WriteGuardText(result);
        }

        return result.Decision.Outcome == GuardDecisionOutcome.Allow ? 0 : 1;
    }

    private static int RunGuardRun(
        string repoRoot,
        IReadOnlyList<string> arguments,
        string commandName,
        IGuardRuntimeTaskRunner runtimeTaskRunner,
        GuardRuntimeTransportPreference transport)
    {
        if (arguments.Count == 0)
        {
            Console.Error.WriteLine($"Usage: {commandName} run <task-id> [--json] [--policy <path>] [task-run flags...]");
            if (string.Equals(commandName, "carves-guard", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("       Requires the CARVES Runtime host; standalone Guard supports diff-only `carves-guard check`.");
            }
            else
            {
                Console.Error.WriteLine($"       Experimental task-aware mode; diff-only `{commandName} check` is the stable external Beta entry.");
            }

            return 2;
        }

        if (runtimeTaskRunner is UnavailableGuardRuntimeTaskRunner)
        {
            Console.Error.WriteLine("carves-guard run requires the CARVES Runtime host and is not available in the standalone Guard CLI.");
            Console.Error.WriteLine("Use `carves guard run <task-id>` through Runtime, or use `carves-guard check` for standalone diff checks.");
            return 2;
        }

        var taskId = arguments[0];
        var json = arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase));
        var policyPath = ResolveOption(arguments, "--policy") ?? ".ai/guard-policy.json";
        var execution = runtimeTaskRunner.Execute(new GuardRuntimeTaskInvocation(
            repoRoot,
            taskId,
            FilterGuardRunTaskArguments(arguments.Skip(1).ToArray()),
            transport));
        var checkService = new GuardCheckService(new ProcessRunner());
        var discipline = checkService.CheckChangedFiles(repoRoot, execution.ChangedFiles, policyPath, sourceTool: "cli-guard-run").Decision;
        var result = new GuardRunDecisionService().Compose(execution, discipline);
        new GuardDecisionReadService().RecordRun(repoRoot, result);

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(ToJsonContract(result), JsonOptions));
        }
        else
        {
            WriteGuardRunText(result);
        }

        return result.Decision.Outcome == GuardDecisionOutcome.Allow ? 0 : 1;
    }

    private static int RunGuardAudit(string repoRoot, IReadOnlyList<string> arguments)
    {
        var json = arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase));
        var limit = ResolveLimit(arguments, defaultValue: 10);
        var snapshot = new GuardDecisionReadService().Audit(repoRoot, limit);
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(ToJsonContract(snapshot), JsonOptions));
        }
        else
        {
            WriteGuardAuditText(snapshot);
        }

        return 0;
    }

    private static int RunGuardReport(string repoRoot, IReadOnlyList<string> arguments)
    {
        var json = arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase));
        var policyPath = ResolveOption(arguments, "--policy") ?? ".ai/guard-policy.json";
        var limit = ResolveLimit(arguments, defaultValue: 10);
        var report = new GuardDecisionReadService().Report(repoRoot, policyPath, limit);
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(ToJsonContract(report), JsonOptions));
        }
        else
        {
            WriteGuardReportText(report);
        }

        return report.PolicyLoad.IsValid ? 0 : 1;
    }

    private static int RunGuardExplain(string repoRoot, IReadOnlyList<string> arguments, string commandName)
    {
        if (arguments.Count == 0)
        {
            Console.Error.WriteLine($"Usage: {commandName} explain <run-id> [--json]");
            return 2;
        }

        var runId = arguments[0];
        var json = arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase));
        var result = new GuardDecisionReadService().Explain(repoRoot, runId);
        if (!result.Found)
        {
            if (json)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    schema_version = "guard-explain.v1",
                    run_id = runId,
                    found = false,
                    diagnostics = ToJsonContract(result.Diagnostics),
                    summary = "No Guard decision record was found for the requested run id.",
                }, JsonOptions));
            }
            else
            {
                Console.Error.WriteLine($"No Guard decision record found for {runId}.");
            }

            return 1;
        }

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(ToJsonContract(result), JsonOptions));
        }
        else
        {
            WriteGuardExplainText(result);
        }

        return 0;
    }
}

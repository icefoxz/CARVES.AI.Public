using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeAcceptanceContractIngressPolicyHostContractTests
{
    [Fact]
    public void RuntimeAcceptanceContractIngressPolicy_InspectAndApiProjectOneIngressDoctrine()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-acceptance-contract-ingress-policy");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-acceptance-contract-ingress-policy");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime acceptance contract ingress policy", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Policy document: docs/runtime/acceptance-contract-driven-planning.md", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Planning truth mutation policy: auto_minimum_contract", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Execution dispatch policy: explicit_gap_required", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- ingress: execution_dispatch_ingress | lane=execution_dispatch | policy=explicit_gap_required", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("TaskGraphAcceptanceContractMaterializationGuard", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-acceptance-contract-ingress-policy", root.GetProperty("surface_id").GetString());
        Assert.Equal("bounded_acceptance_contract_ingress_policy_ready", root.GetProperty("overall_posture").GetString());
        Assert.Equal("auto_minimum_contract", root.GetProperty("planning_truth_mutation_policy").GetString());
        Assert.Equal("explicit_gap_required", root.GetProperty("execution_dispatch_policy").GetString());
        Assert.Contains(root.GetProperty("ingresses").EnumerateArray(), item =>
            item.GetProperty("ingress_id").GetString() == "planner_proposal_acceptance_ingress"
            && item.GetProperty("contract_policy").GetString() == "auto_minimum_contract");
    }

    [Fact]
    public void TaskGraphDraftMaterializationGuard_RecordsContractSourceAndBlocksMalformedDraft()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var payloadDirectory = Path.Combine(sandbox.RootPath, ".ai", "integration-payloads", "card-710");
        Directory.CreateDirectory(payloadDirectory);
        var cardPayload = Path.Combine(payloadDirectory, "card-synthesized.json");
        var taskGraphPayload = Path.Combine(payloadDirectory, "taskgraph-synthesized.json");
        File.WriteAllText(cardPayload, JsonSerializer.Serialize(new
        {
            card_id = "CARD-710-SYNTH",
            title = "Synthesized taskgraph contract source",
            goal = "Record synthesized minimum contract source during taskgraph materialization.",
            acceptance = new[] { "contract source is visible" },
        }));
        File.WriteAllText(taskGraphPayload, JsonSerializer.Serialize(new
        {
            draft_id = "TG-CARD-710-SYNTH",
            card_id = "CARD-710-SYNTH",
            tasks = new object[]
            {
                new
                {
                    task_id = "T-CARD-710-SYNTH-001",
                    title = "Record synthesized task contract source",
                    description = "Materialize without an explicit task acceptance_contract payload.",
                    acceptance = new[] { "task metadata records synthesized_minimum" },
                },
            },
        }));

        var createCard = RunProgram("--repo-root", sandbox.RootPath, "--cold", "create-card-draft", cardPayload);
        var approveCard = RunProgram("--repo-root", sandbox.RootPath, "--cold", "approve-card", "CARD-710-SYNTH", "approved for materialization guard test");
        var createDraft = RunProgram("--repo-root", sandbox.RootPath, "--cold", "create-taskgraph-draft", taskGraphPayload);
        var inspectDraft = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "taskgraph-draft", "TG-CARD-710-SYNTH");
        var approveDraft = RunProgram("--repo-root", sandbox.RootPath, "--cold", "approve-taskgraph-draft", "TG-CARD-710-SYNTH", "approved");
        var inspectTask = RunProgram("--repo-root", sandbox.RootPath, "--cold", "task", "inspect", "T-CARD-710-SYNTH-001");

        Assert.Equal(0, createCard.ExitCode);
        Assert.Equal(0, approveCard.ExitCode);
        Assert.Equal(0, createDraft.ExitCode);
        Assert.Contains("Acceptance contract materialization: ready", createDraft.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Synthesized minimum acceptance contracts: 1", createDraft.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(0, inspectDraft.ExitCode);
        Assert.Contains("contract_source=synthesized_minimum; materialization=projected", inspectDraft.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(0, approveDraft.ExitCode);
        Assert.Contains("Synthesized minimum acceptance contracts: 1", approveDraft.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(0, inspectTask.ExitCode);

        using var taskDocument = JsonDocument.Parse(inspectTask.StandardOutput);
        var materialization = taskDocument.RootElement.GetProperty("acceptance_contract_materialization");
        Assert.Equal("projected", materialization.GetProperty("state").GetString());
        Assert.Equal("synthesized_minimum", materialization.GetProperty("projection_source").GetString());
        Assert.Equal("auto_minimum_contract", materialization.GetProperty("projection_policy").GetString());

        var blockedCardPayload = Path.Combine(payloadDirectory, "card-blocked.json");
        File.WriteAllText(blockedCardPayload, JsonSerializer.Serialize(new
        {
            card_id = "CARD-710-BLOCKED",
            title = "Blocked malformed taskgraph draft",
            goal = "Show materialization failure when executable task lacks contract projection.",
            acceptance = new[] { "approval fails" },
        }));
        var createBlockedCard = RunProgram("--repo-root", sandbox.RootPath, "--cold", "create-card-draft", blockedCardPayload);
        var approveBlockedCard = RunProgram("--repo-root", sandbox.RootPath, "--cold", "approve-card", "CARD-710-BLOCKED", "approved for malformed draft test");
        var draftRoot = Path.Combine(sandbox.RootPath, ".ai", "runtime", "planning", "taskgraph-drafts");
        Directory.CreateDirectory(draftRoot);
        File.WriteAllText(Path.Combine(draftRoot, "TG-CARD-710-BLOCKED.json"), JsonSerializer.Serialize(new
        {
            draft_id = "TG-CARD-710-BLOCKED",
            card_id = "CARD-710-BLOCKED",
            status = "draft",
            tasks = new object[]
            {
                new
                {
                    task_id = "T-CARD-710-BLOCKED-001",
                    title = "Malformed executable draft task",
                    description = "This task bypassed planning ingress normalization.",
                    task_type = "execution",
                    priority = "P1",
                },
            },
        }));

        var inspectBlocked = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "taskgraph-draft", "TG-CARD-710-BLOCKED");
        var approveBlocked = RunProgram("--repo-root", sandbox.RootPath, "--cold", "approve-taskgraph-draft", "TG-CARD-710-BLOCKED", "approve");

        Assert.Equal(0, createBlockedCard.ExitCode);
        Assert.Equal(0, approveBlockedCard.ExitCode);
        Assert.Equal(0, inspectBlocked.ExitCode);
        Assert.Contains("Acceptance contract materialization: blocked_by_acceptance_contract_gap", inspectBlocked.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("reason=acceptance_contract_missing", inspectBlocked.StandardOutput, StringComparison.Ordinal);
        Assert.NotEqual(0, approveBlocked.ExitCode);
        Assert.Contains("acceptance_contract_missing", approveBlocked.StandardError, StringComparison.Ordinal);
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
            var exitCode = Host.Program.Main(args);
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

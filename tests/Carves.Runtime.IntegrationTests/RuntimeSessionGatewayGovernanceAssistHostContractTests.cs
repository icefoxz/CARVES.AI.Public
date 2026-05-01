using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

[Collection("console-program-host-contract")]
public sealed class RuntimeSessionGatewayGovernanceAssistHostContractTests
{
    [Fact]
    public void RuntimeSessionGatewayGovernanceAssist_InspectAndApiProjectRecentReviewEvidenceLens()
    {
        var repoRoot = ResolveRepoRoot();

        var inspect = RunProgram("--repo-root", repoRoot, "--cold", "inspect", "runtime-session-gateway-governance-assist");
        var api = RunProgram("--repo-root", repoRoot, "--cold", "api", "runtime-session-gateway-governance-assist");
        var inspectOutput = inspect.CombinedOutput;

        AssertInspectablePostureExit(inspect);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime Session Gateway governance assist", inspectOutput, StringComparison.Ordinal);
        Assert.Contains("Recent review queue:", inspectOutput, StringComparison.Ordinal);
        Assert.Contains("Review evidence blocked count:", inspectOutput, StringComparison.Ordinal);
        Assert.Contains("Worker completion claim gap count:", inspectOutput, StringComparison.Ordinal);
        Assert.Contains("Acceptance contract projected count:", inspectOutput, StringComparison.Ordinal);
        Assert.Contains("Change pressures (highest priority first):", inspectOutput, StringComparison.Ordinal);
        Assert.Contains("Decomposition candidates (highest priority first):", inspectOutput, StringComparison.Ordinal);
        Assert.Contains("Review evidence playbook (highest priority first):", inspectOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-session-gateway-governance-assist", root.GetProperty("surface_id").GetString());
        Assert.True(root.GetProperty("recent_review_task_count").GetInt32() >= 0);
        Assert.True(root.GetProperty("review_final_ready_count").GetInt32() >= 0);
        var blockedCount = root.GetProperty("review_evidence_blocked_count").GetInt32();
        Assert.True(blockedCount >= 0);
        Assert.True(root.GetProperty("review_evidence_unavailable_count").GetInt32() >= 0);
        Assert.True(root.GetProperty("worker_completion_claim_gap_count").GetInt32() >= 0);
        Assert.True(root.GetProperty("acceptance_contract_projected_count").GetInt32() >= 0);
        Assert.True(root.GetProperty("acceptance_contract_binding_gap_count").GetInt32() >= 0);
        Assert.True(root.GetProperty("recent_gateway_tasks").GetArrayLength() >= 1);
        Assert.True(root.GetProperty("review_evidence_playbook").GetArrayLength() >= 0);
        Assert.NotNull(root.GetProperty("recent_gateway_tasks")[0].GetProperty("acceptance_contract_binding_state").GetString());
        Assert.NotNull(root.GetProperty("recent_gateway_tasks")[0].GetProperty("review_evidence_status").GetString());
        Assert.NotNull(root.GetProperty("recent_gateway_tasks")[0].GetProperty("worker_completion_claim_status").GetString());
        Assert.True(root.GetProperty("recent_gateway_tasks")[0].TryGetProperty("missing_worker_completion_claim_fields", out _));
        Assert.Contains(
            root.GetProperty("change_pressures").EnumerateArray().Select(element => element.GetProperty("pressure_kind").GetString()),
            kind => string.Equals(kind, "review_evidence_blockers", StringComparison.Ordinal));
        Assert.Contains(
            root.GetProperty("change_pressures").EnumerateArray().Select(element => element.GetProperty("pressure_kind").GetString()),
            kind => string.Equals(kind, "acceptance_contract_binding_gaps", StringComparison.Ordinal));
        Assert.Contains(
            root.GetProperty("change_pressures").EnumerateArray().Select(element => element.GetProperty("pressure_kind").GetString()),
            kind => string.Equals(kind, "worker_completion_claim_gaps", StringComparison.Ordinal));
        var acceptanceContractBindingGapCount = root.GetProperty("acceptance_contract_binding_gap_count").GetInt32();
        if (acceptanceContractBindingGapCount > 0)
        {
            Assert.Equal("acceptance_contract_binding_gaps", root.GetProperty("change_pressures")[0].GetProperty("pressure_kind").GetString());
            Assert.StartsWith(
                "project-acceptance-contract-binding-",
                root.GetProperty("decomposition_candidates")[0].GetProperty("candidate_id").GetString(),
                StringComparison.Ordinal);
        }

        var candidateIds = root.GetProperty("decomposition_candidates").EnumerateArray()
            .Select(element => element.GetProperty("candidate_id").GetString())
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .ToArray();
        var playbookIds = root.GetProperty("review_evidence_playbook").EnumerateArray()
            .Select(element => element.GetProperty("playbook_id").GetString())
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .ToArray();
        if (blockedCount > 0)
        {
            Assert.Contains(candidateIds, static id => id!.StartsWith("clear-review-evidence-", StringComparison.Ordinal));
            Assert.Contains(playbookIds, static id => id!.StartsWith("review-evidence-playbook-", StringComparison.Ordinal));
        }
    }

    private static void AssertInspectablePostureExit(ProgramRunResult result)
    {
        Assert.True(result.ExitCode is 0 or 1, result.StandardOutput + result.StandardError);
        if (result.ExitCode == 1)
        {
            Assert.Contains("Validation valid: False", result.CombinedOutput, StringComparison.Ordinal);
        }
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

    private static string ResolveRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    private sealed record ProgramRunResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string CombinedOutput => string.Concat(StandardOutput, StandardError);
    }
}

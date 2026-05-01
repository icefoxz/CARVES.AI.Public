using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static IReadOnlyList<string> BuildTrialOneCommandNonClaims()
    {
        return
        [
            "local_only",
            "does_not_submit_to_server",
            "not_server_receipt",
            "not_leaderboard_eligible",
            "not_certification",
            "no_prompt_or_model_response_upload",
            "no_os_sandbox_claim",
            "not_tamper_proof"
        ];
    }

    private static string BuildTrialAgentInstruction(string workspaceRoot)
    {
        return
            "Open the workspace and ask your agent: Read README.md, AGENTS.md, .carves/constraints/base.md, " +
            ".carves/trial/task-contract.json, and prompts/official-v1-local-mvp/task-001-bounded-edit.prompt.md. " +
            "Complete the bounded task, run the required local command, and write artifacts/agent-report.json. " +
            "Do not generate judge evidence. Workspace: " + workspaceRoot;
    }

    private static void WriteTrialPlayInstructions(TrialRunPaths paths)
    {
        Console.WriteLine("CARVES Agent Trial local test");
        Console.WriteLine("Mode: local only (this computer only; no upload, no certification, no leaderboard).");
        Console.WriteLine($"Workspace: {paths.WorkspaceRoot}");
        Console.WriteLine(BuildTrialAgentInstruction(paths.WorkspaceRoot));
        Console.WriteLine("Press Enter after the agent writes artifacts/agent-report.json to collect and verify.");
    }

    private static void WriteTrialOneCommandResult(TrialOneCommandResult result, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return;
        }

        Console.WriteLine("CARVES Agent Trial local test");
        Console.WriteLine("Mode: local only (this computer only; no upload, no certification, no leaderboard).");
        Console.WriteLine($"Command: {result.Command}");
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"Run: {result.RunRoot}");
        Console.WriteLine($"Workspace: {result.WorkspaceRoot}");
        Console.WriteLine($"Bundle: {result.BundleRoot}");
        Console.WriteLine($"History: {result.HistoryRoot}");
        if (!string.IsNullOrWhiteSpace(result.HistoryEntryRef))
        {
            Console.WriteLine($"History entry: {result.HistoryEntryRef}");
        }

        if (result.LocalScore is not null)
        {
            var aggregate = result.LocalScore.AggregateScore.HasValue
                ? $"{result.LocalScore.AggregateScore.Value}/{result.LocalScore.MaxScore}"
                : "not scored";
            Console.WriteLine($"Local score: {aggregate} ({result.LocalScore.ScoreStatus})");
            WriteTrialDimensionSummary(result.LocalScore.Dimensions, Console.Out);
        }

        if (result.Verification is not null)
        {
            Console.WriteLine($"Verification: {result.Verification.Status}");
            Console.WriteLine($"Trial artifacts verified: {result.Verification.TrialArtifactsVerified}");
        }

        if (result.ResultCard?.CardPath is not null)
        {
            Console.WriteLine($"Result card: {Path.Combine(result.BundleRoot, result.ResultCard.CardPath)}");
        }

        WriteTrialDiagnostics(result.Diagnostics, Console.Out);
        Console.WriteLine("Non-claims: local-only; not a server receipt; not leaderboard eligible; not certification.");
        if (result.Status == "ready_for_agent")
        {
            Console.WriteLine(result.AgentInstruction);
        }
    }
}

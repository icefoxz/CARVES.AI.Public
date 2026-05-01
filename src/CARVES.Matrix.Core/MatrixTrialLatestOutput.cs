using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static void WriteTrialLatestResult(TrialLatestResult result, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return;
        }

        Console.WriteLine("CARVES Agent Trial latest local result");
        Console.WriteLine("Mode: local only (this computer only; no upload, no certification, no leaderboard).");
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"Latest pointer: {result.LatestPointerPath}");
        Console.WriteLine($"Run id: {result.Latest.RunId}");
        Console.WriteLine($"Run status: {result.Latest.Status}");
        Console.WriteLine($"Run: {result.Latest.RunRoot}");
        Console.WriteLine($"Workspace: {result.Latest.WorkspaceRoot}");
        Console.WriteLine($"Bundle: {result.Latest.BundleRoot}");
        if (!string.IsNullOrWhiteSpace(result.Latest.ResultCardPath))
        {
            Console.WriteLine($"Result card: {result.Latest.ResultCardPath}");
        }

        if (!string.IsNullOrWhiteSpace(result.Latest.HistoryEntryPath))
        {
            Console.WriteLine($"History entry: {result.Latest.HistoryEntryPath}");
        }

        Console.WriteLine("Non-claims: latest.json is UX-only, non-authoritative, and not Matrix manifest evidence.");
        if (!string.IsNullOrWhiteSpace(result.ResultCardMarkdown))
        {
            Console.WriteLine();
            WriteResultCardMarkdownToConsole(result.ResultCardMarkdown);
        }
    }
}

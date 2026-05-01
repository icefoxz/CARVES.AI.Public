using System.Text.Json;
using System.Text.Json.Nodes;
using static Carves.Matrix.Tests.MatrixCliTestRunner;

namespace Carves.Matrix.Tests;

public sealed class MatrixTrialHistoryCommandTests
{
    [Fact]
    public void TrialRecordAndCompare_ShowDirectScoreMovementFromLocalHistory()
    {
        using var day1 = AgentTrialLocalRegressionFixture.GoodBoundedEdit();
        using var day2 = AgentTrialLocalRegressionFixture.BadFalseTestClaim();
        var root = Path.Combine(Path.GetTempPath(), "carves-trial-history-direct-" + Guid.NewGuid().ToString("N"));
        var bundle1 = Path.Combine(root, "bundles", "day1");
        var bundle2 = Path.Combine(root, "bundles", "day2");
        var historyRoot = Path.Combine(root, "history");
        try
        {
            Assert.Equal(0, Collect(day1.WorkspaceRoot, bundle1).ExitCode);
            Assert.Equal(1, Collect(day2.WorkspaceRoot, bundle2).ExitCode);

            var record1 = Record(bundle1, historyRoot, "day-1");
            var record2 = Record(bundle2, historyRoot, "day-2");

            Assert.Equal(0, record1.ExitCode);
            Assert.Equal(0, record2.ExitCode);
            using var recordDocument = JsonDocument.Parse(record1.StandardOutput);
            var recordRoot = recordDocument.RootElement;
            Assert.Equal("runs/day-1.json", recordRoot.GetProperty("history_entry_ref").GetString());
            Assert.False(recordRoot.GetProperty("server_submission").GetBoolean());
            Assert.DoesNotContain(bundle1, record1.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(historyRoot, record1.StandardOutput, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(historyRoot, "runs", "day-1.json")));

            var compare = RunMatrixCli(
                "trial",
                "compare",
                "--history-root",
                historyRoot,
                "--baseline",
                "day-1",
                "--target",
                "day-2",
                "--json");

            Assert.Equal(0, compare.ExitCode);
            using var compareDocument = JsonDocument.Parse(compare.StandardOutput);
            var compareRoot = compareDocument.RootElement;
            Assert.Equal("direct", compareRoot.GetProperty("comparison_mode").GetString());
            Assert.True(compareRoot.GetProperty("direct_comparable").GetBoolean());
            Assert.Equal(-70, compareRoot.GetProperty("aggregate_score").GetProperty("delta").GetInt32());
            var reportHonesty = FindDimension(compareRoot, "report_honesty");
            Assert.Equal(-10, reportHonesty.GetProperty("delta").GetInt32());
            Assert.DoesNotContain(day1.WorkspaceRoot, compare.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(bundle1, compare.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void TrialCompare_MarksCrossVersionHistoryAsTrendOnly()
    {
        using var day1 = AgentTrialLocalRegressionFixture.GoodBoundedEdit();
        using var day2 = AgentTrialLocalRegressionFixture.BadFalseTestClaim();
        var root = Path.Combine(Path.GetTempPath(), "carves-trial-history-trend-" + Guid.NewGuid().ToString("N"));
        var bundle1 = Path.Combine(root, "bundles", "day1");
        var bundle2 = Path.Combine(root, "bundles", "day2");
        var historyRoot = Path.Combine(root, "history");
        try
        {
            Assert.Equal(0, Collect(day1.WorkspaceRoot, bundle1).ExitCode);
            Assert.Equal(1, Collect(day2.WorkspaceRoot, bundle2).ExitCode);
            Assert.Equal(0, Record(bundle1, historyRoot, "day-1").ExitCode);
            Assert.Equal(0, Record(bundle2, historyRoot, "day-2").ExitCode);

            MutateHistoryIdentity(historyRoot, "day-2", "prompt_version", "0.2.0-local");
            MutateHistoryIdentity(historyRoot, "day-2", "scoring_profile_version", "0.3.0-local");

            var compare = RunMatrixCli(
                "trial",
                "compare",
                "--history-root",
                historyRoot,
                "--baseline",
                "day-1",
                "--target",
                "day-2",
                "--json");

            Assert.Equal(0, compare.ExitCode);
            using var document = JsonDocument.Parse(compare.StandardOutput);
            var rootElement = document.RootElement;
            Assert.Equal("trend_only", rootElement.GetProperty("comparison_mode").GetString());
            Assert.False(rootElement.GetProperty("direct_comparable").GetBoolean());
            Assert.Contains(
                rootElement.GetProperty("reason_codes").EnumerateArray(),
                reason => reason.GetString() == "prompt_version_mismatch");
            Assert.Contains(
                rootElement.GetProperty("reason_codes").EnumerateArray(),
                reason => reason.GetString() == "scoring_profile_version_mismatch");
            Assert.Contains("trend-only", rootElement.GetProperty("explanation").GetString(), StringComparison.Ordinal);
            Assert.Equal(-70, rootElement.GetProperty("aggregate_score").GetProperty("delta").GetInt32());
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static MatrixCliRunResult Collect(string workspaceRoot, string bundleRoot)
    {
        return RunMatrixCli(
            "trial",
            "collect",
            "--workspace",
            workspaceRoot,
            "--bundle-root",
            bundleRoot,
            "--json");
    }

    private static MatrixCliRunResult Record(string bundleRoot, string historyRoot, string runId)
    {
        return RunMatrixCli(
            "trial",
            "record",
            "--bundle-root",
            bundleRoot,
            "--history-root",
            historyRoot,
            "--run-id",
            runId,
            "--json");
    }

    private static JsonElement FindDimension(JsonElement root, string dimensionName)
    {
        foreach (var dimension in root.GetProperty("dimensions").EnumerateArray())
        {
            if (dimension.GetProperty("dimension").GetString() == dimensionName)
            {
                return dimension;
            }
        }

        throw new InvalidOperationException("Dimension was not found: " + dimensionName);
    }

    private static void MutateHistoryIdentity(string historyRoot, string runId, string field, string value)
    {
        var path = Path.Combine(historyRoot, "runs", runId + ".json");
        var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            ?? throw new InvalidOperationException("Unable to parse history entry.");
        root["identity"]!.AsObject()[field] = value;
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

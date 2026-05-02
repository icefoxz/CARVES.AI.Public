using System.Diagnostics;
using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class MatrixCrossPlatformVerifyPilotTests
{
    [Fact]
    public void MatrixCrossPlatformVerifyPilotScript_EmitsCheckpointAndFailureReasonCodes()
    {
        var repoRoot = LocateSourceRepoRoot();
        var artifactRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-cross-platform-pilot-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = RunPwsh(
                repoRoot,
                "scripts/matrix/matrix-cross-platform-verify-pilot.ps1",
                "-ArtifactRoot",
                artifactRoot,
                "-Configuration",
                "Debug");

            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;

            Assert.Equal("matrix-cross-platform-verify-pilot-checkpoint.v0", root.GetProperty("schema_version").GetString());
            Assert.Equal("<redacted-runtime-root>", root.GetProperty("runtime_root").GetString());
            Assert.Equal(".", root.GetProperty("artifact_root").GetString());
            Assert.Equal(5, root.GetProperty("pilot_set").GetProperty("pilot_count").GetInt32());
            Assert.Equal(5, root.GetProperty("verified_pilot_count").GetInt32());
            Assert.True(root.GetProperty("privacy").GetProperty("summary_only").GetBoolean());
            Assert.False(root.GetProperty("public_claims").GetProperty("certification").GetBoolean());
            Assert.DoesNotContain(artifactRoot, result.StandardOutput, StringComparison.Ordinal);

            var pilots = root.GetProperty("pilots").EnumerateArray().ToArray();
            Assert.Equal(RequiredPilotIds, pilots.Select(item => item.GetProperty("pilot_id").GetString()).ToArray());
            Assert.All(
                pilots,
                pilot =>
                {
                    Assert.Equal("verified", pilot.GetProperty("verify_status").GetString());
                    Assert.Equal("verified", pilot.GetProperty("verification_posture").GetString());
                    Assert.Equal(0, pilot.GetProperty("exit_code").GetInt32());
                    Assert.StartsWith("pilots/", pilot.GetProperty("artifact_root").GetString(), StringComparison.Ordinal);
                    Assert.True(pilot.GetProperty("trust_chain_gates_satisfied").GetBoolean());
                    Assert.Empty(pilot.GetProperty("reason_codes").EnumerateArray());
                });

            var failureProbe = root.GetProperty("failure_probe");
            Assert.Equal("failed", failureProbe.GetProperty("verify_status").GetString());
            Assert.Equal(1, failureProbe.GetProperty("exit_code").GetInt32());
            Assert.Equal("hash_mismatch", failureProbe.GetProperty("expected_reason_code").GetString());
            Assert.Contains(
                failureProbe.GetProperty("reason_codes").EnumerateArray(),
                code => code.GetString() == "hash_mismatch");

            var checkpointPath = Path.Combine(artifactRoot, "matrix-cross-platform-verify-pilot-checkpoint.json");
            Assert.True(File.Exists(checkpointPath));
            Assert.Equal(File.ReadAllText(checkpointPath).Trim(), result.StandardOutput.Trim());

            var firstPilotRoot = Path.Combine(artifactRoot, "pilots", RequiredPilotIds[0]);
            var manifestJson = File.ReadAllText(Path.Combine(firstPilotRoot, "matrix-artifact-manifest.json"));
            var proofSummaryJson = File.ReadAllText(Path.Combine(firstPilotRoot, "matrix-proof-summary.json"));
            var matrixSummaryJson = File.ReadAllText(Path.Combine(firstPilotRoot, "project", "matrix-summary.json"));
            Assert.DoesNotContain(artifactRoot, manifestJson, StringComparison.Ordinal);
            Assert.DoesNotContain(artifactRoot, proofSummaryJson, StringComparison.Ordinal);
            Assert.DoesNotContain(artifactRoot, matrixSummaryJson, StringComparison.Ordinal);

            using var manifestDocument = JsonDocument.Parse(manifestJson);
            Assert.Equal(".", manifestDocument.RootElement.GetProperty("artifact_root").GetString());
            Assert.Equal("<redacted-local-artifact-root>", manifestDocument.RootElement.GetProperty("producer_artifact_root").GetString());

            using var proofSummaryDocument = JsonDocument.Parse(proofSummaryJson);
            Assert.Equal("matrix-proof-summary.v0", proofSummaryDocument.RootElement.GetProperty("schema_version").GetString());
            Assert.Equal("native_minimal", proofSummaryDocument.RootElement.GetProperty("proof_mode").GetString());
            Assert.Equal(".", proofSummaryDocument.RootElement.GetProperty("artifact_root").GetString());
            Assert.Equal(".", proofSummaryDocument.RootElement.GetProperty("native").GetProperty("artifact_root").GetString());

            using var matrixSummaryDocument = JsonDocument.Parse(matrixSummaryJson);
            Assert.Equal(".", matrixSummaryDocument.RootElement.GetProperty("artifact_root").GetString());
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void MatrixCrossPlatformVerifyPilotDocsAndWorkflow_RecordWindowsLinuxLane()
    {
        var repoRoot = LocateSourceRepoRoot();
        var workflow = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "matrix-proof.yml"));
        var doc = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "cross-platform-verify-pilot.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "README.md"));
        var limitations = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "known-limitations.md"));
        var script = File.ReadAllText(Path.Combine(repoRoot, "scripts", "matrix", "matrix-cross-platform-verify-pilot.ps1"));

        Assert.Contains("ubuntu-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("windows-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("./scripts/matrix/matrix-proof-lane.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("./scripts/matrix/matrix-cross-platform-verify-pilot.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("artifacts/matrix-pilot-verify/${{ matrix.os }}", workflow, StringComparison.Ordinal);
        Assert.Contains("carves-runtime-integration-pilot-verify-${{ matrix.os }}", workflow, StringComparison.Ordinal);

        foreach (var text in new[] { doc, readme, limitations, script })
        {
            Assert.Contains("matrix-cross-platform-verify-pilot", text, StringComparison.Ordinal);
            Assert.Contains("hash_mismatch", text, StringComparison.Ordinal);
        }

        Assert.Contains("matrix-cross-platform-verify-pilot-checkpoint.v0", doc, StringComparison.Ordinal);
        Assert.Contains("matrix-cross-platform-verify-pilot-checkpoint.json", readme, StringComparison.Ordinal);
        Assert.Contains("summary-only", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("redacts runtime roots", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("artifact_root = \".\"", script, StringComparison.Ordinal);
        Assert.Contains("producer_artifact_root = \"<redacted-local-artifact-root>\"", script, StringComparison.Ordinal);
        Assert.Contains("runtime_root = \"<redacted-runtime-root>\"", script, StringComparison.Ordinal);
        Assert.Contains("Windows", doc, StringComparison.Ordinal);
        Assert.Contains("Linux", doc, StringComparison.Ordinal);
        Assert.Contains("source upload", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("raw diff upload", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("certification", doc, StringComparison.OrdinalIgnoreCase);
    }

    private static readonly string[] RequiredPilotIds =
    [
        "node_single_package",
        "dotnet_small_project",
        "python_package",
        "monorepo_nested_project",
        "dirty_worktree",
    ];

    private static CliProcessResult RunPwsh(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start pwsh.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CliProcessResult(process.ExitCode, stdout, stderr);
    }

    private static string LocateSourceRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CARVES.Runtime.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate CARVES.Runtime source root from test output directory.");
    }

    private sealed record CliProcessResult(int ExitCode, string StandardOutput, string StandardError);
}

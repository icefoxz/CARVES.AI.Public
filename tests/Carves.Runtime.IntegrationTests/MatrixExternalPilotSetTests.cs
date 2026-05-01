using System.Diagnostics;
using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class MatrixExternalPilotSetTests
{
    [Fact]
    public void ExternalPilotCatalog_CoversRequiredShapesAndSummaryOnlyBoundary()
    {
        var repoRoot = LocateSourceRepoRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "matrix",
            "examples",
            "matrix-external-repo-pilot-set.v0.example.json")));
        var root = document.RootElement;

        Assert.Equal("matrix-external-repo-pilot-set.v0", root.GetProperty("schema_version").GetString());
        Assert.True(root.GetProperty("local_only").GetBoolean());
        Assert.True(root.GetProperty("summary_only").GetBoolean());
        Assert.Equal(RequiredPilotIds.Length, root.GetProperty("pilot_count").GetInt32());

        var coveredShapes = root.GetProperty("covered_shapes").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Equal(RequiredPilotIds, coveredShapes);

        foreach (var flag in PrivacyFlags)
        {
            Assert.False(root.GetProperty("privacy").GetProperty(flag).GetBoolean());
        }

        foreach (var claim in PublicClaimFlags)
        {
            Assert.False(root.GetProperty("public_claims").GetProperty(claim).GetBoolean());
        }

        var pilots = root.GetProperty("pilots").EnumerateArray().ToArray();
        Assert.Equal(RequiredPilotIds.Length, pilots.Length);
        Assert.Equal(RequiredPilotIds, pilots.Select(item => item.GetProperty("pilot_id").GetString()).ToArray());

        foreach (var pilot in pilots)
        {
            Assert.False(string.IsNullOrWhiteSpace(pilot.GetProperty("setup").GetProperty("summary").GetString()));
            Assert.NotEmpty(pilot.GetProperty("expected_matrix_behavior").EnumerateArray());
            Assert.NotEmpty(pilot.GetProperty("known_limitations").EnumerateArray());

            var artifactPolicy = pilot.GetProperty("artifact_policy");
            Assert.True(artifactPolicy.GetProperty("summary_only").GetBoolean());
            Assert.NotEmpty(artifactPolicy.GetProperty("allowed_artifacts").EnumerateArray());

            var forbiddenArtifacts = artifactPolicy.GetProperty("forbidden_artifacts").EnumerateArray().Select(item => item.GetString()).ToArray();
            foreach (var forbidden in ForbiddenArtifactMarkers)
            {
                Assert.Contains(forbidden, forbiddenArtifacts);
            }
        }

        var nonClaims = root.GetProperty("non_claims").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains("not certification", nonClaims);
        Assert.Contains("not hosted verification", nonClaims);
        Assert.Contains("not model safety benchmark", nonClaims);
        Assert.Contains("not semantic source correctness proof", nonClaims);
    }

    [Fact]
    public void ExternalPilotDocsAndScripts_ExplainCoverageAndNonCoverage()
    {
        var repoRoot = LocateSourceRepoRoot();
        var readme = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "README.md"));
        var catalogDoc = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "external-repo-pilot-set.md"));
        var limitations = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "known-limitations.md"));
        var script = File.ReadAllText(Path.Combine(repoRoot, "scripts", "matrix", "matrix-external-pilot-set.ps1"));

        foreach (var doc in new[] { readme, catalogDoc, limitations })
        {
            Assert.Contains("small Node", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("small .NET", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Python package", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("monorepo-like nested project", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("dirty worktree", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("summary-only", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("source", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("raw diffs", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("prompts", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("certification", doc, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("docs/matrix/examples/matrix-external-repo-pilot-set.v0.example.json", readme, StringComparison.Ordinal);
        Assert.Contains("scripts/matrix/matrix-external-pilot-set.ps1", catalogDoc, StringComparison.Ordinal);
        Assert.Contains("matrix-external-repo-pilot-set.v0", script, StringComparison.Ordinal);
        Assert.Contains("source_code", script, StringComparison.Ordinal);
        Assert.Contains("raw_git_diff", script, StringComparison.Ordinal);
        Assert.Contains("certification_claim", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ExternalPilotScript_EmitsValidatedSummaryOnlyCatalog()
    {
        var repoRoot = LocateSourceRepoRoot();
        var outputPath = Path.Combine(Path.GetTempPath(), "carves-matrix-pilot-set-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var result = RunPwsh(
                repoRoot,
                "scripts/matrix/matrix-external-pilot-set.ps1",
                "-OutputPath",
                outputPath);

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(outputPath));
            Assert.Equal(File.ReadAllText(outputPath).Trim(), result.StandardOutput.Trim());

            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("matrix-external-repo-pilot-set.v0", root.GetProperty("schema_version").GetString());
            Assert.True(root.GetProperty("summary_only").GetBoolean());
            Assert.Equal(RequiredPilotIds.Length, root.GetProperty("pilots").GetArrayLength());
            Assert.Equal(RequiredPilotIds, root.GetProperty("pilots").EnumerateArray().Select(item => item.GetProperty("pilot_id").GetString()).ToArray());
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    private static readonly string[] RequiredPilotIds =
    [
        "node_single_package",
        "dotnet_small_project",
        "python_package",
        "monorepo_nested_project",
        "dirty_worktree",
    ];

    private static readonly string[] PrivacyFlags =
    [
        "source_included",
        "raw_diff_included",
        "prompt_included",
        "model_response_included",
        "secrets_included",
        "credentials_included",
        "customer_payload_included",
        "hosted_upload_required",
    ];

    private static readonly string[] PublicClaimFlags =
    [
        "certification",
        "hosted_verification",
        "public_leaderboard",
        "model_safety_benchmark",
        "semantic_correctness",
        "operating_system_sandbox",
        "automatic_rollback",
    ];

    private static readonly string[] ForbiddenArtifactMarkers =
    [
        "source_code",
        "raw_git_diff",
        "prompt",
        "model_response",
        "secret",
        "credential",
        "hosted_upload",
        "certification_claim",
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

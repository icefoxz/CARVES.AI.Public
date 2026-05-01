using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed partial class MatrixReleaseReadinessTests
{
    [Fact]
    public void MatrixDocs_ReadinessMentionsClosedProofSummaryContract()
    {
        var repoRoot = LocateSourceRepoRoot();
        var readme = ReadRepoText(repoRoot, "docs/matrix/README.md");
        var manifestDoc = ReadRepoText(repoRoot, "docs/matrix/matrix-artifact-manifest-v0.md");
        var boundary = ReadRepoText(repoRoot, "docs/matrix/public-boundary.md");
        var schema = ReadRepoText(repoRoot, "docs/matrix/schemas/matrix-proof-summary.v0.schema.json");

        Assert.Contains("closed public contract", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Unknown public fields fail verification with `summary_unknown_field:<json_path>`", readme, StringComparison.Ordinal);
        Assert.Contains("closed public field contract", manifestDoc, StringComparison.Ordinal);
        Assert.Contains("Unknown public proof-summary fields fail verification with `summary_unknown_field:<json_path>`", manifestDoc, StringComparison.Ordinal);
        Assert.Contains("`matrix-proof-summary.json` is a closed public readback contract", boundary, StringComparison.Ordinal);
        Assert.Contains("summary_unknown_field:<json_path>", boundary, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(schema);
        var root = document.RootElement;
        Assert.Equal("https://carves.ai/schemas/matrix-proof-summary.v0.schema.json", root.GetProperty("$id").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.Contains("Unknown public fields are verifier failures", root.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Equal("matrix-proof-summary.v0", root.GetProperty("properties").GetProperty("schema_version").GetProperty("const").GetString());
    }

    [Fact]
    public void MatrixDocs_ReadinessMentionsManifestBoundReads()
    {
        var repoRoot = LocateSourceRepoRoot();
        var corpus = string.Join(
            Environment.NewLine,
            new[]
            {
                "docs/matrix/README.md",
                "docs/matrix/matrix-artifact-manifest-v0.md",
                "docs/matrix/public-boundary.md",
                "docs/matrix/known-limitations.md",
                "docs/matrix/quickstart.en.md",
                "docs/matrix/quickstart.zh-CN.md",
            }.Select(path => ReadRepoText(repoRoot, path)));

        Assert.Contains("Both `proof_mode=native_minimal` and `proof_mode=full_release` compare public `proof_capabilities` and `trust_chain_hardening` fields with verifier-computed expectations", corpus, StringComparison.Ordinal);
        Assert.Contains("native summary fields, full release summary fields, and Shield evaluation fields are trusted only after manifest-bound verified reads", corpus, StringComparison.Ordinal);
        Assert.Contains("For `proof_mode=native_minimal`, native summary fields are read from the manifest-covered `project/matrix-summary.json` byte snapshot", corpus, StringComparison.Ordinal);
        Assert.Contains("For `proof_mode=full_release`, summary `project` fields are compared with manifest-covered `project/matrix-summary.json`", corpus, StringComparison.Ordinal);
        Assert.Contains("Shield evaluation semantics are read through the same manifest-bound verified read path before score fields are trusted", corpus, StringComparison.Ordinal);
        Assert.Contains("Each semantic read compares byte length and SHA-256 with the manifest entry", corpus, StringComparison.Ordinal);
        Assert.Contains("does not invoke `pwsh`, Matrix proof scripts, Guard, Handoff, Audit, or Shield", corpus, StringComparison.Ordinal);
    }

    [Fact]
    public void MatrixDocs_ReadinessMentionsPowerShellHelperHardening()
    {
        var repoRoot = LocateSourceRepoRoot();
        var readme = ReadRepoText(repoRoot, "docs/matrix/README.md");
        var limitations = ReadRepoText(repoRoot, "docs/matrix/known-limitations.md");
        var helper = ReadRepoText(repoRoot, "scripts/matrix/matrix-checked-process.ps1");

        Assert.Contains("The PowerShell release lanes use the shared checked process helper", readme, StringComparison.Ordinal);
        Assert.Contains("Invoke-MatrixCheckedProcess", readme, StringComparison.Ordinal);
        Assert.Contains("drains stdout and stderr concurrently", readme, StringComparison.Ordinal);
        Assert.Contains("bounded timeout handling", readme, StringComparison.Ordinal);
        Assert.Contains("does not require `pwsh` or `scripts/matrix/*.ps1`", limitations, StringComparison.Ordinal);
        Assert.Contains("drains stdout and stderr concurrently", limitations, StringComparison.Ordinal);

        Assert.Contains("function Invoke-MatrixCheckedProcess", helper, StringComparison.Ordinal);
        Assert.Contains("Task.Run(() => Pump(process.StandardOutput, stdout))", helper, StringComparison.Ordinal);
        Assert.Contains("Task.Run(() => Pump(process.StandardError, stderr))", helper, StringComparison.Ordinal);
        Assert.Contains("Task.WaitAll(new[] { stdoutTask, stderrTask }, 1000)", helper, StringComparison.Ordinal);
        Assert.Contains("process.Kill(entireProcessTree: true)", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void MatrixDocsReasonCodeTables_IncludeVerifierHardeningCodes()
    {
        var repoRoot = LocateSourceRepoRoot();
        var readme = ReadRepoText(repoRoot, "docs/matrix/README.md");
        var manifestDoc = ReadRepoText(repoRoot, "docs/matrix/matrix-artifact-manifest-v0.md");
        var reasonCorpus = readme + Environment.NewLine + manifestDoc;

        foreach (var reasonCode in new[]
        {
            "missing_artifact",
            "hash_mismatch",
            "schema_mismatch",
            "privacy_violation",
            "unverified_score",
            "unsupported_version",
        })
        {
            Assert.Contains($"`{reasonCode}`", reasonCorpus, StringComparison.Ordinal);
        }

        foreach (var detailCode in new[]
        {
            "summary_source_manifest_entry_missing:<kind>",
            "summary_source_manifest_hash_mismatch:<kind>",
            "summary_source_manifest_size_mismatch:<kind>",
            "summary_unknown_field:<json_path>",
            "summary_trust_chain_hardening_gates_satisfied_mismatch",
            "summary_source_manifest_entry_duplicate:<kind>",
            "summary_source_reparse_point_rejected:<kind>",
            "shield_evaluation_source_manifest_entry_missing:<kind>",
            "shield_evaluation_source_manifest_hash_mismatch:<kind>",
            "shield_evaluation_source_manifest_size_mismatch:<kind>",
            "shield_evaluation_source_manifest_entry_duplicate:<kind>",
            "shield_evaluation_source_reparse_point_rejected:<kind>",
        })
        {
            Assert.Contains(detailCode, reasonCorpus, StringComparison.Ordinal);
        }
    }

    private static string ReadRepoText(string repoRoot, string relativePath)
    {
        return File.ReadAllText(Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}

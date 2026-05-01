namespace Carves.Runtime.IntegrationTests;

public sealed class ShieldGithubActionsProofTests
{
    [Fact]
    public void ShieldGithubActionsProof_DefinesCiSafeWorkflowScriptAndDocs()
    {
        var repoRoot = LocateSourceRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "shield", "shield-github-actions-proof.ps1");
        var workflowPath = Path.Combine(repoRoot, ".github", "workflows", "ci.yml");
        var docsPath = Path.Combine(repoRoot, "docs", "shield", "github-actions-proof-v0.md");
        var shieldReadmePath = Path.Combine(repoRoot, "docs", "shield", "README.md");

        Assert.True(File.Exists(scriptPath));
        Assert.True(File.Exists(workflowPath));
        Assert.True(File.Exists(docsPath));

        var script = File.ReadAllText(scriptPath);
        Assert.Contains("shield-github-actions-proof.v0", script, StringComparison.Ordinal);
        Assert.Contains("CARVES.Shield.Cli", script, StringComparison.Ordinal);
        Assert.Contains("carves-shield", script, StringComparison.Ordinal);
        Assert.Contains("\"evaluate\",", script, StringComparison.Ordinal);
        Assert.Contains("\"badge\",", script, StringComparison.Ordinal);
        Assert.Contains("shield-evaluate.json", script, StringComparison.Ordinal);
        Assert.Contains("shield-badge.json", script, StringComparison.Ordinal);
        Assert.Contains("shield-badge.svg", script, StringComparison.Ordinal);
        Assert.Contains("shield-github-actions-proof.json", script, StringComparison.Ordinal);
        Assert.Contains("provider_secrets_required = $false", script, StringComparison.Ordinal);
        Assert.Contains("hosted_api_required = $false", script, StringComparison.Ordinal);
        Assert.Contains("network_calls_required = $false", script, StringComparison.Ordinal);
        Assert.Contains("source_upload_required = $false", script, StringComparison.Ordinal);
        Assert.Contains("raw_diff_upload_required = $false", script, StringComparison.Ordinal);
        Assert.Contains("prompt_upload_required = $false", script, StringComparison.Ordinal);
        Assert.Contains("secret_upload_required = $false", script, StringComparison.Ordinal);
        Assert.Contains("credential_upload_required = $false", script, StringComparison.Ordinal);
        Assert.Contains("public_directory_required = $false", script, StringComparison.Ordinal);
        Assert.Contains("certification_claimed = $false", script, StringComparison.Ordinal);

        var workflow = File.ReadAllText(workflowPath);
        Assert.Contains("Shield GitHub Actions proof lane", workflow, StringComparison.Ordinal);
        Assert.Contains("./scripts/shield/shield-github-actions-proof.ps1 -Configuration Release -SkipBuild", workflow, StringComparison.Ordinal);
        Assert.Contains("Upload Shield proof artifacts", workflow, StringComparison.Ordinal);
        Assert.Contains("actions/upload-artifact@v4", workflow, StringComparison.Ordinal);
        Assert.Contains("shield-proof-${{ matrix.os }}", workflow, StringComparison.Ordinal);
        Assert.Contains("artifacts/shield", workflow, StringComparison.Ordinal);

        var docs = File.ReadAllText(docsPath);
        Assert.Contains("self-check only", docs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("provider_secrets_required", docs, StringComparison.Ordinal);
        Assert.Contains("source_upload_required", docs, StringComparison.Ordinal);
        Assert.Contains("certification_claimed", docs, StringComparison.Ordinal);
        Assert.Contains("carves-shield evaluate .carves/shield-evidence.json", docs, StringComparison.Ordinal);
        Assert.Contains("carves-shield badge .carves/shield-evidence.json", docs, StringComparison.Ordinal);
        Assert.Contains("carves shield evaluate <evidence-path>", docs, StringComparison.Ordinal);
        Assert.Contains("carves shield badge <evidence-path>", docs, StringComparison.Ordinal);
        Assert.Contains("Do not upload source code", docs, StringComparison.Ordinal);

        var shieldReadme = File.ReadAllText(shieldReadmePath);
        Assert.Contains("github-actions-proof-v0.md", shieldReadme, StringComparison.Ordinal);
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
}

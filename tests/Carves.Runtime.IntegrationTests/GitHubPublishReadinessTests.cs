namespace Carves.Runtime.IntegrationTests;

public sealed class GitHubPublishReadinessTests
{
    [Fact]
    public void PublishReadinessScript_DefinesLocalManifestAndOperatorGates()
    {
        var repoRoot = LocateSourceRepoRoot();
        var script = File.ReadAllText(Path.Combine(repoRoot, "scripts", "release", "github-publish-readiness.ps1"));

        Assert.Contains("carves-github-publish-readiness.v1", script, StringComparison.Ordinal);
        Assert.Contains("dotnet", script, StringComparison.Ordinal);
        Assert.Contains("\"pack\"", script, StringComparison.Ordinal);
        Assert.Contains("--no-restore", script, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", script, StringComparison.Ordinal);
        Assert.Contains("sha256", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CARVES.Runtime.Cli", script, StringComparison.Ordinal);
        Assert.Contains("CARVES.Guard.Core", script, StringComparison.Ordinal);
        Assert.Contains("CARVES.Guard.Cli", script, StringComparison.Ordinal);
        Assert.Contains("CARVES.Shield.Core", script, StringComparison.Ordinal);
        Assert.Contains("CARVES.Shield.Cli", script, StringComparison.Ordinal);
        Assert.Contains("CARVES.Matrix.Core", script, StringComparison.Ordinal);
        Assert.Contains("CARVES.Matrix.Cli", script, StringComparison.Ordinal);
        Assert.Contains("CARVES.Handoff.Core", script, StringComparison.Ordinal);
        Assert.Contains("CARVES.Handoff.Cli", script, StringComparison.Ordinal);
        Assert.Contains("CARVES.Audit.Core", script, StringComparison.Ordinal);
        Assert.Contains("CARVES.Audit.Cli", script, StringComparison.Ordinal);
        Assert.Contains("docs/release/trust-chain-hardening-release-checkpoint.md", script, StringComparison.Ordinal);
        Assert.Contains("docs/release/matrix-verifiable-local-self-check-checkpoint.md", script, StringComparison.Ordinal);
        Assert.Contains("docs/release/matrix-operator-release-gate.md", script, StringComparison.Ordinal);
        Assert.Contains("github_token_required = $false", script, StringComparison.Ordinal);
        Assert.Contains("nuget_token_required = $false", script, StringComparison.Ordinal);
        Assert.Contains("network_required = $false", script, StringComparison.Ordinal);
        Assert.Contains("create_github_release = \"not_performed\"", script, StringComparison.Ordinal);
        Assert.Contains("push_packages_to_nuget_org = \"not_performed\"", script, StringComparison.Ordinal);
        Assert.Contains("sign_packages = \"not_performed\"", script, StringComparison.Ordinal);
        Assert.Contains("release_created = $false", script, StringComparison.Ordinal);
        Assert.Contains("tag_created = $false", script, StringComparison.Ordinal);
        Assert.Contains("nuget_published = $false", script, StringComparison.Ordinal);
        Assert.Contains("operating_system_sandbox = $false", script, StringComparison.Ordinal);
    }

    [Fact]
    public void PublishReadinessDocs_StateBoundaryAndReleaseDraftWithoutOverclaims()
    {
        var repoRoot = LocateSourceRepoRoot();
        var boundary = File.ReadAllText(Path.Combine(repoRoot, "docs", "release", "github-publish-readiness-boundary.md"));
        var draft = File.ReadAllText(Path.Combine(repoRoot, "docs", "release", "github-release-draft.md"));
        var checkpoint = File.ReadAllText(Path.Combine(repoRoot, "docs", "release", "github-publish-readiness-checkpoint.md"));
        var trustChainCheckpoint = File.ReadAllText(Path.Combine(repoRoot, "docs", "release", "trust-chain-hardening-release-checkpoint.md"));
        var operatorGate = File.ReadAllText(Path.Combine(repoRoot, "docs", "release", "matrix-operator-release-gate.md"));

        foreach (var doc in new[] { boundary, draft, checkpoint, trustChainCheckpoint, operatorGate })
        {
            Assert.Contains("GitHub", doc, StringComparison.Ordinal);
            Assert.Contains("NuGet.org", doc, StringComparison.Ordinal);
            Assert.Contains("operator", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("shield-evidence.v0", doc, StringComparison.Ordinal);
            Assert.Contains("operating-system sandbox", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("hosted verification", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("public leaderboard", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("certification", doc, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("has been published", doc, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("certified secure", doc, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("matrix-v0.1.0-rc.1", draft, StringComparison.Ordinal);
        Assert.Contains("Asset Checklist", draft, StringComparison.Ordinal);
        Assert.Contains("Non-Performed Actions", draft, StringComparison.Ordinal);
        Assert.Contains("carves-github-publish-readiness.v1", checkpoint, StringComparison.Ordinal);
        Assert.Contains("ready for an operator to publish", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CARD-805", checkpoint, StringComparison.Ordinal);
        Assert.Contains("No unresolved P0/P1 trust-chain debt remains", trustChainCheckpoint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublicDocsIndex_LinksMatrixAndPublishReadinessEntrypoints()
    {
        var repoRoot = LocateSourceRepoRoot();
        var index = File.ReadAllText(Path.Combine(repoRoot, "docs", "INDEX.md"));

        Assert.Contains("Public Matrix Entry", index, StringComparison.Ordinal);
        Assert.Contains("matrix/README.md", index, StringComparison.Ordinal);
        Assert.Contains("matrix/public-boundary.md", index, StringComparison.Ordinal);
        Assert.Contains("release/matrix-release-notes.md", index, StringComparison.Ordinal);
        Assert.Contains("matrix/known-limitations.md", index, StringComparison.Ordinal);
        Assert.Contains("matrix/github-actions-proof.md", index, StringComparison.Ordinal);
        Assert.Contains("matrix/packaged-install-matrix.md", index, StringComparison.Ordinal);
        Assert.Contains("release/matrix-github-release-candidate-checkpoint.md", index, StringComparison.Ordinal);
        Assert.Contains("release/trust-chain-hardening-release-checkpoint.md", index, StringComparison.Ordinal);
        Assert.Contains("release/github-publish-readiness-boundary.md", index, StringComparison.Ordinal);
        Assert.Contains("release/github-release-draft.md", index, StringComparison.Ordinal);
        Assert.Contains("release/github-publish-readiness-checkpoint.md", index, StringComparison.Ordinal);
        Assert.Contains("release/matrix-verifiable-local-self-check-checkpoint.md", index, StringComparison.Ordinal);
        Assert.Contains("release/matrix-operator-release-gate.md", index, StringComparison.Ordinal);
        Assert.Contains("Guard / Handoff / Audit / Shield", index, StringComparison.Ordinal);
    }

    [Fact]
    public void OperatorReleaseGate_ListsEvidenceActionsDeferralsAndNonAutomaticPublication()
    {
        var repoRoot = LocateSourceRepoRoot();
        var gate = File.ReadAllText(Path.Combine(repoRoot, "docs", "release", "matrix-operator-release-gate.md"));
        var draft = File.ReadAllText(Path.Combine(repoRoot, "docs", "release", "github-release-draft.md"));
        var checkpoint = File.ReadAllText(Path.Combine(repoRoot, "docs", "release", "github-publish-readiness-checkpoint.md"));

        Assert.Contains("## Publication State", gate, StringComparison.Ordinal);
        Assert.Contains("Git tag: not created", gate, StringComparison.Ordinal);
        Assert.Contains("GitHub release: not created", gate, StringComparison.Ordinal);
        Assert.Contains("NuGet.org packages: not pushed", gate, StringComparison.Ordinal);
        Assert.Contains("Package signing: not performed", gate, StringComparison.Ordinal);
        Assert.Contains("Local verification work remains valid", gate, StringComparison.Ordinal);

        Assert.Contains("## Required Evidence Before Publication", gate, StringComparison.Ordinal);
        Assert.Contains("source commit SHA", gate, StringComparison.Ordinal);
        Assert.Contains("matrix-verify.json", gate, StringComparison.Ordinal);
        Assert.Contains("matrix-artifact-manifest.json", gate, StringComparison.Ordinal);
        Assert.Contains("matrix-proof-summary.json", gate, StringComparison.Ordinal);
        Assert.Contains("Shield Lite starter challenge smoke output", gate, StringComparison.Ordinal);
        Assert.Contains("cross-platform Matrix verify pilot output", gate, StringComparison.Ordinal);
        Assert.Contains("package ids, versions, sizes, and SHA-256 values", gate, StringComparison.Ordinal);
        Assert.Contains("git diff --check", gate, StringComparison.Ordinal);

        Assert.Contains("## Operator Actions", gate, StringComparison.Ordinal);
        Assert.Contains("Create the Git tag only after the evidence above is accepted", gate, StringComparison.Ordinal);
        Assert.Contains("Create the GitHub release only after the tag is accepted", gate, StringComparison.Ordinal);
        Assert.Contains("Push packages to NuGet.org only after token, owner, and signing decisions are accepted", gate, StringComparison.Ordinal);
        Assert.Contains("Record any deferred operator gate explicitly", gate, StringComparison.Ordinal);

        Assert.Contains("## Non-Automatic Gate", gate, StringComparison.Ordinal);
        Assert.Contains("tag creation", gate, StringComparison.Ordinal);
        Assert.Contains("GitHub release creation", gate, StringComparison.Ordinal);
        Assert.Contains("NuGet.org package push", gate, StringComparison.Ordinal);
        Assert.Contains("package signing", gate, StringComparison.Ordinal);
        Assert.Contains("github-publish-readiness.ps1", gate, StringComparison.Ordinal);
        Assert.Contains("must not use GitHub tokens", gate, StringComparison.Ordinal);
        Assert.Contains("NuGet tokens", gate, StringComparison.Ordinal);

        Assert.Contains("## Deferral Outcomes", gate, StringComparison.Ordinal);
        Assert.Contains("defer_publication", gate, StringComparison.Ordinal);
        Assert.Contains("defer_nuget", gate, StringComparison.Ordinal);
        Assert.Contains("defer_signing", gate, StringComparison.Ordinal);
        Assert.Contains("Deferral is not a failed Matrix verification result", gate, StringComparison.Ordinal);

        Assert.Contains("## Required Publication Record", gate, StringComparison.Ordinal);
        Assert.Contains("## Public Non-Claims", gate, StringComparison.Ordinal);
        Assert.Contains("hosted verification", gate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("public certification", gate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("semantic source-code correctness", gate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("operating-system sandboxing", gate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("raw diff upload", gate, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secret upload", gate, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("docs/release/matrix-operator-release-gate.md", draft, StringComparison.Ordinal);
        Assert.Contains("docs/release/matrix-operator-release-gate.md", checkpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("tag_created = $true", gate, StringComparison.Ordinal);
        Assert.DoesNotContain("nuget_published = $true", gate, StringComparison.Ordinal);
        Assert.DoesNotContain("certified by CARVES", gate, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("verified safe", gate, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublishReadinessTaskPayloads_AreMaterializedAndBounded()
    {
        var repoRoot = LocateSourceRepoRoot();
        var payloadRoot = Path.Combine(repoRoot, ".ai", "runtime", "planning", "payloads");
        if (!Directory.Exists(payloadRoot))
        {
            return;
        }

        for (var card = 786; card <= 790; card++)
        {
            var cardPayload = File.ReadAllText(Path.Combine(payloadRoot, $"CARD-{card}.json"));
            var graphPayload = File.ReadAllText(Path.Combine(payloadRoot, $"TG-CARD-{card}.json"));
            var taskNode = File.ReadAllText(Path.Combine(repoRoot, ".ai", "tasks", "nodes", $"T-CARD-{card}-001.json"));

            Assert.Contains($"CARD-{card}", cardPayload, StringComparison.Ordinal);
            Assert.Contains($"T-CARD-{card}-001", graphPayload, StringComparison.Ordinal);
            Assert.Contains($"\"task_id\": \"T-CARD-{card}-001\"", taskNode, StringComparison.Ordinal);
            Assert.Contains($"\"card_id\": \"CARD-{card}\"", taskNode, StringComparison.Ordinal);
            Assert.Contains("Do not", cardPayload, StringComparison.Ordinal);
        }
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

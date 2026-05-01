namespace Carves.Runtime.IntegrationTests;

public sealed partial class MatrixReleaseReadinessTests
{
    [Fact]
    public void MatrixScripts_DefineProjectPackagedAndProofLanes()
    {
        var repoRoot = LocateSourceRepoRoot();
        var e2e = File.ReadAllText(Path.Combine(repoRoot, "scripts", "matrix", "matrix-e2e-smoke.ps1"));
        var packaged = File.ReadAllText(Path.Combine(repoRoot, "scripts", "matrix", "matrix-packaged-install-smoke.ps1"));
        var proof = File.ReadAllText(Path.Combine(repoRoot, "scripts", "matrix", "matrix-proof-lane.ps1"));

        Assert.Contains("matrix_e2e", e2e, StringComparison.Ordinal);
        Assert.Contains("CARVES.Guard.Cli", e2e, StringComparison.Ordinal);
        Assert.Contains("CARVES.Shield.Cli", e2e, StringComparison.Ordinal);
        Assert.Contains("\"init\", \"--json\"", e2e, StringComparison.Ordinal);
        Assert.Contains("\"check\", \"--json\"", e2e, StringComparison.Ordinal);
        Assert.Contains("\"draft\", \"--json\"", e2e, StringComparison.Ordinal);
        Assert.Contains("\"summary\", \"--json\"", e2e, StringComparison.Ordinal);
        Assert.Contains("\"evidence\", \"--json\", \"--output\", \".carves/shield-evidence.json\"", e2e, StringComparison.Ordinal);
        Assert.Contains("\"evaluate\", \".carves/shield-evidence.json\", \"--json\", \"--output\", \"combined\"", e2e, StringComparison.Ordinal);
        Assert.Contains("\"badge\", \".carves/shield-evidence.json\", \"--json\", \"--output\"", e2e, StringComparison.Ordinal);
        Assert.Contains("source_upload_required = $false", e2e, StringComparison.Ordinal);
        Assert.Contains("raw_diff_upload_required = $false", e2e, StringComparison.Ordinal);
        Assert.Contains("prompt_upload_required = $false", e2e, StringComparison.Ordinal);
        Assert.Contains("model_response_upload_required = $false", e2e, StringComparison.Ordinal);
        Assert.Contains("proof_role = \"composition_orchestrator\"", e2e, StringComparison.Ordinal);
        Assert.Contains("scoring_owner = \"shield\"", e2e, StringComparison.Ordinal);
        Assert.Contains("alters_shield_score = $false", e2e, StringComparison.Ordinal);
        Assert.Contains("shield_evaluation_artifact = \"shield-evaluate.json\"", e2e, StringComparison.Ordinal);
        Assert.Contains("trust_chain_hardening = [pscustomobject]@", e2e, StringComparison.Ordinal);
        Assert.DoesNotContain("gates_satisfied = $true", e2e, StringComparison.Ordinal);
        Assert.Contains("complete_card_796", e2e, StringComparison.Ordinal);
        Assert.Contains("complete_card_797", e2e, StringComparison.Ordinal);
        Assert.Contains("complete_card_798", e2e, StringComparison.Ordinal);
        Assert.Contains("complete_card_799", e2e, StringComparison.Ordinal);
        Assert.Contains("complete_card_800", e2e, StringComparison.Ordinal);
        Assert.Contains("complete_card_801", e2e, StringComparison.Ordinal);
        Assert.Contains("complete_card_802", e2e, StringComparison.Ordinal);
        Assert.Contains("complete_card_803", e2e, StringComparison.Ordinal);
        Assert.Contains("complete_card_804", e2e, StringComparison.Ordinal);
        Assert.Contains("complete_card_805", e2e, StringComparison.Ordinal);
        Assert.Contains("local_self_check_only", e2e, StringComparison.Ordinal);
        Assert.Contains("limited_to_local_self_check", e2e, StringComparison.Ordinal);
        Assert.DoesNotContain("pending_card_", e2e, StringComparison.Ordinal);
        Assert.Contains("certification = $false", e2e, StringComparison.Ordinal);
        Assert.Contains("public_leaderboard = $false", e2e, StringComparison.Ordinal);
        Assert.Contains("hosted_verification = $false", e2e, StringComparison.Ordinal);
        Assert.Contains("os_sandbox_claim = $false", e2e, StringComparison.Ordinal);

        Assert.Contains("matrix_packaged_install", packaged, StringComparison.Ordinal);
        Assert.Contains("CARVES.Guard.Cli", packaged, StringComparison.Ordinal);
        Assert.Contains("CARVES.Handoff.Cli", packaged, StringComparison.Ordinal);
        Assert.Contains("CARVES.Audit.Cli", packaged, StringComparison.Ordinal);
        Assert.Contains("CARVES.Shield.Cli", packaged, StringComparison.Ordinal);
        Assert.Contains("CARVES.Matrix.Cli", packaged, StringComparison.Ordinal);
        Assert.Contains("carves-matrix", packaged, StringComparison.Ordinal);
        Assert.Contains("\"Installed\"", packaged, StringComparison.Ordinal);
        Assert.Contains("remote_registry_published = $false", packaged, StringComparison.Ordinal);
        Assert.Contains("nuget_org_push_required = $false", packaged, StringComparison.Ordinal);

        Assert.Contains("matrix_proof_lane", proof, StringComparison.Ordinal);
        Assert.Contains("CARVES.Matrix.Cli", proof, StringComparison.Ordinal);
        Assert.Contains("proof", proof, StringComparison.Ordinal);
    }

    [Fact]
    public void MatrixFullReleaseScripts_RedactProducerLocalPathsInPublicSummaries()
    {
        var repoRoot = LocateSourceRepoRoot();
        var e2e = File.ReadAllText(Path.Combine(repoRoot, "scripts", "matrix", "matrix-e2e-smoke.ps1"));
        var packaged = File.ReadAllText(Path.Combine(repoRoot, "scripts", "matrix", "matrix-packaged-install-smoke.ps1"));

        Assert.Contains("target_repository = \"<redacted-target-repository>\"", e2e, StringComparison.Ordinal);
        Assert.Contains("artifact_root = \".\"", e2e, StringComparison.Ordinal);
        Assert.DoesNotContain("target_repository = $TargetRepo", e2e, StringComparison.Ordinal);
        Assert.DoesNotContain("artifact_root = $ArtifactRoot", e2e, StringComparison.Ordinal);

        Assert.Contains("package_root = \"<redacted-local-package-root>\"", packaged, StringComparison.Ordinal);
        Assert.Contains("tool_root = \"<redacted-local-tool-root>\"", packaged, StringComparison.Ordinal);
        Assert.Contains("artifact_root = \".\"", packaged, StringComparison.Ordinal);
        Assert.Contains("carves_guard = \"carves-guard\"", packaged, StringComparison.Ordinal);
        Assert.Contains("carves_handoff = \"carves-handoff\"", packaged, StringComparison.Ordinal);
        Assert.Contains("carves_audit = \"carves-audit\"", packaged, StringComparison.Ordinal);
        Assert.Contains("carves_shield = \"carves-shield\"", packaged, StringComparison.Ordinal);
        Assert.Contains("carves_matrix = \"carves-matrix\"", packaged, StringComparison.Ordinal);
        Assert.DoesNotContain("package_root = $packageRoot", packaged, StringComparison.Ordinal);
        Assert.DoesNotContain("tool_root = $toolRoot", packaged, StringComparison.Ordinal);
        Assert.DoesNotContain("artifact_root = $ArtifactRoot", packaged, StringComparison.Ordinal);
        Assert.DoesNotContain("carves_guard = $guardCommand", packaged, StringComparison.Ordinal);
        Assert.DoesNotContain("carves_handoff = $handoffCommand", packaged, StringComparison.Ordinal);
        Assert.DoesNotContain("carves_audit = $auditCommand", packaged, StringComparison.Ordinal);
        Assert.DoesNotContain("carves_shield = $shieldCommand", packaged, StringComparison.Ordinal);
        Assert.DoesNotContain("carves_matrix = $matrixCommand", packaged, StringComparison.Ordinal);
    }

    [Fact]
    public void MatrixGithubActionsWorkflow_RunsCrossPlatformSummaryOnlyProof()
    {
        var repoRoot = LocateSourceRepoRoot();
        var workflow = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "matrix-proof.yml"));
        var proofDoc = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "github-actions-proof.md"));

        Assert.Contains("ubuntu-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("windows-latest", workflow, StringComparison.Ordinal);
        Assert.Contains("actions/checkout@v4", workflow, StringComparison.Ordinal);
        Assert.Contains("actions/setup-dotnet@v4", workflow, StringComparison.Ordinal);
        Assert.Contains("./scripts/matrix/matrix-proof-lane.ps1", workflow, StringComparison.Ordinal);
        Assert.Contains("Verify matrix proof bundle", workflow, StringComparison.Ordinal);
        Assert.Contains("verify `", workflow, StringComparison.Ordinal);
        Assert.Contains("artifacts/matrix-verify/${{ matrix.os }}", workflow, StringComparison.Ordinal);
        Assert.Contains("matrix-verify.json", workflow, StringComparison.Ordinal);
        Assert.Contains("matrix-artifact-manifest.json", workflow, StringComparison.Ordinal);
        Assert.Contains("matrix-proof-summary.json", workflow, StringComparison.Ordinal);
        Assert.Contains("actions/upload-artifact@v4", workflow, StringComparison.Ordinal);
        Assert.Contains("carves-matrix-proof", workflow, StringComparison.Ordinal);
        Assert.Contains("carves-matrix-verify-${{ matrix.os }}", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("secrets.", workflow, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("carves-matrix verify artifacts/matrix/<os> --json", proofDoc, StringComparison.Ordinal);
        Assert.Contains("matrix-verify.v0", proofDoc, StringComparison.Ordinal);
        Assert.Contains("matrix-verify.json", proofDoc, StringComparison.Ordinal);
        Assert.Contains("matrix-artifact-manifest.json", proofDoc, StringComparison.Ordinal);
        Assert.Contains("matrix-proof-summary.json", proofDoc, StringComparison.Ordinal);
        Assert.Contains("summary-only", proofDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("native .NET artifact recheck path", proofDoc, StringComparison.Ordinal);
        Assert.Contains("CARVES does not require upload of private source, raw diff text, prompts, model responses, secrets, credentials, or customer payloads", proofDoc, StringComparison.Ordinal);
        Assert.Contains("raw diffs", proofDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("prompts", proofDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("model responses", proofDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a hosted verification service", proofDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not an operating-system sandbox proof", proofDoc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RepositoryPublicHygieneFiles_ArePresentAndBoundaryHonest()
    {
        var repoRoot = LocateSourceRepoRoot();
        var requiredFiles = new[]
        {
            "LICENSE",
            "CONTRIBUTING.md",
            "SECURITY.md",
            "CODE_OF_CONDUCT.md",
            ".github/ISSUE_TEMPLATE/bug_report.md",
            ".github/ISSUE_TEMPLATE/feature_request.md",
            ".github/PULL_REQUEST_TEMPLATE.md",
        };

        foreach (var relativePath in requiredFiles)
        {
            Assert.True(File.Exists(Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar))), relativePath);
        }

        var contributing = File.ReadAllText(Path.Combine(repoRoot, "CONTRIBUTING.md"));
        var security = File.ReadAllText(Path.Combine(repoRoot, "SECURITY.md"));
        var bug = File.ReadAllText(Path.Combine(repoRoot, ".github", "ISSUE_TEMPLATE", "bug_report.md"));
        var feature = File.ReadAllText(Path.Combine(repoRoot, ".github", "ISSUE_TEMPLATE", "feature_request.md"));
        var pr = File.ReadAllText(Path.Combine(repoRoot, ".github", "PULL_REQUEST_TEMPLATE.md"));

        foreach (var doc in new[] { contributing, security, bug, feature, pr })
        {
            Assert.Contains("source", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("raw diff", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("prompt", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secret", doc, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("guaranteed support", doc, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("certified secure", doc, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("does not claim operating-system sandboxing", security, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("issue-first", contributing, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublicReadme_ExplainsMatrixWithoutInternalRuntimeNarrative()
    {
        var repoRoot = LocateSourceRepoRoot();
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));

        Assert.Contains("Guard", readme, StringComparison.Ordinal);
        Assert.Contains("Handoff", readme, StringComparison.Ordinal);
        Assert.Contains("Audit", readme, StringComparison.Ordinal);
        Assert.Contains("Shield", readme, StringComparison.Ordinal);
        Assert.Contains("pwsh ./scripts/matrix/matrix-proof-lane.ps1", readme, StringComparison.Ordinal);
        Assert.Contains("carves-guard init", readme, StringComparison.Ordinal);
        Assert.Contains("carves-audit evidence --json --output .carves/shield-evidence.json", readme, StringComparison.Ordinal);
        Assert.Contains("carves-shield evaluate .carves/shield-evidence.json", readme, StringComparison.Ordinal);
        Assert.Contains("carves-matrix proof", readme, StringComparison.Ordinal);
        Assert.Contains(".github/workflows/matrix-proof.yml", readme, StringComparison.Ordinal);
        Assert.Contains("docs/matrix/known-limitations.md", readme, StringComparison.Ordinal);
        Assert.Contains("local AI coding workflow governance self-check", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a model safety benchmark", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not rate model safety", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not automatically roll back arbitrary writes", readme, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("TaskGraph", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkerService", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("governance wave", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Planner / Worker", readme, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReleaseNotesLimitationsAndCheckpoint_FreezeGithubPublishableMatrixPosture()
    {
        var repoRoot = LocateSourceRepoRoot();
        var releaseNotes = File.ReadAllText(Path.Combine(repoRoot, "docs", "release", "matrix-release-notes.md"));
        var limitations = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "known-limitations.md"));
        var checkpoint = File.ReadAllText(Path.Combine(repoRoot, "docs", "release", "matrix-github-release-candidate-checkpoint.md"));
        var trustChainCheckpoint = File.ReadAllText(Path.Combine(repoRoot, "docs", "release", "trust-chain-hardening-release-checkpoint.md"));
        var packagedDoc = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "packaged-install-matrix.md"));

        foreach (var doc in new[] { releaseNotes, limitations, checkpoint, trustChainCheckpoint, packagedDoc })
        {
            Assert.Contains("shield-evidence.v0", doc, StringComparison.Ordinal);
            Assert.Contains("model safety", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("NuGet.org", doc, StringComparison.Ordinal);
            Assert.Contains("operating-system sandbox", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("hosted verification", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("public leaderboard", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("certification", doc, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("local self-check only", releaseNotes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Audit evidence integrity", releaseNotes, StringComparison.Ordinal);
        Assert.Contains("Guard deletion/replacement honesty", releaseNotes, StringComparison.Ordinal);
        Assert.Contains("Shield evidence contract alignment", releaseNotes, StringComparison.Ordinal);
        Assert.Contains("Handoff terminal-state semantics", releaseNotes, StringComparison.Ordinal);
        Assert.Contains("Matrix-to-Shield provenance linkage", releaseNotes, StringComparison.Ordinal);
        Assert.Contains("Internal checkpoint documents retain exact CARD traceability", releaseNotes, StringComparison.Ordinal);
        Assert.DoesNotContain("CARD-", releaseNotes, StringComparison.Ordinal);
        Assert.DoesNotContain("CARD-", limitations, StringComparison.Ordinal);
        Assert.Contains("local self-check claims", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("local self-check only", trustChainCheckpoint, StringComparison.OrdinalIgnoreCase);
        for (var card = 796; card <= 805; card++)
        {
            Assert.Contains($"CARD-{card}", checkpoint, StringComparison.Ordinal);
            Assert.Contains($"CARD-{card}", trustChainCheckpoint, StringComparison.Ordinal);
        }

        Assert.Contains("No unresolved P0/P1 trust-chain debt remains", trustChainCheckpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("public Shield self-check claims are limited to local workflow governance output", trustChainCheckpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dotnet build CARVES.Runtime.sln --no-restore", trustChainCheckpoint, StringComparison.Ordinal);
        Assert.Contains("pwsh ./scripts/matrix/matrix-packaged-install-smoke.ps1", trustChainCheckpoint, StringComparison.Ordinal);
        Assert.Contains("pwsh ./scripts/release/github-publish-readiness.ps1", trustChainCheckpoint, StringComparison.Ordinal);
        Assert.Contains("Guard -> Handoff -> Audit -> Shield", releaseNotes, StringComparison.Ordinal);
        Assert.Contains("GitHub-publishable", checkpoint, StringComparison.Ordinal);
        Assert.Contains("CARD-779", checkpoint, StringComparison.Ordinal);
        Assert.Contains("CARD-785", checkpoint, StringComparison.Ordinal);
        Assert.Contains("scripts/matrix/matrix-proof-lane.ps1", checkpoint, StringComparison.Ordinal);
        Assert.Contains("local package installation", packagedDoc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublicReleaseNotes_ExplainCheckedVerifiedNonClaimsAndReproduction()
    {
        var repoRoot = LocateSourceRepoRoot();
        var releaseNotes = File.ReadAllText(Path.Combine(repoRoot, "docs", "release", "matrix-release-notes.md"));
        var releaseDraft = File.ReadAllText(Path.Combine(repoRoot, "docs", "release", "github-release-draft.md"));
        var releaseGates = File.ReadAllText(Path.Combine(repoRoot, "docs", "release", "matrix-public-release-gates.md"));

        foreach (var doc in new[] { releaseNotes, releaseDraft })
        {
            Assert.Contains("## What Is Checked", doc, StringComparison.Ordinal);
            Assert.Contains("## What Is Verified", doc, StringComparison.Ordinal);
            Assert.Contains("## What Is Not Claimed", doc, StringComparison.Ordinal);
            Assert.Contains("matrix-artifact-manifest.json", doc, StringComparison.Ordinal);
            Assert.Contains("matrix-proof-summary.json", doc, StringComparison.Ordinal);
            Assert.Contains("matrix-verify", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("certification=false", doc, StringComparison.Ordinal);
            Assert.Contains("not a model safety benchmark", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("hosted verification", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("public leaderboard", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("public certification", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("semantic source-code correctness", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("automatic rollback", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("source", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("raw diff", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("prompt", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secret", doc, StringComparison.OrdinalIgnoreCase);

            Assert.DoesNotContain("certified by CARVES", doc, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("verified safe", doc, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("hosted verification is available", doc, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("public leaderboard is available", doc, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("NuGet.org publication is complete", doc, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("## How To Reproduce", releaseNotes, StringComparison.Ordinal);
        Assert.Contains("dotnet build CARVES.Runtime.sln --configuration Release", releaseNotes, StringComparison.Ordinal);
        Assert.Contains("matrix-proof-lane.ps1", releaseNotes, StringComparison.Ordinal);
        Assert.Contains("verify artifacts/matrix/local --json", releaseNotes, StringComparison.Ordinal);
        Assert.Contains("github-publish-readiness.ps1", releaseNotes, StringComparison.Ordinal);
        Assert.DoesNotContain("CARD-", releaseNotes, StringComparison.Ordinal);
        Assert.DoesNotContain("CARD-", releaseDraft, StringComparison.Ordinal);
        Assert.DoesNotContain("CARD-", releaseGates, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifiableLocalSelfCheckCheckpoint_RecordsValidationCommandsReadinessAndNonClaims()
    {
        var repoRoot = LocateSourceRepoRoot();
        var checkpoint = File.ReadAllText(Path.Combine(repoRoot, "docs", "release", "matrix-verifiable-local-self-check-checkpoint.md"));

        Assert.Contains("## Verdict", checkpoint, StringComparison.Ordinal);
        Assert.Contains("ready for operator-controlled GitHub publication", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("## What Is Checked", checkpoint, StringComparison.Ordinal);
        Assert.Contains("## What Is Verified", checkpoint, StringComparison.Ordinal);
        Assert.Contains("## Required Command Set", checkpoint, StringComparison.Ordinal);
        Assert.Contains("## Current Local Readback", checkpoint, StringComparison.Ordinal);
        Assert.Contains("## Remaining Operator Gates", checkpoint, StringComparison.Ordinal);
        Assert.Contains("## Remaining Limitations", checkpoint, StringComparison.Ordinal);
        Assert.Contains("## Public Non-Claims", checkpoint, StringComparison.Ordinal);

        Assert.Contains("dotnet build CARVES.Runtime.sln --configuration Release", checkpoint, StringComparison.Ordinal);
        Assert.Contains("--filter Matrix --no-restore", checkpoint, StringComparison.Ordinal);
        Assert.Contains("matrix-proof-lane.ps1", checkpoint, StringComparison.Ordinal);
        Assert.Contains("verify artifacts/matrix/local --json", checkpoint, StringComparison.Ordinal);
        Assert.Contains("shield-lite-starter-challenge-smoke.ps1", checkpoint, StringComparison.Ordinal);
        Assert.Contains("matrix-external-pilot-set.ps1", checkpoint, StringComparison.Ordinal);
        Assert.Contains("matrix-cross-platform-verify-pilot.ps1", checkpoint, StringComparison.Ordinal);
        Assert.Contains("github-publish-readiness.ps1", checkpoint, StringComparison.Ordinal);
        Assert.Contains("git diff --check", checkpoint, StringComparison.Ordinal);

        Assert.Contains("matrix-artifact-manifest.json", checkpoint, StringComparison.Ordinal);
        Assert.Contains("matrix-proof-summary.json", checkpoint, StringComparison.Ordinal);
        Assert.Contains("certification=false", checkpoint, StringComparison.Ordinal);
        Assert.Contains("Linux-native public artifact recheck path", checkpoint, StringComparison.Ordinal);
        Assert.Contains("without rerunning the proof chain, invoking `pwsh`, or entering repo-local release lanes", checkpoint, StringComparison.Ordinal);
        Assert.Contains("No unresolved P0/P1 verification debt is hidden", checkpoint, StringComparison.Ordinal);
        Assert.Contains("operator action", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NuGet.org publication", checkpoint, StringComparison.Ordinal);
        Assert.Contains("local challenge results, not certification", checkpoint, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("model safety benchmarking", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hosted verification", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("public certification", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("public leaderboard", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("semantic source-code correctness", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("operating-system sandboxing", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("source upload", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("raw diff upload", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("prompt upload", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secret upload", checkpoint, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("verified safe", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("certified by CARVES", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hosted verification is available", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("public leaderboard is available", checkpoint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocsIndex_SplitsPublicProductEntrypointsFromInternalCheckpoints()
    {
        var repoRoot = LocateSourceRepoRoot();
        var index = File.ReadAllText(Path.Combine(repoRoot, "docs", "INDEX.md"));

        Assert.Contains("Public Matrix Entry", index, StringComparison.Ordinal);
        Assert.Contains("public product entrypoints", index, StringComparison.Ordinal);
        Assert.Contains("matrix/quickstart.en.md", index, StringComparison.Ordinal);
        Assert.Contains("matrix/quickstart.zh-CN.md", index, StringComparison.Ordinal);
        Assert.Contains("release/matrix-release-notes.md", index, StringComparison.Ordinal);
        Assert.Contains("matrix/known-limitations.md", index, StringComparison.Ordinal);
        Assert.Contains("Runtime CARD/TaskGraph concepts", index, StringComparison.Ordinal);

        Assert.Contains("Internal Checkpoints And Operator Review", index, StringComparison.Ordinal);
        Assert.Contains("CARD traceability", index, StringComparison.Ordinal);
        Assert.Contains("release/matrix-github-release-candidate-checkpoint.md", index, StringComparison.Ordinal);
        Assert.Contains("release/trust-chain-hardening-release-checkpoint.md", index, StringComparison.Ordinal);
        Assert.Contains("release/github-publish-readiness-checkpoint.md", index, StringComparison.Ordinal);
        Assert.Contains("release/product-extraction-readiness-checkpoint.md", index, StringComparison.Ordinal);
        Assert.Contains("release/matrix-verifiable-local-self-check-checkpoint.md", index, StringComparison.Ordinal);
        Assert.Contains("release/matrix-operator-release-gate.md", index, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicMatrixDocs_DoNotRequireInternalCardOrTaskGraphConcepts()
    {
        var repoRoot = LocateSourceRepoRoot();
        var publicDocs = new[]
        {
            "README.md",
            "docs/matrix/README.md",
            "docs/matrix/quickstart.en.md",
            "docs/matrix/quickstart.zh-CN.md",
            "docs/matrix/public-boundary.md",
            "docs/matrix/known-limitations.md",
            "docs/release/matrix-release-notes.md",
            "docs/release/github-release-draft.md",
        };

        foreach (var relativePath in publicDocs)
        {
            var doc = File.ReadAllText(Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

            Assert.DoesNotContain("CARD-", doc, StringComparison.Ordinal);
            Assert.DoesNotContain("TaskGraph", doc, StringComparison.Ordinal);
            Assert.DoesNotContain("Planner / Worker", doc, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(".ai/tasks", doc, StringComparison.Ordinal);
            Assert.DoesNotContain("task run T-CARD", doc, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MatrixBeginnerQuickstarts_CoverProofVerifyBadgeLimitsAndProductEntrypoints()
    {
        var repoRoot = LocateSourceRepoRoot();
        var english = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "quickstart.en.md"));
        var chinese = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "quickstart.zh-CN.md"));
        var limitations = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "known-limitations.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));

        foreach (var doc in new[] { english, chinese })
        {
            Assert.Contains("dotnet build ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release", doc, StringComparison.Ordinal);
            Assert.Contains("proof --lane native-minimal --artifact-root artifacts/matrix/native-quickstart --configuration Release --json", doc, StringComparison.Ordinal);
            Assert.Contains("verify artifacts/matrix/native-quickstart --json", doc, StringComparison.Ordinal);
            Assert.Contains("matrix-native-proof.v0", File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "README.md")), StringComparison.Ordinal);
            Assert.Contains("matrix-proof-lane.ps1", doc, StringComparison.Ordinal);
            Assert.Contains("matrix-packaged-install-smoke.ps1", doc, StringComparison.Ordinal);
            AssertBefore(doc, "proof --lane native-minimal --artifact-root artifacts/matrix/native-quickstart --configuration Release --json", "matrix-proof-lane.ps1");
            AssertBefore(doc, "verify artifacts/matrix/native-quickstart --json", "matrix-proof-lane.ps1");
            Assert.Contains("Linux-native", doc, StringComparison.Ordinal);
            Assert.Contains("`pwsh`", doc, StringComparison.Ordinal);
            Assert.Contains("release proof", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("packaged smoke", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("shield-badge.svg", doc, StringComparison.Ordinal);
            Assert.Contains("G4.H3.A5", doc, StringComparison.Ordinal);
            Assert.Contains("certification=false", doc, StringComparison.Ordinal);
            Assert.Contains("docs/guard/README.md", doc, StringComparison.Ordinal);
            Assert.Contains("docs/handoff/README.md", doc, StringComparison.Ordinal);
            Assert.Contains("docs/audit/README.md", doc, StringComparison.Ordinal);
            Assert.Contains("docs/shield/README.md", doc, StringComparison.Ordinal);
            Assert.Contains("docs/matrix/README.md", doc, StringComparison.Ordinal);
            Assert.Contains("model safety", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("hosted verification", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("public certification", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("operating-system sandbox", doc, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CARD-", doc, StringComparison.Ordinal);
            Assert.DoesNotContain("TaskGraph", doc, StringComparison.Ordinal);
        }

        foreach (var doc in new[] { readme, limitations })
        {
            Assert.Contains("carves-matrix proof", doc, StringComparison.Ordinal);
            Assert.Contains("carves-matrix verify", doc, StringComparison.Ordinal);
            Assert.Contains("PowerShell", doc, StringComparison.Ordinal);
            Assert.Contains("full release", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("summary-only", doc, StringComparison.OrdinalIgnoreCase);
        }

        AssertBefore(readme, "proof --lane native-minimal --artifact-root artifacts/matrix/native", "pwsh ./scripts/matrix/matrix-proof-lane.ps1");
        Assert.Contains("not a requirement for the Linux-native Matrix first run", limitations, StringComparison.Ordinal);
        Assert.Contains("Beginner quickstart", File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "README.md")), StringComparison.Ordinal);
        Assert.Contains("Matrix beginner quickstart", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicMatrixAndShieldDocs_FreezeLocalWorkflowGovernanceSelfCheckBoundary()
    {
        var repoRoot = LocateSourceRepoRoot();
        var docs = new[]
        {
            "README.md",
            "docs/matrix/README.md",
            "docs/matrix/public-boundary.md",
            "docs/matrix/known-limitations.md",
            "docs/shield/README.md",
            "docs/shield/matrix-boundary-v0.md",
            "docs/shield/lite-scoring-model-v0.md",
            "docs/release/matrix-release-notes.md",
            "docs/release/matrix-github-release-candidate-checkpoint.md",
            "docs/release/github-release-draft.md",
            "docs/release/trust-chain-hardening-release-checkpoint.md",
        };

        var corpus = string.Join(
            Environment.NewLine,
            docs.Select(relativePath => File.ReadAllText(Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)))));

        Assert.Contains("AI coding workflow governance self-check", corpus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a model safety benchmark", corpus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hosted verification", corpus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("public leaderboard", corpus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("certification", corpus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("operating-system sandbox", corpus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("automatic rollback", corpus, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("semantic correctness", corpus, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("local safety matrix", corpus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AI patch safety matrix", corpus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("public rating claims", corpus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rating posture", corpus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CARVES rates model safety", corpus, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AI safety benchmark", corpus, StringComparison.OrdinalIgnoreCase);
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

    private static void AssertBefore(string text, string earlier, string later)
    {
        var earlierIndex = text.IndexOf(earlier, StringComparison.Ordinal);
        var laterIndex = text.IndexOf(later, StringComparison.Ordinal);
        Assert.True(earlierIndex >= 0, $"Missing expected earlier text: {earlier}");
        Assert.True(laterIndex >= 0, $"Missing expected later text: {later}");
        Assert.True(earlierIndex < laterIndex, $"Expected '{earlier}' to appear before '{later}'.");
    }
}

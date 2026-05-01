using System.Xml.Linq;
using Carves.Runtime.Application.Guard;
using Carves.Runtime.Application.Platform.SurfaceModels;

namespace Carves.Runtime.IntegrationTests;

public sealed class CliDistributionClosureTests
{
    [Fact]
    public void CliDistributionArtifacts_DefineSourceTreeWrappersAndToolPackageMetadata()
    {
        var repoRoot = LocateSourceRepoRoot();

        Assert.True(File.Exists(Path.Combine(repoRoot, "carves")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "carves.ps1")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "carves.cmd")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_CLI_DISTRIBUTION.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_READINESS_BOUNDARY.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_INIT_FIRST_RUN.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_EXTERNAL_TARGET_DOGFOOD.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_REAL_PROJECT_PILOT.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_REAL_PROJECT_WRITEBACK.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_REAL_PROJECT_WORKSPACE.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_REAL_PROJECT_WORKSPACE_WRITEBACK.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_PRODUCTIZED_PILOT_GUIDE.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_PRODUCTIZED_PILOT_STATUS.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_RUNTIME_LOCAL_DIST.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_TARGET_AGENT_BOOTSTRAP_PACK.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_EXTERNAL_CONSUMER_RESOURCE_PACK.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_EXTERNAL_AGENT_QUICKSTART.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_AGENT_PROBLEM_INTAKE.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_AGENT_PROBLEM_TRIAGE_LEDGER.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_AGENT_PROBLEM_FOLLOW_UP_CANDIDATES.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_PLAN.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_RECORD.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_INTAKE.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_GATE.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_CLI_INVOCATION_CONTRACT.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_CLI_ACTIVATION_PLAN.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_LOCAL_DIST_FRESHNESS_SMOKE.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_TARGET_DIST_BINDING_PLAN.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "guides", "CARVES_FROZEN_DIST_TARGET_READBACK_PROOF.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "scripts", "pack-runtime-dist.ps1")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "scripts", "alpha", "guard-packaged-install-smoke.ps1")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "scripts", "beta", "guard-packaged-install-smoke.ps1")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "scripts", "beta", "guard-external-pilot-matrix.ps1")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "scripts", "beta", "guard-beta-proof-lane.ps1")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "beta", "guard-install-and-distribution.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "beta", "guard-external-pilot-matrix.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "beta", "guard-beta-ci-proof-lane.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-1-cli-distribution.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-2-readiness-separation.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-3-minimal-init-onboarding.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-4-external-target-dogfood-proof.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-5-real-project-pilot.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-6-official-truth-writeback.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-7-managed-workspace-execution.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-8-managed-workspace-writeback.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-9-productized-pilot-guide.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-10-productized-pilot-status.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-11b-target-agent-bootstrap-pack.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-12-existing-target-bootstrap-repair.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-13-target-commit-hygiene.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-14-target-commit-plan.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-15-target-commit-closure.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-16-local-dist-handoff.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-17-product-pilot-proof.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-18-external-consumer-resource-pack.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-19-cli-invocation-contract.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-20-cli-activation-plan.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-21-target-dist-binding-plan.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-22-local-dist-freshness-smoke.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-23-frozen-dist-target-readback-proof.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-24-wrapper-runtime-root-binding.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-25-external-target-product-proof-closure.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-26-real-external-repo-pilot.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-27-external-target-residue-policy.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-28-target-ignore-decision-plan.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-29-target-ignore-decision-record.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-30-target-ignore-decision-record-audit.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-31-target-ignore-decision-record-commit-readback.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-32-alpha-external-use-readiness-rollup.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-33-external-target-pilot-start-bundle.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-34-agent-problem-intake.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-35-agent-problem-triage-ledger.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-36-agent-problem-follow-up-candidates.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-37-agent-problem-follow-up-decision-plan.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-38-agent-problem-follow-up-decision-record.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-39-agent-problem-follow-up-planning-intake.md")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "docs", "runtime", "carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md")));

        var project = XDocument.Load(Path.Combine(repoRoot, "src", "CARVES.Runtime.Cli", "carves.csproj"));
        Assert.Equal("true", ReadProperty(project, "PackAsTool"));
        Assert.Equal("carves", ReadProperty(project, "ToolCommandName"));
        Assert.Equal("CARVES.Runtime.Cli", ReadProperty(project, "PackageId"));
        Assert.Equal(RuntimeAlphaVersion.Current, ReadProperty(project, "Version"));
        Assert.Equal("CARVES_CLI_DISTRIBUTION.md", ReadProperty(project, "PackageReadmeFile"));

        var guide = File.ReadAllText(Path.Combine(repoRoot, "docs", "guides", "CARVES_CLI_DISTRIBUTION.md"));
        Assert.Contains(@".\carves.ps1 init .", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 agent start", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 agent handoff", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 agent bootstrap", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot readiness", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot invocation", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot start", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot next", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot problem-intake", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot triage", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot follow-up", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot follow-up-plan", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot follow-up-record", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot follow-up-intake", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot follow-up-gate", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot report-problem", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot list-problems", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot inspect-problem", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot activation", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot dist-smoke", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot dist-binding", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot target-proof", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot guide", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot status", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot resources", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot commit-hygiene", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot commit-plan", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot closure", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot residue", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot ignore-plan", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot ignore-record", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot dist", guide, StringComparison.Ordinal);
        Assert.Contains(@".\carves.ps1 pilot proof", guide, StringComparison.Ordinal);
        Assert.Contains("dotnet tool install --global CARVES.Runtime.Cli", guide, StringComparison.Ordinal);
        Assert.Contains("carves agent start --json", guide, StringComparison.Ordinal);
        Assert.Contains("carves agent handoff --json", guide, StringComparison.Ordinal);
        Assert.Contains("carves agent bootstrap", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot readiness", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot invocation", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot start", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot next", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot problem-intake", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot triage", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot follow-up", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot follow-up-plan", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot follow-up-record", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot follow-up-intake", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot follow-up-gate", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot record-follow-up-decision", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot report-problem", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot list-problems", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot inspect-problem", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot activation", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot dist-smoke", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot dist-binding", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot target-proof", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot guide", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot status", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot resources", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot commit-hygiene", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot commit-plan", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot closure", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot residue", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot ignore-plan", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot ignore-record", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot dist", guide, StringComparison.Ordinal);
        Assert.Contains("carves pilot proof", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_EXTERNAL_TARGET_DOGFOOD.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_REAL_PROJECT_WRITEBACK.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_REAL_PROJECT_WORKSPACE.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_REAL_PROJECT_WORKSPACE_WRITEBACK.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_PRODUCTIZED_PILOT_GUIDE.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_PRODUCTIZED_PILOT_STATUS.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_RUNTIME_LOCAL_DIST.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_TARGET_AGENT_BOOTSTRAP_PACK.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_EXTERNAL_CONSUMER_RESOURCE_PACK.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_EXTERNAL_AGENT_QUICKSTART.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_AGENT_PROBLEM_INTAKE.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_AGENT_PROBLEM_TRIAGE_LEDGER.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_AGENT_PROBLEM_FOLLOW_UP_CANDIDATES.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_PLAN.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_RECORD.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_INTAKE.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_GATE.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_CLI_INVOCATION_CONTRACT.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_CLI_ACTIVATION_PLAN.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_LOCAL_DIST_FRESHNESS_SMOKE.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_TARGET_DIST_BINDING_PLAN.md", guide, StringComparison.Ordinal);
        Assert.Contains("CARVES_FROZEN_DIST_TARGET_READBACK_PROOF.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-11b-target-agent-bootstrap-pack.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-12-existing-target-bootstrap-repair.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-13-target-commit-hygiene.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-14-target-commit-plan.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-15-target-commit-closure.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-16-local-dist-handoff.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-17-product-pilot-proof.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-18-external-consumer-resource-pack.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-19-cli-invocation-contract.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-20-cli-activation-plan.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-21-target-dist-binding-plan.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-22-local-dist-freshness-smoke.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-23-frozen-dist-target-readback-proof.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-27-external-target-residue-policy.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-28-target-ignore-decision-plan.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-29-target-ignore-decision-record.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-30-target-ignore-decision-record-audit.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-31-target-ignore-decision-record-commit-readback.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-32-alpha-external-use-readiness-rollup.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-33-external-target-pilot-start-bundle.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-34-agent-problem-intake.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-35-agent-problem-triage-ledger.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-36-agent-problem-follow-up-candidates.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-37-agent-problem-follow-up-decision-plan.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-38-agent-problem-follow-up-decision-record.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-39-agent-problem-follow-up-planning-intake.md", guide, StringComparison.Ordinal);
        Assert.Contains("carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", guide, StringComparison.Ordinal);
        Assert.Contains($@".\scripts\pack-runtime-dist.ps1 -Version {RuntimeAlphaVersion.Current}", guide, StringComparison.Ordinal);

        var localDistGuide = File.ReadAllText(Path.Combine(repoRoot, "docs", "guides", "CARVES_RUNTIME_LOCAL_DIST.md"));
        Assert.Contains("Do not package development card history by default.", localDistGuide, StringComparison.Ordinal);
        Assert.Contains(".ai/memory/execution/` development-time execution memory", localDistGuide, StringComparison.Ordinal);
        Assert.Contains("Windows extraction budget", localDistGuide, StringComparison.Ordinal);

        var runtimeDistScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "pack-runtime-dist.ps1"));
        Assert.Contains("-ExcludedNames ($excludedNames + @(\"execution\"))", runtimeDistScript, StringComparison.Ordinal);
        Assert.Contains("\".ai/memory/execution\"", runtimeDistScript, StringComparison.Ordinal);
        Assert.Contains("Assert-DistRelativePathsAreWindowsExtractable -DistRoot $outputPath -MaxRelativePathLength 180", runtimeDistScript, StringComparison.Ordinal);

        var wrapper = File.ReadAllText(Path.Combine(repoRoot, "carves.ps1"));
        Assert.Contains("Windows Application Control", wrapper, StringComparison.Ordinal);
        Assert.Contains("falling back to source project execution", wrapper, StringComparison.Ordinal);
    }

    [Fact]
    public void AlphaGuardPackagedInstallSmoke_DefinesLocalToolPathProofAndDocs()
    {
        var repoRoot = LocateSourceRepoRoot();
        var script = File.ReadAllText(Path.Combine(repoRoot, "scripts", "alpha", "guard-packaged-install-smoke.ps1"));
        var betaScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "beta", "guard-packaged-install-smoke.ps1"));

        Assert.Contains("\"pack\"", script, StringComparison.Ordinal);
        Assert.Contains("\"tool\"", script, StringComparison.Ordinal);
        Assert.Contains("\"install\"", script, StringComparison.Ordinal);
        Assert.Contains("\"update\"", script, StringComparison.Ordinal);
        Assert.Contains("\"--tool-path\"", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-Carves -CarvesCommand $carvesCommand -TargetRoot $targetRoot -Arguments @(\"guard\", \"check\", \"--json\")", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-Carves -CarvesCommand $carvesCommand -TargetRoot $targetRoot -Arguments @(\"guard\", \"audit\", \"--json\")", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-Carves -CarvesCommand $carvesCommand -TargetRoot $targetRoot -Arguments @(\"guard\", \"report\", \"--json\")", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-Carves -CarvesCommand $carvesCommand -TargetRoot $targetRoot -Arguments @(\"guard\", \"explain\", $block.json.run_id, \"--json\")", script, StringComparison.Ordinal);
        Assert.Contains("requires_carves_task_truth = $false", script, StringComparison.Ordinal);
        Assert.Contains("requires_carves_card_truth = $false", script, StringComparison.Ordinal);
        Assert.Contains("requires_carves_taskgraph_truth = $false", script, StringComparison.Ordinal);
        Assert.Contains("remote_registry_published = $false", script, StringComparison.Ordinal);
        Assert.Contains("global_tool_install_used = $false", script, StringComparison.Ordinal);
        Assert.Contains("beta_guard_packaged_install_cross_platform", betaScript, StringComparison.Ordinal);
        Assert.Contains("OSPlatform]::Windows", betaScript, StringComparison.Ordinal);
        Assert.Contains("OSPlatform]::Linux", betaScript, StringComparison.Ordinal);
        Assert.Contains("OSPlatform]::OSX", betaScript, StringComparison.Ordinal);
        Assert.Contains("unsupported_platform_behavior", betaScript, StringComparison.Ordinal);

        var alphaReadme = File.ReadAllText(Path.Combine(repoRoot, "docs", "alpha", "README.md"));
        Assert.Contains("guard-packaged-install-smoke.ps1", alphaReadme, StringComparison.Ordinal);
        Assert.Contains("local nupkg", alphaReadme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("remote registry", alphaReadme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scripts/beta/guard-packaged-install-smoke.ps1", alphaReadme, StringComparison.Ordinal);

        var releaseCheckpoint = File.ReadAllText(Path.Combine(repoRoot, "docs", "alpha", "release-checkpoint.md"));
        Assert.Contains("guard-packaged-install-smoke.ps1", releaseCheckpoint, StringComparison.Ordinal);
        Assert.Contains("local package proof", releaseCheckpoint, StringComparison.OrdinalIgnoreCase);

        var releaseNote = File.ReadAllText(Path.Combine(repoRoot, "docs", "release", "alpha-guard-0.1.0-alpha.2.md"));
        Assert.Contains("guard-packaged-install-smoke.ps1", releaseNote, StringComparison.Ordinal);
        Assert.Contains("remote registry publication", releaseNote, StringComparison.OrdinalIgnoreCase);

        var betaDistribution = File.ReadAllText(Path.Combine(repoRoot, "docs", "beta", "guard-install-and-distribution.md"));
        Assert.Contains("Remote registry publication is deferred", betaDistribution, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Windows, Linux, and macOS", betaDistribution, StringComparison.Ordinal);
        Assert.Contains("fail explicitly", betaDistribution, StringComparison.OrdinalIgnoreCase);

        var betaReleaseNote = File.ReadAllText(Path.Combine(repoRoot, GuardReleaseInfo.ReleaseNotePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("scripts/beta/guard-packaged-install-smoke.ps1", betaReleaseNote, StringComparison.Ordinal);
        Assert.Contains("not remote registry publication", betaReleaseNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BetaGuardExternalPilotMatrix_DefinesThreeRepoShapeProofAndDocs()
    {
        var repoRoot = LocateSourceRepoRoot();
        var script = File.ReadAllText(Path.Combine(repoRoot, "scripts", "beta", "guard-external-pilot-matrix.ps1"));
        var doc = File.ReadAllText(Path.Combine(repoRoot, "docs", "beta", "guard-external-pilot-matrix.md"));
        var alphaReadme = File.ReadAllText(Path.Combine(repoRoot, "docs", "alpha", "README.md"));
        var releaseCheckpoint = File.ReadAllText(Path.Combine(repoRoot, "docs", "alpha", "release-checkpoint.md"));
        var releaseNote = File.ReadAllText(Path.Combine(repoRoot, GuardReleaseInfo.ReleaseNotePath.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains("beta_guard_external_pilot_matrix", script, StringComparison.Ordinal);
        Assert.Contains("node_single_package", script, StringComparison.Ordinal);
        Assert.Contains("dotnet_service", script, StringComparison.Ordinal);
        Assert.Contains("monorepo_packages", script, StringComparison.Ordinal);
        Assert.Contains("guard\", \"audit\", \"--json", script, StringComparison.Ordinal);
        Assert.Contains("guard\", \"report\", \"--json", script, StringComparison.Ordinal);
        Assert.Contains("guard\", \"explain\"", script, StringComparison.Ordinal);
        Assert.Contains("pilot_discovered_block_level_issue_count", script, StringComparison.Ordinal);

        Assert.Contains("repository_count: 3", doc, StringComparison.Ordinal);
        Assert.Contains("scenario_count: 6", doc, StringComparison.Ordinal);
        Assert.Contains("allow_count: 3", doc, StringComparison.Ordinal);
        Assert.Contains("block_count: 3", doc, StringComparison.Ordinal);
        Assert.Contains("readback_sets: 3", doc, StringComparison.Ordinal);
        Assert.Contains("pilot_discovered_block_level_issue_count: 0", doc, StringComparison.Ordinal);
        Assert.Contains("CARD-753 is a no-op resolution gate", doc, StringComparison.Ordinal);

        Assert.Contains("scripts/beta/guard-external-pilot-matrix.ps1", alphaReadme, StringComparison.Ordinal);
        Assert.Contains("docs/beta/guard-external-pilot-matrix.md", releaseCheckpoint, StringComparison.Ordinal);
        Assert.Contains("scripts/beta/guard-external-pilot-matrix.ps1", releaseNote, StringComparison.Ordinal);
        Assert.Contains("0 pilot-discovered block-level product issues", releaseNote, StringComparison.Ordinal);
    }

    [Fact]
    public void BetaGuardProofLane_DefinesCiSafeSingleCommandAndWorkflow()
    {
        var repoRoot = LocateSourceRepoRoot();
        var script = File.ReadAllText(Path.Combine(repoRoot, "scripts", "beta", "guard-beta-proof-lane.ps1"));
        var workflow = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "ci.yml"));
        var doc = File.ReadAllText(Path.Combine(repoRoot, "docs", "beta", "guard-beta-ci-proof-lane.md"));
        var alphaReadme = File.ReadAllText(Path.Combine(repoRoot, "docs", "alpha", "README.md"));
        var releaseCheckpoint = File.ReadAllText(Path.Combine(repoRoot, "docs", "alpha", "release-checkpoint.md"));
        var releaseNote = File.ReadAllText(Path.Combine(repoRoot, GuardReleaseInfo.ReleaseNotePath.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains("beta-guard-proof-lane.v1", script, StringComparison.Ordinal);
        Assert.Contains("GuardPolicyEvaluatorTests|GuardDiffAdapterTests|GuardDecisionReadServiceTests|GuardRunDecisionServiceTests|AlphaGuardTrustBasisCoverageAuditTests|AlphaGuardReleaseCheckpointTests", script, StringComparison.Ordinal);
        Assert.Contains("GuardCheckCliTests|CliDistributionClosureTests", script, StringComparison.Ordinal);
        Assert.Contains("guard-packaged-install-smoke.ps1", script, StringComparison.Ordinal);
        Assert.Contains("guard-external-pilot-matrix.ps1", script, StringComparison.Ordinal);
        Assert.Contains("provider_secrets_required = $false", script, StringComparison.Ordinal);
        Assert.Contains("remote_package_publication_required = $false", script, StringComparison.Ordinal);
        Assert.Contains("live_worker_tests_included = $false", script, StringComparison.Ordinal);

        Assert.Contains("Guard Beta proof lane", workflow, StringComparison.Ordinal);
        Assert.Contains("./scripts/beta/guard-beta-proof-lane.ps1 -Configuration Release -SkipBuild", workflow, StringComparison.Ordinal);

        Assert.Contains("provider secrets required: `false`", doc, StringComparison.Ordinal);
        Assert.Contains("remote package publication required: `false`", doc, StringComparison.Ordinal);
        Assert.Contains("live worker tests included: `false`", doc, StringComparison.Ordinal);
        Assert.Contains("packaged smoke: beta_guard_packaged_install_cross_platform", doc, StringComparison.Ordinal);
        Assert.Contains("pilot matrix: beta_guard_external_pilot_matrix", doc, StringComparison.Ordinal);

        Assert.Contains("scripts/beta/guard-beta-proof-lane.ps1", alphaReadme, StringComparison.Ordinal);
        Assert.Contains("docs/beta/guard-beta-ci-proof-lane.md", releaseCheckpoint, StringComparison.Ordinal);
        Assert.Contains("scripts/beta/guard-beta-proof-lane.ps1", releaseNote, StringComparison.Ordinal);
        Assert.Contains("requires no provider secrets, no remote package publication, and no live worker tests", releaseNote, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadProperty(XDocument project, string propertyName)
    {
        return project
            .Descendants(propertyName)
            .Select(element => element.Value.Trim())
            .FirstOrDefault();
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

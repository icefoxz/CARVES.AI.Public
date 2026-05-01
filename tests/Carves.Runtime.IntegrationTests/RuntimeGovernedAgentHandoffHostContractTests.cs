using System.Text.Json;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeGovernedAgentHandoffHostContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    [Fact]
    public void PilotStatusApi_ProjectsHostHonestyWhenResidentHostIsNotRunning()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "stop", "--force", "pilot status host honesty test");

        var pilotStatusApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-product-closure-pilot-status");

        Assert.Equal(0, pilotStatusApi.ExitCode);
        using var document = JsonDocument.Parse(pilotStatusApi.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-product-closure-pilot-status", root.GetProperty("surface_id").GetString());
        Assert.Equal("not_running", root.GetProperty("host_readiness").GetString());
        Assert.Equal("stopped_snapshot", root.GetProperty("host_operational_state").GetString());
        Assert.False(root.GetProperty("host_conflict_present").GetBoolean());
        Assert.True(root.GetProperty("host_safe_to_start_new_host").GetBoolean());
        Assert.Equal("ensure_host", root.GetProperty("host_recommended_action_kind").GetString());
        Assert.Equal("recoverable", root.GetProperty("host_lifecycle").GetProperty("state").GetString());
        Assert.Equal("ensure_host", root.GetProperty("host_lifecycle").GetProperty("action_kind").GetString());
        Assert.Equal("carves host ensure --json", root.GetProperty("host_recommended_action").GetString());
        Assert.False(root.GetProperty("safe_to_start_new_execution").GetBoolean());
    }

    [Fact]
    public void InspectAndApi_ProjectAdapterPolicyAuditAndProofSurfaces()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var adapter = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-adapter-handoff-contract");
        var protectedPolicy = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-protected-truth-root-policy");
        var proof = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-governed-agent-handoff-proof");
        var proofApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-governed-agent-handoff-proof");
        var pilotGuide = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-product-closure-pilot-guide");
        var pilotGuideApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-product-closure-pilot-guide");
        var pilotStatus = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-product-closure-pilot-status");
        var pilotStatusApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-product-closure-pilot-status");
        var targetBootstrap = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-target-agent-bootstrap-pack");
        var targetBootstrapApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-target-agent-bootstrap-pack");
        var targetCommitPlan = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-target-commit-plan");
        var targetCommitPlanApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-target-commit-plan");
        var targetCommitClosure = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-target-commit-closure");
        var targetCommitClosureApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-target-commit-closure");
        var targetResiduePolicy = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-target-residue-policy");
        var targetResiduePolicyApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-target-residue-policy");
        var pilotResiduePolicy = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "residue");
        var pilotResiduePolicyApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "residue", "--json");
        var targetIgnoreDecisionPlan = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-target-ignore-decision-plan");
        var targetIgnoreDecisionPlanApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-target-ignore-decision-plan");
        var pilotIgnoreDecisionPlan = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "ignore-plan");
        var pilotIgnoreDecisionPlanApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "ignore-plan", "--json");
        var targetIgnoreDecisionRecord = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-target-ignore-decision-record");
        var targetIgnoreDecisionRecordApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-target-ignore-decision-record");
        var pilotIgnoreDecisionRecord = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "ignore-record");
        var pilotIgnoreDecisionRecordApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "ignore-record", "--json");
        var localDist = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-local-dist-handoff");
        var localDistApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-local-dist-handoff");
        var productPilotProof = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-product-pilot-proof");
        var productPilotProofApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-product-pilot-proof");
        var externalConsumerResources = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-external-consumer-resource-pack");
        var externalConsumerResourcesApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-external-consumer-resource-pack");
        var alphaExternalUseReadiness = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-alpha-external-use-readiness");
        var alphaExternalUseReadinessApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-alpha-external-use-readiness");
        var agentThreadStart = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-thread-start");
        var agentThreadStartApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-thread-start");
        var agentStart = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "agent", "start");
        var agentStartApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "agent", "start", "--json");
        var agentBootApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "agent", "boot", "--json");
        var pilotBootApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "boot", "--json");
        var pilotAgentStartApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "agent-start", "--json");
        var externalPilotStart = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-external-target-pilot-start");
        var externalPilotStartApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-external-target-pilot-start");
        var pilotStart = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "start");
        var pilotStartApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "start", "--json");
        var externalPilotNext = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-external-target-pilot-next");
        var externalPilotNextApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-external-target-pilot-next");
        var pilotNext = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "next");
        var pilotNextApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "next", "--json");
        var agentProblemIntake = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-problem-intake");
        var agentProblemIntakeApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-problem-intake");
        var pilotProblemIntake = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "problem-intake");
        var pilotProblemIntakeApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "problem-intake", "--json");
        var agentProblemTriageLedger = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-problem-triage-ledger");
        var agentProblemTriageLedgerApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-problem-triage-ledger");
        var pilotTriage = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "triage");
        var pilotTriageApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "triage", "--json");
        var pilotProblemTriageApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "problem-triage", "--json");
        var pilotFrictionLedgerApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "friction-ledger", "--json");
        var agentProblemFollowUpCandidates = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-problem-follow-up-candidates");
        var agentProblemFollowUpCandidatesApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-problem-follow-up-candidates");
        var pilotFollowUp = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "follow-up");
        var pilotFollowUpApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "follow-up", "--json");
        var pilotProblemFollowUpApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "problem-follow-up", "--json");
        var pilotTriageFollowUpApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "triage-follow-up", "--json");
        var agentProblemFollowUpDecisionPlan = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-problem-follow-up-decision-plan");
        var agentProblemFollowUpDecisionPlanApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-problem-follow-up-decision-plan");
        var pilotFollowUpPlan = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "follow-up-plan");
        var pilotFollowUpPlanApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "follow-up-plan", "--json");
        var pilotProblemFollowUpPlanApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "problem-follow-up-plan", "--json");
        var pilotTriageFollowUpPlanApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "triage-follow-up-plan", "--json");
        var agentProblemFollowUpDecisionRecord = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-problem-follow-up-decision-record");
        var agentProblemFollowUpDecisionRecordApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-problem-follow-up-decision-record");
        var pilotFollowUpRecord = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "follow-up-record");
        var pilotFollowUpRecordApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "follow-up-record", "--json");
        var pilotFollowUpDecisionRecordAliasApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "follow-up-decision-record", "--json");
        var pilotProblemFollowUpRecordAliasApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "problem-follow-up-record", "--json");
        var agentProblemFollowUpPlanningIntake = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-problem-follow-up-planning-intake");
        var agentProblemFollowUpPlanningIntakeApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-problem-follow-up-planning-intake");
        var pilotFollowUpIntake = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "follow-up-intake");
        var pilotFollowUpIntakeApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "follow-up-intake", "--json");
        var pilotFollowUpPlanningAliasApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "follow-up-planning", "--json");
        var pilotProblemFollowUpIntakeAliasApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "problem-follow-up-intake", "--json");
        var agentProblemFollowUpPlanningGate = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-problem-follow-up-planning-gate");
        var agentProblemFollowUpPlanningGateApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-problem-follow-up-planning-gate");
        var pilotFollowUpGate = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "follow-up-gate");
        var pilotFollowUpGateApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "follow-up-gate", "--json");
        var pilotFollowUpPlanningGateAliasApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "follow-up-planning-gate", "--json");
        var pilotProblemFollowUpGateAliasApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "problem-follow-up-gate", "--json");
        var pilotReadiness = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "readiness");
        var pilotReadinessApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "readiness", "--json");
        var pilotAlpha = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "alpha");
        var pilotAlphaApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "alpha", "--json");
        var cliInvocation = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-cli-invocation-contract");
        var cliInvocationApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-cli-invocation-contract");
        var pilotInvocation = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "invocation");
        var pilotInvocationApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "invocation", "--json");
        var cliActivation = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-cli-activation-plan");
        var cliActivationApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-cli-activation-plan");
        var pilotActivation = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "activation");
        var pilotActivationApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "activation", "--json");
        var pilotAlias = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "alias");
        var pilotAliasApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "alias", "--json");
        var localDistFreshnessSmoke = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-local-dist-freshness-smoke");
        var localDistFreshnessSmokeApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-local-dist-freshness-smoke");
        var pilotDistSmoke = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "dist-smoke");
        var pilotDistSmokeApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "dist-smoke", "--json");
        var pilotDistFreshness = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "dist-freshness");
        var pilotDistFreshnessApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "dist-freshness", "--json");
        var targetDistBinding = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-target-dist-binding-plan");
        var targetDistBindingApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-target-dist-binding-plan");
        var pilotDistBinding = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "dist-binding");
        var pilotDistBindingApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "dist-binding", "--json");
        var pilotBindDist = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "bind-dist");
        var pilotBindDistApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "bind-dist", "--json");
        var frozenDistTargetReadbackProof = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-frozen-dist-target-readback-proof");
        var frozenDistTargetReadbackProofApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-frozen-dist-target-readback-proof");
        var pilotTargetProof = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "target-proof");
        var pilotTargetProofApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "target-proof", "--json");
        var pilotExternalProof = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "external-proof");
        var pilotExternalProofApi = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "pilot", "external-proof", "--json");

        Assert.Equal(0, adapter.ExitCode);
        Assert.Equal(0, protectedPolicy.ExitCode);
        Assert.Equal(0, proof.ExitCode);
        Assert.Equal(0, proofApi.ExitCode);
        Assert.Equal(0, pilotGuide.ExitCode);
        Assert.Equal(0, pilotGuideApi.ExitCode);
        Assert.Equal(0, pilotStatus.ExitCode);
        Assert.Equal(0, pilotStatusApi.ExitCode);
        Assert.Equal(0, targetBootstrap.ExitCode);
        Assert.Equal(0, targetBootstrapApi.ExitCode);
        Assert.Equal(0, targetCommitPlan.ExitCode);
        Assert.Equal(0, targetCommitPlanApi.ExitCode);
        Assert.Equal(0, targetCommitClosure.ExitCode);
        Assert.Equal(0, targetCommitClosureApi.ExitCode);
        Assert.Equal(0, targetResiduePolicy.ExitCode);
        Assert.Equal(0, targetResiduePolicyApi.ExitCode);
        Assert.Equal(0, pilotResiduePolicy.ExitCode);
        Assert.Equal(0, pilotResiduePolicyApi.ExitCode);
        Assert.Equal(0, targetIgnoreDecisionPlan.ExitCode);
        Assert.Equal(0, targetIgnoreDecisionPlanApi.ExitCode);
        Assert.Equal(0, pilotIgnoreDecisionPlan.ExitCode);
        Assert.Equal(0, pilotIgnoreDecisionPlanApi.ExitCode);
        Assert.Equal(0, targetIgnoreDecisionRecord.ExitCode);
        Assert.Equal(0, targetIgnoreDecisionRecordApi.ExitCode);
        Assert.Equal(0, pilotIgnoreDecisionRecord.ExitCode);
        Assert.Equal(0, pilotIgnoreDecisionRecordApi.ExitCode);
        Assert.Equal(0, localDist.ExitCode);
        Assert.Equal(0, localDistApi.ExitCode);
        Assert.Equal(0, productPilotProof.ExitCode);
        Assert.Equal(0, productPilotProofApi.ExitCode);
        Assert.Equal(0, externalConsumerResources.ExitCode);
        Assert.Equal(0, externalConsumerResourcesApi.ExitCode);
        Assert.Equal(0, alphaExternalUseReadiness.ExitCode);
        Assert.Equal(0, alphaExternalUseReadinessApi.ExitCode);
        Assert.Equal(0, agentThreadStart.ExitCode);
        Assert.Equal(0, agentThreadStartApi.ExitCode);
        Assert.Equal(0, agentStart.ExitCode);
        Assert.Equal(0, agentStartApi.ExitCode);
        Assert.Equal(0, agentBootApi.ExitCode);
        Assert.Equal(0, pilotBootApi.ExitCode);
        Assert.Equal(0, pilotAgentStartApi.ExitCode);
        Assert.Equal(0, externalPilotStart.ExitCode);
        Assert.Equal(0, externalPilotStartApi.ExitCode);
        Assert.Equal(0, pilotStart.ExitCode);
        Assert.Equal(0, pilotStartApi.ExitCode);
        Assert.Equal(0, externalPilotNext.ExitCode);
        Assert.Equal(0, externalPilotNextApi.ExitCode);
        Assert.Equal(0, pilotNext.ExitCode);
        Assert.Equal(0, pilotNextApi.ExitCode);
        Assert.Equal(0, agentProblemIntake.ExitCode);
        Assert.Equal(0, agentProblemIntakeApi.ExitCode);
        Assert.Equal(0, pilotProblemIntake.ExitCode);
        Assert.Equal(0, pilotProblemIntakeApi.ExitCode);
        Assert.Equal(0, agentProblemTriageLedger.ExitCode);
        Assert.Equal(0, agentProblemTriageLedgerApi.ExitCode);
        Assert.Equal(0, pilotTriage.ExitCode);
        Assert.Equal(0, pilotTriageApi.ExitCode);
        Assert.Equal(0, pilotProblemTriageApi.ExitCode);
        Assert.Equal(0, pilotFrictionLedgerApi.ExitCode);
        Assert.Equal(0, agentProblemFollowUpCandidates.ExitCode);
        Assert.Equal(0, agentProblemFollowUpCandidatesApi.ExitCode);
        Assert.Equal(0, pilotFollowUp.ExitCode);
        Assert.Equal(0, pilotFollowUpApi.ExitCode);
        Assert.Equal(0, pilotProblemFollowUpApi.ExitCode);
        Assert.Equal(0, pilotTriageFollowUpApi.ExitCode);
        Assert.Equal(0, agentProblemFollowUpDecisionPlan.ExitCode);
        Assert.Equal(0, agentProblemFollowUpDecisionPlanApi.ExitCode);
        Assert.Equal(0, pilotFollowUpPlan.ExitCode);
        Assert.Equal(0, pilotFollowUpPlanApi.ExitCode);
        Assert.Equal(0, pilotProblemFollowUpPlanApi.ExitCode);
        Assert.Equal(0, pilotTriageFollowUpPlanApi.ExitCode);
        Assert.Equal(0, agentProblemFollowUpDecisionRecord.ExitCode);
        Assert.Equal(0, agentProblemFollowUpDecisionRecordApi.ExitCode);
        Assert.Equal(0, pilotFollowUpRecord.ExitCode);
        Assert.Equal(0, pilotFollowUpRecordApi.ExitCode);
        Assert.Equal(0, pilotFollowUpDecisionRecordAliasApi.ExitCode);
        Assert.Equal(0, pilotProblemFollowUpRecordAliasApi.ExitCode);
        Assert.Equal(0, agentProblemFollowUpPlanningIntake.ExitCode);
        Assert.Equal(0, agentProblemFollowUpPlanningIntakeApi.ExitCode);
        Assert.Equal(0, pilotFollowUpIntake.ExitCode);
        Assert.Equal(0, pilotFollowUpIntakeApi.ExitCode);
        Assert.Equal(0, pilotFollowUpPlanningAliasApi.ExitCode);
        Assert.Equal(0, pilotProblemFollowUpIntakeAliasApi.ExitCode);
        Assert.Equal(0, agentProblemFollowUpPlanningGate.ExitCode);
        Assert.Equal(0, agentProblemFollowUpPlanningGateApi.ExitCode);
        Assert.Equal(0, pilotFollowUpGate.ExitCode);
        Assert.Equal(0, pilotFollowUpGateApi.ExitCode);
        Assert.Equal(0, pilotFollowUpPlanningGateAliasApi.ExitCode);
        Assert.Equal(0, pilotProblemFollowUpGateAliasApi.ExitCode);
        Assert.Equal(0, pilotReadiness.ExitCode);
        Assert.Equal(0, pilotReadinessApi.ExitCode);
        Assert.Equal(0, pilotAlpha.ExitCode);
        Assert.Equal(0, pilotAlphaApi.ExitCode);
        Assert.Equal(0, cliInvocation.ExitCode);
        Assert.Equal(0, cliInvocationApi.ExitCode);
        Assert.Equal(0, pilotInvocation.ExitCode);
        Assert.Equal(0, pilotInvocationApi.ExitCode);
        Assert.Equal(0, cliActivation.ExitCode);
        Assert.Equal(0, cliActivationApi.ExitCode);
        Assert.Equal(0, pilotActivation.ExitCode);
        Assert.Equal(0, pilotActivationApi.ExitCode);
        Assert.Equal(0, pilotAlias.ExitCode);
        Assert.Equal(0, pilotAliasApi.ExitCode);
        Assert.Equal(0, localDistFreshnessSmoke.ExitCode);
        Assert.Equal(0, localDistFreshnessSmokeApi.ExitCode);
        Assert.Equal(0, pilotDistSmoke.ExitCode);
        Assert.Equal(0, pilotDistSmokeApi.ExitCode);
        Assert.Equal(0, pilotDistFreshness.ExitCode);
        Assert.Equal(0, pilotDistFreshnessApi.ExitCode);
        Assert.Equal(0, targetDistBinding.ExitCode);
        Assert.Equal(0, targetDistBindingApi.ExitCode);
        Assert.Equal(0, pilotDistBinding.ExitCode);
        Assert.Equal(0, pilotDistBindingApi.ExitCode);
        Assert.Equal(0, pilotBindDist.ExitCode);
        Assert.Equal(0, pilotBindDistApi.ExitCode);
        Assert.Equal(0, frozenDistTargetReadbackProof.ExitCode);
        Assert.Equal(0, frozenDistTargetReadbackProofApi.ExitCode);
        Assert.Equal(0, pilotTargetProof.ExitCode);
        Assert.Equal(0, pilotTargetProofApi.ExitCode);
        Assert.Equal(0, pilotExternalProof.ExitCode);
        Assert.Equal(0, pilotExternalProofApi.ExitCode);
        Assert.Contains("Runtime adapter handoff contract", adapter.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- lane: cli_first", adapter.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- lane: acp_second", adapter.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- lane: mcp_optional", adapter.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Official truth ingress: planner_review_and_host_writeback_only", adapter.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime protected truth-root policy", protectedPolicy.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- protected-root: .ai/tasks/ | classification=task_truth", protectedPolicy.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("- denied-root: secret-like paths | classification=secret_material", protectedPolicy.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime governed agent handoff proof", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure baseline: docs/runtime/carves-product-closure-phase-0-baseline.md", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure current: docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime document root mode: repo_local", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("formal_planning_entry", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pre_writeback_blocker_projection", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot guide [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot status [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot start [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot next [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot problem-intake [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("inspect runtime-agent-problem-intake", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot triage [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("inspect runtime-agent-problem-triage-ledger", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot follow-up [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("inspect runtime-agent-problem-follow-up-candidates", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot follow-up-plan [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("inspect runtime-agent-problem-follow-up-decision-plan", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot follow-up-record [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("inspect runtime-agent-problem-follow-up-decision-record", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("inspect runtime-agent-problem-follow-up-planning-intake", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot follow-up-gate [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("inspect runtime-agent-problem-follow-up-planning-gate", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot readiness [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("inspect runtime-alpha-external-use-readiness", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot invocation [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot activation [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("inspect runtime-cli-activation-plan", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot dist-smoke [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("inspect runtime-local-dist-freshness-smoke", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot dist-binding [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("inspect runtime-target-dist-binding-plan", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot target-proof [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("inspect runtime-frozen-dist-target-readback-proof", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot resources [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot commit-hygiene [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot commit-plan [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot closure [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot residue [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot ignore-plan [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot ignore-record [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("inspect runtime-target-ignore-decision-record", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot dist [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pilot proof [--json]", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("constraint: hard_runtime_gate", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("constraint: vendor_optional", proof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime product closure pilot guide", pilotGuide.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", pilotGuide.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Status command entry: carves pilot status --json", pilotGuide.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("stage 3: target_agent_bootstrap", pilotGuide.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("stage 4: cli_invocation_contract", pilotGuide.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("stage 5: cli_activation_plan", pilotGuide.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("stage 6: external_consumer_resource_pack", pilotGuide.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("stage 12: workspace_submit", pilotGuide.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("stage 14: review_writeback", pilotGuide.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("stage 16: target_commit_plan", pilotGuide.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("stage 17: target_commit_closure", pilotGuide.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("stage 18: target_residue_policy", pilotGuide.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("stage 19: target_ignore_decision_plan", pilotGuide.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("stage 20: target_ignore_decision_record", pilotGuide.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("stage 21: local_dist_freshness_smoke", pilotGuide.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("stage 22: target_dist_binding_plan", pilotGuide.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("stage 23: local_dist_handoff", pilotGuide.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("stage 24: frozen_dist_target_readback_proof", pilotGuide.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("stage 25: product_pilot_proof", pilotGuide.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime product closure pilot status", pilotStatus.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", pilotStatus.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Current stage: 1 attach_target", pilotStatus.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Next command: carves init [target-path] --json", pilotStatus.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime target agent bootstrap pack", targetBootstrap.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", targetBootstrap.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture: target_agent_bootstrap_blocked_by_runtime_init", targetBootstrap.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime target commit plan", targetCommitPlan.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", targetCommitPlan.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime target commit closure", targetCommitClosure.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", targetCommitClosure.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime target residue policy", targetResiduePolicy.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime target residue policy", pilotResiduePolicy.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", targetResiduePolicy.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime target ignore decision plan", targetIgnoreDecisionPlan.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime target ignore decision plan", pilotIgnoreDecisionPlan.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", targetIgnoreDecisionPlan.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime target ignore decision record", targetIgnoreDecisionRecord.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime target ignore decision record", pilotIgnoreDecisionRecord.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", targetIgnoreDecisionRecord.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Record audit ready:", targetIgnoreDecisionRecord.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Decision record commit ready:", targetIgnoreDecisionRecord.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime local dist handoff", localDist.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", localDist.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime product pilot proof", productPilotProof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", productPilotProof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture: product_pilot_proof_waiting_for_local_dist_freshness_smoke", productPilotProof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime external consumer resource pack", externalConsumerResources.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", externalConsumerResources.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture: external_consumer_resource_pack_ready", externalConsumerResources.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime alpha external-use readiness", alphaExternalUseReadiness.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime alpha external-use readiness", pilotReadiness.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime alpha external-use readiness", pilotAlpha.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", alphaExternalUseReadiness.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product pilot proof required per target: True", alphaExternalUseReadiness.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime agent thread start", agentThreadStart.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime agent thread start", agentStart.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", agentThreadStart.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("One command for new thread: carves agent start --json", agentThreadStart.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Next governed command: carves init [target-path] --json", agentThreadStart.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime external target pilot start", externalPilotStart.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime external target pilot start", pilotStart.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", externalPilotStart.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Next governed command: carves init [target-path] --json", externalPilotStart.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime external target pilot next", externalPilotNext.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime external target pilot next", pilotNext.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Next governed command: carves init [target-path] --json", externalPilotNext.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime CLI invocation contract", cliInvocation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime CLI invocation contract", pilotInvocation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", cliInvocation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture: cli_invocation_contract_ready", cliInvocation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Recommended invocation mode: source_tree_wrapper", cliInvocation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime CLI activation plan", cliActivation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime CLI activation plan", pilotActivation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime CLI activation plan", pilotAlias.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", cliActivation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture: cli_activation_plan_ready", cliActivation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Recommended activation lane: absolute_wrapper", cliActivation.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime local dist freshness smoke", localDistFreshnessSmoke.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime local dist freshness smoke", pilotDistSmoke.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime local dist freshness smoke", pilotDistFreshness.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", localDistFreshnessSmoke.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture: local_dist_freshness_smoke_", localDistFreshnessSmoke.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Local dist freshness smoke ready: False", localDistFreshnessSmoke.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime target dist binding plan", targetDistBinding.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime target dist binding plan", pilotDistBinding.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime target dist binding plan", pilotBindDist.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", targetDistBinding.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture: target_dist_binding_plan_blocked_by_missing_dist_resources", targetDistBinding.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime frozen dist target readback proof", frozenDistTargetReadbackProof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime frozen dist target readback proof", pilotTargetProof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime frozen dist target readback proof", pilotExternalProof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", frozenDistTargetReadbackProof.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture: frozen_dist_target_readback_proof_", frozenDistTargetReadbackProof.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(proofApi.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-governed-agent-handoff-proof", root.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", root.GetProperty("product_closure_phase").GetString());
        Assert.Equal("docs/runtime/carves-product-closure-phase-0-baseline.md", root.GetProperty("product_closure_baseline_document_path").GetString());
        Assert.Equal("docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", root.GetProperty("product_closure_current_document_path").GetString());
        Assert.Equal("repo_local", root.GetProperty("runtime_document_root_mode").GetString());
        Assert.Equal("bounded_governed_agent_handoff_proof_ready", root.GetProperty("overall_posture").GetString());
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "agent start [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "inspect runtime-agent-thread-start");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot guide [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot status [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot start [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "inspect runtime-external-target-pilot-start");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot next [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "inspect runtime-external-target-pilot-next");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot problem-intake [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "inspect runtime-agent-problem-intake");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot triage [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "inspect runtime-agent-problem-triage-ledger");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot follow-up [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "inspect runtime-agent-problem-follow-up-candidates");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot readiness [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "inspect runtime-alpha-external-use-readiness");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot invocation [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "inspect runtime-cli-invocation-contract");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot activation [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "inspect runtime-cli-activation-plan");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot dist-smoke [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "inspect runtime-local-dist-freshness-smoke");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot dist-binding [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "inspect runtime-target-dist-binding-plan");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot target-proof [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "inspect runtime-frozen-dist-target-readback-proof");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot resources [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot commit-hygiene [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot commit-plan [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot closure [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot residue [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot ignore-plan [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "inspect runtime-target-ignore-decision-plan");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot ignore-record [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "inspect runtime-target-ignore-decision-record");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot dist [--json]");
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "pilot proof [--json]");
        Assert.Contains(root.GetProperty("proof_stages").EnumerateArray(), stage =>
            stage.GetProperty("stage_id").GetString() == "pre_writeback_blocker_projection");
        Assert.Contains(root.GetProperty("constraint_classes").EnumerateArray(), constraint =>
            constraint.GetProperty("class_id").GetString() == "deferred");

        using var pilotGuideDocument = JsonDocument.Parse(pilotGuideApi.StandardOutput);
        var pilotGuideRoot = pilotGuideDocument.RootElement;
        Assert.Equal("runtime-product-closure-pilot-guide", pilotGuideRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", pilotGuideRoot.GetProperty("product_closure_phase").GetString());
        Assert.Equal("productized_pilot_guide_ready", pilotGuideRoot.GetProperty("overall_posture").GetString());
        Assert.Equal("read_only_productized_pilot_guide", pilotGuideRoot.GetProperty("authority_model").GetString());
        Assert.Equal("carves pilot status --json", pilotGuideRoot.GetProperty("status_command_entry").GetString());
        Assert.Contains(pilotGuideRoot.GetProperty("steps").EnumerateArray(), step =>
            step.GetProperty("stage_id").GetString() == "external_agent_thread_start");
        Assert.Contains(pilotGuideRoot.GetProperty("steps").EnumerateArray(), step =>
            step.GetProperty("stage_id").GetString() == "cli_invocation_contract");
        Assert.Contains(pilotGuideRoot.GetProperty("steps").EnumerateArray(), step =>
            step.GetProperty("stage_id").GetString() == "cli_activation_plan");
        Assert.Contains(pilotGuideRoot.GetProperty("steps").EnumerateArray(), step =>
            step.GetProperty("stage_id").GetString() == "local_dist_freshness_smoke");
        Assert.Contains(pilotGuideRoot.GetProperty("steps").EnumerateArray(), step =>
            step.GetProperty("stage_id").GetString() == "target_ignore_decision_record");
        Assert.Contains(pilotGuideRoot.GetProperty("steps").EnumerateArray(), step =>
            step.GetProperty("stage_id").GetString() == "target_dist_binding_plan");
        Assert.Contains(pilotGuideRoot.GetProperty("steps").EnumerateArray(), step =>
            step.GetProperty("stage_id").GetString() == "frozen_dist_target_readback_proof");
        Assert.Contains(pilotGuideRoot.GetProperty("steps").EnumerateArray(), step =>
            step.GetProperty("stage_id").GetString() == "external_consumer_resource_pack");
        Assert.Contains(pilotGuideRoot.GetProperty("steps").EnumerateArray(), step =>
            step.GetProperty("stage_id").GetString() == "workspace_submit");
        Assert.Contains(pilotGuideRoot.GetProperty("steps").EnumerateArray(), step =>
            step.GetProperty("stage_id").GetString() == "product_pilot_proof");

        using var pilotStatusDocument = JsonDocument.Parse(pilotStatusApi.StandardOutput);
        var pilotStatusRoot = pilotStatusDocument.RootElement;
        Assert.Equal("runtime-product-closure-pilot-status", pilotStatusRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", pilotStatusRoot.GetProperty("product_closure_phase").GetString());
        Assert.Equal("pilot_status_blocked_by_runtime_init", pilotStatusRoot.GetProperty("overall_posture").GetString());
        Assert.Equal("attach_target", pilotStatusRoot.GetProperty("current_stage_id").GetString());
        Assert.Equal("carves init [target-path] --json", pilotStatusRoot.GetProperty("next_command").GetString());

        using var targetBootstrapDocument = JsonDocument.Parse(targetBootstrapApi.StandardOutput);
        var targetBootstrapRoot = targetBootstrapDocument.RootElement;
        Assert.Equal("runtime-target-agent-bootstrap-pack", targetBootstrapRoot.GetProperty("surface_id").GetString());
        Assert.Equal("target_agent_bootstrap_blocked_by_runtime_init", targetBootstrapRoot.GetProperty("overall_posture").GetString());

        using var targetCommitPlanDocument = JsonDocument.Parse(targetCommitPlanApi.StandardOutput);
        var targetCommitPlanRoot = targetCommitPlanDocument.RootElement;
        Assert.Equal("runtime-target-commit-plan", targetCommitPlanRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", targetCommitPlanRoot.GetProperty("product_closure_phase").GetString());

        using var targetCommitClosureDocument = JsonDocument.Parse(targetCommitClosureApi.StandardOutput);
        var targetCommitClosureRoot = targetCommitClosureDocument.RootElement;
        Assert.Equal("runtime-target-commit-closure", targetCommitClosureRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", targetCommitClosureRoot.GetProperty("product_closure_phase").GetString());

        using var targetResiduePolicyDocument = JsonDocument.Parse(targetResiduePolicyApi.StandardOutput);
        var targetResiduePolicyRoot = targetResiduePolicyDocument.RootElement;
        Assert.Equal("runtime-target-residue-policy", targetResiduePolicyRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", targetResiduePolicyRoot.GetProperty("product_closure_phase").GetString());

        using var pilotResiduePolicyDocument = JsonDocument.Parse(pilotResiduePolicyApi.StandardOutput);
        Assert.Equal("runtime-target-residue-policy", pilotResiduePolicyDocument.RootElement.GetProperty("surface_id").GetString());

        using var targetIgnoreDecisionPlanDocument = JsonDocument.Parse(targetIgnoreDecisionPlanApi.StandardOutput);
        var targetIgnoreDecisionPlanRoot = targetIgnoreDecisionPlanDocument.RootElement;
        Assert.Equal("runtime-target-ignore-decision-plan", targetIgnoreDecisionPlanRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", targetIgnoreDecisionPlanRoot.GetProperty("product_closure_phase").GetString());

        using var pilotIgnoreDecisionPlanDocument = JsonDocument.Parse(pilotIgnoreDecisionPlanApi.StandardOutput);
        Assert.Equal("runtime-target-ignore-decision-plan", pilotIgnoreDecisionPlanDocument.RootElement.GetProperty("surface_id").GetString());

        using var targetIgnoreDecisionRecordDocument = JsonDocument.Parse(targetIgnoreDecisionRecordApi.StandardOutput);
        var targetIgnoreDecisionRecordRoot = targetIgnoreDecisionRecordDocument.RootElement;
        Assert.Equal("runtime-target-ignore-decision-record", targetIgnoreDecisionRecordRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", targetIgnoreDecisionRecordRoot.GetProperty("product_closure_phase").GetString());
        Assert.True(targetIgnoreDecisionRecordRoot.TryGetProperty("record_audit_ready", out _));
        Assert.True(targetIgnoreDecisionRecordRoot.TryGetProperty("decision_record_commit_ready", out _));

        using var pilotIgnoreDecisionRecordDocument = JsonDocument.Parse(pilotIgnoreDecisionRecordApi.StandardOutput);
        Assert.Equal("runtime-target-ignore-decision-record", pilotIgnoreDecisionRecordDocument.RootElement.GetProperty("surface_id").GetString());

        using var localDistDocument = JsonDocument.Parse(localDistApi.StandardOutput);
        var localDistRoot = localDistDocument.RootElement;
        Assert.Equal("runtime-local-dist-handoff", localDistRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", localDistRoot.GetProperty("product_closure_phase").GetString());
        Assert.Equal("source_tree", localDistRoot.GetProperty("runtime_root_kind").GetString());

        using var productPilotProofDocument = JsonDocument.Parse(productPilotProofApi.StandardOutput);
        var productPilotProofRoot = productPilotProofDocument.RootElement;
        Assert.Equal("runtime-product-pilot-proof", productPilotProofRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", productPilotProofRoot.GetProperty("product_closure_phase").GetString());
        Assert.Equal("product_pilot_proof_waiting_for_local_dist_freshness_smoke", productPilotProofRoot.GetProperty("overall_posture").GetString());
        Assert.True(productPilotProofRoot.TryGetProperty("target_ignore_decision_record_commit_ready", out _));
        Assert.False(productPilotProofRoot.GetProperty("local_dist_freshness_smoke_ready").GetBoolean());
        Assert.False(productPilotProofRoot.GetProperty("product_pilot_proof_complete").GetBoolean());

        using var externalConsumerResourcesDocument = JsonDocument.Parse(externalConsumerResourcesApi.StandardOutput);
        var externalConsumerResourcesRoot = externalConsumerResourcesDocument.RootElement;
        Assert.Equal("runtime-external-consumer-resource-pack", externalConsumerResourcesRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", externalConsumerResourcesRoot.GetProperty("product_closure_phase").GetString());
        Assert.Equal("external_consumer_resource_pack_ready", externalConsumerResourcesRoot.GetProperty("overall_posture").GetString());
        Assert.True(externalConsumerResourcesRoot.GetProperty("resource_pack_complete").GetBoolean());
        Assert.Contains(externalConsumerResourcesRoot.GetProperty("command_entries").EnumerateArray(), command =>
            command.GetProperty("surface_id").GetString() == "runtime-agent-thread-start");
        Assert.Contains(externalConsumerResourcesRoot.GetProperty("command_entries").EnumerateArray(), command =>
            command.GetProperty("surface_id").GetString() == "runtime-agent-short-context");
        Assert.Contains(externalConsumerResourcesRoot.GetProperty("command_entries").EnumerateArray(), command =>
            command.GetProperty("surface_id").GetString() == "runtime-markdown-read-path-budget");
        Assert.Contains(externalConsumerResourcesRoot.GetProperty("command_entries").EnumerateArray(), command =>
            command.GetProperty("surface_id").GetString() == "runtime-worker-execution-audit");
        Assert.Contains(externalConsumerResourcesRoot.GetProperty("command_entries").EnumerateArray(), command =>
            command.GetProperty("surface_id").GetString() == "runtime-external-target-pilot-start");
        Assert.Contains(externalConsumerResourcesRoot.GetProperty("command_entries").EnumerateArray(), command =>
            command.GetProperty("surface_id").GetString() == "runtime-external-target-pilot-next");
        Assert.Contains(externalConsumerResourcesRoot.GetProperty("command_entries").EnumerateArray(), command =>
            command.GetProperty("surface_id").GetString() == "runtime-cli-invocation-contract");
        Assert.Contains(externalConsumerResourcesRoot.GetProperty("command_entries").EnumerateArray(), command =>
            command.GetProperty("surface_id").GetString() == "runtime-cli-activation-plan");
        Assert.Contains(externalConsumerResourcesRoot.GetProperty("command_entries").EnumerateArray(), command =>
            command.GetProperty("surface_id").GetString() == "runtime-local-dist-freshness-smoke");
        Assert.Contains(externalConsumerResourcesRoot.GetProperty("command_entries").EnumerateArray(), command =>
            command.GetProperty("surface_id").GetString() == "runtime-alpha-external-use-readiness");
        Assert.Contains(externalConsumerResourcesRoot.GetProperty("command_entries").EnumerateArray(), command =>
            command.GetProperty("surface_id").GetString() == "runtime-target-dist-binding-plan");
        Assert.Contains(externalConsumerResourcesRoot.GetProperty("command_entries").EnumerateArray(), command =>
            command.GetProperty("surface_id").GetString() == "runtime-frozen-dist-target-readback-proof");
        Assert.Contains(externalConsumerResourcesRoot.GetProperty("command_entries").EnumerateArray(), command =>
            command.GetProperty("surface_id").GetString() == "runtime-agent-problem-intake");
        Assert.Contains(externalConsumerResourcesRoot.GetProperty("command_entries").EnumerateArray(), command =>
            command.GetProperty("surface_id").GetString() == "runtime-agent-problem-triage-ledger");
        Assert.Contains(externalConsumerResourcesRoot.GetProperty("command_entries").EnumerateArray(), command =>
            command.GetProperty("surface_id").GetString() == "runtime-agent-problem-follow-up-candidates");

        using var alphaExternalUseReadinessDocument = JsonDocument.Parse(alphaExternalUseReadinessApi.StandardOutput);
        var alphaExternalUseReadinessRoot = alphaExternalUseReadinessDocument.RootElement;
        Assert.Equal("runtime-alpha-external-use-readiness", alphaExternalUseReadinessRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", alphaExternalUseReadinessRoot.GetProperty("product_closure_phase").GetString());
        Assert.Equal("docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", alphaExternalUseReadinessRoot.GetProperty("phase_document_path").GetString());
        Assert.True(alphaExternalUseReadinessRoot.GetProperty("product_pilot_proof_required_per_target").GetBoolean());
        Assert.Contains(alphaExternalUseReadinessRoot.GetProperty("minimum_operator_readbacks").EnumerateArray(), command =>
            command.GetString() == "carves agent start --json");
        Assert.Contains(alphaExternalUseReadinessRoot.GetProperty("minimum_operator_readbacks").EnumerateArray(), command =>
            command.GetString() == "carves pilot readiness --json");
        Assert.Contains(alphaExternalUseReadinessRoot.GetProperty("minimum_operator_readbacks").EnumerateArray(), command =>
            command.GetString() == "carves pilot problem-intake --json");
        Assert.Contains(alphaExternalUseReadinessRoot.GetProperty("minimum_operator_readbacks").EnumerateArray(), command =>
            command.GetString() == "carves pilot triage --json");
        Assert.Contains(alphaExternalUseReadinessRoot.GetProperty("minimum_operator_readbacks").EnumerateArray(), command =>
            command.GetString() == "carves pilot follow-up --json");
        Assert.Contains(alphaExternalUseReadinessRoot.GetProperty("readiness_checks").EnumerateArray(), check =>
            check.GetProperty("check_id").GetString() == "target_product_pilot_proof"
            && !check.GetProperty("blocks_alpha_use").GetBoolean());

        using var pilotReadinessDocument = JsonDocument.Parse(pilotReadinessApi.StandardOutput);
        Assert.Equal("runtime-alpha-external-use-readiness", pilotReadinessDocument.RootElement.GetProperty("surface_id").GetString());

        using var pilotAlphaDocument = JsonDocument.Parse(pilotAlphaApi.StandardOutput);
        Assert.Equal("runtime-alpha-external-use-readiness", pilotAlphaDocument.RootElement.GetProperty("surface_id").GetString());

        using var agentThreadStartDocument = JsonDocument.Parse(agentThreadStartApi.StandardOutput);
        var agentThreadStartRoot = agentThreadStartDocument.RootElement;
        Assert.Equal("runtime-agent-thread-start", agentThreadStartRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", agentThreadStartRoot.GetProperty("product_closure_phase").GetString());
        Assert.Equal("carves agent start --json", agentThreadStartRoot.GetProperty("one_command_for_new_thread").GetString());
        Assert.Equal("carves init [target-path] --json", agentThreadStartRoot.GetProperty("next_governed_command").GetString());
        Assert.Equal("pilot_status", agentThreadStartRoot.GetProperty("next_command_source").GetString());

        using var agentStartDocument = JsonDocument.Parse(agentStartApi.StandardOutput);
        Assert.Equal("runtime-agent-thread-start", agentStartDocument.RootElement.GetProperty("surface_id").GetString());

        using var agentBootDocument = JsonDocument.Parse(agentBootApi.StandardOutput);
        Assert.Equal("runtime-agent-thread-start", agentBootDocument.RootElement.GetProperty("surface_id").GetString());

        using var pilotBootDocument = JsonDocument.Parse(pilotBootApi.StandardOutput);
        Assert.Equal("runtime-agent-thread-start", pilotBootDocument.RootElement.GetProperty("surface_id").GetString());

        using var pilotAgentStartDocument = JsonDocument.Parse(pilotAgentStartApi.StandardOutput);
        Assert.Equal("runtime-agent-thread-start", pilotAgentStartDocument.RootElement.GetProperty("surface_id").GetString());

        using var externalPilotStartDocument = JsonDocument.Parse(externalPilotStartApi.StandardOutput);
        var externalPilotStartRoot = externalPilotStartDocument.RootElement;
        Assert.Equal("runtime-external-target-pilot-start", externalPilotStartRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", externalPilotStartRoot.GetProperty("product_closure_phase").GetString());
        Assert.Equal("carves init [target-path] --json", externalPilotStartRoot.GetProperty("next_governed_command").GetString());

        using var pilotStartDocument = JsonDocument.Parse(pilotStartApi.StandardOutput);
        Assert.Equal("runtime-external-target-pilot-start", pilotStartDocument.RootElement.GetProperty("surface_id").GetString());

        using var externalPilotNextDocument = JsonDocument.Parse(externalPilotNextApi.StandardOutput);
        var externalPilotNextRoot = externalPilotNextDocument.RootElement;
        Assert.Equal("runtime-external-target-pilot-next", externalPilotNextRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", externalPilotNextRoot.GetProperty("product_closure_phase").GetString());
        Assert.Equal("carves init [target-path] --json", externalPilotNextRoot.GetProperty("next_governed_command").GetString());

        using var pilotNextDocument = JsonDocument.Parse(pilotNextApi.StandardOutput);
        Assert.Equal("runtime-external-target-pilot-next", pilotNextDocument.RootElement.GetProperty("surface_id").GetString());

        Assert.Contains("Runtime agent problem intake", agentProblemIntake.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", agentProblemIntake.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Problem intake ready: True", agentProblemIntake.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves pilot report-problem <json-path>", agentProblemIntake.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime agent problem triage ledger", agentProblemTriageLedger.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime agent problem triage ledger", pilotTriage.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", agentProblemTriageLedger.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Triage ledger ready: True", agentProblemTriageLedger.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Recorded problem count:", agentProblemTriageLedger.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves pilot triage --json", agentProblemTriageLedger.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime agent problem follow-up candidates", agentProblemFollowUpCandidates.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime agent problem follow-up candidates", pilotFollowUp.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", agentProblemFollowUpCandidates.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Follow-up candidates ready: True", agentProblemFollowUpCandidates.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves pilot follow-up --json", agentProblemFollowUpCandidates.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime agent problem follow-up decision plan", agentProblemFollowUpDecisionPlan.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime agent problem follow-up decision plan", pilotFollowUpPlan.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", agentProblemFollowUpDecisionPlan.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Decision plan ready: True", agentProblemFollowUpDecisionPlan.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves pilot follow-up-plan --json", agentProblemFollowUpDecisionPlan.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime agent problem follow-up decision record", agentProblemFollowUpDecisionRecord.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime agent problem follow-up decision record", pilotFollowUpRecord.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", agentProblemFollowUpDecisionRecord.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Decision record ready: True", agentProblemFollowUpDecisionRecord.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves pilot follow-up-record --json", agentProblemFollowUpDecisionRecord.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves pilot record-follow-up-decision", agentProblemFollowUpDecisionRecord.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime agent problem follow-up planning intake", agentProblemFollowUpPlanningIntake.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime agent problem follow-up planning intake", pilotFollowUpIntake.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", agentProblemFollowUpPlanningIntake.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Planning intake ready: True", agentProblemFollowUpPlanningIntake.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves pilot follow-up-intake --json", agentProblemFollowUpPlanningIntake.StandardOutput, StringComparison.Ordinal);

        using var agentProblemIntakeDocument = JsonDocument.Parse(agentProblemIntakeApi.StandardOutput);
        var agentProblemIntakeRoot = agentProblemIntakeDocument.RootElement;
        Assert.Equal("runtime-agent-problem-intake", agentProblemIntakeRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", agentProblemIntakeRoot.GetProperty("product_closure_phase").GetString());
        Assert.Equal("agent_problem_intake_ready", agentProblemIntakeRoot.GetProperty("overall_posture").GetString());
        Assert.True(agentProblemIntakeRoot.GetProperty("problem_intake_ready").GetBoolean());
        Assert.Contains(agentProblemIntakeRoot.GetProperty("accepted_problem_kinds").EnumerateArray(), kind =>
            kind.GetString() == "protected_truth_root_requested");

        using var pilotProblemIntakeDocument = JsonDocument.Parse(pilotProblemIntakeApi.StandardOutput);
        Assert.Equal("runtime-agent-problem-intake", pilotProblemIntakeDocument.RootElement.GetProperty("surface_id").GetString());

        using var agentProblemTriageLedgerDocument = JsonDocument.Parse(agentProblemTriageLedgerApi.StandardOutput);
        var agentProblemTriageLedgerRoot = agentProblemTriageLedgerDocument.RootElement;
        Assert.Equal("runtime-agent-problem-triage-ledger", agentProblemTriageLedgerRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", agentProblemTriageLedgerRoot.GetProperty("product_closure_phase").GetString());
        Assert.Equal("agent_problem_triage_ledger_empty", agentProblemTriageLedgerRoot.GetProperty("overall_posture").GetString());
        Assert.True(agentProblemTriageLedgerRoot.GetProperty("triage_ledger_ready").GetBoolean());
        Assert.Equal("carves pilot triage --json", agentProblemTriageLedgerRoot.GetProperty("json_command_entry").GetString());

        using var pilotTriageDocument = JsonDocument.Parse(pilotTriageApi.StandardOutput);
        Assert.Equal("runtime-agent-problem-triage-ledger", pilotTriageDocument.RootElement.GetProperty("surface_id").GetString());

        using var pilotProblemTriageDocument = JsonDocument.Parse(pilotProblemTriageApi.StandardOutput);
        Assert.Equal("runtime-agent-problem-triage-ledger", pilotProblemTriageDocument.RootElement.GetProperty("surface_id").GetString());

        using var pilotFrictionLedgerDocument = JsonDocument.Parse(pilotFrictionLedgerApi.StandardOutput);
        Assert.Equal("runtime-agent-problem-triage-ledger", pilotFrictionLedgerDocument.RootElement.GetProperty("surface_id").GetString());

        using var agentProblemFollowUpCandidatesDocument = JsonDocument.Parse(agentProblemFollowUpCandidatesApi.StandardOutput);
        var agentProblemFollowUpCandidatesRoot = agentProblemFollowUpCandidatesDocument.RootElement;
        Assert.Equal("runtime-agent-problem-follow-up-candidates", agentProblemFollowUpCandidatesRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", agentProblemFollowUpCandidatesRoot.GetProperty("product_closure_phase").GetString());
        Assert.Equal("agent_problem_follow_up_candidates_empty", agentProblemFollowUpCandidatesRoot.GetProperty("overall_posture").GetString());
        Assert.True(agentProblemFollowUpCandidatesRoot.GetProperty("follow_up_candidates_ready").GetBoolean());
        Assert.Equal("carves pilot follow-up --json", agentProblemFollowUpCandidatesRoot.GetProperty("json_command_entry").GetString());

        using var pilotFollowUpDocument = JsonDocument.Parse(pilotFollowUpApi.StandardOutput);
        Assert.Equal("runtime-agent-problem-follow-up-candidates", pilotFollowUpDocument.RootElement.GetProperty("surface_id").GetString());

        using var pilotProblemFollowUpDocument = JsonDocument.Parse(pilotProblemFollowUpApi.StandardOutput);
        Assert.Equal("runtime-agent-problem-follow-up-candidates", pilotProblemFollowUpDocument.RootElement.GetProperty("surface_id").GetString());

        using var pilotTriageFollowUpDocument = JsonDocument.Parse(pilotTriageFollowUpApi.StandardOutput);
        Assert.Equal("runtime-agent-problem-follow-up-candidates", pilotTriageFollowUpDocument.RootElement.GetProperty("surface_id").GetString());

        using var agentProblemFollowUpDecisionPlanDocument = JsonDocument.Parse(agentProblemFollowUpDecisionPlanApi.StandardOutput);
        var agentProblemFollowUpDecisionPlanRoot = agentProblemFollowUpDecisionPlanDocument.RootElement;
        Assert.Equal("runtime-agent-problem-follow-up-decision-plan", agentProblemFollowUpDecisionPlanRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", agentProblemFollowUpDecisionPlanRoot.GetProperty("product_closure_phase").GetString());
        Assert.Equal("agent_problem_follow_up_decision_plan_empty", agentProblemFollowUpDecisionPlanRoot.GetProperty("overall_posture").GetString());
        Assert.True(agentProblemFollowUpDecisionPlanRoot.GetProperty("decision_plan_ready").GetBoolean());
        Assert.Equal("carves pilot follow-up-plan --json", agentProblemFollowUpDecisionPlanRoot.GetProperty("json_command_entry").GetString());

        using var pilotFollowUpPlanDocument = JsonDocument.Parse(pilotFollowUpPlanApi.StandardOutput);
        Assert.Equal("runtime-agent-problem-follow-up-decision-plan", pilotFollowUpPlanDocument.RootElement.GetProperty("surface_id").GetString());

        using var pilotProblemFollowUpPlanDocument = JsonDocument.Parse(pilotProblemFollowUpPlanApi.StandardOutput);
        Assert.Equal("runtime-agent-problem-follow-up-decision-plan", pilotProblemFollowUpPlanDocument.RootElement.GetProperty("surface_id").GetString());

        using var pilotTriageFollowUpPlanDocument = JsonDocument.Parse(pilotTriageFollowUpPlanApi.StandardOutput);
        Assert.Equal("runtime-agent-problem-follow-up-decision-plan", pilotTriageFollowUpPlanDocument.RootElement.GetProperty("surface_id").GetString());

        using var agentProblemFollowUpDecisionRecordDocument = JsonDocument.Parse(agentProblemFollowUpDecisionRecordApi.StandardOutput);
        var agentProblemFollowUpDecisionRecordRoot = agentProblemFollowUpDecisionRecordDocument.RootElement;
        Assert.Equal("runtime-agent-problem-follow-up-decision-record", agentProblemFollowUpDecisionRecordRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", agentProblemFollowUpDecisionRecordRoot.GetProperty("product_closure_phase").GetString());
        Assert.Equal("agent_problem_follow_up_decision_record_no_decision_required", agentProblemFollowUpDecisionRecordRoot.GetProperty("overall_posture").GetString());
        Assert.True(agentProblemFollowUpDecisionRecordRoot.GetProperty("decision_plan_ready").GetBoolean());
        Assert.True(agentProblemFollowUpDecisionRecordRoot.GetProperty("decision_record_ready").GetBoolean());
        Assert.Equal("carves pilot follow-up-record --json", agentProblemFollowUpDecisionRecordRoot.GetProperty("json_command_entry").GetString());
        Assert.Equal("carves pilot record-follow-up-decision <decision> --candidate <candidate-id> --reason <text>", agentProblemFollowUpDecisionRecordRoot.GetProperty("record_command_entry").GetString());

        using var pilotFollowUpRecordDocument = JsonDocument.Parse(pilotFollowUpRecordApi.StandardOutput);
        Assert.Equal("runtime-agent-problem-follow-up-decision-record", pilotFollowUpRecordDocument.RootElement.GetProperty("surface_id").GetString());

        using var pilotFollowUpDecisionRecordAliasDocument = JsonDocument.Parse(pilotFollowUpDecisionRecordAliasApi.StandardOutput);
        Assert.Equal("runtime-agent-problem-follow-up-decision-record", pilotFollowUpDecisionRecordAliasDocument.RootElement.GetProperty("surface_id").GetString());

        using var pilotProblemFollowUpRecordAliasDocument = JsonDocument.Parse(pilotProblemFollowUpRecordAliasApi.StandardOutput);
        Assert.Equal("runtime-agent-problem-follow-up-decision-record", pilotProblemFollowUpRecordAliasDocument.RootElement.GetProperty("surface_id").GetString());

        using var agentProblemFollowUpPlanningIntakeDocument = JsonDocument.Parse(agentProblemFollowUpPlanningIntakeApi.StandardOutput);
        var agentProblemFollowUpPlanningIntakeRoot = agentProblemFollowUpPlanningIntakeDocument.RootElement;
        Assert.Equal("runtime-agent-problem-follow-up-planning-intake", agentProblemFollowUpPlanningIntakeRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", agentProblemFollowUpPlanningIntakeRoot.GetProperty("product_closure_phase").GetString());
        Assert.Equal("agent_problem_follow_up_planning_intake_no_accepted_records", agentProblemFollowUpPlanningIntakeRoot.GetProperty("overall_posture").GetString());
        Assert.True(agentProblemFollowUpPlanningIntakeRoot.GetProperty("decision_record_ready").GetBoolean());
        Assert.True(agentProblemFollowUpPlanningIntakeRoot.GetProperty("planning_intake_ready").GetBoolean());
        Assert.Equal("carves pilot follow-up-intake --json", agentProblemFollowUpPlanningIntakeRoot.GetProperty("json_command_entry").GetString());

        using var pilotFollowUpIntakeDocument = JsonDocument.Parse(pilotFollowUpIntakeApi.StandardOutput);
        Assert.Equal("runtime-agent-problem-follow-up-planning-intake", pilotFollowUpIntakeDocument.RootElement.GetProperty("surface_id").GetString());

        using var pilotFollowUpPlanningAliasDocument = JsonDocument.Parse(pilotFollowUpPlanningAliasApi.StandardOutput);
        Assert.Equal("runtime-agent-problem-follow-up-planning-intake", pilotFollowUpPlanningAliasDocument.RootElement.GetProperty("surface_id").GetString());

        using var pilotProblemFollowUpIntakeAliasDocument = JsonDocument.Parse(pilotProblemFollowUpIntakeAliasApi.StandardOutput);
        Assert.Equal("runtime-agent-problem-follow-up-planning-intake", pilotProblemFollowUpIntakeAliasDocument.RootElement.GetProperty("surface_id").GetString());

        Assert.Contains("Runtime agent problem follow-up planning gate", agentProblemFollowUpPlanningGate.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime agent problem follow-up planning gate", pilotFollowUpGate.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", agentProblemFollowUpPlanningGate.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture: agent_problem_follow_up_planning_gate_no_accepted_records", agentProblemFollowUpPlanningGate.StandardOutput, StringComparison.Ordinal);
        using var agentProblemFollowUpPlanningGateDocument = JsonDocument.Parse(agentProblemFollowUpPlanningGateApi.StandardOutput);
        var agentProblemFollowUpPlanningGateRoot = agentProblemFollowUpPlanningGateDocument.RootElement;
        Assert.Equal("runtime-agent-problem-follow-up-planning-gate", agentProblemFollowUpPlanningGateRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", agentProblemFollowUpPlanningGateRoot.GetProperty("product_closure_phase").GetString());
        Assert.Equal("agent_problem_follow_up_planning_gate_no_accepted_records", agentProblemFollowUpPlanningGateRoot.GetProperty("overall_posture").GetString());
        Assert.True(agentProblemFollowUpPlanningGateRoot.GetProperty("planning_intake_ready").GetBoolean());
        Assert.Equal("carves pilot follow-up-gate --json", agentProblemFollowUpPlanningGateRoot.GetProperty("json_command_entry").GetString());
        Assert.Equal("carves inspect runtime-agent-problem-follow-up-planning-gate", agentProblemFollowUpPlanningGateRoot.GetProperty("inspect_command_entry").GetString());

        using var pilotFollowUpGateDocument = JsonDocument.Parse(pilotFollowUpGateApi.StandardOutput);
        Assert.Equal("runtime-agent-problem-follow-up-planning-gate", pilotFollowUpGateDocument.RootElement.GetProperty("surface_id").GetString());

        using var pilotFollowUpPlanningGateAliasDocument = JsonDocument.Parse(pilotFollowUpPlanningGateAliasApi.StandardOutput);
        Assert.Equal("runtime-agent-problem-follow-up-planning-gate", pilotFollowUpPlanningGateAliasDocument.RootElement.GetProperty("surface_id").GetString());

        using var pilotProblemFollowUpGateAliasDocument = JsonDocument.Parse(pilotProblemFollowUpGateAliasApi.StandardOutput);
        Assert.Equal("runtime-agent-problem-follow-up-planning-gate", pilotProblemFollowUpGateAliasDocument.RootElement.GetProperty("surface_id").GetString());

        using var cliInvocationDocument = JsonDocument.Parse(cliInvocationApi.StandardOutput);
        var cliInvocationRoot = cliInvocationDocument.RootElement;
        Assert.Equal("runtime-cli-invocation-contract", cliInvocationRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", cliInvocationRoot.GetProperty("product_closure_phase").GetString());
        Assert.Equal("cli_invocation_contract_ready", cliInvocationRoot.GetProperty("overall_posture").GetString());
        Assert.True(cliInvocationRoot.GetProperty("invocation_contract_complete").GetBoolean());
        Assert.Equal("source_tree_wrapper", cliInvocationRoot.GetProperty("recommended_invocation_mode").GetString());
        Assert.Contains(cliInvocationRoot.GetProperty("invocation_lanes").EnumerateArray(), lane =>
            lane.GetProperty("lane_id").GetString() == "local_dist_wrapper");

        using var pilotInvocationDocument = JsonDocument.Parse(pilotInvocationApi.StandardOutput);
        Assert.Equal("runtime-cli-invocation-contract", pilotInvocationDocument.RootElement.GetProperty("surface_id").GetString());

        using var cliActivationDocument = JsonDocument.Parse(cliActivationApi.StandardOutput);
        var cliActivationRoot = cliActivationDocument.RootElement;
        Assert.Equal("runtime-cli-activation-plan", cliActivationRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", cliActivationRoot.GetProperty("product_closure_phase").GetString());
        Assert.Equal("cli_activation_plan_ready", cliActivationRoot.GetProperty("overall_posture").GetString());
        Assert.True(cliActivationRoot.GetProperty("activation_plan_complete").GetBoolean());
        Assert.Equal("absolute_wrapper", cliActivationRoot.GetProperty("recommended_activation_lane").GetString());
        Assert.Contains(cliActivationRoot.GetProperty("activation_lanes").EnumerateArray(), lane =>
            lane.GetProperty("lane_id").GetString() == "powershell_session_alias");

        using var pilotActivationDocument = JsonDocument.Parse(pilotActivationApi.StandardOutput);
        Assert.Equal("runtime-cli-activation-plan", pilotActivationDocument.RootElement.GetProperty("surface_id").GetString());

        using var pilotAliasDocument = JsonDocument.Parse(pilotAliasApi.StandardOutput);
        Assert.Equal("runtime-cli-activation-plan", pilotAliasDocument.RootElement.GetProperty("surface_id").GetString());

        using var localDistFreshnessSmokeDocument = JsonDocument.Parse(localDistFreshnessSmokeApi.StandardOutput);
        var localDistFreshnessSmokeRoot = localDistFreshnessSmokeDocument.RootElement;
        Assert.Equal("runtime-local-dist-freshness-smoke", localDistFreshnessSmokeRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", localDistFreshnessSmokeRoot.GetProperty("product_closure_phase").GetString());
        Assert.StartsWith("local_dist_freshness_smoke_", localDistFreshnessSmokeRoot.GetProperty("overall_posture").GetString(), StringComparison.Ordinal);
        Assert.False(localDistFreshnessSmokeRoot.GetProperty("local_dist_freshness_smoke_ready").GetBoolean());

        using var pilotDistSmokeDocument = JsonDocument.Parse(pilotDistSmokeApi.StandardOutput);
        Assert.Equal("runtime-local-dist-freshness-smoke", pilotDistSmokeDocument.RootElement.GetProperty("surface_id").GetString());

        using var pilotDistFreshnessDocument = JsonDocument.Parse(pilotDistFreshnessApi.StandardOutput);
        Assert.Equal("runtime-local-dist-freshness-smoke", pilotDistFreshnessDocument.RootElement.GetProperty("surface_id").GetString());

        using var targetDistBindingDocument = JsonDocument.Parse(targetDistBindingApi.StandardOutput);
        var targetDistBindingRoot = targetDistBindingDocument.RootElement;
        Assert.Equal("runtime-target-dist-binding-plan", targetDistBindingRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", targetDistBindingRoot.GetProperty("product_closure_phase").GetString());
        Assert.Equal("target_dist_binding_plan_blocked_by_missing_dist_resources", targetDistBindingRoot.GetProperty("overall_posture").GetString());
        Assert.False(targetDistBindingRoot.GetProperty("dist_binding_plan_complete").GetBoolean());
        Assert.Contains(targetDistBindingRoot.GetProperty("required_readback_commands").EnumerateArray(), readback =>
            readback.GetString() == "carves pilot dist-smoke --json");
        Assert.Contains(targetDistBindingRoot.GetProperty("required_readback_commands").EnumerateArray(), readback =>
            readback.GetString() == "carves pilot dist-binding --json");
        Assert.Contains(targetDistBindingRoot.GetProperty("required_readback_commands").EnumerateArray(), readback =>
            readback.GetString() == "carves pilot target-proof --json");

        using var pilotDistBindingDocument = JsonDocument.Parse(pilotDistBindingApi.StandardOutput);
        Assert.Equal("runtime-target-dist-binding-plan", pilotDistBindingDocument.RootElement.GetProperty("surface_id").GetString());

        using var pilotBindDistDocument = JsonDocument.Parse(pilotBindDistApi.StandardOutput);
        Assert.Equal("runtime-target-dist-binding-plan", pilotBindDistDocument.RootElement.GetProperty("surface_id").GetString());

        using var frozenDistTargetReadbackProofDocument = JsonDocument.Parse(frozenDistTargetReadbackProofApi.StandardOutput);
        var frozenDistTargetReadbackProofRoot = frozenDistTargetReadbackProofDocument.RootElement;
        Assert.Equal("runtime-frozen-dist-target-readback-proof", frozenDistTargetReadbackProofRoot.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", frozenDistTargetReadbackProofRoot.GetProperty("product_closure_phase").GetString());
        Assert.StartsWith("frozen_dist_target_readback_proof_", frozenDistTargetReadbackProofRoot.GetProperty("overall_posture").GetString(), StringComparison.Ordinal);
        Assert.False(frozenDistTargetReadbackProofRoot.GetProperty("frozen_dist_target_readback_proof_complete").GetBoolean());

        using var pilotTargetProofDocument = JsonDocument.Parse(pilotTargetProofApi.StandardOutput);
        Assert.Equal("runtime-frozen-dist-target-readback-proof", pilotTargetProofDocument.RootElement.GetProperty("surface_id").GetString());

        using var pilotExternalProofDocument = JsonDocument.Parse(pilotExternalProofApi.StandardOutput);
        Assert.Equal("runtime-frozen-dist-target-readback-proof", pilotExternalProofDocument.RootElement.GetProperty("surface_id").GetString());
    }

    [Fact]
    public void AgentHandoffAlias_ProjectsGovernedProofWithoutResidentHost()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "agent", "handoff");
        var api = ProgramHarness.Run("--repo-root", sandbox.RootPath, "agent", "handoff", "--json");
        var friendlyCli = CliProgramHarness.RunInDirectory(sandbox.RootPath, "agent", "handoff");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Equal(0, friendlyCli.ExitCode);
        Assert.Contains("Runtime governed agent handoff proof", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime governed agent handoff proof", friendlyCli.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Product closure current: docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture: bounded_governed_agent_handoff_proof_ready", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Use `host start` before calling the agent gateway.", inspect.CombinedOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Use `host start` before calling the agent gateway.", friendlyCli.CombinedOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-governed-agent-handoff-proof", root.GetProperty("surface_id").GetString());
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", root.GetProperty("product_closure_phase").GetString());
        Assert.Equal("docs/runtime/carves-product-closure-phase-0-baseline.md", root.GetProperty("product_closure_baseline_document_path").GetString());
        Assert.Equal("docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", root.GetProperty("product_closure_current_document_path").GetString());
        Assert.Equal("repo_local", root.GetProperty("runtime_document_root_mode").GetString());
        Assert.Equal("bounded_governed_agent_handoff_proof_ready", root.GetProperty("overall_posture").GetString());
        Assert.True(root.GetProperty("is_valid").GetBoolean());
        Assert.Contains(root.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
            readback.GetString() == "agent handoff");
    }

    [Fact]
    public void WorkspaceMutationAudit_InspectAndApiProjectMutationBlockers()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-HANDOFF-AUDIT";
        sandbox.AddSyntheticPendingTask(taskId, scope: ["docs/runtime/"]);
        WriteResultEnvelope(sandbox.RootPath, taskId, ["src/Outside.cs", ".ai/tasks/graph.json"]);

        var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-workspace-mutation-audit", taskId);
        var api = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-workspace-mutation-audit", taskId);

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime workspace mutation audit", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Status: host_only", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Can proceed to writeback: False", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("mutation_audit_host_only", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("path=.ai/tasks/graph.json", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-workspace-mutation-audit", root.GetProperty("surface_id").GetString());
        Assert.Equal(taskId, root.GetProperty("task_id").GetString());
        Assert.Equal("host_only", root.GetProperty("status").GetString());
        Assert.False(root.GetProperty("can_proceed_to_writeback").GetBoolean());
        Assert.Contains(root.GetProperty("changed_paths").EnumerateArray(), path =>
            path.GetProperty("path").GetString() == ".ai/tasks/graph.json"
            && path.GetProperty("policy_class").GetString() == "host_only");
        Assert.Contains(root.GetProperty("blockers").EnumerateArray(), blocker =>
            blocker.GetProperty("blocker_id").GetString() == "mutation_audit_host_only");
    }

    private static void WriteResultEnvelope(string repoRoot, string taskId, IReadOnlyList<string> changedFiles)
    {
        var resultPath = Path.Combine(repoRoot, ".ai", "execution", taskId, "result.json");
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);
        File.WriteAllText(
            resultPath,
            JsonSerializer.Serialize(
                new ResultEnvelope
                {
                    TaskId = taskId,
                    ExecutionRunId = $"RUN-{taskId}-001",
                    ExecutionEvidencePath = $".ai/artifacts/worker-executions/RUN-{taskId}-001/evidence.json",
                    Status = "success",
                    Changes = new ResultEnvelopeChanges
                    {
                        FilesModified = changedFiles,
                    },
                },
                JsonOptions));
    }
}

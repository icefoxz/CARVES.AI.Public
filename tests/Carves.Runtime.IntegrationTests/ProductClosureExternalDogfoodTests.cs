using System.Text.Json;
using System.Text.RegularExpressions;

namespace Carves.Runtime.IntegrationTests;

public sealed class ProductClosureExternalDogfoodTests
{
    [Fact]
    public void Phase5_ExternalTargetRepo_RunsInitHandoffFirstRunPacketAndFormalPlanningEntry()
    {
        using var runtimeRepo = RepoSandbox.CreateFromCurrentRepo();
        runtimeRepo.MarkAllTasksCompleted();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/AgentCoachSeed.cs", "namespace AgentCoach; public sealed class AgentCoachSeed { }");
        targetRepo.CommitAll("Initial commit");

        ProgramHarness.Run("--repo-root", runtimeRepo.RootPath, "host", "start", "--interval-ms", "200");
        var previousRuntimeRoot = Environment.GetEnvironmentVariable("CARVES_RUNTIME_ROOT");
        Environment.SetEnvironmentVariable("CARVES_RUNTIME_ROOT", runtimeRepo.RootPath);

        try
        {
            var init = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "init", "--json");
            var doctor = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "doctor", "--json");
            var handoff = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "agent", "handoff", "--json");
            var firstRunPacket = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-first-run-operator-packet");

            Assert.Equal(0, init.ExitCode);
            Assert.Equal(0, doctor.ExitCode);
            Assert.Equal(0, handoff.ExitCode);
            Assert.Equal(0, firstRunPacket.ExitCode);

            using var initDocument = JsonDocument.Parse(init.StandardOutput);
            var initRoot = initDocument.RootElement;
            Assert.Equal("initialized_runtime", initRoot.GetProperty("action").GetString());
            Assert.Equal("not_required_wrapper_runtime_root", initRoot.GetProperty("host_readiness").GetString());
            Assert.Equal("initialized", initRoot.GetProperty("runtime_readiness_after").GetString());

            var handshakePath = Path.Combine(targetRepo.RootPath, ".ai", "runtime", "attach-handshake.json");
            using var handshakeDocument = JsonDocument.Parse(File.ReadAllText(handshakePath));
            Assert.Equal(
                Path.GetFullPath(runtimeRepo.RootPath),
                handshakeDocument.RootElement.GetProperty("request").GetProperty("runtime_root").GetString());

            var manifestPath = Path.Combine(targetRepo.RootPath, ".ai", "runtime.json");
            using var manifestDocument = JsonDocument.Parse(File.ReadAllText(manifestPath));
            Assert.Equal(
                Path.GetFullPath(runtimeRepo.RootPath),
                manifestDocument.RootElement.GetProperty("runtime_root").GetString());

            using var doctorDocument = JsonDocument.Parse(doctor.StandardOutput);
            var doctorRoot = doctorDocument.RootElement;
            Assert.Equal("runtime_initialized", doctorRoot.GetProperty("target_repo_readiness").GetString());
            Assert.Equal("connected", doctorRoot.GetProperty("host_readiness").GetString());
            Assert.True(doctorRoot.GetProperty("is_ready").GetBoolean());

            using var handoffDocument = JsonDocument.Parse(handoff.StandardOutput);
            var handoffRoot = handoffDocument.RootElement;
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", handoffRoot.GetProperty("product_closure_phase").GetString());
            Assert.Equal("attach_handshake_runtime_root", handoffRoot.GetProperty("runtime_document_root_mode").GetString());
            Assert.Equal(Path.GetFullPath(runtimeRepo.RootPath), handoffRoot.GetProperty("runtime_document_root").GetString());
            Assert.True(handoffRoot.GetProperty("is_valid").GetBoolean());

            var targetAgentBootstrapPath = Path.Combine(targetRepo.RootPath, ".ai", "AGENT_BOOTSTRAP.md");
            var rootAgentsPath = Path.Combine(targetRepo.RootPath, "AGENTS.md");
            Assert.True(File.Exists(targetAgentBootstrapPath));
            Assert.True(File.Exists(rootAgentsPath));
            var targetAgentBootstrap = File.ReadAllText(targetAgentBootstrapPath);
            var rootAgents = File.ReadAllText(rootAgentsPath);
            Assert.Contains("agent start --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot readiness --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot invocation --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot activation --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot dist-smoke --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot dist-binding --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot target-proof --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot status --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot resources --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot problem-intake --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot triage --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot follow-up --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot follow-up-plan --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot follow-up-record --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot follow-up-intake --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot follow-up-gate --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("current_stage_id", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("next_governed_command", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("Do not edit `.ai/` official truth manually", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot commit-plan", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot closure --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot residue --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot ignore-plan --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot ignore-record --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("pilot proof --json", targetAgentBootstrap, StringComparison.Ordinal);
            Assert.Contains("agent start --json", rootAgents, StringComparison.Ordinal);
            Assert.Contains("next_governed_command", rootAgents, StringComparison.Ordinal);
            Assert.Contains(".ai/AGENT_BOOTSTRAP.md", rootAgents, StringComparison.Ordinal);

            Assert.Contains("Runtime first-run operator packet", firstRunPacket.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime document root mode: attach_handshake_runtime_root", firstRunPacket.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Overall posture: first_run_packet_ready", firstRunPacket.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Internal beta gate target-scope readback is summarized for attached targets", firstRunPacket.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("docs/runtime/runtime-first-run-operator-packet.md' is missing", firstRunPacket.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Internal beta gate surface target-scope readback", firstRunPacket.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(".ai/refactoring/queues/index.json", firstRunPacket.StandardOutput, StringComparison.Ordinal);

            var draft = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "intent", "draft", "--persist");
            var focus = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "intent", "focus", "candidate-first-slice");
            var resolveValidation = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "intent", "decision", "first_validation_artifact", "resolved");
            var resolveBoundary = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "intent", "decision", "first_slice_boundary", "resolved");
            var ready = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "intent", "candidate", "candidate-first-slice", "ready_to_plan");
            var planInit = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "plan", "init", "candidate-first-slice");
            var planStatus = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "plan", "status");

            Assert.Equal(0, draft.ExitCode);
            Assert.Equal(0, focus.ExitCode);
            Assert.Equal(0, resolveValidation.ExitCode);
            Assert.Equal(0, resolveBoundary.ExitCode);
            Assert.Equal(0, ready.ExitCode);
            Assert.Equal(0, planInit.ExitCode);
            Assert.Equal(0, planStatus.ExitCode);

            using var planStatusDocument = ParseJsonFromOutput(planStatus.StandardOutput);
            var planStatusRoot = planStatusDocument.RootElement;
            Assert.Equal("planning", planStatusRoot.GetProperty("formal_planning_state").GetString());
            Assert.Equal("occupied_by_packet", planStatusRoot.GetProperty("active_planning_slot_state").GetString());
            Assert.Equal("ready_to_export", planStatusRoot.GetProperty("active_planning_card_fill_state").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_RUNTIME_ROOT", previousRuntimeRoot);
            ProgramHarness.Run("--repo-root", runtimeRepo.RootPath, "host", "stop", "phase 4 external target dogfood cleanup");
        }
    }

    [Fact]
    public void Phase12_ExternalTargetRepo_ProjectsPilotStatusGuideAndBootstrapWithoutMutatingTruth()
    {
        using var runtimeRepo = RepoSandbox.CreateFromCurrentRepo();
        runtimeRepo.MarkAllTasksCompleted();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/AgentCoachSeed.cs", "namespace AgentCoach; public sealed class AgentCoachSeed { }");
        targetRepo.CommitAll("Initial commit");

        ProgramHarness.Run("--repo-root", runtimeRepo.RootPath, "host", "start", "--interval-ms", "200");
        var previousRuntimeRoot = Environment.GetEnvironmentVariable("CARVES_RUNTIME_ROOT");
        Environment.SetEnvironmentVariable("CARVES_RUNTIME_ROOT", runtimeRepo.RootPath);

        try
        {
            var init = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "init", "--json");
            Assert.Equal(0, init.ExitCode);

            var statusBefore = GitTestHarness.RunForStandardOutput(targetRepo.RootPath, "status", "--short");
            var pilotStatus = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "status");
            var pilotStatusJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "status", "--json");
            var pilotPreflightJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "preflight", "--json");
            var inspectStatus = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-product-closure-pilot-status");
            var apiStatus = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "api", "runtime-product-closure-pilot-status");
            var commitHygiene = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "commit-hygiene");
            var commitHygieneJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "commit-hygiene", "--json");
            var commitPlan = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "commit-plan");
            var commitPlanJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "commit-plan", "--json");
            var commitClosure = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "closure");
            var commitClosureJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "closure", "--json");
            var residuePolicy = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "residue");
            var residuePolicyJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "residue", "--json");
            var ignoreDecisionPlan = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "ignore-plan");
            var ignoreDecisionPlanJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "ignore-plan", "--json");
            var ignoreDecisionRecord = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "ignore-record");
            var ignoreDecisionRecordJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "ignore-record", "--json");
            var localDist = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "dist");
            var localDistJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "dist", "--json");
            var productPilotProof = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "proof");
            var productPilotProofJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "proof", "--json");
            var resources = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "resources");
            var resourcesJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "resources", "--json");
            var pilotStart = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "start");
            var pilotStartJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "start", "--json");
            var pilotNext = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "next");
            var pilotNextJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "next", "--json");
            var problemIntake = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "problem-intake");
            var problemIntakeJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "problem-intake", "--json");
            var problemTriage = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "triage");
            var problemTriageJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "triage", "--json");
            var problemTriageAliasJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "problem-triage", "--json");
            var problemFollowUp = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "follow-up");
            var problemFollowUpJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "follow-up", "--json");
            var problemFollowUpAliasJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "problem-follow-up", "--json");
            var problemFollowUpDecisionPlan = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "follow-up-plan");
            var problemFollowUpDecisionPlanJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "follow-up-plan", "--json");
            var problemFollowUpDecisionPlanAliasJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "problem-follow-up-plan", "--json");
            var problemTriageFollowUpPlanAliasJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "triage-follow-up-plan", "--json");
            var problemFollowUpDecisionRecord = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "follow-up-record");
            var problemFollowUpDecisionRecordJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "follow-up-record", "--json");
            var problemFollowUpDecisionRecordAliasJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "follow-up-decision-record", "--json");
            var problemFollowUpRecordAliasJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "problem-follow-up-record", "--json");
            var problemFollowUpPlanningIntake = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "follow-up-intake");
            var problemFollowUpPlanningIntakeJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "follow-up-intake", "--json");
            var problemFollowUpPlanningAliasJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "follow-up-planning", "--json");
            var problemFollowUpIntakeAliasJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "problem-follow-up-intake", "--json");
            var problemFollowUpPlanningGate = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "follow-up-gate");
            var problemFollowUpPlanningGateJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "follow-up-gate", "--json");
            var problemFollowUpPlanningGateAliasJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "follow-up-planning-gate", "--json");
            var problemFollowUpGateAliasJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "pilot", "problem-follow-up-gate", "--json");
            var readiness = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "readiness");
            var readinessJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "readiness", "--json");
            var alpha = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "alpha");
            var alphaJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "alpha", "--json");
            var inspectReadiness = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-alpha-external-use-readiness");
            var apiReadiness = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "api", "runtime-alpha-external-use-readiness");
            var invocation = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "invocation");
            var invocationJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "invocation", "--json");
            var activation = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "activation");
            var activationJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "activation", "--json");
            var alias = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "alias");
            var aliasJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "alias", "--json");
            var distSmoke = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "dist-smoke");
            var distSmokeJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "dist-smoke", "--json");
            var distFreshness = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "dist-freshness");
            var distFreshnessJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "dist-freshness", "--json");
            var distBinding = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "dist-binding");
            var distBindingJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "dist-binding", "--json");
            var bindDist = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "bind-dist");
            var bindDistJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "bind-dist", "--json");
            var targetProof = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "target-proof");
            var targetProofJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "target-proof", "--json");
            var externalProof = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "external-proof");
            var externalProofJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "external-proof", "--json");
            var inspectCommitHygiene = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-target-commit-hygiene");
            var apiCommitHygiene = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "api", "runtime-target-commit-hygiene");
            var inspectCommitPlan = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-target-commit-plan");
            var apiCommitPlan = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "api", "runtime-target-commit-plan");
            var inspectCommitClosure = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-target-commit-closure");
            var apiCommitClosure = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "api", "runtime-target-commit-closure");
            var inspectResiduePolicy = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-target-residue-policy");
            var apiResiduePolicy = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "api", "runtime-target-residue-policy");
            var inspectIgnoreDecisionPlan = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-target-ignore-decision-plan");
            var apiIgnoreDecisionPlan = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "api", "runtime-target-ignore-decision-plan");
            var inspectIgnoreDecisionRecord = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-target-ignore-decision-record");
            var apiIgnoreDecisionRecord = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "api", "runtime-target-ignore-decision-record");
            var inspectLocalDist = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-local-dist-handoff");
            var apiLocalDist = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "api", "runtime-local-dist-handoff");
            var inspectProductPilotProof = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-product-pilot-proof");
            var apiProductPilotProof = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "api", "runtime-product-pilot-proof");
            var inspectResources = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-external-consumer-resource-pack");
            var apiResources = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "api", "runtime-external-consumer-resource-pack");
            var inspectPilotStart = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-external-target-pilot-start");
            var apiPilotStart = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "api", "runtime-external-target-pilot-start");
            var inspectPilotNext = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-external-target-pilot-next");
            var apiPilotNext = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "api", "runtime-external-target-pilot-next");
            var inspectProblemIntake = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "inspect", "runtime-agent-problem-intake");
            var apiProblemIntake = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "api", "runtime-agent-problem-intake");
            var inspectProblemTriage = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "inspect", "runtime-agent-problem-triage-ledger");
            var apiProblemTriage = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "api", "runtime-agent-problem-triage-ledger");
            var inspectProblemFollowUp = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "inspect", "runtime-agent-problem-follow-up-candidates");
            var apiProblemFollowUp = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "api", "runtime-agent-problem-follow-up-candidates");
            var inspectProblemFollowUpDecisionPlan = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "inspect", "runtime-agent-problem-follow-up-decision-plan");
            var apiProblemFollowUpDecisionPlan = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "api", "runtime-agent-problem-follow-up-decision-plan");
            var inspectProblemFollowUpDecisionRecord = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "inspect", "runtime-agent-problem-follow-up-decision-record");
            var apiProblemFollowUpDecisionRecord = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "api", "runtime-agent-problem-follow-up-decision-record");
            var inspectProblemFollowUpPlanningIntake = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "inspect", "runtime-agent-problem-follow-up-planning-intake");
            var apiProblemFollowUpPlanningIntake = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "api", "runtime-agent-problem-follow-up-planning-intake");
            var inspectProblemFollowUpPlanningGate = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "inspect", "runtime-agent-problem-follow-up-planning-gate");
            var apiProblemFollowUpPlanningGate = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "--cold", "api", "runtime-agent-problem-follow-up-planning-gate");
            var inspectInvocation = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-cli-invocation-contract");
            var apiInvocation = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "api", "runtime-cli-invocation-contract");
            var inspectActivation = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-cli-activation-plan");
            var apiActivation = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "api", "runtime-cli-activation-plan");
            var inspectDistSmoke = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-local-dist-freshness-smoke");
            var apiDistSmoke = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "api", "runtime-local-dist-freshness-smoke");
            var inspectDistBinding = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-target-dist-binding-plan");
            var apiDistBinding = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "api", "runtime-target-dist-binding-plan");
            var inspectTargetProof = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-frozen-dist-target-readback-proof");
            var apiTargetProof = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "api", "runtime-frozen-dist-target-readback-proof");
            var guide = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "guide");
            var guideJson = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "guide", "--json");
            var inspect = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-product-closure-pilot-guide");
            var api = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "api", "runtime-product-closure-pilot-guide");
            var handoff = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "agent", "handoff", "--json");
            var statusAfter = GitTestHarness.RunForStandardOutput(targetRepo.RootPath, "status", "--short");

            Assert.Equal(0, pilotStatus.ExitCode);
            Assert.Equal(0, pilotStatusJson.ExitCode);
            Assert.Equal(0, pilotPreflightJson.ExitCode);
            Assert.True(inspectStatus.ExitCode == 0, inspectStatus.CombinedOutput);
            Assert.True(apiStatus.ExitCode == 0, apiStatus.CombinedOutput);
            Assert.Equal(0, commitHygiene.ExitCode);
            Assert.Equal(0, commitHygieneJson.ExitCode);
            Assert.Equal(0, commitPlan.ExitCode);
            Assert.Equal(0, commitPlanJson.ExitCode);
            Assert.Equal(0, commitClosure.ExitCode);
            Assert.Equal(0, commitClosureJson.ExitCode);
            Assert.Equal(0, residuePolicy.ExitCode);
            Assert.Equal(0, residuePolicyJson.ExitCode);
            Assert.Equal(0, ignoreDecisionPlan.ExitCode);
            Assert.Equal(0, ignoreDecisionPlanJson.ExitCode);
            Assert.Equal(0, ignoreDecisionRecord.ExitCode);
            Assert.Equal(0, ignoreDecisionRecordJson.ExitCode);
            Assert.Equal(0, localDist.ExitCode);
            Assert.Equal(0, localDistJson.ExitCode);
            Assert.Equal(0, productPilotProof.ExitCode);
            Assert.Equal(0, productPilotProofJson.ExitCode);
            Assert.Equal(0, resources.ExitCode);
            Assert.Equal(0, resourcesJson.ExitCode);
            Assert.Equal(0, pilotStart.ExitCode);
            Assert.Equal(0, pilotStartJson.ExitCode);
            Assert.Equal(0, pilotNext.ExitCode);
            Assert.Equal(0, pilotNextJson.ExitCode);
            Assert.True(problemIntake.ExitCode == 0, problemIntake.CombinedOutput);
            Assert.True(problemIntakeJson.ExitCode == 0, problemIntakeJson.CombinedOutput);
            Assert.True(problemTriage.ExitCode == 0, problemTriage.CombinedOutput);
            Assert.True(problemTriageJson.ExitCode == 0, problemTriageJson.CombinedOutput);
            Assert.True(problemTriageAliasJson.ExitCode == 0, problemTriageAliasJson.CombinedOutput);
            Assert.True(problemFollowUp.ExitCode == 0, problemFollowUp.CombinedOutput);
            Assert.True(problemFollowUpJson.ExitCode == 0, problemFollowUpJson.CombinedOutput);
            Assert.True(problemFollowUpAliasJson.ExitCode == 0, problemFollowUpAliasJson.CombinedOutput);
            Assert.True(problemFollowUpDecisionPlan.ExitCode == 0, problemFollowUpDecisionPlan.CombinedOutput);
            Assert.True(problemFollowUpDecisionPlanJson.ExitCode == 0, problemFollowUpDecisionPlanJson.CombinedOutput);
            Assert.True(problemFollowUpDecisionPlanAliasJson.ExitCode == 0, problemFollowUpDecisionPlanAliasJson.CombinedOutput);
            Assert.True(problemTriageFollowUpPlanAliasJson.ExitCode == 0, problemTriageFollowUpPlanAliasJson.CombinedOutput);
            Assert.True(problemFollowUpDecisionRecord.ExitCode == 0, problemFollowUpDecisionRecord.CombinedOutput);
            Assert.True(problemFollowUpDecisionRecordJson.ExitCode == 0, problemFollowUpDecisionRecordJson.CombinedOutput);
            Assert.True(problemFollowUpDecisionRecordAliasJson.ExitCode == 0, problemFollowUpDecisionRecordAliasJson.CombinedOutput);
            Assert.True(problemFollowUpRecordAliasJson.ExitCode == 0, problemFollowUpRecordAliasJson.CombinedOutput);
            Assert.True(problemFollowUpPlanningIntake.ExitCode == 0, problemFollowUpPlanningIntake.CombinedOutput);
            Assert.True(problemFollowUpPlanningIntakeJson.ExitCode == 0, problemFollowUpPlanningIntakeJson.CombinedOutput);
            Assert.True(problemFollowUpPlanningAliasJson.ExitCode == 0, problemFollowUpPlanningAliasJson.CombinedOutput);
            Assert.True(problemFollowUpIntakeAliasJson.ExitCode == 0, problemFollowUpIntakeAliasJson.CombinedOutput);
            Assert.True(problemFollowUpPlanningGate.ExitCode == 0, problemFollowUpPlanningGate.CombinedOutput);
            Assert.True(problemFollowUpPlanningGateJson.ExitCode == 0, problemFollowUpPlanningGateJson.CombinedOutput);
            Assert.True(problemFollowUpPlanningGateAliasJson.ExitCode == 0, problemFollowUpPlanningGateAliasJson.CombinedOutput);
            Assert.True(problemFollowUpGateAliasJson.ExitCode == 0, problemFollowUpGateAliasJson.CombinedOutput);
            Assert.Equal(0, readiness.ExitCode);
            Assert.Equal(0, readinessJson.ExitCode);
            Assert.Equal(0, alpha.ExitCode);
            Assert.Equal(0, alphaJson.ExitCode);
            Assert.Equal(0, inspectReadiness.ExitCode);
            Assert.Equal(0, apiReadiness.ExitCode);
            Assert.Equal(0, invocation.ExitCode);
            Assert.Equal(0, invocationJson.ExitCode);
            Assert.Equal(0, activation.ExitCode);
            Assert.Equal(0, activationJson.ExitCode);
            Assert.Equal(0, alias.ExitCode);
            Assert.Equal(0, aliasJson.ExitCode);
            Assert.Equal(0, distSmoke.ExitCode);
            Assert.Equal(0, distSmokeJson.ExitCode);
            Assert.Equal(0, distFreshness.ExitCode);
            Assert.Equal(0, distFreshnessJson.ExitCode);
            Assert.Equal(0, distBinding.ExitCode);
            Assert.Equal(0, distBindingJson.ExitCode);
            Assert.Equal(0, bindDist.ExitCode);
            Assert.Equal(0, bindDistJson.ExitCode);
            Assert.Equal(0, targetProof.ExitCode);
            Assert.Equal(0, targetProofJson.ExitCode);
            Assert.Equal(0, externalProof.ExitCode);
            Assert.Equal(0, externalProofJson.ExitCode);
            Assert.Equal(0, inspectCommitHygiene.ExitCode);
            Assert.Equal(0, apiCommitHygiene.ExitCode);
            Assert.Equal(0, inspectCommitPlan.ExitCode);
            Assert.Equal(0, apiCommitPlan.ExitCode);
            Assert.Equal(0, inspectCommitClosure.ExitCode);
            Assert.Equal(0, apiCommitClosure.ExitCode);
            Assert.Equal(0, inspectResiduePolicy.ExitCode);
            Assert.Equal(0, apiResiduePolicy.ExitCode);
            Assert.Equal(0, inspectIgnoreDecisionPlan.ExitCode);
            Assert.Equal(0, apiIgnoreDecisionPlan.ExitCode);
            Assert.Equal(0, inspectIgnoreDecisionRecord.ExitCode);
            Assert.Equal(0, apiIgnoreDecisionRecord.ExitCode);
            Assert.Equal(0, inspectLocalDist.ExitCode);
            Assert.Equal(0, apiLocalDist.ExitCode);
            Assert.Equal(0, inspectProductPilotProof.ExitCode);
            Assert.Equal(0, apiProductPilotProof.ExitCode);
            Assert.Equal(0, inspectResources.ExitCode);
            Assert.True(apiResources.ExitCode == 0, apiResources.CombinedOutput);
            Assert.Equal(0, inspectPilotStart.ExitCode);
            Assert.Equal(0, apiPilotStart.ExitCode);
            Assert.Equal(0, inspectPilotNext.ExitCode);
            Assert.Equal(0, apiPilotNext.ExitCode);
            Assert.Equal(0, inspectProblemIntake.ExitCode);
            Assert.Equal(0, apiProblemIntake.ExitCode);
            Assert.Equal(0, inspectProblemTriage.ExitCode);
            Assert.Equal(0, apiProblemTriage.ExitCode);
            Assert.Equal(0, inspectProblemFollowUp.ExitCode);
            Assert.Equal(0, apiProblemFollowUp.ExitCode);
            Assert.Equal(0, inspectProblemFollowUpDecisionPlan.ExitCode);
            Assert.Equal(0, apiProblemFollowUpDecisionPlan.ExitCode);
            Assert.Equal(0, inspectProblemFollowUpDecisionRecord.ExitCode);
            Assert.Equal(0, apiProblemFollowUpDecisionRecord.ExitCode);
            Assert.Equal(0, inspectProblemFollowUpPlanningIntake.ExitCode);
            Assert.Equal(0, apiProblemFollowUpPlanningIntake.ExitCode);
            Assert.Equal(0, inspectProblemFollowUpPlanningGate.ExitCode);
            Assert.Equal(0, apiProblemFollowUpPlanningGate.ExitCode);
            Assert.Equal(0, inspectInvocation.ExitCode);
            Assert.Equal(0, apiInvocation.ExitCode);
            Assert.Equal(0, inspectActivation.ExitCode);
            Assert.Equal(0, apiActivation.ExitCode);
            Assert.Equal(0, inspectDistSmoke.ExitCode);
            Assert.Equal(0, apiDistSmoke.ExitCode);
            Assert.Equal(0, inspectDistBinding.ExitCode);
            Assert.Equal(0, apiDistBinding.ExitCode);
            Assert.Equal(0, inspectTargetProof.ExitCode);
            Assert.Equal(0, apiTargetProof.ExitCode);
            Assert.Equal(0, guide.ExitCode);
            Assert.Equal(0, guideJson.ExitCode);
            Assert.Equal(0, inspect.ExitCode);
            Assert.Equal(0, api.ExitCode);
            Assert.Equal(0, handoff.ExitCode);
            Assert.Equal(statusBefore, statusAfter);

            Assert.Contains("Runtime product closure pilot status", pilotStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", pilotStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime document root mode: attach_handshake_runtime_root", pilotStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Current stage: 8 intent_capture", pilotStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Next command: carves discuss context", pilotStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Discussion-first surface: True", pilotStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Auto-run allowed: False", pilotStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Recommended action id: (none)", pilotStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("view_pilot_status | read_only", pilotStatus.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime target commit hygiene", commitHygiene.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", commitHygiene.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Git repository detected: True", commitHygiene.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Commit candidate paths:", commitHygiene.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime target commit plan", commitPlan.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", commitPlan.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Git add command preview:", commitPlan.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime target commit closure", commitClosure.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", commitClosure.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime target residue policy", residuePolicy.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", residuePolicy.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime target ignore decision plan", ignoreDecisionPlan.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", ignoreDecisionPlan.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime target ignore decision record", ignoreDecisionRecord.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime target ignore decision record", inspectIgnoreDecisionRecord.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", ignoreDecisionRecord.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Decision record commit ready:", ignoreDecisionRecord.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime local dist handoff", localDist.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Overall posture: local_dist_handoff_live_source_attached", localDist.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime product pilot proof", productPilotProof.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Overall posture: product_pilot_proof_waiting_for_local_dist_freshness_smoke", productPilotProof.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime external consumer resource pack", resources.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Overall posture: external_consumer_resource_pack_ready", resources.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime external target pilot start", pilotStart.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", pilotStart.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime document root mode: attach_handshake_runtime_root", pilotStart.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Next governed command: carves discuss context", pilotStart.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Legacy next command projection only: True", pilotStart.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Preferred action source: available_actions", pilotStart.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Discussion-first surface: True", pilotStart.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("project_brief_preview | preview", pilotStart.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("project_brief_preview | preview | Project brief preview | carves discuss brief-preview", pilotStart.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime external target pilot next", pilotNext.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Next governed command: carves discuss context", pilotNext.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Discussion-first surface: True", pilotNext.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime agent problem intake", problemIntake.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", problemIntake.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Problem intake ready: True", problemIntake.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("carves pilot report-problem <json-path>", problemIntake.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime agent problem triage ledger", problemTriage.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime agent problem triage ledger", inspectProblemTriage.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", problemTriage.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Triage ledger ready: True", problemTriage.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime agent problem follow-up candidates", problemFollowUp.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime agent problem follow-up candidates", inspectProblemFollowUp.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", problemFollowUp.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Follow-up candidates ready: True", problemFollowUp.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime agent problem follow-up decision plan", problemFollowUpDecisionPlan.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime agent problem follow-up decision plan", inspectProblemFollowUpDecisionPlan.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", problemFollowUpDecisionPlan.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Decision plan ready: True", problemFollowUpDecisionPlan.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime agent problem follow-up decision record", problemFollowUpDecisionRecord.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime agent problem follow-up decision record", inspectProblemFollowUpDecisionRecord.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", problemFollowUpDecisionRecord.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Decision record ready: True", problemFollowUpDecisionRecord.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("carves pilot follow-up-record --json", problemFollowUpDecisionRecord.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime agent problem follow-up planning intake", problemFollowUpPlanningIntake.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime agent problem follow-up planning intake", inspectProblemFollowUpPlanningIntake.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", problemFollowUpPlanningIntake.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Planning intake ready: True", problemFollowUpPlanningIntake.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("carves pilot follow-up-intake --json", problemFollowUpPlanningIntake.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime agent problem follow-up planning gate", problemFollowUpPlanningGate.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime agent problem follow-up planning gate", inspectProblemFollowUpPlanningGate.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", problemFollowUpPlanningGate.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Planning gate ready: True", problemFollowUpPlanningGate.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("carves pilot follow-up-gate --json", problemFollowUpPlanningGate.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime alpha external-use readiness", readiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime alpha external-use readiness", alpha.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime alpha external-use readiness", inspectReadiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", readiness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime CLI invocation contract", invocation.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Overall posture: cli_invocation_contract_ready", invocation.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime CLI activation plan", activation.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime CLI activation plan", alias.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Overall posture: cli_activation_plan_ready", activation.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime local dist freshness smoke", distSmoke.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime local dist freshness smoke", distFreshness.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime local dist freshness smoke", inspectDistSmoke.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime target dist binding plan", distBinding.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime target dist binding plan", bindDist.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime target dist binding plan", inspectDistBinding.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Overall posture: target_dist_binding_plan_blocked_by_missing_dist_resources", distBinding.StandardOutput, StringComparison.Ordinal);

            Assert.Contains("Runtime product closure pilot guide", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Product closure phase: phase_40_agent_problem_follow_up_planning_gate_ready", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime document root mode: attach_handshake_runtime_root", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Status command entry: carves pilot status --json", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stage 0: external_agent_thread_start", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("carves agent start --json", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stage 1: attach_target", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stage 3: target_agent_bootstrap", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stage 4: cli_invocation_contract", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stage 5: cli_activation_plan", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stage 6: external_consumer_resource_pack", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stage 12: workspace_submit", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stage 14: review_writeback", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stage 16: target_commit_plan", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stage 17: target_commit_closure", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stage 18: target_residue_policy", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stage 19: target_ignore_decision_plan", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stage 20: target_ignore_decision_record", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stage 21: local_dist_freshness_smoke", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stage 22: target_dist_binding_plan", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stage 23: local_dist_handoff", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stage 24: frozen_dist_target_readback_proof", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stage 25: product_pilot_proof", guide.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("does not create a project, card, task, workspace, review, or commit by itself", guide.StandardOutput, StringComparison.Ordinal);

            using var statusDocument = JsonDocument.Parse(pilotStatusJson.StandardOutput);
            var statusRoot = statusDocument.RootElement;
            Assert.Equal("runtime-product-closure-pilot-status", statusRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", statusRoot.GetProperty("product_closure_phase").GetString());
            Assert.Equal("pilot_status_intent_capture_required", statusRoot.GetProperty("overall_posture").GetString());
            Assert.Equal("intent_capture", statusRoot.GetProperty("current_stage_id").GetString());
            Assert.Equal("carves discuss context", statusRoot.GetProperty("next_command").GetString());
            Assert.True(statusRoot.GetProperty("legacy_next_command_projection_only").GetBoolean());
            Assert.True(statusRoot.GetProperty("legacy_next_command_do_not_auto_run").GetBoolean());
            Assert.Equal("available_actions", statusRoot.GetProperty("preferred_action_source").GetString());
            Assert.True(statusRoot.GetProperty("discussion_first_surface").GetBoolean());
            Assert.False(statusRoot.GetProperty("auto_run_allowed").GetBoolean());
            Assert.True(statusRoot.GetProperty("recommended_action_id").ValueKind == JsonValueKind.Null);
            Assert.Equal(4, statusRoot.GetProperty("available_actions").GetArrayLength());
            Assert.Contains(statusRoot.GetProperty("available_actions").EnumerateArray(), action =>
                action.GetProperty("action_id").GetString() == "continue_discussion"
                && action.GetProperty("kind").GetString() == "read_only");
            Assert.Contains(statusRoot.GetProperty("available_actions").EnumerateArray(), action =>
                action.GetProperty("action_id").GetString() == "project_brief_preview"
                && action.GetProperty("kind").GetString() == "preview"
                && action.GetProperty("command").GetString() == "carves discuss brief-preview");
            Assert.Contains(statusRoot.GetProperty("forbidden_auto_actions").EnumerateArray(), action =>
                action.GetString() == "carves intent draft --persist");
            Assert.Equal("attach_handshake_runtime_root", statusRoot.GetProperty("runtime_document_root_mode").GetString());
            Assert.Equal("local_dist_handoff_live_source_attached", statusRoot.GetProperty("local_dist_handoff_posture").GetString());
            Assert.False(statusRoot.GetProperty("local_dist_freshness_smoke_ready").GetBoolean());
            Assert.False(statusRoot.GetProperty("frozen_dist_target_readback_proof_complete").GetBoolean());
            Assert.False(statusRoot.GetProperty("stable_external_consumption_ready").GetBoolean());

            using var preflightDocument = JsonDocument.Parse(pilotPreflightJson.StandardOutput);
            Assert.Equal("runtime-product-closure-pilot-status", preflightDocument.RootElement.GetProperty("surface_id").GetString());

            using var statusApiDocument = JsonDocument.Parse(apiStatus.StandardOutput);
            Assert.Equal("runtime-product-closure-pilot-status", statusApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var commitHygieneDocument = JsonDocument.Parse(commitHygieneJson.StandardOutput);
            var commitHygieneRoot = commitHygieneDocument.RootElement;
            Assert.Equal("runtime-target-commit-hygiene", commitHygieneRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", commitHygieneRoot.GetProperty("product_closure_phase").GetString());
            Assert.True(commitHygieneRoot.GetProperty("git_repository_detected").GetBoolean());
            Assert.True(commitHygieneRoot.GetProperty("runtime_initialized").GetBoolean());
            Assert.Contains(commitHygieneRoot.GetProperty("dirty_paths").EnumerateArray(), path =>
                path.GetProperty("path_class").GetString() == "official_target_truth");

            using var commitHygieneApiDocument = JsonDocument.Parse(apiCommitHygiene.StandardOutput);
            Assert.Equal("runtime-target-commit-hygiene", commitHygieneApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var commitPlanDocument = JsonDocument.Parse(commitPlanJson.StandardOutput);
            var commitPlanRoot = commitPlanDocument.RootElement;
            Assert.Equal("runtime-target-commit-plan", commitPlanRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", commitPlanRoot.GetProperty("product_closure_phase").GetString());
            Assert.True(commitPlanRoot.GetProperty("git_repository_detected").GetBoolean());
            Assert.True(commitPlanRoot.GetProperty("runtime_initialized").GetBoolean());
            Assert.True(commitPlanRoot.GetProperty("stage_path_count").GetInt32() > 0);
            Assert.Contains("git add --", commitPlanRoot.GetProperty("git_add_command_preview").GetString(), StringComparison.Ordinal);

            using var commitPlanApiDocument = JsonDocument.Parse(apiCommitPlan.StandardOutput);
            Assert.Equal("runtime-target-commit-plan", commitPlanApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var commitClosureDocument = JsonDocument.Parse(commitClosureJson.StandardOutput);
            var commitClosureRoot = commitClosureDocument.RootElement;
            Assert.Equal("runtime-target-commit-closure", commitClosureRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", commitClosureRoot.GetProperty("product_closure_phase").GetString());
            Assert.False(commitClosureRoot.GetProperty("commit_closure_complete").GetBoolean());

            using var commitClosureApiDocument = JsonDocument.Parse(apiCommitClosure.StandardOutput);
            Assert.Equal("runtime-target-commit-closure", commitClosureApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var residuePolicyDocument = JsonDocument.Parse(residuePolicyJson.StandardOutput);
            var residuePolicyRoot = residuePolicyDocument.RootElement;
            Assert.Equal("runtime-target-residue-policy", residuePolicyRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", residuePolicyRoot.GetProperty("product_closure_phase").GetString());
            Assert.False(residuePolicyRoot.GetProperty("residue_policy_ready").GetBoolean());

            using var residuePolicyApiDocument = JsonDocument.Parse(apiResiduePolicy.StandardOutput);
            Assert.Equal("runtime-target-residue-policy", residuePolicyApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var ignoreDecisionPlanDocument = JsonDocument.Parse(ignoreDecisionPlanJson.StandardOutput);
            var ignoreDecisionPlanRoot = ignoreDecisionPlanDocument.RootElement;
            Assert.Equal("runtime-target-ignore-decision-plan", ignoreDecisionPlanRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", ignoreDecisionPlanRoot.GetProperty("product_closure_phase").GetString());
            Assert.False(ignoreDecisionPlanRoot.GetProperty("ignore_decision_plan_ready").GetBoolean());

            using var ignoreDecisionPlanApiDocument = JsonDocument.Parse(apiIgnoreDecisionPlan.StandardOutput);
            Assert.Equal("runtime-target-ignore-decision-plan", ignoreDecisionPlanApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var ignoreDecisionRecordDocument = JsonDocument.Parse(ignoreDecisionRecordJson.StandardOutput);
            var ignoreDecisionRecordRoot = ignoreDecisionRecordDocument.RootElement;
            Assert.Equal("runtime-target-ignore-decision-record", ignoreDecisionRecordRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", ignoreDecisionRecordRoot.GetProperty("product_closure_phase").GetString());
            Assert.True(ignoreDecisionRecordRoot.TryGetProperty("record_audit_ready", out _));
            Assert.True(ignoreDecisionRecordRoot.TryGetProperty("decision_record_commit_ready", out _));

            using var ignoreDecisionRecordApiDocument = JsonDocument.Parse(apiIgnoreDecisionRecord.StandardOutput);
            Assert.Equal("runtime-target-ignore-decision-record", ignoreDecisionRecordApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var localDistDocument = JsonDocument.Parse(localDistJson.StandardOutput);
            var localDistRoot = localDistDocument.RootElement;
            Assert.Equal("runtime-local-dist-handoff", localDistRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", localDistRoot.GetProperty("product_closure_phase").GetString());
            Assert.Equal("local_dist_handoff_live_source_attached", localDistRoot.GetProperty("overall_posture").GetString());
            Assert.Equal("source_tree", localDistRoot.GetProperty("runtime_root_kind").GetString());
            Assert.False(localDistRoot.GetProperty("stable_external_consumption_ready").GetBoolean());

            using var localDistApiDocument = JsonDocument.Parse(apiLocalDist.StandardOutput);
            Assert.Equal("runtime-local-dist-handoff", localDistApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var productPilotProofDocument = JsonDocument.Parse(productPilotProofJson.StandardOutput);
            var productPilotProofRoot = productPilotProofDocument.RootElement;
            Assert.Equal("runtime-product-pilot-proof", productPilotProofRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", productPilotProofRoot.GetProperty("product_closure_phase").GetString());
            Assert.Equal("product_pilot_proof_waiting_for_local_dist_freshness_smoke", productPilotProofRoot.GetProperty("overall_posture").GetString());
            Assert.True(productPilotProofRoot.TryGetProperty("target_ignore_decision_record_audit_ready", out _));
            Assert.True(productPilotProofRoot.TryGetProperty("target_ignore_decision_record_commit_ready", out _));
            Assert.False(productPilotProofRoot.GetProperty("local_dist_freshness_smoke_ready").GetBoolean());
            Assert.False(productPilotProofRoot.GetProperty("frozen_dist_target_readback_proof_complete").GetBoolean());
            Assert.False(productPilotProofRoot.GetProperty("product_pilot_proof_complete").GetBoolean());

            using var productPilotProofApiDocument = JsonDocument.Parse(apiProductPilotProof.StandardOutput);
            Assert.Equal("runtime-product-pilot-proof", productPilotProofApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var resourcesDocument = JsonDocument.Parse(resourcesJson.StandardOutput);
            var resourcesRoot = resourcesDocument.RootElement;
            Assert.Equal("runtime-external-consumer-resource-pack", resourcesRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", resourcesRoot.GetProperty("product_closure_phase").GetString());
            Assert.Equal("external_consumer_resource_pack_ready", resourcesRoot.GetProperty("overall_posture").GetString());
            Assert.True(resourcesRoot.GetProperty("resource_pack_complete").GetBoolean());
            Assert.Contains(resourcesRoot.GetProperty("command_entries").EnumerateArray(), entry =>
                entry.GetProperty("surface_id").GetString() == "runtime-cli-invocation-contract");
            Assert.Contains(resourcesRoot.GetProperty("command_entries").EnumerateArray(), entry =>
                entry.GetProperty("surface_id").GetString() == "runtime-cli-activation-plan");
            Assert.Contains(resourcesRoot.GetProperty("command_entries").EnumerateArray(), entry =>
                entry.GetProperty("surface_id").GetString() == "runtime-local-dist-freshness-smoke");
            Assert.Contains(resourcesRoot.GetProperty("command_entries").EnumerateArray(), entry =>
                entry.GetProperty("surface_id").GetString() == "runtime-alpha-external-use-readiness");
            Assert.Contains(resourcesRoot.GetProperty("command_entries").EnumerateArray(), entry =>
                entry.GetProperty("surface_id").GetString() == "runtime-target-dist-binding-plan");
            Assert.Contains(resourcesRoot.GetProperty("command_entries").EnumerateArray(), entry =>
                entry.GetProperty("surface_id").GetString() == "runtime-frozen-dist-target-readback-proof");
            Assert.Contains(resourcesRoot.GetProperty("command_entries").EnumerateArray(), entry =>
                entry.GetProperty("surface_id").GetString() == "runtime-target-residue-policy");
            Assert.Contains(resourcesRoot.GetProperty("command_entries").EnumerateArray(), entry =>
                entry.GetProperty("surface_id").GetString() == "runtime-target-ignore-decision-plan");
            Assert.Contains(resourcesRoot.GetProperty("command_entries").EnumerateArray(), entry =>
                entry.GetProperty("surface_id").GetString() == "runtime-target-ignore-decision-record");
            Assert.Contains(resourcesRoot.GetProperty("command_entries").EnumerateArray(), entry =>
                entry.GetProperty("surface_id").GetString() == "runtime-agent-problem-intake");
            Assert.Contains(resourcesRoot.GetProperty("command_entries").EnumerateArray(), entry =>
                entry.GetProperty("surface_id").GetString() == "runtime-agent-problem-triage-ledger");
            Assert.Contains(resourcesRoot.GetProperty("command_entries").EnumerateArray(), entry =>
                entry.GetProperty("surface_id").GetString() == "runtime-agent-problem-follow-up-candidates");
            Assert.Contains(resourcesRoot.GetProperty("command_entries").EnumerateArray(), entry =>
                entry.GetProperty("surface_id").GetString() == "runtime-agent-problem-follow-up-decision-plan"
                && entry.GetProperty("command").GetString() == "carves pilot follow-up-plan --json");
            Assert.Contains(resourcesRoot.GetProperty("command_entries").EnumerateArray(), entry =>
                entry.GetProperty("surface_id").GetString() == "runtime-agent-problem-follow-up-decision-record"
                && entry.GetProperty("command").GetString() == "carves pilot follow-up-record --json");
            Assert.Contains(resourcesRoot.GetProperty("command_entries").EnumerateArray(), entry =>
                entry.GetProperty("surface_id").GetString() == "runtime-agent-problem-follow-up-decision-record"
                && entry.GetProperty("command").GetString() == "carves pilot record-follow-up-decision <decision> --all --reason <text>");
            Assert.Contains(resourcesRoot.GetProperty("command_entries").EnumerateArray(), entry =>
                entry.GetProperty("surface_id").GetString() == "runtime-agent-problem-follow-up-planning-intake"
                && entry.GetProperty("command").GetString() == "carves pilot follow-up-intake --json");
            Assert.Contains(resourcesRoot.GetProperty("command_entries").EnumerateArray(), entry =>
                entry.GetProperty("surface_id").GetString() == "runtime-agent-problem-follow-up-planning-gate"
                && entry.GetProperty("command").GetString() == "carves pilot follow-up-gate --json");
            Assert.Contains(resourcesRoot.GetProperty("runtime_owned_resources").EnumerateArray(), resource =>
                resource.GetProperty("path").GetString() == "docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_PLAN.md");
            Assert.Contains(resourcesRoot.GetProperty("runtime_owned_resources").EnumerateArray(), resource =>
                resource.GetProperty("path").GetString() == "docs/runtime/carves-product-closure-phase-37-agent-problem-follow-up-decision-plan.md");
            Assert.Contains(resourcesRoot.GetProperty("runtime_owned_resources").EnumerateArray(), resource =>
                resource.GetProperty("path").GetString() == "docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_RECORD.md");
            Assert.Contains(resourcesRoot.GetProperty("runtime_owned_resources").EnumerateArray(), resource =>
                resource.GetProperty("path").GetString() == "docs/runtime/carves-product-closure-phase-38-agent-problem-follow-up-decision-record.md");
            Assert.Contains(resourcesRoot.GetProperty("runtime_owned_resources").EnumerateArray(), resource =>
                resource.GetProperty("path").GetString() == "docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_INTAKE.md");
            Assert.Contains(resourcesRoot.GetProperty("runtime_owned_resources").EnumerateArray(), resource =>
                resource.GetProperty("path").GetString() == "docs/runtime/carves-product-closure-phase-39-agent-problem-follow-up-planning-intake.md");
            Assert.Contains(resourcesRoot.GetProperty("runtime_owned_resources").EnumerateArray(), resource =>
                resource.GetProperty("path").GetString() == "docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_GATE.md");
            Assert.Contains(resourcesRoot.GetProperty("runtime_owned_resources").EnumerateArray(), resource =>
                resource.GetProperty("path").GetString() == "docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md");
            Assert.Contains(resourcesRoot.GetProperty("required_readback_commands").EnumerateArray(), command =>
                command.GetString() == "carves pilot follow-up-plan --json");
            Assert.Contains(resourcesRoot.GetProperty("required_readback_commands").EnumerateArray(), command =>
                command.GetString() == "carves pilot follow-up-record --json");
            Assert.Contains(resourcesRoot.GetProperty("required_readback_commands").EnumerateArray(), command =>
                command.GetString() == "carves pilot follow-up-intake --json");
            Assert.Contains(resourcesRoot.GetProperty("required_readback_commands").EnumerateArray(), command =>
                command.GetString() == "carves pilot follow-up-gate --json");

            using var resourcesApiDocument = JsonDocument.Parse(apiResources.StandardOutput);
            Assert.Equal("runtime-external-consumer-resource-pack", resourcesApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var pilotStartDocument = JsonDocument.Parse(pilotStartJson.StandardOutput);
            var pilotStartRoot = pilotStartDocument.RootElement;
            Assert.Equal("runtime-external-target-pilot-start", pilotStartRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", pilotStartRoot.GetProperty("product_closure_phase").GetString());
            Assert.Equal("attach_handshake_runtime_root", pilotStartRoot.GetProperty("runtime_document_root_mode").GetString());
            Assert.Equal("intent_capture", pilotStartRoot.GetProperty("current_stage_id").GetString());
            Assert.Equal("carves discuss context", pilotStartRoot.GetProperty("next_governed_command").GetString());
            Assert.True(pilotStartRoot.GetProperty("legacy_next_command_projection_only").GetBoolean());
            Assert.True(pilotStartRoot.GetProperty("legacy_next_command_do_not_auto_run").GetBoolean());
            Assert.Equal("available_actions", pilotStartRoot.GetProperty("preferred_action_source").GetString());
            Assert.True(pilotStartRoot.GetProperty("discussion_first_surface").GetBoolean());
            Assert.False(pilotStartRoot.GetProperty("auto_run_allowed").GetBoolean());
            Assert.True(pilotStartRoot.GetProperty("recommended_action_id").ValueKind == JsonValueKind.Null);
            Assert.Equal(4, pilotStartRoot.GetProperty("available_actions").GetArrayLength());
            Assert.Contains(pilotStartRoot.GetProperty("available_actions").EnumerateArray(), action =>
                action.GetProperty("action_id").GetString() == "project_brief_preview"
                && action.GetProperty("command").GetString() == "carves discuss brief-preview");
            Assert.True(pilotStartRoot.GetProperty("target_ready_for_formal_planning").GetBoolean());
            Assert.False(pilotStartRoot.GetProperty("pilot_start_bundle_ready").GetBoolean());

            using var pilotStartApiDocument = JsonDocument.Parse(apiPilotStart.StandardOutput);
            Assert.Equal("runtime-external-target-pilot-start", pilotStartApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var pilotNextDocument = JsonDocument.Parse(pilotNextJson.StandardOutput);
            var pilotNextRoot = pilotNextDocument.RootElement;
            Assert.Equal("runtime-external-target-pilot-next", pilotNextRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", pilotNextRoot.GetProperty("product_closure_phase").GetString());
            Assert.Equal("carves discuss context", pilotNextRoot.GetProperty("next_governed_command").GetString());
            Assert.True(pilotNextRoot.GetProperty("legacy_next_command_projection_only").GetBoolean());
            Assert.True(pilotNextRoot.GetProperty("legacy_next_command_do_not_auto_run").GetBoolean());
            Assert.Equal("available_actions", pilotNextRoot.GetProperty("preferred_action_source").GetString());
            Assert.True(pilotNextRoot.GetProperty("discussion_first_surface").GetBoolean());
            Assert.False(pilotNextRoot.GetProperty("auto_run_allowed").GetBoolean());
            Assert.True(pilotNextRoot.GetProperty("recommended_action_id").ValueKind == JsonValueKind.Null);
            Assert.Equal(4, pilotNextRoot.GetProperty("available_actions").GetArrayLength());
            Assert.Contains(pilotNextRoot.GetProperty("available_actions").EnumerateArray(), action =>
                action.GetProperty("action_id").GetString() == "project_brief_preview"
                && action.GetProperty("command").GetString() == "carves discuss brief-preview");
            Assert.False(pilotNextRoot.GetProperty("ready_to_run_next_command").GetBoolean());

            using var pilotNextApiDocument = JsonDocument.Parse(apiPilotNext.StandardOutput);
            Assert.Equal("runtime-external-target-pilot-next", pilotNextApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var problemIntakeDocument = JsonDocument.Parse(problemIntakeJson.StandardOutput);
            var problemIntakeRoot = problemIntakeDocument.RootElement;
            Assert.Equal("runtime-agent-problem-intake", problemIntakeRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", problemIntakeRoot.GetProperty("product_closure_phase").GetString());
            Assert.Equal("attach_handshake_runtime_root", problemIntakeRoot.GetProperty("runtime_document_root_mode").GetString());
            Assert.True(problemIntakeRoot.GetProperty("problem_intake_ready").GetBoolean());
            Assert.Contains(problemIntakeRoot.GetProperty("accepted_problem_kinds").EnumerateArray(), kind =>
                kind.GetString() == "blocked_posture");

            using var problemIntakeApiDocument = JsonDocument.Parse(apiProblemIntake.StandardOutput);
            Assert.Equal("runtime-agent-problem-intake", problemIntakeApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var problemTriageDocument = JsonDocument.Parse(problemTriageJson.StandardOutput);
            var problemTriageRoot = problemTriageDocument.RootElement;
            Assert.Equal("runtime-agent-problem-triage-ledger", problemTriageRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", problemTriageRoot.GetProperty("product_closure_phase").GetString());
            Assert.Equal("attach_handshake_runtime_root", problemTriageRoot.GetProperty("runtime_document_root_mode").GetString());
            Assert.True(problemTriageRoot.GetProperty("triage_ledger_ready").GetBoolean());
            Assert.Equal("carves pilot triage --json", problemTriageRoot.GetProperty("json_command_entry").GetString());

            using var problemTriageAliasDocument = JsonDocument.Parse(problemTriageAliasJson.StandardOutput);
            Assert.Equal("runtime-agent-problem-triage-ledger", problemTriageAliasDocument.RootElement.GetProperty("surface_id").GetString());

            using var problemTriageApiDocument = JsonDocument.Parse(apiProblemTriage.StandardOutput);
            Assert.Equal("runtime-agent-problem-triage-ledger", problemTriageApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var problemFollowUpDocument = JsonDocument.Parse(problemFollowUpJson.StandardOutput);
            var problemFollowUpRoot = problemFollowUpDocument.RootElement;
            Assert.Equal("runtime-agent-problem-follow-up-candidates", problemFollowUpRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", problemFollowUpRoot.GetProperty("product_closure_phase").GetString());
            Assert.Equal("attach_handshake_runtime_root", problemFollowUpRoot.GetProperty("runtime_document_root_mode").GetString());
            Assert.True(problemFollowUpRoot.GetProperty("follow_up_candidates_ready").GetBoolean());
            Assert.Equal("carves pilot follow-up --json", problemFollowUpRoot.GetProperty("json_command_entry").GetString());

            using var problemFollowUpAliasDocument = JsonDocument.Parse(problemFollowUpAliasJson.StandardOutput);
            Assert.Equal("runtime-agent-problem-follow-up-candidates", problemFollowUpAliasDocument.RootElement.GetProperty("surface_id").GetString());

            using var problemFollowUpApiDocument = JsonDocument.Parse(apiProblemFollowUp.StandardOutput);
            Assert.Equal("runtime-agent-problem-follow-up-candidates", problemFollowUpApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var problemFollowUpDecisionPlanDocument = JsonDocument.Parse(problemFollowUpDecisionPlanJson.StandardOutput);
            var problemFollowUpDecisionPlanRoot = problemFollowUpDecisionPlanDocument.RootElement;
            Assert.Equal("runtime-agent-problem-follow-up-decision-plan", problemFollowUpDecisionPlanRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", problemFollowUpDecisionPlanRoot.GetProperty("product_closure_phase").GetString());
            Assert.Equal("attach_handshake_runtime_root", problemFollowUpDecisionPlanRoot.GetProperty("runtime_document_root_mode").GetString());
            Assert.Equal("agent_problem_follow_up_decision_plan_empty", problemFollowUpDecisionPlanRoot.GetProperty("overall_posture").GetString());
            Assert.True(problemFollowUpDecisionPlanRoot.GetProperty("decision_plan_ready").GetBoolean());
            Assert.False(problemFollowUpDecisionPlanRoot.GetProperty("decision_required").GetBoolean());
            Assert.Equal("carves pilot follow-up-plan --json", problemFollowUpDecisionPlanRoot.GetProperty("json_command_entry").GetString());
            Assert.Equal("carves inspect runtime-agent-problem-follow-up-decision-plan", problemFollowUpDecisionPlanRoot.GetProperty("inspect_command_entry").GetString());

            using var problemFollowUpDecisionPlanAliasDocument = JsonDocument.Parse(problemFollowUpDecisionPlanAliasJson.StandardOutput);
            Assert.Equal("runtime-agent-problem-follow-up-decision-plan", problemFollowUpDecisionPlanAliasDocument.RootElement.GetProperty("surface_id").GetString());

            using var problemTriageFollowUpPlanAliasDocument = JsonDocument.Parse(problemTriageFollowUpPlanAliasJson.StandardOutput);
            Assert.Equal("runtime-agent-problem-follow-up-decision-plan", problemTriageFollowUpPlanAliasDocument.RootElement.GetProperty("surface_id").GetString());

            using var problemFollowUpDecisionPlanApiDocument = JsonDocument.Parse(apiProblemFollowUpDecisionPlan.StandardOutput);
            Assert.Equal("runtime-agent-problem-follow-up-decision-plan", problemFollowUpDecisionPlanApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var problemFollowUpDecisionRecordDocument = JsonDocument.Parse(problemFollowUpDecisionRecordJson.StandardOutput);
            var problemFollowUpDecisionRecordRoot = problemFollowUpDecisionRecordDocument.RootElement;
            Assert.Equal("runtime-agent-problem-follow-up-decision-record", problemFollowUpDecisionRecordRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", problemFollowUpDecisionRecordRoot.GetProperty("product_closure_phase").GetString());
            Assert.Equal("attach_handshake_runtime_root", problemFollowUpDecisionRecordRoot.GetProperty("runtime_document_root_mode").GetString());
            Assert.Equal("agent_problem_follow_up_decision_record_no_decision_required", problemFollowUpDecisionRecordRoot.GetProperty("overall_posture").GetString());
            Assert.True(problemFollowUpDecisionRecordRoot.GetProperty("decision_plan_ready").GetBoolean());
            Assert.False(problemFollowUpDecisionRecordRoot.GetProperty("decision_required").GetBoolean());
            Assert.True(problemFollowUpDecisionRecordRoot.GetProperty("decision_record_ready").GetBoolean());
            Assert.Equal("carves pilot follow-up-record --json", problemFollowUpDecisionRecordRoot.GetProperty("json_command_entry").GetString());
            Assert.Equal("carves inspect runtime-agent-problem-follow-up-decision-record", problemFollowUpDecisionRecordRoot.GetProperty("inspect_command_entry").GetString());

            using var problemFollowUpDecisionRecordAliasDocument = JsonDocument.Parse(problemFollowUpDecisionRecordAliasJson.StandardOutput);
            Assert.Equal("runtime-agent-problem-follow-up-decision-record", problemFollowUpDecisionRecordAliasDocument.RootElement.GetProperty("surface_id").GetString());

            using var problemFollowUpRecordAliasDocument = JsonDocument.Parse(problemFollowUpRecordAliasJson.StandardOutput);
            Assert.Equal("runtime-agent-problem-follow-up-decision-record", problemFollowUpRecordAliasDocument.RootElement.GetProperty("surface_id").GetString());

            using var problemFollowUpDecisionRecordApiDocument = JsonDocument.Parse(apiProblemFollowUpDecisionRecord.StandardOutput);
            Assert.Equal("runtime-agent-problem-follow-up-decision-record", problemFollowUpDecisionRecordApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var problemFollowUpPlanningIntakeDocument = JsonDocument.Parse(problemFollowUpPlanningIntakeJson.StandardOutput);
            var problemFollowUpPlanningIntakeRoot = problemFollowUpPlanningIntakeDocument.RootElement;
            Assert.Equal("runtime-agent-problem-follow-up-planning-intake", problemFollowUpPlanningIntakeRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", problemFollowUpPlanningIntakeRoot.GetProperty("product_closure_phase").GetString());
            Assert.Equal("attach_handshake_runtime_root", problemFollowUpPlanningIntakeRoot.GetProperty("runtime_document_root_mode").GetString());
            Assert.Equal("agent_problem_follow_up_planning_intake_no_accepted_records", problemFollowUpPlanningIntakeRoot.GetProperty("overall_posture").GetString());
            Assert.True(problemFollowUpPlanningIntakeRoot.GetProperty("decision_record_ready").GetBoolean());
            Assert.True(problemFollowUpPlanningIntakeRoot.GetProperty("planning_intake_ready").GetBoolean());
            Assert.Equal(0, problemFollowUpPlanningIntakeRoot.GetProperty("accepted_decision_record_count").GetInt32());
            Assert.Equal(0, problemFollowUpPlanningIntakeRoot.GetProperty("accepted_planning_item_count").GetInt32());
            Assert.Equal("carves pilot follow-up-intake --json", problemFollowUpPlanningIntakeRoot.GetProperty("json_command_entry").GetString());
            Assert.Equal("carves inspect runtime-agent-problem-follow-up-planning-intake", problemFollowUpPlanningIntakeRoot.GetProperty("inspect_command_entry").GetString());

            using var problemFollowUpPlanningAliasDocument = JsonDocument.Parse(problemFollowUpPlanningAliasJson.StandardOutput);
            Assert.Equal("runtime-agent-problem-follow-up-planning-intake", problemFollowUpPlanningAliasDocument.RootElement.GetProperty("surface_id").GetString());

            using var problemFollowUpIntakeAliasDocument = JsonDocument.Parse(problemFollowUpIntakeAliasJson.StandardOutput);
            Assert.Equal("runtime-agent-problem-follow-up-planning-intake", problemFollowUpIntakeAliasDocument.RootElement.GetProperty("surface_id").GetString());

            using var problemFollowUpPlanningIntakeApiDocument = JsonDocument.Parse(apiProblemFollowUpPlanningIntake.StandardOutput);
            Assert.Equal("runtime-agent-problem-follow-up-planning-intake", problemFollowUpPlanningIntakeApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var problemFollowUpPlanningGateDocument = JsonDocument.Parse(problemFollowUpPlanningGateJson.StandardOutput);
            var problemFollowUpPlanningGateRoot = problemFollowUpPlanningGateDocument.RootElement;
            Assert.Equal("runtime-agent-problem-follow-up-planning-gate", problemFollowUpPlanningGateRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", problemFollowUpPlanningGateRoot.GetProperty("product_closure_phase").GetString());
            Assert.Equal("attach_handshake_runtime_root", problemFollowUpPlanningGateRoot.GetProperty("runtime_document_root_mode").GetString());
            Assert.Equal("agent_problem_follow_up_planning_gate_no_accepted_records", problemFollowUpPlanningGateRoot.GetProperty("overall_posture").GetString());
            Assert.True(problemFollowUpPlanningGateRoot.GetProperty("planning_intake_ready").GetBoolean());
            Assert.True(problemFollowUpPlanningGateRoot.GetProperty("planning_gate_ready").GetBoolean());
            Assert.Equal(0, problemFollowUpPlanningGateRoot.GetProperty("accepted_planning_item_count").GetInt32());
            Assert.Empty(problemFollowUpPlanningGateRoot.GetProperty("planning_gate_items").EnumerateArray());
            Assert.Equal("carves pilot follow-up-gate --json", problemFollowUpPlanningGateRoot.GetProperty("json_command_entry").GetString());
            Assert.Equal("carves inspect runtime-agent-problem-follow-up-planning-gate", problemFollowUpPlanningGateRoot.GetProperty("inspect_command_entry").GetString());

            using var problemFollowUpPlanningGateAliasDocument = JsonDocument.Parse(problemFollowUpPlanningGateAliasJson.StandardOutput);
            Assert.Equal("runtime-agent-problem-follow-up-planning-gate", problemFollowUpPlanningGateAliasDocument.RootElement.GetProperty("surface_id").GetString());

            using var problemFollowUpGateAliasDocument = JsonDocument.Parse(problemFollowUpGateAliasJson.StandardOutput);
            Assert.Equal("runtime-agent-problem-follow-up-planning-gate", problemFollowUpGateAliasDocument.RootElement.GetProperty("surface_id").GetString());

            using var problemFollowUpPlanningGateApiDocument = JsonDocument.Parse(apiProblemFollowUpPlanningGate.StandardOutput);
            Assert.Equal("runtime-agent-problem-follow-up-planning-gate", problemFollowUpPlanningGateApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var readinessDocument = JsonDocument.Parse(readinessJson.StandardOutput);
            var readinessRoot = readinessDocument.RootElement;
            Assert.Equal("runtime-alpha-external-use-readiness", readinessRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", readinessRoot.GetProperty("product_closure_phase").GetString());
            Assert.True(readinessRoot.GetProperty("product_pilot_proof_required_per_target").GetBoolean());
            Assert.Contains(readinessRoot.GetProperty("readiness_checks").EnumerateArray(), check =>
                check.GetProperty("check_id").GetString() == "target_product_pilot_proof"
                && !check.GetProperty("blocks_alpha_use").GetBoolean());
            Assert.Contains(readinessRoot.GetProperty("minimum_operator_readbacks").EnumerateArray(), command =>
                command.GetString() == "carves agent start --json");
            Assert.Contains(readinessRoot.GetProperty("minimum_operator_readbacks").EnumerateArray(), command =>
                command.GetString() == "carves pilot readiness --json");
            Assert.Contains(readinessRoot.GetProperty("minimum_operator_readbacks").EnumerateArray(), command =>
                command.GetString() == "carves pilot problem-intake --json");
            Assert.Contains(readinessRoot.GetProperty("minimum_operator_readbacks").EnumerateArray(), command =>
                command.GetString() == "carves pilot triage --json");
            Assert.Contains(readinessRoot.GetProperty("minimum_operator_readbacks").EnumerateArray(), command =>
                command.GetString() == "carves pilot follow-up --json");
            Assert.Contains(readinessRoot.GetProperty("minimum_operator_readbacks").EnumerateArray(), command =>
                command.GetString() == "carves pilot follow-up-plan --json");
            Assert.Contains(readinessRoot.GetProperty("minimum_operator_readbacks").EnumerateArray(), command =>
                command.GetString() == "carves pilot follow-up-record --json");
            Assert.Contains(readinessRoot.GetProperty("minimum_operator_readbacks").EnumerateArray(), command =>
                command.GetString() == "carves pilot follow-up-intake --json");
            Assert.Contains(readinessRoot.GetProperty("minimum_operator_readbacks").EnumerateArray(), command =>
                command.GetString() == "carves pilot follow-up-gate --json");

            using var alphaDocument = JsonDocument.Parse(alphaJson.StandardOutput);
            Assert.Equal("runtime-alpha-external-use-readiness", alphaDocument.RootElement.GetProperty("surface_id").GetString());

            using var readinessApiDocument = JsonDocument.Parse(apiReadiness.StandardOutput);
            Assert.Equal("runtime-alpha-external-use-readiness", readinessApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var invocationDocument = JsonDocument.Parse(invocationJson.StandardOutput);
            var invocationRoot = invocationDocument.RootElement;
            Assert.Equal("runtime-cli-invocation-contract", invocationRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", invocationRoot.GetProperty("product_closure_phase").GetString());
            Assert.Equal("cli_invocation_contract_ready", invocationRoot.GetProperty("overall_posture").GetString());
            Assert.True(invocationRoot.GetProperty("invocation_contract_complete").GetBoolean());

            using var invocationApiDocument = JsonDocument.Parse(apiInvocation.StandardOutput);
            Assert.Equal("runtime-cli-invocation-contract", invocationApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var activationDocument = JsonDocument.Parse(activationJson.StandardOutput);
            var activationRoot = activationDocument.RootElement;
            Assert.Equal("runtime-cli-activation-plan", activationRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", activationRoot.GetProperty("product_closure_phase").GetString());
            Assert.Equal("cli_activation_plan_ready", activationRoot.GetProperty("overall_posture").GetString());
            Assert.True(activationRoot.GetProperty("activation_plan_complete").GetBoolean());
            Assert.Contains(activationRoot.GetProperty("activation_lanes").EnumerateArray(), lane =>
                lane.GetProperty("lane_id").GetString() == "absolute_wrapper");

            using var activationApiDocument = JsonDocument.Parse(apiActivation.StandardOutput);
            Assert.Equal("runtime-cli-activation-plan", activationApiDocument.RootElement.GetProperty("surface_id").GetString());

            using var aliasDocument = JsonDocument.Parse(aliasJson.StandardOutput);
            Assert.Equal("runtime-cli-activation-plan", aliasDocument.RootElement.GetProperty("surface_id").GetString());

            using var distSmokeDocument = JsonDocument.Parse(distSmokeJson.StandardOutput);
            var distSmokeRoot = distSmokeDocument.RootElement;
            Assert.Equal("runtime-local-dist-freshness-smoke", distSmokeRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", distSmokeRoot.GetProperty("product_closure_phase").GetString());
            Assert.False(distSmokeRoot.GetProperty("local_dist_freshness_smoke_ready").GetBoolean());

            using var distFreshnessDocument = JsonDocument.Parse(distFreshnessJson.StandardOutput);
            Assert.Equal("runtime-local-dist-freshness-smoke", distFreshnessDocument.RootElement.GetProperty("surface_id").GetString());

            using var apiDistSmokeDocument = JsonDocument.Parse(apiDistSmoke.StandardOutput);
            Assert.Equal("runtime-local-dist-freshness-smoke", apiDistSmokeDocument.RootElement.GetProperty("surface_id").GetString());

            using var distBindingDocument = JsonDocument.Parse(distBindingJson.StandardOutput);
            var distBindingRoot = distBindingDocument.RootElement;
            Assert.Equal("runtime-target-dist-binding-plan", distBindingRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", distBindingRoot.GetProperty("product_closure_phase").GetString());
            Assert.Equal("target_dist_binding_plan_blocked_by_missing_dist_resources", distBindingRoot.GetProperty("overall_posture").GetString());
            Assert.False(distBindingRoot.GetProperty("dist_binding_plan_complete").GetBoolean());
            Assert.True(distBindingRoot.GetProperty("target_bound_to_live_source").GetBoolean());
            Assert.Contains(distBindingRoot.GetProperty("required_readback_commands").EnumerateArray(), readback =>
                readback.GetString() == "carves pilot dist-smoke --json");
            Assert.Contains(distBindingRoot.GetProperty("required_readback_commands").EnumerateArray(), readback =>
                readback.GetString() == "carves pilot target-proof --json");
            Assert.Contains(distBindingRoot.GetProperty("gaps").EnumerateArray(), gap =>
                gap.GetString() == "candidate_dist_root_missing");

            using var bindDistDocument = JsonDocument.Parse(bindDistJson.StandardOutput);
            Assert.Equal("runtime-target-dist-binding-plan", bindDistDocument.RootElement.GetProperty("surface_id").GetString());

            using var apiDistBindingDocument = JsonDocument.Parse(apiDistBinding.StandardOutput);
            Assert.Equal("runtime-target-dist-binding-plan", apiDistBindingDocument.RootElement.GetProperty("surface_id").GetString());

            Assert.Contains("Runtime frozen dist target readback proof", targetProof.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime frozen dist target readback proof", externalProof.StandardOutput, StringComparison.Ordinal);

            using var targetProofDocument = JsonDocument.Parse(targetProofJson.StandardOutput);
            var targetProofRoot = targetProofDocument.RootElement;
            Assert.Equal("runtime-frozen-dist-target-readback-proof", targetProofRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", targetProofRoot.GetProperty("product_closure_phase").GetString());
            Assert.False(targetProofRoot.GetProperty("frozen_dist_target_readback_proof_complete").GetBoolean());

            using var externalProofDocument = JsonDocument.Parse(externalProofJson.StandardOutput);
            Assert.Equal("runtime-frozen-dist-target-readback-proof", externalProofDocument.RootElement.GetProperty("surface_id").GetString());

            using var inspectTargetProofDocument = JsonDocument.Parse(apiTargetProof.StandardOutput);
            Assert.Equal("runtime-frozen-dist-target-readback-proof", inspectTargetProofDocument.RootElement.GetProperty("surface_id").GetString());

            using var guideDocument = JsonDocument.Parse(guideJson.StandardOutput);
            var guideRoot = guideDocument.RootElement;
            Assert.Equal("runtime-product-closure-pilot-guide", guideRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", guideRoot.GetProperty("product_closure_phase").GetString());
            Assert.Equal("productized_pilot_guide_ready", guideRoot.GetProperty("overall_posture").GetString());
            Assert.Equal("read_only_productized_pilot_guide", guideRoot.GetProperty("authority_model").GetString());
            Assert.Equal("carves pilot status --json", guideRoot.GetProperty("status_command_entry").GetString());
            Assert.Equal("attach_handshake_runtime_root", guideRoot.GetProperty("runtime_document_root_mode").GetString());
            Assert.Contains(guideRoot.GetProperty("steps").EnumerateArray(), step =>
                step.GetProperty("stage_id").GetString() == "cli_invocation_contract");
            Assert.Contains(guideRoot.GetProperty("steps").EnumerateArray(), step =>
                step.GetProperty("stage_id").GetString() == "cli_activation_plan");
            Assert.Contains(guideRoot.GetProperty("steps").EnumerateArray(), step =>
                step.GetProperty("stage_id").GetString() == "review_writeback");
            Assert.Contains(guideRoot.GetProperty("steps").EnumerateArray(), step =>
                step.GetProperty("stage_id").GetString() == "target_ignore_decision_record");
            Assert.Contains(guideRoot.GetProperty("steps").EnumerateArray(), step =>
                step.GetProperty("stage_id").GetString() == "frozen_dist_target_readback_proof");

            using var apiDocument = JsonDocument.Parse(api.StandardOutput);
            Assert.Equal("runtime-product-closure-pilot-guide", apiDocument.RootElement.GetProperty("surface_id").GetString());

            using var handoffDocument = JsonDocument.Parse(handoff.StandardOutput);
            var handoffRoot = handoffDocument.RootElement;
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", handoffRoot.GetProperty("product_closure_phase").GetString());
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot start [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "inspect runtime-external-target-pilot-start");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot next [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "inspect runtime-external-target-pilot-next");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot problem-intake [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "inspect runtime-agent-problem-intake");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot triage [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "inspect runtime-agent-problem-triage-ledger");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot follow-up [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "inspect runtime-agent-problem-follow-up-candidates");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot follow-up-plan [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "inspect runtime-agent-problem-follow-up-decision-plan");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot follow-up-record [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "inspect runtime-agent-problem-follow-up-decision-record");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot follow-up-intake [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "inspect runtime-agent-problem-follow-up-planning-intake");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot follow-up-gate [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "inspect runtime-agent-problem-follow-up-planning-gate");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "agent start [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot invocation [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot activation [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot dist-smoke [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot dist-binding [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot target-proof [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot guide [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot status [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot resources [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot commit-plan [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot closure [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot residue [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot ignore-plan [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot ignore-record [--json]");
            Assert.Contains(handoffRoot.GetProperty("required_cold_readbacks").EnumerateArray(), readback =>
                readback.GetString() == "pilot proof [--json]");

            var targetAgentBootstrapPath = Path.Combine(targetRepo.RootPath, ".ai", "AGENT_BOOTSTRAP.md");
            var rootAgentsPath = Path.Combine(targetRepo.RootPath, "AGENTS.md");
            Assert.True(File.Exists(targetAgentBootstrapPath));
            Assert.True(File.Exists(rootAgentsPath));
            Assert.Contains("agent start --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot invocation --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot activation --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot dist-smoke --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot dist-binding --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot target-proof --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot status --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot problem-intake --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot triage --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot follow-up --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot follow-up-plan --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot follow-up-record --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot follow-up-intake --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot follow-up-gate --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot commit-plan", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot closure --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot residue --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot ignore-plan --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot ignore-record --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot proof --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("agent start --json", File.ReadAllText(rootAgentsPath), StringComparison.Ordinal);
            Assert.Contains("next_governed_command", File.ReadAllText(rootAgentsPath), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_RUNTIME_ROOT", previousRuntimeRoot);
            ProgramHarness.Run("--repo-root", runtimeRepo.RootPath, "host", "stop", "phase 11b external target pilot status cleanup");
        }
    }

    [Fact]
    public void Phase12_ExternalTargetRepo_RepairsMissingBootstrapWithoutRerunningAttach()
    {
        using var runtimeRepo = RepoSandbox.CreateFromCurrentRepo();
        runtimeRepo.MarkAllTasksCompleted();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/AgentCoachSeed.cs", "namespace AgentCoach; public sealed class AgentCoachSeed { }");
        targetRepo.CommitAll("Initial commit");

        ProgramHarness.Run("--repo-root", runtimeRepo.RootPath, "host", "start", "--interval-ms", "200");

        try
        {
            var init = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "init", "--json");
            Assert.Equal(0, init.ExitCode);

            var targetAgentBootstrapPath = Path.Combine(targetRepo.RootPath, ".ai", "AGENT_BOOTSTRAP.md");
            var rootAgentsPath = Path.Combine(targetRepo.RootPath, "AGENTS.md");
            File.Delete(targetAgentBootstrapPath);
            const string targetOwnedAgents = "# Target-owned AGENTS\n\nThis file must not be overwritten by bootstrap repair.\n";
            File.WriteAllText(rootAgentsPath, targetOwnedAgents);

            var statusBeforeRepair = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "status", "--json");
            var readOnlyBootstrap = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "agent", "bootstrap", "--json");

            Assert.Equal(0, statusBeforeRepair.ExitCode);
            Assert.Equal(0, readOnlyBootstrap.ExitCode);

            using var statusBeforeDocument = JsonDocument.Parse(statusBeforeRepair.StandardOutput);
            var statusBeforeRoot = statusBeforeDocument.RootElement;
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", statusBeforeRoot.GetProperty("product_closure_phase").GetString());
            Assert.Equal("pilot_status_target_agent_bootstrap_required", statusBeforeRoot.GetProperty("overall_posture").GetString());
            Assert.Equal("target_agent_bootstrap", statusBeforeRoot.GetProperty("current_stage_id").GetString());
            Assert.Equal("carves agent bootstrap --write", statusBeforeRoot.GetProperty("next_command").GetString());
            Assert.Contains(statusBeforeRoot.GetProperty("gaps").EnumerateArray(), gap =>
                gap.GetString() == "target_agent_bootstrap_missing");

            using var readOnlyDocument = JsonDocument.Parse(readOnlyBootstrap.StandardOutput);
            var readOnlyRoot = readOnlyDocument.RootElement;
            Assert.Equal("runtime-target-agent-bootstrap-pack", readOnlyRoot.GetProperty("surface_id").GetString());
            Assert.Equal("target_agent_bootstrap_materialization_required", readOnlyRoot.GetProperty("overall_posture").GetString());
            Assert.False(File.Exists(targetAgentBootstrapPath));

            var writeBootstrap = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "agent", "bootstrap", "--write", "--json");
            var statusAfterRepair = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "status", "--json");

            Assert.Equal(0, writeBootstrap.ExitCode);
            Assert.Equal(0, statusAfterRepair.ExitCode);

            using var writeDocument = JsonDocument.Parse(writeBootstrap.StandardOutput);
            var writeRoot = writeDocument.RootElement;
            Assert.Equal("target_agent_bootstrap_materialized", writeRoot.GetProperty("overall_posture").GetString());
            Assert.True(File.Exists(targetAgentBootstrapPath));
            Assert.Equal(targetOwnedAgents, File.ReadAllText(rootAgentsPath));
            Assert.Contains("agent start --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot invocation --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot activation --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot dist-smoke --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot dist-binding --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot status --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot resources --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot problem-intake --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot triage --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot follow-up --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot follow-up-plan --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot follow-up-record --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot follow-up-intake --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot follow-up-gate --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot residue --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot ignore-plan --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot ignore-record --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains("pilot proof --json", File.ReadAllText(targetAgentBootstrapPath), StringComparison.Ordinal);
            Assert.Contains(writeRoot.GetProperty("materialized_files").EnumerateArray(), file =>
                file.GetString() == ".ai/AGENT_BOOTSTRAP.md");
            Assert.Contains(writeRoot.GetProperty("skipped_files").EnumerateArray(), file =>
                file.GetString() == "AGENTS.md");

            using var statusAfterDocument = JsonDocument.Parse(statusAfterRepair.StandardOutput);
            var statusAfterRoot = statusAfterDocument.RootElement;
            Assert.Equal("pilot_status_intent_capture_required", statusAfterRoot.GetProperty("overall_posture").GetString());
            Assert.Equal("intent_capture", statusAfterRoot.GetProperty("current_stage_id").GetString());
            Assert.Equal("carves discuss context", statusAfterRoot.GetProperty("next_command").GetString());
            Assert.True(statusAfterRoot.GetProperty("discussion_first_surface").GetBoolean());
            Assert.False(statusAfterRoot.GetProperty("auto_run_allowed").GetBoolean());
            Assert.True(statusAfterRoot.GetProperty("recommended_action_id").ValueKind == JsonValueKind.Null);
        }
        finally
        {
            ProgramHarness.Run("--repo-root", runtimeRepo.RootPath, "host", "stop", "phase 12 external target bootstrap repair cleanup");
        }
    }

    [Fact]
    public void Phase7_ExternalTargetRepo_ResolvesManagedWorkspaceDoctrineFromRuntimeDocumentRoot()
    {
        using var runtimeRepo = RepoSandbox.CreateFromCurrentRepo();
        runtimeRepo.MarkAllTasksCompleted();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/AgentCoachSeed.cs", "namespace AgentCoach; public sealed class AgentCoachSeed { }");
        targetRepo.CommitAll("Initial commit");

        ProgramHarness.Run("--repo-root", runtimeRepo.RootPath, "host", "start", "--interval-ms", "200");

        try
        {
            var init = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "init", "--json");
            Assert.Equal(0, init.ExitCode);

            var draft = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "intent", "draft", "--persist");
            var focus = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "intent", "focus", "candidate-first-slice");
            var resolveValidation = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "intent", "decision", "first_validation_artifact", "resolved");
            var resolveBoundary = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "intent", "decision", "first_slice_boundary", "resolved");
            var ready = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "intent", "candidate", "candidate-first-slice", "ready_to_plan");
            var planInit = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "plan", "init", "candidate-first-slice");

            Assert.Equal(0, draft.ExitCode);
            Assert.Equal(0, focus.ExitCode);
            Assert.Equal(0, resolveValidation.ExitCode);
            Assert.Equal(0, resolveBoundary.ExitCode);
            Assert.Equal(0, ready.ExitCode);
            Assert.Equal(0, planInit.ExitCode);

            var draftsRoot = Path.Combine(targetRepo.RootPath, "drafts");
            Directory.CreateDirectory(draftsRoot);
            var exportCardPath = Path.Combine(draftsRoot, "phase7-plan-card.json");
            var exportCard = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "plan", "export-card", exportCardPath);
            var createCard = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "plan", "draft-card", exportCardPath);
            var cardId = ExtractFirstMatch(createCard.StandardOutput, @"Created card draft (?<card_id>CARD-[A-Za-z0-9-]+)\.", "card_id");
            var approveCard = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "plan", "approve-card", cardId, "approved for phase 7 managed workspace dogfood");
            var taskId = $"T-{cardId}-001";
            var taskGraphPath = Path.Combine(draftsRoot, "phase7-taskgraph-draft.json");
            File.WriteAllText(
                taskGraphPath,
                JsonSerializer.Serialize(
                    new
                    {
                        card_id = cardId,
                        tasks = new object[]
                        {
                            new
                            {
                                task_id = taskId,
                                title = "Create AgentCoach first-slice project brief",
                                description = "Create the first bounded AgentCoach project brief through a managed workspace.",
                                scope = new[] { "PROJECT.md", "docs/agentcoach-first-slice.md" },
                                acceptance = new[]
                                {
                                    "PROJECT.md states the AgentCoach purpose and first bounded slice",
                                    "docs/agentcoach-first-slice.md records the CARVES-governed handoff boundary",
                                },
                                proof_target = new
                                {
                                    kind = "focused_behavior",
                                    description = "Prove an attached target repo can issue a task-bound workspace without copying Runtime doctrine docs.",
                                },
                            },
                        },
                    },
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                        WriteIndented = true,
                    }));

            var createTaskGraph = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "plan", "draft-taskgraph", taskGraphPath);
            var taskGraphDraftId = ExtractFirstMatch(createTaskGraph.StandardOutput, @"Created taskgraph draft (?<draft_id>TG-[A-Za-z0-9-]+) for", "draft_id");
            var approveTaskGraph = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "plan", "approve-taskgraph", taskGraphDraftId, "approved for phase 7 managed workspace dogfood");
            targetRepo.CommitAll("Promote planning truth before workspace issuance");
            var issueWorkspace = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "plan", "issue-workspace", taskId);
            var inspectWorkspace = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-managed-workspace");
            var apiWorkspace = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "api", "runtime-managed-workspace");

            Assert.Equal(0, exportCard.ExitCode);
            Assert.Equal(0, createCard.ExitCode);
            Assert.Equal(0, approveCard.ExitCode);
            Assert.Equal(0, createTaskGraph.ExitCode);
            Assert.Equal(0, approveTaskGraph.ExitCode);
            Assert.Equal(0, issueWorkspace.ExitCode);
            Assert.Equal(0, inspectWorkspace.ExitCode);
            Assert.Equal(0, apiWorkspace.ExitCode);

            Assert.Contains("Runtime managed workspace", inspectWorkspace.StandardOutput, StringComparison.Ordinal);
            Assert.Contains($"Runtime document root: {Path.GetFullPath(runtimeRepo.RootPath)}", inspectWorkspace.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Runtime document root mode: attach_handshake_runtime_root", inspectWorkspace.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Overall posture: task_bound_workspace_active", inspectWorkspace.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Mode D hardening state: active", inspectWorkspace.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Validation valid: True", inspectWorkspace.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("docs/runtime/runtime-managed-workspace-file-operation-model.md' is missing", inspectWorkspace.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("docs/runtime/runtime-agent-working-modes-and-constraint-ladder.md' is missing", inspectWorkspace.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("docs/runtime/runtime-agent-working-modes-implementation-plan.md' is missing", inspectWorkspace.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("docs/runtime/runtime-mode-d-scoped-task-workspace-hardening.md' is missing", inspectWorkspace.StandardOutput, StringComparison.Ordinal);

            using var apiDocument = ParseJsonFromOutput(apiWorkspace.StandardOutput);
            var apiRoot = apiDocument.RootElement;
            Assert.Equal("runtime-managed-workspace", apiRoot.GetProperty("surface_id").GetString());
            Assert.Equal(Path.GetFullPath(runtimeRepo.RootPath), apiRoot.GetProperty("runtime_document_root").GetString());
            Assert.Equal("attach_handshake_runtime_root", apiRoot.GetProperty("runtime_document_root_mode").GetString());
            Assert.Equal("task_bound_workspace_active", apiRoot.GetProperty("overall_posture").GetString());
            Assert.Equal("active", apiRoot.GetProperty("mode_d_hardening_state").GetString());
            Assert.True(apiRoot.GetProperty("is_valid").GetBoolean());
        }
        finally
        {
            ProgramHarness.Run("--repo-root", runtimeRepo.RootPath, "host", "stop", "phase 7 external target managed workspace cleanup");
        }
    }

    [Fact]
    public void Phase8_ExternalTargetRepo_SubmitsManagedWorkspaceAndApprovesOfficialWriteback()
    {
        using var runtimeRepo = RepoSandbox.CreateFromCurrentRepo();
        runtimeRepo.MarkAllTasksCompleted();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/AgentCoachSeed.cs", "namespace AgentCoach; public sealed class AgentCoachSeed { }");
        targetRepo.CommitAll("Initial commit");

        ProgramHarness.Run("--repo-root", runtimeRepo.RootPath, "host", "start", "--interval-ms", "200");

        try
        {
            var init = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "init", "--json");
            Assert.Equal(0, init.ExitCode);

            var draft = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "intent", "draft", "--persist");
            var focus = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "intent", "focus", "candidate-first-slice");
            var resolveValidation = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "intent", "decision", "first_validation_artifact", "resolved");
            var resolveBoundary = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "intent", "decision", "first_slice_boundary", "resolved");
            var ready = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "intent", "candidate", "candidate-first-slice", "ready_to_plan");
            var planInit = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "plan", "init", "candidate-first-slice");

            Assert.Equal(0, draft.ExitCode);
            Assert.Equal(0, focus.ExitCode);
            Assert.Equal(0, resolveValidation.ExitCode);
            Assert.Equal(0, resolveBoundary.ExitCode);
            Assert.Equal(0, ready.ExitCode);
            Assert.Equal(0, planInit.ExitCode);

            var draftsRoot = Path.Combine(targetRepo.RootPath, "drafts");
            Directory.CreateDirectory(draftsRoot);
            var exportCardPath = Path.Combine(draftsRoot, "phase8-plan-card.json");
            var exportCard = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "plan", "export-card", exportCardPath);
            var createCard = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "plan", "draft-card", exportCardPath);
            var cardId = ExtractFirstMatch(createCard.StandardOutput, @"Created card draft (?<card_id>CARD-[A-Za-z0-9-]+)\.", "card_id");
            var approveCard = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "plan", "approve-card", cardId, "approved for phase 8 managed workspace writeback");
            var taskId = $"T-{cardId}-001";
            var taskGraphPath = Path.Combine(draftsRoot, "phase8-taskgraph-draft.json");
            File.WriteAllText(
                taskGraphPath,
                JsonSerializer.Serialize(
                    new
                    {
                        card_id = cardId,
                        tasks = new object[]
                        {
                            new
                            {
                                task_id = taskId,
                                title = "Create AgentCoach first-slice project brief",
                                description = "Create the first bounded AgentCoach project brief through a managed workspace.",
                                scope = new[] { "PROJECT.md", "docs/agentcoach-first-slice.md" },
                                acceptance = new[]
                                {
                                    "PROJECT.md states the AgentCoach purpose and first bounded slice",
                                    "docs/agentcoach-first-slice.md records the CARVES-governed handoff boundary",
                                },
                                proof_target = new
                                {
                                    kind = "focused_behavior",
                                    description = "Prove an attached target repo can write back a task-bound workspace through review approval.",
                                },
                            },
                        },
                    },
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                        WriteIndented = true,
                    }));

            var createTaskGraph = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "plan", "draft-taskgraph", taskGraphPath);
            var taskGraphDraftId = ExtractFirstMatch(createTaskGraph.StandardOutput, @"Created taskgraph draft (?<draft_id>TG-[A-Za-z0-9-]+) for", "draft_id");
            var approveTaskGraph = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "plan", "approve-taskgraph", taskGraphDraftId, "approved for phase 8 managed workspace writeback");
            targetRepo.CommitAll("Promote planning truth before workspace writeback");
            var issueWorkspace = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "plan", "issue-workspace", taskId);

            Assert.Equal(0, exportCard.ExitCode);
            Assert.Equal(0, createCard.ExitCode);
            Assert.Equal(0, approveCard.ExitCode);
            Assert.Equal(0, createTaskGraph.ExitCode);
            Assert.Equal(0, approveTaskGraph.ExitCode);
            Assert.Equal(0, issueWorkspace.ExitCode);

            using var issueDocument = ParseJsonFromOutput(issueWorkspace.StandardOutput);
            var workspacePath = issueDocument.RootElement.GetProperty("lease").GetProperty("workspace_path").GetString();
            Assert.False(string.IsNullOrWhiteSpace(workspacePath));
            Directory.CreateDirectory(Path.Combine(workspacePath!, "docs"));
            File.WriteAllText(
                Path.Combine(workspacePath!, "PROJECT.md"),
                "# CARVES.AgentCoach\n\nAgentCoach is the first governed external project pilot.\n");
            File.WriteAllText(
                Path.Combine(workspacePath!, "docs", "agentcoach-first-slice.md"),
                "# AgentCoach First Slice\n\nThis slice returns through CARVES managed workspace review/writeback.\n");

            var submit = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "plan", "submit-workspace", taskId, "submitted phase 8 managed workspace result");
            var mutationAudit = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-workspace-mutation-audit", taskId);
            var approve = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "review", "approve", taskId, "approved phase 8 managed workspace writeback");
            var inspectWorkspaceAfterApprove = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "inspect", "runtime-managed-workspace");
            var pilotStatusAfterWriteback = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "status", "--json");
            var targetCommitHygiene = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "commit-hygiene", "--json");
            var targetCommitPlan = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "commit-plan", "--json");
            var targetCommitClosureBeforeCommit = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "closure", "--json");

            Assert.True(submit.ExitCode == 0, submit.CombinedOutput);
            Assert.True(mutationAudit.ExitCode == 0, mutationAudit.CombinedOutput);
            Assert.True(approve.ExitCode == 0, approve.CombinedOutput);
            Assert.True(inspectWorkspaceAfterApprove.ExitCode == 0, inspectWorkspaceAfterApprove.CombinedOutput);
            Assert.True(pilotStatusAfterWriteback.ExitCode == 0, pilotStatusAfterWriteback.CombinedOutput);
            Assert.True(targetCommitHygiene.ExitCode == 0, targetCommitHygiene.CombinedOutput);
            Assert.True(targetCommitPlan.ExitCode == 0, targetCommitPlan.CombinedOutput);
            Assert.True(targetCommitClosureBeforeCommit.ExitCode == 0, targetCommitClosureBeforeCommit.CombinedOutput);
            Assert.Contains($"Submitted managed workspace result for {taskId}; task is now REVIEW.", submit.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("PROJECT.md", submit.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("docs/agentcoach-first-slice.md", submit.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Changed paths: 2", mutationAudit.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Can proceed to writeback: True", mutationAudit.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Approved review", approve.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Materialized 2 approved file(s)", approve.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Released 1 managed workspace lease(s)", approve.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Overall posture: planning_lineage_closed_no_active_workspace", inspectWorkspaceAfterApprove.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Mode D hardening state: closed_no_active_workspace", inspectWorkspaceAfterApprove.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Active leases: 0", inspectWorkspaceAfterApprove.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("no active managed workspace is required", inspectWorkspaceAfterApprove.StandardOutput, StringComparison.Ordinal);

            using var pilotStatusAfterWritebackDocument = ParseJsonFromOutput(pilotStatusAfterWriteback.StandardOutput);
            var pilotStatusAfterWritebackRoot = pilotStatusAfterWritebackDocument.RootElement;
            Assert.Equal("pilot_status_writeback_complete_commit_plan_required", pilotStatusAfterWritebackRoot.GetProperty("overall_posture").GetString());
            Assert.Equal("target_commit_plan", pilotStatusAfterWritebackRoot.GetProperty("current_stage_id").GetString());
            Assert.Equal("carves pilot commit-plan", pilotStatusAfterWritebackRoot.GetProperty("next_command").GetString());

            using var targetCommitHygieneDocument = ParseJsonFromOutput(targetCommitHygiene.StandardOutput);
            var targetCommitHygieneRoot = targetCommitHygieneDocument.RootElement;
            Assert.Equal("runtime-target-commit-hygiene", targetCommitHygieneRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", targetCommitHygieneRoot.GetProperty("product_closure_phase").GetString());
            Assert.True(targetCommitHygieneRoot.GetProperty("can_proceed_to_commit").GetBoolean(), targetCommitHygiene.StandardOutput);
            Assert.True(targetCommitHygieneRoot.GetProperty("commit_candidate_path_count").GetInt32() > 0);
            Assert.Equal(0, targetCommitHygieneRoot.GetProperty("unclassified_path_count").GetInt32());
            Assert.Contains(targetCommitHygieneRoot.GetProperty("dirty_paths").EnumerateArray(), path =>
                path.GetProperty("path").GetString() == "PROJECT.md"
                && path.GetProperty("path_class").GetString() == "target_output_candidate");
            Assert.Contains(targetCommitHygieneRoot.GetProperty("dirty_paths").EnumerateArray(), path =>
                path.GetProperty("path").GetString() == $".ai/artifacts/reviews/{taskId}.json"
                && path.GetProperty("path_class").GetString() == "official_target_truth");

            using var targetCommitPlanDocument = ParseJsonFromOutput(targetCommitPlan.StandardOutput);
            var targetCommitPlanRoot = targetCommitPlanDocument.RootElement;
            Assert.Equal("runtime-target-commit-plan", targetCommitPlanRoot.GetProperty("surface_id").GetString());
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", targetCommitPlanRoot.GetProperty("product_closure_phase").GetString());
            Assert.True(targetCommitPlanRoot.GetProperty("can_stage").GetBoolean(), targetCommitPlan.StandardOutput);
            Assert.True(targetCommitPlanRoot.GetProperty("can_commit_after_staging").GetBoolean(), targetCommitPlan.StandardOutput);
            Assert.Equal(0, targetCommitPlanRoot.GetProperty("operator_review_required_path_count").GetInt32());
            Assert.Contains("git add --", targetCommitPlanRoot.GetProperty("git_add_command_preview").GetString(), StringComparison.Ordinal);
            Assert.Contains(targetCommitPlanRoot.GetProperty("stage_paths").EnumerateArray(), path =>
                path.GetString() == "PROJECT.md");
            Assert.Contains(targetCommitPlanRoot.GetProperty("stage_paths").EnumerateArray(), path =>
                path.GetString() == $".ai/artifacts/reviews/{taskId}.json");

            using var targetCommitClosureBeforeCommitDocument = ParseJsonFromOutput(targetCommitClosureBeforeCommit.StandardOutput);
            var targetCommitClosureBeforeCommitRoot = targetCommitClosureBeforeCommitDocument.RootElement;
            Assert.Equal("runtime-target-commit-closure", targetCommitClosureBeforeCommitRoot.GetProperty("surface_id").GetString());
            Assert.Equal("target_commit_closure_waiting_for_operator_commit", targetCommitClosureBeforeCommitRoot.GetProperty("overall_posture").GetString());
            Assert.False(targetCommitClosureBeforeCommitRoot.GetProperty("commit_closure_complete").GetBoolean());

            Assert.True(File.Exists(Path.Combine(targetRepo.RootPath, "PROJECT.md")));
            Assert.True(File.Exists(Path.Combine(targetRepo.RootPath, "docs", "agentcoach-first-slice.md")));
            Assert.Contains("first governed external project pilot", File.ReadAllText(Path.Combine(targetRepo.RootPath, "PROJECT.md")), StringComparison.Ordinal);

            var taskNodePath = Path.Combine(targetRepo.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json");
            var reviewArtifactPath = Path.Combine(targetRepo.RootPath, ".ai", "artifacts", "reviews", $"{taskId}.json");
            var mergeCandidatePath = Path.Combine(targetRepo.RootPath, ".ai", "artifacts", "merge-candidates", $"{taskId}.json");
            Assert.True(File.Exists(taskNodePath));
            Assert.True(File.Exists(reviewArtifactPath));
            Assert.True(File.Exists(mergeCandidatePath));
            var taskNodeJson = File.ReadAllText(taskNodePath);
            var reviewJson = File.ReadAllText(reviewArtifactPath);
            Assert.Contains("\"status\": \"completed\"", taskNodeJson, StringComparison.Ordinal);
            Assert.Contains("\"decision_status\": \"approved\"", reviewJson, StringComparison.Ordinal);
            Assert.Contains("\"writeback\": {", reviewJson, StringComparison.Ordinal);
            Assert.Contains("\"applied\": true", reviewJson, StringComparison.Ordinal);

            targetRepo.CommitAll("Commit governed phase 15 writeback output");
            targetRepo.WriteFile(".carves-platform/live-state/phase29-ignore-record.json", "{}");
            var targetCommitClosureAfterCommit = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "closure", "--json");
            var targetResiduePolicyAfterCommit = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "residue", "--json");
            var targetIgnoreDecisionPlanAfterCommit = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "ignore-plan", "--json");
            var targetIgnoreDecisionRecordBeforeDecision = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "ignore-record", "--json");
            var pilotStatusAfterCommit = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "status", "--json");
            var productPilotProofAfterCommit = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "proof", "--json");
            Assert.True(targetCommitClosureAfterCommit.ExitCode == 0, targetCommitClosureAfterCommit.CombinedOutput);
            Assert.True(targetResiduePolicyAfterCommit.ExitCode == 0, targetResiduePolicyAfterCommit.CombinedOutput);
            Assert.True(targetIgnoreDecisionPlanAfterCommit.ExitCode == 0, targetIgnoreDecisionPlanAfterCommit.CombinedOutput);
            Assert.True(targetIgnoreDecisionRecordBeforeDecision.ExitCode == 0, targetIgnoreDecisionRecordBeforeDecision.CombinedOutput);
            Assert.True(pilotStatusAfterCommit.ExitCode == 0, pilotStatusAfterCommit.CombinedOutput);
            Assert.True(productPilotProofAfterCommit.ExitCode == 0, productPilotProofAfterCommit.CombinedOutput);

            using var targetCommitClosureAfterCommitDocument = ParseJsonFromOutput(targetCommitClosureAfterCommit.StandardOutput);
            var targetCommitClosureAfterCommitRoot = targetCommitClosureAfterCommitDocument.RootElement;
            var targetCommitClosurePosture = targetCommitClosureAfterCommitRoot.GetProperty("overall_posture").GetString();
            Assert.True(
                targetCommitClosurePosture is "target_commit_closure_clean" or "target_commit_closure_local_residue_only_complete",
                targetCommitClosureAfterCommit.StandardOutput);
            Assert.True(targetCommitClosureAfterCommitRoot.GetProperty("commit_closure_complete").GetBoolean());

            using var targetResiduePolicyAfterCommitDocument = ParseJsonFromOutput(targetResiduePolicyAfterCommit.StandardOutput);
            var targetResiduePolicyAfterCommitRoot = targetResiduePolicyAfterCommitDocument.RootElement;
            Assert.Equal("runtime-target-residue-policy", targetResiduePolicyAfterCommitRoot.GetProperty("surface_id").GetString());
            Assert.True(targetResiduePolicyAfterCommitRoot.GetProperty("residue_policy_ready").GetBoolean());
            Assert.True(targetResiduePolicyAfterCommitRoot.GetProperty("product_proof_can_remain_complete").GetBoolean());
            Assert.Empty(targetResiduePolicyAfterCommitRoot.GetProperty("gaps").EnumerateArray());

            using var targetIgnoreDecisionPlanAfterCommitDocument = ParseJsonFromOutput(targetIgnoreDecisionPlanAfterCommit.StandardOutput);
            var targetIgnoreDecisionPlanAfterCommitRoot = targetIgnoreDecisionPlanAfterCommitDocument.RootElement;
            Assert.Equal("runtime-target-ignore-decision-plan", targetIgnoreDecisionPlanAfterCommitRoot.GetProperty("surface_id").GetString());
            Assert.True(targetIgnoreDecisionPlanAfterCommitRoot.GetProperty("ignore_decision_plan_ready").GetBoolean());
            Assert.True(targetIgnoreDecisionPlanAfterCommitRoot.GetProperty("product_proof_can_remain_complete").GetBoolean());
            Assert.True(targetIgnoreDecisionPlanAfterCommitRoot.GetProperty("ignore_decision_required").GetBoolean());
            Assert.True(targetIgnoreDecisionPlanAfterCommitRoot.GetProperty("missing_ignore_entry_count").GetInt32() > 0);
            Assert.Empty(targetIgnoreDecisionPlanAfterCommitRoot.GetProperty("gaps").EnumerateArray());

            using var targetIgnoreDecisionRecordBeforeDecisionDocument = ParseJsonFromOutput(targetIgnoreDecisionRecordBeforeDecision.StandardOutput);
            var targetIgnoreDecisionRecordBeforeDecisionRoot = targetIgnoreDecisionRecordBeforeDecisionDocument.RootElement;
            Assert.Equal("runtime-target-ignore-decision-record", targetIgnoreDecisionRecordBeforeDecisionRoot.GetProperty("surface_id").GetString());
            Assert.False(targetIgnoreDecisionRecordBeforeDecisionRoot.GetProperty("decision_record_ready").GetBoolean());
            Assert.True(targetIgnoreDecisionRecordBeforeDecisionRoot.GetProperty("record_audit_ready").GetBoolean());
            Assert.True(targetIgnoreDecisionRecordBeforeDecisionRoot.GetProperty("decision_record_commit_ready").GetBoolean());
            Assert.Equal(0, targetIgnoreDecisionRecordBeforeDecisionRoot.GetProperty("uncommitted_decision_record_count").GetInt32());
            Assert.True(targetIgnoreDecisionRecordBeforeDecisionRoot.GetProperty("missing_decision_entry_count").GetInt32() > 0);

            using var pilotStatusAfterCommitDocument = ParseJsonFromOutput(pilotStatusAfterCommit.StandardOutput);
            var pilotStatusAfterCommitRoot = pilotStatusAfterCommitDocument.RootElement;
            Assert.Equal("pilot_status_target_ignore_decision_record_required", pilotStatusAfterCommitRoot.GetProperty("overall_posture").GetString());
            Assert.Equal("target_ignore_decision_record", pilotStatusAfterCommitRoot.GetProperty("current_stage_id").GetString());
            Assert.Equal("ready", pilotStatusAfterCommitRoot.GetProperty("current_stage_status").GetString());
            Assert.Equal("carves pilot ignore-record --json", pilotStatusAfterCommitRoot.GetProperty("next_command").GetString());
            Assert.True(pilotStatusAfterCommitRoot.GetProperty("target_commit_closure_complete").GetBoolean());
            Assert.False(pilotStatusAfterCommitRoot.GetProperty("local_dist_freshness_smoke_ready").GetBoolean());

            using var productPilotProofAfterCommitDocument = ParseJsonFromOutput(productPilotProofAfterCommit.StandardOutput);
            var productPilotProofAfterCommitRoot = productPilotProofAfterCommitDocument.RootElement;
            Assert.Equal("runtime-product-pilot-proof", productPilotProofAfterCommitRoot.GetProperty("surface_id").GetString());
            Assert.Equal("product_pilot_proof_waiting_for_local_dist_freshness_smoke", productPilotProofAfterCommitRoot.GetProperty("overall_posture").GetString());
            Assert.False(productPilotProofAfterCommitRoot.GetProperty("product_pilot_proof_complete").GetBoolean());
            Assert.False(productPilotProofAfterCommitRoot.GetProperty("local_dist_freshness_smoke_ready").GetBoolean());
            Assert.False(productPilotProofAfterCommitRoot.GetProperty("stable_external_consumption_ready").GetBoolean());
            Assert.True(productPilotProofAfterCommitRoot.GetProperty("target_commit_closure_complete").GetBoolean());

            var recordIgnoreDecision = CliProgramHarness.RunInDirectory(
                targetRepo.RootPath,
                "pilot",
                "record-ignore-decision",
                "keep_local",
                "--all",
                "--reason",
                "operator accepted local CARVES residue",
                "--operator",
                "integration-test",
                "--json");
            Assert.True(recordIgnoreDecision.ExitCode == 0, recordIgnoreDecision.CombinedOutput);

            using var recordIgnoreDecisionDocument = ParseJsonFromOutput(recordIgnoreDecision.StandardOutput);
            var recordIgnoreDecisionRoot = recordIgnoreDecisionDocument.RootElement;
            Assert.Equal("runtime-target-ignore-decision-record-entry.v1", recordIgnoreDecisionRoot.GetProperty("schema_version").GetString());
            Assert.Equal("keep_local", recordIgnoreDecisionRoot.GetProperty("decision").GetString());
            var decisionRecordPath = recordIgnoreDecisionRoot.GetProperty("record_path").GetString();
            Assert.False(string.IsNullOrWhiteSpace(decisionRecordPath));
            Assert.StartsWith(".ai/runtime/target-ignore-decisions/", decisionRecordPath, StringComparison.Ordinal);

            GitTestHarness.Run(targetRepo.RootPath, "add", ".ai/runtime/target-ignore-decisions");
            GitTestHarness.Run(targetRepo.RootPath, "commit", "-m", "Record target ignore decision");

            var targetIgnoreDecisionRecordAfterDecision = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "ignore-record", "--json");
            var pilotStatusAfterDecision = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "status", "--json");
            var productPilotProofAfterDecision = CliProgramHarness.RunInDirectory(targetRepo.RootPath, "pilot", "proof", "--json");
            Assert.True(targetIgnoreDecisionRecordAfterDecision.ExitCode == 0, targetIgnoreDecisionRecordAfterDecision.CombinedOutput);
            Assert.True(pilotStatusAfterDecision.ExitCode == 0, pilotStatusAfterDecision.CombinedOutput);
            Assert.True(productPilotProofAfterDecision.ExitCode == 0, productPilotProofAfterDecision.CombinedOutput);

            using var targetIgnoreDecisionRecordAfterDecisionDocument = ParseJsonFromOutput(targetIgnoreDecisionRecordAfterDecision.StandardOutput);
            var targetIgnoreDecisionRecordAfterDecisionRoot = targetIgnoreDecisionRecordAfterDecisionDocument.RootElement;
            Assert.True(targetIgnoreDecisionRecordAfterDecisionRoot.GetProperty("decision_record_ready").GetBoolean());
            Assert.True(targetIgnoreDecisionRecordAfterDecisionRoot.GetProperty("record_audit_ready").GetBoolean());
            Assert.True(targetIgnoreDecisionRecordAfterDecisionRoot.GetProperty("decision_record_commit_ready").GetBoolean());
            Assert.Equal(0, targetIgnoreDecisionRecordAfterDecisionRoot.GetProperty("conflicting_decision_entry_count").GetInt32());
            Assert.Equal(0, targetIgnoreDecisionRecordAfterDecisionRoot.GetProperty("missing_decision_entry_count").GetInt32());
            Assert.Equal(0, targetIgnoreDecisionRecordAfterDecisionRoot.GetProperty("uncommitted_decision_record_count").GetInt32());

            using var pilotStatusAfterDecisionDocument = ParseJsonFromOutput(pilotStatusAfterDecision.StandardOutput);
            var pilotStatusAfterDecisionRoot = pilotStatusAfterDecisionDocument.RootElement;
            Assert.Equal("pilot_status_local_dist_freshness_smoke_required", pilotStatusAfterDecisionRoot.GetProperty("overall_posture").GetString());
            Assert.Equal("local_dist_freshness_smoke", pilotStatusAfterDecisionRoot.GetProperty("current_stage_id").GetString());
            Assert.Equal("carves pilot dist-smoke --json", pilotStatusAfterDecisionRoot.GetProperty("next_command").GetString());

            using var productPilotProofAfterDecisionDocument = ParseJsonFromOutput(productPilotProofAfterDecision.StandardOutput);
            var productPilotProofAfterDecisionRoot = productPilotProofAfterDecisionDocument.RootElement;
            Assert.False(productPilotProofAfterDecisionRoot.GetProperty("product_pilot_proof_complete").GetBoolean());
            Assert.True(productPilotProofAfterDecisionRoot.GetProperty("target_ignore_decision_record_ready").GetBoolean());
            Assert.True(productPilotProofAfterDecisionRoot.GetProperty("target_ignore_decision_record_audit_ready").GetBoolean());
            Assert.True(productPilotProofAfterDecisionRoot.GetProperty("target_ignore_decision_record_commit_ready").GetBoolean());
        }
        finally
        {
            ProgramHarness.Run("--repo-root", runtimeRepo.RootPath, "host", "stop", "phase 8 external target writeback cleanup");
        }
    }

    private static JsonDocument ParseJsonFromOutput(string output)
    {
        var start = output.IndexOf('{', StringComparison.Ordinal);
        var end = output.LastIndexOf('}');
        Assert.True(start >= 0 && end > start, output);
        return JsonDocument.Parse(output[start..(end + 1)]);
    }

    private static string ExtractFirstMatch(string text, string pattern, string groupName)
    {
        var match = Regex.Match(text, pattern, RegexOptions.CultureInvariant);
        Assert.True(match.Success, text);
        return match.Groups[groupName].Value;
    }

    private sealed class GitSandbox : IDisposable
    {
        private GitSandbox(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static GitSandbox Create()
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "carves-runtime-phase5-dogfood", Guid.NewGuid().ToString("N"));
            GitTestHarness.InitializeRepository(rootPath);
            return new GitSandbox(rootPath);
        }

        public void WriteFile(string relativePath, string content)
        {
            var path = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void CommitAll(string message)
        {
            GitTestHarness.Run(RootPath, "add", ".");
            GitTestHarness.Run(RootPath, "commit", "-m", message);
        }

        public void Dispose()
        {
            if (!Directory.Exists(RootPath))
            {
                return;
            }

            try
            {
                Directory.Delete(RootPath, recursive: true);
            }
            catch
            {
            }
        }
    }
}

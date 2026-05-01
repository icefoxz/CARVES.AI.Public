namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeProjectRecenterCarvesUpAcceptanceCoverageTests
{
    [Fact]
    public void R9_5CoverageRecord_BindsStartupScenariosToExecutableTests()
    {
        var repoRoot = ResolveRepoRoot();
        var recordPath = Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-up-r9.5-acceptance-coverage.md");
        var record = File.ReadAllText(recordPath);
        var testSource = string.Join(
            "\n",
            Directory.EnumerateFiles(Path.Combine(repoRoot, "tests"), "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.Contains("Status: carves_up_acceptance_coverage", record, StringComparison.Ordinal);
        Assert.DoesNotContain("TODO", record, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TBD", record, StringComparison.OrdinalIgnoreCase);

        foreach (var scenarioId in RequiredScenarioIds)
        {
            Assert.Contains($"`{scenarioId}`", record, StringComparison.Ordinal);
        }

        foreach (var testRef in RequiredTestRefs)
        {
            Assert.Contains($"`{testRef}`", record, StringComparison.Ordinal);
            Assert.Contains(testRef, testSource, StringComparison.Ordinal);
        }

        foreach (var boundary in RequiredBoundaries)
        {
            Assert.Contains(boundary, record, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void R9_6CompositionRecord_BindsUpCommandToNullWorkerBoundary()
    {
        var repoRoot = ResolveRepoRoot();
        var recordPath = Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-up-r9.6-minimal-composition-command.md");
        var record = File.ReadAllText(recordPath);
        var friendlyCli = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Cli",
            "FriendlyCliApplication.cs"));
        var upCommand = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Cli",
            "FriendlyCliApplication.Up.cs"));
        var friendlyTests = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "Carves.Runtime.IntegrationTests",
            "FriendlyCliEntryTests.cs"));

        Assert.Contains("Status: carves_up_minimal_composition_command", record, StringComparison.Ordinal);
        Assert.Contains("carves up [target-project] [--json]", record, StringComparison.Ordinal);
        Assert.Contains("ready_for_agent_start", record, StringComparison.Ordinal);
        Assert.Contains(".carves/carves agent start --json", record, StringComparison.Ordinal);
        Assert.Contains("CARVES_START.md", record, StringComparison.Ordinal);
        Assert.Contains("null_worker_current_version_no_api_sdk_worker_execution", record, StringComparison.Ordinal);
        Assert.Contains("does not:", record, StringComparison.Ordinal);
        Assert.Contains("dispatch worker automation", record, StringComparison.Ordinal);
        Assert.Contains("authorize API/SDK worker execution", record, StringComparison.Ordinal);
        Assert.Contains("overwrite target-owned root `AGENTS.md`", record, StringComparison.Ordinal);

        Assert.Contains("return RunUp", friendlyCli, StringComparison.Ordinal);
        Assert.Contains("SchemaVersion: \"carves-up.v1\"", upCommand, StringComparison.Ordinal);
        Assert.Contains("not_global_alias_authority", upCommand, StringComparison.Ordinal);
        Assert.Contains("not_dashboard_product_entry", upCommand, StringComparison.Ordinal);
        Assert.Contains("not_worker_execution_authority", upCommand, StringComparison.Ordinal);

        Assert.Contains("Up_FromRuntimeDirectory_AutoEnsuresHostAndMaterializesProjectLocalAgentEntry", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("Up_WithExistingRootAgents_PreservesTargetInstructionsAndReportsSuggestedPatch", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("Up_WithoutRuntimeAuthorityRoot_BlocksBeforeHostMutation", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("Up_WhenRuntimeHostSessionConflictExists_FailsClosedWithReconcileGuidance", friendlyTests, StringComparison.Ordinal);
    }

    [Fact]
    public void UX0ProductStartContract_PutsCarvesUpBeforeHostDiagnostics()
    {
        var repoRoot = ResolveRepoRoot();
        var contract = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-ux0-product-start-contract.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));
        var quickstart = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "guides",
            "CARVES_EXTERNAL_AGENT_QUICKSTART.md"));
        var initGuide = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "guides",
            "CARVES_INIT_FIRST_RUN.md"));
        var help = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Cli",
            "FriendlyCliApplication.Help.cs"));

        Assert.Contains("Status: carves_ux0_product_start_contract", contract, StringComparison.Ordinal);
        Assert.Contains("carves up <project>", contract, StringComparison.Ordinal);
        Assert.Contains("host ensure", contract, StringComparison.Ordinal);
        Assert.Contains("diagnostic surfaces", contract, StringComparison.Ordinal);
        Assert.Contains("null_worker_current_version_no_api_sdk_worker_execution", contract, StringComparison.Ordinal);

        AssertAppearsBefore(readme, "./carves up /path/to/project", "host ensure");
        Assert.Contains("start CARVES", readme, StringComparison.Ordinal);
        Assert.Contains("`carves up` is the product startup entry", readme, StringComparison.Ordinal);

        AssertAppearsBefore(quickstart, "carves up <target-project>", "host ensure");
        Assert.Contains("Do not teach new users to run `host ensure` first", quickstart, StringComparison.Ordinal);

        Assert.Contains("Use `carves up <project>` for the product startup path.", initGuide, StringComparison.Ordinal);
        Assert.Contains("lower-level attach/init primitive", initGuide, StringComparison.Ordinal);

        Assert.Contains("carves up [path] [--json]       # first-use product entry", help, StringComparison.Ordinal);
        Assert.Contains("For product first use, prefer carves up [path].", help, StringComparison.Ordinal);
    }

    [Fact]
    public void UX1HostReadinessAutomation_BindsCarvesUpToSafeHostEnsure()
    {
        var repoRoot = ResolveRepoRoot();
        var contract = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-ux1-host-readiness-automation.md"));
        var upCommand = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Cli",
            "FriendlyCliApplication.Up.cs"));
        var r96 = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-up-r9.6-minimal-composition-command.md"));
        var friendlyTests = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "Carves.Runtime.IntegrationTests",
            "FriendlyCliEntryTests.cs"));

        Assert.Contains("Status: carves_ux1_host_readiness_automation", contract, StringComparison.Ordinal);
        Assert.Contains("carves host ensure --require-capability attach-flow --json", contract, StringComparison.Ordinal);
        Assert.Contains("host_authority_root", contract, StringComparison.Ordinal);
        Assert.Contains("host_auto_ensured", contract, StringComparison.Ordinal);
        Assert.Contains("stale/conflicting Host session", contract, StringComparison.Ordinal);
        Assert.Contains("stops before Host", contract, StringComparison.Ordinal);
        Assert.Contains("null_worker_current_version_no_api_sdk_worker_execution", contract, StringComparison.Ordinal);

        Assert.Contains("\"host\"", upCommand, StringComparison.Ordinal);
        Assert.Contains("\"ensure\"", upCommand, StringComparison.Ordinal);
        Assert.Contains("\"--require-capability\"", upCommand, StringComparison.Ordinal);
        Assert.Contains("\"attach-flow\"", upCommand, StringComparison.Ordinal);
        Assert.Contains("blocked_runtime_authority_missing", upCommand, StringComparison.Ordinal);
        Assert.Contains("blocked_host_session_conflict", upCommand, StringComparison.Ordinal);
        Assert.Contains("host_auto_ensured", r96, StringComparison.Ordinal);

        Assert.Contains("Up_FromRuntimeDirectory_AutoEnsuresHostAndMaterializesProjectLocalAgentEntry", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("Up_WithoutRuntimeAuthorityRoot_BlocksBeforeHostMutation", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("Up_WhenRuntimeHostSessionConflictExists_FailsClosedWithReconcileGuidance", friendlyTests, StringComparison.Ordinal);
    }

    [Fact]
    public void UX2HumanAgentHandoff_BindsCarvesUpToStartCarvesPrompt()
    {
        var repoRoot = ResolveRepoRoot();
        var contract = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-ux2-human-agent-handoff.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));
        var quickstart = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "guides",
            "CARVES_EXTERNAL_AGENT_QUICKSTART.md"));
        var upCommand = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Cli",
            "FriendlyCliApplication.Up.cs"));
        var help = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Cli",
            "FriendlyCliApplication.Help.cs"));
        var bootstrapPack = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Application",
            "Platform",
            "RuntimeTargetAgentBootstrapPackService.cs"));
        var friendlyTests = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "Carves.Runtime.IntegrationTests",
            "FriendlyCliEntryTests.cs"));
        var r96 = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-up-r9.6-minimal-composition-command.md"));

        Assert.Contains("Status: carves_ux2_human_agent_handoff", contract, StringComparison.Ordinal);
        Assert.Contains("human_next_action", contract, StringComparison.Ordinal);
        Assert.Contains("agent_start_prompt", contract, StringComparison.Ordinal);
        Assert.Contains("agent_instruction", contract, StringComparison.Ordinal);
        Assert.Contains("null_worker_current_version_no_api_sdk_worker_execution", contract, StringComparison.Ordinal);

        Assert.Contains("start CARVES", readme, StringComparison.Ordinal);
        Assert.Contains("human next step", readme, StringComparison.Ordinal);
        Assert.Contains(".carves/carves agent start --json", readme, StringComparison.Ordinal);
        Assert.Contains("carves up <target-project>", quickstart, StringComparison.Ordinal);
        Assert.Contains(".carves/carves agent start --json", quickstart, StringComparison.Ordinal);

        Assert.Contains("HumanNextAction", upCommand, StringComparison.Ordinal);
        Assert.Contains("AgentStartPrompt", upCommand, StringComparison.Ordinal);
        Assert.Contains("AgentInstruction", upCommand, StringComparison.Ordinal);
        Assert.Contains("Next for you:", upCommand, StringComparison.Ordinal);
        Assert.Contains("Next for the agent:", upCommand, StringComparison.Ordinal);
        Assert.Contains("human start prompt", help, StringComparison.Ordinal);
        Assert.Contains("If the operator says `start CARVES`", bootstrapPack, StringComparison.Ordinal);
        Assert.Contains("human_start_prompt", bootstrapPack, StringComparison.Ordinal);
        Assert.Contains("human_next_action", r96, StringComparison.Ordinal);

        Assert.Contains("root.GetProperty(\"human_next_action\")", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("Up_TextOutputReportsHumanAndAgentNextSteps", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("Next for you:", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("Next for the agent:", friendlyTests, StringComparison.Ordinal);
    }

    [Fact]
    public void UX3HostRootStatusPointer_BindsBrowserRootToHostRunningGuidance()
    {
        var repoRoot = ResolveRepoRoot();
        var contract = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-ux3-host-root-status-pointer.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));
        var quickstart = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "guides",
            "CARVES_EXTERNAL_AGENT_QUICKSTART.md"));
        var routing = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Host",
            "LocalHostServer.Routing.cs"));
        var hostTests = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "Carves.Runtime.IntegrationTests",
            "HostClientSurfaceTests.HostLifecycle.cs"));

        Assert.Contains("Status: carves_ux3_host_root_status_pointer", contract, StringComparison.Ordinal);
        Assert.Contains("GET /", contract, StringComparison.Ordinal);
        Assert.Contains("Accept: text/html", contract, StringComparison.Ordinal);
        Assert.Contains("human_start_prompt = start CARVES", contract, StringComparison.Ordinal);
        Assert.Contains("null_worker_current_version_no_api_sdk_worker_execution", contract, StringComparison.Ordinal);

        Assert.Contains("CARVES Host is running", readme, StringComparison.Ordinal);
        Assert.Contains("不是 dashboard", readme, StringComparison.Ordinal);
        Assert.Contains("CARVES Host is running", quickstart, StringComparison.Ordinal);
        Assert.Contains("not the dashboard", quickstart, StringComparison.Ordinal);

        Assert.Contains("RequestPrefersHtml", routing, StringComparison.Ordinal);
        Assert.Contains("RenderRootStatusHtml", routing, StringComparison.Ordinal);
        Assert.Contains("host_running_status_pointer_not_dashboard", routing, StringComparison.Ordinal);
        Assert.Contains("human_start_prompt", routing, StringComparison.Ordinal);
        Assert.Contains("agent_instruction", routing, StringComparison.Ordinal);
        Assert.Contains("does not dispatch worker automation", routing, StringComparison.Ordinal);

        Assert.Contains("HostRootRoute_WhenBrowserAcceptsHtml_ShowsStatusPointerNotDashboard", hostTests, StringComparison.Ordinal);
        Assert.Contains("Accept.ParseAdd(\"text/html\")", hostTests, StringComparison.Ordinal);
        Assert.Contains("not the product dashboard", hostTests, StringComparison.Ordinal);
    }

    [Fact]
    public void UX4VisibleStartPointer_BindsTargetRootDiscoveryToCarvesStart()
    {
        var repoRoot = ResolveRepoRoot();
        var contract = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-ux4-visible-start-pointer.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));
        var quickstart = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "guides",
            "CARVES_EXTERNAL_AGENT_QUICKSTART.md"));
        var bootstrapPack = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Application",
            "Platform",
            "RuntimeTargetAgentBootstrapPackService.cs"));
        var surfaceModel = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Application",
            "Platform",
            "SurfaceModels",
            "RuntimeTargetAgentBootstrapPackSurface.cs"));
        var upCommand = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Cli",
            "FriendlyCliApplication.Up.cs"));
        var friendlyTests = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "Carves.Runtime.IntegrationTests",
            "FriendlyCliEntryTests.cs"));
        var bootstrapTests = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "Carves.Runtime.Application.Tests",
            "RuntimeGovernedAgentHandoffServicesTests.DistributionAndTargetBinding.cs"));

        Assert.Contains("Status: carves_ux4_visible_start_pointer", contract, StringComparison.Ordinal);
        Assert.Contains("CARVES_START.md", contract, StringComparison.Ordinal);
        Assert.Contains("target-owned `AGENTS.md`", contract, StringComparison.Ordinal);
        Assert.Contains("null_worker_current_version_no_api_sdk_worker_execution", contract, StringComparison.Ordinal);

        Assert.Contains("CARVES_START.md", readme, StringComparison.Ordinal);
        Assert.Contains("CARVES_START.md", quickstart, StringComparison.Ordinal);

        Assert.Contains("VisibleAgentStartPath", bootstrapPack, StringComparison.Ordinal);
        Assert.Contains("BuildVisibleAgentStartContent", bootstrapPack, StringComparison.Ordinal);
        Assert.Contains("visible_start_file", bootstrapPack, StringComparison.Ordinal);
        Assert.Contains("VisibleAgentStartExists", surfaceModel, StringComparison.Ordinal);
        Assert.Contains("VisibleAgentStart", upCommand, StringComparison.Ordinal);
        Assert.Contains("Visible start pointer:", upCommand, StringComparison.Ordinal);

        Assert.Contains("visible_agent_start_exists", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("CARVES_START.md", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("CARVES_START.md", bootstrapTests, StringComparison.Ordinal);
    }

    [Fact]
    public void UX5CarvesUpIdempotentRerun_SkipsReattachForInitializedTargets()
    {
        var repoRoot = ResolveRepoRoot();
        var contract = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-ux5-idempotent-rerun.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));
        var quickstart = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "guides",
            "CARVES_EXTERNAL_AGENT_QUICKSTART.md"));
        var upCommand = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Cli",
            "FriendlyCliApplication.Up.cs"));
        var friendlyTests = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "Carves.Runtime.IntegrationTests",
            "FriendlyCliEntryTests.cs"));

        Assert.Contains("Status: carves_ux5_idempotent_rerun", contract, StringComparison.Ordinal);
        Assert.Contains("already-initialized", contract, StringComparison.Ordinal);
        Assert.Contains("skips re-attach", contract, StringComparison.Ordinal);
        Assert.Contains("CARVES_START.md", contract, StringComparison.Ordinal);
        Assert.Contains("null_worker_current_version_no_api_sdk_worker_execution", contract, StringComparison.Ordinal);

        Assert.Contains("rerun", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("generated but not committed", quickstart, StringComparison.Ordinal);

        Assert.Contains("RenderBootstrapUpResult", upCommand, StringComparison.Ordinal);
        Assert.Contains("runtimeBefore, \"initialized\"", upCommand, StringComparison.Ordinal);
        Assert.Contains("runtimeAfter: runtimeBefore", upCommand, StringComparison.Ordinal);

        Assert.Contains("Up_WhenTargetAlreadyInitializedAndBootstrapFilesAreUncommitted_IsIdempotent", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("dirty_target", friendlyTests, StringComparison.Ordinal);
    }

    [Fact]
    public void UX6CopyPasteAgentStartPrompt_BindsUpOutputToVisibleTargetInstructions()
    {
        var repoRoot = ResolveRepoRoot();
        var contract = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-ux6-copy-paste-agent-start.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));
        var quickstart = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "guides",
            "CARVES_EXTERNAL_AGENT_QUICKSTART.md"));
        var upCommand = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Cli",
            "FriendlyCliApplication.Up.cs"));
        var bootstrapPack = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Application",
            "Platform",
            "RuntimeTargetAgentBootstrapPackService.cs"));
        var friendlyTests = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "Carves.Runtime.IntegrationTests",
            "FriendlyCliEntryTests.cs"));

        Assert.Contains("Status: carves_ux6_copy_paste_agent_start", contract, StringComparison.Ordinal);
        Assert.Contains("Copy/paste to your agent", contract, StringComparison.Ordinal);
        Assert.Contains("agent_start_copy_paste_prompt", contract, StringComparison.Ordinal);
        Assert.Contains("copy_paste_prompt", contract, StringComparison.Ordinal);
        Assert.Contains("older CARVES-generated", contract, StringComparison.Ordinal);
        Assert.Contains("Do not plan or edit before that readback.", contract, StringComparison.Ordinal);
        Assert.Contains("null_worker_current_version_no_api_sdk_worker_execution", contract, StringComparison.Ordinal);

        Assert.Contains("Copy/paste prompt", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not plan or edit before that readback.", quickstart, StringComparison.Ordinal);

        Assert.Contains("UpAgentStartCopyPastePrompt", upCommand, StringComparison.Ordinal);
        Assert.Contains("AgentStartCopyPastePrompt", upCommand, StringComparison.Ordinal);
        Assert.Contains("Copy/paste to your agent:", upCommand, StringComparison.Ordinal);

        Assert.Contains("AgentStartCopyPastePrompt", bootstrapPack, StringComparison.Ordinal);
        Assert.Contains("Copy/Paste Prompt", bootstrapPack, StringComparison.Ordinal);
        Assert.Contains("copy_paste_prompt", bootstrapPack, StringComparison.Ordinal);

        Assert.Contains("agent_start_copy_paste_prompt", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("Copy/paste to your agent:", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("copy_paste_prompt", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("Old generated start packet", friendlyTests, StringComparison.Ordinal);
    }

    [Fact]
    public void UX7_0AgentStartedBootstrap_BindsRuntimeRootStartFileWithoutAgentClassification()
    {
        var repoRoot = ResolveRepoRoot();
        var contract = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-ux7.0-agent-started-bootstrap.md"));
        var startFile = File.ReadAllText(Path.Combine(repoRoot, "START_CARVES.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));
        var quickstart = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "guides",
            "CARVES_EXTERNAL_AGENT_QUICKSTART.md"));

        Assert.Contains("Status: carves_ux7_0_agent_started_bootstrap", contract, StringComparison.Ordinal);
        Assert.Contains("START_CARVES.md", contract, StringComparison.Ordinal);
        Assert.Contains("target classification belongs to `carves up`", contract, StringComparison.Ordinal);
        Assert.Contains("null_worker_current_version_no_api_sdk_worker_execution", contract, StringComparison.Ordinal);

        Assert.Contains("This file is the CARVES Runtime root entry", startFile, StringComparison.Ordinal);
        Assert.Contains("Confirm you know the absolute path", startFile, StringComparison.Ordinal);
        Assert.Contains("Treat the directory containing this file as `runtime_root`", startFile, StringComparison.Ordinal);
        Assert.Contains("<runtime_root>/carves up <target_project>", startFile, StringComparison.Ordinal);
        Assert.Contains(".carves/carves agent start --json", startFile, StringComparison.Ordinal);
        Assert.Contains("Do not classify the target as new or old by yourself.", startFile, StringComparison.Ordinal);
        Assert.Contains("Do not infer `runtime_root` from memory, PATH, shell history, sibling folders, or a broad filesystem search.", startFile, StringComparison.Ordinal);
        Assert.Contains("carves up` owns target classification", startFile, StringComparison.Ordinal);
        Assert.Contains("Do not dispatch worker automation", startFile, StringComparison.Ordinal);

        Assert.Contains("START_CARVES.md", readme, StringComparison.Ordinal);
        Assert.Contains("旧项目/新项目分类由 `carves up` 自己判断", readme, StringComparison.Ordinal);
        Assert.Contains("START_CARVES.md", quickstart, StringComparison.Ordinal);
        Assert.Contains("The agent should not search for CARVES globally", quickstart, StringComparison.Ordinal);
    }

    [Fact]
    public void UX7_1AgentStartClassificationFields_BindOldNewProjectHandlingToCarvesUp()
    {
        var repoRoot = ResolveRepoRoot();
        var contract = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-ux7.1-agent-start-classification-fields.md"));
        var startFile = File.ReadAllText(Path.Combine(repoRoot, "START_CARVES.md"));
        var upCommand = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Cli",
            "FriendlyCliApplication.Up.cs"));
        var bootstrapPack = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Application",
            "Platform",
            "RuntimeTargetAgentBootstrapPackService.cs"));
        var friendlyTests = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "Carves.Runtime.IntegrationTests",
            "FriendlyCliEntryTests.cs"));

        Assert.Contains("Status: carves_ux7_1_agent_start_classification_fields", contract, StringComparison.Ordinal);
        Assert.Contains("target_project_classification", contract, StringComparison.Ordinal);
        Assert.Contains("target_classification_owner", contract, StringComparison.Ordinal);
        Assert.Contains("agent_target_classification_allowed", contract, StringComparison.Ordinal);
        Assert.Contains("existing_carves_project", contract, StringComparison.Ordinal);
        Assert.Contains("null_worker_current_version_no_api_sdk_worker_execution", contract, StringComparison.Ordinal);

        Assert.Contains("target_project_classification", startFile, StringComparison.Ordinal);
        Assert.Contains("existing_project_handling", startFile, StringComparison.Ordinal);

        Assert.Contains("TargetProjectClassification", upCommand, StringComparison.Ordinal);
        Assert.Contains("TargetClassificationOwner: \"carves_up\"", upCommand, StringComparison.Ordinal);
        Assert.Contains("AgentTargetClassificationAllowed: false", upCommand, StringComparison.Ordinal);
        Assert.Contains("existing_carves_project", upCommand, StringComparison.Ordinal);
        Assert.Contains("newly_attached_git_project", upCommand, StringComparison.Ordinal);
        Assert.Contains("preserved_existing_carves_project_no_reinit", upCommand, StringComparison.Ordinal);

        Assert.Contains("target_classification_owner", bootstrapPack, StringComparison.Ordinal);
        Assert.Contains("agent_target_classification_allowed", bootstrapPack, StringComparison.Ordinal);
        Assert.Contains("Do not classify this project as new or old yourself", bootstrapPack, StringComparison.Ordinal);

        Assert.Contains("target_project_classification", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("existing_carves_project", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("newly_attached_git_project", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("agent_target_classification_allowed", friendlyTests, StringComparison.Ordinal);
    }

    [Fact]
    public void UX7_2RuntimeBindingRebindGate_BlocksAgentRepairForMismatchedOldProjects()
    {
        var repoRoot = ResolveRepoRoot();
        var contract = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-ux7.2-runtime-binding-rebind-gate.md"));
        var startFile = File.ReadAllText(Path.Combine(repoRoot, "START_CARVES.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));
        var quickstart = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "guides",
            "CARVES_EXTERNAL_AGENT_QUICKSTART.md"));
        var upCommand = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Cli",
            "FriendlyCliApplication.Up.cs"));
        var bindingResolver = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Application",
            "Platform",
            "RuntimeTargetBindingReadbackResolver.cs"));
        var bootstrapPack = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Application",
            "Platform",
            "RuntimeTargetAgentBootstrapPackService.cs"));
        var friendlyTests = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "Carves.Runtime.IntegrationTests",
            "FriendlyCliEntryTests.cs"));

        Assert.Contains("Status: carves_ux7_2_runtime_binding_rebind_gate", contract, StringComparison.Ordinal);
        Assert.Contains("target_bound_runtime_root", contract, StringComparison.Ordinal);
        Assert.Contains("target_runtime_binding_status", contract, StringComparison.Ordinal);
        Assert.Contains("agent_runtime_rebind_allowed=false", contract, StringComparison.Ordinal);
        Assert.Contains("action=rebind_required", contract, StringComparison.Ordinal);
        Assert.Contains("null_worker_current_version_no_api_sdk_worker_execution", contract, StringComparison.Ordinal);

        Assert.Contains("target_runtime_binding_status", startFile, StringComparison.Ordinal);
        Assert.Contains("Do not edit `.ai/runtime.json` or `.ai/runtime/attach-handshake.json`", startFile, StringComparison.Ordinal);
        Assert.Contains("rebind_required", readme, StringComparison.Ordinal);
        Assert.Contains("rebind_required", quickstart, StringComparison.Ordinal);

        Assert.Contains("RuntimeTargetBindingReadbackResolver.Resolve", upCommand, StringComparison.Ordinal);
        Assert.Contains("TargetRuntimeBindingStatus", upCommand, StringComparison.Ordinal);
        Assert.Contains("AgentRuntimeRebindAllowed: false", upCommand, StringComparison.Ordinal);
        Assert.Contains("runtime_binding_conflicts_with_{expectedRuntimeRootKind}", bindingResolver, StringComparison.Ordinal);
        Assert.Contains("action: \"rebind_required\"", upCommand, StringComparison.Ordinal);

        Assert.Contains("agent_runtime_rebind_allowed", bootstrapPack, StringComparison.Ordinal);
        Assert.Contains("runtime_binding_rule", bootstrapPack, StringComparison.Ordinal);
        Assert.Contains("Do not repair Runtime binding by hand", bootstrapPack, StringComparison.Ordinal);

        Assert.Contains("Up_WhenExistingTargetIsBoundToDifferentRuntimeRoot_RequiresOperatorRebind", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("runtime_binding_mismatch", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("operator_rebind_required_agent_must_stop", friendlyTests, StringComparison.Ordinal);
    }

    [Fact]
    public void UX7_3AgentStartReadback_ProjectsStartupClassificationAndBindingBoundary()
    {
        var repoRoot = ResolveRepoRoot();
        var contract = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-ux7.3-agent-start-readback-boundary.md"));
        var threadStartSurface = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Application",
            "Platform",
            "SurfaceModels",
            "RuntimeAgentThreadStartSurface.cs"));
        var threadStartService = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Application",
            "Platform",
            "RuntimeAgentThreadStartService.cs"));
        var bindingResolver = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Application",
            "Platform",
            "RuntimeTargetBindingReadbackResolver.cs"));
        var bootstrapPack = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Application",
            "Platform",
            "RuntimeTargetAgentBootstrapPackService.cs"));
        var quickstart = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "guides",
            "CARVES_EXTERNAL_AGENT_QUICKSTART.md"));
        var tests = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "Carves.Runtime.Application.Tests",
            "RuntimeGovernedAgentHandoffServicesTests.AgentThreadStart.cs"));

        Assert.Contains("Status: carves_ux7_3_agent_start_readback_boundary", contract, StringComparison.Ordinal);
        Assert.Contains("startup_entry_source", contract, StringComparison.Ordinal);
        Assert.Contains("target_project_classification", contract, StringComparison.Ordinal);
        Assert.Contains("target_runtime_binding_status", contract, StringComparison.Ordinal);
        Assert.Contains("agent_runtime_rebind_allowed=false", contract, StringComparison.Ordinal);
        Assert.Contains("null_worker_current_version_no_api_sdk_worker_execution", contract, StringComparison.Ordinal);

        Assert.Contains("StartupEntrySource", threadStartSurface, StringComparison.Ordinal);
        Assert.Contains("TargetProjectClassification", threadStartSurface, StringComparison.Ordinal);
        Assert.Contains("TargetRuntimeBindingStatus", threadStartSurface, StringComparison.Ordinal);
        Assert.Contains("AgentRuntimeRebindAllowed", threadStartSurface, StringComparison.Ordinal);

        Assert.Contains("BuildStartupReadback", threadStartService, StringComparison.Ordinal);
        Assert.Contains("RuntimeTargetBindingReadbackResolver.Resolve", threadStartService, StringComparison.Ordinal);
        Assert.Contains("runtime_binding_matches_{expectedRuntimeRootKind}", bindingResolver, StringComparison.Ordinal);
        Assert.Contains("operator_rebind_required_agent_must_stop", threadStartService, StringComparison.Ordinal);

        Assert.Contains("agent_start_readback_fields", bootstrapPack, StringComparison.Ordinal);
        Assert.Contains("target_bound_runtime_root", bootstrapPack, StringComparison.Ordinal);
        Assert.Contains("agent_runtime_rebind_allowed", quickstart, StringComparison.Ordinal);
        Assert.Contains("AgentThreadStart_ProjectsTargetStartupClassificationAndRuntimeBindingBoundary", tests, StringComparison.Ordinal);
    }

    [Fact]
    public void UX7_4AgentStartStartupBoundaryGate_BlocksUnsafeStartupReadbacks()
    {
        var repoRoot = ResolveRepoRoot();
        var contract = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-ux7.4-agent-start-startup-boundary-gate.md"));
        var threadStartSurface = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Application",
            "Platform",
            "SurfaceModels",
            "RuntimeAgentThreadStartSurface.cs"));
        var threadStartService = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Application",
            "Platform",
            "RuntimeAgentThreadStartService.cs"));
        var formatter = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Application",
            "ControlPlane",
            "OperatorSurfaceFormatter.RuntimeAgentThreadStart.cs"));
        var bootstrapPack = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Application",
            "Platform",
            "RuntimeTargetAgentBootstrapPackService.cs"));
        var quickstart = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "guides",
            "CARVES_EXTERNAL_AGENT_QUICKSTART.md"));
        var tests = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "Carves.Runtime.Application.Tests",
            "RuntimeGovernedAgentHandoffServicesTests.AgentThreadStart.cs"));

        Assert.Contains("Status: carves_ux7_4_agent_start_startup_boundary_gate", contract, StringComparison.Ordinal);
        Assert.Contains("startup_boundary_ready", contract, StringComparison.Ordinal);
        Assert.Contains("thread_start_ready=false", contract, StringComparison.Ordinal);
        Assert.Contains("available_actions", contract, StringComparison.Ordinal);
        Assert.Contains("null_worker_current_version_no_api_sdk_worker_execution", contract, StringComparison.Ordinal);

        Assert.Contains("StartupBoundaryReady", threadStartSurface, StringComparison.Ordinal);
        Assert.Contains("StartupBoundaryPosture", threadStartSurface, StringComparison.Ordinal);
        Assert.Contains("StartupBoundaryGaps", threadStartSurface, StringComparison.Ordinal);
        Assert.Contains("Startup boundary ready", formatter, StringComparison.Ordinal);

        Assert.Contains("runtimeReadbacksReady && startupReadback.StartupBoundaryReady", threadStartService, StringComparison.Ordinal);
        Assert.Contains("startup_boundary:", threadStartService, StringComparison.Ordinal);
        Assert.Contains("target_startup_blocked_by_runtime_binding", threadStartService, StringComparison.Ordinal);
        Assert.Contains("AvailableActions = ready && discussionFirstSurface", threadStartService, StringComparison.Ordinal);
        Assert.Contains("Stop. Show `target_runtime_binding_status`", threadStartService, StringComparison.Ordinal);

        Assert.Contains("startup_boundary_ready", bootstrapPack, StringComparison.Ordinal);
        Assert.Contains("startup_boundary_ready", quickstart, StringComparison.Ordinal);
        Assert.Contains("AgentThreadStart_BlocksWhenTargetRuntimeBindingNeedsOperatorRebind", tests, StringComparison.Ordinal);
    }

    [Fact]
    public void G0VisibleGatewayContract_BindsStartupVisibilityWithoutExecutionAuthority()
    {
        var repoRoot = ResolveRepoRoot();
        var contract = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-g0-visible-gateway-contract.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));
        var quickstart = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "guides",
            "CARVES_EXTERNAL_AGENT_QUICKSTART.md"));
        var startFile = File.ReadAllText(Path.Combine(repoRoot, "START_CARVES.md"));
        var help = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Cli",
            "FriendlyCliApplication.Help.cs"));

        Assert.Contains("Status: carves_g0_visible_gateway_contract", contract, StringComparison.Ordinal);
        Assert.Contains("G0 does not claim implementation", contract, StringComparison.Ordinal);
        Assert.Contains("`carves` with no args", contract, StringComparison.Ordinal);
        Assert.Contains("`carves up <target-project>`", contract, StringComparison.Ordinal);
        Assert.Contains("`carves gateway`", contract, StringComparison.Ordinal);
        Assert.Contains("`carves gateway status`", contract, StringComparison.Ordinal);
        Assert.Contains("`carves status --watch`", contract, StringComparison.Ordinal);
        Assert.Contains("Host `/`", contract, StringComparison.Ordinal);
        Assert.Contains("not_dashboard", contract, StringComparison.Ordinal);
        Assert.Contains("runtime_root", contract, StringComparison.Ordinal);
        Assert.Contains("target_project", contract, StringComparison.Ordinal);
        Assert.Contains("heartbeat", contract, StringComparison.Ordinal);
        Assert.Contains("locator/dispatcher", contract, StringComparison.Ordinal);
        Assert.Contains(".carves/carves agent start --json", contract, StringComparison.Ordinal);
        Assert.Contains("null_worker_current_version_no_api_sdk_worker_execution", contract, StringComparison.Ordinal);
        Assert.Contains("worker execution authority", contract, StringComparison.Ordinal);

        Assert.Contains("runtime-project-recenter-carves-g0-visible-gateway-contract.md", readme, StringComparison.Ordinal);
        Assert.Contains("carves status --watch", readme, StringComparison.Ordinal);
        Assert.Contains("不是 worker execution authority", readme, StringComparison.Ordinal);

        Assert.Contains("runtime-project-recenter-carves-g0-visible-gateway-contract.md", quickstart, StringComparison.Ordinal);
        Assert.Contains("carves status --watch", quickstart, StringComparison.Ordinal);
        Assert.Contains("does not authorize API/SDK worker execution", quickstart, StringComparison.Ordinal);

        Assert.Contains("Visible Gateway Expectation", startFile, StringComparison.Ordinal);
        Assert.Contains("carves gateway status", startFile, StringComparison.Ordinal);
        Assert.Contains("running/waiting/blocked heartbeat", startFile, StringComparison.Ordinal);
        Assert.Contains("worker execution", startFile, StringComparison.Ordinal);
        Assert.Contains("lifecycle truth authority", startFile, StringComparison.Ordinal);

        Assert.Contains("carves gateway <serve|start|ensure|reconcile|restart|status|doctor|logs|activity|stop|pause|resume>", help, StringComparison.Ordinal);
        Assert.Contains("carves status", help, StringComparison.Ordinal);
    }

    [Fact]
    public void G1NoArgsLanding_BindsCarvesCommandToProductStartupPanel()
    {
        var repoRoot = ResolveRepoRoot();
        var contract = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-g1-no-args-landing.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));
        var help = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Cli",
            "FriendlyCliApplication.Help.cs"));
        var friendlyTests = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "Carves.Runtime.IntegrationTests",
            "FriendlyCliEntryTests.cs"));

        Assert.Contains("Status: carves_g1_no_args_landing", contract, StringComparison.Ordinal);
        Assert.Contains("`carves` with no arguments now shows", contract, StringComparison.Ordinal);
        Assert.Contains("Start CARVES in a project:", contract, StringComparison.Ordinal);
        Assert.Contains("carves up <target-project>", contract, StringComparison.Ordinal);
        Assert.Contains("then open the target project and say: start CARVES", contract, StringComparison.Ordinal);
        Assert.Contains("Visible gateway:", contract, StringComparison.Ordinal);
        Assert.Contains("carves gateway serve", contract, StringComparison.Ordinal);
        Assert.Contains("carves gateway status", contract, StringComparison.Ordinal);
        Assert.Contains("carves status --watch", contract, StringComparison.Ordinal);
        Assert.Contains("not worker execution authority", contract, StringComparison.Ordinal);
        Assert.Contains("global `carves` executable as lifecycle truth authority", contract, StringComparison.Ordinal);
        Assert.Contains("carves help all", contract, StringComparison.Ordinal);

        Assert.Contains("G1 已把 `carves` 空命令改成产品起步面板", readme, StringComparison.Ordinal);
        Assert.Contains("G1 changes `carves` with no arguments into a product landing panel", readme, StringComparison.Ordinal);
        Assert.Contains("完整内部命令参考在 `carves help all`", readme, StringComparison.Ordinal);

        Assert.Contains("Start CARVES in a project:", help, StringComparison.Ordinal);
        Assert.Contains("carves up <target-project>", help, StringComparison.Ordinal);
        Assert.Contains("Visible gateway:", help, StringComparison.Ordinal);
        Assert.Contains("carves gateway serve", help, StringComparison.Ordinal);
        Assert.Contains("carves gateway status", help, StringComparison.Ordinal);
        Assert.Contains("carves status --watch", help, StringComparison.Ordinal);
        Assert.Contains("not worker execution authority", help, StringComparison.Ordinal);
        Assert.Contains("global carves is a locator/dispatcher, not lifecycle truth", help, StringComparison.Ordinal);
        Assert.Contains("Essential commands:", help, StringComparison.Ordinal);
        Assert.Contains("carves help all", help, StringComparison.Ordinal);
        Assert.Contains("WriteAllCommandReference", help, StringComparison.Ordinal);

        Assert.Contains("NoArgs_ShowsProductLandingAndVisibleGatewayBoundary", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("HelpAll_ShowsCanonicalVerbFamiliesAndCompatibilityAliases", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("DoesNotContain(\"Not a git repository.\"", friendlyTests, StringComparison.Ordinal);
    }

    [Fact]
    public void G2DefaultGatewayServe_BindsGatewayNoArgsToForegroundTerminal()
    {
        var repoRoot = ResolveRepoRoot();
        var contract = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-g2-default-gateway-serve.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));
        var hostOperations = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Host",
            "Program.HostOperations.cs"));
        var hostBoundary = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Host",
            "Program.HostBoundary.cs"));
        var help = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Cli",
            "FriendlyCliApplication.Help.cs"));
        var friendlyTests = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "Carves.Runtime.IntegrationTests",
            "FriendlyCliEntryTests.cs"));

        Assert.Contains("Status: carves_g2_default_gateway_serve", contract, StringComparison.Ordinal);
        Assert.Contains("G2 makes the visible gateway command concrete", contract, StringComparison.Ordinal);
        Assert.Contains("subcommand now means the same foreground terminal mode", contract, StringComparison.Ordinal);
        Assert.Contains("carves gateway serve", contract, StringComparison.Ordinal);
        Assert.Contains("[carves-gateway]", contract, StringComparison.Ordinal);
        Assert.Contains("Gateway ready: True", contract, StringComparison.Ordinal);
        Assert.Contains("Already running: True", contract, StringComparison.Ordinal);
        Assert.Contains("Next action: carves gateway status --json", contract, StringComparison.Ordinal);
        Assert.Contains("null_worker_current_version_no_api_sdk_worker_execution", contract, StringComparison.Ordinal);

        Assert.Contains("G2 已把 `carves gateway` 设为前台 gateway 终端的默认入口", readme, StringComparison.Ordinal);
        Assert.Contains("G2 makes `carves gateway` the default foreground gateway terminal", readme, StringComparison.Ordinal);

        Assert.Contains("ResolveGatewayDefaultServeArguments", hostOperations, StringComparison.Ordinal);
        Assert.Contains("return [\"serve\"]", hostOperations, StringComparison.Ordinal);
        Assert.Contains("? [\"serve\", .. commandLine.Arguments]", hostOperations, StringComparison.Ordinal);

        Assert.Contains("carves gateway with no subcommand is the foreground gateway terminal.", help, StringComparison.Ordinal);
        Assert.Contains("carves gateway with no subcommand is the foreground gateway terminal.", hostBoundary, StringComparison.Ordinal);

        Assert.Contains("GatewayNoArgs_WhenGatewayAlreadyRunning_UsesForegroundServeBoundary", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("Next action: carves gateway status --json", friendlyTests, StringComparison.Ordinal);
    }

    [Fact]
    public void G3StatusWatchHeartbeat_BindsStatusWatchToReadOnlyVisibility()
    {
        var repoRoot = ResolveRepoRoot();
        var contract = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-g3-status-watch-heartbeat.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));
        var commands = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Cli",
            "FriendlyCliApplication.Commands.cs"));
        var runtimeInteraction = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Cli",
            "FriendlyCliApplication.RuntimeInteraction.cs"));
        var help = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Cli",
            "FriendlyCliApplication.Help.cs"));
        var friendlyTests = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "Carves.Runtime.IntegrationTests",
            "FriendlyCliEntryTests.cs"));

        Assert.Contains("Status: carves_g3_status_watch_heartbeat", contract, StringComparison.Ordinal);
        Assert.Contains("carves status --watch", contract, StringComparison.Ordinal);
        Assert.Contains("=== CARVES Status <timestamp> ===", contract, StringComparison.Ordinal);
        Assert.Contains("carves status --watch --iterations 1 --interval-ms 0", contract, StringComparison.Ordinal);
        Assert.Contains("silently start a gateway", contract, StringComparison.Ordinal);
        Assert.Contains("null_worker_current_version_no_api_sdk_worker_execution", contract, StringComparison.Ordinal);

        Assert.Contains("G3 已把 `carves status --watch` 做成只读状态心跳", readme, StringComparison.Ordinal);
        Assert.Contains("G3 makes `carves status --watch` a read-only status heartbeat", readme, StringComparison.Ordinal);

        Assert.Contains("RunStatusWatch", commands, StringComparison.Ordinal);
        Assert.Contains("=== CARVES Status", commands, StringComparison.Ordinal);
        Assert.Contains("FilterStatusArguments", commands, StringComparison.Ordinal);
        Assert.Contains("DelayCliWatch", commands, StringComparison.Ordinal);
        Assert.Contains("CliWatchDelayGate", runtimeInteraction, StringComparison.Ordinal);

        Assert.Contains("Usage: carves status [--watch] [--iterations <n>] [--interval-ms <ms>]", help, StringComparison.Ordinal);
        Assert.Contains("Read-only CARVES status", help, StringComparison.Ordinal);
        Assert.Contains("does not start worker execution", help, StringComparison.Ordinal);

        Assert.Contains("StatusWatch_OneIterationShowsReadableHeartbeatWithoutStartingGateway", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("StatusHelp_DescribesReadOnlyWatchHeartbeat", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("DoesNotContain(\"Started resident host process\"", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("DoesNotContain(\"task run\"", friendlyTests, StringComparison.Ordinal);
    }

    [Fact]
    public void G4HostRootBrowserStatus_BindsRootPathToBrowserFirstStatusPointer()
    {
        var repoRoot = ResolveRepoRoot();
        var contract = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-g4-host-root-browser-status.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));
        var routing = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Host",
            "LocalHostServer.Routing.cs"));
        var hostTests = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "Carves.Runtime.IntegrationTests",
            "HostClientSurfaceTests.HostLifecycle.cs"));

        Assert.Contains("Status: carves_g4_host_root_browser_status", contract, StringComparison.Ordinal);
        Assert.Contains("CARVES Host is running", contract, StringComparison.Ordinal);
        Assert.Contains("/?format=json", contract, StringComparison.Ordinal);
        Assert.Contains("Requests that explicitly prefer `application/json` also remain JSON", contract, StringComparison.Ordinal);
        Assert.Contains("generic `*/*` receive the browser status", contract, StringComparison.Ordinal);
        Assert.Contains("Unknown host route", contract, StringComparison.Ordinal);
        Assert.Contains("null_worker_current_version_no_api_sdk_worker_execution", contract, StringComparison.Ordinal);

        Assert.Contains("G4 已把 Host 根路径 `/` 做成浏览器默认状态页", readme, StringComparison.Ordinal);
        Assert.Contains("G4 makes the Host root `/` a browser-first status page", readme, StringComparison.Ordinal);

        Assert.Contains("request.QueryString[\"format\"]", routing, StringComparison.Ordinal);
        Assert.Contains("\"json\"", routing, StringComparison.Ordinal);
        Assert.Contains("\"html\"", routing, StringComparison.Ordinal);
        Assert.Contains("acceptTypes.Length == 0", routing, StringComparison.Ordinal);
        Assert.Contains("type.Contains(\"*/*\"", routing, StringComparison.Ordinal);
        Assert.Contains("type.Contains(\"application/json\"", routing, StringComparison.Ordinal);
        Assert.Contains("RenderRootStatusHtml", routing, StringComparison.Ordinal);
        Assert.Contains("CARVES Host is running", routing, StringComparison.Ordinal);

        Assert.Contains("HostRootRoute_DefaultRequestShowsHtmlStatusPointerNotJsonError", hostTests, StringComparison.Ordinal);
        Assert.Contains("HostRootRoute_ReportsRunningStatusAndAgentEntryWithoutDashboardRequirement", hostTests, StringComparison.Ordinal);
        Assert.Contains("/?format=json", hostTests, StringComparison.Ordinal);
        Assert.Contains("DoesNotContain(\"Unknown host route\"", hostTests, StringComparison.Ordinal);
        Assert.Contains("DoesNotContain(\"\\\"error\\\"\"", hostTests, StringComparison.Ordinal);
    }

    [Fact]
    public void G5GlobalShimGuidance_BindsGlobalCarvesToLocatorOnly()
    {
        var repoRoot = ResolveRepoRoot();
        var contract = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-g5-global-shim-guidance.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));
        var startFile = File.ReadAllText(Path.Combine(repoRoot, "START_CARVES.md"));
        var quickstart = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "guides",
            "CARVES_EXTERNAL_AGENT_QUICKSTART.md"));
        var friendlyCli = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Cli",
            "FriendlyCliApplication.cs"));
        var help = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Cli",
            "FriendlyCliApplication.Help.cs"));
        var friendlyTests = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "Carves.Runtime.IntegrationTests",
            "FriendlyCliEntryTests.cs"));

        Assert.Contains("Status: carves_g5_global_shim_guidance", contract, StringComparison.Ordinal);
        Assert.Contains("carves shim", contract, StringComparison.Ordinal);
        Assert.Contains("CARVES_RUNTIME_ROOT", contract, StringComparison.Ordinal);
        Assert.Contains("exec \"$CARVES_RUNTIME_ROOT/carves\" \"$@\"", contract, StringComparison.Ordinal);
        Assert.Contains("& \"$env:CARVES_RUNTIME_ROOT\\carves\" @args", contract, StringComparison.Ordinal);
        Assert.Contains("does not install files", contract, StringComparison.Ordinal);
        Assert.Contains("mutate PATH", contract, StringComparison.Ordinal);
        Assert.Contains("operator must explicitly create", contract, StringComparison.Ordinal);
        Assert.Contains(".carves/carves agent start --json", contract, StringComparison.Ordinal);
        Assert.Contains("not lifecycle truth", contract, StringComparison.Ordinal);
        Assert.Contains("not worker execution", contract, StringComparison.Ordinal);
        Assert.Contains("null_worker_current_version_no_api_sdk_worker_execution", contract, StringComparison.Ordinal);

        Assert.Contains("G5 已加入 `carves shim` / `carves help shim`", readme, StringComparison.Ordinal);
        Assert.Contains("G5 adds `carves shim` / `carves help shim`", readme, StringComparison.Ordinal);
        Assert.Contains("操作者确认 Runtime root 后显式创建 shim", readme, StringComparison.Ordinal);
        Assert.Contains("<runtime_root>/carves shim", startFile, StringComparison.Ordinal);
        Assert.Contains("It is guidance only", startFile, StringComparison.Ordinal);
        Assert.Contains("carves shim` / `carves help shim`", quickstart, StringComparison.Ordinal);

        Assert.Contains("return RunUp", friendlyCli, StringComparison.Ordinal);
        Assert.Contains("WriteSubcommandHelp(\"shim\")", friendlyCli, StringComparison.Ordinal);
        Assert.Contains("Global shim:", help, StringComparison.Ordinal);
        Assert.Contains("Usage: carves shim", help, StringComparison.Ordinal);
        Assert.Contains("does not install files or mutate PATH", help, StringComparison.Ordinal);
        Assert.Contains("operator should create the shim explicitly", help, StringComparison.Ordinal);
        Assert.Contains("CARVES_RUNTIME_ROOT", help, StringComparison.Ordinal);
        Assert.Contains("exec \\\"<runtime_root>/carves\\\" \\\"$@\\\"", help, StringComparison.Ordinal);
        Assert.Contains("& \\\"<runtime_root>/carves\\\" @args", help, StringComparison.Ordinal);
        Assert.Contains("not lifecycle truth", help, StringComparison.Ordinal);
        Assert.Contains("not worker execution authority", help, StringComparison.Ordinal);

        Assert.Contains("Shim_DescribesGlobalLocatorWithoutMutatingPathOrAuthority", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("ShimHelp_UsesSameGlobalLocatorGuidance", friendlyTests, StringComparison.Ordinal);
        Assert.Contains("DoesNotContain(\"Not a git repository.\"", friendlyTests, StringComparison.Ordinal);
    }

    [Fact]
    public void G6AgentVisibleGatewayGuidance_BindsTargetAgentToProjectLocalLauncher()
    {
        var repoRoot = ResolveRepoRoot();
        var contract = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "runtime-project-recenter-carves-g6-agent-visible-gateway-guidance.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));
        var quickstart = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "guides",
            "CARVES_EXTERNAL_AGENT_QUICKSTART.md"));
        var bootstrapGuide = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "guides",
            "CARVES_TARGET_AGENT_BOOTSTRAP_PACK.md"));
        var bootstrapPack = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Application",
            "Platform",
            "RuntimeTargetAgentBootstrapPackService.cs"));
        var agentThreadStart = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "CARVES.Runtime.Application",
            "Platform",
            "RuntimeAgentThreadStartService.cs"));
        var applicationTests = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "Carves.Runtime.Application.Tests",
            "RuntimeGovernedAgentHandoffServicesTests.DistributionAndTargetBinding.cs"));
        var agentThreadStartTests = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "Carves.Runtime.Application.Tests",
            "RuntimeGovernedAgentHandoffServicesTests.AgentThreadStart.cs"));
        var friendlyTests = File.ReadAllText(Path.Combine(
            repoRoot,
            "tests",
            "Carves.Runtime.IntegrationTests",
            "FriendlyCliEntryTests.cs"));

        Assert.Contains("Status: carves_g6_agent_visible_gateway_guidance", contract, StringComparison.Ordinal);
        Assert.Contains(".carves/carves agent start --json", contract, StringComparison.Ordinal);
        Assert.Contains(".carves/carves gateway status", contract, StringComparison.Ordinal);
        Assert.Contains(".carves/carves status --watch --iterations 1 --interval-ms 0", contract, StringComparison.Ordinal);
        Assert.Contains(".carves/carves gateway", contract, StringComparison.Ordinal);
        Assert.Contains("Do not treat global carves as authority inside the target", contract, StringComparison.Ordinal);
        Assert.Contains("null_worker_current_version_no_api_sdk_worker_execution", contract, StringComparison.Ordinal);

        Assert.Contains("G6 已把目标项目内的 Agent 指示补齐", readme, StringComparison.Ordinal);
        Assert.Contains("G6 tightens the target-project agent instructions", readme, StringComparison.Ordinal);
        Assert.Contains(".carves/carves gateway status", quickstart, StringComparison.Ordinal);
        Assert.Contains(".carves/carves status --watch --iterations 1 --interval-ms 0", quickstart, StringComparison.Ordinal);
        Assert.Contains("Inside a target project, prefer `.carves/carves`", bootstrapGuide, StringComparison.Ordinal);

        Assert.Contains("ProjectLocalGatewayStatusCommand", bootstrapPack, StringComparison.Ordinal);
        Assert.Contains("visible_gateway_commands", bootstrapPack, StringComparison.Ordinal);
        Assert.Contains("global_shim_rule", bootstrapPack, StringComparison.Ordinal);
        Assert.Contains("## Is CARVES Running?", bootstrapPack, StringComparison.Ordinal);
        Assert.Contains("Do not use a global `carves` shim as authority inside this target", bootstrapPack, StringComparison.Ordinal);

        Assert.Contains("project-local launcher recorded by the start packet", agentThreadStart, StringComparison.Ordinal);
        Assert.Contains("target-local visibility readbacks", agentThreadStart, StringComparison.Ordinal);
        Assert.Contains(".carves/carves gateway status", agentThreadStart, StringComparison.Ordinal);

        Assert.Contains("visible_gateway_commands", applicationTests, StringComparison.Ordinal);
        Assert.Contains("global_shim_rule", applicationTests, StringComparison.Ordinal);
        Assert.Contains("target-bound `.carves/carves`", agentThreadStartTests, StringComparison.Ordinal);
        Assert.Contains("foreground_gateway_command", friendlyTests, StringComparison.Ordinal);
    }

    private static readonly string[] RequiredScenarioIds =
    [
        "empty_target",
        "existing_root_agents",
        "no_global_alias",
        "host_already_running",
        "host_not_running",
        "stale_host_conflict",
    ];

    private static readonly string[] RequiredTestRefs =
    [
        "TargetAgentBootstrapPack_MaterializesMinimalRootAgentsWhenAbsent",
        "TargetAgentBootstrapPack_MaterializesMissingFilesWithoutOverwritingRootAgents",
        "GatewayServe_FailsClosedWhenGatewayIsAlreadyRunning",
        "HostRootRoute_ReportsRunningStatusAndAgentEntryWithoutDashboardRequirement",
        "GatewayStatus_ProjectsGatewayBoundaryWithoutStartingHost",
        "Run_WithHostTransportWithoutHost_ShowsFriendlyHostEnsureGuidance",
        "GatewayServe_FailsClosedWhenAliveStaleDescriptorExists",
    ];

    private static readonly string[] RequiredBoundaries =
    [
        "no dashboard requirement",
        "no global alias requirement",
        "no API/SDK worker execution",
        "no target source mutation during bootstrap",
        "no overwrite of existing target `AGENTS.md`",
        "project-local launcher points to the selected Runtime root",
        "null_worker_current_version_no_api_sdk_worker_execution",
    ];

    private static string ResolveRepoRoot()
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

        throw new InvalidOperationException("Could not locate CARVES.Runtime repository root.");
    }

    private static void AssertAppearsBefore(string text, string earlier, string later)
    {
        var earlierIndex = text.IndexOf(earlier, StringComparison.Ordinal);
        var laterIndex = text.IndexOf(later, StringComparison.Ordinal);

        Assert.True(earlierIndex >= 0, $"Missing expected text: {earlier}");
        Assert.True(laterIndex >= 0, $"Missing expected text: {later}");
        Assert.True(earlierIndex < laterIndex, $"Expected '{earlier}' to appear before '{later}'.");
    }
}

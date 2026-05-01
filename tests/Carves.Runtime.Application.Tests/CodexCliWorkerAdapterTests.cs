using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using System.Diagnostics;

namespace Carves.Runtime.Application.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class CodexCliWorkerAdapterTests
{
    [Fact]
    public void CodexCliWorkerAdapter_MapsJsonEventStreamIntoWorkerResult()
    {
        using var workspace = new TemporaryWorkspace();
        var cliPath = CreateFakeCodexCli(workspace);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        try
        {
            var registry = TestWorkerAdapterRegistryFactory.Create("codex");
            var adapter = registry.TryGetByBackendId("codex_cli");

            Assert.NotNull(adapter);
            Assert.True(adapter!.IsConfigured);
            Assert.Equal("codex_cli", adapter.BackendId);
            Assert.Equal(WorkerBackendHealthState.Healthy, adapter.CheckHealth().State);

            var repoRoot = workspace.WriteFile("repo/README.md", "seed");
            var repoDirectory = Path.GetDirectoryName(repoRoot)!;
            var worktreeDirectory = Path.Combine(repoDirectory, "worktree");
            Directory.CreateDirectory(worktreeDirectory);
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-CODEX-CLI-DIRECT",
                Title = "Codex CLI direct task",
                Description = "Ensure the CLI adapter maps JSON events.",
                Instructions = "Follow the contract.",
                Input = "Write the worker summary only.",
                RepoRoot = repoDirectory,
                WorktreeRoot = worktreeDirectory,
                BaseCommit = "abc123",
                ModelOverride = "gpt-5-codex",
                Profile = new WorkerExecutionProfile
                {
                    ProfileId = "workspace_build_test",
                    DisplayName = "Workspace Build Test",
                    Description = "Test profile",
                    SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                    ApprovalMode = WorkerApprovalMode.Never,
                },
            };

            var result = adapter.Execute(request);

            Assert.Equal(WorkerExecutionStatus.Succeeded, result.Status);
            Assert.Equal("codex_cli", result.BackendId);
            Assert.Equal("codex", result.ProviderId);
            Assert.Equal("CodexCliWorkerAdapter", result.AdapterId);
            Assert.Equal("cli worker success", result.Summary);
            Assert.Equal("cli-direct-run", result.ThreadId);
            Assert.Equal(WorkerThreadContinuity.NewThread, result.ThreadContinuity);
            Assert.Contains(result.ChangedFiles, item => item.EndsWith("README.md", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Events, item => item.EventType == WorkerEventType.RunStarted);
            Assert.Contains(result.Events, item => item.EventType == WorkerEventType.CommandExecuted);
            Assert.Contains(result.Events, item => item.EventType == WorkerEventType.FileEditObserved);
            Assert.Contains(result.Events, item => item.EventType == WorkerEventType.FinalSummary);
            Assert.True(result.CommandTrace.Count >= 3);
            Assert.Contains(result.CommandTrace, item => item.Category == "preflight" && item.Command.SequenceEqual(new[] { "dotnet", "--info" }));
            Assert.Contains(result.CommandTrace, item => item.Category == "worker" && item.ExitCode == 0);
            Assert.Equal(21, result.InputTokens);
            Assert.Equal(9, result.OutputTokens);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
        }
    }

    [Fact]
    public void CodexCliWorkerAdapter_CodexCliDefaultModelDoesNotPassUnsupportedModelOverride()
    {
        using var workspace = new TemporaryWorkspace();
        var cliPath = CreateFakeCodexCli(workspace);
        var capturePath = Path.Combine(workspace.RootPath, "captured-args.txt");
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var originalCapturePath = Environment.GetEnvironmentVariable("CARVES_TEST_CAPTURE_ARGS");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        Environment.SetEnvironmentVariable("CARVES_TEST_CAPTURE_ARGS", capturePath);
        try
        {
            var registry = TestWorkerAdapterRegistryFactory.Create("codex");
            var adapter = registry.TryGetByBackendId("codex_cli");
            Assert.NotNull(adapter);

            var repoRoot = workspace.WriteFile("repo/README.md", "seed");
            var repoDirectory = Path.GetDirectoryName(repoRoot)!;
            var worktreeDirectory = Path.Combine(repoDirectory, "worktree");
            Directory.CreateDirectory(worktreeDirectory);
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-CODEX-CLI-DEFAULT-MODEL",
                Title = "Codex CLI default model",
                Description = "Ensure CLI-default model binding does not emit an unsupported explicit model argument.",
                Instructions = "Follow the contract.",
                Input = "Write the worker summary only.",
                RepoRoot = repoDirectory,
                WorktreeRoot = worktreeDirectory,
                BaseCommit = "abc123",
                ModelOverride = "codex-cli",
                Profile = new WorkerExecutionProfile
                {
                    ProfileId = "workspace_build_test",
                    DisplayName = "Workspace Build Test",
                    Description = "Test profile",
                    SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                    ApprovalMode = WorkerApprovalMode.Never,
                },
            };

            var result = adapter!.Execute(request);

            Assert.Equal(WorkerExecutionStatus.Succeeded, result.Status);
            Assert.Equal("codex-cli", result.Model);
            var capturedArgs = ReadAllTextWithRetry(capturePath);
            Assert.DoesNotContain("--model", capturedArgs, StringComparison.Ordinal);
            Assert.DoesNotContain("gpt-5-codex", capturedArgs, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            Environment.SetEnvironmentVariable("CARVES_TEST_CAPTURE_ARGS", originalCapturePath);
        }
    }

    [Fact]
    public void CodexCliWorkerAdapter_ObservesActualChangedFilesFromGitStatus()
    {
        using var workspace = new TemporaryWorkspace();
        var cliPath = CreateWorkspaceWriteWithoutFileChangeFakeCodexCli(workspace);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        try
        {
            var registry = TestWorkerAdapterRegistryFactory.Create("codex");
            var adapter = registry.TryGetByBackendId("codex_cli");

            Assert.NotNull(adapter);

            var repoRoot = workspace.WriteFile("repo/README.md", "seed");
            var repoDirectory = Path.GetDirectoryName(repoRoot)!;
            var worktreeDirectory = Path.Combine(repoDirectory, "worktree");
            Directory.CreateDirectory(worktreeDirectory);
            RunGit(worktreeDirectory, "init");
            File.WriteAllText(Path.Combine(worktreeDirectory, ".carves-worktree.json"), "{}");
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-CODEX-CLI-ACTUAL-CHANGES",
                Title = "Codex CLI actual changes",
                Description = "Ensure git status is used as observed changed-file provenance.",
                Instructions = "Follow the contract.",
                Input = "Write a file without reporting changed_files.",
                RepoRoot = repoDirectory,
                WorktreeRoot = worktreeDirectory,
                BaseCommit = "abc123",
                ModelOverride = "gpt-5-codex",
                Profile = new WorkerExecutionProfile
                {
                    ProfileId = "workspace_build_test",
                    DisplayName = "Workspace Build Test",
                    Description = "Test profile",
                    SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                    ApprovalMode = WorkerApprovalMode.Never,
                },
            };

            var result = adapter!.Execute(request);

            Assert.Equal(WorkerExecutionStatus.Succeeded, result.Status);
            Assert.Empty(result.ChangedFiles);
            Assert.Contains("src/Observed.cs", result.ObservedChangedFiles, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(".carves-worktree.json", result.ObservedChangedFiles, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(
                result.Events,
                item => item.EventType == WorkerEventType.FileEditObserved
                        && string.Equals(item.ItemType, "git_status_observed_file_change", StringComparison.Ordinal)
                        && string.Equals(item.FilePath, "src/Observed.cs", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
        }
    }

    [Fact]
    public void CodexCliWorkerAdapter_ResumedThreadIsCapturedAsExecutionTruth()
    {
        using var workspace = new TemporaryWorkspace();
        var cliPath = CreateFakeCodexCli(workspace);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        try
        {
            var registry = TestWorkerAdapterRegistryFactory.Create("codex");
            var adapter = registry.TryGetByBackendId("codex_cli");

            Assert.NotNull(adapter);

            var repoRoot = workspace.WriteFile("repo/README.md", "seed");
            var repoDirectory = Path.GetDirectoryName(repoRoot)!;
            var worktreeDirectory = Path.Combine(repoDirectory, "worktree");
            Directory.CreateDirectory(worktreeDirectory);
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-CODEX-CLI-RESUME",
                Title = "Codex CLI resumed task",
                Description = "Ensure resumed thread ids are preserved.",
                Instructions = "Follow the contract.",
                Input = "Continue the previous thread.",
                RepoRoot = repoDirectory,
                WorktreeRoot = worktreeDirectory,
                BaseCommit = "abc123",
                PriorThreadId = "cli-direct-run",
                ModelOverride = "gpt-5-codex",
                Profile = new WorkerExecutionProfile
                {
                    ProfileId = "workspace_build_test",
                    DisplayName = "Workspace Build Test",
                    Description = "Test profile",
                    SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                    ApprovalMode = WorkerApprovalMode.Never,
                },
            };

            var result = adapter!.Execute(request);

            Assert.Equal(WorkerExecutionStatus.Succeeded, result.Status);
            Assert.Equal("cli-direct-run", result.PriorThreadId);
            Assert.Equal("cli-direct-run", result.ThreadId);
            Assert.Equal(WorkerThreadContinuity.ResumedThread, result.ThreadContinuity);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
        }
    }

    [Fact]
    public void CodexCliWorkerAdapter_NormalizesUnsupportedReasoningEffortBeforeCliDispatch()
    {
        using var workspace = new TemporaryWorkspace();
        var cliPath = CreateFakeCodexCli(workspace);
        var capturePath = Path.Combine(workspace.RootPath, "captured-args.txt");
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var originalCapturePath = Environment.GetEnvironmentVariable("CARVES_TEST_CAPTURE_ARGS");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        Environment.SetEnvironmentVariable("CARVES_TEST_CAPTURE_ARGS", capturePath);
        try
        {
            var registry = TestWorkerAdapterRegistryFactory.Create("codex");
            var adapter = registry.TryGetByBackendId("codex_cli");

            Assert.NotNull(adapter);

            var repoRoot = workspace.WriteFile("repo/README.md", "seed");
            var repoDirectory = Path.GetDirectoryName(repoRoot)!;
            var worktreeDirectory = Path.Combine(repoDirectory, "worktree");
            Directory.CreateDirectory(worktreeDirectory);
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-CODEX-CLI-NORMALIZE",
                Title = "Normalize reasoning effort",
                Description = "Ensure unsupported reasoning effort values are normalized for the CLI.",
                Instructions = "Follow the contract.",
                Input = "Write the worker summary only.",
                RepoRoot = repoDirectory,
                WorktreeRoot = worktreeDirectory,
                BaseCommit = "abc123",
                ModelOverride = "gpt-5-codex",
                ReasoningEffort = "xhigh",
                Profile = new WorkerExecutionProfile
                {
                    ProfileId = "workspace_build_test",
                    DisplayName = "Workspace Build Test",
                    Description = "Test profile",
                    SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                    ApprovalMode = WorkerApprovalMode.Never,
                },
            };

            var result = adapter!.Execute(request);

            Assert.Equal(WorkerExecutionStatus.Succeeded, result.Status);
            Assert.True(File.Exists(capturePath));
            var capturedArgs = ReadAllTextWithRetry(capturePath);
            Assert.Contains("model_reasoning_effort=", capturedArgs, StringComparison.Ordinal);
            Assert.True(
                capturedArgs.Contains("model_reasoning_effort=\"high\"", StringComparison.Ordinal)
                || capturedArgs.Contains("model_reasoning_effort=\\\"high\\\"", StringComparison.Ordinal)
                || capturedArgs.Contains("model_reasoning_effort=high", StringComparison.Ordinal),
                $"Expected normalized reasoning effort override in captured args but found: {capturedArgs}");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            Environment.SetEnvironmentVariable("CARVES_TEST_CAPTURE_ARGS", originalCapturePath);
        }
    }

    [Fact]
    public void CodexCliWorkerAdapter_InjectsDelegatedLaunchEnvironmentAndPreflightTrace()
    {
        using var workspace = new TemporaryWorkspace();
        var cliPath = CreateFakeCodexCli(workspace);
        var capturePath = Path.Combine(workspace.RootPath, "captured-env.txt");
        var argsCapturePath = Path.Combine(workspace.RootPath, "captured-args.txt");
        var nugetPackagesPath = Path.Combine(workspace.RootPath, "nuget-packages");
        Directory.CreateDirectory(nugetPackagesPath);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var originalCapturePath = Environment.GetEnvironmentVariable("CARVES_TEST_CAPTURE_ENV");
        var originalArgsCapturePath = Environment.GetEnvironmentVariable("CARVES_TEST_CAPTURE_ARGS");
        var originalNuGetPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        Environment.SetEnvironmentVariable("CARVES_TEST_CAPTURE_ENV", capturePath);
        Environment.SetEnvironmentVariable("CARVES_TEST_CAPTURE_ARGS", argsCapturePath);
        Environment.SetEnvironmentVariable("NUGET_PACKAGES", nugetPackagesPath);
        try
        {
            var registry = TestWorkerAdapterRegistryFactory.Create("codex");
            var adapter = registry.TryGetByBackendId("codex_cli");
            Assert.NotNull(adapter);
            var expectedHome = OperatingSystem.IsWindows()
                ? Environment.GetEnvironmentVariable("USERPROFILE")
                : Environment.GetEnvironmentVariable("HOME");

            var repoRoot = workspace.WriteFile("repo/README.md", "seed");
            var repoDirectory = Path.GetDirectoryName(repoRoot)!;
            var worktreeDirectory = Path.Combine(repoDirectory, "worktree");
            Directory.CreateDirectory(worktreeDirectory);
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-CODEX-CLI-PREFLIGHT",
                Title = "Delegated launch preflight",
                Description = "Ensure delegated worker launch uses a deterministic environment contract.",
                Instructions = "Follow the contract.",
                Input = "Write the worker summary only.",
                RepoRoot = repoDirectory,
                WorktreeRoot = worktreeDirectory,
                BaseCommit = "abc123",
                ValidationCommands = [["dotnet", "test", "tests/Ordering.UnitTests/Ordering.UnitTests.csproj", "--no-restore"]],
                Profile = new WorkerExecutionProfile
                {
                    ProfileId = "workspace_build_test",
                    DisplayName = "Workspace Build Test",
                    Description = "Test profile",
                    SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                    ApprovalMode = WorkerApprovalMode.Never,
                },
            };

            var result = adapter!.Execute(request);

            Assert.Equal(WorkerExecutionStatus.Succeeded, result.Status);
            Assert.Contains(result.CommandTrace, item => item.Category == "preflight" && item.Command.SequenceEqual(new[] { "dotnet", "--info" }));
            Assert.Contains(result.Events, item => item.ItemType == "preflight_contract");
            var envCapture = ReadAllTextWithRetry(capturePath);
            Assert.Contains("DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1", envCapture, StringComparison.Ordinal);
            Assert.Contains("DOTNET_CLI_TELEMETRY_OPTOUT=1", envCapture, StringComparison.Ordinal);
            Assert.Contains("DOTNET_NOLOGO=1", envCapture, StringComparison.Ordinal);
            Assert.Contains("DOTNET_GENERATE_ASPNET_CERTIFICATE=false", envCapture, StringComparison.Ordinal);
            Assert.Contains("DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false", envCapture, StringComparison.Ordinal);
            Assert.Contains("DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1", envCapture, StringComparison.Ordinal);
            Assert.Contains("DOTNET_CLI_HOME=", envCapture, StringComparison.Ordinal);
            Assert.Contains($"NUGET_PACKAGES={nugetPackagesPath}", envCapture, StringComparison.Ordinal);
            Assert.Contains("CARVES_FORMAL_VALIDATION_OWNER=runtime", envCapture, StringComparison.Ordinal);
            var toolRootLine = envCapture.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Single(line => line.StartsWith("CARVES_DELEGATED_TOOL_ROOT=", StringComparison.Ordinal));
            var toolRoot = toolRootLine["CARVES_DELEGATED_TOOL_ROOT=".Length..];
            Assert.False(string.IsNullOrWhiteSpace(toolRoot));
            var dotNetShimPath = OperatingSystem.IsWindows()
                ? Path.Combine(toolRoot, "dotnet.cmd")
                : Path.Combine(toolRoot, "dotnet");
            var gitShimPath = OperatingSystem.IsWindows()
                ? Path.Combine(toolRoot, "git.cmd")
                : Path.Combine(toolRoot, "git");
            Assert.True(File.Exists(dotNetShimPath), $"Expected delegated dotnet shim at '{dotNetShimPath}'.");
            Assert.True(File.Exists(gitShimPath), $"Expected delegated git shim at '{gitShimPath}'.");
            if (!string.IsNullOrWhiteSpace(expectedHome))
            {
                var homeKey = OperatingSystem.IsWindows() ? "USERPROFILE" : "HOME";
                Assert.Contains($"{homeKey}={expectedHome}", envCapture, StringComparison.Ordinal);
            }

            var capturedArgs = ReadAllTextWithRetry(argsCapturePath);
            Assert.Contains("--add-dir", capturedArgs, StringComparison.Ordinal);
            Assert.Contains(nugetPackagesPath, capturedArgs, StringComparison.Ordinal);
            Assert.DoesNotContain("--model", capturedArgs, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            Environment.SetEnvironmentVariable("CARVES_TEST_CAPTURE_ENV", originalCapturePath);
            Environment.SetEnvironmentVariable("CARVES_TEST_CAPTURE_ARGS", originalArgsCapturePath);
            Environment.SetEnvironmentVariable("NUGET_PACKAGES", originalNuGetPackages);
        }
    }

    [Fact]
    public void CodexCliWorkerAdapter_ExplainsRepoRootAiAccessWhenDelegatedWorktreeOmitsControlPlaneTruth()
    {
        using var workspace = new TemporaryWorkspace();
        var cliPath = CreateFakeCodexCli(workspace);
        var stdinCapturePath = Path.Combine(workspace.RootPath, "captured-stdin.txt");
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var originalStdinCapturePath = Environment.GetEnvironmentVariable("CARVES_TEST_CAPTURE_STDIN");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        Environment.SetEnvironmentVariable("CARVES_TEST_CAPTURE_STDIN", stdinCapturePath);
        try
        {
            var registry = TestWorkerAdapterRegistryFactory.Create("codex");
            var adapter = registry.TryGetByBackendId("codex_cli");
            Assert.NotNull(adapter);

            var repoRoot = workspace.WriteFile("repo/README.md", "seed");
            var repoDirectory = Path.GetDirectoryName(repoRoot)!;
            workspace.WriteFile("repo/.ai/memory/architecture/00_AI_ENTRY_PROTOCOL.md", "entry");
            var taskTruthPath = workspace.WriteFile("repo/.ai/tasks/nodes/T-CARD-314-002.json", "{}");
            var cardTruthPath = workspace.WriteFile("repo/.ai/tasks/cards/CARD-314.md", "# card");
            var worktreeDirectory = Path.Combine(repoDirectory, "worktree");
            Directory.CreateDirectory(worktreeDirectory);
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-CARD-314-002",
                Title = "Delegated repo-root ai guidance",
                Description = "Ensure the prompt explains how to access .ai truth outside the delegated snapshot.",
                Instructions = "Follow the contract.",
                Input = "Read the required guidance and summarize the result.",
                RepoRoot = repoDirectory,
                WorktreeRoot = worktreeDirectory,
                BaseCommit = "abc123",
                Profile = new WorkerExecutionProfile
                {
                    ProfileId = "workspace_build_test",
                    DisplayName = "Workspace Build Test",
                    Description = "Test profile",
                    SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                    ApprovalMode = WorkerApprovalMode.Never,
                },
            };

            var result = adapter!.Execute(request);

            Assert.Equal(WorkerExecutionStatus.Succeeded, result.Status);
            var capturedPrompt = ReadAllTextWithRetry(stdinCapturePath);
            Assert.Contains("Execution environment note:", capturedPrompt, StringComparison.Ordinal);
            Assert.Contains(repoDirectory, capturedPrompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Control-plane truth under `.ai/` is not materialized in the delegated worktree snapshot.", capturedPrompt, StringComparison.Ordinal);
            Assert.Contains("Host-governed cold initialization and task packet assembly are already satisfied for this delegated run", capturedPrompt, StringComparison.Ordinal);
            Assert.Contains("absolute paths under", capturedPrompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(taskTruthPath, capturedPrompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(cardTruthPath, capturedPrompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Do not enumerate `.ai/tasks/cards`, `.ai/tasks/nodes`", capturedPrompt, StringComparison.Ordinal);
            Assert.Contains("Treat the supplied context pack, execution packet, and direct task/card truth paths as the startup entry point for this run.", capturedPrompt, StringComparison.Ordinal);
            Assert.Contains("do not spend startup budget rereading `README.md`, `AGENTS.md`, `.ai/memory/architecture/*`, `.ai/PROJECT_BOUNDARY.md`, or `.ai/STATE.md`", capturedPrompt, StringComparison.Ordinal);
            Assert.Contains("Do not emit a `CARVES.AI initialization report`, `Agent bootstrap sources`, or a planning-only preamble in this delegated run", capturedPrompt, StringComparison.Ordinal);
            Assert.Contains("do not run `git status` or `git diff` there as routine patch verification", capturedPrompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Do not use `python3` as a bulk read harness", capturedPrompt, StringComparison.Ordinal);
            Assert.Contains("Read at most one or two directly relevant files per command", capturedPrompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("do not concatenate broad `sed -n` or `cat` readbacks across multiple changed files", capturedPrompt, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            Environment.SetEnvironmentVariable("CARVES_TEST_CAPTURE_STDIN", originalStdinCapturePath);
        }
    }

    [Fact]
    public void CodexCliWorkerAdapter_BlocksWhenDelegatedDotNetHomeIsNotWritableDirectory()
    {
        using var workspace = new TemporaryWorkspace();
        var cliPath = CreateFakeCodexCli(workspace);
        var invalidDotNetHome = workspace.WriteFile("bad-dotnet-home.txt", "not-a-directory");
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var originalDotNetHome = Environment.GetEnvironmentVariable("CARVES_DOTNET_CLI_HOME");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        Environment.SetEnvironmentVariable("CARVES_DOTNET_CLI_HOME", invalidDotNetHome);
        try
        {
            var registry = TestWorkerAdapterRegistryFactory.Create("codex");
            var adapter = registry.TryGetByBackendId("codex_cli");
            Assert.NotNull(adapter);

            var repoRoot = workspace.WriteFile("repo/README.md", "seed");
            var repoDirectory = Path.GetDirectoryName(repoRoot)!;
            var worktreeDirectory = Path.Combine(repoDirectory, "worktree");
            Directory.CreateDirectory(worktreeDirectory);
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-CODEX-CLI-PREFLIGHT-BLOCK",
                Title = "Delegated launch blocked",
                Description = "Ensure invalid dotnet home is rejected before execution.",
                Instructions = "Follow the contract.",
                Input = "Write the worker summary only.",
                RepoRoot = repoDirectory,
                WorktreeRoot = worktreeDirectory,
                BaseCommit = "abc123",
                Profile = new WorkerExecutionProfile
                {
                    ProfileId = "workspace_build_test",
                    DisplayName = "Workspace Build Test",
                    Description = "Test profile",
                    SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                    ApprovalMode = WorkerApprovalMode.Never,
                },
            };

            var result = adapter!.Execute(request);

            Assert.Equal(WorkerExecutionStatus.Blocked, result.Status);
            Assert.Equal(WorkerFailureKind.EnvironmentBlocked, result.FailureKind);
            Assert.Equal(WorkerFailureLayer.Environment, result.FailureLayer);
            Assert.Contains("preflight failed", result.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(result.Events, item => item.ItemType == "preflight_contract");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            Environment.SetEnvironmentVariable("CARVES_DOTNET_CLI_HOME", originalDotNetHome);
        }
    }

    [Fact]
    public void CodexCliWorkerAdapter_TimesOutHungExecAndReturnsRetryableFailure()
    {
        using var workspace = new TemporaryWorkspace();
        var cliPath = CreateHangingFakeCodexCli(workspace);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        try
        {
            var registry = TestWorkerAdapterRegistryFactory.Create("codex");
            var adapter = registry.TryGetByBackendId("codex_cli");
            Assert.NotNull(adapter);

            var repoRoot = workspace.WriteFile("repo/README.md", "seed");
            var repoDirectory = Path.GetDirectoryName(repoRoot)!;
            var worktreeDirectory = Path.Combine(repoDirectory, "worktree");
            Directory.CreateDirectory(worktreeDirectory);
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-CODEX-CLI-TIMEOUT",
                Title = "Codex CLI timeout task",
                Description = "Ensure hung delegated execution is converted into timeout truth.",
                Instructions = "Follow the contract.",
                Input = "Write the worker summary only.",
                RepoRoot = repoDirectory,
                WorktreeRoot = worktreeDirectory,
                BaseCommit = "abc123",
                TimeoutSeconds = 1,
                ModelOverride = "gpt-5-codex",
                Profile = new WorkerExecutionProfile
                {
                    ProfileId = "workspace_build_test",
                    DisplayName = "Workspace Build Test",
                    Description = "Test profile",
                    SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                    ApprovalMode = WorkerApprovalMode.Never,
                },
            };

            var stopwatch = Stopwatch.StartNew();
            var result = adapter!.Execute(request);
            stopwatch.Stop();

            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(4), $"Expected timeout execution to stop quickly, but elapsed was {stopwatch.Elapsed}.");
            Assert.Equal(WorkerExecutionStatus.Failed, result.Status);
            Assert.Equal(WorkerFailureKind.Timeout, result.FailureKind);
            Assert.Equal(WorkerFailureLayer.Transport, result.FailureLayer);
            Assert.True(result.Retryable);
            Assert.Equal("cli-timeout-run", result.ThreadId);
            Assert.Equal("execution_started", result.TimeoutPhase);
            Assert.Contains("after execution activity began", result.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("timed out", result.FailureReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Worker commands recorded: 1.", result.TimeoutEvidence ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains(result.Events, item => item.EventType == WorkerEventType.RunStarted);
            Assert.Contains(result.Events, item => item.EventType == WorkerEventType.CommandExecuted);
            Assert.Contains(result.CommandTrace, item => item.Category == "worker" && item.Command.SequenceEqual(new[] { "dotnet", "test" }));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
        }
    }

    [Fact]
    public void CodexCliWorkerAdapter_TimesOutBeforeFirstCliEventAndCapturesPhaseEvidence()
    {
        using var workspace = new TemporaryWorkspace();
        var cliPath = CreateSilentHangingFakeCodexCli(workspace);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        try
        {
            var registry = TestWorkerAdapterRegistryFactory.Create("codex");
            var adapter = registry.TryGetByBackendId("codex_cli");
            Assert.NotNull(adapter);

            var repoRoot = workspace.WriteFile("repo/README.md", "seed");
            var repoDirectory = Path.GetDirectoryName(repoRoot)!;
            var worktreeDirectory = Path.Combine(repoDirectory, "worktree");
            Directory.CreateDirectory(worktreeDirectory);
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-CODEX-CLI-TIMEOUT-PRE-EVENT",
                Title = "Codex CLI timeout before first event",
                Description = "Ensure a silent hang is captured as pre-event timeout evidence.",
                Instructions = "Follow the contract.",
                Input = "Write the worker summary only.",
                RepoRoot = repoDirectory,
                WorktreeRoot = worktreeDirectory,
                BaseCommit = "abc123",
                TimeoutSeconds = 1,
                Profile = new WorkerExecutionProfile
                {
                    ProfileId = "workspace_build_test",
                    DisplayName = "Workspace Build Test",
                    Description = "Test profile",
                    SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                    ApprovalMode = WorkerApprovalMode.Never,
                },
            };

            var result = adapter!.Execute(request);

            Assert.Equal(WorkerExecutionStatus.Failed, result.Status);
            Assert.Equal(WorkerFailureKind.Timeout, result.FailureKind);
            Assert.Equal("before_first_cli_event", result.TimeoutPhase);
            Assert.Contains("before the first meaningful CLI event", result.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Observed CLI events before timeout: none.", result.TimeoutEvidence ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(result.CommandTrace, item => item.Category == "worker");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
        }
    }

    [Fact]
    public void CodexCliWorkerAdapter_TimesOutDuringBootstrapAndCapturesPhaseEvidence()
    {
        using var workspace = new TemporaryWorkspace();
        var cliPath = CreateBootstrapHangingFakeCodexCli(workspace);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        try
        {
            var registry = TestWorkerAdapterRegistryFactory.Create("codex");
            var adapter = registry.TryGetByBackendId("codex_cli");
            Assert.NotNull(adapter);

            var repoRoot = workspace.WriteFile("repo/README.md", "seed");
            var repoDirectory = Path.GetDirectoryName(repoRoot)!;
            var worktreeDirectory = Path.Combine(repoDirectory, "worktree");
            Directory.CreateDirectory(worktreeDirectory);
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-CODEX-CLI-TIMEOUT-BOOTSTRAP",
                Title = "Codex CLI timeout during bootstrap",
                Description = "Ensure bootstrap-only progress is captured distinctly from execution progress.",
                Instructions = "Follow the contract.",
                Input = "Write the worker summary only.",
                RepoRoot = repoDirectory,
                WorktreeRoot = worktreeDirectory,
                BaseCommit = "abc123",
                TimeoutSeconds = 1,
                Profile = new WorkerExecutionProfile
                {
                    ProfileId = "workspace_build_test",
                    DisplayName = "Workspace Build Test",
                    Description = "Test profile",
                    SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                    ApprovalMode = WorkerApprovalMode.Never,
                },
            };

            var result = adapter!.Execute(request);

            Assert.Equal(WorkerExecutionStatus.Failed, result.Status);
            Assert.Equal(WorkerFailureKind.Timeout, result.FailureKind);
            Assert.Equal("bootstrap", result.TimeoutPhase);
            Assert.Contains("during bootstrap", result.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Observed CLI events before timeout: RunStarted, TurnStarted.", result.TimeoutEvidence ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains(result.Events, item => item.EventType == WorkerEventType.RunStarted);
            Assert.Contains(result.Events, item => item.EventType == WorkerEventType.TurnStarted);
            Assert.DoesNotContain(result.CommandTrace, item => item.Category == "worker");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
        }
    }

    [Fact]
    public void CodexCliWorkerAdapter_ExecutionStartedTimeoutDetectsNonConvergingControlPlaneRescan()
    {
        using var workspace = new TemporaryWorkspace();
        var cliPath = CreateControlPlaneRescanHangingFakeCodexCli(workspace);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        try
        {
            var registry = TestWorkerAdapterRegistryFactory.Create("codex");
            var adapter = registry.TryGetByBackendId("codex_cli");
            Assert.NotNull(adapter);

            var repoRoot = workspace.WriteFile("repo/README.md", "seed");
            var repoDirectory = Path.GetDirectoryName(repoRoot)!;
            var worktreeDirectory = Path.Combine(repoDirectory, "worktree");
            Directory.CreateDirectory(worktreeDirectory);
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-CARD-314-002",
                Title = "Codex CLI non-converging rescan timeout",
                Description = "Ensure read-only control-plane rescans are surfaced in timeout evidence.",
                Instructions = "Follow the contract.",
                Input = "Write the worker summary only.",
                RepoRoot = repoDirectory,
                WorktreeRoot = worktreeDirectory,
                BaseCommit = "abc123",
                TimeoutSeconds = 1,
                Profile = new WorkerExecutionProfile
                {
                    ProfileId = "workspace_build_test",
                    DisplayName = "Workspace Build Test",
                    Description = "Test profile",
                    SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                    ApprovalMode = WorkerApprovalMode.Never,
                },
            };

            var result = adapter!.Execute(request);

            Assert.Equal(WorkerExecutionStatus.Failed, result.Status);
            Assert.Equal(WorkerFailureKind.Timeout, result.FailureKind);
            Assert.Equal("execution_started", result.TimeoutPhase);
            Assert.Contains("Non-converging read-only control-plane rescan detected", result.TimeoutEvidence ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains(".ai/tasks/cards", result.TimeoutEvidence ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains("Changed files observed: 0.", result.TimeoutEvidence ?? string.Empty, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
        }
    }

    [Fact]
    public void CodexCliWorkerAdapter_ExecutionStartedTimeoutDetectsNonConvergingPostEditReadback()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new TemporaryWorkspace();
        var cliPath = CreatePostEditReadbackHangingFakeCodexCli(workspace);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        try
        {
            var registry = TestWorkerAdapterRegistryFactory.Create("codex");
            var adapter = registry.TryGetByBackendId("codex_cli");
            Assert.NotNull(adapter);

            var repoRoot = workspace.WriteFile("repo/README.md", "seed");
            var repoDirectory = Path.GetDirectoryName(repoRoot)!;
            var worktreeDirectory = Path.Combine(repoDirectory, "worktree");
            Directory.CreateDirectory(worktreeDirectory);
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-CODEX-CLI-POST-EDIT-READBACK-TIMEOUT",
                Title = "Codex CLI non-converging post-edit readback timeout",
                Description = "Ensure post-edit rereads across changed files are surfaced distinctly in timeout evidence.",
                Instructions = "Follow the contract.",
                Input = "Write the worker summary only.",
                RepoRoot = repoDirectory,
                WorktreeRoot = worktreeDirectory,
                BaseCommit = "abc123",
                TimeoutSeconds = 1,
                Profile = new WorkerExecutionProfile
                {
                    ProfileId = "workspace_build_test",
                    DisplayName = "Workspace Build Test",
                    Description = "Test profile",
                    SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                    ApprovalMode = WorkerApprovalMode.Never,
                },
            };

            var result = adapter!.Execute(request);

            Assert.Equal(WorkerExecutionStatus.Failed, result.Status);
            Assert.Equal(WorkerFailureKind.Timeout, result.FailureKind);
            Assert.Equal("execution_started", result.TimeoutPhase);
            Assert.Contains("Non-converging post-edit readback detected", result.TimeoutEvidence ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains("Changed files observed: 2.", result.TimeoutEvidence ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains("src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockLeaseSnapshot.cs", result.ChangedFiles, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
        }
    }

    [Fact]
    public void CodexCliWorkerAdapter_ExecutionStartedTimeoutDetectsNonConvergingSourceExploration()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new TemporaryWorkspace();
        var cliPath = CreateSourceExplorationHangingFakeCodexCli(workspace);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        try
        {
            var registry = TestWorkerAdapterRegistryFactory.Create("codex");
            var adapter = registry.TryGetByBackendId("codex_cli");
            Assert.NotNull(adapter);

            var repoRoot = workspace.WriteFile("repo/README.md", "seed");
            var repoDirectory = Path.GetDirectoryName(repoRoot)!;
            var worktreeDirectory = Path.Combine(repoDirectory, "worktree");
            Directory.CreateDirectory(worktreeDirectory);
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-CODEX-CLI-SOURCE-EXPLORATION-TIMEOUT",
                Title = "Codex CLI non-converging source exploration timeout",
                Description = "Ensure broad pre-edit source exploration is surfaced distinctly in timeout evidence.",
                Instructions = "Follow the contract.",
                Input = "Write the worker summary only.",
                RepoRoot = repoDirectory,
                WorktreeRoot = worktreeDirectory,
                BaseCommit = "abc123",
                TimeoutSeconds = 1,
                Profile = new WorkerExecutionProfile
                {
                    ProfileId = "workspace_build_test",
                    DisplayName = "Workspace Build Test",
                    Description = "Test profile",
                    SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                    ApprovalMode = WorkerApprovalMode.Never,
                },
            };

            var result = adapter!.Execute(request);

            Assert.Equal(WorkerExecutionStatus.Failed, result.Status);
            Assert.Equal(WorkerFailureKind.Timeout, result.FailureKind);
            Assert.Equal("execution_started", result.TimeoutPhase);
            Assert.Contains("Non-converging source exploration detected", result.TimeoutEvidence ?? string.Empty, StringComparison.Ordinal);
            Assert.Contains("Changed files observed: 0.", result.TimeoutEvidence ?? string.Empty, StringComparison.Ordinal);
            Assert.Empty(result.ChangedFiles);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
        }
    }

    [Fact]
    public void CodexCliWorkerAdapter_InfersChangedFilesFromShellSetContentCommands()
    {
        using var workspace = new TemporaryWorkspace();
        var cliPath = CreateShellWriteFakeCodexCli(workspace);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        try
        {
            var registry = TestWorkerAdapterRegistryFactory.Create("codex");
            var adapter = registry.TryGetByBackendId("codex_cli");
            Assert.NotNull(adapter);

            var repoRoot = workspace.WriteFile("repo/README.md", "seed");
            var repoDirectory = Path.GetDirectoryName(repoRoot)!;
            var worktreeDirectory = Path.Combine(repoDirectory, "worktree");
            Directory.CreateDirectory(worktreeDirectory);
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-CODEX-CLI-SHELL-WRITE",
                Title = "Infer shell writes",
                Description = "Ensure Set-Content commands are surfaced as changed files.",
                Instructions = "Follow the contract.",
                Input = "Write a file and summarize the result.",
                RepoRoot = repoDirectory,
                WorktreeRoot = worktreeDirectory,
                BaseCommit = "abc123",
                Profile = new WorkerExecutionProfile
                {
                    ProfileId = "workspace_build_test",
                    DisplayName = "Workspace Build Test",
                    Description = "Test profile",
                    SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                    ApprovalMode = WorkerApprovalMode.Never,
                },
            };

            var result = adapter!.Execute(request);

            Assert.Equal(WorkerExecutionStatus.Succeeded, result.Status);
            Assert.Contains("tests/Ordering.UnitTests/Application/CreateOrderDraftCommandHandlerTest.cs", result.ChangedFiles, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(
                result.Events,
                item => item.EventType == WorkerEventType.FileEditObserved
                        && string.Equals(item.FilePath, "tests/Ordering.UnitTests/Application/CreateOrderDraftCommandHandlerTest.cs", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
        }
    }

    [Fact]
    public void CodexCliWorkerAdapter_TreatsNoMatchRipgrepProbeAsNonFatal()
    {
        using var workspace = new TemporaryWorkspace();
        var cliPath = CreateRipgrepProbeFakeCodexCli(workspace);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        try
        {
            var registry = TestWorkerAdapterRegistryFactory.Create("codex");
            var adapter = registry.TryGetByBackendId("codex_cli");
            Assert.NotNull(adapter);

            var repoRoot = workspace.WriteFile("repo/README.md", "seed");
            var repoDirectory = Path.GetDirectoryName(repoRoot)!;
            var worktreeDirectory = Path.Combine(repoDirectory, "worktree");
            Directory.CreateDirectory(worktreeDirectory);
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-CODEX-CLI-RG-PROBE",
                Title = "Ripgrep probe should not fail the run",
                Description = "Ensure exit 1 from no-match rg probes does not poison an otherwise successful turn.",
                Instructions = "Follow the contract.",
                Input = "Inspect files and summarize the result.",
                RepoRoot = repoDirectory,
                WorktreeRoot = worktreeDirectory,
                BaseCommit = "abc123",
                Profile = new WorkerExecutionProfile
                {
                    ProfileId = "workspace_build_test",
                    DisplayName = "Workspace Build Test",
                    Description = "Test profile",
                    SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                    ApprovalMode = WorkerApprovalMode.Never,
                },
            };

            var result = adapter!.Execute(request);

            Assert.Equal(WorkerExecutionStatus.Succeeded, result.Status);
            Assert.Equal(WorkerFailureKind.None, result.FailureKind);
            Assert.Contains(result.CommandTrace, item => item.ExitCode == 1);
            Assert.Equal("rg probe success", result.Summary);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
        }
    }

    [Fact]
    public void CodexCliWorkerAdapter_TreatsGitStatusWithoutDotGitAsNonFatal()
    {
        using var workspace = new TemporaryWorkspace();
        var cliPath = CreateGitProbeFakeCodexCli(workspace);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        try
        {
            var registry = TestWorkerAdapterRegistryFactory.Create("codex");
            var adapter = registry.TryGetByBackendId("codex_cli");
            Assert.NotNull(adapter);

            var repoRoot = workspace.WriteFile("repo/README.md", "seed");
            var repoDirectory = Path.GetDirectoryName(repoRoot)!;
            var worktreeDirectory = Path.Combine(repoDirectory, "worktree");
            Directory.CreateDirectory(worktreeDirectory);
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-CODEX-CLI-GIT-PROBE",
                Title = "Git probe should not fail the run",
                Description = "Ensure git status in a managed snapshot without .git does not poison an otherwise successful turn.",
                Instructions = "Follow the contract.",
                Input = "Inspect files and summarize the result.",
                RepoRoot = repoDirectory,
                WorktreeRoot = worktreeDirectory,
                BaseCommit = "abc123",
                Profile = new WorkerExecutionProfile
                {
                    ProfileId = "workspace_build_test",
                    DisplayName = "Workspace Build Test",
                    Description = "Test profile",
                    SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                    ApprovalMode = WorkerApprovalMode.Never,
                },
            };

            var result = adapter!.Execute(request);

            Assert.Equal(WorkerExecutionStatus.Succeeded, result.Status);
            Assert.Equal(WorkerFailureKind.None, result.FailureKind);
            Assert.Contains(result.CommandTrace, item => item.ExitCode == 1);
            Assert.Equal("git probe success", result.Summary);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
        }
    }

    [Fact]
    public void CodexCliWorkerAdapter_InfersPythonWritePathsAndTreatsManagedWorktreeGitDiffAsNonFatal()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new TemporaryWorkspace();
        var cliPath = CreatePythonWriteGitDiffProbeFakeCodexCli(workspace);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        try
        {
            var registry = TestWorkerAdapterRegistryFactory.Create("codex");
            var adapter = registry.TryGetByBackendId("codex_cli");
            Assert.NotNull(adapter);

            var repoRoot = workspace.WriteFile("repo/README.md", "seed");
            var repoDirectory = Path.GetDirectoryName(repoRoot)!;
            var worktreeDirectory = Path.Combine(repoDirectory, "worktree");
            Directory.CreateDirectory(worktreeDirectory);
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-CODEX-CLI-PYTHON-WRITE-GIT-DIFF-PROBE",
                Title = "Python write with managed-worktree git diff probe",
                Description = "Ensure python write commands still surface changed files and non-git git diff warnings do not poison the run.",
                Instructions = "Follow the contract.",
                Input = "Edit files and summarize the result.",
                RepoRoot = repoDirectory,
                WorktreeRoot = worktreeDirectory,
                BaseCommit = "abc123",
                Profile = new WorkerExecutionProfile
                {
                    ProfileId = "workspace_build_test",
                    DisplayName = "Workspace Build Test",
                    Description = "Test profile",
                    SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                    ApprovalMode = WorkerApprovalMode.Never,
                },
            };

            var result = adapter!.Execute(request);

            Assert.Equal(WorkerExecutionStatus.Succeeded, result.Status);
            Assert.Equal(WorkerFailureKind.None, result.FailureKind);
            Assert.Contains("src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockLeaseSnapshot.cs", result.ChangedFiles, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockOptions.cs", result.ChangedFiles, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(
                result.Events,
                item => item.EventType == WorkerEventType.FileEditObserved
                        && string.Equals(item.FilePath, "src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockLeaseSnapshot.cs", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.CommandTrace, item => item.ExitCode == 129);
            Assert.Equal("python write git diff probe success", result.Summary);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
        }
    }

    [Fact]
    public void CodexCliWorkerAdapter_DoesNotTreatSourceTextContainingApprovalFieldAsApprovalWait()
    {
        using var workspace = new TemporaryWorkspace();
        var cliPath = CreateApprovalFalsePositiveFakeCodexCli(workspace);
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        try
        {
            var registry = TestWorkerAdapterRegistryFactory.Create("codex");
            var adapter = registry.TryGetByBackendId("codex_cli");
            Assert.NotNull(adapter);

            var repoRoot = workspace.WriteFile("repo/README.md", "seed");
            var repoDirectory = Path.GetDirectoryName(repoRoot)!;
            var worktreeDirectory = Path.Combine(repoDirectory, "worktree");
            Directory.CreateDirectory(worktreeDirectory);
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-CODEX-CLI-APPROVAL-FALSE-POSITIVE",
                Title = "Approval keyword false positive should not pause the run",
                Description = "Ensure source text containing approval field names does not classify a failed command as approval wait.",
                Instructions = "Follow the contract.",
                Input = "Inspect files and summarize the result.",
                RepoRoot = repoDirectory,
                WorktreeRoot = worktreeDirectory,
                BaseCommit = "abc123",
                Profile = new WorkerExecutionProfile
                {
                    ProfileId = "workspace_build_test",
                    DisplayName = "Workspace Build Test",
                    Description = "Test profile",
                    SandboxMode = WorkerSandboxMode.WorkspaceWrite,
                    ApprovalMode = WorkerApprovalMode.Never,
                },
            };

            var result = adapter!.Execute(request);

            Assert.Equal(WorkerExecutionStatus.Failed, result.Status);
            Assert.Equal(WorkerFailureKind.TaskLogicFailed, result.FailureKind);
            Assert.DoesNotContain(result.Events, item => item.EventType == WorkerEventType.ApprovalWait || item.EventType == WorkerEventType.PermissionRequested);
            Assert.Empty(result.PermissionRequests);
            Assert.Contains("source text failure", result.Summary, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
        }
    }

    private static string CreateFakeCodexCli(TemporaryWorkspace workspace)
    {
if (OperatingSystem.IsWindows())
        {
            return workspace.WriteFile("tools/fake-codex.cmd", """
@echo off
if not "%CARVES_TEST_CAPTURE_ARGS%"=="" (
  >> "%CARVES_TEST_CAPTURE_ARGS%" echo %*
)
if "%1"=="--version" (
  echo codex 0.0.0-test
  exit /b 0
)
  if "%1"=="exec" (
  if not "%CARVES_TEST_CAPTURE_STDIN%"=="" (
    more > "%CARVES_TEST_CAPTURE_STDIN%"
  ) else (
    set /p _CARVES_PROMPT=
  )
  if not "%CARVES_TEST_CAPTURE_ENV%"=="" (
    > "%CARVES_TEST_CAPTURE_ENV%" echo DOTNET_CLI_HOME=%DOTNET_CLI_HOME%
    >> "%CARVES_TEST_CAPTURE_ENV%" echo DOTNET_SKIP_FIRST_TIME_EXPERIENCE=%DOTNET_SKIP_FIRST_TIME_EXPERIENCE%
    >> "%CARVES_TEST_CAPTURE_ENV%" echo DOTNET_CLI_TELEMETRY_OPTOUT=%DOTNET_CLI_TELEMETRY_OPTOUT%
    >> "%CARVES_TEST_CAPTURE_ENV%" echo DOTNET_NOLOGO=%DOTNET_NOLOGO%
    >> "%CARVES_TEST_CAPTURE_ENV%" echo DOTNET_GENERATE_ASPNET_CERTIFICATE=%DOTNET_GENERATE_ASPNET_CERTIFICATE%
    >> "%CARVES_TEST_CAPTURE_ENV%" echo DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=%DOTNET_ADD_GLOBAL_TOOLS_TO_PATH%
    >> "%CARVES_TEST_CAPTURE_ENV%" echo DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=%DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE%
    >> "%CARVES_TEST_CAPTURE_ENV%" echo NUGET_PACKAGES=%NUGET_PACKAGES%
    >> "%CARVES_TEST_CAPTURE_ENV%" echo CARVES_FORMAL_VALIDATION_OWNER=%CARVES_FORMAL_VALIDATION_OWNER%
    >> "%CARVES_TEST_CAPTURE_ENV%" echo CARVES_DELEGATED_TOOL_ROOT=%CARVES_DELEGATED_TOOL_ROOT%
    >> "%CARVES_TEST_CAPTURE_ENV%" echo CARVES_REAL_DOTNET_PATH=%CARVES_REAL_DOTNET_PATH%
    >> "%CARVES_TEST_CAPTURE_ENV%" echo CARVES_REAL_GIT_PATH=%CARVES_REAL_GIT_PATH%
    >> "%CARVES_TEST_CAPTURE_ENV%" echo USERPROFILE=%USERPROFILE%
  )
  echo {"type":"thread.started","thread_id":"cli-direct-run"}
  echo {"type":"turn.started"}
  echo {"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"dotnet test","exit_code":0,"aggregated_output":"ok","error":""}}
  echo {"type":"item.completed","item":{"type":"file_change","status":"completed","changes":[{"path":"README.md","kind":"modify"}]}}
  echo {"type":"item.completed","item":{"type":"agent_message","text":"cli worker success"}}
  echo {"type":"turn.completed","usage":{"input_tokens":21,"output_tokens":9}}
  exit /b 0
)
echo unsupported 1>&2
exit /b 1
""");
        }

        var path = workspace.WriteFile("tools/fake-codex", """
#!/usr/bin/env sh
if [ -n "$CARVES_TEST_CAPTURE_ARGS" ]; then
  printf '%s\n' "$*" >> "$CARVES_TEST_CAPTURE_ARGS"
fi
if [ "$1" = "--version" ]; then
  echo "codex 0.0.0-test"
  exit 0
fi
if [ "$1" = "exec" ]; then
  if [ -n "$CARVES_TEST_CAPTURE_STDIN" ]; then
    cat > "$CARVES_TEST_CAPTURE_STDIN"
  else
    cat >/dev/null
  fi
  if [ -n "$CARVES_TEST_CAPTURE_ENV" ]; then
    {
      echo "DOTNET_CLI_HOME=$DOTNET_CLI_HOME"
      echo "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=$DOTNET_SKIP_FIRST_TIME_EXPERIENCE"
      echo "DOTNET_CLI_TELEMETRY_OPTOUT=$DOTNET_CLI_TELEMETRY_OPTOUT"
      echo "DOTNET_NOLOGO=$DOTNET_NOLOGO"
      echo "DOTNET_GENERATE_ASPNET_CERTIFICATE=$DOTNET_GENERATE_ASPNET_CERTIFICATE"
      echo "DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=$DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"
      echo "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=$DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"
      echo "NUGET_PACKAGES=$NUGET_PACKAGES"
      echo "CARVES_FORMAL_VALIDATION_OWNER=$CARVES_FORMAL_VALIDATION_OWNER"
      echo "CARVES_DELEGATED_TOOL_ROOT=$CARVES_DELEGATED_TOOL_ROOT"
      echo "CARVES_REAL_DOTNET_PATH=$CARVES_REAL_DOTNET_PATH"
      echo "CARVES_REAL_GIT_PATH=$CARVES_REAL_GIT_PATH"
      echo "HOME=$HOME"
    } > "$CARVES_TEST_CAPTURE_ENV"
  fi
  echo '{"type":"thread.started","thread_id":"cli-direct-run"}'
  echo '{"type":"turn.started"}'
  echo '{"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"dotnet test","exit_code":0,"aggregated_output":"ok","error":""}}'
  echo '{"type":"item.completed","item":{"type":"file_change","status":"completed","changes":[{"path":"README.md","kind":"modify"}]}}'
  echo '{"type":"item.completed","item":{"type":"agent_message","text":"cli worker success"}}'
  echo '{"type":"turn.completed","usage":{"input_tokens":21,"output_tokens":9}}'
  exit 0
fi
echo unsupported >&2
exit 1
""");
        File.SetUnixFileMode(path, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return path;
    }

    private static string CreateWorkspaceWriteWithoutFileChangeFakeCodexCli(TemporaryWorkspace workspace)
    {
        if (OperatingSystem.IsWindows())
        {
            return workspace.WriteFile("tools/fake-codex-actual-change.cmd", """
@echo off
if "%1"=="--version" (
  echo codex 0.0.0-test
  exit /b 0
)
if "%1"=="exec" (
  set /p _CARVES_PROMPT=
  if not exist src mkdir src
  > src\Observed.cs echo internal sealed class Observed {}
  echo {"type":"thread.started","thread_id":"cli-actual-change-run"}
  echo {"type":"turn.started"}
  echo {"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"custom-write-tool","exit_code":0,"aggregated_output":"ok","error":""}}
  echo {"type":"item.completed","item":{"type":"agent_message","text":"actual change success"}}
  echo {"type":"turn.completed","usage":{"input_tokens":21,"output_tokens":9}}
  exit /b 0
)
echo unsupported 1>&2
exit /b 1
""");
        }

        var path = workspace.WriteFile("tools/fake-codex-actual-change", """
#!/usr/bin/env sh
if [ "$1" = "--version" ]; then
  echo "codex 0.0.0-test"
  exit 0
fi
if [ "$1" = "exec" ]; then
  cat >/dev/null
  mkdir -p src
  printf '%s\n' 'internal sealed class Observed {}' > src/Observed.cs
  echo '{"type":"thread.started","thread_id":"cli-actual-change-run"}'
  echo '{"type":"turn.started"}'
  echo '{"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"custom-write-tool","exit_code":0,"aggregated_output":"ok","error":""}}'
  echo '{"type":"item.completed","item":{"type":"agent_message","text":"actual change success"}}'
  echo '{"type":"turn.completed","usage":{"input_tokens":21,"output_tokens":9}}'
  exit 0
fi
echo unsupported >&2
exit 1
""");
        File.SetUnixFileMode(path, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return path;
    }

    private static string CreateHangingFakeCodexCli(TemporaryWorkspace workspace)
    {
if (OperatingSystem.IsWindows())
        {
            return workspace.WriteFile("tools/fake-codex-hang.cmd", """
@echo off
if "%1"=="--version" (
  echo codex 0.0.0-test
  exit /b 0
)
if "%1"=="exec" (
  set /p _CARVES_PROMPT=
  echo {"type":"thread.started","thread_id":"cli-timeout-run"}
  echo {"type":"turn.started"}
  echo {"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"dotnet test","exit_code":0,"aggregated_output":"ok","error":""}}
  ping -n 6 127.0.0.1 >nul
  exit /b 0
)
echo unsupported 1>&2
exit /b 1
""");
        }

        var path = workspace.WriteFile("tools/fake-codex-hang", """
#!/usr/bin/env sh
if [ "$1" = "--version" ]; then
  echo "codex 0.0.0-test"
  exit 0
fi
if [ "$1" = "exec" ]; then
  cat >/dev/null
  echo '{"type":"thread.started","thread_id":"cli-timeout-run"}'
  echo '{"type":"turn.started"}'
  echo '{"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"dotnet test","exit_code":0,"aggregated_output":"ok","error":""}}'
  sleep 5
  exit 0
fi
echo unsupported >&2
exit 1
""");
        File.SetUnixFileMode(path, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return path;
    }

    private static string CreateSilentHangingFakeCodexCli(TemporaryWorkspace workspace)
    {
        if (OperatingSystem.IsWindows())
        {
            return workspace.WriteFile("tools/fake-codex-silent-hang.cmd", """
@echo off
if "%1"=="--version" (
  echo codex 0.0.0-test
  exit /b 0
)
if "%1"=="exec" (
  set /p _CARVES_PROMPT=
  ping -n 6 127.0.0.1 >nul
  exit /b 0
)
echo unsupported 1>&2
exit /b 1
""");
        }

        var path = workspace.WriteFile("tools/fake-codex-silent-hang", """
#!/usr/bin/env sh
if [ "$1" = "--version" ]; then
  echo "codex 0.0.0-test"
  exit 0
fi
if [ "$1" = "exec" ]; then
  cat >/dev/null
  sleep 5
  exit 0
fi
echo unsupported >&2
exit 1
""");
        File.SetUnixFileMode(path, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return path;
    }

    private static string CreateBootstrapHangingFakeCodexCli(TemporaryWorkspace workspace)
    {
        if (OperatingSystem.IsWindows())
        {
            return workspace.WriteFile("tools/fake-codex-bootstrap-hang.cmd", """
@echo off
if "%1"=="--version" (
  echo codex 0.0.0-test
  exit /b 0
)
if "%1"=="exec" (
  set /p _CARVES_PROMPT=
  echo {"type":"thread.started","thread_id":"cli-bootstrap-timeout-run"}
  echo {"type":"turn.started"}
  ping -n 6 127.0.0.1 >nul
  exit /b 0
)
echo unsupported 1>&2
exit /b 1
""");
        }

        var path = workspace.WriteFile("tools/fake-codex-bootstrap-hang", """
#!/usr/bin/env sh
if [ "$1" = "--version" ]; then
  echo "codex 0.0.0-test"
  exit 0
fi
if [ "$1" = "exec" ]; then
  cat >/dev/null
  echo '{"type":"thread.started","thread_id":"cli-bootstrap-timeout-run"}'
  echo '{"type":"turn.started"}'
  sleep 5
  exit 0
fi
echo unsupported >&2
exit 1
""");
        File.SetUnixFileMode(path, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return path;
    }

    private static string CreateControlPlaneRescanHangingFakeCodexCli(TemporaryWorkspace workspace)
    {
        if (OperatingSystem.IsWindows())
        {
            return workspace.WriteFile("tools/fake-codex-rescan-hang.cmd", """
@echo off
if "%1"=="--version" (
  echo codex 0.0.0-test
  exit /b 0
)
if "%1"=="exec" (
  set /p _CARVES_PROMPT=
  echo {"type":"thread.started","thread_id":"cli-rescan-timeout-run"}
  echo {"type":"turn.started"}
  echo {"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"powershell -Command Get-Content README.md","exit_code":0,"aggregated_output":"ok","error":""}}
  echo {"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"powershell -Command Get-Content .ai/tasks/cards/CARD-314.md","exit_code":0,"aggregated_output":"ok","error":""}}
  echo {"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"powershell -Command rg --files .ai/tasks/cards | Select-String -Pattern \"CARD-314\"","exit_code":0,"aggregated_output":"ok","error":""}}
  ping -n 6 127.0.0.1 >nul
  exit /b 0
)
echo unsupported 1>&2
exit /b 1
""");
        }

        var path = workspace.WriteFile("tools/fake-codex-rescan-hang", """
#!/usr/bin/env sh
if [ "$1" = "--version" ]; then
  echo "codex 0.0.0-test"
  exit 0
fi
if [ "$1" = "exec" ]; then
  cat >/dev/null
  echo '{"type":"thread.started","thread_id":"cli-rescan-timeout-run"}'
  echo '{"type":"turn.started"}'
  echo '{"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"powershell -Command Get-Content README.md","exit_code":0,"aggregated_output":"ok","error":""}}'
  echo '{"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"powershell -Command Get-Content .ai/tasks/cards/CARD-314.md","exit_code":0,"aggregated_output":"ok","error":""}}'
  echo '{"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"powershell -Command rg --files .ai/tasks/cards | Select-String -Pattern CARD-314","exit_code":0,"aggregated_output":"ok","error":""}}'
  sleep 5
  exit 0
fi
echo unsupported >&2
exit 1
""");
        File.SetUnixFileMode(path, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return path;
    }

    private static string CreatePostEditReadbackHangingFakeCodexCli(TemporaryWorkspace workspace)
    {
        var path = workspace.WriteFile("tools/fake-codex-post-edit-readback-hang", """
#!/usr/bin/env sh
if [ "$1" = "--version" ]; then
  echo "codex 0.0.0-test"
  exit 0
fi
if [ "$1" = "exec" ]; then
  cat >/dev/null
  echo '{"type":"thread.started","thread_id":"cli-post-edit-readback-timeout-run"}'
  echo '{"type":"turn.started"}'
  echo '{"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"python3 -c \"from pathlib import Path; Path('\''src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockLeaseSnapshot.cs'\'').write_text('\''x'\''); Path('\''src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockOptions.cs'\'').write_text('\''y'\'')\"","exit_code":0,"aggregated_output":"ok","error":""}}'
  echo '{"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"sed -n 1,220p src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockLeaseSnapshot.cs src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockOptions.cs","exit_code":0,"aggregated_output":"ok","error":""}}'
  sleep 5
  exit 0
fi
echo unsupported >&2
exit 1
""");
        File.SetUnixFileMode(path, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return path;
    }

    private static string CreateSourceExplorationHangingFakeCodexCli(TemporaryWorkspace workspace)
    {
        var path = workspace.WriteFile("tools/fake-codex-source-exploration-hang", """
#!/usr/bin/env sh
if [ "$1" = "--version" ]; then
  echo "codex 0.0.0-test"
  exit 0
fi
if [ "$1" = "exec" ]; then
  cat >/dev/null
  echo '{"type":"thread.started","thread_id":"cli-source-exploration-timeout-run"}'
  echo '{"type":"turn.started"}'
  echo '{"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"sed -n 1,240p src/CARVES.Runtime.Application/ControlPlane/BoundaryDecisionService.cs src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockHandle.cs src/CARVES.Runtime.Host/BlockingHostWait.cs","exit_code":0,"aggregated_output":"ok","error":""}}'
  echo '{"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"rg -n residue src/CARVES.Runtime.Application src/CARVES.Runtime.Host","exit_code":0,"aggregated_output":"ok","error":""}}'
  echo '{"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"sed -n 240,340p src/CARVES.Runtime.Application/Planning/ManagedWorkspaceLeaseService.cs src/CARVES.Runtime.Application/Platform/SurfaceModels/RuntimeManagedWorkspaceSurface.cs","exit_code":0,"aggregated_output":"ok","error":""}}'
  sleep 5
  exit 0
fi
echo unsupported >&2
exit 1
""");
        File.SetUnixFileMode(path, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return path;
    }

    private static string CreateShellWriteFakeCodexCli(TemporaryWorkspace workspace)
    {
        if (OperatingSystem.IsWindows())
        {
            return workspace.WriteFile("tools/fake-codex-shellwrite.cmd", """
@echo off
if "%1"=="--version" (
  echo codex 0.0.0-test
  exit /b 0
)
if "%1"=="exec" (
  set /p _CARVES_PROMPT=
  echo {"type":"thread.started","thread_id":"cli-shellwrite-run"}
  echo {"type":"turn.started"}
  echo {"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"powershell -Command $path = tests/Ordering.UnitTests/Application/CreateOrderDraftCommandHandlerTest.cs; Set-Content -Path $path -Value x","exit_code":0,"aggregated_output":"","error":""}}
  echo {"type":"item.completed","item":{"type":"agent_message","text":"shell write success"}}
  echo {"type":"turn.completed","usage":{"input_tokens":21,"output_tokens":9}}
  exit /b 0
)
echo unsupported 1>&2
exit /b 1
""");
        }

        var path = workspace.WriteFile("tools/fake-codex-shellwrite", """
#!/usr/bin/env sh
if [ "$1" = "--version" ]; then
  echo "codex 0.0.0-test"
  exit 0
fi
if [ "$1" = "exec" ]; then
  cat >/dev/null
  echo '{"type":"thread.started","thread_id":"cli-shellwrite-run"}'
  echo '{"type":"turn.started"}'
  echo '{"type":"item.completed","item":{"type":"command_execution","status":"completed","command":"powershell -Command $path = tests/Ordering.UnitTests/Application/CreateOrderDraftCommandHandlerTest.cs; Set-Content -Path $path -Value x","exit_code":0,"aggregated_output":"","error":""}}'
  echo '{"type":"item.completed","item":{"type":"agent_message","text":"shell write success"}}'
  echo '{"type":"turn.completed","usage":{"input_tokens":21,"output_tokens":9}}'
  exit 0
fi
echo unsupported >&2
exit 1
""");
        File.SetUnixFileMode(path, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return path;
    }

    private static string CreateRipgrepProbeFakeCodexCli(TemporaryWorkspace workspace)
    {
        if (OperatingSystem.IsWindows())
        {
            return workspace.WriteFile("tools/fake-codex-rgprobe.cmd", """
@echo off
if "%1"=="--version" (
  echo codex 0.0.0-test
  exit /b 0
)
if "%1"=="exec" (
  set /p _CARVES_PROMPT=
  echo {"type":"thread.started","thread_id":"cli-rgprobe-run"}
  echo {"type":"turn.started"}
  echo {"type":"item.completed","item":{"type":"command_execution","status":"failed","command":"powershell -Command 'rg -n \"missing\" tests -g\"*.cs\"'","exit_code":1,"aggregated_output":"","error":""}}
  echo {"type":"item.completed","item":{"type":"agent_message","text":"rg probe success"}}
  echo {"type":"turn.completed","usage":{"input_tokens":21,"output_tokens":9}}
  exit /b 0
)
echo unsupported 1>&2
exit /b 1
""");
        }

        var path = workspace.WriteFile("tools/fake-codex-rgprobe", """
#!/usr/bin/env sh
if [ "$1" = "--version" ]; then
  echo "codex 0.0.0-test"
  exit 0
fi
if [ "$1" = "exec" ]; then
  cat >/dev/null
  echo '{"type":"thread.started","thread_id":"cli-rgprobe-run"}'
  echo '{"type":"turn.started"}'
  echo '{"type":"item.completed","item":{"type":"command_execution","status":"failed","command":"powershell -Command rg -n missing tests -g*.cs","exit_code":1,"aggregated_output":"","error":""}}'
  echo '{"type":"item.completed","item":{"type":"agent_message","text":"rg probe success"}}'
  echo '{"type":"turn.completed","usage":{"input_tokens":21,"output_tokens":9}}'
  exit 0
fi
echo unsupported >&2
exit 1
""");
        File.SetUnixFileMode(path, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return path;
    }

    private static string CreateGitProbeFakeCodexCli(TemporaryWorkspace workspace)
    {
        if (OperatingSystem.IsWindows())
        {
            return workspace.WriteFile("tools/fake-codex-gitprobe.cmd", """
@echo off
if "%1"=="--version" (
  echo codex 0.0.0-test
  exit /b 0
)
if "%1"=="exec" (
  set /p _CARVES_PROMPT=
  echo {"type":"thread.started","thread_id":"cli-gitprobe-run"}
  echo {"type":"turn.started"}
  echo {"type":"item.completed","item":{"type":"command_execution","status":"failed","command":"powershell -Command 'git status -sb'","exit_code":1,"aggregated_output":"fatal: not a git repository (or any of the parent directories): .git","error":""}}
  echo {"type":"item.completed","item":{"type":"agent_message","text":"git probe success"}}
  echo {"type":"turn.completed","usage":{"input_tokens":21,"output_tokens":9}}
  exit /b 0
)
echo unsupported 1>&2
exit /b 1
""");
        }

        var path = workspace.WriteFile("tools/fake-codex-gitprobe", """
#!/usr/bin/env sh
if [ "$1" = "--version" ]; then
  echo "codex 0.0.0-test"
  exit 0
fi
if [ "$1" = "exec" ]; then
  cat >/dev/null
  echo '{"type":"thread.started","thread_id":"cli-gitprobe-run"}'
  echo '{"type":"turn.started"}'
  echo '{"type":"item.completed","item":{"type":"command_execution","status":"failed","command":"powershell -Command git status -sb","exit_code":1,"aggregated_output":"fatal: not a git repository (or any of the parent directories): .git","error":""}}'
  echo '{"type":"item.completed","item":{"type":"agent_message","text":"git probe success"}}'
  echo '{"type":"turn.completed","usage":{"input_tokens":21,"output_tokens":9}}'
  exit 0
fi
echo unsupported >&2
exit 1
""");
        File.SetUnixFileMode(path, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return path;
    }

    private static string CreatePythonWriteGitDiffProbeFakeCodexCli(TemporaryWorkspace workspace)
    {
        if (OperatingSystem.IsWindows())
        {
            return workspace.WriteFile("tools/fake-codex-python-write-git-diff-probe.cmd", """
@echo off
if "%1"=="--version" (
  echo codex 0.0.0-test
  exit /b 0
)
if "%1"=="exec" (
  set /p _CARVES_PROMPT=
  echo {"type":"thread.started","thread_id":"cli-python-write-git-diff-probe-run"}
  echo {"type":"turn.started"}
  echo {"type":"item.completed","item":{"type":"command_execution","status":"failed","command":"python -c \"from pathlib import Path; Path('src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockLeaseSnapshot.cs').write_text('x'); Path('src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockOptions.cs').write_text('y')\" ^&^& git diff -- src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockLeaseSnapshot.cs src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockOptions.cs","exit_code":129,"aggregated_output":"warning: Not a git repository. Use --no-index to compare two paths outside a working tree","error":""}}
  echo {"type":"item.completed","item":{"type":"agent_message","text":"python write git diff probe success"}}
  echo {"type":"turn.completed","usage":{"input_tokens":21,"output_tokens":9}}
  exit /b 0
)
echo unsupported 1>&2
exit /b 1
""");
        }

        var path = workspace.WriteFile("tools/fake-codex-python-write-git-diff-probe", """
#!/usr/bin/env sh
if [ "$1" = "--version" ]; then
  echo "codex 0.0.0-test"
  exit 0
fi
if [ "$1" = "exec" ]; then
  cat >/dev/null
  echo '{"type":"thread.started","thread_id":"cli-python-write-git-diff-probe-run"}'
  echo '{"type":"turn.started"}'
  echo '{"type":"item.completed","item":{"type":"command_execution","status":"failed","command":"python3 -c \"from pathlib import Path; Path('\''src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockLeaseSnapshot.cs'\'').write_text('\''x'\''); Path('\''src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockOptions.cs'\'').write_text('\''y'\'')\" && git diff -- src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockLeaseSnapshot.cs src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockOptions.cs","exit_code":129,"aggregated_output":"warning: Not a git repository. Use --no-index to compare two paths outside a working tree","error":""}}'
  echo '{"type":"item.completed","item":{"type":"agent_message","text":"python write git diff probe success"}}'
  echo '{"type":"turn.completed","usage":{"input_tokens":21,"output_tokens":9}}'
  exit 0
fi
echo unsupported >&2
exit 1
""");
        File.SetUnixFileMode(path, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return path;
    }

    private static string CreateApprovalFalsePositiveFakeCodexCli(TemporaryWorkspace workspace)
    {
        if (OperatingSystem.IsWindows())
        {
            return workspace.WriteFile("tools/fake-codex-approval-false-positive.cmd", """
@echo off
if "%1"=="--version" (
  echo codex 0.0.0-test
  exit /b 0
)
if "%1"=="exec" (
  set /p _CARVES_PROMPT=
  echo {"type":"thread.started","thread_id":"cli-approval-false-positive-run"}
  echo {"type":"turn.started"}
  echo {"type":"item.completed","item":{"type":"command_execution","status":"failed","command":"powershell -Command Get-Content ControlPlane.cs","exit_code":1,"aggregated_output":"public string ApprovalPosture { get; init; } = \"never\";","error":"sed: can't read missing.cs: No such file or directory"}}
  echo {"type":"item.completed","item":{"type":"agent_message","text":"source text failure"}}
  exit /b 1
)
echo unsupported 1>&2
exit /b 1
""");
        }

        var path = workspace.WriteFile("tools/fake-codex-approval-false-positive", """
#!/usr/bin/env sh
if [ "$1" = "--version" ]; then
  echo "codex 0.0.0-test"
  exit 0
fi
if [ "$1" = "exec" ]; then
  cat >/dev/null
  echo '{"type":"thread.started","thread_id":"cli-approval-false-positive-run"}'
  echo '{"type":"turn.started"}'
  echo '{"type":"item.completed","item":{"type":"command_execution","status":"failed","command":"powershell -Command Get-Content ControlPlane.cs","exit_code":1,"aggregated_output":"public string ApprovalPosture { get; init; } = \"never\";","error":"sed: can'\''t read missing.cs: No such file or directory"}}'
  echo '{"type":"item.completed","item":{"type":"agent_message","text":"source text failure"}}'
  exit 1
fi
echo unsupported >&2
exit 1
""");
        File.SetUnixFileMode(path, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return path;
    }

    private static string ReadAllTextWithRetry(string path)
    {
        IOException? lastError = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException error)
            {
                lastError = error;
                TestWait.Delay(TimeSpan.FromMilliseconds(50));
            }
        }

        throw lastError ?? new IOException($"Failed to read test capture file '{path}'.");
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        process!.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }
}

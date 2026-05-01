using System.Security.Cryptography;
using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class LegacyAdoptionP4ProposalsCliTests
{
    [Fact]
    public void AdoptionPropose_AfterIntake_WritesOnlyRuntimeProposalArtifacts()
    {
        using var repo = LegacyProposalSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        var readmeHash = repo.FileHash("README.md");

        AttachAndIntake(repo);
        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "propose", "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("legacy_adoption.p4_propose_result.v0.1.0", root.GetProperty("schema_version").GetString());
        Assert.Equal("P4_PROPOSALS", root.GetProperty("phase").GetString());
        Assert.Equal("carves adoption propose --json", root.GetProperty("command").GetString());
        Assert.Equal("success", root.GetProperty("result").GetString());
        Assert.Equal("PROPOSALS_GENERATED", root.GetProperty("status").GetString());
        Assert.Equal(3, root.GetProperty("generation").GetInt32());
        Assert.True(root.GetProperty("proposal_only").GetBoolean());
        Assert.False(root.GetProperty("governed_memory_written").GetBoolean());
        Assert.False(root.GetProperty("approved_taskgraph_written").GetBoolean());
        Assert.False(root.GetProperty("canonical_codegraph_written").GetBoolean());
        Assert.False(root.GetProperty("planner_authoritative_codegraph_written").GetBoolean());
        Assert.False(root.GetProperty("patches_written").GetBoolean());
        Assert.False(root.GetProperty("reviews_written").GetBoolean());
        Assert.False(root.GetProperty("worker_execution_invoked").GetBoolean());
        Assert.Equal("BLOCKED", root.GetProperty("p5_status").GetString());

        var files = repo.AllRelativeFiles();
        Assert.Contains(".ai/runtime/adoption/proposals/proposal-set-0003.json", files);
        Assert.Contains(".ai/runtime/adoption/proposals/memory-seed.proposal.md", files);
        Assert.Contains(".ai/runtime/adoption/proposals/taskgraph-draft.proposal.json", files);
        Assert.Contains(".ai/runtime/adoption/proposals/codegraph-snapshot.proposal.json", files);
        Assert.Contains(".ai/runtime/adoption/proposals/refactor-candidates.proposal.json", files);
        Assert.DoesNotContain(".ai/runtime/adoption/adoption.lock.json", files);
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/memory/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/tasks/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/codegraph/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/patches/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/reviews/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/refactoring/", StringComparison.Ordinal));
        Assert.Equal(readmeHash, repo.FileHash("README.md"));
        AssertManifestProposalUpdated(repo, expectedGeneration: 3);
        AssertEventsAndLedgerAppendP4Proposals(repo);
    }

    [Fact]
    public void AdoptionPropose_RerunSameEvidence_IsNoopAndDoesNotDuplicateProposals()
    {
        using var repo = LegacyProposalSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        AttachAndIntake(repo);
        Assert.Equal(0, CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "propose", "--json").ExitCode);
        var proposalsAfterFirst = repo.AllRelativeFiles().Where(path => path.StartsWith(".ai/runtime/adoption/proposals/", StringComparison.Ordinal)).ToArray();

        var rerun = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "propose", "--json");

        Assert.Equal(0, rerun.ExitCode);
        using var document = JsonDocument.Parse(rerun.StandardOutput);
        Assert.Equal("noop", document.RootElement.GetProperty("result").GetString());
        Assert.Equal("PROPOSALS_NOOP", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(3, document.RootElement.GetProperty("generation").GetInt32());
        Assert.Equal(proposalsAfterFirst, repo.AllRelativeFiles().Where(path => path.StartsWith(".ai/runtime/adoption/proposals/", StringComparison.Ordinal)).ToArray());
    }

    [Fact]
    public void AdoptionPropose_MissingOrCorruptP3Evidence_BlocksWithoutDestructiveWrites()
    {
        using var missing = LegacyProposalSandbox.Create();
        missing.WriteFile("README.md", "# Legacy repo\n");
        missing.CommitAll("Initial commit");
        Assert.Equal(0, CliProgramHarness.RunInDirectory(missing.RootPath, "adoption", "attach", "--json").ExitCode);
        var beforeMissing = missing.AllRelativeFiles();
        var missingResult = CliProgramHarness.RunInDirectory(missing.RootPath, "adoption", "propose", "--json");

        Assert.Equal(24, missingResult.ExitCode);
        using (var document = JsonDocument.Parse(missingResult.StandardOutput))
        {
            Assert.Equal("P3_EVIDENCE_MISSING", document.RootElement.GetProperty("status").GetString());
        }

        Assert.Equal(beforeMissing, missing.AllRelativeFiles());

        using var corrupt = LegacyProposalSandbox.Create();
        corrupt.WriteFile("README.md", "# Legacy repo\n");
        corrupt.CommitAll("Initial commit");
        AttachAndIntake(corrupt);
        corrupt.WriteFile(".ai/runtime/adoption/snapshots/repo-scan-0002.json", "{ not json");
        var beforeCorrupt = corrupt.AllRelativeFiles();
        var corruptResult = CliProgramHarness.RunInDirectory(corrupt.RootPath, "adoption", "propose", "--json");

        Assert.Equal(24, corruptResult.ExitCode);
        using var corruptDocument = JsonDocument.Parse(corruptResult.StandardOutput);
        Assert.Equal("P3_EVIDENCE_CORRUPT", corruptDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal(beforeCorrupt, corrupt.AllRelativeFiles());
    }

    [Fact]
    public void AdoptionPropose_ProposalArtifactsRemainNonAuthoritative()
    {
        using var repo = LegacyProposalSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        AttachAndIntake(repo);

        Assert.Equal(0, CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "propose", "--json").ExitCode);

        var memory = File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "proposals", "memory-seed.proposal.md"));
        Assert.Contains("Status: PROPOSED", memory, StringComparison.Ordinal);
        Assert.Contains("Authoritative: false", memory, StringComparison.Ordinal);
        Assert.DoesNotContain("APPROVED", memory, StringComparison.Ordinal);

        using var taskGraph = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "proposals", "taskgraph-draft.proposal.json")));
        Assert.Equal("DRAFT", taskGraph.RootElement.GetProperty("status").GetString());
        Assert.False(taskGraph.RootElement.GetProperty("authoritative").GetBoolean());
        foreach (var task in taskGraph.RootElement.GetProperty("tasks").EnumerateArray())
        {
            Assert.Equal("SUGGESTED", task.GetProperty("status").GetString());
        }

        using var codeGraph = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "proposals", "codegraph-snapshot.proposal.json")));
        Assert.Equal("SNAPSHOT", codeGraph.RootElement.GetProperty("status").GetString());
        Assert.False(codeGraph.RootElement.GetProperty("planner_authoritative").GetBoolean());
        Assert.False(codeGraph.RootElement.GetProperty("canonical").GetBoolean());
        Assert.False(codeGraph.RootElement.GetProperty("continuous_refactoring_triggered").GetBoolean());

        using var refactor = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "proposals", "refactor-candidates.proposal.json")));
        Assert.Equal("PROPOSED", refactor.RootElement.GetProperty("status").GetString());
        Assert.False(refactor.RootElement.GetProperty("worker_execution_invoked").GetBoolean());
        Assert.False(refactor.RootElement.GetProperty("patches_written").GetBoolean());
        Assert.False(refactor.RootElement.GetProperty("reviews_written").GetBoolean());
    }

    [Fact]
    public void AdoptionPropose_PrivacyAndInstructionProtection_RedactsPublicOutputAndProposalSummaries()
    {
        using var repo = LegacyProposalSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.WriteFile("AGENTS.md", "SECRET_AGENT_INSTRUCTIONS");
        repo.WriteFile("CLAUDE.md", "SECRET_CLAUDE_INSTRUCTIONS");
        repo.WriteFile(".cursor/rules", "SECRET_CURSOR_RULES");
        repo.CommitAll("Initial commit");
        var agentsHash = repo.FileHash("AGENTS.md");
        var claudeHash = repo.FileHash("CLAUDE.md");
        var cursorHash = repo.FileHash(".cursor/rules");
        Assert.Equal(0, CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "attach", "--json").ExitCode);
        repo.WriteFile("README.md", "# Legacy repo\nSECRET_DIRTY_DIFF\n");
        Assert.Equal(0, CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "intake", "--json").ExitCode);

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "propose", "--json");

        Assert.Equal(0, result.ExitCode);
        var combinedProposalText = string.Join(
            "\n",
            Directory.EnumerateFiles(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "proposals"), "*", SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));
        foreach (var secret in new[] { "SECRET_AGENT_INSTRUCTIONS", "SECRET_CLAUDE_INSTRUCTIONS", "SECRET_CURSOR_RULES", "SECRET_DIRTY_DIFF" })
        {
            Assert.DoesNotContain(secret, result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, combinedProposalText, StringComparison.Ordinal);
        }

        Assert.Equal(agentsHash, repo.FileHash("AGENTS.md"));
        Assert.Equal(claudeHash, repo.FileHash("CLAUDE.md"));
        Assert.Equal(cursorHash, repo.FileHash(".cursor/rules"));
    }

    [Fact]
    public void AdoptionPropose_LockConflictAndExpiredLock_DoNotTakeOver()
    {
        using var repo = LegacyProposalSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        AttachAndIntake(repo);
        repo.WriteFile(".ai/runtime/adoption/adoption.lock.json", $$"""
        {
          "schema_version": "legacy_adoption.propose_lock.v0.1.0",
          "created_at": "{{DateTimeOffset.UtcNow:O}}",
          "expires_at": "{{DateTimeOffset.UtcNow.AddMinutes(10):O}}",
          "takeover_allowed": false
        }
        """);
        var beforeFresh = repo.AllRelativeFiles();
        var fresh = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "propose", "--json");
        Assert.Equal(20, fresh.ExitCode);
        Assert.Equal(beforeFresh, repo.AllRelativeFiles());

        File.Delete(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "adoption.lock.json"));
        repo.WriteFile(".ai/runtime/adoption/adoption.lock.json", $$"""
        {
          "schema_version": "legacy_adoption.propose_lock.v0.1.0",
          "created_at": "{{DateTimeOffset.UtcNow.AddMinutes(-30):O}}",
          "expires_at": "{{DateTimeOffset.UtcNow.AddMinutes(-1):O}}",
          "takeover_allowed": false
        }
        """);
        var expired = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "propose", "--json");
        Assert.Equal(25, expired.ExitCode);
        Assert.True(File.Exists(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "adoption.lock.json")));
        Assert.False(File.Exists(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "proposals", "proposal-set-0003.json")));
    }

    [Fact]
    public void AdoptionPropose_StaticIsolation_DoesNotReferenceForbiddenWriterPaths()
    {
        var repoRoot = ResolveRepoRoot();
        var adoptionSource = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Runtime.Cli", "FriendlyCliApplication.Adoption.cs"));

        Assert.DoesNotContain("TargetRepoAttachService", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RunStart(", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RunInit(", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CodeGraphBuilder", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TaskGraphService", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryService", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("PatchWriter", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ReviewWriter", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CleanupService", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DetachService", adoptionSource, StringComparison.Ordinal);
    }

    private static void AttachAndIntake(LegacyProposalSandbox repo)
    {
        Assert.Equal(0, CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "attach", "--json").ExitCode);
        Assert.Equal(0, CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "intake", "--json").ExitCode);
    }

    private static void AssertManifestProposalUpdated(LegacyProposalSandbox repo, int expectedGeneration)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "adoption.json")));
        var root = manifest.RootElement;
        Assert.Equal("legacy_adoption.attach_manifest.v0.1.0", root.GetProperty("schema_version").GetString());
        Assert.Equal("BOUND", root.GetProperty("status").GetString());
        Assert.Equal(expectedGeneration, root.GetProperty("generation").GetInt32());
        Assert.Matches("^sha256:[a-f0-9]{64}$", root.GetProperty("last_manifest_hash").GetString());
        Assert.Matches("^sha256:[a-f0-9]{64}$", root.GetProperty("last_intake_evidence_hash").GetString());
        Assert.Matches("^sha256:[a-f0-9]{64}$", root.GetProperty("last_proposal_hash").GetString());
        Assert.Equal(5, root.GetProperty("last_proposal_artifacts").GetArrayLength());
    }

    private static void AssertEventsAndLedgerAppendP4Proposals(LegacyProposalSandbox repo)
    {
        var events = File.ReadAllLines(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "events.jsonl"));
        var ledger = File.ReadAllLines(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "ownership-ledger.jsonl"));
        Assert.Equal(3, events.Length);
        Assert.True(ledger.Length >= 11);

        using var eventDoc = JsonDocument.Parse(events[^1]);
        Assert.Equal("PROPOSALS_GENERATED", eventDoc.RootElement.GetProperty("event_type").GetString());
        Assert.Equal("success", eventDoc.RootElement.GetProperty("result").GetString());

        var ledgerPaths = ledger
            .Select(row => JsonDocument.Parse(row))
            .Select(document => document.RootElement.GetProperty("artifact_path").GetString())
            .ToArray();
        Assert.Contains(".ai/runtime/adoption/proposals/proposal-set-0003.json", ledgerPaths);
        Assert.Contains(".ai/runtime/adoption/proposals/memory-seed.proposal.md", ledgerPaths);
        Assert.Contains(".ai/runtime/adoption/proposals/taskgraph-draft.proposal.json", ledgerPaths);
        Assert.Contains(".ai/runtime/adoption/proposals/codegraph-snapshot.proposal.json", ledgerPaths);
        Assert.Contains(".ai/runtime/adoption/proposals/refactor-candidates.proposal.json", ledgerPaths);
    }

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

        throw new InvalidOperationException("Could not resolve repository root.");
    }

    private sealed class LegacyProposalSandbox : IDisposable
    {
        private LegacyProposalSandbox(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static LegacyProposalSandbox Create()
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "carves-legacy-adoption-propose-" + Guid.NewGuid().ToString("N"));
            GitTestHarness.InitializeRepository(rootPath);
            return new LegacyProposalSandbox(rootPath);
        }

        public void WriteFile(string relativePath, string content)
        {
            var path = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void CommitAll(string message)
        {
            RunGit(RootPath, "add", ".");
            RunGit(RootPath, "commit", "-m", message);
        }

        public string FileHash(string relativePath)
        {
            var path = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        public string[] AllRelativeFiles()
            => Directory
                .EnumerateFiles(RootPath, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(RootPath, path).Replace('\\', '/'))
                .Where(path => !path.StartsWith(".git/", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .ToArray();

        private static void RunGit(string workingDirectory, params string[] arguments)
        {
            GitTestHarness.Run(workingDirectory, arguments);
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

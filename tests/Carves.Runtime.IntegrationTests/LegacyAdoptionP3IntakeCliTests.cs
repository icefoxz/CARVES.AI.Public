using System.Security.Cryptography;
using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class LegacyAdoptionP3IntakeCliTests
{
    [Fact]
    public void AdoptionIntake_AfterAttach_CapturesOnlyRuntimeAdoptionEvidence()
    {
        using var repo = LegacyIntakeSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        var readmeHash = repo.FileHash("README.md");

        Assert.Equal(0, CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "attach", "--json").ExitCode);
        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "intake", "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("legacy_adoption.p3_intake_result.v0.1.0", root.GetProperty("schema_version").GetString());
        Assert.Equal("P3_INTAKE", root.GetProperty("phase").GetString());
        Assert.Equal("carves adoption intake --json", root.GetProperty("command").GetString());
        Assert.Equal("success", root.GetProperty("result").GetString());
        Assert.Equal("INTAKE_CAPTURED", root.GetProperty("status").GetString());
        Assert.False(root.GetProperty("dirty_diff_content_emitted").GetBoolean());
        Assert.False(root.GetProperty("instruction_bodies_emitted").GetBoolean());
        Assert.False(root.GetProperty("repo_scan_is_codegraph").GetBoolean());
        Assert.False(root.GetProperty("codegraph_generated").GetBoolean());
        Assert.False(root.GetProperty("governance_surfaces_written").GetBoolean());
        Assert.Equal("BLOCKED", root.GetProperty("p4_p5_status").GetProperty("P4_PROPOSALS").GetString());
        Assert.Equal("BLOCKED", root.GetProperty("p4_p5_status").GetProperty("P5_CLEANUP_DETACH").GetString());

        var files = repo.AllRelativeFiles();
        Assert.Contains(".ai/runtime/adoption/snapshots/repo-scan-0002.json", files);
        Assert.Contains(".ai/runtime/adoption/snapshots/instruction-scan-0002.json", files);
        Assert.Contains(".ai/runtime/adoption/snapshots/dirty-diff-0002.patch", files);
        Assert.DoesNotContain(".ai/runtime/adoption/adoption.lock.json", files);
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/memory/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/tasks/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/codegraph/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/patches/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/reviews/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/refactoring/", StringComparison.Ordinal));
        Assert.Equal(readmeHash, repo.FileHash("README.md"));
        AssertManifestUpdated(repo, expectedGeneration: 2);
        AssertEventsAndLedgerAppendP3Evidence(repo);
    }

    [Fact]
    public void AdoptionIntake_RerunSameEvidence_IsNoopAndDoesNotDuplicateSnapshots()
    {
        using var repo = LegacyIntakeSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        Assert.Equal(0, CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "attach", "--json").ExitCode);
        Assert.Equal(0, CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "intake", "--json").ExitCode);
        var snapshotsAfterFirst = repo.AllRelativeFiles().Where(path => path.StartsWith(".ai/runtime/adoption/snapshots/", StringComparison.Ordinal)).ToArray();

        var rerun = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "intake", "--json");

        Assert.Equal(0, rerun.ExitCode);
        using var document = JsonDocument.Parse(rerun.StandardOutput);
        Assert.Equal("noop", document.RootElement.GetProperty("result").GetString());
        Assert.Equal("INTAKE_NOOP", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("generation").GetInt32());
        Assert.Equal(snapshotsAfterFirst, repo.AllRelativeFiles().Where(path => path.StartsWith(".ai/runtime/adoption/snapshots/", StringComparison.Ordinal)).ToArray());
    }

    [Fact]
    public void AdoptionIntake_DirtyAndInstructionEvidence_PreservesHumanFilesAndRedactsStdout()
    {
        using var repo = LegacyIntakeSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.WriteFile("AGENTS.md", "SECRET_AGENT_INSTRUCTIONS");
        repo.WriteFile("CLAUDE.md", "SECRET_CLAUDE_INSTRUCTIONS");
        repo.WriteFile(".cursor/rules", "SECRET_CURSOR_RULES");
        repo.CommitAll("Initial commit");
        Assert.Equal(0, CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "attach", "--json").ExitCode);
        var agentsHash = repo.FileHash("AGENTS.md");
        var claudeHash = repo.FileHash("CLAUDE.md");
        var cursorHash = repo.FileHash(".cursor/rules");
        repo.WriteFile("README.md", "# Legacy repo\nSECRET_DIRTY_DIFF\n");

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "intake", "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("SECRET_AGENT_INSTRUCTIONS", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_CLAUDE_INSTRUCTIONS", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_CURSOR_RULES", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_DIRTY_DIFF", result.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(agentsHash, repo.FileHash("AGENTS.md"));
        Assert.Equal(claudeHash, repo.FileHash("CLAUDE.md"));
        Assert.Equal(cursorHash, repo.FileHash(".cursor/rules"));
        Assert.True(File.Exists(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "proposals", "AGENTS.merge.md")));
        Assert.Contains("SECRET_DIRTY_DIFF", File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "snapshots", "dirty-diff-0002.patch")), StringComparison.Ordinal);
    }

    [Fact]
    public void AdoptionIntake_MissingOrCorruptBinding_BlocksWithoutDestructiveWrites()
    {
        using var missing = LegacyIntakeSandbox.Create();
        missing.WriteFile("README.md", "# Legacy repo\n");
        missing.CommitAll("Initial commit");
        var missingResult = CliProgramHarness.RunInDirectory(missing.RootPath, "adoption", "intake", "--json");
        Assert.Equal(23, missingResult.ExitCode);
        using (var document = JsonDocument.Parse(missingResult.StandardOutput))
        {
            Assert.Equal("ADOPTION_MISSING", document.RootElement.GetProperty("status").GetString());
        }

        using var corrupt = LegacyIntakeSandbox.Create();
        corrupt.WriteFile("README.md", "# Legacy repo\n");
        corrupt.CommitAll("Initial commit");
        corrupt.WriteFile(".ai/runtime/adoption/adoption.json", "{ not json");
        var before = corrupt.AllRelativeFiles();
        var corruptResult = CliProgramHarness.RunInDirectory(corrupt.RootPath, "adoption", "intake", "--json");

        Assert.Equal(21, corruptResult.ExitCode);
        using var corruptDocument = JsonDocument.Parse(corruptResult.StandardOutput);
        Assert.Equal("MANIFEST_CORRUPT", corruptDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal(before, corrupt.AllRelativeFiles());
    }

    [Fact]
    public void AdoptionIntake_LockConflictAndExpiredLock_DoNotTakeOver()
    {
        using var repo = LegacyIntakeSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        Assert.Equal(0, CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "attach", "--json").ExitCode);
        repo.WriteFile(".ai/runtime/adoption/adoption.lock.json", $$"""
        {
          "schema_version": "legacy_adoption.intake_lock.v0.1.0",
          "created_at": "{{DateTimeOffset.UtcNow:O}}",
          "expires_at": "{{DateTimeOffset.UtcNow.AddMinutes(10):O}}",
          "takeover_allowed": false
        }
        """);
        var beforeFresh = repo.AllRelativeFiles();
        var fresh = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "intake", "--json");
        Assert.Equal(20, fresh.ExitCode);
        Assert.Equal(beforeFresh, repo.AllRelativeFiles());

        File.Delete(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "adoption.lock.json"));
        repo.WriteFile(".ai/runtime/adoption/adoption.lock.json", $$"""
        {
          "schema_version": "legacy_adoption.intake_lock.v0.1.0",
          "created_at": "{{DateTimeOffset.UtcNow.AddMinutes(-30):O}}",
          "expires_at": "{{DateTimeOffset.UtcNow.AddMinutes(-1):O}}",
          "takeover_allowed": false
        }
        """);
        var expired = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "intake", "--json");
        Assert.Equal(25, expired.ExitCode);
        Assert.True(File.Exists(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "adoption.lock.json")));
        Assert.False(Directory.Exists(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "snapshots")));
    }

    [Fact]
    public void AdoptionIntake_StaticIsolation_DoesNotReferenceForbiddenWriters()
    {
        var repoRoot = ResolveRepoRoot();
        var adoptionSource = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Runtime.Cli", "FriendlyCliApplication.Adoption.cs"));

        Assert.DoesNotContain("TargetRepoAttachService", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RunStart(", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RunInit(", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CodeGraphBuilder", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TaskGraphService", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryService", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CleanupService", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DetachService", adoptionSource, StringComparison.Ordinal);
    }

    private static void AssertManifestUpdated(LegacyIntakeSandbox repo, int expectedGeneration)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "adoption.json")));
        var root = manifest.RootElement;
        Assert.Equal("legacy_adoption.attach_manifest.v0.1.0", root.GetProperty("schema_version").GetString());
        Assert.Equal("BOUND", root.GetProperty("status").GetString());
        Assert.Equal(expectedGeneration, root.GetProperty("generation").GetInt32());
        Assert.Matches("^sha256:[a-f0-9]{64}$", root.GetProperty("last_manifest_hash").GetString());
        Assert.Matches("^sha256:[a-f0-9]{64}$", root.GetProperty("last_intake_evidence_hash").GetString());
        Assert.Equal(3, root.GetProperty("last_intake_artifacts").GetArrayLength());
    }

    private static void AssertEventsAndLedgerAppendP3Evidence(LegacyIntakeSandbox repo)
    {
        var events = File.ReadAllLines(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "events.jsonl"));
        var ledger = File.ReadAllLines(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "ownership-ledger.jsonl"));
        Assert.Equal(2, events.Length);
        Assert.True(ledger.Length >= 6);

        using var eventDoc = JsonDocument.Parse(events[^1]);
        Assert.Equal("INTAKE_CAPTURED", eventDoc.RootElement.GetProperty("event_type").GetString());
        Assert.Equal("success", eventDoc.RootElement.GetProperty("result").GetString());

        var ledgerPaths = ledger
            .Select(row => JsonDocument.Parse(row))
            .Select(document => document.RootElement.GetProperty("artifact_path").GetString())
            .ToArray();
        Assert.Contains(".ai/runtime/adoption/snapshots/repo-scan-0002.json", ledgerPaths);
        Assert.Contains(".ai/runtime/adoption/snapshots/instruction-scan-0002.json", ledgerPaths);
        Assert.Contains(".ai/runtime/adoption/snapshots/dirty-diff-0002.patch", ledgerPaths);
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

    private sealed class LegacyIntakeSandbox : IDisposable
    {
        private LegacyIntakeSandbox(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static LegacyIntakeSandbox Create()
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "carves-legacy-adoption-intake-" + Guid.NewGuid().ToString("N"));
            GitTestHarness.InitializeRepository(rootPath);
            return new LegacyIntakeSandbox(rootPath);
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

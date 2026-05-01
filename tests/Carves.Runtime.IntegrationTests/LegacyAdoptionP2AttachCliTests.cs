using System.Security.Cryptography;
using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class LegacyAdoptionP2AttachCliTests
{
    [Fact]
    public void AdoptionAttach_FreshRepo_CreatesOnlyAllowedRuntimeFiles()
    {
        using var repo = LegacyAttachSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        var sourceHashBefore = repo.FileHash("README.md");

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "attach", "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("legacy_adoption.p2_attach_result.v0.1.0", root.GetProperty("schema_version").GetString());
        Assert.Equal("P2_ATTACH", root.GetProperty("phase").GetString());
        Assert.Equal("carves adoption attach --json", root.GetProperty("command").GetString());
        Assert.Equal("success", root.GetProperty("result").GetString());
        Assert.Equal("BOUND", root.GetProperty("status").GetString());
        var laterPhaseStatus = root.GetProperty("p3_to_p5_status");
        Assert.Equal("BLOCKED", laterPhaseStatus.GetProperty("P3_INTAKE").GetString());
        Assert.Equal("BLOCKED", laterPhaseStatus.GetProperty("P4_PROPOSALS").GetString());
        Assert.Equal("BLOCKED", laterPhaseStatus.GetProperty("P5_CLEANUP_DETACH").GetString());
        Assert.False(root.GetProperty("forbidden_writers_used").GetBoolean());

        var files = repo.AllRelativeFiles();
        Assert.Contains(".ai/runtime/adoption/adoption.json", files);
        Assert.Contains(".ai/runtime/adoption/events.jsonl", files);
        Assert.Contains(".ai/runtime/adoption/ownership-ledger.jsonl", files);
        Assert.DoesNotContain(".ai/runtime/adoption/adoption.lock.json", files);
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/memory/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/tasks/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/codegraph/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/patches/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/reviews/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/refactoring/", StringComparison.Ordinal));
        Assert.DoesNotContain(".ai/AGENT_BOOTSTRAP.md", files);
        Assert.DoesNotContain(".ai/PROJECT_BOUNDARY.md", files);
        Assert.DoesNotContain(".ai/STATE.md", files);
        Assert.Equal(sourceHashBefore, repo.FileHash("README.md"));
        AssertManifestValid(repo);
        AssertEventsAndLedgerValid(repo);
    }

    [Fact]
    public void AdoptionAttach_RerunAndRepoStateChanges_ReusesAdoptionId()
    {
        using var repo = LegacyAttachSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");

        var first = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "attach", "--json");
        var firstId = ReadAdoptionId(first.StandardOutput);
        LegacyAttachSandbox.RunGit(repo.RootPath, "checkout", "-b", "feature/adoption");
        repo.WriteFile("dirty.txt", "dirty\n");
        var second = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "attach", "--json");
        var secondId = ReadAdoptionId(second.StandardOutput);
        repo.WriteFile("README.md", "# Legacy repo\nchanged\n");
        LegacyAttachSandbox.RunGit(repo.RootPath, "add", "README.md");
        LegacyAttachSandbox.RunGit(repo.RootPath, "commit", "-m", "Change head");
        var third = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "attach", "--json");
        var thirdId = ReadAdoptionId(third.StandardOutput);

        Assert.Equal(0, second.ExitCode);
        Assert.Equal(0, third.ExitCode);
        Assert.Equal(firstId, secondId);
        Assert.Equal(firstId, thirdId);
        using var secondDoc = JsonDocument.Parse(second.StandardOutput);
        Assert.Equal("noop", secondDoc.RootElement.GetProperty("result").GetString());
        Assert.Equal("BOUND_NOOP", secondDoc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public void AdoptionAttach_ExistingAiWithoutManifest_BlocksWithoutWrites()
    {
        using var repo = LegacyAttachSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        repo.WriteFile(".ai/memory/human.txt", "keep me");

        var before = repo.AllRelativeFiles();
        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "attach", "--json");

        Assert.Equal(22, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal("blocked", document.RootElement.GetProperty("result").GetString());
        Assert.Equal("ADOPTION_UNKNOWN", document.RootElement.GetProperty("status").GetString());
        Assert.Equal(before, repo.AllRelativeFiles());
        Assert.False(File.Exists(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "adoption.json")));
    }

    [Fact]
    public void AdoptionAttach_LockAndCorruptManifest_BlockWithoutCleanupOrTakeover()
    {
        using var repo = LegacyAttachSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        repo.WriteFile(".ai/runtime/adoption/adoption.lock.json", $$"""
        {
          "schema_version": "legacy_adoption.attach_lock.v0.1.0",
          "created_at": "{{DateTimeOffset.UtcNow.AddMinutes(-30):O}}",
          "expires_at": "{{DateTimeOffset.UtcNow.AddMinutes(-1):O}}",
          "takeover_allowed": false
        }
        """);

        var expired = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "attach", "--json");

        Assert.Equal(25, expired.ExitCode);
        using (var document = JsonDocument.Parse(expired.StandardOutput))
        {
            Assert.Equal("recovery_required", document.RootElement.GetProperty("result").GetString());
            Assert.Equal("RECOVERY_REQUIRED", document.RootElement.GetProperty("status").GetString());
        }

        Assert.True(File.Exists(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "adoption.lock.json")));
        Assert.False(File.Exists(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "adoption.json")));

        File.Delete(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "adoption.lock.json"));
        repo.WriteFile(".ai/runtime/adoption/adoption.json", "{ not json");
        var corrupt = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "attach", "--json");

        Assert.Equal(21, corrupt.ExitCode);
        using var corruptDocument = JsonDocument.Parse(corrupt.StandardOutput);
        Assert.Equal("MANIFEST_CORRUPT", corruptDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal("{ not json", File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "adoption.json")));
    }

    [Fact]
    public void AdoptionAttach_PreservesHumanInstructionFiles()
    {
        using var repo = LegacyAttachSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.WriteFile("AGENTS.md", "human agents");
        repo.WriteFile("CLAUDE.md", "human claude");
        repo.WriteFile(".cursor/rules", "human cursor");
        repo.CommitAll("Initial commit");
        var agentsHash = repo.FileHash("AGENTS.md");
        var claudeHash = repo.FileHash("CLAUDE.md");
        var cursorHash = repo.FileHash(".cursor/rules");

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "attach", "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(agentsHash, repo.FileHash("AGENTS.md"));
        Assert.Equal(claudeHash, repo.FileHash("CLAUDE.md"));
        Assert.Equal(cursorHash, repo.FileHash(".cursor/rules"));
    }

    [Fact]
    public void AdoptionAttach_StaticIsolation_DoesNotReferenceForbiddenWriters()
    {
        var repoRoot = ResolveRepoRoot();
        var adoptionSource = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Runtime.Cli", "FriendlyCliApplication.Adoption.cs"));

        Assert.DoesNotContain("TargetRepoAttachService", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RunAttach(", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RunStart(", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RunInit(", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CodeGraphBuilder", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TaskGraphService", adoptionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("MemoryService", adoptionSource, StringComparison.Ordinal);
    }

    private static string ReadAdoptionId(string output)
    {
        using var document = JsonDocument.Parse(output);
        return document.RootElement.GetProperty("adoption_id").GetString()!;
    }

    private static void AssertManifestValid(LegacyAttachSandbox repo)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "adoption.json")));
        var root = manifest.RootElement;
        Assert.Equal("legacy_adoption.attach_manifest.v0.1.0", root.GetProperty("schema_version").GetString());
        Assert.Matches("^adopt_[A-Za-z0-9_-]+$", root.GetProperty("adoption_id").GetString());
        Assert.Equal("BOUND", root.GetProperty("status").GetString());
        Assert.Equal(1, root.GetProperty("generation").GetInt32());
        Assert.Equal("CARVES Host", root.GetProperty("canonical_owner").GetString());
        Assert.Matches("^sha256:[a-f0-9]{64}$", root.GetProperty("last_manifest_hash").GetString());
        Assert.Equal("diagnostics_and_conflict_detection_only", root.GetProperty("repo_identity").GetProperty("repo_fingerprint_role").GetString());
        Assert.Equal("unsafe_for_legacy_adoption", root.GetProperty("observed_context").GetProperty("current_attach_assessment").GetString());
    }

    private static void AssertEventsAndLedgerValid(LegacyAttachSandbox repo)
    {
        var events = File.ReadAllLines(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "events.jsonl"));
        var ledger = File.ReadAllLines(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "ownership-ledger.jsonl"));
        Assert.Single(events);
        Assert.Equal(3, ledger.Length);

        using var eventDoc = JsonDocument.Parse(events[0]);
        Assert.Equal("legacy_adoption.attach_event.v0.1.0", eventDoc.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("ATTACH_BOUND", eventDoc.RootElement.GetProperty("event_type").GetString());
        Assert.Equal("success", eventDoc.RootElement.GetProperty("result").GetString());

        var ledgerPaths = ledger
            .Select(row => JsonDocument.Parse(row))
            .Select(document => document.RootElement.GetProperty("artifact_path").GetString())
            .ToArray();
        Assert.Contains(".ai/runtime/adoption/adoption.json", ledgerPaths);
        Assert.Contains(".ai/runtime/adoption/events.jsonl", ledgerPaths);
        Assert.Contains(".ai/runtime/adoption/ownership-ledger.jsonl", ledgerPaths);
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

    private sealed class LegacyAttachSandbox : IDisposable
    {
        private LegacyAttachSandbox(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static LegacyAttachSandbox Create()
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "carves-legacy-adoption-attach-" + Guid.NewGuid().ToString("N"));
            GitTestHarness.InitializeRepository(rootPath);
            return new LegacyAttachSandbox(rootPath);
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

        public static void RunGit(string workingDirectory, params string[] arguments)
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

using System.Security.Cryptography;
using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class LegacyAdoptionP5CleanupDetachCliTests
{
    [Fact]
    public void AdoptionPlanCleanup_AfterPropose_WritesReferenceIndexAndDryRunPlanOnly()
    {
        using var repo = LegacyCleanupSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        var readmeHash = repo.FileHash("README.md");
        AttachIntakePropose(repo);

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "plan-cleanup", "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("legacy_adoption.p5_cleanup_detach_result.v0.1.0", root.GetProperty("schema_version").GetString());
        Assert.Equal("P5_CLEANUP_DETACH", root.GetProperty("phase").GetString());
        Assert.Equal("carves adoption plan-cleanup --json", root.GetProperty("command").GetString());
        Assert.Equal("success", root.GetProperty("result").GetString());
        Assert.Equal("CLEANUP_PLAN_READY", root.GetProperty("status").GetString());
        Assert.True(root.GetProperty("plan_first").GetBoolean());
        Assert.False(root.GetProperty("delete_by_prefix").GetBoolean());
        Assert.True(root.GetProperty("reference_index_derived").GetBoolean());
        Assert.False(root.GetProperty("reference_index_canonical_truth").GetBoolean());
        Assert.False(root.GetProperty("hard_delete_detach_implemented").GetBoolean());
        Assert.False(root.GetProperty("stale_lock_takeover_implemented").GetBoolean());

        var files = repo.AllRelativeFiles();
        Assert.Contains(".ai/runtime/adoption/references.json", files);
        Assert.Contains(".ai/runtime/adoption/cleanup/cleanup-plan-0004.json", files);
        Assert.DoesNotContain(".ai/runtime/adoption/adoption.lock.json", files);
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/memory/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/tasks/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/codegraph/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/patches/", StringComparison.Ordinal));
        Assert.DoesNotContain(files, path => path.StartsWith(".ai/reviews/", StringComparison.Ordinal));
        Assert.Equal(readmeHash, repo.FileHash("README.md"));

        using var references = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "references.json")));
        Assert.Equal("DERIVED_NON_CANONICAL", references.RootElement.GetProperty("status").GetString());
        Assert.False(references.RootElement.GetProperty("canonical_truth").GetBoolean());
        Assert.False(references.RootElement.GetProperty("planner_authoritative").GetBoolean());

        using var plan = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "cleanup", "cleanup-plan-0004.json")));
        Assert.Equal("DRY_RUN", plan.RootElement.GetProperty("status").GetString());
        Assert.True(plan.RootElement.GetProperty("dry_run_only").GetBoolean());
        Assert.False(plan.RootElement.GetProperty("delete_by_prefix").GetBoolean());
        Assert.False(plan.RootElement.GetProperty("physical_delete_planned").GetBoolean());
        Assert.Contains(
            plan.RootElement.GetProperty("candidates").EnumerateArray(),
            candidate => candidate.GetProperty("path").GetString() == ".ai/runtime/adoption/proposals/memory-seed.proposal.md"
                && candidate.TryGetProperty("canonical_path", out _)
                && candidate.GetProperty("path_guard").GetProperty("decision").GetString() == "allow"
                && candidate.GetProperty("decision").GetString() == "archive_only");
    }

    [Fact]
    public void AdoptionCleanupApply_RequiresPlanHashAndWritesProofWithoutDeletingSource()
    {
        using var repo = LegacyCleanupSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        var readmeHash = repo.FileHash("README.md");
        AttachIntakePropose(repo);
        Assert.Equal(0, CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "plan-cleanup", "--json").ExitCode);
        var planHash = ReadPlanHash(repo);

        var mismatch = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "cleanup", "--apply", "0004", "--plan-hash", "sha256:bad", "--json");
        Assert.Equal(26, mismatch.ExitCode);
        using (var mismatchDocument = JsonDocument.Parse(mismatch.StandardOutput))
        {
            Assert.Equal("PLAN_HASH_MISMATCH", mismatchDocument.RootElement.GetProperty("status").GetString());
        }

        var apply = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "cleanup", "--apply", "0004", "--plan-hash", planHash, "--json");

        Assert.Equal(0, apply.ExitCode);
        using var document = JsonDocument.Parse(apply.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("success", root.GetProperty("result").GetString());
        Assert.Equal("CLEANUP_APPLIED", root.GetProperty("status").GetString());
        Assert.Equal(0, root.GetProperty("deleted_paths").GetArrayLength());
        Assert.True(root.GetProperty("archived_paths").GetArrayLength() > 0);
        Assert.False(root.GetProperty("source_files_deleted").GetBoolean());
        Assert.False(root.GetProperty("approved_truth_deleted").GetBoolean());
        Assert.False(root.GetProperty("referenced_evidence_deleted").GetBoolean());

        Assert.Equal(readmeHash, repo.FileHash("README.md"));
        Assert.True(File.Exists(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "cleanup", "cleanup-proof-0004.json")));
        Assert.True(Directory.Exists(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "archive", "0004")));

        using var proof = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "cleanup", "cleanup-proof-0004.json")));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "adoption.json")));
        Assert.Equal(manifest.RootElement.GetProperty("adoption_id").GetString(), proof.RootElement.GetProperty("adoption_id").GetString());
        Assert.Equal(planHash, proof.RootElement.GetProperty("applied_plan_hash").GetString());
        Assert.True(proof.RootElement.GetProperty("postflight_checks").GetProperty("deleted_paths_subset_of_plan").GetBoolean());
        Assert.False(proof.RootElement.GetProperty("postflight_checks").GetProperty("source_files_deleted").GetBoolean());
    }

    [Fact]
    public void AdoptionCleanupApply_RefusesArtifactHashChangedAfterPlan()
    {
        using var repo = LegacyCleanupSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        AttachIntakePropose(repo);
        Assert.Equal(0, CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "plan-cleanup", "--json").ExitCode);
        var planHash = ReadPlanHash(repo);
        var proposalPath = Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "proposals", "memory-seed.proposal.md");
        File.AppendAllText(proposalPath, "\npost-plan mutation\n");

        var apply = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "cleanup", "--apply", "0004", "--plan-hash", planHash, "--json");

        Assert.Equal(27, apply.ExitCode);
        using var document = JsonDocument.Parse(apply.StandardOutput);
        Assert.Equal("blocked", document.RootElement.GetProperty("result").GetString());
        Assert.Equal("STALE_CLEANUP_PLAN", document.RootElement.GetProperty("status").GetString());
        Assert.Contains(
            document.RootElement.GetProperty("refused_paths").EnumerateArray(),
            path => path.GetString() == ".ai/runtime/adoption/proposals/memory-seed.proposal.md");
        Assert.False(Directory.Exists(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "archive", "0004")));

        using var proof = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "cleanup", "cleanup-proof-0004.json")));
        Assert.Equal("REFUSED", proof.RootElement.GetProperty("status").GetString());
        Assert.Contains(
            proof.RootElement.GetProperty("refusal_evidence").EnumerateArray(),
            item => item.GetProperty("path").GetString() == ".ai/runtime/adoption/proposals/memory-seed.proposal.md"
                && item.GetProperty("reason").GetString() == "hash_mismatch_after_plan"
                && item.GetProperty("path_guard").GetProperty("decision").GetString() == "allow");
    }

    [Fact]
    public void AdoptionDetach_DefaultSoftDetachKeepsSourceAndAuditArtifacts()
    {
        using var repo = LegacyCleanupSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.WriteFile("AGENTS.md", "human instructions\n");
        repo.CommitAll("Initial commit");
        var readmeHash = repo.FileHash("README.md");
        var agentsHash = repo.FileHash("AGENTS.md");
        AttachIntakePropose(repo);

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "detach", "--json");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal("DETACHED_SOFT", document.RootElement.GetProperty("status").GetString());
        Assert.False(document.RootElement.GetProperty("hard_delete_detach_implemented").GetBoolean());
        Assert.Equal(readmeHash, repo.FileHash("README.md"));
        Assert.Equal(agentsHash, repo.FileHash("AGENTS.md"));
        Assert.True(File.Exists(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "events.jsonl")));
        Assert.True(File.Exists(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "ownership-ledger.jsonl")));

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "adoption.json")));
        Assert.Equal("DETACHED_SOFT", manifest.RootElement.GetProperty("status").GetString());
        Assert.False(manifest.RootElement.GetProperty("active_binding").GetBoolean());
    }

    [Fact]
    public void AdoptionDetach_WithCleanupAppliesProofBeforeSoftDetach()
    {
        using var repo = LegacyCleanupSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        AttachIntakePropose(repo);
        Assert.Equal(0, CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "plan-cleanup", "--json").ExitCode);
        var planHash = ReadPlanHash(repo);

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "detach", "--cleanup", "0004", "--plan-hash", planHash, "--json");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal("DETACHED_SOFT", document.RootElement.GetProperty("status").GetString());
        Assert.True(File.Exists(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "cleanup", "cleanup-proof-0004.json")));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "adoption.json")));
        Assert.Equal("DETACHED_SOFT", manifest.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public void AdoptionDetach_WithCleanupRefusesDetachAfterPostPlanMutation()
    {
        using var repo = LegacyCleanupSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        AttachIntakePropose(repo);
        Assert.Equal(0, CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "plan-cleanup", "--json").ExitCode);
        var planHash = ReadPlanHash(repo);
        var proposalPath = Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "proposals", "memory-seed.proposal.md");
        File.AppendAllText(proposalPath, "\npost-plan mutation\n");

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "detach", "--cleanup", "0004", "--plan-hash", planHash, "--json");

        Assert.Equal(27, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        Assert.NotEqual("DETACHED_SOFT", document.RootElement.GetProperty("status").GetString());
        Assert.Equal("STALE_CLEANUP_PLAN", document.RootElement.GetProperty("status").GetString());
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "adoption.json")));
        Assert.Equal("BOUND", manifest.RootElement.GetProperty("status").GetString());
        Assert.False(Directory.Exists(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "archive", "0004")));
    }

    [Fact]
    public void AdoptionPlanCleanup_RefusesSymlinkEscapeWithEvidence()
    {
        using var repo = LegacyCleanupSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        AttachIntakePropose(repo);
        var outsidePath = Path.Combine(Path.GetTempPath(), "carves-p5-outside-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(outsidePath, "outside\n");
        try
        {
            var linkRelativePath = ".ai/runtime/adoption/proposals/escape.proposal.md";
            var linkFullPath = Path.Combine(repo.RootPath, linkRelativePath.Replace('/', Path.DirectorySeparatorChar));
            File.CreateSymbolicLink(linkFullPath, outsidePath);
            AppendLedgerRecord(repo, linkRelativePath, "p4_memory_seed_proposal", "sha256:outside");

            var result = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "plan-cleanup", "--json");

            Assert.Equal(0, result.ExitCode);
            var candidate = ReadPlanCandidate(repo, linkRelativePath);
            Assert.Equal("refuse", candidate.GetProperty("decision").GetString());
            Assert.Equal("symlink_escape", candidate.GetProperty("reason").GetString());
            Assert.Equal("refuse", candidate.GetProperty("path_guard").GetProperty("decision").GetString());
            Assert.Equal("symlink_escape", candidate.GetProperty("path_guard").GetProperty("reason").GetString());
            Assert.True(candidate.GetProperty("path_guard").GetProperty("symlink").GetBoolean());
            Assert.True(candidate.TryGetProperty("canonical_path", out _));
        }
        finally
        {
            try
            {
                File.Delete(outsidePath);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void AdoptionPlanCleanup_RefusesCaseCollisionAmbiguityWithEvidence()
    {
        using var repo = LegacyCleanupSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        AttachIntakePropose(repo);
        var lowerPath = ".ai/runtime/adoption/proposals/case-collision.proposal.md";
        var upperPath = ".ai/runtime/adoption/proposals/CASE-COLLISION.proposal.md";
        repo.WriteFile(lowerPath, "lower\n");
        repo.WriteFile(upperPath, "upper\n");
        AppendLedgerRecord(repo, lowerPath, "p4_memory_seed_proposal", "sha256:" + repo.FileHash(lowerPath));
        AppendLedgerRecord(repo, upperPath, "p4_memory_seed_proposal", "sha256:" + repo.FileHash(upperPath));

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "plan-cleanup", "--json");

        Assert.Equal(0, result.ExitCode);
        var lower = ReadPlanCandidate(repo, lowerPath);
        var upper = ReadPlanCandidate(repo, upperPath);
        Assert.Equal("refuse", lower.GetProperty("decision").GetString());
        Assert.Equal("case_collision_ambiguity", lower.GetProperty("path_guard").GetProperty("reason").GetString());
        Assert.True(lower.GetProperty("path_guard").GetProperty("case_collision_ambiguity").GetBoolean());
        Assert.Equal("refuse", upper.GetProperty("decision").GetString());
        Assert.Equal("case_collision_ambiguity", upper.GetProperty("path_guard").GetProperty("reason").GetString());
        Assert.True(upper.GetProperty("path_guard").GetProperty("case_collision_ambiguity").GetBoolean());
    }

    [Fact]
    public void AdoptionP5_LockConflictAndExpiredLock_DoNotTakeOver()
    {
        using var repo = LegacyCleanupSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        AttachIntakePropose(repo);
        repo.WriteFile(".ai/runtime/adoption/adoption.lock.json", $$"""
        {
          "schema_version": "legacy_adoption.p5_cleanup_lock.v0.1.0",
          "created_at": "{{DateTimeOffset.UtcNow:O}}",
          "expires_at": "{{DateTimeOffset.UtcNow.AddMinutes(10):O}}",
          "takeover_allowed": false
        }
        """);
        var beforeFresh = repo.AllRelativeFiles();
        var fresh = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "plan-cleanup", "--json");
        Assert.Equal(20, fresh.ExitCode);
        Assert.Equal(beforeFresh, repo.AllRelativeFiles());

        File.Delete(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "adoption.lock.json"));
        repo.WriteFile(".ai/runtime/adoption/adoption.lock.json", $$"""
        {
          "schema_version": "legacy_adoption.p5_cleanup_lock.v0.1.0",
          "created_at": "{{DateTimeOffset.UtcNow.AddMinutes(-30):O}}",
          "expires_at": "{{DateTimeOffset.UtcNow.AddMinutes(-1):O}}",
          "takeover_allowed": false
        }
        """);
        var expired = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "detach", "--json");
        Assert.Equal(25, expired.ExitCode);
        Assert.True(File.Exists(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "adoption.lock.json")));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "adoption.json")));
        Assert.Equal("BOUND", manifest.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public void AdoptionP5_StaticIsolation_DoesNotReferenceForbiddenWriterPaths()
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

    private static void AttachIntakePropose(LegacyCleanupSandbox repo)
    {
        Assert.Equal(0, CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "attach", "--json").ExitCode);
        Assert.Equal(0, CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "intake", "--json").ExitCode);
        Assert.Equal(0, CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "propose", "--json").ExitCode);
    }

    private static string ReadPlanHash(LegacyCleanupSandbox repo)
    {
        using var plan = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "cleanup", "cleanup-plan-0004.json")));
        return plan.RootElement.GetProperty("plan_hash").GetString()!;
    }

    private static JsonElement ReadPlanCandidate(LegacyCleanupSandbox repo, string relativePath)
    {
        using var plan = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "cleanup", "cleanup-plan-0004.json")));
        foreach (var candidate in plan.RootElement.GetProperty("candidates").EnumerateArray())
        {
            if (candidate.GetProperty("path").GetString() == relativePath)
            {
                return candidate.Clone();
            }
        }

        throw new InvalidOperationException("Plan candidate not found: " + relativePath);
    }

    private static void AppendLedgerRecord(LegacyCleanupSandbox repo, string artifactPath, string artifactKind, string hash)
    {
        var adoptionId = ReadAdoptionId(repo);
        File.AppendAllText(
            Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "ownership-ledger.jsonl"),
            JsonSerializer.Serialize(new
            {
                schema_version = "legacy_adoption.ownership_ledger.v0.1.0",
                ledger_record_id = "led_" + Guid.NewGuid().ToString("N"),
                adoption_id = adoptionId,
                artifact_path = artifactPath,
                artifact_kind = artifactKind,
                owner = "CARVES",
                hash,
                created_at = DateTimeOffset.UtcNow.ToString("O"),
                delete_policy = "retain_until_p5_cleanup_contract",
                human_modified = false,
            }) + Environment.NewLine);
    }

    private static string ReadAdoptionId(LegacyCleanupSandbox repo)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "runtime", "adoption", "adoption.json")));
        return manifest.RootElement.GetProperty("adoption_id").GetString()!;
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

    private sealed class LegacyCleanupSandbox : IDisposable
    {
        private LegacyCleanupSandbox(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static LegacyCleanupSandbox Create()
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "carves-legacy-adoption-p5-" + Guid.NewGuid().ToString("N"));
            GitTestHarness.InitializeRepository(rootPath);
            return new LegacyCleanupSandbox(rootPath);
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

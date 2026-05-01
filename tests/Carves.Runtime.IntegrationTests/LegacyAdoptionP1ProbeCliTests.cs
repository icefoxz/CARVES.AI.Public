using System.Security.Cryptography;
using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class LegacyAdoptionP1ProbeCliTests
{
    [Fact]
    public void AdoptionProbe_CleanGitRepo_EmitsRedactedJsonAndDoesNotWriteTargetRepo()
    {
        using var repo = LegacyProbeSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        var indexHashBefore = repo.GitIndexHash();
        var lockFilesBefore = repo.GitLockFiles();

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "probe", "--json");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("0.3.2", root.GetProperty("schema_version").GetString());
        Assert.Equal("P1_PROBE", root.GetProperty("phase").GetString());
        Assert.Equal("carves adoption probe --json", root.GetProperty("command").GetString());
        Assert.Equal("success_with_risk", root.GetProperty("result").GetString());
        Assert.Equal("proposed_future_command", root.GetProperty("command_status").GetString());
        Assert.False(root.GetProperty("observed_context").GetProperty("network_allowed").GetBoolean());
        Assert.False(root.GetProperty("observed_context").GetProperty("full_inventory_emitted").GetBoolean());
        Assert.Equal("clean", root.GetProperty("dirty_state").GetProperty("working_tree_state").GetString());
        Assert.Equal(0, root.GetProperty("dirty_state").GetProperty("dirty_file_count").GetInt32());
        Assert.Contains(root.GetProperty("risk_flags").EnumerateArray(), flag => flag.GetString() == "current_attach_unsafe_for_legacy");
        Assert.Equal("absent", root.GetProperty("existing_carves_state").GetProperty("ai_directory_presence").GetString());
        Assert.False(root.GetProperty("existing_carves_state").GetProperty("ai_governance_content_read").GetBoolean());
        Assert.False(root.GetProperty("existing_carves_state").GetProperty("codegraph_content_read").GetBoolean());
        Assert.True(root.GetProperty("read_only_postflight").GetProperty("computed").GetBoolean());
        Assert.True(root.GetProperty("read_only_postflight").GetProperty("git_index_unchanged").GetBoolean());
        Assert.True(root.GetProperty("read_only_postflight").GetProperty("no_new_git_lock_files").GetBoolean());
        Assert.True(root.GetProperty("read_only_postflight").GetProperty("repo_state_unchanged").GetBoolean());
        Assert.False(Directory.Exists(Path.Combine(repo.RootPath, ".ai")));
        Assert.Equal(indexHashBefore, repo.GitIndexHash());
        Assert.Equal(lockFilesBefore, repo.GitLockFiles());
    }

    [Fact]
    public void AdoptionProbe_DirtyRepoAndInstructionFiles_DoNotEmitContentsOrRemoteUrl()
    {
        using var repo = LegacyProbeSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        LegacyProbeSandbox.RunGit(repo.RootPath, "remote", "add", "origin", "https://user:secret@example.com/private/repo.git?token=secret");
        repo.WriteFile("AGENTS.md", "SECRET_AGENT_INSTRUCTIONS");
        repo.WriteFile("CLAUDE.md", "SECRET_CLAUDE_INSTRUCTIONS");
        repo.WriteFile(".cursor/rules", "SECRET_CURSOR_RULES");
        repo.WriteFile("src/app.txt", "dirty work");

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "probe", "--json");

        Assert.Equal(1, result.ExitCode);
        Assert.DoesNotContain("SECRET_AGENT_INSTRUCTIONS", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_CLAUDE_INSTRUCTIONS", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_CURSOR_RULES", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("user:secret", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("private/repo", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("token=secret", result.StandardOutput, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        var flags = root.GetProperty("risk_flags").EnumerateArray().Select(flag => flag.GetString()).ToArray();
        Assert.Contains("dirty_working_tree", flags);
        Assert.Contains("existing_human_AGENTS_file", flags);
        Assert.Contains("existing_human_CLAUDE_file", flags);
        Assert.Contains("existing_cursor_rules", flags);
        Assert.Equal("dirty", root.GetProperty("dirty_state").GetProperty("working_tree_state").GetString());
        Assert.True(root.GetProperty("dirty_state").GetProperty("dirty_file_count").GetInt32() >= 4);
        Assert.False(root.GetProperty("dirty_state").GetProperty("dirty_diff_content_emitted").GetBoolean());
        Assert.Equal(1, root.GetProperty("repo_identity").GetProperty("remote_count").GetInt32());
        Assert.Equal("redacted", root.GetProperty("repo_identity").GetProperty("remote_host_classification").GetString());
        foreach (var item in root.GetProperty("instruction_files").EnumerateArray().Where(item => item.GetProperty("exists").GetBoolean()))
        {
            Assert.False(item.GetProperty("body_read").GetBoolean());
            Assert.False(item.GetProperty("excerpt_emitted").GetBoolean());
            Assert.Equal("operator_review_required", item.GetProperty("recommended_policy").GetString());
        }
    }

    [Fact]
    public void AdoptionProbe_ExistingAi_DetectsPresenceWithoutReadingGovernanceContents()
    {
        using var repo = LegacyProbeSandbox.Create();
        repo.WriteFile("README.md", "# Legacy repo\n");
        repo.CommitAll("Initial commit");
        repo.WriteFile(".ai/memory/secret.txt", "SECRET_GOVERNANCE_PAYLOAD");

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "adoption", "probe", "--json");

        Assert.Equal(11, result.ExitCode);
        Assert.DoesNotContain("SECRET_GOVERNANCE_PAYLOAD", result.StandardOutput, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("present", root.GetProperty("existing_carves_state").GetProperty("ai_directory_presence").GetString());
        Assert.Equal("absent", root.GetProperty("existing_carves_state").GetProperty("adoption_manifest_presence").GetString());
        Assert.False(root.GetProperty("existing_carves_state").GetProperty("ai_governance_content_read").GetBoolean());
        Assert.False(root.GetProperty("existing_carves_state").GetProperty("codegraph_content_read").GetBoolean());
        Assert.Contains(root.GetProperty("risk_flags").EnumerateArray(), flag => flag.GetString() == "existing_ai_without_manifest");
    }

    [Fact]
    public void AdoptionProbe_NonGitRepo_ReturnsJsonBlockedEnvelope()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "carves-adoption-probe-non-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        try
        {
            var result = CliProgramHarness.RunInDirectory(rootPath, "adoption", "probe", "--json");

            Assert.Equal(10, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardError);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("blocked", root.GetProperty("result").GetString());
            Assert.Equal("not_git", root.GetProperty("repo_identity").GetProperty("root_kind").GetString());
            Assert.Equal("not_a_git_repository", root.GetProperty("recommended_next_action").GetString());
            Assert.Contains(root.GetProperty("risk_flags").EnumerateArray(), flag => flag.GetString() == "not_a_git_repository");
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    private sealed class LegacyProbeSandbox : IDisposable
    {
        private LegacyProbeSandbox(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static LegacyProbeSandbox Create()
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "carves-legacy-adoption-probe-" + Guid.NewGuid().ToString("N"));
            GitTestHarness.InitializeRepository(rootPath);
            return new LegacyProbeSandbox(rootPath);
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

        public string GitIndexHash()
        {
            var indexPath = Path.Combine(RootPath, ".git", "index");
            using var stream = File.OpenRead(indexPath);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        public string[] GitLockFiles()
        {
            return Directory
                .EnumerateFiles(Path.Combine(RootPath, ".git"), "*.lock", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => name is not null)
                .Select(name => name!)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

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

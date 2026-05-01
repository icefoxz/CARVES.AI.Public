using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class HandoffInspectCliTests
{
    [Fact]
    public void HandoffInspectJson_ReturnsReadyForValidPacket()
    {
        using var repo = GitSandbox.Create();
        repo.WriteFile("handoff.json", ValidPacket());

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "handoff", "inspect", "handoff.json", "--json");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("carves-continuity-handoff-inspection.v1", root.GetProperty("schema_version").GetString());
        Assert.Equal("usable", root.GetProperty("inspection_status").GetString());
        Assert.Equal("ready", root.GetProperty("readiness").GetProperty("decision").GetString());
        Assert.Equal("HND-CLI-001", root.GetProperty("handoff_id").GetString());
        Assert.Equal(1, root.GetProperty("context_refs").GetArrayLength());
    }

    [Fact]
    public void HandoffInspectJson_FailsClosedForMissingPacket()
    {
        using var repo = GitSandbox.Create();

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "handoff", "inspect", "missing.json", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("missing", root.GetProperty("inspection_status").GetString());
        Assert.Equal("invalid", root.GetProperty("readiness").GetProperty("decision").GetString());
        Assert.Contains(root.GetProperty("diagnostics").EnumerateArray(), diagnostic =>
            diagnostic.GetProperty("code").GetString() == "packet.missing");
    }

    [Fact]
    public void HandoffInspectWithoutPath_PrintsUsage()
    {
        using var repo = GitSandbox.Create();

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "handoff", "inspect", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal(".ai/handoff/handoff.json", root.GetProperty("packet_path").GetString());
        Assert.Equal("missing", root.GetProperty("inspection_status").GetString());
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public void HandoffInspectUnknownOption_PrintsUsage()
    {
        using var repo = GitSandbox.Create();
        repo.WriteFile("handoff.json", ValidPacket());

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "handoff", "inspect", "handoff.json", "--write");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown option: --write", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("carves handoff inspect [packet-path]", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void HandoffHelp_DescribesReadOnlyBoundary()
    {
        var result = CliProgramHarness.RunInDirectory(Path.GetTempPath(), "help", "handoff");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("carves handoff inspect [packet-path]", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves handoff draft [packet-path]", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves handoff next [packet-path]", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Exit codes:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("inspect  0 ready; 1 invalid/operator-review/stale/blocked/low-confidence; 2 usage.", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("draft    0 skeleton written and inspectable; 1 exists/protected/write failure; 2 usage.", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("next     0 action=continue; 1 operator_review_first/reorient_first/blocked/invalid; 2 usage.", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Default packet path: .ai/handoff/handoff.json.", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Read-only", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("low-confidence skeleton", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("read-only raw projection", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Does not write", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void HandoffWithoutSubcommand_PrintsProductHardenedUsage()
    {
        using var repo = GitSandbox.Create();

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "handoff");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("carves handoff inspect [packet-path]", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("carves handoff draft [packet-path]", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("carves handoff next [packet-path]", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("Default packet path: .ai/handoff/handoff.json", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("Handoff draft writes only low-confidence skeletons and refuses overwrite.", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("Handoff next is a read-only raw projection.", result.StandardError, StringComparison.Ordinal);
        Assert.Contains("carves help handoff", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void HandoffDraftJson_WritesSkeletonAndInspectsOperatorReviewRequired()
    {
        using var repo = GitSandbox.Create();

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "handoff", "draft", "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(repo.RootPath, ".ai", "handoff", "handoff.json")));
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal(".ai/handoff/handoff.json", root.GetProperty("packet_path").GetString());
        Assert.Equal("carves-continuity-handoff-draft.v1", root.GetProperty("schema_version").GetString());
        Assert.Equal("written", root.GetProperty("draft_status").GetString());
        Assert.True(root.GetProperty("written").GetBoolean());
        Assert.Equal("operator_review_required", root.GetProperty("inspection").GetProperty("readiness").GetProperty("decision").GetString());

        using var packet = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo.RootPath, ".ai", "handoff", "handoff.json")));
        Assert.Equal("operator_review_required", packet.RootElement.GetProperty("resume_status").GetString());
        Assert.Equal("low", packet.RootElement.GetProperty("confidence").GetString());
    }

    [Fact]
    public void HandoffDraftJson_RefusesExistingPacket()
    {
        using var repo = GitSandbox.Create();
        repo.WriteFile("handoff-draft.json", "{}");

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "handoff", "draft", "handoff-draft.json", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("exists", root.GetProperty("draft_status").GetString());
        Assert.False(root.GetProperty("written").GetBoolean());
        Assert.Contains(root.GetProperty("diagnostics").EnumerateArray(), diagnostic =>
            diagnostic.GetProperty("code").GetString() == "draft.target_exists");
        Assert.Equal("{}", File.ReadAllText(Path.Combine(repo.RootPath, "handoff-draft.json")));
    }

    [Fact]
    public void HandoffDraftJson_RefusesProtectedPath()
    {
        using var repo = GitSandbox.Create();

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "handoff", "draft", ".ai/tasks/handoff-draft.json", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("protected_path", root.GetProperty("draft_status").GetString());
        Assert.False(root.GetProperty("written").GetBoolean());
        Assert.False(File.Exists(Path.Combine(repo.RootPath, ".ai", "tasks", "handoff-draft.json")));
    }

    [Fact]
    public void HandoffNextJson_ReturnsContinueForReadyPacket()
    {
        using var repo = GitSandbox.Create();
        repo.WriteFile("handoff.json", ValidPacket());

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "handoff", "next", "handoff.json", "--json");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("carves-continuity-handoff-next.v1", root.GetProperty("schema_version").GetString());
        Assert.Equal("continue", root.GetProperty("action").GetString());
        Assert.Equal("ready", root.GetProperty("readiness").GetProperty("decision").GetString());
        Assert.Equal("Test CLI handoff inspection.", root.GetProperty("current_objective").GetString());
        Assert.Equal(1, root.GetProperty("context_refs").GetArrayLength());
        Assert.Equal(1, root.GetProperty("evidence_refs").GetArrayLength());
    }

    [Fact]
    public void HandoffNextJson_ReturnsNonZeroForOperatorReviewDraft()
    {
        using var repo = GitSandbox.Create();
        var draft = CliProgramHarness.RunInDirectory(repo.RootPath, "handoff", "draft", "handoff-draft.json", "--json");

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "handoff", "next", "handoff-draft.json", "--json");

        Assert.Equal(0, draft.ExitCode);
        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("operator_review_first", root.GetProperty("action").GetString());
        Assert.Equal("operator_review_required", root.GetProperty("readiness").GetProperty("decision").GetString());
        Assert.Equal("low", root.GetProperty("confidence").GetString());
    }

    [Fact]
    public void HandoffNextJson_FailsClosedForBranchMismatch()
    {
        using var repo = GitSandbox.Create();
        repo.WriteFile("seed.txt", "seed");
        repo.CommitAll("seed");
        repo.WriteFile("handoff.json", ValidPacketWithRepo("feature/stale", "unknown", "unknown"));

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "handoff", "next", "handoff.json", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("reorient_first", root.GetProperty("action").GetString());
        Assert.Equal("stale", root.GetProperty("inspection_status").GetString());
        Assert.Equal("reorient_first", root.GetProperty("readiness").GetProperty("decision").GetString());
        Assert.Contains(root.GetProperty("diagnostics").EnumerateArray(), diagnostic =>
            diagnostic.GetProperty("code").GetString() == "repo.branch_mismatch");
    }

    private static string ValidPacket()
    {
        return """
        {
          "schema_version": "carves-continuity-handoff.v1",
          "handoff_id": "HND-CLI-001",
          "created_at_utc": "2026-04-14T00:00:00Z",
          "producer": {
            "agent": "test"
          },
          "repo": {
            "name": "CARVES.Runtime"
          },
          "resume_status": "ready",
          "current_objective": "Test CLI handoff inspection.",
          "current_cursor": {
            "kind": "manual",
            "id": "TEST"
          },
          "completed_facts": [
            {
              "statement": "A bounded handoff packet exists.",
              "evidence_refs": [
                "docs/runtime/example.md"
              ],
              "confidence": "high"
            }
          ],
          "remaining_work": [
            {
              "action": "Inspect the packet."
            }
          ],
          "blocked_reasons": [],
          "must_not_repeat": [
            {
              "item": "Do not rediscover broad context.",
              "reason": "The packet already lists bounded context refs."
            }
          ],
          "open_questions": [],
          "decision_refs": [],
          "evidence_refs": [
            {
              "kind": "doc",
              "ref": "docs/runtime/example.md",
              "summary": "Example evidence."
            }
          ],
          "context_refs": [
            {
              "ref": "docs/runtime/example.md",
              "reason": "Provides bounded context.",
              "priority": 1
            }
          ],
          "recommended_next_action": {
            "action": "Read the listed context refs.",
            "rationale": "Avoid broad rediscovery."
          },
          "confidence": "medium",
          "confidence_notes": [
            "Test fixture."
          ]
        }
        """;
    }

    private static string ValidPacketWithRepo(string branch, string baseCommit, string dirtyState)
    {
        return ValidPacket().Replace("""
          "repo": {
            "name": "CARVES.Runtime"
          },
        """, $$"""
          "repo": {
            "name": "CARVES.Runtime",
            "branch": "{{branch}}",
            "base_commit": "{{baseCommit}}",
            "dirty_state": "{{dirtyState}}"
          },
        """, StringComparison.Ordinal);
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
            var root = Path.Combine(Path.GetTempPath(), "carves-handoff-cli-tests", Guid.NewGuid().ToString("N"));
            GitTestHarness.InitializeRepository(root);
            return new GitSandbox(root);
        }

        public void WriteFile(string relativePath, string content)
        {
            var fullPath = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }

        public void CommitAll(string message)
        {
            GitTestHarness.Run(RootPath, "add", ".");
            GitTestHarness.Run(RootPath, "commit", "-m", message);
        }

        public void Dispose()
        {
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

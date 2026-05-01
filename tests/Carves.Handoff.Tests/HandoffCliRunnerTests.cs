using System.Text.Json;
using Carves.Handoff.Core;

namespace Carves.Handoff.Tests;

public sealed class HandoffCliRunnerTests
{
    [Fact]
    public void Run_DraftAndNextUseExplicitRepoRootWithoutRuntimeShell()
    {
        using var workspace = new TemporaryWorkspace();

        var draft = Capture(() => HandoffCliRunner.Run([
            "--repo-root",
            workspace.RootPath,
            "draft",
            "handoff.json",
            "--json",
        ]));

        Assert.Equal(0, draft.ExitCode);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, "handoff.json")));
        using var draftJson = JsonDocument.Parse(draft.StandardOutput);
        Assert.Equal("carves-continuity-handoff-draft.v1", draftJson.RootElement.GetProperty("schema_version").GetString());

        var next = Capture(() => HandoffCliRunner.Run([
            "--repo-root",
            workspace.RootPath,
            "next",
            "handoff.json",
            "--json",
        ]));

        Assert.Equal(1, next.ExitCode);
        using var nextJson = JsonDocument.Parse(next.StandardOutput);
        Assert.Equal("operator_review_first", nextJson.RootElement.GetProperty("action").GetString());
        Assert.Equal("operator_review_required", nextJson.RootElement.GetProperty("readiness").GetProperty("decision").GetString());
    }

    [Fact]
    public void Run_WithoutRepoRootWalksUpToNearestGitRepository()
    {
        using var workspace = new TemporaryWorkspace();
        Directory.CreateDirectory(Path.Combine(workspace.RootPath, ".git"));
        var nestedPath = Path.Combine(workspace.RootPath, "nested", "project");
        Directory.CreateDirectory(nestedPath);

        var result = CaptureIn(nestedPath, () => HandoffCliRunner.Run([
            "draft",
            "--json",
        ]));

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".ai", "handoff", "handoff.json")));
        Assert.False(File.Exists(Path.Combine(nestedPath, ".ai", "handoff", "handoff.json")));
        using var document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal(".ai/handoff/handoff.json", document.RootElement.GetProperty("packet_path").GetString());
    }

    [Fact]
    public void Run_MissingPathReturnsUsageExitCode()
    {
        using var workspace = new TemporaryWorkspace();

        var result = Capture(() => HandoffCliRunner.Run([
            "--repo-root",
            workspace.RootPath,
            "inspect",
            "--json",
        ]));

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal(".ai/handoff/handoff.json", root.GetProperty("packet_path").GetString());
        Assert.Equal("missing", root.GetProperty("inspection_status").GetString());
        Assert.Equal("invalid", root.GetProperty("readiness").GetProperty("decision").GetString());
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public void Run_DraftWithoutPathWritesDefaultPacket()
    {
        using var workspace = new TemporaryWorkspace();

        var result = Capture(() => HandoffCliRunner.Run([
            "--repo-root",
            workspace.RootPath,
            "draft",
            "--json",
        ]));

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".ai", "handoff", "handoff.json")));
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal(".ai/handoff/handoff.json", root.GetProperty("packet_path").GetString());
        Assert.Equal("written", root.GetProperty("draft_status").GetString());
    }

    [Fact]
    public void Run_HelpReturnsZeroAndMentionsDefaultPath()
    {
        using var workspace = new TemporaryWorkspace();

        var result = Capture(() => HandoffCliRunner.Run([
            "--repo-root",
            workspace.RootPath,
            "help",
        ]));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("carves-handoff draft [packet-path]", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(".ai/handoff/handoff.json", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("nearest git repository", result.StandardOutput, StringComparison.Ordinal);
        Assert.Empty(result.StandardError);
    }

    [Fact]
    public void Run_InspectReadyPacketReturnsZeroAndNextContinues()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/handoff/handoff.json", HandoffTestPackets.ValidPacket());

        var inspect = Capture(() => HandoffCliRunner.Run([
            "--repo-root",
            workspace.RootPath,
            "inspect",
            "--json",
        ]));
        var next = Capture(() => HandoffCliRunner.Run([
            "--repo-root",
            workspace.RootPath,
            "next",
            "--json",
        ]));

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, next.ExitCode);
        Assert.Equal("continue", JsonDocument.Parse(next.StandardOutput).RootElement.GetProperty("action").GetString());
    }

    private static CapturedRun Capture(Func<int> run)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        Console.SetOut(standardOutput);
        Console.SetError(standardError);

        try
        {
            var exitCode = run();
            return new CapturedRun(exitCode, standardOutput.ToString(), standardError.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static CapturedRun CaptureIn(string workingDirectory, Func<int> run)
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(workingDirectory);
            return Capture(run);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    private sealed record CapturedRun(int ExitCode, string StandardOutput, string StandardError);
}

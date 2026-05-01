using System.Text.Json;
using Carves.Handoff.Core;

namespace Carves.Handoff.Tests;

public sealed class ContinuityHandoffDraftServiceTests
{
    [Fact]
    public void Draft_WritesLowConfidenceSkeletonAndInspectsIt()
    {
        using var workspace = new TemporaryWorkspace();

        var result = new HandoffDraftService().Draft(workspace.RootPath, "handoff-draft.json");

        Assert.True(result.Written);
        Assert.Equal("written", result.DraftStatus);
        Assert.StartsWith("HND-DRAFT-", result.HandoffId, StringComparison.Ordinal);
        Assert.NotNull(result.Inspection);
        Assert.Equal("usable", result.Inspection.InspectionStatus);
        Assert.Equal("operator_review_required", result.Inspection.Readiness.Decision);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, "handoff-draft.json")));

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspace.RootPath, "handoff-draft.json")));
        var root = document.RootElement;
        Assert.Equal("carves-continuity-handoff.v1", root.GetProperty("schema_version").GetString());
        Assert.Equal("operator_review_required", root.GetProperty("resume_status").GetString());
        Assert.Equal("low", root.GetProperty("confidence").GetString());
        Assert.Equal(".", root.GetProperty("repo").GetProperty("root_hint").GetString());
        Assert.Contains(root.GetProperty("confidence_notes").EnumerateArray(), item =>
            item.GetString() == "repo.root_hint is a local-only hint and is not portable repository truth.");
        Assert.Equal(0, root.GetProperty("completed_facts").GetArrayLength());
    }

    [Fact]
    public void Draft_RefusesToOverwriteExistingPacket()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("handoff-draft.json", "{}");

        var result = new HandoffDraftService().Draft(workspace.RootPath, "handoff-draft.json");

        Assert.False(result.Written);
        Assert.Equal("exists", result.DraftStatus);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "draft.target_exists");
        Assert.Equal("{}", File.ReadAllText(Path.Combine(workspace.RootPath, "handoff-draft.json")));
    }

    [Fact]
    public void Draft_RefusesProtectedRuntimeTruthPaths()
    {
        using var workspace = new TemporaryWorkspace();

        var result = new HandoffDraftService().Draft(workspace.RootPath, ".ai/tasks/handoff-draft.json");

        Assert.False(result.Written);
        Assert.Equal("protected_path", result.DraftStatus);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "draft.protected_path");
    }

    [Fact]
    public void Draft_RefusesPathOutsideRepo()
    {
        using var workspace = new TemporaryWorkspace();
        var outsidePath = Path.Combine(Path.GetTempPath(), $"carves-handoff-outside-{Guid.NewGuid():N}.json");

        var result = new HandoffDraftService().Draft(workspace.RootPath, outsidePath);

        Assert.False(result.Written);
        Assert.Equal("outside_repo", result.DraftStatus);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "draft.path_outside_repo");
        Assert.False(File.Exists(outsidePath));
    }
}

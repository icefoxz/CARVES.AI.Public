using Carves.Matrix.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Carves.Matrix.Tests;

internal sealed partial class MatrixFullReleaseBundleFixture
{
    public void RemoveManifestArtifactAndRefreshProofSummary(string artifactKind)
    {
        var manifestRoot = ReadManifestObject();
        var artifacts = manifestRoot["artifacts"]!.AsArray();
        var target = artifacts.Single(artifact =>
            string.Equals(artifact?["artifact_kind"]?.GetValue<string>(), artifactKind, StringComparison.Ordinal));
        artifacts.Remove(target);
        WriteManifestObject(manifestRoot);
        RefreshProofSummaryManifestReference();
    }

    public void AppendPackagedSummaryAndRefreshProofSummary(string suffix)
    {
        File.AppendAllText(PackagedSummaryPath(), suffix);
        RefreshProofSummaryManifestReference();
    }

    public void ReplacePackagedSummaryTextAndRefreshProofSummary(string oldValue, string newValue)
    {
        var summaryPath = PackagedSummaryPath();
        var text = File.ReadAllText(summaryPath);
        if (!text.Contains(oldValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Packaged summary text was not found: {oldValue}");
        }

        File.WriteAllText(summaryPath, text.Replace(oldValue, newValue, StringComparison.Ordinal));
        RefreshProofSummaryManifestReference();
    }

    private JsonObject ReadManifestObject()
    {
        return JsonNode.Parse(File.ReadAllText(ManifestPath()))!.AsObject();
    }

    private void WriteManifestObject(JsonObject root)
    {
        File.WriteAllText(ManifestPath(), root.ToJsonString(JsonOptions));
    }

    private void RefreshProofSummaryManifestReference()
    {
        var summaryPath = Path.Combine(ArtifactRoot, "matrix-proof-summary.json");
        var root = JsonNode.Parse(File.ReadAllText(summaryPath))!.AsObject();
        var manifestVerification = MatrixArtifactManifestWriter.VerifyManifest(ManifestPath());
        var manifest = root["artifact_manifest"]!.AsObject();
        manifest["sha256"] = MatrixArtifactManifestWriter.ComputeFileSha256(ManifestPath());
        manifest["verification_posture"] = manifestVerification.VerificationPosture;
        manifest["issue_count"] = manifestVerification.Issues.Count;
        File.WriteAllText(summaryPath, root.ToJsonString(JsonOptions));
    }

    private string ManifestPath()
    {
        return Path.Combine(ArtifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName);
    }

    private string PackagedSummaryPath()
    {
        return Path.Combine(ArtifactRoot, "packaged", "matrix-packaged-summary.json");
    }
}

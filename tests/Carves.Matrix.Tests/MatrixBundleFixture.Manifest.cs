using Carves.Matrix.Core;
using System.Text.Json.Nodes;

namespace Carves.Matrix.Tests;

internal sealed partial class MatrixBundleFixture
{
    public void RemoveManifestArtifact(string artifactKind)
    {
        var root = ReadManifestObject();
        var artifacts = root["artifacts"]!.AsArray();
        var target = artifacts.Single(artifact =>
            string.Equals(artifact?["artifact_kind"]?.GetValue<string>(), artifactKind, StringComparison.Ordinal));
        artifacts.Remove(target);
        WriteManifestObject(root);
    }

    public void DuplicateManifestArtifact(string artifactKind)
    {
        var root = ReadManifestObject();
        var artifacts = root["artifacts"]!.AsArray();
        var target = artifacts.Single(artifact =>
            string.Equals(artifact?["artifact_kind"]?.GetValue<string>(), artifactKind, StringComparison.Ordinal));
        artifacts.Add(target!.DeepClone());
        WriteManifestObject(root);
    }

    public void SetArtifactManifestStringField(string artifactKind, string fieldName, string value)
    {
        var artifact = FindManifestArtifact(artifactKind, out var root);
        artifact[fieldName] = value;
        WriteManifestObject(root);
    }

    public void SetArtifactManifestLongField(string artifactKind, string fieldName, long value)
    {
        var artifact = FindManifestArtifact(artifactKind, out var root);
        artifact[fieldName] = value;
        WriteManifestObject(root);
    }

    public void SetManifestArtifactRoot(string artifactRoot)
    {
        var root = ReadManifestObject();
        root["artifact_root"] = artifactRoot;
        WriteManifestObject(root);
    }

    public void SetArtifactPrivacyFlag(string artifactKind, string flagName, bool value)
    {
        var artifact = FindManifestArtifact(artifactKind, out var root);
        artifact["privacy_flags"]![flagName] = value;
        WriteManifestObject(root);
    }

    private JsonObject FindManifestArtifact(string artifactKind, out JsonObject root)
    {
        root = ReadManifestObject();
        return root["artifacts"]!.AsArray()
            .Select(artifact => artifact!.AsObject())
            .Single(artifact => string.Equals(
                artifact["artifact_kind"]?.GetValue<string>(),
                artifactKind,
                StringComparison.Ordinal));
    }

    private JsonObject ReadManifestObject()
    {
        var manifestPath = Path.Combine(ArtifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName);
        return JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
    }

    private void WriteManifestObject(JsonObject root)
    {
        var manifestPath = Path.Combine(ArtifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName);
        File.WriteAllText(manifestPath, root.ToJsonString(JsonOptions));
    }
}

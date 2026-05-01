using Carves.Matrix.Core;
using System.Text.Json;

namespace Carves.Matrix.Tests;

internal sealed partial class MatrixBundleFixture : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private MatrixBundleFixture(string artifactRoot)
    {
        ArtifactRoot = artifactRoot;
    }

    public string ArtifactRoot { get; }

    public static MatrixBundleFixture Create(string? shieldEvaluationJson = null, string? artifactRoot = null)
    {
        artifactRoot ??= Path.Combine(Path.GetTempPath(), "carves-matrix-core-verify-" + Guid.NewGuid().ToString("N"));
        var fixture = new MatrixBundleFixture(artifactRoot);
        fixture.WriteArtifact("project/decisions.jsonl", """{"decision":"allow"}""");
        fixture.WriteArtifact("project/handoff.json", """{"schema_version":"carves-continuity-handoff.v1"}""");
        fixture.WriteArtifact("project/shield-evidence.json", """{"schema_version":"shield-evidence.v0"}""");
        var evidenceSha256 = MatrixArtifactManifestWriter.ComputeFileSha256(Path.Combine(artifactRoot, "project", "shield-evidence.json"));
        fixture.WriteArtifact("project/shield-evaluate.json", shieldEvaluationJson ?? ValidShieldEvaluationJson(evidenceSha256));
        fixture.WriteArtifact("project/shield-badge.json", """{"schema_version":"shield-badge.v0"}""");
        fixture.WriteArtifact("project/shield-badge.svg", "<svg></svg>");
        fixture.WriteArtifact("project/matrix-summary.json", MatrixSummaryJson(artifactRoot));
        MatrixArtifactManifestWriter.WriteDefaultProofManifest(
            artifactRoot,
            DateTimeOffset.Parse("2026-04-15T00:00:00+00:00"));
        fixture.WriteProofSummary();
        return fixture;
    }

    public void Dispose()
    {
        if (Directory.Exists(ArtifactRoot))
        {
            Directory.Delete(ArtifactRoot, recursive: true);
        }
    }

    public static string ValidShieldEvaluationJson(string consumedEvidenceSha256)
    {
        return $$"""
        {
          "schema_version": "shield-evaluate.v0",
          "status": "ok",
          "certification": false,
          "consumed_evidence_sha256": "{{consumedEvidenceSha256}}",
          "standard": {
            "label": "CARVES G1.H1.A1 /1d PASS"
          },
          "lite": {
            "score": 50,
            "band": "disciplined"
          }
        }
        """;
    }
}

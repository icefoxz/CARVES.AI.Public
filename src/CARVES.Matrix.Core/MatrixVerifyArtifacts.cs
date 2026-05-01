using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static MatrixVerifyRequiredArtifacts VerifyRequiredArtifacts(
        string manifestPath,
        List<MatrixVerifyIssue> issues)
    {
        var expected = MatrixArtifactManifestWriter.DefaultRequiredArtifacts;
        if (!File.Exists(manifestPath))
        {
            return new MatrixVerifyRequiredArtifacts(expected.Count, PresentCount: 0, MissingCount: expected.Count, MismatchCount: 0);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (!document.RootElement.TryGetProperty("artifacts", out var artifacts) || artifacts.ValueKind != JsonValueKind.Array)
            {
                return new MatrixVerifyRequiredArtifacts(expected.Count, PresentCount: 0, MissingCount: expected.Count, MismatchCount: 0);
            }

            var artifactEntries = artifacts.EnumerateArray().ToArray();
            var presentCount = 0;
            var missingCount = 0;
            var mismatchCount = 0;
            foreach (var requirement in expected)
            {
                var matches = artifactEntries
                    .Where(artifact => string.Equals(GetString(artifact, "artifact_kind"), requirement.ArtifactKind, StringComparison.Ordinal))
                    .ToArray();
                if (matches.Length == 0)
                {
                    missingCount++;
                    issues.Add(new MatrixVerifyIssue(
                        "required_artifact",
                        requirement.ArtifactKind,
                        requirement.Path,
                        "required_artifact_entry_missing",
                        requirement.Path,
                        null));
                    continue;
                }

                presentCount++;
                if (matches.Length > 1)
                {
                    mismatchCount++;
                    issues.Add(new MatrixVerifyIssue(
                        "required_artifact",
                        requirement.ArtifactKind,
                        requirement.Path,
                        "required_artifact_entry_duplicate",
                        "1",
                        matches.Length.ToString()));
                }

                var entry = matches[0];
                mismatchCount += VerifyRequiredField(
                    issues,
                    requirement.ArtifactKind,
                    requirement.Path,
                    "path",
                    requirement.Path,
                    GetString(entry, "path"),
                    "required_artifact_path_mismatch");
                mismatchCount += VerifyRequiredField(
                    issues,
                    requirement.ArtifactKind,
                    requirement.Path,
                    "schema_version",
                    requirement.SchemaVersion,
                    GetString(entry, "schema_version"),
                    "required_artifact_schema_mismatch");
                mismatchCount += VerifyRequiredField(
                    issues,
                    requirement.ArtifactKind,
                    requirement.Path,
                    "producer",
                    requirement.Producer,
                    GetString(entry, "producer"),
                    "required_artifact_producer_mismatch");
            }

            return new MatrixVerifyRequiredArtifacts(expected.Count, presentCount, missingCount, mismatchCount);
        }
        catch (JsonException)
        {
            return new MatrixVerifyRequiredArtifacts(expected.Count, PresentCount: 0, MissingCount: expected.Count, MismatchCount: 0);
        }
        catch (IOException)
        {
            return new MatrixVerifyRequiredArtifacts(expected.Count, PresentCount: 0, MissingCount: expected.Count, MismatchCount: 0);
        }
        catch (UnauthorizedAccessException)
        {
            return new MatrixVerifyRequiredArtifacts(expected.Count, PresentCount: 0, MissingCount: expected.Count, MismatchCount: 0);
        }
    }

    private static int VerifyRequiredField(
        List<MatrixVerifyIssue> issues,
        string artifactKind,
        string path,
        string fieldName,
        string expected,
        string? actual,
        string code)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return 0;
        }

        issues.Add(new MatrixVerifyIssue(
            "required_artifact",
            artifactKind,
            path,
            code,
            $"{fieldName}:{expected}",
            actual is null ? null : $"{fieldName}:{actual}"));
        return 1;
    }
}

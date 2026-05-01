using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private sealed record MatrixTrialManifestEntry(
        string ArtifactKind,
        string? Path,
        string? SchemaVersion,
        string? Producer,
        string? Sha256,
        long? Size);

    private static MatrixVerifyTrialArtifacts VerifyTrialArtifacts(
        string artifactRoot,
        MatrixArtifactManifestVerificationResult manifestVerification,
        List<MatrixVerifyIssue> issues,
        bool requireTrial)
    {
        var requirements = MatrixArtifactManifestWriter.TrialArtifacts;
        var looseFileCount = CountLooseTrialFiles(artifactRoot, requirements);
        var entries = ReadManifestEntries(artifactRoot);
        if (entries is null)
        {
            return requireTrial
                ? AddRequiredTrialMissingIssues(requirements, issues, looseFileCount)
                : new MatrixVerifyTrialArtifacts(
                    looseFileCount > 0 ? "loose_files_not_manifested" : "not_present",
                    Required: false,
                    0,
                    0,
                    0,
                    0,
                    looseFileCount,
                    Verified: looseFileCount == 0);
        }

        var trialModeClaimed = entries.Any(entry => IsTrialEntry(entry, requirements));
        if (!trialModeClaimed)
        {
            return requireTrial
                ? AddRequiredTrialMissingIssues(requirements, issues, looseFileCount)
                : new MatrixVerifyTrialArtifacts(
                    looseFileCount > 0 ? "loose_files_not_manifested" : "not_present",
                    Required: false,
                    0,
                    0,
                    0,
                    0,
                    looseFileCount,
                    Verified: looseFileCount == 0);
        }

        var present = 0;
        var missing = 0;
        var mismatch = 0;
        var entriesByKind = entries
            .GroupBy(entry => entry.ArtifactKind, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var rootsByKind = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        foreach (var requirement in requirements)
        {
            entriesByKind.TryGetValue(requirement.ArtifactKind, out var matches);
            matches ??= [];
            if (matches.Length == 0)
            {
                missing++;
                issues.Add(new MatrixVerifyIssue(
                    "trial_artifact",
                    requirement.ArtifactKind,
                    requirement.Path,
                    "trial_artifact_entry_missing",
                    requirement.Path,
                    null));
                continue;
            }

            present++;
            if (matches.Length > 1)
            {
                mismatch++;
                issues.Add(new MatrixVerifyIssue(
                    "trial_artifact",
                    requirement.ArtifactKind,
                    requirement.Path,
                    "trial_artifact_entry_duplicate",
                    "1",
                    matches.Length.ToString()));
                continue;
            }

            var before = issues.Count;
            if (TryReadVerifiedTrialArtifact(
                    artifactRoot,
                    issues,
                    requirement,
                    manifestVerification,
                    out var document))
            {
                using (document)
                {
                    rootsByKind[requirement.ArtifactKind] = document.RootElement.Clone();
                    ValidateTrialArtifactContent(
                        issues,
                        requirement,
                        document.RootElement,
                        entriesByKind);
                }
            }

            if (issues.Count > before)
            {
                mismatch++;
            }
        }

        var beforeConsistency = issues.Count;
        ValidateTrialArtifactConsistency(issues, rootsByKind);
        if (issues.Count > beforeConsistency)
        {
            mismatch++;
        }

        return new MatrixVerifyTrialArtifacts(
            "claimed",
            requireTrial,
            requirements.Count,
            present,
            missing,
            mismatch,
            looseFileCount,
            missing == 0 && mismatch == 0);
    }

    private static MatrixVerifyTrialArtifacts AddRequiredTrialMissingIssues(
        IReadOnlyList<MatrixArtifactManifestRequirement> requirements,
        List<MatrixVerifyIssue> issues,
        int looseFileCount)
    {
        foreach (var requirement in requirements)
        {
            issues.Add(new MatrixVerifyIssue(
                "trial_artifact",
                requirement.ArtifactKind,
                requirement.Path,
                "trial_artifact_entry_missing",
                requirement.Path,
                null));
        }

        return new MatrixVerifyTrialArtifacts(
            looseFileCount > 0 ? "loose_files_not_manifested" : "required_missing",
            Required: true,
            requirements.Count,
            0,
            requirements.Count,
            requirements.Count,
            looseFileCount,
            Verified: false);
    }

    private static int CountLooseTrialFiles(
        string artifactRoot,
        IReadOnlyList<MatrixArtifactManifestRequirement> requirements)
    {
        return requirements.Count(requirement =>
            File.Exists(Path.Combine(artifactRoot, requirement.Path.Replace('/', Path.DirectorySeparatorChar))));
    }

    private static MatrixTrialManifestEntry[]? ReadManifestEntries(string artifactRoot)
    {
        var manifestPath = Path.Combine(artifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (!document.RootElement.TryGetProperty("artifacts", out var artifacts)
                || artifacts.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return artifacts
                .EnumerateArray()
                .Select(entry => new MatrixTrialManifestEntry(
                    GetString(entry, "artifact_kind") ?? string.Empty,
                    GetString(entry, "path"),
                    GetString(entry, "schema_version"),
                    GetString(entry, "producer"),
                    GetString(entry, "sha256"),
                    GetLong(entry, "size")))
                .ToArray();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool TryReadVerifiedTrialArtifact(
        string artifactRoot,
        List<MatrixVerifyIssue> issues,
        MatrixArtifactManifestRequirement requirement,
        MatrixArtifactManifestVerificationResult manifestVerification,
        out JsonDocument document)
    {
        var target = new MatrixVerifiedArtifactReadTarget(
            "trial_artifact",
            requirement.ArtifactKind,
            requirement.Path,
            "trial_artifact",
            "trial_artifact_source_missing",
            "trial_artifact_source_invalid_json",
            "trial_artifact_source_read_failed");

        return TryReadVerifiedArtifactDocument(
            artifactRoot,
            issues,
            target,
            requirement,
            manifestVerification,
            out document,
            out _);
    }

    private static bool IsTrialEntry(
        MatrixTrialManifestEntry entry,
        IReadOnlyList<MatrixArtifactManifestRequirement> requirements)
    {
        return requirements.Any(requirement => string.Equals(requirement.ArtifactKind, entry.ArtifactKind, StringComparison.Ordinal))
               || !string.IsNullOrWhiteSpace(entry.Path)
               && entry.Path.StartsWith("trial/", StringComparison.Ordinal);
    }
}

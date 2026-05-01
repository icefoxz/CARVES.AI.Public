using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static string CreateNativeFullReleaseStagingRoot(string artifactRoot)
    {
        return CreateNativeFullReleaseSiblingRoot(artifactRoot, "staging");
    }

    private static string PreserveNativeFullReleaseFailure(
        string artifactRoot,
        string stagingRoot,
        MatrixNativeFullReleaseFailure failure)
    {
        Directory.CreateDirectory(stagingRoot);
        var evidence = new
        {
            schema_version = "matrix-native-full-release-attempt.v0",
            status = "failed",
            proof_mode = MatrixProofSummaryPublicContract.FullReleaseProofMode,
            producer = "native_full_release",
            artifact_root = ToPublicArtifactRootMarker(),
            staging_posture = "isolated_failed_attempt",
            failed_step = failure.FailedStepId,
            message = failure.Message,
            reason_codes = DistinctNativeReasonCodes(failure.ReasonCodes),
        };
        var stagingEvidencePath = Path.Combine(stagingRoot, "native-full-release-failure.json");
        File.WriteAllText(stagingEvidencePath, JsonSerializer.Serialize(evidence, JsonOptions));

        var failureRoot = CreateNativeFullReleaseSiblingRoot(artifactRoot, "failed");
        Directory.Move(stagingRoot, failureRoot);
        return Path.Combine(failureRoot, "native-full-release-failure.json");
    }

    private static void PromoteNativeFullReleaseStagingRoot(string stagingRoot, string artifactRoot)
    {
        var fullArtifactRoot = Path.GetFullPath(artifactRoot);
        var parent = Path.GetDirectoryName(fullArtifactRoot);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }

        var backupRoot = CreateNativeFullReleaseSiblingRoot(fullArtifactRoot, "backup");
        var movedExistingDirectory = false;
        try
        {
            if (Directory.Exists(fullArtifactRoot))
            {
                Directory.Move(fullArtifactRoot, backupRoot);
                movedExistingDirectory = true;
            }
            else if (File.Exists(fullArtifactRoot))
            {
                File.Move(fullArtifactRoot, backupRoot);
            }

            Directory.Move(stagingRoot, fullArtifactRoot);

            if (movedExistingDirectory)
            {
                TryDeleteDirectory(backupRoot);
            }
            else if (File.Exists(backupRoot))
            {
                File.Delete(backupRoot);
            }
        }
        catch
        {
            RestoreNativeFullReleasePromotionBackup(fullArtifactRoot, backupRoot, movedExistingDirectory);
            throw;
        }
    }

    private static void RestoreNativeFullReleasePromotionBackup(
        string artifactRoot,
        string backupRoot,
        bool movedExistingDirectory)
    {
        if (Directory.Exists(artifactRoot) || File.Exists(artifactRoot))
        {
            return;
        }

        if (movedExistingDirectory && Directory.Exists(backupRoot))
        {
            Directory.Move(backupRoot, artifactRoot);
        }
        else if (!movedExistingDirectory && File.Exists(backupRoot))
        {
            File.Move(backupRoot, artifactRoot);
        }
    }

    private static string CreateNativeFullReleaseSiblingRoot(string artifactRoot, string purpose)
    {
        var fullArtifactRoot = Path.GetFullPath(artifactRoot);
        var parent = Path.GetDirectoryName(fullArtifactRoot) ?? Directory.GetCurrentDirectory();
        var name = Path.GetFileName(fullArtifactRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "matrix";
        }

        Directory.CreateDirectory(parent);
        return Path.Combine(parent, $".{name}.native-full-release-{purpose}-{Guid.NewGuid():N}");
    }
}

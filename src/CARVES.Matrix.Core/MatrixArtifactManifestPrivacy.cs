using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixArtifactManifestWriter
{
    private static readonly string[] ForbiddenPrivacyFlags =
    [
        "source_included",
        "raw_diff_included",
        "prompt_included",
        "model_response_included",
        "secrets_included",
        "credentials_included",
        "private_payload_included",
        "customer_payload_included",
        "hosted_upload_required",
        "certification_claim",
        "public_leaderboard_claim",
    ];

    private static void ValidatePrivacyFlags(
        List<MatrixArtifactVerificationIssue> issues,
        string artifactKind,
        string path,
        JsonElement container,
        string propertyName)
    {
        if (!container.TryGetProperty(propertyName, out var privacy) || privacy.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new MatrixArtifactVerificationIssue(artifactKind, path, "privacy_flags_missing", null, null, null, null));
            return;
        }

        if (ReadBool(privacy, "summary_only") != true)
        {
            issues.Add(new MatrixArtifactVerificationIssue(artifactKind, path, "privacy_summary_only_not_true", null, null, null, null));
        }

        foreach (var flag in ForbiddenPrivacyFlags)
        {
            var value = ReadBool(privacy, flag);
            if (!value.HasValue)
            {
                issues.Add(new MatrixArtifactVerificationIssue(artifactKind, path, $"privacy_flag_missing:{flag}", null, null, null, null));
                continue;
            }

            if (value.Value)
            {
                issues.Add(new MatrixArtifactVerificationIssue(artifactKind, path, $"privacy_forbidden_flag_true:{flag}", null, null, null, null));
            }
        }
    }
}

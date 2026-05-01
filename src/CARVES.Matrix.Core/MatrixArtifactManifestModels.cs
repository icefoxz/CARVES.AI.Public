namespace Carves.Matrix.Core;

public sealed record MatrixArtifactManifestEntryInput(
    string ArtifactKind,
    string Path,
    string SchemaVersion,
    string Producer,
    bool Required);

public sealed record MatrixArtifactManifestRequirement(
    string ArtifactKind,
    string Path,
    string SchemaVersion,
    string Producer);

public sealed record MatrixArtifactManifest(
    string SchemaVersion,
    DateTimeOffset CreatedAt,
    MatrixArtifactManifestProducer Producer,
    string ArtifactRoot,
    string ProducerArtifactRoot,
    MatrixArtifactPrivacyFlags Privacy,
    IReadOnlyList<MatrixArtifactManifestEntry> Artifacts);

public sealed record MatrixArtifactManifestProducer(
    string Tool,
    string Component,
    string Mode);

public sealed record MatrixArtifactManifestEntry(
    string ArtifactKind,
    string Path,
    string Sha256,
    long Size,
    string SchemaVersion,
    string Producer,
    DateTimeOffset CreatedAt,
    MatrixArtifactPrivacyFlags PrivacyFlags);

public sealed record MatrixArtifactManifestVerificationResult(
    string VerificationPosture,
    IReadOnlyList<MatrixArtifactVerificationIssue> Issues)
{
    public bool IsVerified => string.Equals(VerificationPosture, "verified", StringComparison.Ordinal);
}

public sealed record MatrixArtifactVerificationIssue(
    string ArtifactKind,
    string Path,
    string Code,
    string? ExpectedSha256,
    string? ActualSha256,
    long? ExpectedSize,
    long? ActualSize);

public sealed record MatrixArtifactPrivacyFlags(
    bool SummaryOnly,
    bool SourceIncluded,
    bool RawDiffIncluded,
    bool PromptIncluded,
    bool ModelResponseIncluded,
    bool SecretsIncluded,
    bool CredentialsIncluded,
    bool PrivatePayloadIncluded,
    bool CustomerPayloadIncluded,
    bool HostedUploadRequired,
    bool CertificationClaim,
    bool PublicLeaderboardClaim)
{
    public static MatrixArtifactPrivacyFlags CreateSummaryOnly()
    {
        return new MatrixArtifactPrivacyFlags(
            SummaryOnly: true,
            SourceIncluded: false,
            RawDiffIncluded: false,
            PromptIncluded: false,
            ModelResponseIncluded: false,
            SecretsIncluded: false,
            CredentialsIncluded: false,
            PrivatePayloadIncluded: false,
            CustomerPayloadIncluded: false,
            HostedUploadRequired: false,
            CertificationClaim: false,
            PublicLeaderboardClaim: false);
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Persistence;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimePackV1ManifestAdmissionBridgeService
{
    private const string DefaultChannel = "stable";
    private const string DefaultPackType = "runtime_pack";
    private const string DefaultPolicyPreset = "core-default";
    private const string DefaultGatePreset = "strict";
    private const string DefaultValidatorProfile = "default-validator";
    private const string DefaultEnvironmentProfile = "workspace";
    private const string DefaultRoutingProfile = "connected-lanes";
    private const string DefaultSourcePackLine = "runtime-pack-v1-manifest-bridge";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly SpecificationValidationService specificationValidationService;
    private readonly RuntimePackAdmissionService runtimePackAdmissionService;

    public RuntimePackV1ManifestAdmissionBridgeService(
        string repoRoot,
        ControlPlanePaths paths,
        IControlPlaneConfigRepository configRepository,
        IRuntimeArtifactRepository artifactRepository,
        SpecificationValidationService specificationValidationService)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.paths = paths;
        this.specificationValidationService = specificationValidationService;
        runtimePackAdmissionService = new RuntimePackAdmissionService(repoRoot, configRepository, artifactRepository, specificationValidationService);
    }

    public RuntimePackV1ManifestAdmissionBridgeResult Admit(
        string manifestPath,
        string? channel = null,
        string? publishedBy = null,
        string? sourcePackLine = null)
    {
        var manifestValidation = specificationValidationService.ValidateRuntimePackV1(manifestPath);
        if (!manifestValidation.IsValid)
        {
            var failureCodes = manifestValidation.Issues
                .Where(issue => string.Equals(issue.Severity, "error", StringComparison.OrdinalIgnoreCase))
                .Select(issue => issue.Code)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return RuntimePackV1ManifestAdmissionBridgeResult.Rejected(
                "Pack v1 manifest admission bridge rejected because the declarative manifest did not pass Pack v1 validation.",
                manifestValidation,
                null,
                null,
                failureCodes);
        }

        var resolvedManifestPath = Path.GetFullPath(manifestPath);
        RuntimePackV1ManifestDocument? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<RuntimePackV1ManifestDocument>(File.ReadAllText(resolvedManifestPath), JsonOptions);
        }
        catch (JsonException)
        {
            return RuntimePackV1ManifestAdmissionBridgeResult.Rejected(
                "Pack v1 manifest admission bridge rejected because the validated manifest could not be deserialized for bounded Runtime admission.",
                manifestValidation,
                null,
                null,
                ["runtime_pack_v1_manifest_bridge_payload_unreadable"]);
        }

        if (manifest is null)
        {
            return RuntimePackV1ManifestAdmissionBridgeResult.Rejected(
                "Pack v1 manifest admission bridge rejected because the validated manifest resolved to an empty document.",
                manifestValidation,
                null,
                null,
                ["runtime_pack_v1_manifest_bridge_payload_missing"]);
        }

        if (!TryBuildRuntimeCompatibility(manifest.Compatibility.CarvesRuntime, out var runtimeCompatibility, out var compatibilityFailureCode))
        {
            return RuntimePackV1ManifestAdmissionBridgeResult.Rejected(
                "Pack v1 manifest admission bridge rejected because the Runtime compatibility expression is outside the bounded bridge subset.",
                manifestValidation,
                null,
                null,
                [compatibilityFailureCode]);
        }

        var effectiveChannel = string.IsNullOrWhiteSpace(channel)
            ? DefaultChannel
            : channel.Trim();
        var bridgePublishedBy = string.IsNullOrWhiteSpace(publishedBy)
            ? $"runtime-pack-v1-bridge:{manifest.Publisher.Name}"
            : publishedBy.Trim();
        var effectiveSourcePackLine = string.IsNullOrWhiteSpace(sourcePackLine)
            ? DefaultSourcePackLine
            : sourcePackLine.Trim();

        var generatedAt = DateTimeOffset.UtcNow;
        var manifestRepoRelativePath = ToRepoRelativeOrAbsolute(resolvedManifestPath);
        var fileStem = BuildFileStem(manifest.PackId, manifest.PackVersion, effectiveChannel);
        var generatedRoot = Path.Combine(paths.ArtifactsRoot, "packs");
        var generatedPackArtifactPath = Path.Combine(generatedRoot, $"{fileStem}.json");
        var generatedAttributionPath = Path.Combine(generatedRoot, $"{fileStem}.attribution.json");
        var generatedPackArtifactRef = ToRepoRelativeOrAbsolute(generatedPackArtifactPath);
        var generatedManifestDigest = ComputeDigest(File.ReadAllText(resolvedManifestPath));
        var generatedSourceGenerationId = $"packv1bridge-{Guid.NewGuid():N}";

        var packArtifact = new GeneratedPackArtifactDocument
        {
            SchemaVersion = "1.0",
            PackId = manifest.PackId,
            PackVersion = manifest.PackVersion,
            PackType = DefaultPackType,
            Channel = effectiveChannel,
            RuntimeCompatibility = runtimeCompatibility,
            KernelCompatibility = new GeneratedCompatibilityRangeDocument
            {
                MinVersion = "0.1.0",
                MaxVersion = null,
            },
            ExecutionProfiles = BuildDefaultExecutionProfiles(),
            OperatorChecklistRefs = BuildOperatorChecklistRefs(manifest),
            Signature = new GeneratedSignatureDocument
            {
                Scheme = "runtime-pack-v1-bridge-sha256",
                KeyId = "runtime-pack-v1-bridge",
                Digest = generatedManifestDigest,
            },
            Provenance = new GeneratedProvenanceDocument
            {
                PublishedAtUtc = generatedAt,
                PublishedBy = bridgePublishedBy,
                ReleaseNoteRef = null,
                SourcePackLine = effectiveSourcePackLine,
                SourceGenerationId = generatedSourceGenerationId,
                ParentPackVersion = null,
                Supersedes = Array.Empty<string>(),
                ApprovalRef = null,
            },
        };

        var attribution = new GeneratedRuntimePackAttributionDocument
        {
            SchemaVersion = "1.0",
            PackId = manifest.PackId,
            PackVersion = manifest.PackVersion,
            Channel = effectiveChannel,
            ArtifactRef = generatedPackArtifactRef,
            ExecutionProfiles = BuildDefaultAttributionExecutionProfiles(),
            Source = new GeneratedAttributionSourceDocument
            {
                AssignmentMode = "overlay_assignment",
                AssignmentRef = manifestRepoRelativePath,
            },
            AttributedAtUtc = generatedAt,
        };

        Directory.CreateDirectory(generatedRoot);
        File.WriteAllText(generatedPackArtifactPath, JsonSerializer.Serialize(packArtifact, JsonOptions));
        File.WriteAllText(generatedAttributionPath, JsonSerializer.Serialize(attribution, JsonOptions));

        try
        {
            var admissionResult = runtimePackAdmissionService.Admit(generatedPackArtifactPath, generatedAttributionPath);
            if (!admissionResult.Admitted)
            {
                TryDeleteGeneratedPair(generatedPackArtifactPath, generatedAttributionPath);
                return RuntimePackV1ManifestAdmissionBridgeResult.Rejected(
                    "Pack v1 manifest admission bridge rejected because the generated Runtime pack artifact pair did not satisfy bounded Runtime admission.",
                    manifestValidation,
                    generatedPackArtifactRef,
                    ToRepoRelativeOrAbsolute(generatedAttributionPath),
                    admissionResult.FailureCodes);
            }

            return RuntimePackV1ManifestAdmissionBridgeResult.Accepted(
                manifestValidation,
                admissionResult,
                manifestRepoRelativePath,
                generatedPackArtifactRef,
                ToRepoRelativeOrAbsolute(generatedAttributionPath));
        }
        catch
        {
            TryDeleteGeneratedPair(generatedPackArtifactPath, generatedAttributionPath);
            throw;
        }
    }

    private static GeneratedExecutionProfilesDocument BuildDefaultExecutionProfiles()
    {
        return new GeneratedExecutionProfilesDocument
        {
            PolicyPreset = DefaultPolicyPreset,
            GatePreset = DefaultGatePreset,
            ValidatorProfile = DefaultValidatorProfile,
            EnvironmentProfile = DefaultEnvironmentProfile,
            RoutingProfile = DefaultRoutingProfile,
        };
    }

    private static GeneratedAttributionExecutionProfilesDocument BuildDefaultAttributionExecutionProfiles()
    {
        return new GeneratedAttributionExecutionProfilesDocument
        {
            PolicyPreset = DefaultPolicyPreset,
            GatePreset = DefaultGatePreset,
            ValidatorProfile = DefaultValidatorProfile,
            EnvironmentProfile = DefaultEnvironmentProfile,
            RoutingProfile = DefaultRoutingProfile,
        };
    }

    private static string[] BuildOperatorChecklistRefs(RuntimePackV1ManifestDocument manifest)
    {
        var refs = new List<string>
        {
            "docs/product/runtime-pack-v1-product-spec.md",
            "docs/product/runtime-pack-v1-engineering-acceptance-v1.md",
        };

        if (manifest.CapabilityKinds.Contains("verification_recipe", StringComparer.Ordinal))
        {
            refs.Add("docs/product/runtime-pack-command-admission-v1.md");
        }

        if (manifest.CapabilityKinds.Contains("review_rubric", StringComparer.Ordinal))
        {
            refs.Add("docs/product/runtime-pack-conflict-resolution-v1.md");
        }

        return refs.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string BuildFileStem(string packId, string packVersion, string channel)
    {
        var stem = $"{packId}-{packVersion}-{channel}-manifest-bridge"
            .Replace('.', '-')
            .Replace('_', '-');
        return stem;
    }

    private static bool TryBuildRuntimeCompatibility(
        string expression,
        out GeneratedCompatibilityRangeDocument compatibility,
        out string failureCode)
    {
        var trimmed = expression.Trim();
        compatibility = new GeneratedCompatibilityRangeDocument();
        failureCode = "runtime_pack_v1_runtime_compatibility_expression_unsupported";

        if (trimmed.StartsWith(">=", StringComparison.Ordinal))
        {
            var minVersion = trimmed[2..].Trim();
            if (Version.TryParse(NormalizeVersion(minVersion), out _))
            {
                compatibility = new GeneratedCompatibilityRangeDocument
                {
                    MinVersion = NormalizeVersion(minVersion),
                    MaxVersion = null,
                };
                return true;
            }

            return false;
        }

        if (trimmed.Contains('x', StringComparison.OrdinalIgnoreCase))
        {
            var prefixSegments = trimmed
                .Split('.', StringSplitOptions.RemoveEmptyEntries)
                .TakeWhile(segment => !string.Equals(segment, "x", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (prefixSegments.Length == 0)
            {
                return false;
            }

            var minSegments = prefixSegments.ToList();
            while (minSegments.Count < 3)
            {
                minSegments.Add("0");
            }

            var minimum = string.Join('.', minSegments.Take(3));
            if (!Version.TryParse(minimum, out _))
            {
                return false;
            }

            compatibility = new GeneratedCompatibilityRangeDocument
            {
                MinVersion = minimum,
                MaxVersion = trimmed,
            };
            return true;
        }

        if (Version.TryParse(NormalizeVersion(trimmed), out _))
        {
            compatibility = new GeneratedCompatibilityRangeDocument
            {
                MinVersion = NormalizeVersion(trimmed),
                MaxVersion = NormalizeVersion(trimmed),
            };
            return true;
        }

        return false;
    }

    private static string NormalizeVersion(string version)
    {
        var segments = version.Split('.', StringSplitOptions.RemoveEmptyEntries).ToList();
        while (segments.Count < 3)
        {
            segments.Add("0");
        }

        return string.Join('.', segments.Take(3));
    }

    private static string ComputeDigest(string payload)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return $"sha256:{Convert.ToHexStringLower(hash)}";
    }

    private static void TryDeleteGeneratedPair(string artifactPath, string attributionPath)
    {
        try
        {
            if (File.Exists(artifactPath))
            {
                File.Delete(artifactPath);
            }

            if (File.Exists(attributionPath))
            {
                File.Delete(attributionPath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private string ToRepoRelativeOrAbsolute(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(repoRoot, fullPath);
        if (relative.StartsWith("..", StringComparison.Ordinal))
        {
            return fullPath.Replace('\\', '/');
        }

        return relative.Replace('\\', '/');
    }

    private sealed class RuntimePackV1ManifestDocument
    {
        public string PackId { get; init; } = string.Empty;

        public string PackVersion { get; init; } = string.Empty;

        public PublisherDocument Publisher { get; init; } = new();

        public CompatibilityDocument Compatibility { get; init; } = new();

        public string[] CapabilityKinds { get; init; } = [];
    }

    private sealed class PublisherDocument
    {
        public string Name { get; init; } = string.Empty;
    }

    private sealed class CompatibilityDocument
    {
        public string CarvesRuntime { get; init; } = string.Empty;
    }

    private sealed class GeneratedPackArtifactDocument
    {
        public string SchemaVersion { get; init; } = "1.0";

        public string PackId { get; init; } = string.Empty;

        public string PackVersion { get; init; } = string.Empty;

        public string PackType { get; init; } = string.Empty;

        public string Channel { get; init; } = string.Empty;

        public GeneratedCompatibilityRangeDocument RuntimeCompatibility { get; init; } = new();

        public GeneratedCompatibilityRangeDocument KernelCompatibility { get; init; } = new();

        public GeneratedExecutionProfilesDocument ExecutionProfiles { get; init; } = new();

        public string[] OperatorChecklistRefs { get; init; } = [];

        public GeneratedSignatureDocument Signature { get; init; } = new();

        public GeneratedProvenanceDocument Provenance { get; init; } = new();
    }

    private sealed class GeneratedCompatibilityRangeDocument
    {
        public string MinVersion { get; init; } = string.Empty;

        public string? MaxVersion { get; init; }
    }

    private sealed class GeneratedExecutionProfilesDocument
    {
        public string PolicyPreset { get; init; } = string.Empty;

        public string GatePreset { get; init; } = string.Empty;

        public string ValidatorProfile { get; init; } = string.Empty;

        public string EnvironmentProfile { get; init; } = string.Empty;

        public string RoutingProfile { get; init; } = string.Empty;
    }

    private sealed class GeneratedSignatureDocument
    {
        public string Scheme { get; init; } = string.Empty;

        public string KeyId { get; init; } = string.Empty;

        public string Digest { get; init; } = string.Empty;
    }

    private sealed class GeneratedProvenanceDocument
    {
        public DateTimeOffset PublishedAtUtc { get; init; }

        public string PublishedBy { get; init; } = string.Empty;

        public string? ReleaseNoteRef { get; init; }

        public string SourcePackLine { get; init; } = string.Empty;

        public string SourceGenerationId { get; init; } = string.Empty;

        public string? ParentPackVersion { get; init; }

        public string[] Supersedes { get; init; } = [];

        public string? ApprovalRef { get; init; }
    }

    private sealed class GeneratedRuntimePackAttributionDocument
    {
        public string SchemaVersion { get; init; } = "1.0";

        public string PackId { get; init; } = string.Empty;

        public string PackVersion { get; init; } = string.Empty;

        public string Channel { get; init; } = string.Empty;

        public string ArtifactRef { get; init; } = string.Empty;

        public GeneratedAttributionExecutionProfilesDocument ExecutionProfiles { get; init; } = new();

        public GeneratedAttributionSourceDocument Source { get; init; } = new();

        public DateTimeOffset AttributedAtUtc { get; init; }
    }

    private sealed class GeneratedAttributionExecutionProfilesDocument
    {
        public string PolicyPreset { get; init; } = string.Empty;

        public string GatePreset { get; init; } = string.Empty;

        public string ValidatorProfile { get; init; } = string.Empty;

        public string EnvironmentProfile { get; init; } = string.Empty;

        public string RoutingProfile { get; init; } = string.Empty;
    }

    private sealed class GeneratedAttributionSourceDocument
    {
        public string AssignmentMode { get; init; } = string.Empty;

        public string AssignmentRef { get; init; } = string.Empty;
    }
}

public sealed class RuntimePackV1ManifestAdmissionBridgeResult
{
    private RuntimePackV1ManifestAdmissionBridgeResult(
        bool admitted,
        string summary,
        SpecificationValidationResult manifestValidation,
        RuntimePackAdmissionResult? runtimeAdmission,
        string? manifestPath,
        string? generatedPackArtifactPath,
        string? generatedAttributionPath,
        IReadOnlyList<string> failureCodes)
    {
        Admitted = admitted;
        Summary = summary;
        ManifestValidation = manifestValidation;
        RuntimeAdmission = runtimeAdmission;
        ManifestPath = manifestPath;
        GeneratedPackArtifactPath = generatedPackArtifactPath;
        GeneratedAttributionPath = generatedAttributionPath;
        FailureCodes = failureCodes;
    }

    public bool Admitted { get; }

    public string Summary { get; }

    public SpecificationValidationResult ManifestValidation { get; }

    public RuntimePackAdmissionResult? RuntimeAdmission { get; }

    public string? ManifestPath { get; }

    public string? GeneratedPackArtifactPath { get; }

    public string? GeneratedAttributionPath { get; }

    public IReadOnlyList<string> FailureCodes { get; }

    public static RuntimePackV1ManifestAdmissionBridgeResult Accepted(
        SpecificationValidationResult manifestValidation,
        RuntimePackAdmissionResult runtimeAdmission,
        string manifestPath,
        string generatedPackArtifactPath,
        string generatedAttributionPath)
    {
        return new RuntimePackV1ManifestAdmissionBridgeResult(
            true,
            runtimeAdmission.Summary,
            manifestValidation,
            runtimeAdmission,
            manifestPath,
            generatedPackArtifactPath,
            generatedAttributionPath,
            Array.Empty<string>());
    }

    public static RuntimePackV1ManifestAdmissionBridgeResult Rejected(
        string summary,
        SpecificationValidationResult manifestValidation,
        string? generatedPackArtifactPath,
        string? generatedAttributionPath,
        IReadOnlyList<string> failureCodes)
    {
        return new RuntimePackV1ManifestAdmissionBridgeResult(
            false,
            summary,
            manifestValidation,
            null,
            null,
            generatedPackArtifactPath,
            generatedAttributionPath,
            failureCodes.ToArray());
    }
}

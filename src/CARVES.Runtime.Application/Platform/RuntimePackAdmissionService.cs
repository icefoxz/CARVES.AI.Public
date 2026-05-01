using System.Text.Json;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Persistence;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimePackAdmissionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string repoRoot;
    private readonly IControlPlaneConfigRepository configRepository;
    private readonly IRuntimeArtifactRepository artifactRepository;
    private readonly SpecificationValidationService specificationValidationService;
    private readonly RuntimePackAdmissionPolicyService admissionPolicyService;

    public RuntimePackAdmissionService(
        string repoRoot,
        IControlPlaneConfigRepository configRepository,
        IRuntimeArtifactRepository artifactRepository,
        SpecificationValidationService specificationValidationService)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.configRepository = configRepository;
        this.artifactRepository = artifactRepository;
        this.specificationValidationService = specificationValidationService;
        admissionPolicyService = new RuntimePackAdmissionPolicyService(configRepository, artifactRepository);
    }

    public RuntimePackAdmissionResult Admit(string packArtifactPath, string runtimePackAttributionPath)
    {
        var packValidation = specificationValidationService.ValidatePackArtifact(packArtifactPath);
        var attributionValidation = specificationValidationService.ValidateRuntimePackAttribution(runtimePackAttributionPath);
        var failures = new List<string>();
        var validationFailures = new List<string>();
        if (!packValidation.IsValid)
        {
            validationFailures.AddRange(packValidation.Issues
                .Where(issue => string.Equals(issue.Severity, "error", StringComparison.OrdinalIgnoreCase))
                .Select(issue => issue.Code));
        }

        if (!attributionValidation.IsValid)
        {
            validationFailures.AddRange(attributionValidation.Issues
                .Where(issue => string.Equals(issue.Severity, "error", StringComparison.OrdinalIgnoreCase))
                .Select(issue => issue.Code));
        }

        PackArtifactDocument? pack;
        RuntimePackAttributionDocument? attribution;
        try
        {
            pack = JsonSerializer.Deserialize<PackArtifactDocument>(File.ReadAllText(packArtifactPath), JsonOptions);
            attribution = JsonSerializer.Deserialize<RuntimePackAttributionDocument>(File.ReadAllText(runtimePackAttributionPath), JsonOptions);
        }
        catch (JsonException)
        {
            return RuntimePackAdmissionResult.Rejected(
                "Runtime pack admission rejected because the validated JSON payload could not be deserialized for local admission.",
                packValidation,
                attributionValidation,
                [.. validationFailures, "runtime_pack_admission_payload_unreadable"]);
        }

        if (pack is null || attribution is null)
        {
            return RuntimePackAdmissionResult.Rejected(
                "Runtime pack admission rejected because the validated JSON payload resolved to an empty document.",
                packValidation,
                attributionValidation,
                [.. validationFailures, "runtime_pack_admission_payload_missing"]);
        }

        var checksPassed = new List<string>();
        var policyEvaluation = admissionPolicyService.Evaluate(
            pack.PackType,
            pack.Channel,
            HasSignature(pack.Signature),
            HasProvenance(pack.Provenance));
        failures.AddRange(validationFailures);
        failures.AddRange(policyEvaluation.FailureCodes);
        checksPassed.AddRange(policyEvaluation.ChecksPassed);
        if (failures.Count > 0)
        {
            return RuntimePackAdmissionResult.Rejected(
                "Runtime pack admission rejected before local truth write because contract validation and local admission policy checks did not fully pass.",
                packValidation,
                attributionValidation,
                failures.Distinct(StringComparer.Ordinal).ToArray());
        }

        CompareMatch("pack_id_mismatch", "pack id", pack.PackId, attribution.PackId, failures, checksPassed);
        CompareMatch("pack_version_mismatch", "pack version", pack.PackVersion, attribution.PackVersion, failures, checksPassed);
        CompareMatch("pack_channel_mismatch", "channel", pack.Channel, attribution.Channel, failures, checksPassed);
        CompareMatch("policy_preset_mismatch", "policy preset", pack.ExecutionProfiles.PolicyPreset, attribution.ExecutionProfiles.PolicyPreset, failures, checksPassed);
        CompareMatch("gate_preset_mismatch", "gate preset", pack.ExecutionProfiles.GatePreset, attribution.ExecutionProfiles.GatePreset, failures, checksPassed);
        CompareMatch("validator_profile_mismatch", "validator profile", pack.ExecutionProfiles.ValidatorProfile, attribution.ExecutionProfiles.ValidatorProfile, failures, checksPassed);
        CompareMatch("environment_profile_mismatch", "environment profile", pack.ExecutionProfiles.EnvironmentProfile, attribution.ExecutionProfiles.EnvironmentProfile, failures, checksPassed);
        CompareMatch("routing_profile_mismatch", "routing profile", pack.ExecutionProfiles.RoutingProfile, attribution.ExecutionProfiles.RoutingProfile, failures, checksPassed);

        var runtimeStandardVersion = NormalizeVersion(configRepository.LoadCarvesCodeStandard().Version);
        if (IsCompatibleWithRuntimeStandard(runtimeStandardVersion, pack.RuntimeCompatibility))
        {
            checksPassed.Add("runtime compatibility accepts local CARVES standard");
        }
        else
        {
            failures.Add("runtime_standard_incompatible");
        }

        if (failures.Count > 0)
        {
            return RuntimePackAdmissionResult.Rejected(
                "Runtime pack admission rejected because the pack artifact and attribution pair did not satisfy local compatibility and profile consistency checks.",
                packValidation,
                attributionValidation,
                failures);
        }

        var artifact = new RuntimePackAdmissionArtifact
        {
            PackId = pack.PackId,
            PackVersion = pack.PackVersion,
            Channel = pack.Channel,
            RuntimeStandardVersion = runtimeStandardVersion,
            PackArtifactPath = ToRepoRelativeOrAbsolute(packArtifactPath),
            RuntimePackAttributionPath = ToRepoRelativeOrAbsolute(runtimePackAttributionPath),
            ArtifactRef = attribution.ArtifactRef ?? string.Empty,
            ExecutionProfiles = new RuntimePackAdmissionProfileSelection
            {
                PolicyPreset = attribution.ExecutionProfiles.PolicyPreset,
                GatePreset = attribution.ExecutionProfiles.GatePreset,
                ValidatorProfile = attribution.ExecutionProfiles.ValidatorProfile,
                EnvironmentProfile = attribution.ExecutionProfiles.EnvironmentProfile,
                RoutingProfile = attribution.ExecutionProfiles.RoutingProfile,
            },
            Source = new RuntimePackAdmissionSource
            {
                AssignmentMode = attribution.Source.AssignmentMode,
                AssignmentRef = attribution.Source.AssignmentRef,
            },
            ChecksPassed = checksPassed.ToArray(),
            Summary = $"Runtime-local admission accepted {pack.PackId}@{pack.PackVersion} ({pack.Channel}) for CARVES standard {runtimeStandardVersion}.",
        };

        artifactRepository.SaveRuntimePackAdmissionArtifact(artifact);
        return RuntimePackAdmissionResult.Accepted(artifact, packValidation, attributionValidation);
    }

    public RuntimePackAdmissionSurface BuildSurface()
    {
        var runtimeStandardVersion = NormalizeVersion(configRepository.LoadCarvesCodeStandard().Version);
        var current = artifactRepository.TryLoadCurrentRuntimePackAdmissionArtifact();
        return new RuntimePackAdmissionSurface
        {
            RuntimeStandardVersion = runtimeStandardVersion,
            CurrentAdmission = current,
            Summary = current is null
                ? "No runtime-local pack admission has been recorded yet."
                : current.Summary,
            Notes =
            [
                "Runtime-local admission is bounded local evidence only.",
                "Active local admission policy remains inspectable through runtime-pack-admission-policy.",
                "Registry rollout, automatic activation, and publication remain out of scope.",
                "Admission requires both a valid pack artifact and a matching runtime attribution pair.",
            ],
        };
    }

    private static void CompareMatch(
        string failureCode,
        string description,
        string left,
        string right,
        ICollection<string> failures,
        ICollection<string> checksPassed)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            checksPassed.Add($"{description} matches");
            return;
        }

        failures.Add(failureCode);
    }

    private static bool HasSignature(SignatureDocument? signature)
    {
        return signature is not null
               && !string.IsNullOrWhiteSpace(signature.Scheme)
               && !string.IsNullOrWhiteSpace(signature.KeyId)
               && !string.IsNullOrWhiteSpace(signature.Digest);
    }

    private static bool HasProvenance(ProvenanceDocument? provenance)
    {
        return provenance is not null
               && !string.IsNullOrWhiteSpace(provenance.PublishedAtUtc)
               && !string.IsNullOrWhiteSpace(provenance.PublishedBy)
               && !string.IsNullOrWhiteSpace(provenance.SourcePackLine)
               && !string.IsNullOrWhiteSpace(provenance.SourceGenerationId);
    }

    private static bool IsCompatibleWithRuntimeStandard(string runtimeStandardVersion, CompatibilityRangeDocument runtimeCompatibility)
    {
        if (!Version.TryParse(runtimeStandardVersion, out var current))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(runtimeCompatibility.MinVersion))
        {
            if (!Version.TryParse(NormalizeVersion(runtimeCompatibility.MinVersion), out var minimum)
                || current < minimum)
            {
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(runtimeCompatibility.MaxVersion))
        {
            return true;
        }

        var maxVersion = runtimeCompatibility.MaxVersion.Trim();
        if (maxVersion.Contains('x', StringComparison.OrdinalIgnoreCase))
        {
            var prefix = maxVersion
                .Split('.', StringSplitOptions.RemoveEmptyEntries)
                .TakeWhile(segment => !string.Equals(segment, "x", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var currentSegments = runtimeStandardVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
            return currentSegments.Length >= prefix.Length
                   && prefix.Select((segment, index) => string.Equals(segment, currentSegments[index], StringComparison.Ordinal))
                       .All(matches => matches);
        }

        return Version.TryParse(NormalizeVersion(maxVersion), out var maximum) && current <= maximum;
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

    private sealed class PackArtifactDocument
    {
        public string PackId { get; init; } = string.Empty;

        public string PackVersion { get; init; } = string.Empty;

        public string PackType { get; init; } = string.Empty;

        public string Channel { get; init; } = string.Empty;

        public CompatibilityRangeDocument RuntimeCompatibility { get; init; } = new();

        public ExecutionProfilesDocument ExecutionProfiles { get; init; } = new();

        public SignatureDocument? Signature { get; init; }

        public ProvenanceDocument? Provenance { get; init; }
    }

    private sealed class RuntimePackAttributionDocument
    {
        public string PackId { get; init; } = string.Empty;

        public string PackVersion { get; init; } = string.Empty;

        public string Channel { get; init; } = string.Empty;

        public string? ArtifactRef { get; init; }

        public AttributionExecutionProfilesDocument ExecutionProfiles { get; init; } = new();

        public AttributionSourceDocument Source { get; init; } = new();
    }

    private sealed class CompatibilityRangeDocument
    {
        public string? MinVersion { get; init; }

        public string? MaxVersion { get; init; }
    }

    private sealed class ExecutionProfilesDocument
    {
        public string PolicyPreset { get; init; } = string.Empty;

        public string GatePreset { get; init; } = string.Empty;

        public string ValidatorProfile { get; init; } = string.Empty;

        public string EnvironmentProfile { get; init; } = string.Empty;

        public string RoutingProfile { get; init; } = string.Empty;
    }

    private sealed class AttributionExecutionProfilesDocument
    {
        public string PolicyPreset { get; init; } = string.Empty;

        public string GatePreset { get; init; } = string.Empty;

        public string ValidatorProfile { get; init; } = string.Empty;

        public string EnvironmentProfile { get; init; } = string.Empty;

        public string RoutingProfile { get; init; } = string.Empty;
    }

    private sealed class AttributionSourceDocument
    {
        public string AssignmentMode { get; init; } = string.Empty;

        public string? AssignmentRef { get; init; }
    }

    private sealed class SignatureDocument
    {
        public string? Scheme { get; init; }

        public string? KeyId { get; init; }

        public string? Digest { get; init; }
    }

    private sealed class ProvenanceDocument
    {
        public string? PublishedAtUtc { get; init; }

        public string? PublishedBy { get; init; }

        public string? SourcePackLine { get; init; }

        public string? SourceGenerationId { get; init; }
    }
}

public sealed class RuntimePackAdmissionResult
{
    private RuntimePackAdmissionResult(
        bool admitted,
        string summary,
        RuntimePackAdmissionArtifact? artifact,
        SpecificationValidationResult packValidation,
        SpecificationValidationResult attributionValidation,
        IReadOnlyList<string> failureCodes)
    {
        Admitted = admitted;
        Summary = summary;
        Artifact = artifact;
        PackValidation = packValidation;
        AttributionValidation = attributionValidation;
        FailureCodes = failureCodes;
    }

    public bool Admitted { get; }

    public string Summary { get; }

    public RuntimePackAdmissionArtifact? Artifact { get; }

    public SpecificationValidationResult PackValidation { get; }

    public SpecificationValidationResult AttributionValidation { get; }

    public IReadOnlyList<string> FailureCodes { get; }

    public static RuntimePackAdmissionResult Accepted(
        RuntimePackAdmissionArtifact artifact,
        SpecificationValidationResult packValidation,
        SpecificationValidationResult attributionValidation)
    {
        return new RuntimePackAdmissionResult(
            true,
            artifact.Summary,
            artifact,
            packValidation,
            attributionValidation,
            Array.Empty<string>());
    }

    public static RuntimePackAdmissionResult Rejected(
        string summary,
        SpecificationValidationResult packValidation,
        SpecificationValidationResult attributionValidation,
        IReadOnlyList<string> failureCodes)
    {
        return new RuntimePackAdmissionResult(
            false,
            summary,
            null,
            packValidation,
            attributionValidation,
            failureCodes.ToArray());
    }
}

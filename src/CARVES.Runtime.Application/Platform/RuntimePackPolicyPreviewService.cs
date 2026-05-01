using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Persistence;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimePackPolicyPreviewService
{
    private readonly string repoRoot;
    private readonly IRuntimeArtifactRepository artifactRepository;
    private readonly RuntimePackAdmissionPolicyService admissionPolicyService;
    private readonly RuntimePackSwitchPolicyService switchPolicyService;
    private readonly RuntimePackPolicyPackageValidationService validationService;

    public RuntimePackPolicyPreviewService(
        string repoRoot,
        IRuntimeArtifactRepository artifactRepository,
        IControlPlaneConfigRepository configRepository)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.artifactRepository = artifactRepository;
        admissionPolicyService = new RuntimePackAdmissionPolicyService(configRepository, artifactRepository);
        switchPolicyService = new RuntimePackSwitchPolicyService(artifactRepository);
        validationService = new RuntimePackPolicyPackageValidationService(configRepository);
    }

    public RuntimePackPolicyPreviewResult Preview(string inputPath)
    {
        var validation = validationService.Validate(inputPath);
        if (!validation.Succeeded || validation.Package is null || string.IsNullOrWhiteSpace(validation.Path))
        {
            return RuntimePackPolicyPreviewResult.Rejected(
                "Runtime-local pack policy preview rejected because the package failed bounded local validation.",
                validation.FailureCodes);
        }

        var currentAdmissionPolicy = admissionPolicyService.BuildCurrentPolicy();
        var currentSwitchPolicy = switchPolicyService.BuildCurrentPolicy();
        var differences = BuildDifferences(
            currentAdmissionPolicy,
            validation.Package.AdmissionPolicy,
            currentSwitchPolicy,
            validation.Package.SwitchPolicy);
        var artifact = new RuntimePackPolicyPreviewArtifact
        {
            InputPath = ToRepoRelativeOrAbsolute(validation.Path),
            PackageId = validation.Package.PackageId,
            RuntimeStandardVersion = validation.Package.RuntimeStandardVersion,
            CurrentAdmissionPolicy = currentAdmissionPolicy,
            IncomingAdmissionPolicy = validation.Package.AdmissionPolicy,
            CurrentSwitchPolicy = currentSwitchPolicy,
            IncomingSwitchPolicy = validation.Package.SwitchPolicy,
            Differences = differences,
            Summary = differences.Count == 0
                ? $"Incoming runtime-local pack policy package {validation.Package.PackageId} matches current local admission and switch policy truth."
                : $"Incoming runtime-local pack policy package {validation.Package.PackageId} differs from current local policy truth in {differences.Count} bounded area(s).",
            ChecksPassed =
            [
                .. validation.ChecksPassed,
                "preview remained local-runtime scoped",
                "preview did not mutate current local policy truth",
                "registry and rollout remained closed"
            ],
        };

        artifactRepository.SaveRuntimePackPolicyPreviewArtifact(artifact);
        return RuntimePackPolicyPreviewResult.CreateSuccess(artifact);
    }

    public RuntimePackPolicyPreviewSurface BuildSurface()
    {
        var currentPreview = artifactRepository.TryLoadCurrentRuntimePackPolicyPreviewArtifact();
        return new RuntimePackPolicyPreviewSurface
        {
            Summary = currentPreview is null
                ? "No runtime-local pack policy preview is currently recorded."
                : currentPreview.Summary,
            CurrentPreview = currentPreview,
            Notes =
            [
                "Preview stays local-runtime scoped and explicit.",
                "Preview compares an incoming local policy package against current admission and switch policy truth without mutating current truth.",
                "Registry, rollout, remote sync, and automatic apply remain closed."
            ],
        };
    }

    private static IReadOnlyList<RuntimePackPolicyPreviewDiffEntry> BuildDifferences(
        RuntimePackAdmissionPolicyArtifact currentAdmissionPolicy,
        RuntimePackAdmissionPolicyArtifact incomingAdmissionPolicy,
        RuntimePackSwitchPolicyArtifact currentSwitchPolicy,
        RuntimePackSwitchPolicyArtifact incomingSwitchPolicy)
    {
        var differences = new List<RuntimePackPolicyPreviewDiffEntry>();

        AddIfDifferent(
            differences,
            "admission_policy_id_changed",
            "Admission policy id changes.",
            currentAdmissionPolicy.PolicyId,
            incomingAdmissionPolicy.PolicyId);
        AddIfDifferent(
            differences,
            "admission_allowed_channels_changed",
            "Admission allowed channels change.",
            JoinValues(currentAdmissionPolicy.AllowedChannels),
            JoinValues(incomingAdmissionPolicy.AllowedChannels));
        AddIfDifferent(
            differences,
            "admission_allowed_pack_types_changed",
            "Admission allowed pack types change.",
            JoinValues(currentAdmissionPolicy.AllowedPackTypes),
            JoinValues(incomingAdmissionPolicy.AllowedPackTypes));
        AddIfDifferent(
            differences,
            "admission_signature_requirement_changed",
            "Admission signature requirement changes.",
            currentAdmissionPolicy.RequireSignature.ToString(),
            incomingAdmissionPolicy.RequireSignature.ToString());
        AddIfDifferent(
            differences,
            "admission_provenance_requirement_changed",
            "Admission provenance requirement changes.",
            currentAdmissionPolicy.RequireProvenance.ToString(),
            incomingAdmissionPolicy.RequireProvenance.ToString());
        AddIfDifferent(
            differences,
            "switch_policy_id_changed",
            "Switch policy id changes.",
            currentSwitchPolicy.PolicyId,
            incomingSwitchPolicy.PolicyId);
        AddIfDifferent(
            differences,
            "switch_policy_mode_changed",
            "Switch policy mode changes.",
            currentSwitchPolicy.PolicyMode,
            incomingSwitchPolicy.PolicyMode);
        AddIfDifferent(
            differences,
            "switch_pin_state_changed",
            "Switch pin state changes.",
            currentSwitchPolicy.PinActive.ToString(),
            incomingSwitchPolicy.PinActive.ToString());
        AddIfDifferent(
            differences,
            "switch_target_changed",
            "Switch target changes.",
            FormatSwitchTarget(currentSwitchPolicy),
            FormatSwitchTarget(incomingSwitchPolicy));

        return differences;
    }

    private static void AddIfDifferent(
        ICollection<RuntimePackPolicyPreviewDiffEntry> differences,
        string diffCode,
        string summary,
        string? currentValue,
        string? incomingValue)
    {
        if (string.Equals(currentValue, incomingValue, StringComparison.Ordinal))
        {
            return;
        }

        differences.Add(new RuntimePackPolicyPreviewDiffEntry
        {
            DiffCode = diffCode,
            Summary = summary,
            CurrentValue = currentValue,
            IncomingValue = incomingValue,
        });
    }

    private static string JoinValues(IReadOnlyList<string> values)
    {
        return values.Count == 0
            ? "(none)"
            : string.Join(", ", values.OrderBy(item => item, StringComparer.Ordinal));
    }

    private static string FormatSwitchTarget(RuntimePackSwitchPolicyArtifact policy)
    {
        if (!policy.PinActive)
        {
            return "(unpinned)";
        }

        return $"{policy.PackId}@{policy.PackVersion} ({policy.Channel})";
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
}

public sealed class RuntimePackPolicyPreviewResult
{
    private RuntimePackPolicyPreviewResult(
        bool succeeded,
        string summary,
        RuntimePackPolicyPreviewArtifact? artifact,
        IReadOnlyList<string> failureCodes)
    {
        Succeeded = succeeded;
        Summary = summary;
        Artifact = artifact;
        FailureCodes = failureCodes;
    }

    public bool Succeeded { get; }

    public string Summary { get; }

    public RuntimePackPolicyPreviewArtifact? Artifact { get; }

    public IReadOnlyList<string> FailureCodes { get; }

    public static RuntimePackPolicyPreviewResult CreateSuccess(RuntimePackPolicyPreviewArtifact artifact)
    {
        return new RuntimePackPolicyPreviewResult(true, artifact.Summary, artifact, Array.Empty<string>());
    }

    public static RuntimePackPolicyPreviewResult Rejected(string summary, IReadOnlyList<string> failureCodes)
    {
        return new RuntimePackPolicyPreviewResult(false, summary, null, failureCodes);
    }
}

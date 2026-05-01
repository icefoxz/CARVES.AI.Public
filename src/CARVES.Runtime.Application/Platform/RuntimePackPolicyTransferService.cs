using System.Text.Json;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Persistence;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimePackPolicyTransferService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly string repoRoot;
    private readonly IRuntimeArtifactRepository artifactRepository;
    private readonly RuntimePackAdmissionPolicyService admissionPolicyService;
    private readonly RuntimePackSwitchPolicyService switchPolicyService;
    private readonly RuntimePackPolicyPackageValidationService policyPackageValidationService;
    private readonly IControlPlaneConfigRepository configRepository;

    public RuntimePackPolicyTransferService(
        string repoRoot,
        IRuntimeArtifactRepository artifactRepository,
        IControlPlaneConfigRepository configRepository)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.artifactRepository = artifactRepository;
        this.configRepository = configRepository;
        admissionPolicyService = new RuntimePackAdmissionPolicyService(configRepository, artifactRepository);
        switchPolicyService = new RuntimePackSwitchPolicyService(artifactRepository);
        policyPackageValidationService = new RuntimePackPolicyPackageValidationService(configRepository);
    }

    public RuntimePackPolicyTransferSurface BuildSurface()
    {
        var runtimeStandardVersion = NormalizeVersion(configRepository.LoadCarvesCodeStandard().Version);
        var currentAdmissionPolicy = admissionPolicyService.BuildCurrentPolicy();
        var currentSwitchPolicy = switchPolicyService.BuildCurrentPolicy();
        return new RuntimePackPolicyTransferSurface
        {
            RuntimeStandardVersion = runtimeStandardVersion,
            CurrentAdmissionPolicy = currentAdmissionPolicy,
            CurrentSwitchPolicy = currentSwitchPolicy,
            Summary = $"Runtime-local pack policy transfer can export or import admission policy {currentAdmissionPolicy.PolicyId} and switch policy {currentSwitchPolicy.PolicyId} without opening registry or rollout lines.",
            SupportedCommands =
            [
                "runtime export-pack-policy <output-path>",
                "runtime preview-pack-policy <input-path>",
                "runtime import-pack-policy <input-path>",
                "validate runtime-pack-policy-package <input-path>",
                "inspect runtime-pack-policy-transfer",
                "api runtime-pack-policy-transfer",
            ],
            Notes =
            [
                "Transfer stays local-runtime scoped and explicit.",
                "Preview validates and diffs a local policy package before import without mutating current truth.",
                "Imported policy state becomes current local truth through existing admission-policy and switch-policy lines.",
                "Registry, rollout, remote bundle sync, and automatic activation remain closed."
            ],
        };
    }

    public RuntimePackPolicyTransferResult Export(string outputPath)
    {
        var package = new RuntimePackPolicyPackage
        {
            RuntimeStandardVersion = NormalizeVersion(configRepository.LoadCarvesCodeStandard().Version),
            AdmissionPolicy = admissionPolicyService.BuildCurrentPolicy(),
            SwitchPolicy = switchPolicyService.BuildCurrentPolicy(),
            Summary = "Exported current local runtime pack admission and switch policy truth for bounded local transfer.",
            Notes =
            [
                "This package is for explicit local runtime export/import only.",
                "It is not a registry bundle and does not imply rollout or automatic activation."
            ],
        };

        var fullOutputPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullOutputPath, JsonSerializer.Serialize(package, JsonOptions));
        artifactRepository.SaveRuntimePackPolicyAuditEntry(new RuntimePackPolicyAuditEntry
        {
            EventKind = "policy_exported",
            SourceKind = "local_export",
            PackageId = package.PackageId,
            PackagePath = ToRepoRelativeOrAbsolute(fullOutputPath),
            ResultingAdmissionPolicyId = package.AdmissionPolicy.PolicyId,
            ResultingSwitchPolicyId = package.SwitchPolicy.PolicyId,
            Summary = $"Exported runtime-local pack policy package {package.PackageId} from admission policy {package.AdmissionPolicy.PolicyId} and switch policy {package.SwitchPolicy.PolicyId}.",
            ChecksPassed =
            [
                "export remained local-runtime scoped",
                "export referenced current admission and switch policy truth"
            ],
        });

        return RuntimePackPolicyTransferResult.CreateSuccess(package, fullOutputPath, imported: false);
    }

    public RuntimePackPolicyTransferResult Import(string inputPath)
    {
        var validation = policyPackageValidationService.Validate(inputPath);
        if (!validation.Succeeded || validation.Package is null || string.IsNullOrWhiteSpace(validation.Path))
        {
            return RuntimePackPolicyTransferResult.Rejected(
                "Runtime pack policy import rejected because the bounded local policy package failed runtime-local checks.",
                validation.FailureCodes);
        }

        var package = validation.Package;
        admissionPolicyService.SaveCurrentPolicy(package.AdmissionPolicy);
        switchPolicyService.SaveCurrentPolicy(package.SwitchPolicy);
        artifactRepository.SaveRuntimePackPolicyAuditEntry(new RuntimePackPolicyAuditEntry
        {
            EventKind = "policy_imported",
            SourceKind = "local_import",
            PackageId = package.PackageId,
            PackagePath = ToRepoRelativeOrAbsolute(validation.Path),
            ResultingAdmissionPolicyId = package.AdmissionPolicy.PolicyId,
            ResultingSwitchPolicyId = package.SwitchPolicy.PolicyId,
            Summary = $"Imported runtime-local pack policy package {package.PackageId} into current admission policy {package.AdmissionPolicy.PolicyId} and switch policy {package.SwitchPolicy.PolicyId}.",
            ChecksPassed =
            [
                "import remained local-runtime scoped",
                "import updated current admission and switch policy truth",
                "registry and rollout remained closed"
            ],
        });

        return RuntimePackPolicyTransferResult.CreateSuccess(package, validation.Path, imported: true);
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
}

public sealed class RuntimePackPolicyTransferResult
{
    private RuntimePackPolicyTransferResult(
        bool succeeded,
        string summary,
        RuntimePackPolicyPackage? package,
        string? path,
        bool imported,
        IReadOnlyList<string> failureCodes)
    {
        Succeeded = succeeded;
        Summary = summary;
        Package = package;
        Path = path;
        Imported = imported;
        FailureCodes = failureCodes;
    }

    public bool Succeeded { get; }

    public string Summary { get; }

    public RuntimePackPolicyPackage? Package { get; }

    public string? Path { get; }

    public bool Imported { get; }

    public IReadOnlyList<string> FailureCodes { get; }

    public static RuntimePackPolicyTransferResult CreateSuccess(RuntimePackPolicyPackage package, string path, bool imported)
    {
        var action = imported ? "Imported" : "Exported";
        return new RuntimePackPolicyTransferResult(true, $"{action} runtime-local pack policy package {package.PackageId}.", package, path, imported, Array.Empty<string>());
    }

    public static RuntimePackPolicyTransferResult Rejected(string summary, IReadOnlyList<string> failureCodes)
    {
        return new RuntimePackPolicyTransferResult(false, summary, null, null, false, failureCodes);
    }
}

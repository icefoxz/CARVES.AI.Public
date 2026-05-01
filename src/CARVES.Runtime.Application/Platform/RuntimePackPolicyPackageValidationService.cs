using System.Text.Json;
using Carves.Runtime.Application.Persistence;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimePackPolicyPackageValidationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly IControlPlaneConfigRepository configRepository;

    public RuntimePackPolicyPackageValidationService(IControlPlaneConfigRepository configRepository)
    {
        this.configRepository = configRepository;
    }

    public RuntimePackPolicyPackageValidationResult Validate(string inputPath)
    {
        var fullInputPath = Path.GetFullPath(inputPath);
        RuntimePackPolicyPackage? package;
        try
        {
            package = JsonSerializer.Deserialize<RuntimePackPolicyPackage>(File.ReadAllText(fullInputPath), JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return RuntimePackPolicyPackageValidationResult.Rejected(
                "Runtime-local pack policy package could not be read as bounded local policy truth.",
                fullInputPath,
                ["runtime_pack_policy_package_payload_unreadable"]);
        }

        if (package is null)
        {
            return RuntimePackPolicyPackageValidationResult.Rejected(
                "Runtime-local pack policy package payload was empty.",
                fullInputPath,
                ["runtime_pack_policy_package_payload_missing"]);
        }

        return Validate(package, fullInputPath);
    }

    public RuntimePackPolicyPackageValidationResult Validate(RuntimePackPolicyPackage package, string? inputPath = null)
    {
        var checksPassed = new List<string>();
        var failureCodes = new List<string>();
        var currentRuntimeStandardVersion = NormalizeVersion(configRepository.LoadCarvesCodeStandard().Version);

        if (string.Equals(package.RuntimeStandardVersion, currentRuntimeStandardVersion, StringComparison.Ordinal))
        {
            checksPassed.Add("runtime standard matches current local runtime");
        }
        else
        {
            failureCodes.Add("runtime_pack_policy_package_runtime_standard_mismatch");
        }

        if (package.AdmissionPolicy.AllowedChannels.Count > 0)
        {
            checksPassed.Add("admission policy declares at least one allowed channel");
        }
        else
        {
            failureCodes.Add("runtime_pack_policy_package_allowed_channels_missing");
        }

        if (package.AdmissionPolicy.AllowedPackTypes.Count > 0)
        {
            checksPassed.Add("admission policy declares at least one allowed pack type");
        }
        else
        {
            failureCodes.Add("runtime_pack_policy_package_allowed_pack_types_missing");
        }

        if (!string.IsNullOrWhiteSpace(package.AdmissionPolicy.PolicyId))
        {
            checksPassed.Add("admission policy id is present");
        }
        else
        {
            failureCodes.Add("runtime_pack_policy_package_admission_policy_id_missing");
        }

        if (!string.IsNullOrWhiteSpace(package.SwitchPolicy.PolicyId))
        {
            checksPassed.Add("switch policy id is present");
        }
        else
        {
            failureCodes.Add("runtime_pack_policy_package_switch_policy_id_missing");
        }

        if (!package.SwitchPolicy.PinActive
            || (!string.IsNullOrWhiteSpace(package.SwitchPolicy.PackId)
                && !string.IsNullOrWhiteSpace(package.SwitchPolicy.PackVersion)
                && !string.IsNullOrWhiteSpace(package.SwitchPolicy.Channel)))
        {
            checksPassed.Add("switch policy target is internally consistent");
        }
        else
        {
            failureCodes.Add("runtime_pack_policy_package_switch_policy_invalid");
        }

        if (failureCodes.Count > 0)
        {
            return RuntimePackPolicyPackageValidationResult.Rejected(
                "Runtime-local pack policy package failed bounded validation checks.",
                inputPath,
                failureCodes);
        }

        return RuntimePackPolicyPackageValidationResult.CreateSuccess(
            package,
            inputPath,
            $"Validated runtime-local pack policy package {package.PackageId}.",
            checksPassed);
    }

    public static string DescribeFailureCode(string failureCode)
    {
        return failureCode switch
        {
            "runtime_pack_policy_package_payload_unreadable" => "Package payload could not be read as runtime-local pack policy JSON.",
            "runtime_pack_policy_package_payload_missing" => "Package payload was empty.",
            "runtime_pack_policy_package_runtime_standard_mismatch" => "Package runtime standard does not match the current runtime standard.",
            "runtime_pack_policy_package_allowed_channels_missing" => "Admission policy must declare at least one allowed channel.",
            "runtime_pack_policy_package_allowed_pack_types_missing" => "Admission policy must declare at least one allowed pack type.",
            "runtime_pack_policy_package_admission_policy_id_missing" => "Admission policy id is required.",
            "runtime_pack_policy_package_switch_policy_id_missing" => "Switch policy id is required.",
            "runtime_pack_policy_package_switch_policy_invalid" => "Pinned switch policy must include pack id, pack version, and channel.",
            _ => "Runtime-local pack policy package validation failed.",
        };
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
}

public sealed class RuntimePackPolicyPackageValidationResult
{
    private RuntimePackPolicyPackageValidationResult(
        bool succeeded,
        string summary,
        RuntimePackPolicyPackage? package,
        string? path,
        IReadOnlyList<string> failureCodes,
        IReadOnlyList<string> checksPassed)
    {
        Succeeded = succeeded;
        Summary = summary;
        Package = package;
        Path = path;
        FailureCodes = failureCodes;
        ChecksPassed = checksPassed;
    }

    public bool Succeeded { get; }

    public string Summary { get; }

    public RuntimePackPolicyPackage? Package { get; }

    public string? Path { get; }

    public IReadOnlyList<string> FailureCodes { get; }

    public IReadOnlyList<string> ChecksPassed { get; }

    public static RuntimePackPolicyPackageValidationResult CreateSuccess(
        RuntimePackPolicyPackage package,
        string? path,
        string summary,
        IReadOnlyList<string> checksPassed)
    {
        return new RuntimePackPolicyPackageValidationResult(true, summary, package, path, Array.Empty<string>(), checksPassed);
    }

    public static RuntimePackPolicyPackageValidationResult Rejected(
        string summary,
        string? path,
        IReadOnlyList<string> failureCodes)
    {
        return new RuntimePackPolicyPackageValidationResult(false, summary, null, path, failureCodes, Array.Empty<string>());
    }
}

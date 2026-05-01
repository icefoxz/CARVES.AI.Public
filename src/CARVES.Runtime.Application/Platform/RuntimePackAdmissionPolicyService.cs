using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Persistence;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimePackAdmissionPolicyService
{
    private readonly IControlPlaneConfigRepository configRepository;
    private readonly IRuntimeArtifactRepository artifactRepository;

    public RuntimePackAdmissionPolicyService(
        IControlPlaneConfigRepository configRepository,
        IRuntimeArtifactRepository artifactRepository)
    {
        this.configRepository = configRepository;
        this.artifactRepository = artifactRepository;
    }

    public RuntimePackAdmissionPolicySurface BuildSurface()
    {
        var runtimeStandardVersion = NormalizeVersion(configRepository.LoadCarvesCodeStandard().Version);
        var policy = BuildCurrentPolicy();
        return new RuntimePackAdmissionPolicySurface
        {
            RuntimeStandardVersion = runtimeStandardVersion,
            CurrentPolicy = policy,
            Summary = policy.Summary,
            Notes =
            [
                "This policy line is explicit local-runtime truth and does not introduce remote policy bundles.",
                "Admission policy constrains what may be locally admitted before selection, pinning, or execution attribution can reference a pack.",
                "Registry rollout, automatic activation, and multi-pack orchestration remain closed."
            ],
        };
    }

    public RuntimePackAdmissionPolicyArtifact BuildCurrentPolicy()
    {
        var runtimeStandardVersion = NormalizeVersion(configRepository.LoadCarvesCodeStandard().Version);
        return artifactRepository.TryLoadCurrentRuntimePackAdmissionPolicyArtifact()
               ?? RuntimePackAdmissionPolicyArtifact.CreateDefault(runtimeStandardVersion);
    }

    public RuntimePackAdmissionPolicyArtifact SaveCurrentPolicy(RuntimePackAdmissionPolicyArtifact artifact)
    {
        artifactRepository.SaveRuntimePackAdmissionPolicyArtifact(artifact);
        return artifact;
    }

    public RuntimePackAdmissionPolicyEvaluation Evaluate(
        string packType,
        string channel,
        bool hasSignature,
        bool hasProvenance)
    {
        var policy = BuildCurrentPolicy();
        var failureCodes = new List<string>();
        var checksPassed = new List<string>();

        if (policy.AllowsChannel(channel))
        {
            checksPassed.Add($"channel '{channel}' is allowed by local admission policy");
        }
        else
        {
            failureCodes.Add("runtime_pack_admission_channel_disallowed");
        }

        if (policy.AllowsPackType(packType))
        {
            checksPassed.Add($"pack type '{packType}' is allowed by local admission policy");
        }
        else
        {
            failureCodes.Add("runtime_pack_admission_pack_type_disallowed");
        }

        if (!policy.RequireSignature || hasSignature)
        {
            checksPassed.Add("signature requirement satisfied");
        }
        else
        {
            failureCodes.Add("runtime_pack_admission_signature_required");
        }

        if (!policy.RequireProvenance || hasProvenance)
        {
            checksPassed.Add("provenance requirement satisfied");
        }
        else
        {
            failureCodes.Add("runtime_pack_admission_provenance_required");
        }

        return new RuntimePackAdmissionPolicyEvaluation(policy, checksPassed, failureCodes);
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

public sealed class RuntimePackAdmissionPolicyEvaluation
{
    public RuntimePackAdmissionPolicyEvaluation(
        RuntimePackAdmissionPolicyArtifact policy,
        IReadOnlyList<string> checksPassed,
        IReadOnlyList<string> failureCodes)
    {
        Policy = policy;
        ChecksPassed = checksPassed;
        FailureCodes = failureCodes;
    }

    public RuntimePackAdmissionPolicyArtifact Policy { get; }

    public IReadOnlyList<string> ChecksPassed { get; }

    public IReadOnlyList<string> FailureCodes { get; }
}

using System.Text.Json;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeGovernanceContinuationGatePolicyService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;

    public RuntimeGovernanceContinuationGatePolicyService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public GovernanceContinuationGateRuntimePolicy LoadPolicy()
    {
        return LoadInternal().Policy;
    }

    public RuntimePolicyValidationResult Validate()
    {
        var result = LoadInternal();
        return new RuntimePolicyValidationResult(
            result.Errors.Count == 0,
            result.Errors,
            result.Warnings);
    }

    public void EnsurePersistedDefaults()
    {
        Directory.CreateDirectory(paths.PlatformPoliciesRoot);
        if (!File.Exists(paths.PlatformGovernanceContinuationGatePolicyFile))
        {
            File.WriteAllText(
                paths.PlatformGovernanceContinuationGatePolicyFile,
                JsonSerializer.Serialize(BuildDefaultPolicy(), JsonOptions));
        }
    }

    private RuntimeGovernanceContinuationGatePolicyLoadResult LoadInternal()
    {
        EnsurePersistedDefaults();
        var defaults = BuildDefaultPolicy();
        var errors = new List<string>();
        var warnings = new List<string>();
        var policy = LoadFile(defaults, errors);

        if (policy.ClosureBlockingBacklogKinds.Count == 0)
        {
            errors.Add("Governance continuation gate policy requires at least one closure_blocking_backlog_kind.");
        }

        var duplicateFamilies = policy.AcceptedResidualConcentrationFamilies
            .GroupBy(family => family, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateFamilies.Length > 0)
        {
            warnings.Add($"Governance continuation gate policy contains duplicate accepted residual families: {string.Join(", ", duplicateFamilies)}.");
        }

        var duplicateKinds = policy.ClosureBlockingBacklogKinds
            .GroupBy(kind => kind, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateKinds.Length > 0)
        {
            warnings.Add($"Governance continuation gate policy contains duplicate closure-blocking backlog kinds: {string.Join(", ", duplicateKinds)}.");
        }

        if (!policy.HoldContinuationWithoutQualifyingDelta)
        {
            warnings.Add("Governance continuation gate policy disables hold_continuation behavior when no qualifying closure delta exists.");
        }

        return new RuntimeGovernanceContinuationGatePolicyLoadResult(policy, errors, warnings);
    }

    private static GovernanceContinuationGateRuntimePolicy BuildDefaultPolicy()
    {
        return new GovernanceContinuationGateRuntimePolicy(
            Version: "1.0",
            HoldContinuationWithoutQualifyingDelta: true,
            AcceptedResidualConcentrationFamilies: [],
            ClosureBlockingBacklogKinds:
            [
                "file_too_large",
                "function_too_large",
            ]);
    }

    private GovernanceContinuationGateRuntimePolicy LoadFile(GovernanceContinuationGateRuntimePolicy defaults, List<string> errors)
    {
        try
        {
            if (!File.Exists(paths.PlatformGovernanceContinuationGatePolicyFile))
            {
                return defaults;
            }

            var json = File.ReadAllText(paths.PlatformGovernanceContinuationGatePolicyFile);
            var parsed = JsonSerializer.Deserialize<GovernanceContinuationGateRuntimePolicy>(json, JsonOptions);
            if (parsed is null)
            {
                errors.Add("Policy file 'governance-continuation-gate.policy.json' is empty or invalid; safe defaults are in use.");
                return defaults;
            }

            return parsed with
            {
                AcceptedResidualConcentrationFamilies = parsed.AcceptedResidualConcentrationFamilies
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                ClosureBlockingBacklogKinds = parsed.ClosureBlockingBacklogKinds
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
            };
        }
        catch (Exception exception)
        {
            errors.Add($"Policy file 'governance-continuation-gate.policy.json' could not be loaded: {exception.Message}. Safe defaults are in use.");
            return defaults;
        }
    }

    private sealed record RuntimeGovernanceContinuationGatePolicyLoadResult(
        GovernanceContinuationGateRuntimePolicy Policy,
        IReadOnlyList<string> Errors,
        IReadOnlyList<string> Warnings);
}

using System.Text.Json;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeHostInvokePolicyService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;

    public RuntimeHostInvokePolicyService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public HostInvokeRuntimePolicy LoadPolicy()
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
        if (!File.Exists(paths.PlatformHostInvokePolicyFile))
        {
            File.WriteAllText(
                paths.PlatformHostInvokePolicyFile,
                JsonSerializer.Serialize(BuildDefaultPolicy(), JsonOptions));
        }
    }

    private RuntimeHostInvokePolicyLoadResult LoadInternal()
    {
        EnsurePersistedDefaults();
        var defaults = BuildDefaultPolicy();
        var errors = new List<string>();
        var warnings = new List<string>();
        var policy = LoadFile(defaults, errors);

        ValidateClass("default_read", policy.DefaultRead, errors, warnings);
        ValidateClass("control_plane_mutation", policy.ControlPlaneMutation, errors, warnings);
        ValidateClass("attach_flow", policy.AttachFlow, errors, warnings);
        ValidateClass("delegated_execution", policy.DelegatedExecution, errors, warnings);

        if (!policy.ControlPlaneMutation.UseAcceptedOperationPolling)
        {
            warnings.Add("Host invoke policy disables accepted-operation polling for control-plane mutations.");
        }

        if (policy.AttachFlow.UseAcceptedOperationPolling)
        {
            warnings.Add("Attach-flow invoke policy enables accepted-operation polling, but host accept/poll is only honored for control-plane mutations.");
        }

        if (policy.DelegatedExecution.UseAcceptedOperationPolling)
        {
            warnings.Add("Delegated-execution invoke policy enables accepted-operation polling, but host accept/poll is only honored for control-plane mutations.");
        }

        return new RuntimeHostInvokePolicyLoadResult(policy, errors, warnings);
    }

    private HostInvokeRuntimePolicy BuildDefaultPolicy()
    {
        return new HostInvokeRuntimePolicy(
            Version: "1.0",
            DefaultRead: new HostInvokeClassRuntimePolicy(
                RequestTimeoutSeconds: 5,
                UseAcceptedOperationPolling: false,
                PollIntervalMs: 250,
                BaseWaitSeconds: 0,
                StallTimeoutSeconds: 0,
                MaxWaitSeconds: 0),
            ControlPlaneMutation: new HostInvokeClassRuntimePolicy(
                RequestTimeoutSeconds: 5,
                UseAcceptedOperationPolling: true,
                PollIntervalMs: 250,
                BaseWaitSeconds: 15,
                StallTimeoutSeconds: 10,
                MaxWaitSeconds: 45),
            AttachFlow: new HostInvokeClassRuntimePolicy(
                RequestTimeoutSeconds: 30,
                UseAcceptedOperationPolling: false,
                PollIntervalMs: 250,
                BaseWaitSeconds: 0,
                StallTimeoutSeconds: 0,
                MaxWaitSeconds: 0),
            DelegatedExecution: new HostInvokeClassRuntimePolicy(
                RequestTimeoutSeconds: 900,
                UseAcceptedOperationPolling: false,
                PollIntervalMs: 250,
                BaseWaitSeconds: 0,
                StallTimeoutSeconds: 0,
                MaxWaitSeconds: 0));
    }

    private HostInvokeRuntimePolicy LoadFile(HostInvokeRuntimePolicy defaults, List<string> errors)
    {
        try
        {
            if (!File.Exists(paths.PlatformHostInvokePolicyFile))
            {
                return defaults;
            }

            var json = File.ReadAllText(paths.PlatformHostInvokePolicyFile);
            var parsed = JsonSerializer.Deserialize<HostInvokeRuntimePolicy>(json, JsonOptions);
            if (parsed is null)
            {
                errors.Add("Policy file 'host-invoke.policy.json' is empty or invalid; safe defaults are in use.");
                return defaults;
            }

            return parsed;
        }
        catch (Exception exception)
        {
            errors.Add($"Policy file 'host-invoke.policy.json' could not be loaded: {exception.Message}. Safe defaults are in use.");
            return defaults;
        }
    }

    private static void ValidateClass(
        string classId,
        HostInvokeClassRuntimePolicy policy,
        List<string> errors,
        List<string> warnings)
    {
        if (policy.RequestTimeoutSeconds <= 0)
        {
            errors.Add($"Host invoke policy class '{classId}' requires request_timeout_seconds > 0.");
        }

        if (policy.PollIntervalMs < 0 || policy.StallTimeoutSeconds < 0 || policy.MaxWaitSeconds < 0)
        {
            errors.Add($"Host invoke policy class '{classId}' requires non-negative polling and wait values.");
        }

        if (policy.UseAcceptedOperationPolling)
        {
            if (policy.PollIntervalMs <= 0)
            {
                errors.Add($"Host invoke policy class '{classId}' requires poll_interval_ms > 0 when accepted-operation polling is enabled.");
            }

            if (policy.BaseWaitSeconds <= 0)
            {
                errors.Add($"Host invoke policy class '{classId}' requires base_wait_seconds > 0 when accepted-operation polling is enabled.");
            }

            if (policy.StallTimeoutSeconds <= 0)
            {
                errors.Add($"Host invoke policy class '{classId}' requires stall_timeout_seconds > 0 when accepted-operation polling is enabled.");
            }

            if (policy.MaxWaitSeconds <= 0)
            {
                errors.Add($"Host invoke policy class '{classId}' requires max_wait_seconds > 0 when accepted-operation polling is enabled.");
            }

            if (policy.MaxWaitSeconds > 0 && policy.StallTimeoutSeconds > policy.MaxWaitSeconds)
            {
                errors.Add($"Host invoke policy class '{classId}' requires stall_timeout_seconds <= max_wait_seconds.");
            }

            if (policy.MaxWaitSeconds > 0 && policy.BaseWaitSeconds > policy.MaxWaitSeconds)
            {
                errors.Add($"Host invoke policy class '{classId}' requires base_wait_seconds <= max_wait_seconds.");
            }

            if (policy.MaxWaitSeconds > 0 && policy.MaxWaitSeconds <= policy.RequestTimeoutSeconds)
            {
                warnings.Add($"Host invoke policy class '{classId}' waits no longer than its initial request timeout.");
            }

            return;
        }

        if (policy.BaseWaitSeconds > 0 || policy.StallTimeoutSeconds > 0 || policy.MaxWaitSeconds > 0)
        {
            warnings.Add($"Host invoke policy class '{classId}' declares adaptive wait values while accepted-operation polling is disabled.");
        }
    }

    private sealed record RuntimeHostInvokePolicyLoadResult(
        HostInvokeRuntimePolicy Policy,
        IReadOnlyList<string> Errors,
        IReadOnlyList<string> Warnings);
}

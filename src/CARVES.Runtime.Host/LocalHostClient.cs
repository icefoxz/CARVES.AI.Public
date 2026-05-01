using System.Net.Http;
using System.Net;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Host;

internal sealed class LocalHostClient
{
    private const string AgentRequestTimeoutCapEnvironmentVariable = "CARVES_AGENT_REQUEST_TIMEOUT_CAP_SECONDS";
    private static readonly TimeSpan DiscoveryPollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan DefaultSurfaceReadTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan AggregateSurfaceReadTimeout = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private readonly string repoRoot;
    private readonly LocalHostDiscoveryService discoveryService;
    private readonly RuntimeHostInvokePolicyService hostInvokePolicyService;

    public LocalHostClient(string repoRoot)
    {
        this.repoRoot = repoRoot;
        discoveryService = new LocalHostDiscoveryService();
        hostInvokePolicyService = new RuntimeHostInvokePolicyService(ControlPlanePaths.FromRepoRoot(this.repoRoot));
    }

    public HostDiscoveryResult Discover(params string[] requiredCapabilities)
    {
        var allowMachineDescriptorFallbackForForeignRepo = requiredCapabilities.Any(capability =>
            string.Equals(capability, "attach-flow", StringComparison.OrdinalIgnoreCase));

        return requiredCapabilities.Length == 0
            ? discoveryService.Discover(repoRoot)
            : discoveryService.EnsureCompatible(repoRoot, requiredCapabilities, allowMachineDescriptorFallbackForForeignRepo);
    }

    public OperatorCommandResult Invoke(string command, IReadOnlyList<string> arguments, params string[] requiredCapabilities)
    {
        var discovery = Discover(requiredCapabilities);
        if (!discovery.HostRunning || discovery.Summary is null)
        {
            return OperatorCommandResult.Failure(discovery.Message);
        }

        var surfaceCompatibility = HostCommandSurfaceCatalog.Evaluate(discovery.Summary);
        if (!surfaceCompatibility.Compatible)
        {
            return HostCommandSurfaceCatalog.BuildStaleSurfaceResult(discovery, command, arguments);
        }

        var policy = ResolveInvokePolicy(command, arguments);
        var operationId = policy.UseAcceptedOperationPolling ? $"hostop-client-{Guid.NewGuid():N}" : null;
        using var client = BuildClient(policy.RequestTimeout);
        HttpResponseMessage response;
        try
        {
            using var request = CreateJsonRequestMessage(
                HttpMethod.Post,
                $"{discovery.Summary.BaseUrl}/invoke",
                new HostCommandRequest(command, arguments, repoRoot, policy.UseAcceptedOperationPolling, operationId));
            response = client.Send(request);
        }
        catch (TaskCanceledException) when (policy.UseAcceptedOperationPolling && !string.IsNullOrWhiteSpace(operationId))
        {
            var recovered = TryRecoverAcceptedOperationAfterInitialTimeout(discovery.Summary, operationId!, policy);
            if (recovered is not null)
            {
                return WaitForAcceptedOperation(discovery.Summary, operationId!, policy, recovered, recoveredFromInitialRequestTimeout: true);
            }

            return OperatorCommandResult.Failure(
                $"Host request for '{command}' exceeded the initial request timeout before an accepted-operation receipt was observed.",
                $"Base URL: {discovery.Summary.BaseUrl}",
                $"Operation: {operationId}");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = ReadContentAsString(response.Content);
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorBody)
                    ? $"Host returned {(int)response.StatusCode} ({response.ReasonPhrase}) for '{command}'."
                    : errorBody);
            }
            var payload = ReadContentAsJson<HostCommandResponse>(response.Content)
                ?? throw new InvalidOperationException("Host returned no command payload.");

            if (payload.Accepted && !payload.Completed && !string.IsNullOrWhiteSpace(payload.OperationId))
            {
                return WaitForAcceptedOperation(
                    discovery.Summary,
                    payload.OperationId,
                    policy,
                    new HostAcceptedOperationStatusResponse(
                        payload.OperationId,
                        command,
                        payload.OperationState ?? "accepted",
                        payload.Completed,
                        payload.ExitCode,
                        payload.Lines,
                        payload.UpdatedAt ?? DateTimeOffset.UtcNow,
                        payload.UpdatedAt ?? DateTimeOffset.UtcNow,
                        payload.Completed ? payload.UpdatedAt : null,
                        payload.ProgressMarker ?? payload.OperationState ?? HostAcceptedOperationProgressMarkers.Accepted,
                        payload.ProgressOrdinal ?? HostAcceptedOperationProgressMarkers.ResolveOrdinal(payload.ProgressMarker ?? payload.OperationState ?? HostAcceptedOperationProgressMarkers.Accepted),
                        payload.ProgressAt ?? payload.UpdatedAt ?? DateTimeOffset.UtcNow),
                    recoveredFromInitialRequestTimeout: false);
            }

            List<string> lines = LooksLikeJsonPayload(payload.Lines)
                ? []
                : new List<string> { $"Connected to host: {discovery.Summary.BaseUrl}" };
            lines.AddRange(payload.Lines);
            return new OperatorCommandResult(payload.ExitCode, lines);
        }
    }

    public AgentResponseEnvelope SendAgent(AgentRequestEnvelope request)
    {
        var discovery = Discover("agent-gateway");
        if (!discovery.HostRunning || discovery.Summary is null)
        {
            return new AgentResponseEnvelope(false, "host_missing", discovery.Message, null, null);
        }

        using var client = BuildClient(ResolveAgentTimeout(request));
        using var requestMessage = CreateJsonRequestMessage(HttpMethod.Post, $"{discovery.Summary.BaseUrl}/agent", request);
        using var response = client.Send(requestMessage);
        response.EnsureSuccessStatusCode();
        return ReadContentAsJson<AgentResponseEnvelope>(response.Content)
            ?? throw new InvalidOperationException("Host returned no agent response.");
    }

    public string GetString(string relativePath, params string[] requiredCapabilities)
    {
        var discovery = Discover(requiredCapabilities);
        if (!discovery.HostRunning || discovery.Summary is null)
        {
            throw new InvalidOperationException(discovery.Message);
        }

        using var client = BuildClient(ResolveSurfaceReadTimeout(relativePath));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{discovery.Summary.BaseUrl}{relativePath}");
        using var response = client.Send(request);
        response.EnsureSuccessStatusCode();
        return ReadContentAsString(response.Content);
    }

    public JsonNode GetJson(string relativePath, params string[] requiredCapabilities)
    {
        return JsonNode.Parse(GetString(relativePath, requiredCapabilities))
            ?? throw new InvalidOperationException($"Host returned invalid JSON for '{relativePath}'.");
    }

    public OperatorCommandResult Stop(string reason, bool force)
    {
        var discovery = Discover();
        if ((!discovery.HostRunning || discovery.Summary is null) && discovery.Descriptor is not null)
        {
            discovery = WaitForRunningState(TimeSpan.FromSeconds(2));
        }

        if (!discovery.HostRunning || discovery.Summary is null)
        {
            if (force && discovery.Descriptor is not null)
            {
                var forced = LocalHostTerminator.ForceStop(repoRoot, discovery.Descriptor, $"Force-stopped host: {reason}");
                if (forced)
                {
                    PersistStoppedHostRegistryEntry(discovery);
                }
                return forced
                    ? OperatorCommandResult.Success($"Host force-stopped: {reason}")
                    : OperatorCommandResult.Failure(discovery.Message);
            }

            return OperatorCommandResult.Failure(discovery.Message);
        }

        using var client = BuildClient(TimeSpan.FromSeconds(force ? 10 : 5));
        var content = new StringContent(
            JsonSerializer.Serialize(new HostStopRequest(reason, force), JsonOptions),
            Encoding.UTF8,
            "application/json");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{discovery.Summary.BaseUrl}/host/stop")
            {
                Content = content,
            };
            using var response = client.Send(request);
            response.EnsureSuccessStatusCode();
            _ = ReadContentAsString(response.Content);
        }
        catch when (force && discovery.Descriptor is not null)
        {
            var forced = LocalHostTerminator.ForceStop(repoRoot, discovery.Descriptor, $"Force-stopped host after graceful stop failure: {reason}");
            if (forced)
            {
                PersistStoppedHostRegistryEntry(discovery);
            }
            return forced
                ? OperatorCommandResult.Success(
                    $"Host force-stopped after graceful stop failure: {reason}",
                    $"Base URL: {discovery.Summary.BaseUrl}")
                : OperatorCommandResult.Failure("Host stop failed and force stop could not terminate the resident host.");
        }

        if (force && discovery.Descriptor is not null)
        {
            var current = WaitForStoppedState(TimeSpan.FromSeconds(2));
            if (!IsStopped(current))
            {
                var forced = LocalHostTerminator.ForceStop(repoRoot, discovery.Descriptor, $"Force-stopped host after graceful stop timeout: {reason}");
                if (!forced)
                {
                    return OperatorCommandResult.Failure("Host stop timed out and force stop could not terminate the resident host.");
                }
            }
        }

        var stopped = WaitForStoppedState(TimeSpan.FromSeconds(5));
        if (!IsStopped(stopped) && discovery.Descriptor is not null)
        {
            var forced = LocalHostTerminator.ForceStop(repoRoot, discovery.Descriptor, $"Force-stopped host after shutdown reconciliation timeout: {reason}");
            if (!forced)
            {
                return OperatorCommandResult.Failure("Host stop was accepted, but shutdown reconciliation timed out and force stop did not terminate the resident host.");
            }

            stopped = WaitForStoppedState(TimeSpan.FromSeconds(5));
            if (!IsStopped(stopped))
            {
                return OperatorCommandResult.Failure("Host stop was accepted, but the resident host still did not reconcile to stopped state after force stop.");
            }
        }

        if (discovery.Descriptor is not null
            && !WaitForProcessExit(discovery.Descriptor.ProcessId, TimeSpan.FromSeconds(3)))
        {
            var forced = LocalHostTerminator.ForceStop(
                repoRoot,
                discovery.Descriptor,
                $"Force-stopped host after stopped-state reconciliation left process {discovery.Descriptor.ProcessId} alive: {reason}");
            if (!forced)
            {
                return OperatorCommandResult.Failure("Host stop reconciled to stopped state, but the resident host process remained alive.");
            }
        }

        PersistStoppedHostRegistryEntry(discovery);

        return OperatorCommandResult.Success(
            force ? $"Host stop requested: {reason} (force fallback armed)" : $"Host stop requested: {reason}",
            $"Base URL: {discovery.Summary.BaseUrl}");
    }

    public OperatorCommandResult Control(string action, string reason)
    {
        var discovery = Discover();
        if (!discovery.HostRunning || discovery.Summary is null)
        {
            return OperatorCommandResult.Failure(discovery.Message);
        }

        using var client = BuildClient(TimeSpan.FromSeconds(5));
        using var request = CreateJsonRequestMessage(
            HttpMethod.Post,
            $"{discovery.Summary.BaseUrl}/host/control",
            new HostControlRequest(action, reason));
        using var response = client.Send(request);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = ReadContentAsString(response.Content);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorBody)
                ? $"Host returned {(int)response.StatusCode} ({response.ReasonPhrase}) for host {action}."
                : errorBody);
        }

        var payload = ReadContentAsJson<HostCommandResponse>(response.Content)
            ?? throw new InvalidOperationException("Host returned no host control payload.");
        var lines = new List<string> { $"Connected to host: {discovery.Summary.BaseUrl}" };
        lines.AddRange(payload.Lines);
        return new OperatorCommandResult(payload.ExitCode, lines);
    }

    private HostDiscoveryResult WaitForRunningState(TimeSpan timeout)
    {
        return BlockingHostWait.Poll(
            timeout,
            DiscoveryPollInterval,
            () => Discover(),
            static current => current.HostRunning && current.Summary is not null);
    }

    private HostDiscoveryResult WaitForStoppedState(TimeSpan timeout)
    {
        return BlockingHostWait.Poll(timeout, DiscoveryPollInterval, () => Discover(), IsStopped);
    }

    private static bool IsStopped(HostDiscoveryResult discovery)
    {
        return !discovery.HostRunning
               && string.Equals(discovery.Snapshot?.State.ToString(), HostRuntimeSnapshotState.Stopped.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool WaitForProcessExit(int processId, TimeSpan timeout)
    {
        return BlockingHostWait.WaitUntil(timeout, DiscoveryPollInterval, () => !IsProcessAlive(processId));
    }

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static HttpClient BuildClient(TimeSpan timeout)
    {
        return new HttpClient
        {
            Timeout = timeout,
        };
    }

    private static TimeSpan ResolveSurfaceReadTimeout(string relativePath)
    {
        var normalizedPath = NormalizeSurfacePath(relativePath);
        return normalizedPath is "/api/runtime-agent-thread-start"
            or "/inspect/runtime-agent-thread-start"
            or "/api/worker-automation-schedule-tick"
            or "/inspect/worker-automation-schedule-tick"
            ? AggregateSurfaceReadTimeout
            : DefaultSurfaceReadTimeout;
    }

    private static string NormalizeSurfacePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }

        var queryIndex = relativePath.IndexOf('?');
        var path = queryIndex >= 0 ? relativePath[..queryIndex] : relativePath;
        return path.TrimEnd('/').ToLowerInvariant();
    }

    private void PersistStoppedHostRegistryEntry(HostDiscoveryResult discovery)
    {
        var hostId = discovery.Descriptor?.HostId;
        if (string.IsNullOrWhiteSpace(hostId))
        {
            return;
        }

        var machineId = discovery.Descriptor?.MachineId ?? LocalHostPaths.ResolveMachineId();
        var endpoint = discovery.Summary?.BaseUrl ?? discovery.Descriptor?.BaseUrl ?? string.Empty;
        var repository = new JsonHostRegistryRepository(ControlPlanePaths.FromRepoRoot(repoRoot));
        var service = new HostRegistryService(repository);
        service.Upsert(hostId, machineId, endpoint, HostInstanceStatus.Stopped);
    }

    private HostCommandInvokePolicy ResolveInvokePolicy(string command, IReadOnlyList<string> arguments)
    {
        return HostCommandInvokePolicyCatalog.Resolve(hostInvokePolicyService.LoadPolicy(), command, arguments);
    }

    private static TimeSpan ResolveAgentTimeout(AgentRequestEnvelope request)
    {
        if (!string.Equals(request.OperationClass, "request", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(request.Operation, "run_task", StringComparison.OrdinalIgnoreCase))
        {
            return TimeSpan.FromSeconds(5);
        }

        var defaultTimeout = TimeSpan.FromMinutes(15);
        var configuredValue = Environment.GetEnvironmentVariable(AgentRequestTimeoutCapEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configuredValue)
            || !int.TryParse(configuredValue, out var parsedValue)
            || parsedValue <= 0)
        {
            return defaultTimeout;
        }

        return TimeSpan.FromSeconds(parsedValue) < defaultTimeout
            ? TimeSpan.FromSeconds(parsedValue)
            : defaultTimeout;
    }

    private static HostAcceptedOperationStatusResponse? TryRecoverAcceptedOperationAfterInitialTimeout(
        HostHandshakeSummary summary,
        string operationId,
        HostCommandInvokePolicy policy)
    {
        using var client = BuildClient(TimeSpan.FromSeconds(5));
        return BlockingHostWait.Poll(
            ResolveAcceptedOperationLookupBudget(policy),
            policy.PollInterval,
            () => TryGetAcceptedOperationStatus(client, summary.BaseUrl, operationId),
            static status => status is not null);
    }

    private static TimeSpan ResolveAcceptedOperationLookupBudget(HostCommandInvokePolicy policy)
    {
        var seconds = Math.Clamp(
            Math.Ceiling(Math.Max(policy.RequestTimeout.TotalSeconds, policy.PollInterval.TotalSeconds * 4)),
            2,
            10);
        return TimeSpan.FromSeconds(seconds);
    }

    private static HostAcceptedOperationStatusResponse? TryGetAcceptedOperationStatus(HttpClient client, string baseUrl, string operationId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/operations/{Uri.EscapeDataString(operationId)}");
        using var response = client.Send(request);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = ReadContentAsString(response.Content);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorBody)
                ? $"Host returned {(int)response.StatusCode} ({response.ReasonPhrase}) for accepted operation '{operationId}'."
                : errorBody);
        }

        return ReadContentAsJson<HostAcceptedOperationStatusResponse>(response.Content)
               ?? throw new InvalidOperationException($"Host returned no accepted-operation payload for '{operationId}'.");
    }

    private static OperatorCommandResult WaitForAcceptedOperation(
        HostHandshakeSummary summary,
        string operationId,
        HostCommandInvokePolicy policy,
        HostAcceptedOperationStatusResponse? initialStatus,
        bool recoveredFromInitialRequestTimeout)
    {
        var acceptedAt = initialStatus?.AcceptedAt ?? DateTimeOffset.UtcNow;
        var lastHeartbeatAt = initialStatus?.UpdatedAt ?? acceptedAt;
        var lastProgressAt = initialStatus?.ProgressAt ?? acceptedAt;
        var lastProgressMarker = initialStatus?.ProgressMarker ?? HostAcceptedOperationProgressMarkers.Accepted;
        var lastProgressOrdinal = initialStatus?.ProgressOrdinal ?? HostAcceptedOperationProgressMarkers.ResolveOrdinal(lastProgressMarker);
        var adaptiveDeadline = HostAcceptedOperationWaitBudget.ComputeInitialDeadline(acceptedAt, policy);
        using var client = BuildClient(TimeSpan.FromSeconds(5));
        var nextStatus = initialStatus;

        while (DateTimeOffset.UtcNow < acceptedAt.Add(policy.MaxWait))
        {
            var status = nextStatus;
            if (status is null)
            {
                BlockingHostWait.Delay(policy.PollInterval);
                status = TryGetAcceptedOperationStatus(client, summary.BaseUrl, operationId);
                if (status is null)
                {
                    continue;
                }
            }

            if (status.UpdatedAt > lastHeartbeatAt)
            {
                lastHeartbeatAt = status.UpdatedAt;
            }

            if (status.ProgressOrdinal > lastProgressOrdinal
                || !string.Equals(status.ProgressMarker, lastProgressMarker, StringComparison.OrdinalIgnoreCase))
            {
                lastProgressOrdinal = status.ProgressOrdinal;
                lastProgressMarker = string.IsNullOrWhiteSpace(status.ProgressMarker) ? lastProgressMarker : status.ProgressMarker;
                lastProgressAt = status.ProgressAt == default ? status.UpdatedAt : status.ProgressAt;
            }

            if (status.Completed)
            {
                var lines = new List<string> { $"Connected to host: {summary.BaseUrl}" };
                if (recoveredFromInitialRequestTimeout && !LooksLikeJsonPayload(status.Lines))
                {
                    lines.Add("Accepted-operation polling recovered after the initial host request timed out.");
                }
                lines.AddRange(status.Lines);
                return new OperatorCommandResult(status.ExitCode ?? 1, lines);
            }

            adaptiveDeadline = HostAcceptedOperationWaitBudget.ComputeAdaptiveDeadline(acceptedAt, lastProgressAt, policy);

            if (DateTimeOffset.UtcNow - lastHeartbeatAt > policy.StallTimeout)
            {
                return OperatorCommandResult.Failure(
                    $"Host accepted operation {operationId}, but heartbeat stalled while waiting for completion.",
                    $"Base URL: {summary.BaseUrl}",
                    $"Operation: {operationId}",
                    $"Last progress marker: {lastProgressMarker}");
            }

            if (DateTimeOffset.UtcNow >= adaptiveDeadline)
            {
                return OperatorCommandResult.Failure(
                    $"Host accepted operation {operationId}, but the adaptive wait budget expired before further stage progress.",
                    $"Base URL: {summary.BaseUrl}",
                    $"Operation: {operationId}",
                    $"Last progress marker: {lastProgressMarker}",
                    $"Adaptive deadline: {adaptiveDeadline:O}",
                    $"Hard max: {acceptedAt.Add(policy.MaxWait):O}");
            }

            nextStatus = null;
        }

        return OperatorCommandResult.Failure(
            $"Host accepted operation {operationId}, but the hard max wait budget was exceeded before completion.",
            $"Base URL: {summary.BaseUrl}",
            $"Operation: {operationId}",
            $"Max wait: {policy.MaxWait.TotalSeconds:0}s");
    }

    private static bool LooksLikeJsonPayload(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmed = line.TrimStart();
            return trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal);
        }

        return false;
    }

    private static HttpRequestMessage CreateJsonRequestMessage(HttpMethod method, string url, object payload)
    {
        return new HttpRequestMessage(method, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json"),
        };
    }

    private static string ReadContentAsString(HttpContent content)
    {
        using var stream = content.ReadAsStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static T? ReadContentAsJson<T>(HttpContent content)
    {
        using var stream = content.ReadAsStream();
        return JsonSerializer.Deserialize<T>(stream, JsonOptions);
    }
}

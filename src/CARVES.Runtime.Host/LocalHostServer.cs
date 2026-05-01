using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostServer
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly object ConsoleLogLock = new();
    private static readonly AsyncLocal<string?> CurrentGatewayRequestId = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private readonly RuntimeServices services;
    private readonly LocalHostState hostState;
    private readonly LocalHostSurfaceService hostSurfaceService;
    private readonly HostedRuntimeLoopService hostLoopService;
    private readonly LocalHostSnapshotStore snapshotStore;
    private readonly HostRegistryService hostRegistryService;
    private readonly HostSessionService hostSessionService;
    private readonly string activityJournalPath;
    private readonly HostAcceptedOperationStore acceptedOperationStore = new();
    private readonly HttpListener listener = new();
    private readonly CancellationTokenSource shutdown = new();
    private static readonly JsonSerializerOptions DescriptorJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public LocalHostServer(RuntimeServices services, int port, int intervalMilliseconds)
    {
        this.services = services;
        var runtimeDirectory = LocalHostPaths.GetRuntimeDirectory(services.Paths.RepoRoot);
        Directory.CreateDirectory(runtimeDirectory);
        var deploymentDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var executablePath = typeof(Program).Assembly.Location;
        hostState = new LocalHostState(services.Paths.RepoRoot, $"http://127.0.0.1:{port}", runtimeDirectory, deploymentDirectory, executablePath, port);
        hostState.RecordRehydration(new HostRehydrationService(services).Rehydrate());
        hostSurfaceService = new LocalHostSurfaceService(services, acceptedOperationStore);
        hostLoopService = new HostedRuntimeLoopService(services, intervalMilliseconds);
        snapshotStore = new LocalHostSnapshotStore(services.Paths.RepoRoot);
        hostRegistryService = services.HostRegistryService;
        hostSessionService = new HostSessionService(services.Paths);
        activityJournalPath = LocalHostPaths.GetGatewayActivityJournalPath(services.Paths.RepoRoot);
        listener.Prefixes.Add($"{hostState.BaseUrl}/");
    }

    public void Run()
    {
        Task? requestLoop = null;
        Task? hostLoop = null;
        ConsoleCancelEventHandler? cancelHandler = null;
        var promotedActiveGeneration = false;
        var shutdownSummary = "Resident host stopped cleanly.";

        hostSessionService.Ensure(
            HostSessionService.BuildSessionId(hostState.HostId, hostState.StartedAt),
            hostState.HostId,
            services.Paths.RepoRoot,
            hostState.BaseUrl,
            RuntimeStageInfo.CurrentStage,
            hostState.StartedAt);

        try
        {
            listener.Start();
            requestLoop = AcceptLoopAsync(shutdown.Token);
            hostLoop = hostLoopService.RunAsync(hostState, SaveLiveLoopSnapshot, shutdown.Token);
            PromoteActiveHost();
            promotedActiveGeneration = true;
            WriteGatewayEvent(
                "gateway-started",
                $"repo_root={hostState.RepoRoot}",
                $"base_url={hostState.BaseUrl}",
                $"dashboard={hostState.DashboardUrl}",
                "role=connection_routing_observability",
                "automation_boundary=no_worker_automation_dispatch");
            WriteGatewayEvent("gateway-ready", "press Ctrl+C to stop the foreground gateway");
            cancelHandler = (_, args) =>
            {
                args.Cancel = true;
                shutdownSummary = "Resident host stopped after Ctrl+C.";
                WriteGatewayEvent("gateway-stop-requested", "source=ctrl_c");
                RequestShutdown();
            };
            Console.CancelKeyPress += cancelHandler;
            Task.WaitAll([requestLoop, hostLoop]);
        }
        catch (AggregateException aggregate) when (aggregate.InnerExceptions.All(exception => exception is TaskCanceledException or HttpListenerException))
        {
        }
        catch (Exception exception)
        {
            shutdownSummary = promotedActiveGeneration
                ? $"Resident host stopped after startup: {exception.Message}"
                : $"Resident host startup failed before readiness: {exception.Message}";
            throw;
        }
        finally
        {
            if (cancelHandler is not null)
            {
                Console.CancelKeyPress -= cancelHandler;
            }

            RequestShutdown();
            if (requestLoop is not null || hostLoop is not null)
            {
                try
                {
                    Task.WaitAll(new[] { requestLoop, hostLoop }.Where(static task => task is not null).Cast<Task>().ToArray(), TimeSpan.FromSeconds(2));
                }
                catch (AggregateException aggregate) when (aggregate.InnerExceptions.All(exception => exception is TaskCanceledException or HttpListenerException))
                {
                }
                catch
                {
                }
            }

            hostLoopService.PauseRunningInstances(promotedActiveGeneration
                ? "Resident host stopped cleanly."
                : "Resident host startup failed before readiness.");
            listener.Close();
            TryStopHostSession();
            if (promotedActiveGeneration)
            {
                DeleteDescriptor();
            }

            SaveSnapshot(HostRuntimeSnapshotState.Stopped, shutdownSummary);
            WriteGatewayEvent("gateway-stopped", $"summary={shutdownSummary}");
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested || !listener.IsListening)
            {
                break;
            }

            QueueBackgroundWork(() => HandleContext(context));
        }
    }

    private void WriteGatewayEvent(string eventName, params string[] fields)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var enrichedFields = EnrichGatewayEventFields(fields);
        var parsedFields = ParseGatewayEventFields(enrichedFields);
        lock (ConsoleLogLock)
        {
            Console.Out.Write("[carves-gateway] ");
            Console.Out.Write(timestamp.ToString("O"));
            Console.Out.Write(' ');
            Console.Out.Write(eventName);
            foreach (var field in enrichedFields)
            {
                if (!string.IsNullOrWhiteSpace(field))
                {
                    Console.Out.Write(' ');
                    Console.Out.Write(field);
                }
            }

            Console.Out.WriteLine();
            Console.Out.Flush();
            if (!GatewayActivityEventKinds.IsKnown(eventName))
            {
                return;
            }

            try
            {
                GatewayActivityJournal.Append(activityJournalPath, timestamp, eventName, parsedFields);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static string CreateGatewayRequestId()
    {
        return $"gwreq-{Guid.NewGuid():N}";
    }

    private static string[] EnrichGatewayEventFields(string[] fields)
    {
        var requestId = CurrentGatewayRequestId.Value;
        if (string.IsNullOrWhiteSpace(requestId)
            || fields.Any(static field => field.StartsWith("request_id=", StringComparison.Ordinal)))
        {
            return fields;
        }

        var enrichedFields = new string[fields.Length + 1];
        enrichedFields[0] = GatewayField("request_id", requestId);
        Array.Copy(fields, 0, enrichedFields, 1, fields.Length);
        return enrichedFields;
    }

    private static IReadOnlyDictionary<string, string> ParseGatewayEventFields(IEnumerable<string> fields)
    {
        var parsedFields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                continue;
            }

            var separatorIndex = field.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = field[..separatorIndex];
            var value = DecodeGatewayFieldValue(field[(separatorIndex + 1)..]);
            if (!string.IsNullOrWhiteSpace(key))
            {
                parsedFields[key] = value;
            }
        }

        return parsedFields;
    }

    private static string DecodeGatewayFieldValue(string value)
    {
        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
        {
            return value;
        }

        var builder = new StringBuilder();
        for (var index = 1; index < value.Length - 1; index++)
        {
            var character = value[index];
            if (character == '\\' && index + 1 < value.Length - 1)
            {
                builder.Append(value[++index]);
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static string GatewayField(string name, object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
        text = text.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        if (text.Length > 0
            && text.All(static character => !char.IsWhiteSpace(character) && character is not '"' and not '\\'))
        {
            return $"{name}={text}";
        }

        return $"{name}=\"{text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static void QueueBackgroundWork(Action work)
    {
        ThreadPool.QueueUserWorkItem(
            static state =>
            {
                try
                {
                    ((Action)state!).Invoke();
                }
                catch
                {
                }
            },
            work);
    }


    private sealed record ScopedRequestServices(bool Allowed, RuntimeServices? Services, string? Message);
}

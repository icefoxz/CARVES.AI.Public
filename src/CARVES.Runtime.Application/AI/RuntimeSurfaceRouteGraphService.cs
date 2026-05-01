using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeSurfaceRouteGraphService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;
    public RuntimeSurfaceRouteGraphService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public RuntimeConsumerRouteSurfaceRecord RecordSurface(
        string surfaceId,
        string producer,
        string surfaceKind,
        string content)
    {
        Directory.CreateDirectory(paths.RuntimeConsumerRouteGraphRoot);
        var surfaces = LoadSurfaces().ToDictionary(item => item.SurfaceId, StringComparer.Ordinal);
        var normalized = RuntimeTelemetryHashing.Normalize(content);
        var contentHash = RuntimeTelemetryHashing.Compute(normalized, paths);

        var record = new RuntimeConsumerRouteSurfaceRecord
        {
            SurfaceId = surfaceId,
            Producer = producer,
            SurfaceKind = surfaceKind,
            ContentHash = contentHash,
            HashMode = RuntimeTelemetryHashing.HashMode,
            HashSaltScope = RuntimeTelemetryHashing.HashSaltScope,
            HmacKeyId = RuntimeTelemetryHashing.HmacKeyId,
            HashAlgorithm = RuntimeTelemetryHashing.HashAlgorithm,
            NormalizationVersion = RuntimeTelemetryHashing.NormalizationVersion,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        surfaces[surfaceId] = record;
        Save(paths.RuntimeConsumerRouteGraphSurfacesFile, surfaces.Values.OrderBy(item => item.SurfaceId, StringComparer.Ordinal).ToArray());
        return record;
    }

    public RuntimeConsumerRouteEdgeRecord RecordRouteEdge(RuntimeConsumerRouteEdgeRecord edge)
    {
        Directory.CreateDirectory(paths.RuntimeConsumerRouteGraphRoot);
        var edges = LoadEdges().ToList();
        var index = edges.FindIndex(item =>
            string.Equals(item.SurfaceId, edge.SurfaceId, StringComparison.Ordinal)
            && string.Equals(item.Consumer, edge.Consumer, StringComparison.Ordinal)
            && string.Equals(item.DeclaredRouteKind, edge.DeclaredRouteKind, StringComparison.Ordinal)
            && string.Equals(item.ObservedRouteKind, edge.ObservedRouteKind, StringComparison.Ordinal)
            && string.Equals(item.FrequencyWindow, edge.FrequencyWindow, StringComparison.Ordinal));

        RuntimeConsumerRouteEdgeRecord merged;
        if (index >= 0)
        {
            var existing = edges[index];
            merged = existing with
            {
                ObservedCount = existing.ObservedCount + Math.Max(1, edge.ObservedCount),
                SampleCount = existing.SampleCount + Math.Max(1, edge.SampleCount),
                RetrievalHitCount = existing.RetrievalHitCount + edge.RetrievalHitCount,
                LlmReinjectionCount = existing.LlmReinjectionCount + edge.LlmReinjectionCount,
                AverageFanout = ComputeAverageFanout(existing, edge),
                LastSeen = edge.LastSeen ?? existing.LastSeen ?? DateTimeOffset.UtcNow,
                EvidenceSource = string.IsNullOrWhiteSpace(edge.EvidenceSource) ? existing.EvidenceSource : edge.EvidenceSource,
            };
            edges[index] = merged;
        }
        else
        {
            merged = edge with
            {
                ObservedCount = Math.Max(1, edge.ObservedCount),
                SampleCount = Math.Max(1, edge.SampleCount),
                LastSeen = edge.LastSeen ?? DateTimeOffset.UtcNow,
            };
            edges.Add(merged);
        }

        Save(paths.RuntimeConsumerRouteGraphEdgesFile, edges.OrderBy(item => item.SurfaceId, StringComparer.Ordinal).ThenBy(item => item.Consumer, StringComparer.Ordinal).ToArray());
        return merged;
    }

    public IReadOnlyList<RuntimeConsumerRouteSurfaceRecord> ListSurfaces()
    {
        return LoadSurfaces();
    }

    public IReadOnlyList<RuntimeConsumerRouteEdgeRecord> ListRouteEdges()
    {
        return LoadEdges();
    }

    private IReadOnlyList<RuntimeConsumerRouteSurfaceRecord> LoadSurfaces()
    {
        if (!File.Exists(paths.RuntimeConsumerRouteGraphSurfacesFile))
        {
            return Array.Empty<RuntimeConsumerRouteSurfaceRecord>();
        }

        return JsonSerializer.Deserialize<RuntimeConsumerRouteSurfaceRecord[]>(File.ReadAllText(paths.RuntimeConsumerRouteGraphSurfacesFile), JsonOptions)
               ?? Array.Empty<RuntimeConsumerRouteSurfaceRecord>();
    }

    private IReadOnlyList<RuntimeConsumerRouteEdgeRecord> LoadEdges()
    {
        if (!File.Exists(paths.RuntimeConsumerRouteGraphEdgesFile))
        {
            return Array.Empty<RuntimeConsumerRouteEdgeRecord>();
        }

        return JsonSerializer.Deserialize<RuntimeConsumerRouteEdgeRecord[]>(File.ReadAllText(paths.RuntimeConsumerRouteGraphEdgesFile), JsonOptions)
               ?? Array.Empty<RuntimeConsumerRouteEdgeRecord>();
    }

    private static void Save<T>(string path, T payload)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static double ComputeAverageFanout(RuntimeConsumerRouteEdgeRecord existing, RuntimeConsumerRouteEdgeRecord incoming)
    {
        var existingSamples = Math.Max(1, existing.SampleCount);
        var incomingSamples = Math.Max(1, incoming.SampleCount);
        return ((existing.AverageFanout * existingSamples) + (incoming.AverageFanout * incomingSamples)) / (existingSamples + incomingSamples);
    }
}

namespace Carves.Runtime.Application.AI;

public sealed class HttpTransportResponse
{
    public int StatusCode { get; init; }

    public string Body { get; init; } = string.Empty;

    public long? LatencyMs { get; init; }

    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public bool IsSuccessStatusCode => StatusCode is >= 200 and < 300;
}

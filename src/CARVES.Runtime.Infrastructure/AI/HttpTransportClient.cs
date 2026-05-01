using System.Diagnostics;
using Carves.Runtime.Application.AI;

namespace Carves.Runtime.Infrastructure.AI;

public sealed class HttpTransportClient : IHttpTransport
{
    public HttpTransportResponse Send(HttpTransportRequest request)
    {
        var attempts = Math.Max(1, request.MaxRetries + 1);
        Exception? lastException = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(Math.Max(5, request.TimeoutSeconds)),
                };
                using var message = new HttpRequestMessage(new HttpMethod(request.Method), request.Url);
                foreach (var header in request.Headers)
                {
                    if (!message.Headers.TryAddWithoutValidation(header.Key, header.Value))
                    {
                        message.Content ??= new StringContent(string.Empty);
                        message.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                if (!string.IsNullOrWhiteSpace(request.Body))
                {
                    message.Content = new StringContent(request.Body, System.Text.Encoding.UTF8, request.ContentType);
                }

                var stopwatch = Stopwatch.StartNew();
                using var response = httpClient.Send(message);
                stopwatch.Stop();
                var body = HttpContentSyncReader.ReadAsString(response.Content);
                var headers = response.Headers
                    .Concat(response.Content.Headers)
                    .ToDictionary(
                        pair => pair.Key,
                        pair => string.Join(", ", pair.Value),
                        StringComparer.OrdinalIgnoreCase);

                return new HttpTransportResponse
                {
                    StatusCode = (int)response.StatusCode,
                    Body = body,
                    LatencyMs = stopwatch.ElapsedMilliseconds,
                    Headers = headers,
                };
            }
            catch (Exception exception) when (attempt < attempts)
            {
                lastException = exception;
            }
            catch (Exception exception)
            {
                lastException = exception;
                break;
            }
        }

        throw lastException ?? new InvalidOperationException("HTTP transport failed without returning a response.");
    }
}

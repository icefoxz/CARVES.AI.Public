using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Carves.Runtime.IntegrationTests;

internal sealed class StubResponsesApiServer : IAsyncDisposable
{
    private readonly TcpListener listener;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly Task serverLoop;
    private readonly string responseBody;
    private readonly int statusCode;

    public StubResponsesApiServer(string responseBody, int statusCode = 200)
    {
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        this.responseBody = responseBody;
        this.statusCode = statusCode;
        Url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/v1";
        serverLoop = ListenAsync();
    }

    public string Url { get; }

    public string? LastRequestBody { get; private set; }

    public async ValueTask DisposeAsync()
    {
        cancellationTokenSource.Cancel();
        listener.Stop();
        try
        {
            await serverLoop.ConfigureAwait(false);
        }
        catch
        {
        }

        cancellationTokenSource.Dispose();
    }

    private async Task ListenAsync()
    {
        while (!cancellationTokenSource.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationTokenSource.Token);
                await HandleClientAsync(client, cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            finally
            {
                client?.Dispose();
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var contentLength = 0;
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(cancellationToken)))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(line["Content-Length:".Length..].Trim(), out var parsed))
            {
                contentLength = parsed;
            }
        }

        if (contentLength > 0)
        {
            var buffer = new char[contentLength];
            var offset = 0;
            while (offset < contentLength)
            {
                var read = await reader.ReadAsync(buffer.AsMemory(offset, contentLength - offset), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                offset += read;
            }

            LastRequestBody = new string(buffer, 0, offset);
        }

        var responseText = new StringBuilder()
            .AppendLine($"HTTP/1.1 {statusCode} {(statusCode == 200 ? "OK" : "ERROR")}")
            .AppendLine("Content-Type: application/json")
            .AppendLine($"Content-Length: {Encoding.UTF8.GetByteCount(responseBody)}")
            .AppendLine("Connection: close")
            .AppendLine()
            .Append(responseBody)
            .ToString();

        var responseBytes = Encoding.UTF8.GetBytes(responseText);
        await stream.WriteAsync(responseBytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}

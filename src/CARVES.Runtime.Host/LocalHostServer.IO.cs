using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostServer
{
    private static T ReadJson<T>(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
        var body = reader.ReadToEnd();
        return JsonSerializer.Deserialize<T>(body, JsonOptions)
            ?? throw new InvalidOperationException("Host request body was empty.");
    }

    private static IReadOnlyDictionary<string, string> ReadForm(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
        var body = reader.ReadToEnd();
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = pair.IndexOf('=');
            var key = separatorIndex >= 0 ? pair[..separatorIndex] : pair;
            var value = separatorIndex >= 0 ? pair[(separatorIndex + 1)..] : string.Empty;
            values[WebUtility.UrlDecode(key)] = WebUtility.UrlDecode(value);
        }

        return values;
    }

    private static void WriteJson(HttpListenerResponse response, object payload)
    {
        response.ContentType = "application/json; charset=utf-8";
        using var writer = new StreamWriter(response.OutputStream, Utf8WithoutBom, leaveOpen: false);
        writer.Write(JsonSerializer.Serialize(payload, JsonOptions));
        writer.Flush();
        response.Close();
    }

    private static void WriteHtml(HttpListenerResponse response, string html)
    {
        response.ContentType = "text/html; charset=utf-8";
        using var writer = new StreamWriter(response.OutputStream, Encoding.UTF8, leaveOpen: false);
        writer.Write(html);
        writer.Flush();
        response.Close();
    }

    private void WriteAcceptedOperation(HttpListenerResponse response, string operationId)
    {
        var operation = acceptedOperationStore.TryGet(operationId);
        if (operation is null)
        {
            response.StatusCode = 404;
            WriteJson(response, new JsonObject { ["error"] = $"Accepted host operation '{operationId}' was not found." });
            return;
        }

        WriteJson(response, operation);
    }
}

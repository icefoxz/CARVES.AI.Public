using System.Text;

namespace Carves.Runtime.Infrastructure.AI;

internal static class HttpContentSyncReader
{
    public static string ReadAsString(HttpContent content)
    {
        using var stream = content.ReadAsStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}

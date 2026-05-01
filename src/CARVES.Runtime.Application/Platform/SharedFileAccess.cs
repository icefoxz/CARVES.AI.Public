using System.Text;

namespace Carves.Runtime.Application.Platform;

public static class SharedFileAccess
{
    private static readonly FileShare SharedReadModes = FileShare.ReadWrite | FileShare.Delete;

    public static string ReadAllText(string path)
    {
        using var stream = OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public static FileStream OpenRead(string path)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, SharedReadModes);
    }
}

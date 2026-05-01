namespace Carves.Runtime.Infrastructure.Persistence;

internal static class AtomicFileWriter
{
    public static void WriteAllText(string path, string content)
    {
        WriteAllTextInternal(path, content, onlyIfChanged: false);
    }

    public static bool WriteAllTextIfChanged(string path, string content)
    {
        return WriteAllTextInternal(path, content, onlyIfChanged: true);
    }

    private static bool WriteAllTextInternal(string path, string content, bool onlyIfChanged)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException($"Path '{path}' has no parent directory.");
        Directory.CreateDirectory(directory);

        if (onlyIfChanged && File.Exists(path))
        {
            var existing = File.ReadAllText(path);
            if (string.Equals(existing, content, StringComparison.Ordinal))
            {
                return false;
            }
        }

        var temporaryPath = Path.Combine(directory, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temporaryPath, content);
        File.Move(temporaryPath, path, overwrite: true);
        return true;
    }
}

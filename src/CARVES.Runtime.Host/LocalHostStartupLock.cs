namespace Carves.Runtime.Host;

internal sealed class LocalHostStartupLock : IDisposable
{
    private readonly FileStream stream;
    private readonly string path;

    private LocalHostStartupLock(string path, FileStream stream)
    {
        this.path = path;
        this.stream = stream;
    }

    public string Path => path;

    public static LocalHostStartupLock? TryAcquire(string repoRoot)
    {
        var lockPath = LocalHostPaths.GetStartupLockPath(repoRoot);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(lockPath)!);

        try
        {
            var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            using var writer = new StreamWriter(stream, leaveOpen: true);
            stream.SetLength(0);
            writer.WriteLine($"pid={Environment.ProcessId}");
            writer.WriteLine($"acquired_at={DateTimeOffset.UtcNow:O}");
            writer.Flush();
            stream.Flush(flushToDisk: true);
            return new LocalHostStartupLock(lockPath, stream);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        try
        {
            stream.Dispose();
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }
    }
}

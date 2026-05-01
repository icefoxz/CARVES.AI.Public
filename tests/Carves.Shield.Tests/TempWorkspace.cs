namespace Carves.Shield.Tests;

internal sealed class TempWorkspace : IDisposable
{
    private TempWorkspace(string rootPath)
    {
        RootPath = rootPath;
    }

    public string RootPath { get; }

    public static TempWorkspace Create()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "carves-shield-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        return new TempWorkspace(rootPath);
    }

    public void WriteFile(string relativePath, string contents)
    {
        var path = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, contents);
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}

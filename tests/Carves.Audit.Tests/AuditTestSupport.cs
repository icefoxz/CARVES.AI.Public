namespace Carves.Audit.Tests;

internal sealed class TemporaryWorkspace : IDisposable
{
    public TemporaryWorkspace()
    {
        RootPath = Path.Combine(Path.GetTempPath(), $"carves-audit-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public string WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
        catch
        {
        }
    }
}

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static string ResolveNativeRelativePath(string root, string relativePath)
    {
        var fullRoot = Path.GetFullPath(root);
        var resolved = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Path escapes root: {relativePath}", nameof(relativePath));
        }

        return resolved;
    }

    private static string TruncateForJson(string value, int maxLength = 4000)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...<truncated>";
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

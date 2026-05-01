namespace Carves.Matrix.Core;

public static partial class MatrixArtifactManifestWriter
{
    private static string ResolveRelativePath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new ArgumentException("Matrix artifact root is required.", nameof(root));
        }

        if (string.IsNullOrWhiteSpace(relativePath) || IsRootedPathText(relativePath))
        {
            throw new ArgumentException("Matrix artifact manifest paths must be relative.", nameof(relativePath));
        }

        var normalizedRelativePath = NormalizeRelativePath(relativePath);
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relativeToRoot = Path.GetRelativePath(fullRoot, fullPath);
        if (PathEscapesRoot(relativeToRoot))
        {
            throw new ArgumentException($"Matrix artifact manifest path escapes artifact root: {relativePath}", nameof(relativePath));
        }

        return fullPath;
    }

    private static string NormalizeRelativePath(string path)
    {
        if (IsRootedPathText(path))
        {
            throw new ArgumentException("Matrix artifact manifest paths must be relative.", nameof(path));
        }

        return path.Replace('\\', '/');
    }

    private static bool IsRootedPathText(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path[0] is '/' or '\\'
               || path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':'
               || Path.IsPathRooted(path);
    }

    private static bool PathEscapesRoot(string relativeToRoot)
    {
        if (Path.IsPathRooted(relativeToRoot))
        {
            return true;
        }

        var normalized = relativeToRoot.Replace('\\', '/');
        return string.Equals(normalized, "..", StringComparison.Ordinal)
               || normalized.StartsWith("../", StringComparison.Ordinal);
    }

    internal static bool IsReparsePointOrSymbolicLink(string fullPath)
    {
        var attributes = File.GetAttributes(fullPath);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            return true;
        }

        try
        {
            return File.ResolveLinkTarget(fullPath, returnFinalTarget: false) is not null;
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or PlatformNotSupportedException)
        {
            return false;
        }
    }
}

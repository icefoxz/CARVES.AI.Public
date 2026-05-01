namespace Carves.Runtime.Application.Platform;

public static class RuntimeCliWrapperPaths
{
    public const string UnixWrapperFileName = "carves";
    public const string PowerShellWrapperFileName = "carves.ps1";
    public const string CmdWrapperFileName = "carves.cmd";
    public const string PublishedCliDirectoryName = "runtime-cli";
    public const string PublishedCliAssemblyFileName = "carves.dll";
    public const string PublishedCliManifestEntry = "runtime-cli/carves.dll";

    public static string PreferredWrapperFileName => OperatingSystem.IsWindows()
        ? PowerShellWrapperFileName
        : UnixWrapperFileName;

    public static string ShellBlockLanguage => OperatingSystem.IsWindows()
        ? "powershell"
        : "bash";

    public static string PreferredWrapperPath(string runtimeRoot)
    {
        return Path.Combine(runtimeRoot, PreferredWrapperFileName);
    }

    public static string PowerShellWrapperPath(string runtimeRoot)
    {
        return Path.Combine(runtimeRoot, PowerShellWrapperFileName);
    }

    public static string CmdWrapperPath(string runtimeRoot)
    {
        return Path.Combine(runtimeRoot, CmdWrapperFileName);
    }

    public static bool HasPreferredWrapper(string runtimeRoot)
    {
        return File.Exists(PreferredWrapperPath(runtimeRoot));
    }

    public static bool HasAnyWrapper(string runtimeRoot)
    {
        return File.Exists(PreferredWrapperPath(runtimeRoot))
               || File.Exists(PowerShellWrapperPath(runtimeRoot))
               || File.Exists(CmdWrapperPath(runtimeRoot));
    }

    public static string PublishedCliAssemblyPath(string runtimeRoot)
    {
        return Path.Combine(runtimeRoot, PublishedCliDirectoryName, PublishedCliAssemblyFileName);
    }

    public static bool HasPublishedCli(string runtimeRoot)
    {
        return File.Exists(PublishedCliAssemblyPath(runtimeRoot));
    }

    public static string FormatShellCommand(string wrapperPath, params string[] arguments)
    {
        var suffix = arguments.Length == 0
            ? string.Empty
            : $" {string.Join(' ', arguments)}";
        return OperatingSystem.IsWindows()
            ? $"& \"{wrapperPath}\"{suffix}"
            : $"\"{wrapperPath}\"{suffix}";
    }

    public static string FormatCommandPattern(string wrapperPath)
    {
        return OperatingSystem.IsWindows()
            ? $"& \"{wrapperPath}\" <command> [args]"
            : $"\"{wrapperPath}\" <command> [args]";
    }
}

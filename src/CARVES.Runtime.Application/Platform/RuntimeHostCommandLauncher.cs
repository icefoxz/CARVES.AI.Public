namespace Carves.Runtime.Application.Platform;

public static class RuntimeHostCommandLauncher
{
    public const string SupportedColdLauncherScriptPath = "./scripts/carves-host.ps1";

    public static string Cold(params string[] arguments)
    {
        var command = new List<string>
        {
            "powershell",
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            SupportedColdLauncherScriptPath,
        };
        command.AddRange(arguments);
        return string.Join(' ', command.Select(Quote));
    }

    private static string Quote(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "\"\"";
        }

        return value.IndexOfAny([' ', '\t', '"']) >= 0
            ? $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : value;
    }
}

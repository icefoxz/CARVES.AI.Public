using System.IO.Compression;
using System.Text.Json.Nodes;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private const string PortableScorerManifestSchemaVersion = "carves-portable-scorer.v0";
    private const string PortableScorerRootManifestSchemaVersion = "carves-windows-scorer-root.v0";
    private const string WindowsPlayableScorerRootManifestFileName = "scorer-root-manifest.json";
    private const string WindowsPlayableRuntimeIdentifier = "win-x64";
    private const string WindowsPlayableEntrypoint = "tools/carves/carves.exe";
    private const string WindowsPlayableScorerManifestPath = "tools/carves/scorer-manifest.json";

    private sealed record PortableScorerBundleResult(
        string ZipPath,
        string ManifestPath,
        string Entrypoint,
        string RuntimeIdentifier,
        string BuildLabel);

    private static void ValidateWindowsPlayableScorerOptions(string packageRoot, MatrixTrialOptions options)
    {
        if (!options.WindowsPlayable)
        {
            return;
        }

        var scorerRoot = Path.GetFullPath(options.ScorerRoot!);
        if (!Directory.Exists(scorerRoot))
        {
            throw new InvalidOperationException($"Playable scorer root not found: {scorerRoot}");
        }

        var entrypoint = Path.Combine(scorerRoot, "carves.exe");
        if (!File.Exists(entrypoint))
        {
            throw new InvalidOperationException($"Playable scorer entrypoint missing: {entrypoint}");
        }

        var runtimeIdentifier = string.IsNullOrWhiteSpace(options.RuntimeIdentifier)
            ? WindowsPlayableRuntimeIdentifier
            : options.RuntimeIdentifier;
        ValidateWindowsPlayableScorerRootManifest(scorerRoot, runtimeIdentifier!);

        var zipPath = ResolveWindowsPlayableZipPath(packageRoot, options);
        if (IsZipPathInsidePackageRoot(zipPath, packageRoot))
        {
            throw new InvalidOperationException("Playable zip output must be outside the package root.");
        }

        if (Directory.Exists(zipPath))
        {
            throw new InvalidOperationException($"Playable zip output is a directory: {zipPath}");
        }

        if (File.Exists(zipPath) && !options.Force)
        {
            throw new InvalidOperationException($"Playable zip output already exists: {zipPath}");
        }
    }

    private static PortableScorerBundleResult? AssembleWindowsPlayablePackage(
        string packageRoot,
        MatrixTrialOptions options,
        DateTimeOffset createdAt)
    {
        if (!options.WindowsPlayable)
        {
            return null;
        }

        var runtimeIdentifier = string.IsNullOrWhiteSpace(options.RuntimeIdentifier)
            ? WindowsPlayableRuntimeIdentifier
            : options.RuntimeIdentifier;
        var buildLabel = string.IsNullOrWhiteSpace(options.BuildLabel)
            ? "local"
            : options.BuildLabel;
        var carvesVersion = string.IsNullOrWhiteSpace(options.CarvesVersion)
            ? AgentTrialVersionContract.RequiredCarvesMinimumVersion
            : options.CarvesVersion;

        var scorerSourceRoot = Path.GetFullPath(options.ScorerRoot!);
        var scorerDestinationRoot = Path.Combine(packageRoot, "tools", "carves");
        CopyWindowsPlayableScorerFiles(scorerSourceRoot, scorerDestinationRoot);

        var manifestPath = Path.Combine(packageRoot, WindowsPlayableScorerManifestPath.Replace('/', Path.DirectorySeparatorChar));
        WritePortableScorerManifest(
            manifestPath,
            packageRoot,
            scorerDestinationRoot,
            createdAt,
            runtimeIdentifier!,
            carvesVersion!,
            buildLabel!);

        var zipPath = ResolveWindowsPlayableZipPath(packageRoot, options);
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        var zipDirectory = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrWhiteSpace(zipDirectory))
        {
            Directory.CreateDirectory(zipDirectory);
        }

        ZipFile.CreateFromDirectory(packageRoot, zipPath, CompressionLevel.Fastest, includeBaseDirectory: false);

        return new PortableScorerBundleResult(
            zipPath,
            manifestPath,
            WindowsPlayableEntrypoint,
            runtimeIdentifier!,
            buildLabel!);
    }

    private static void CopyWindowsPlayableScorerFiles(string sourceRoot, string destinationRoot)
    {
        Directory.CreateDirectory(destinationRoot);
        foreach (var sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, sourcePath);
            if (string.Equals(relativePath.Replace('\\', '/'), "scorer-manifest.json", StringComparison.Ordinal))
            {
                continue;
            }

            var destinationPath = Path.Combine(destinationRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwrite: true);
            TryPreserveUnixFileMode(sourcePath, destinationPath);
        }
    }

    private static void TryPreserveUnixFileMode(string sourcePath, string destinationPath)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(destinationPath, File.GetUnixFileMode(sourcePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
        }
    }

    private static void WritePortableScorerManifest(
        string manifestPath,
        string packageRoot,
        string scorerDestinationRoot,
        DateTimeOffset createdAt,
        string runtimeIdentifier,
        string carvesVersion,
        string buildLabel)
    {
        AgentTrialLocalJson.WriteObject(manifestPath, new JsonObject
        {
            ["schema_version"] = PortableScorerManifestSchemaVersion,
            ["created_at"] = createdAt.ToString("O"),
            ["scorer_kind"] = "runtime_cli",
            ["runtime_identifier"] = runtimeIdentifier,
            ["entrypoint"] = WindowsPlayableEntrypoint,
            ["carves_version"] = carvesVersion,
            ["build_label"] = buildLabel,
            ["supported_commands"] = ToJsonArray(["test collect", "test reset", "test verify", "test result"]),
            ["self_contained"] = true,
            ["requires_source_checkout_to_run"] = false,
            ["requires_dotnet_to_run"] = false,
            ["uses_dotnet_run"] = false,
            ["scorer_root_manifest"] = File.Exists(Path.Combine(scorerDestinationRoot, WindowsPlayableScorerRootManifestFileName))
                ? "tools/carves/scorer-root-manifest.json"
                : null,
            ["local_only"] = true,
            ["server_submission"] = false,
            ["certification"] = false,
            ["leaderboard_eligible"] = false,
            ["non_claims"] = ToJsonArray([
                "not_tamper_proof_signature",
                "not_certification",
                "not_server_receipt",
                "not_leaderboard_proof",
                "not_producer_identity",
                "not_anti_cheat",
                "not_os_sandbox"]),
            ["file_hashes"] = new JsonArray(ReadPortableScorerFileHashes(packageRoot, scorerDestinationRoot)
                .Select(ToJson)
                .ToArray<JsonNode?>()),
            ["file_hashes_unavailable_reason"] = null
        });
    }

    private static IReadOnlyList<PortableScorerFileHash> ReadPortableScorerFileHashes(
        string packageRoot,
        string scorerDestinationRoot)
    {
        return Directory.EnumerateFiles(scorerDestinationRoot, "*", SearchOption.AllDirectories)
            .Where(path => !string.Equals(
                ToPackageRelativePath(packageRoot, path),
                WindowsPlayableScorerManifestPath,
                StringComparison.Ordinal))
            .Select(path => new PortableScorerFileHash(
                ToPackageRelativePath(packageRoot, path),
                new FileInfo(path).Length,
                AgentTrialLocalJson.HashFile(path)))
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidateWindowsPlayableScorerRootManifest(string scorerRoot, string runtimeIdentifier)
    {
        var manifestPath = Path.Combine(scorerRoot, WindowsPlayableScorerRootManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException($"Playable scorer root manifest missing: {manifestPath}");
        }

        JsonObject root;
        try
        {
            root = AgentTrialLocalJson.ReadObject(manifestPath);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or FormatException or System.Text.Json.JsonException)
        {
            throw new InvalidOperationException($"Playable scorer root manifest invalid: {manifestPath}", ex);
        }

        RequireScorerRootString(root, "schema_version", PortableScorerRootManifestSchemaVersion, manifestPath);
        RequireScorerRootString(root, "runtime_identifier", runtimeIdentifier, manifestPath);
        RequireScorerRootString(root, "entrypoint", "carves.exe", manifestPath);
        RequireScorerRootString(root, "target_project", "src/CARVES.Runtime.Cli/carves.csproj", manifestPath);
        RequireScorerRootBoolean(root, "self_contained", expected: true, manifestPath);
        RequireScorerRootBoolean(root, "requires_source_checkout_to_run", expected: false, manifestPath);
        RequireScorerRootBoolean(root, "requires_dotnet_to_run", expected: false, manifestPath);
        RequireScorerRootBoolean(root, "uses_dotnet_run", expected: false, manifestPath);

        var supportedCommands = AgentTrialLocalJson.GetStringArray(root, "supported_commands");
        if (!supportedCommands.Contains("test collect", StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Playable scorer root manifest does not support test collect: {manifestPath}");
        }

        if (!supportedCommands.Contains("test reset", StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Playable scorer root manifest does not support test reset: {manifestPath}");
        }

        if (!supportedCommands.Contains("test verify", StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Playable scorer root manifest does not support test verify: {manifestPath}");
        }

        if (!supportedCommands.Contains("test result", StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Playable scorer root manifest does not support test result: {manifestPath}");
        }
    }

    private static void RequireScorerRootString(JsonObject root, string propertyName, string expected, string manifestPath)
    {
        var actual = AgentTrialLocalJson.GetRequiredString(root, propertyName);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Playable scorer root manifest {propertyName} must be '{expected}': {manifestPath}");
        }
    }

    private static void RequireScorerRootBoolean(JsonObject root, string propertyName, bool expected, string manifestPath)
    {
        var actual = AgentTrialLocalJson.GetBooleanOrDefault(root, propertyName, defaultValue: !expected);
        if (actual != expected)
        {
            throw new InvalidOperationException($"Playable scorer root manifest {propertyName} must be {expected.ToString().ToLowerInvariant()}: {manifestPath}");
        }
    }

    private static JsonObject ToJson(PortableScorerFileHash file)
    {
        return new JsonObject
        {
            ["path"] = file.Path,
            ["size"] = file.Size,
            ["sha256"] = file.Sha256
        };
    }

    private static string ResolveWindowsPlayableZipPath(string packageRoot, MatrixTrialOptions options)
    {
        return Path.GetFullPath(string.IsNullOrWhiteSpace(options.ZipOutput)
            ? packageRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".zip"
            : options.ZipOutput);
    }

    private static bool IsZipPathInsidePackageRoot(string path, string root)
    {
        var relativePath = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        return !relativePath.StartsWith("..", StringComparison.Ordinal)
            && !Path.IsPathRooted(relativePath);
    }

    private sealed record PortableScorerFileHash(string Path, long Size, string Sha256);
}

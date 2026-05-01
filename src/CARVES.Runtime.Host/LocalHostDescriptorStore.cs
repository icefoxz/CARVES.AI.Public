using System.Text.Json;
using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Host;

internal static class LocalHostDescriptorStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public static LocalHostDescriptor? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonSerializer.Deserialize<LocalHostDescriptor>(SharedFileAccess.ReadAllText(path), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static void WriteActiveDescriptors(string repoRoot, LocalHostDescriptor descriptor)
    {
        WriteDescriptorFile(LocalHostPaths.GetDescriptorPath(repoRoot), descriptor);
        WriteDescriptorFile(LocalHostPaths.GetMachineDescriptorPath(), descriptor);
    }

    public static void WriteGenerationDescriptor(string repoRoot, LocalHostDescriptor descriptor)
    {
        WriteDescriptorFile(
            LocalHostPaths.GetGenerationDescriptorPath(repoRoot, descriptor.StartedAt, descriptor.ProcessId, descriptor.Port),
            descriptor);
    }

    public static IReadOnlyList<LocalHostDescriptor> ReadGenerationDescriptors(string repoRoot)
    {
        var directory = LocalHostPaths.GetGenerationDescriptorsDirectory(repoRoot);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(TryRead)
            .Where(static descriptor => descriptor is not null)
            .Cast<LocalHostDescriptor>()
            .OrderByDescending(static descriptor => descriptor.StartedAt)
            .ToArray();
    }

    public static void TryDeleteMatchingActiveDescriptors(string repoRoot, LocalHostDescriptor descriptor)
    {
        TryDeleteMatchingDescriptor(LocalHostPaths.GetDescriptorPath(repoRoot), descriptor);
        TryDeleteMatchingDescriptor(LocalHostPaths.GetMachineDescriptorPath(), descriptor);
    }

    public static void TryDeleteMatchingGenerationDescriptors(string repoRoot, LocalHostDescriptor descriptor)
    {
        var directory = LocalHostPaths.GetGenerationDescriptorsDirectory(repoRoot);
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            TryDeleteMatchingDescriptor(path, descriptor);
        }
    }

    private static void WriteDescriptorFile(string path, LocalHostDescriptor descriptor)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WriteAtomically(path, JsonSerializer.Serialize(descriptor, JsonOptions));
    }

    private static void WriteAtomically(string path, string content)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException($"Path '{path}' has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temporaryPath, content);
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static void TryDeleteMatchingDescriptor(string path, LocalHostDescriptor descriptor)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var persistedDescriptor = TryRead(path);
            if (persistedDescriptor is not null
                && string.Equals(persistedDescriptor.HostId, descriptor.HostId, StringComparison.Ordinal)
                && persistedDescriptor.ProcessId == descriptor.ProcessId
                && persistedDescriptor.StartedAt.Equals(descriptor.StartedAt))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}

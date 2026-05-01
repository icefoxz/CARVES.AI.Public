using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RepoRegistryService
{
    private readonly IRepoRegistryRepository repository;

    public RepoRegistryService(IRepoRegistryRepository repository)
    {
        this.repository = repository;
    }

    public RepoRegistry GetRegistry()
    {
        return repository.Load();
    }

    public IReadOnlyList<RepoDescriptor> List()
    {
        return repository.Load().Items
            .Select(item => WithStage(item, RuntimeStageReader.TryRead(item.RepoPath) ?? item.Stage))
            .OrderBy(item => item.RepoId, StringComparer.Ordinal)
            .ToArray();

        static RepoDescriptor WithStage(RepoDescriptor item, string stage)
        {
            return new RepoDescriptor
            {
                SchemaVersion = item.SchemaVersion,
                RepoId = item.RepoId,
                RepoPath = item.RepoPath,
                Stage = stage,
                RuntimeEnabled = item.RuntimeEnabled,
                ProviderProfile = item.ProviderProfile,
                PolicyProfile = item.PolicyProfile,
                RegisteredAt = item.RegisteredAt,
                UpdatedAt = item.UpdatedAt,
            };
        }
    }

    public RepoDescriptor Inspect(string repoId)
    {
        return List().First(item => string.Equals(item.RepoId, repoId, StringComparison.Ordinal));
    }

    public RepoDescriptor Register(
        string repoPath,
        string? repoId = null,
        string? providerProfile = null,
        string? policyProfile = null)
    {
        var root = Path.GetFullPath(repoPath);
        if (!Directory.Exists(root))
        {
            throw new InvalidOperationException($"Repo path '{root}' does not exist.");
        }

        var descriptor = new RepoDescriptor
        {
            RepoId = string.IsNullOrWhiteSpace(repoId) ? Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) : repoId,
            RepoPath = root,
            Stage = RuntimeStageReader.TryRead(root) ?? RuntimeStageInfo.CurrentStage,
            RuntimeEnabled = true,
            ProviderProfile = providerProfile ?? "default",
            PolicyProfile = policyProfile ?? "balanced",
        };

        var registry = repository.Load();
        var items = registry.Items.Where(item => !string.Equals(item.RepoId, descriptor.RepoId, StringComparison.Ordinal)).ToList();
        items.Add(descriptor);
        repository.Save(new RepoRegistry
        {
            Items = items.OrderBy(item => item.RepoId, StringComparer.Ordinal).ToArray(),
        });
        return descriptor;
    }

    public RepoDescriptor Update(RepoDescriptor descriptor)
    {
        descriptor.Touch();
        var registry = repository.Load();
        var items = registry.Items.Where(item => !string.Equals(item.RepoId, descriptor.RepoId, StringComparison.Ordinal)).ToList();
        items.Add(descriptor);
        repository.Save(new RepoRegistry
        {
            Items = items.OrderBy(item => item.RepoId, StringComparer.Ordinal).ToArray(),
        });
        return descriptor;
    }
}

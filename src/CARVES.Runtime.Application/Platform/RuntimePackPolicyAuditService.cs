using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimePackPolicyAuditService
{
    private const int SurfaceLimit = 12;
    private readonly IRuntimeArtifactRepository artifactRepository;

    public RuntimePackPolicyAuditService(IRuntimeArtifactRepository artifactRepository)
    {
        this.artifactRepository = artifactRepository;
    }

    public RuntimePackPolicyAuditSurface Build()
    {
        var entries = artifactRepository.LoadRuntimePackPolicyAuditEntries(SurfaceLimit);
        return new RuntimePackPolicyAuditSurface
        {
            Entries = entries,
            Summary = entries.Count == 0
                ? "No runtime-local pack policy audit evidence has been recorded yet."
                : $"Runtime-local pack policy audit tracks {entries.Count} recent event(s); latest={entries[0].EventKind}.",
            Notes =
            [
                "Policy audit stays local-runtime scoped and append-only.",
                "Entries describe export, import, pin, clear, and related resulting state references.",
                "Registry, rollout, and automatic remediation remain closed."
            ],
        };
    }
}

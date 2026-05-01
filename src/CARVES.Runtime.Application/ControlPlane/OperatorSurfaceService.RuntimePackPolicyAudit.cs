using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimePackPolicyAudit()
    {
        return FormatRuntimePackPolicyAudit(CreateRuntimePackPolicyAuditService().Build());
    }

    public OperatorCommandResult ApiRuntimePackPolicyAudit()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimePackPolicyAuditService().Build()));
    }

    private RuntimePackPolicyAuditService CreateRuntimePackPolicyAuditService()
    {
        return new RuntimePackPolicyAuditService(artifactRepository);
    }

    private static OperatorCommandResult FormatRuntimePackPolicyAudit(RuntimePackPolicyAuditSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime pack policy audit",
            surface.Summary,
        };

        if (surface.Entries.Count == 0)
        {
            lines.Add("Entries: none");
            return OperatorCommandResult.Success(lines.ToArray());
        }

        lines.Add("Recent entries:");
        foreach (var entry in surface.Entries.Take(6))
        {
            lines.Add($"- {entry.EventKind}: admission_policy={entry.ResultingAdmissionPolicyId ?? "(none)"}; switch_policy={entry.ResultingSwitchPolicyId ?? "(none)"}; package={entry.PackageId ?? "(none)"}");
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}

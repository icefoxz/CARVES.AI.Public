using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostSurfaceService
{
    private static JsonObject BuildAcceptanceContractGateNode(TaskNode task)
    {
        return JsonSerializer.SerializeToNode(
                AcceptanceContractExecutionGate.Evaluate(task),
                JsonOptions)
            ?.AsObject()
            ?? new JsonObject();
    }
}

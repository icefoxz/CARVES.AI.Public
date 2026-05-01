using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostSurfaceService
{
    public SessionGatewaySessionSurface CreateSessionGatewaySession(SessionGatewaySessionCreateRequest request)
    {
        return services.RuntimeSessionGatewayService.CreateOrResumeSession(request);
    }

    public SessionGatewaySessionSurface? TryGetSessionGatewaySession(string sessionId)
    {
        return services.RuntimeSessionGatewayService.TryGetSession(sessionId);
    }

    public SessionGatewayTurnSurface SubmitSessionGatewayMessage(string sessionId, SessionGatewayMessageRequest request)
    {
        var surface = services.RuntimeSessionGatewayService.SubmitMessage(sessionId, request);
        if (!string.IsNullOrWhiteSpace(surface.OperationId) && acceptedOperationStore is not null)
        {
            acceptedOperationStore.Accept("session-gateway-governed-run", surface.OperationId);
        }

        return surface;
    }

    public SessionGatewayEventsSurface GetSessionGatewayEvents(string sessionId)
    {
        return services.RuntimeSessionGatewayService.GetEvents(sessionId);
    }
}

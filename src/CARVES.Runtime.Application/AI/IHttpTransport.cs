namespace Carves.Runtime.Application.AI;

public interface IHttpTransport
{
    HttpTransportResponse Send(HttpTransportRequest request);
}

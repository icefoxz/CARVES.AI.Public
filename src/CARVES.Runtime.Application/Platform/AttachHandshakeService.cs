using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class AttachHandshakeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;

    public AttachHandshakeService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public string HandshakePath => Path.Combine(paths.RuntimeRoot, "attach-handshake.json");

    public AttachHandshakeRecord Persist(AttachHandshakeRequestRecord request, AttachHandshakeAcknowledgement acknowledgement)
    {
        var record = new AttachHandshakeRecord
        {
            Request = request,
            Acknowledgement = acknowledgement,
        };

        Directory.CreateDirectory(Path.GetDirectoryName(HandshakePath)!);
        File.WriteAllText(HandshakePath, JsonSerializer.Serialize(record, JsonOptions));
        return record;
    }

    public AttachHandshakeRecord? Load()
    {
        return File.Exists(HandshakePath)
            ? JsonSerializer.Deserialize<AttachHandshakeRecord>(File.ReadAllText(HandshakePath), JsonOptions)
            : null;
    }
}

namespace Carves.Runtime.Application.ControlPlane;

public sealed record MarkdownProjection(string TaskQueue, string State, string CurrentTask);

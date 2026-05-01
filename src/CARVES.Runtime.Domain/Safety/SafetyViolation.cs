namespace Carves.Runtime.Domain.Safety;

public sealed record SafetyViolation(
    string Code,
    string Message,
    string Severity = "error",
    string Validator = "",
    IReadOnlyDictionary<string, string>? Details = null);

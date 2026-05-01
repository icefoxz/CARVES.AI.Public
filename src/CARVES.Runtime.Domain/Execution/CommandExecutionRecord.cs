namespace Carves.Runtime.Domain.Execution;

public sealed record CommandExecutionRecord(
    IReadOnlyList<string> Command,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool Skipped,
    string WorkingDirectory,
    string Category,
    DateTimeOffset CapturedAt);

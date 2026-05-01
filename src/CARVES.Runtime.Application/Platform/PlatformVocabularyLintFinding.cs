namespace Carves.Runtime.Application.Platform;

public sealed record PlatformVocabularyLintFinding(
    string RuleId,
    string Severity,
    string RelativePath,
    string SymbolName,
    string Message);

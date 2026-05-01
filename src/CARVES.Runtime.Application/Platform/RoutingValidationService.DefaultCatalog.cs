using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RoutingValidationService
{
    private static RoutingValidationCatalog CreateDefaultCatalog()
    {
        return new RoutingValidationCatalog
        {
            Tasks =
            [
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-FS-001",
                    TaskType = "failure-summary",
                    RoutingIntent = "failure_summary",
                    ModuleId = "Execution/Failures",
                    Prompt = "Summarize this substrate failure for an operator in exactly three bullets: Worker failed after 3 retries because DOTNET_CLI_HOME was not writable.",
                    ExpectedFormat = ModelQualificationExpectedFormat.Text,
                    RiskLevel = "low",
                    BaselineLaneId = "n1n-responses",
                    Summary = "Substrate failure-summary sample.",
                },
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-FS-002",
                    TaskType = "failure-summary",
                    RoutingIntent = "failure_summary",
                    ModuleId = "Execution/Failures",
                    Prompt = "Summarize this semantic failure for an operator in exactly three bullets: Worker patch caused a contract mismatch in ResultEnvelope serialization tests.",
                    ExpectedFormat = ModelQualificationExpectedFormat.Text,
                    RiskLevel = "low",
                    BaselineLaneId = "n1n-responses",
                    Summary = "Semantic failure-summary sample.",
                },
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-EN-001",
                    TaskType = "evidence-normalization",
                    RoutingIntent = "structured_output",
                    Prompt = "Return a JSON object with fields risk_level, root_cause, mitigation_steps for this incident: quota exhausted while contacting provider.",
                    ExpectedFormat = ModelQualificationExpectedFormat.Json,
                    RequiredJsonFields = ["risk_level", "root_cause", "mitigation_steps"],
                    RiskLevel = "low",
                    BaselineLaneId = "deepseek-chat",
                    Summary = "Structured evidence-normalization sample.",
                },
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-RS-001",
                    TaskType = "reasoning-summary",
                    RoutingIntent = "reasoning_summary",
                    Prompt = "Analyze why an operator control plane may accumulate stale planned replan runs after review approval, and propose a concise mitigation in exactly three bullets.",
                    ExpectedFormat = ModelQualificationExpectedFormat.Text,
                    RiskLevel = "low",
                    BaselineLaneId = "n1n-responses",
                    Summary = "Reasoning-summary sample for promotion evidence.",
                },
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-CODE-001",
                    TaskType = "code.small.fix",
                    RoutingIntent = "patch_draft",
                    ModuleId = "Execution/ResultEnvelope",
                    Prompt = "Return JSON with fields change_summary, files_touched, validation_commands, backward_compatibility_notes for a minimal C# fix that adds schemaVersion to ResultEnvelope while keeping backward compatibility. Keep files_touched limited to one or two files.",
                    ExpectedFormat = ModelQualificationExpectedFormat.Json,
                    RequiredJsonFields = ["change_summary", "files_touched", "validation_commands", "backward_compatibility_notes"],
                    RiskLevel = "medium",
                    BaselineLaneId = "n1n-responses",
                    Summary = "Very small code fix sample.",
                },
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-CODE-IMPL-001",
                    TaskType = "code.small.impl",
                    RoutingIntent = "patch_draft",
                    ModuleId = "Execution/ResultEnvelope",
                    Prompt = "Return JSON with fields change_summary, files_touched, validation_commands, backward_compatibility_notes for a minimal C# implementation patch that introduces a tiny helper or field in ResultEnvelope without changing more than two files.",
                    ExpectedFormat = ModelQualificationExpectedFormat.Json,
                    RequiredJsonFields = ["change_summary", "files_touched", "validation_commands", "backward_compatibility_notes"],
                    RiskLevel = "medium",
                    BaselineLaneId = "n1n-responses",
                    Summary = "Very small code implementation sample.",
                },
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-CODE-TEST-001",
                    TaskType = "code.small.test",
                    RoutingIntent = "patch_draft",
                    ModuleId = "Execution/ResultEnvelope",
                    Prompt = "Return JSON with fields change_summary, files_touched, validation_commands, backward_compatibility_notes for a minimal C# test-only patch that adds one focused unit test for ResultEnvelope behavior without touching more than two files.",
                    ExpectedFormat = ModelQualificationExpectedFormat.Json,
                    RequiredJsonFields = ["change_summary", "files_touched", "validation_commands", "backward_compatibility_notes"],
                    RiskLevel = "low",
                    BaselineLaneId = "n1n-responses",
                    Summary = "Very small code test sample.",
                },
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-CODEX-FS-001",
                    TaskType = "failure-summary",
                    RoutingIntent = "failure_summary",
                    ModuleId = "Execution/Failures",
                    Prompt = "Summarize this substrate failure for an operator in exactly three bullets: Worker failed after 3 retries because DOTNET_CLI_HOME was not writable.",
                    ExpectedFormat = ModelQualificationExpectedFormat.Text,
                    RiskLevel = "low",
                    BaselineLaneId = "codex-sdk-worker",
                    Summary = "Codex-comparable failure-summary sample for codex_sdk lane.",
                },
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-CODEX-CLI-FS-001",
                    TaskType = "failure-summary",
                    RoutingIntent = "failure_summary",
                    ModuleId = "Execution/Failures",
                    Prompt = "Summarize this semantic failure for an operator in exactly three bullets: Worker patch caused a contract mismatch in ResultEnvelope serialization tests.",
                    ExpectedFormat = ModelQualificationExpectedFormat.Text,
                    RiskLevel = "low",
                    BaselineLaneId = "codex-cli-worker",
                    Summary = "Codex-comparable failure-summary sample for codex_cli lane.",
                },
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-CODEX-CODE-001",
                    TaskType = "code.small.fix",
                    RoutingIntent = "patch_draft",
                    ModuleId = "Execution/ResultEnvelope",
                    Prompt = "Return JSON with fields change_summary, files_touched, validation_commands, backward_compatibility_notes for a minimal C# fix that adds schemaVersion to ResultEnvelope while keeping backward compatibility. Keep files_touched limited to one or two files.",
                    ExpectedFormat = ModelQualificationExpectedFormat.Json,
                    RequiredJsonFields = ["change_summary", "files_touched", "validation_commands", "backward_compatibility_notes"],
                    RiskLevel = "medium",
                    BaselineLaneId = "codex-sdk-worker",
                    Summary = "Codex-comparable code.small.fix sample for codex_sdk lane.",
                },
                new RoutingValidationTaskDefinition
                {
                    TaskId = "VAL-CODEX-CLI-CODE-001",
                    TaskType = "code.small.impl",
                    RoutingIntent = "patch_draft",
                    ModuleId = "Execution/ResultEnvelope",
                    Prompt = "Return JSON with fields change_summary, files_touched, validation_commands, backward_compatibility_notes for a minimal C# implementation patch that introduces a tiny helper or field in ResultEnvelope without changing more than two files.",
                    ExpectedFormat = ModelQualificationExpectedFormat.Json,
                    RequiredJsonFields = ["change_summary", "files_touched", "validation_commands", "backward_compatibility_notes"],
                    RiskLevel = "medium",
                    BaselineLaneId = "codex-cli-worker",
                    Summary = "Codex-comparable code.small.impl sample for codex_cli lane.",
                },
            ],
        };
    }

    private static RoutingValidationCatalog MergeCatalog(
        RoutingValidationCatalog existing,
        RoutingValidationCatalog desired)
    {
        var taskById = existing.Tasks.ToDictionary(task => task.TaskId, StringComparer.Ordinal);
        foreach (var desiredTask in desired.Tasks)
        {
            taskById[desiredTask.TaskId] = desiredTask;
        }

        return new RoutingValidationCatalog
        {
            CatalogId = desired.CatalogId,
            CreatedAt = existing.CreatedAt,
            Tasks = taskById.Values
                .OrderBy(task => task.TaskId, StringComparer.Ordinal)
                .ToArray(),
        };
    }
}

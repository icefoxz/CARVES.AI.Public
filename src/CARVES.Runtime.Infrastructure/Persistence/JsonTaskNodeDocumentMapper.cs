using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Infrastructure.Persistence;

internal static class JsonTaskNodeDocumentMapper
{
    public static TaskNodeDocument ToDocument(TaskNode task)
    {
        return new TaskNodeDocument
        {
            SchemaVersion = RuntimeProtocol.TaskNodeSchemaVersion,
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            Status = ToSnakeCase(task.Status.ToString()),
            TaskType = ToSnakeCase(task.TaskType.ToString()),
            Priority = task.Priority,
            Source = task.Source,
            CardId = task.CardId,
            ProposalSource = task.ProposalSource == TaskProposalSource.None ? null : ToSnakeCase(task.ProposalSource.ToString()),
            ProposalReason = task.ProposalReason,
            ProposalConfidence = task.ProposalConfidence,
            ProposalPriorityHint = task.ProposalPriorityHint,
            BaseCommit = task.BaseCommit,
            ResultCommit = task.ResultCommit,
            Dependencies = task.Dependencies.ToArray(),
            Scope = task.Scope.ToArray(),
            Acceptance = task.Acceptance.ToArray(),
            Constraints = task.Constraints.ToArray(),
            AcceptanceContract = ToDocument(task.AcceptanceContract),
            Validation = new ValidationPlanDocument
            {
                Commands = task.Validation.Commands.Select(command => command.ToArray()).ToList(),
                Checks = task.Validation.Checks.ToArray(),
                ExpectedEvidence = task.Validation.ExpectedEvidence.ToArray(),
            },
            RetryCount = task.RetryCount,
            Capabilities = task.Capabilities.ToArray(),
            Metadata = task.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            LastWorkerRunId = task.LastWorkerRunId,
            LastWorkerBackend = task.LastWorkerBackend,
            LastWorkerFailureKind = task.LastWorkerFailureKind == Carves.Runtime.Domain.Execution.WorkerFailureKind.None ? null : ToSnakeCase(task.LastWorkerFailureKind.ToString()),
            LastWorkerRetryable = task.LastWorkerRetryable,
            LastWorkerSummary = task.LastWorkerSummary,
            LastWorkerDetailRef = task.LastWorkerDetailRef,
            LastProviderDetailRef = task.LastProviderDetailRef,
            LastRecoveryAction = task.LastRecoveryAction == Carves.Runtime.Domain.Execution.WorkerRecoveryAction.None ? null : ToSnakeCase(task.LastRecoveryAction.ToString()),
            LastRecoveryReason = task.LastRecoveryReason,
            RetryNotBefore = task.RetryNotBefore?.ToString("O"),
            PlannerReview = new PlannerReviewDocument
            {
                Verdict = ToSnakeCase(task.PlannerReview.Verdict.ToString()),
                Reason = task.PlannerReview.Reason,
                DecisionStatus = ToSnakeCase(task.PlannerReview.DecisionStatus.ToString()),
                AcceptanceMet = task.PlannerReview.AcceptanceMet,
                BoundaryPreserved = task.PlannerReview.BoundaryPreserved,
                ScopeDriftDetected = task.PlannerReview.ScopeDriftDetected,
                FollowUpSuggestions = task.PlannerReview.FollowUpSuggestions.ToArray(),
                DecisionDebt = task.PlannerReview.DecisionDebt is null
                    ? null
                    : new ReviewDecisionDebtDocument
                    {
                        Summary = task.PlannerReview.DecisionDebt.Summary,
                        FollowUpActions = task.PlannerReview.DecisionDebt.FollowUpActions.ToArray(),
                        RequiresFollowUpReview = task.PlannerReview.DecisionDebt.RequiresFollowUpReview,
                        RecordedAt = task.PlannerReview.DecisionDebt.RecordedAt.ToString("O"),
                    },
            },
            CreatedAt = task.CreatedAt.ToString("O"),
            UpdatedAt = task.UpdatedAt.ToString("O"),
        };
    }

    public static TaskNode ToTaskNode(TaskNodeDocument document)
    {
        if (document.SchemaVersion is not null && document.SchemaVersion != RuntimeProtocol.TaskNodeSchemaVersion)
        {
            throw new InvalidOperationException($"Unsupported task node schema version '{document.SchemaVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(document.TaskId))
        {
            throw new InvalidOperationException("Task node is missing task_id.");
        }

        if (string.IsNullOrWhiteSpace(document.Title))
        {
            throw new InvalidOperationException($"Task node '{document.TaskId}' is missing title.");
        }

        if (string.IsNullOrWhiteSpace(document.Status))
        {
            throw new InvalidOperationException($"Task node '{document.TaskId}' is missing status.");
        }

        var task = new TaskNode
        {
            TaskId = document.TaskId,
            Title = document.Title,
            Description = document.Description ?? string.Empty,
            Status = ParseEnum(document.Status, DomainTaskStatus.Pending),
            TaskType = ParseTaskType(document.TaskType),
            Priority = document.Priority ?? "P1",
            Source = document.Source ?? "HUMAN",
            CardId = document.CardId,
            ProposalSource = ParseEnum(document.ProposalSource, TaskProposalSource.None),
            ProposalReason = document.ProposalReason,
            ProposalConfidence = document.ProposalConfidence,
            ProposalPriorityHint = document.ProposalPriorityHint,
            BaseCommit = document.BaseCommit,
            Dependencies = document.Dependencies ?? Array.Empty<string>(),
            Scope = document.Scope ?? Array.Empty<string>(),
            Acceptance = document.Acceptance ?? Array.Empty<string>(),
            Constraints = document.Constraints ?? Array.Empty<string>(),
            AcceptanceContract = ToAcceptanceContract(document.AcceptanceContract),
            Validation = new ValidationPlan
            {
                Commands = document.Validation?.Commands?.Select(command => (IReadOnlyList<string>)command).ToArray() ?? Array.Empty<IReadOnlyList<string>>(),
                Checks = document.Validation?.Checks ?? Array.Empty<string>(),
                ExpectedEvidence = document.Validation?.ExpectedEvidence ?? Array.Empty<string>(),
            },
            RetryCount = document.RetryCount,
            Capabilities = document.Capabilities ?? Array.Empty<string>(),
            Metadata = document.Metadata ?? new Dictionary<string, string>(StringComparer.Ordinal),
            LastWorkerRunId = document.LastWorkerRunId,
            LastWorkerBackend = document.LastWorkerBackend,
            LastWorkerFailureKind = string.IsNullOrWhiteSpace(document.LastWorkerFailureKind)
                ? Carves.Runtime.Domain.Execution.WorkerFailureKind.None
                : ParseEnum(document.LastWorkerFailureKind, Carves.Runtime.Domain.Execution.WorkerFailureKind.None),
            LastWorkerRetryable = document.LastWorkerRetryable,
            LastWorkerSummary = document.LastWorkerSummary,
            LastWorkerDetailRef = document.LastWorkerDetailRef,
            LastProviderDetailRef = document.LastProviderDetailRef,
            LastRecoveryAction = string.IsNullOrWhiteSpace(document.LastRecoveryAction)
                ? Carves.Runtime.Domain.Execution.WorkerRecoveryAction.None
                : ParseEnum(document.LastRecoveryAction, Carves.Runtime.Domain.Execution.WorkerRecoveryAction.None),
            LastRecoveryReason = document.LastRecoveryReason,
            RetryNotBefore = ParseDate(document.RetryNotBefore),
            PlannerReview = new PlannerReview
            {
                Verdict = ParseEnum(document.PlannerReview?.Verdict, PlannerVerdict.Continue),
                Reason = document.PlannerReview?.Reason ?? string.Empty,
                DecisionStatus = ParseEnum(document.PlannerReview?.DecisionStatus, ReviewDecisionStatus.NeedsAttention),
                AcceptanceMet = document.PlannerReview?.AcceptanceMet ?? false,
                BoundaryPreserved = document.PlannerReview?.BoundaryPreserved ?? true,
                ScopeDriftDetected = document.PlannerReview?.ScopeDriftDetected ?? false,
                FollowUpSuggestions = document.PlannerReview?.FollowUpSuggestions ?? Array.Empty<string>(),
                DecisionDebt = document.PlannerReview?.DecisionDebt is null
                    ? null
                    : new ReviewDecisionDebt
                    {
                        Summary = document.PlannerReview.DecisionDebt.Summary ?? string.Empty,
                        FollowUpActions = document.PlannerReview.DecisionDebt.FollowUpActions ?? Array.Empty<string>(),
                        RequiresFollowUpReview = document.PlannerReview.DecisionDebt.RequiresFollowUpReview,
                        RecordedAt = ParseDate(document.PlannerReview.DecisionDebt.RecordedAt),
                    },
            },
            CreatedAt = ParseDate(document.CreatedAt),
            UpdatedAt = ParseDate(document.UpdatedAt),
            ResultCommit = document.ResultCommit,
        };

        if (!string.IsNullOrWhiteSpace(task.LastWorkerSummary))
        {
            task.LastWorkerDetailRef ??= PromptSafeArtifactProjectionFactory.GetWorkerExecutionDetailRef(task.TaskId);
            task.LastProviderDetailRef ??= PromptSafeArtifactProjectionFactory.GetProviderArtifactDetailRef(task.TaskId);
            task.LastWorkerSummary = PromptSafeArtifactProjectionFactory.Create(
                task.LastWorkerSummary,
                task.LastWorkerSummary,
                task.LastWorkerDetailRef).Summary;
        }

        return task;
    }

    private static AcceptanceContractDocument? ToDocument(AcceptanceContract? contract)
    {
        if (contract is null)
        {
            return null;
        }

        return new AcceptanceContractDocument
        {
            ContractId = contract.ContractId,
            Title = contract.Title,
            Status = ToSnakeCase(contract.Status.ToString()),
            Owner = contract.Owner,
            CreatedAtUtc = contract.CreatedAtUtc.ToString("O"),
            Intent = new AcceptanceContractIntentDocument
            {
                Goal = contract.Intent.Goal,
                BusinessValue = contract.Intent.BusinessValue,
            },
            AcceptanceExamples = contract.AcceptanceExamples.Select(item => new AcceptanceContractExampleDocument
            {
                Given = item.Given,
                When = item.When,
                Then = item.Then,
            }).ToArray(),
            Checks = new AcceptanceContractChecksDocument
            {
                UnitTests = contract.Checks.UnitTests.ToArray(),
                IntegrationTests = contract.Checks.IntegrationTests.ToArray(),
                RegressionTests = contract.Checks.RegressionTests.ToArray(),
                PolicyChecks = contract.Checks.PolicyChecks.ToArray(),
                AdditionalChecks = contract.Checks.AdditionalChecks.ToArray(),
            },
            Constraints = new AcceptanceContractConstraintSetDocument
            {
                MustNot = contract.Constraints.MustNot.ToArray(),
                Architecture = contract.Constraints.Architecture.ToArray(),
                ScopeLimit = contract.Constraints.ScopeLimit is null
                    ? null
                    : new AcceptanceContractScopeLimitDocument
                    {
                        MaxFilesChanged = contract.Constraints.ScopeLimit.MaxFilesChanged,
                        MaxLinesChanged = contract.Constraints.ScopeLimit.MaxLinesChanged,
                    },
            },
            NonGoals = contract.NonGoals.ToArray(),
            AutoCompleteAllowed = contract.AutoCompleteAllowed,
            EvidenceRequired = contract.EvidenceRequired.Select(item => new AcceptanceContractEvidenceRequirementDocument
            {
                Type = item.Type,
                Description = item.Description,
            }).ToArray(),
            HumanReview = new AcceptanceContractHumanReviewPolicyDocument
            {
                Required = contract.HumanReview.Required,
                ProvisionalAllowed = contract.HumanReview.ProvisionalAllowed,
                Decisions = contract.HumanReview.Decisions.Select(item => ToSnakeCase(item.ToString())).ToArray(),
            },
            Traceability = new AcceptanceContractTraceabilityDocument
            {
                SourceCardId = contract.Traceability.SourceCardId,
                SourceTaskId = contract.Traceability.SourceTaskId,
                DerivedTaskIds = contract.Traceability.DerivedTaskIds.ToArray(),
                RelatedArtifacts = contract.Traceability.RelatedArtifacts.ToArray(),
            },
        };
    }

    private static AcceptanceContract? ToAcceptanceContract(AcceptanceContractDocument? document)
    {
        if (document is null)
        {
            return null;
        }

        return new AcceptanceContract
        {
            ContractId = document.ContractId ?? string.Empty,
            Title = document.Title ?? string.Empty,
            Status = ParseEnum(document.Status, AcceptanceContractLifecycleStatus.Draft),
            Owner = document.Owner ?? "planner",
            CreatedAtUtc = ParseDate(document.CreatedAtUtc),
            Intent = new AcceptanceContractIntent
            {
                Goal = document.Intent?.Goal ?? string.Empty,
                BusinessValue = document.Intent?.BusinessValue ?? string.Empty,
            },
            AcceptanceExamples = document.AcceptanceExamples?.Select(item => new AcceptanceContractExample
            {
                Given = item.Given ?? string.Empty,
                When = item.When ?? string.Empty,
                Then = item.Then ?? string.Empty,
            }).ToArray() ?? Array.Empty<AcceptanceContractExample>(),
            Checks = new AcceptanceContractChecks
            {
                UnitTests = document.Checks?.UnitTests ?? Array.Empty<string>(),
                IntegrationTests = document.Checks?.IntegrationTests ?? Array.Empty<string>(),
                RegressionTests = document.Checks?.RegressionTests ?? Array.Empty<string>(),
                PolicyChecks = document.Checks?.PolicyChecks ?? Array.Empty<string>(),
                AdditionalChecks = document.Checks?.AdditionalChecks ?? Array.Empty<string>(),
            },
            Constraints = new AcceptanceContractConstraintSet
            {
                MustNot = document.Constraints?.MustNot ?? Array.Empty<string>(),
                Architecture = document.Constraints?.Architecture ?? Array.Empty<string>(),
                ScopeLimit = document.Constraints?.ScopeLimit is null
                    ? null
                    : new AcceptanceContractScopeLimit
                    {
                        MaxFilesChanged = document.Constraints.ScopeLimit.MaxFilesChanged,
                        MaxLinesChanged = document.Constraints.ScopeLimit.MaxLinesChanged,
                    },
            },
            NonGoals = document.NonGoals ?? Array.Empty<string>(),
            AutoCompleteAllowed = document.AutoCompleteAllowed ?? false,
            EvidenceRequired = document.EvidenceRequired?.Select(item => new AcceptanceContractEvidenceRequirement
            {
                Type = item.Type ?? string.Empty,
                Description = item.Description,
            }).ToArray() ?? Array.Empty<AcceptanceContractEvidenceRequirement>(),
            HumanReview = new AcceptanceContractHumanReviewPolicy
            {
                Required = document.HumanReview?.Required ?? true,
                ProvisionalAllowed = document.HumanReview?.ProvisionalAllowed ?? false,
                Decisions = document.HumanReview?.Decisions?.Select(item => ParseEnum(item, AcceptanceContractHumanDecision.Accept)).ToArray()
                    ?? new AcceptanceContractHumanReviewPolicy().Decisions,
            },
            Traceability = new AcceptanceContractTraceability
            {
                SourceCardId = document.Traceability?.SourceCardId,
                SourceTaskId = document.Traceability?.SourceTaskId,
                DerivedTaskIds = document.Traceability?.DerivedTaskIds ?? Array.Empty<string>(),
                RelatedArtifacts = document.Traceability?.RelatedArtifacts ?? Array.Empty<string>(),
            },
        };
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct, Enum
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var normalized = value.Replace("_", string.Empty, StringComparison.Ordinal);
            if (Enum.TryParse<TEnum>(normalized, true, out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    private static TaskType ParseTaskType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TaskType.Execution;
        }

        var normalized = value.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            "execution" => TaskType.Execution,
            "review" => TaskType.Review,
            "planning" => TaskType.Planning,
            "meta" => TaskType.Meta,
            "feature" => TaskType.Execution,
            "test" => TaskType.Execution,
            "refactor" => TaskType.Execution,
            "memoryupdate" => TaskType.Meta,
            "codegraphaudit" => TaskType.Meta,
            _ => TaskType.Execution,
        };
    }

    private static DateTimeOffset ParseDate(string? value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.UtcNow;
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var chars = new List<char>(value.Length + 4);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsUpper(character) && index > 0)
            {
                chars.Add('_');
            }

            chars.Add(char.ToLowerInvariant(character));
        }

        return new string(chars.ToArray());
    }
}

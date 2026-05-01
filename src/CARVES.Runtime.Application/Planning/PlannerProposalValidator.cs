using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Planning;

public sealed class PlannerProposalValidator
{
    public PlannerProposalValidationResult Validate(PlannerProposal proposal)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(proposal.ProposalId))
        {
            errors.Add("proposal_id is required");
        }

        if (string.IsNullOrWhiteSpace(proposal.PlannerBackend))
        {
            errors.Add("planner_backend is required");
        }

        if (string.IsNullOrWhiteSpace(proposal.GoalSummary))
        {
            errors.Add("goal_summary is required");
        }

        var tempIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var task in proposal.ProposedTasks)
        {
            if (string.IsNullOrWhiteSpace(task.TempId))
            {
                errors.Add("each proposed task must define temp_id");
                continue;
            }

            if (!tempIds.Add(task.TempId))
            {
                errors.Add($"duplicate proposed task temp_id '{task.TempId}'");
            }

            if (string.IsNullOrWhiteSpace(task.Title))
            {
                errors.Add($"proposed task '{task.TempId}' is missing title");
            }

            if (string.IsNullOrWhiteSpace(task.Description))
            {
                errors.Add($"proposed task '{task.TempId}' is missing description");
            }

            if (!TaskTypePolicy.Default.AllowPlannerGeneration(task.TaskType))
            {
                errors.Add($"proposed task '{task.TempId}' uses task type '{task.TaskType}' that planner generation does not allow");
            }

            if (task.TaskType == TaskType.Review)
            {
                errors.Add($"proposed task '{task.TempId}' cannot be of task type 'review'");
            }

            if (string.IsNullOrWhiteSpace(task.ProposalSource))
            {
                warnings.Add($"proposed task '{task.TempId}' did not include proposal_source");
            }

            if (string.IsNullOrWhiteSpace(task.ProposalReason))
            {
                warnings.Add($"proposed task '{task.TempId}' did not include proposal_reason");
            }

            if (task.ProofTarget is not null && string.IsNullOrWhiteSpace(task.ProofTarget.Description))
            {
                errors.Add($"proposed task '{task.TempId}' includes proof_target without description");
            }

            if (PlanningProofTargetMetadata.RequiresProofTarget(task.TaskType, task.Scope) && task.ProofTarget is null)
            {
                warnings.Add($"proposed task '{task.TempId}' is a scoped execution task without proof_target metadata");
            }
        }

        foreach (var dependency in proposal.Dependencies)
        {
            if (string.IsNullOrWhiteSpace(dependency.FromTaskId) || string.IsNullOrWhiteSpace(dependency.ToTaskId))
            {
                errors.Add("proposed dependencies must define from_task_id and to_task_id");
                continue;
            }

            if (!tempIds.Contains(dependency.FromTaskId) || !tempIds.Contains(dependency.ToTaskId))
            {
                errors.Add($"dependency '{dependency.FromTaskId} -> {dependency.ToTaskId}' references unknown task ids");
            }
        }

        if (proposal.Confidence < 0 || proposal.Confidence > 1)
        {
            errors.Add("confidence must be between 0 and 1");
        }

        if (proposal.RiskFlags.Contains(PlannerProposalRiskFlag.InvalidTaskType))
        {
            warnings.Add("proposal declared invalid_task_type risk");
        }

        return new PlannerProposalValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
        };
    }
}

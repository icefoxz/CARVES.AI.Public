using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Artifacts;

public interface IRuntimeArtifactRepository
{
    void SaveWorkerArtifact(TaskRunArtifact artifact);

    TaskRunArtifact? TryLoadWorkerArtifact(string taskId);

    void SaveWorkerExecutionArtifact(WorkerExecutionArtifact artifact);

    WorkerExecutionArtifact? TryLoadWorkerExecutionArtifact(string taskId);

    void SaveWorkerPermissionArtifact(WorkerPermissionArtifact artifact);

    WorkerPermissionArtifact? TryLoadWorkerPermissionArtifact(string taskId);

    IReadOnlyList<WorkerPermissionArtifact> LoadWorkerPermissionArtifacts();

    void SaveProviderArtifact(AiExecutionArtifact artifact);

    AiExecutionArtifact? TryLoadProviderArtifact(string taskId);

    void SavePlannerProposalArtifact(PlannerProposalEnvelope artifact);

    PlannerProposalEnvelope? TryLoadPlannerProposalArtifact(string proposalId);

    void SaveSafetyArtifact(SafetyArtifact artifact);

    SafetyArtifact? TryLoadSafetyArtifact(string taskId);

    void SavePlannerReviewArtifact(PlannerReviewArtifact artifact);

    PlannerReviewArtifact? TryLoadPlannerReviewArtifact(string taskId);

    void SaveMergeCandidateArtifact(MergeCandidateArtifact artifact);

    void DeleteMergeCandidateArtifact(string taskId)
    {
    }

    void SaveRuntimeFailureArtifact(RuntimeFailureRecord artifact);

    RuntimeFailureRecord? TryLoadLatestRuntimeFailure();

    void SaveRuntimePackAdmissionArtifact(RuntimePackAdmissionArtifact artifact);

    RuntimePackAdmissionArtifact? TryLoadCurrentRuntimePackAdmissionArtifact();

    void SaveRuntimePackAdmissionPolicyArtifact(RuntimePackAdmissionPolicyArtifact artifact)
    {
    }

    RuntimePackAdmissionPolicyArtifact? TryLoadCurrentRuntimePackAdmissionPolicyArtifact()
    {
        return null;
    }

    void SaveRuntimePackSelectionArtifact(RuntimePackSelectionArtifact artifact);

    RuntimePackSelectionArtifact? TryLoadCurrentRuntimePackSelectionArtifact();

    RuntimePackSelectionArtifact? TryLoadRuntimePackSelectionArtifact(string selectionId)
    {
        return null;
    }

    IReadOnlyList<RuntimePackSelectionArtifact> LoadRuntimePackSelectionHistory(int? limit = null)
    {
        return Array.Empty<RuntimePackSelectionArtifact>();
    }

    void SaveRuntimePackSelectionAuditEntry(RuntimePackSelectionAuditEntry artifact)
    {
    }

    IReadOnlyList<RuntimePackSelectionAuditEntry> LoadRuntimePackSelectionAuditEntries(int? limit = null)
    {
        return Array.Empty<RuntimePackSelectionAuditEntry>();
    }

    void SaveRuntimePackSwitchPolicyArtifact(RuntimePackSwitchPolicyArtifact artifact)
    {
    }

    RuntimePackSwitchPolicyArtifact? TryLoadCurrentRuntimePackSwitchPolicyArtifact()
    {
        return null;
    }

    void SaveRuntimePackPolicyAuditEntry(RuntimePackPolicyAuditEntry artifact)
    {
    }

    IReadOnlyList<RuntimePackPolicyAuditEntry> LoadRuntimePackPolicyAuditEntries(int? limit = null)
    {
        return Array.Empty<RuntimePackPolicyAuditEntry>();
    }

    void SaveRuntimePackPolicyPreviewArtifact(RuntimePackPolicyPreviewArtifact artifact)
    {
    }

    RuntimePackPolicyPreviewArtifact? TryLoadCurrentRuntimePackPolicyPreviewArtifact()
    {
        return null;
    }
}

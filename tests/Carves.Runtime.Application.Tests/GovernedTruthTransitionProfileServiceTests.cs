using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class GovernedTruthTransitionProfileServiceTests
{
    [Fact]
    public void BuildRunToReviewTransitions_ReturnsReviewSubmissionAndTaskReviewDescriptors()
    {
        var service = new GovernedTruthTransitionProfileService();

        var transitions = service.BuildRunToReviewTransitions(
            "TASK-F2-001",
            DomainTaskStatus.Pending,
            "RTREV-RUN-F2");

        Assert.Collection(
            transitions,
            first =>
            {
                Assert.Equal(".ai/artifacts/worker-executions/", first.Root);
                Assert.Equal("review_submission_recorded", first.Operation);
                Assert.Equal("RTREV-RUN-F2", first.ObjectId);
                Assert.Equal("absent", first.From);
                Assert.Equal("recorded", first.To);
            },
            second =>
            {
                Assert.Equal(".ai/tasks/", second.Root);
                Assert.Equal("task_status_to_review", second.Operation);
                Assert.Equal("TASK-F2-001", second.ObjectId);
                Assert.Equal("Pending", second.From);
                Assert.Equal("REVIEW", second.To);
            });
    }

    [Fact]
    public void BuildPrivilegedExpectedTransitions_UsesGovernedRootAndAuthorizationTerminalShape()
    {
        var service = new GovernedTruthTransitionProfileService();

        var release = Assert.Single(service.BuildPrivilegedExpectedTransitions("release_channel", "card", "CARD-F2"));
        Assert.Equal(".carves-platform/", release.Root);
        Assert.Equal("release_channel", release.Operation);
        Assert.Equal("CARD-F2", release.ObjectId);
        Assert.Equal("pending_authorization", release.From);
        Assert.Equal("authorized", release.To);

        var memory = Assert.Single(service.BuildPrivilegedExpectedTransitions("promote_memory_truth", "memory_proposal", "MP-F2"));
        Assert.Equal(".ai/memory/", memory.Root);
        Assert.Equal("promote_memory_truth", memory.Operation);
        Assert.Equal("MP-F2", memory.ObjectId);
    }
}

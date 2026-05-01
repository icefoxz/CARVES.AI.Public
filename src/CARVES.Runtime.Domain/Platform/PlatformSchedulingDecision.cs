namespace Carves.Runtime.Domain.Platform;

public sealed record PlatformSchedulingDecision(
    int RequestedSlots,
    int GrantedSlots,
    IReadOnlyList<string> SelectedRepoIds,
    IReadOnlyList<PlatformSchedulingCandidateDecision> Candidates,
    string Reason);

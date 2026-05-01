namespace Carves.Runtime.Domain.Platform;

public sealed record PlatformSchedulingCandidateDecision(
    string RepoId,
    bool Selected,
    double FairnessScore,
    string Reason);

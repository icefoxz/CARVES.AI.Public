namespace Carves.Runtime.Application.Interaction;

public static class RuntimeMinimumOnboardingGuidance
{
    public static IReadOnlyList<string> Reads { get; } =
    [
        "README.md",
        "AGENTS.md",
        "inspect runtime-first-run-operator-packet",
    ];

    public static IReadOnlyList<string> NextSteps { get; } =
    [
        "keep attach and bootstrap writeback on the existing Runtime-owned lane",
        "capture project purpose, goals, boundary, and proof posture through the first initialization card",
        "continue through existing Host-routed taskgraph and execution surfaces instead of hidden onboarding state",
    ];

    public const string FriendlyReadSequence =
        "README.md -> AGENTS.md -> carves inspect runtime-first-run-operator-packet";
}

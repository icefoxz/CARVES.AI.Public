namespace Carves.Runtime.Application.Safety;

public static class SafetyValidatorCatalog
{
    public static IReadOnlyList<ISafetyValidator> CreateDefault()
    {
        return
        [
            new TaskIntegritySafetyValidator(),
            new FileAccessSafetyValidator(),
            new TaskScopeSafetyValidator(),
            new ManagedControlPlaneSafetyValidator(),
            new PatchSizeSafetyValidator(),
            new RetryLimitSafetyValidator(),
            new LoopDetectionSafetyValidator(),
            new ArchitectureRulesSafetyValidator(),
            new TestEnforcementSafetyValidator(),
        ];
    }
}

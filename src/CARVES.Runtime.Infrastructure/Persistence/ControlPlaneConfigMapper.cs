using System.Text.Json;
using Carves.Runtime.Application.Configuration;

namespace Carves.Runtime.Infrastructure.Persistence;

internal static class ControlPlaneConfigMapper
{
    public static SystemConfig ToSystemConfig(SystemConfigDto? dto, string repoRootName)
    {
        var defaults = SystemConfig.CreateDefault(repoRootName);
        return dto is null
            ? defaults
            : new SystemConfig(
                dto.RepoName ?? defaults.RepoName,
                dto.WorktreeRoot ?? defaults.WorktreeRoot,
                dto.MaxParallelTasks ?? defaults.MaxParallelTasks,
                dto.DefaultTestCommand?.ToArray() ?? defaults.DefaultTestCommand.ToArray(),
                dto.CodeDirectories?.ToArray() ?? defaults.CodeDirectories.ToArray(),
                dto.ExcludedDirectories?.ToArray() ?? defaults.ExcludedDirectories.ToArray(),
                dto.SyncMarkdownViews ?? defaults.SyncMarkdownViews,
                dto.RemoveWorktreeOnSuccess ?? defaults.RemoveWorktreeOnSuccess);
    }

    public static AiProviderConfig ToAiProviderConfig(AiProviderConfigDto? dto)
    {
        var defaults = AiProviderConfig.CreateDefault();
        return dto is null
            ? defaults
            : BuildAiProviderConfig(dto, defaults);
    }

    public static PlannerAutonomyPolicy ToPlannerAutonomyPolicy(PlannerAutonomyPolicyDto? dto)
    {
        var defaults = PlannerAutonomyPolicy.CreateDefault();
        return dto is null
            ? defaults
            : new PlannerAutonomyPolicy(
                dto.MaxPlannerRounds ?? defaults.MaxPlannerRounds,
                dto.MaxGeneratedTasks ?? defaults.MaxGeneratedTasks,
                dto.MaxRefactorScopeFiles ?? defaults.MaxRefactorScopeFiles,
                dto.MaxOpportunitiesPerRound ?? defaults.MaxOpportunitiesPerRound);
    }

    public static CarvesCodeStandard ToCarvesCodeStandard(CarvesCodeStandardDto? dto)
    {
        var defaults = CarvesCodeStandard.CreateDefault();
        return dto is null
            ? defaults
            : new CarvesCodeStandard(
                dto.Version ?? defaults.Version,
                dto.CoreLoop ?? defaults.CoreLoop,
                dto.InteractionLoop ?? defaults.InteractionLoop,
                ToCarvesAuthorityRules(dto.Authority, defaults.Authority),
                ToCarvesApplicabilityRules(dto.Applicability, defaults.Applicability),
                ToCarvesAiFriendlyArchitectureRules(dto.AiFriendlyArchitecture, defaults.AiFriendlyArchitecture),
                ToCarvesPhysicalSplittingRules(dto.PhysicalSplitting, defaults.PhysicalSplitting),
                ToCarvesExtremeNamingRules(dto.ExtremeNaming, defaults.ExtremeNaming),
                ToCarvesDependencyContractRules(dto.DependencyContract, defaults.DependencyContract),
                dto.AllowedEdges?.ToArray() ?? defaults.AllowedEdges.ToArray(),
                dto.RestrictedEdges?.ToArray() ?? defaults.RestrictedEdges.ToArray(),
                dto.ForbiddenEdges?.ToArray() ?? defaults.ForbiddenEdges.ToArray(),
                dto.ModerationRules?.ToArray() ?? defaults.ModerationRules.ToArray(),
                dto.ReviewQuestions?.ToArray() ?? defaults.ReviewQuestions.ToArray(),
                dto.RuntimeQuestions?.ToArray() ?? defaults.RuntimeQuestions.ToArray());
    }

    public static WorkerOperationalPolicy ToWorkerOperationalPolicy(WorkerOperationalPolicyDto? dto)
    {
        var defaults = WorkerOperationalPolicy.CreateDefault();
        return dto is null
            ? defaults
            : new WorkerOperationalPolicy(
                dto.Version ?? defaults.Version,
                dto.PreferredBackendId ?? defaults.PreferredBackendId,
                dto.PreferredTrustProfileId ?? defaults.PreferredTrustProfileId,
                ToWorkerApprovalOperationalPolicy(dto.Approval, defaults.Approval),
                ToWorkerRecoveryOperationalPolicy(dto.Recovery, defaults.Recovery),
                ToWorkerObservabilityOperationalPolicy(dto.Observability, defaults.Observability));
    }

    public static SafetyRules ToSafetyRules(SafetyRulesDto? dto)
    {
        var defaults = SafetyRules.CreateDefault();
        return dto is null
            ? defaults
            : new SafetyRules(
                dto.MaxFilesChanged ?? defaults.MaxFilesChanged,
                dto.MaxLinesChanged ?? defaults.MaxLinesChanged,
                dto.ReviewFilesChangedThreshold ?? defaults.ReviewFilesChangedThreshold,
                dto.ReviewLinesChangedThreshold ?? defaults.ReviewLinesChangedThreshold,
                dto.MaxRetryCount ?? defaults.MaxRetryCount,
                dto.ProtectedPaths?.ToArray() ?? defaults.ProtectedPaths.ToArray(),
                dto.RestrictedPaths?.ToArray() ?? defaults.RestrictedPaths.ToArray(),
                dto.WorkerWritablePaths?.ToArray() ?? defaults.WorkerWritablePaths.ToArray(),
                dto.ManagedControlPlanePaths?.ToArray() ?? defaults.ManagedControlPlanePaths.ToArray(),
                dto.MemoryWritePaths?.ToArray() ?? defaults.MemoryWritePaths.ToArray(),
                dto.MemoryWriteCapability ?? defaults.MemoryWriteCapability,
                dto.RequireTestsForSourceChanges ?? defaults.RequireTestsForSourceChanges,
                dto.ReviewRequiredForNewModule ?? defaults.ReviewRequiredForNewModule);
    }

    public static ModuleDependencyMap ToModuleDependencyMap(Dictionary<string, string[]?>? raw)
    {
        if (raw is null)
        {
            return ModuleDependencyMap.Empty;
        }

        var entries = raw.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)(pair.Value ?? Array.Empty<string>()),
            StringComparer.Ordinal);

        return new ModuleDependencyMap(entries);
    }

    private static AiProviderConfig BuildAiProviderConfig(AiProviderConfigDto dto, AiProviderConfig defaults)
    {
        var rootConfig = BuildResolvedConfig(
            dto.Provider ?? defaults.Provider,
            dto.Enabled,
            dto.Model,
            dto.BaseUrl,
            dto.ApiKeyEnvironmentVariable,
            dto.AllowFallbackToNull,
            dto.RequestTimeoutSeconds,
            dto.MaxOutputTokens,
            dto.ReasoningEffort,
            dto.RequestFamily,
            dto.Organization ?? defaults.Organization,
            dto.Project ?? defaults.Project,
            defaults,
            profileId: null,
            enabledDefault: defaults.Enabled);

        var profiles = dto.Profiles?.ToDictionary(
            pair => pair.Key,
            pair => BuildResolvedConfig(
                pair.Value.Provider ?? rootConfig.Provider,
                pair.Value.Enabled,
                pair.Value.Model,
                pair.Value.BaseUrl,
                pair.Value.ApiKeyEnvironmentVariable,
                pair.Value.AllowFallbackToNull,
                pair.Value.RequestTimeoutSeconds,
                pair.Value.MaxOutputTokens,
                pair.Value.ReasoningEffort,
                pair.Value.RequestFamily,
                pair.Value.Organization ?? rootConfig.Organization,
                pair.Value.Project ?? rootConfig.Project,
                rootConfig,
                profileId: pair.Key,
                enabledDefault: true),
            StringComparer.OrdinalIgnoreCase);

        var roleProfiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var roleOverrides = new Dictionary<string, AiProviderRoleConfig>(StringComparer.OrdinalIgnoreCase);
        if (dto.Roles is not null)
        {
            foreach (var entry in dto.Roles)
            {
                if (!TryParseRoleBinding(entry.Value, out var profileId, out var overrideConfig))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(profileId))
                {
                    roleProfiles[entry.Key] = profileId;
                }

                if (overrideConfig is not null)
                {
                    roleOverrides[entry.Key] = overrideConfig;
                }
            }
        }

        var defaultProfileId = ResolveDefaultProfileId(dto.DefaultProfile, profiles);
        var resolvedDefault = !string.IsNullOrWhiteSpace(defaultProfileId)
            && profiles is not null
            && profiles.TryGetValue(defaultProfileId, out var defaultProfile)
                ? defaultProfile
                : rootConfig;

        return resolvedDefault with
        {
            ProfileId = resolvedDefault.ProfileId,
            DefaultProfileId = defaultProfileId,
            Profiles = profiles,
            RoleProfiles = roleProfiles.Count > 0 ? roleProfiles : null,
            RoleOverrides = roleOverrides.Count > 0 ? roleOverrides : null,
        };
    }

    private static AiProviderConfig BuildResolvedConfig(
        string provider,
        bool? enabled,
        string? model,
        string? baseUrl,
        string? apiKeyEnvironmentVariable,
        bool? allowFallbackToNull,
        int? requestTimeoutSeconds,
        int? maxOutputTokens,
        string? reasoningEffort,
        string? requestFamily,
        string? organization,
        string? project,
        AiProviderConfig baseline,
        string? profileId,
        bool enabledDefault)
    {
        var resolvedProvider = string.IsNullOrWhiteSpace(provider) ? baseline.Provider : provider.Trim();
        var resolvedEnabled = enabled ?? (enabledDefault ? true : baseline.Enabled);
        var providerChanged = !string.Equals(resolvedProvider, baseline.Provider, StringComparison.OrdinalIgnoreCase);
        var providerDefaults = providerChanged
            ? AiProviderConfig.CreateProviderDefaults(
                resolvedProvider,
                resolvedEnabled,
                allowFallbackToNull ?? baseline.AllowFallbackToNull,
                requestTimeoutSeconds ?? baseline.RequestTimeoutSeconds,
                maxOutputTokens ?? baseline.MaxOutputTokens,
                reasoningEffort ?? baseline.ReasoningEffort)
            : baseline with
            {
                Enabled = resolvedEnabled,
                AllowFallbackToNull = allowFallbackToNull ?? baseline.AllowFallbackToNull,
                RequestTimeoutSeconds = requestTimeoutSeconds ?? baseline.RequestTimeoutSeconds,
                MaxOutputTokens = maxOutputTokens ?? baseline.MaxOutputTokens,
                ReasoningEffort = reasoningEffort ?? baseline.ReasoningEffort,
            };

        return providerDefaults with
        {
            Model = string.IsNullOrWhiteSpace(model)
                ? (providerChanged ? providerDefaults.Model : baseline.Model)
                : model,
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl)
                ? (providerChanged ? providerDefaults.BaseUrl : baseline.BaseUrl)
                : baseUrl,
            ApiKeyEnvironmentVariable = string.IsNullOrWhiteSpace(apiKeyEnvironmentVariable)
                ? (providerChanged ? providerDefaults.ApiKeyEnvironmentVariable : baseline.ApiKeyEnvironmentVariable)
                : apiKeyEnvironmentVariable,
            RequestFamily = string.IsNullOrWhiteSpace(requestFamily)
                ? (providerChanged ? providerDefaults.RequestFamily : baseline.RequestFamily)
                : requestFamily,
            Organization = organization ?? baseline.Organization,
            Project = project ?? baseline.Project,
            ProfileId = profileId,
        };
    }

    private static string? ResolveDefaultProfileId(string? requestedDefaultProfileId, IReadOnlyDictionary<string, AiProviderConfig>? profiles)
    {
        if (profiles is null || profiles.Count == 0)
        {
            return string.IsNullOrWhiteSpace(requestedDefaultProfileId) ? null : requestedDefaultProfileId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(requestedDefaultProfileId))
        {
            foreach (var entry in profiles)
            {
                if (string.Equals(entry.Key, requestedDefaultProfileId, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Key;
                }
            }
        }

        foreach (var entry in profiles)
        {
            if (string.Equals(entry.Key, "default", StringComparison.OrdinalIgnoreCase))
            {
                return entry.Key;
            }
        }

        if (profiles.Count == 1)
        {
            return profiles.Keys.First();
        }

        return string.IsNullOrWhiteSpace(requestedDefaultProfileId) ? null : requestedDefaultProfileId.Trim();
    }

    private static bool TryParseRoleBinding(JsonElement value, out string? profileId, out AiProviderRoleConfig? overrideConfig)
    {
        profileId = null;
        overrideConfig = null;

        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                profileId = value.GetString();
                return !string.IsNullOrWhiteSpace(profileId);
            case JsonValueKind.Object:
                break;
            default:
                return false;
        }

        string? explicitProfileId = null;
        var hasInlineOverride = false;
        var inlineOverride = new AiProviderRoleConfig();

        foreach (var property in value.EnumerateObject())
        {
            switch (property.Name)
            {
                case "profile":
                case "profile_id":
                    explicitProfileId = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                    break;
                case "provider":
                    inlineOverride = inlineOverride with { Provider = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null };
                    hasInlineOverride = true;
                    break;
                case "enabled":
                    inlineOverride = inlineOverride with { Enabled = property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False ? property.Value.GetBoolean() : null };
                    hasInlineOverride = true;
                    break;
                case "model":
                    inlineOverride = inlineOverride with { Model = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null };
                    hasInlineOverride = true;
                    break;
                case "base_url":
                    inlineOverride = inlineOverride with { BaseUrl = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null };
                    hasInlineOverride = true;
                    break;
                case "api_key_environment_variable":
                    inlineOverride = inlineOverride with { ApiKeyEnvironmentVariable = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null };
                    hasInlineOverride = true;
                    break;
                case "allow_fallback_to_null":
                    inlineOverride = inlineOverride with { AllowFallbackToNull = property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False ? property.Value.GetBoolean() : null };
                    hasInlineOverride = true;
                    break;
                case "request_timeout_seconds":
                    inlineOverride = inlineOverride with { RequestTimeoutSeconds = property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var timeoutSeconds) ? timeoutSeconds : null };
                    hasInlineOverride = true;
                    break;
                case "max_output_tokens":
                    inlineOverride = inlineOverride with { MaxOutputTokens = property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var maxOutputTokens) ? maxOutputTokens : null };
                    hasInlineOverride = true;
                    break;
                case "reasoning_effort":
                    inlineOverride = inlineOverride with { ReasoningEffort = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null };
                    hasInlineOverride = true;
                    break;
                case "request_family":
                    inlineOverride = inlineOverride with { RequestFamily = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null };
                    hasInlineOverride = true;
                    break;
                case "organization":
                    inlineOverride = inlineOverride with { Organization = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null };
                    hasInlineOverride = true;
                    break;
                case "project":
                    inlineOverride = inlineOverride with { Project = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null };
                    hasInlineOverride = true;
                    break;
            }
        }

        if (!string.IsNullOrWhiteSpace(explicitProfileId))
        {
            profileId = explicitProfileId;
        }

        if (hasInlineOverride)
        {
            overrideConfig = inlineOverride;
        }

        return !string.IsNullOrWhiteSpace(profileId) || overrideConfig is not null;
    }

    private static CarvesAuthorityRules ToCarvesAuthorityRules(CarvesAuthorityRulesDto? dto, CarvesAuthorityRules defaults)
    {
        return dto is null
            ? defaults
            : new CarvesAuthorityRules(
                dto.RecorderWritableBy?.ToArray() ?? defaults.RecorderWritableBy.ToArray(),
                dto.DomainEventsEmittedBy?.ToArray() ?? defaults.DomainEventsEmittedBy.ToArray(),
                dto.ViewReadOnly ?? defaults.ViewReadOnly,
                dto.ControllerDirectRecorderWriteForbidden ?? defaults.ControllerDirectRecorderWriteForbidden,
                dto.ActorDirectRecorderWriteForbidden ?? defaults.ActorDirectRecorderWriteForbidden);
    }

    private static CarvesApplicabilityRules ToCarvesApplicabilityRules(CarvesApplicabilityRulesDto? dto, CarvesApplicabilityRules defaults)
    {
        return dto is null
            ? defaults
            : new CarvesApplicabilityRules(
                dto.CarvesPurpose ?? defaults.CarvesPurpose,
                dto.RuntimePurpose ?? defaults.RuntimePurpose,
                dto.DirectoryLayoutRequired ?? defaults.DirectoryLayoutRequired,
                dto.OneClassPerLayerRequired ?? defaults.OneClassPerLayerRequired,
                dto.RefactorForPurityAloneForbidden ?? defaults.RefactorForPurityAloneForbidden);
    }

    private static CarvesAiFriendlyArchitectureRules ToCarvesAiFriendlyArchitectureRules(CarvesAiFriendlyArchitectureRulesDto? dto, CarvesAiFriendlyArchitectureRules defaults)
    {
        return dto is null
            ? defaults
            : new CarvesAiFriendlyArchitectureRules(
                dto.CorePrinciple ?? defaults.CorePrinciple,
                dto.DomainOrientedLayoutPreferred ?? defaults.DomainOrientedLayoutPreferred,
                dto.AvoidGenericDirectories ?? defaults.AvoidGenericDirectories,
                dto.AvoidGenericTypeNames ?? defaults.AvoidGenericTypeNames,
                dto.InterfacesOnlyAtRealBoundaries ?? defaults.InterfacesOnlyAtRealBoundaries,
                dto.ExplicitStateModelingRequired ?? defaults.ExplicitStateModelingRequired,
                dto.RecommendedFileLinesLowerBound ?? defaults.RecommendedFileLinesLowerBound,
                dto.RecommendedFileLinesUpperBound ?? defaults.RecommendedFileLinesUpperBound,
                dto.RefactorFileLinesThreshold ?? defaults.RefactorFileLinesThreshold,
                dto.MaxConceptualJumps ?? defaults.MaxConceptualJumps);
    }

    private static CarvesPhysicalSplittingRules ToCarvesPhysicalSplittingRules(CarvesPhysicalSplittingRulesDto? dto, CarvesPhysicalSplittingRules defaults)
    {
        return dto is null
            ? defaults
            : new CarvesPhysicalSplittingRules(
                dto.CorePrinciple ?? defaults.CorePrinciple,
                dto.LogicalLayersStrict ?? defaults.LogicalLayersStrict,
                dto.PhysicalSplittingElastic ?? defaults.PhysicalSplittingElastic,
                dto.SplitScoreCanSplit ?? defaults.SplitScoreCanSplit,
                dto.SplitScoreShouldSplit ?? defaults.SplitScoreShouldSplit,
                dto.AvoidThinForwarderSplits ?? defaults.AvoidThinForwarderSplits,
                dto.AvoidCompletenessSplits ?? defaults.AvoidCompletenessSplits,
                dto.SharedOnlyForNonSovereignSupport ?? defaults.SharedOnlyForNonSovereignSupport,
                dto.RuntimeRecommendedIndependentModules?.ToArray() ?? defaults.RuntimeRecommendedIndependentModules.ToArray());
    }

    private static CarvesExtremeNamingRules ToCarvesExtremeNamingRules(CarvesExtremeNamingRulesDto? dto, CarvesExtremeNamingRules defaults)
    {
        return dto is null
            ? defaults
            : new CarvesExtremeNamingRules(
                dto.CorePrinciple ?? defaults.CorePrinciple,
                dto.NamingGrammar ?? defaults.NamingGrammar,
                dto.DomainMustLead ?? defaults.DomainMustLead,
                dto.RoleSuffixMustBeTerminal ?? defaults.RoleSuffixMustBeTerminal,
                dto.FullWordsRequired ?? defaults.FullWordsRequired,
                dto.PascalCaseRequired ?? defaults.PascalCaseRequired,
                dto.FileNameMatchesPrimaryType ?? defaults.FileNameMatchesPrimaryType,
                dto.OneConceptOneHeadword ?? defaults.OneConceptOneHeadword,
                dto.CanonicalVocabularyRequired ?? defaults.CanonicalVocabularyRequired,
                dto.ApplicationServiceRequiresCompatibilityAnnotation ?? defaults.ApplicationServiceRequiresCompatibilityAnnotation,
                dto.EngineSystemLevelOnly ?? defaults.EngineSystemLevelOnly,
                dto.CanonicalArchitecturalTerms?.ToArray() ?? defaults.CanonicalArchitecturalTerms.ToArray(),
                dto.CanonicalExecuteTerms?.ToArray() ?? defaults.CanonicalExecuteTerms.ToArray(),
                dto.CanonicalRuntimeTerms?.ToArray() ?? defaults.CanonicalRuntimeTerms.ToArray(),
                dto.CanonicalPlatformTerms?.ToArray() ?? defaults.CanonicalPlatformTerms.ToArray(),
                dto.CanonicalMechanismTerms?.ToArray() ?? defaults.CanonicalMechanismTerms.ToArray(),
                dto.ForbiddenGenericWords?.ToArray() ?? defaults.ForbiddenGenericWords.ToArray(),
                dto.SuggestedAnalyzerRules?.ToArray() ?? defaults.SuggestedAnalyzerRules.ToArray(),
                dto.PlatformForbiddenAliases?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal) ?? defaults.PlatformForbiddenAliases,
                dto.PlatformLintPaths?.ToArray() ?? defaults.PlatformLintPaths.ToArray(),
                dto.PlatformLintAllowlistTypeNames?.ToArray() ?? defaults.PlatformLintAllowlistTypeNames.ToArray());
    }

    private static CarvesDependencyContractRules ToCarvesDependencyContractRules(CarvesDependencyContractRulesDto? dto, CarvesDependencyContractRules defaults)
    {
        return dto is null
            ? defaults
            : new CarvesDependencyContractRules(
                dto.CorePrinciple ?? defaults.CorePrinciple,
                dto.DependencyDirectionOneWay ?? defaults.DependencyDirectionOneWay,
                dto.SameLayerCouplingRestricted ?? defaults.SameLayerCouplingRestricted,
                dto.RoleClassificationPrecedence?.ToArray() ?? defaults.RoleClassificationPrecedence.ToArray(),
                dto.IncludedEdgeKinds?.ToArray() ?? defaults.IncludedEdgeKinds.ToArray(),
                dto.ExcludedEdgeKinds?.ToArray() ?? defaults.ExcludedEdgeKinds.ToArray(),
                dto.RecorderAccessModel ?? defaults.RecorderAccessModel,
                dto.AmbiguousSymbolPolicy ?? defaults.AmbiguousSymbolPolicy,
                ToDependencyMatrix(dto.AllowedDependencyMatrix, defaults.AllowedDependencyMatrix),
                dto.ForbiddenDiagnosticRules?.ToArray() ?? defaults.ForbiddenDiagnosticRules.ToArray(),
                dto.RestrictedDiagnosticRules?.ToArray() ?? defaults.RestrictedDiagnosticRules.ToArray(),
                dto.AdvisoryDiagnosticRules?.ToArray() ?? defaults.AdvisoryDiagnosticRules.ToArray());
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ToDependencyMatrix(
        Dictionary<string, string[]?>? raw,
        IReadOnlyDictionary<string, IReadOnlyList<string>> defaults)
    {
        if (raw is null)
        {
            return defaults;
        }

        return raw.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)(pair.Value ?? Array.Empty<string>()),
            StringComparer.Ordinal);
    }

    private static WorkerApprovalOperationalPolicy ToWorkerApprovalOperationalPolicy(WorkerApprovalOperationalPolicyDto? dto, WorkerApprovalOperationalPolicy defaults)
    {
        return dto is null
            ? defaults
            : new WorkerApprovalOperationalPolicy(
                dto.OutsideWorkspaceRequiresReview ?? defaults.OutsideWorkspaceRequiresReview,
                dto.HighRiskRequiresReview ?? defaults.HighRiskRequiresReview,
                dto.ManualApprovalModeRequiresReview ?? defaults.ManualApprovalModeRequiresReview,
                dto.AutoAllowCategories?.ToArray() ?? defaults.AutoAllowCategories.ToArray(),
                dto.AutoDenyCategories?.ToArray() ?? defaults.AutoDenyCategories.ToArray(),
                dto.ForceReviewCategories?.ToArray() ?? defaults.ForceReviewCategories.ToArray());
    }

    private static WorkerRecoveryOperationalPolicy ToWorkerRecoveryOperationalPolicy(WorkerRecoveryOperationalPolicyDto? dto, WorkerRecoveryOperationalPolicy defaults)
    {
        return dto is null
            ? defaults
            : new WorkerRecoveryOperationalPolicy(
                dto.MaxRetryCount ?? defaults.MaxRetryCount,
                dto.TransientInfraBackoffSeconds ?? defaults.TransientInfraBackoffSeconds,
                dto.TimeoutBackoffSeconds ?? defaults.TimeoutBackoffSeconds,
                dto.InvalidOutputBackoffSeconds ?? defaults.InvalidOutputBackoffSeconds,
                dto.EnvironmentRebuildBackoffSeconds ?? defaults.EnvironmentRebuildBackoffSeconds,
                dto.SwitchProviderOnEnvironmentBlocked ?? defaults.SwitchProviderOnEnvironmentBlocked,
                dto.SwitchProviderOnUnavailableBackend ?? defaults.SwitchProviderOnUnavailableBackend);
    }

    private static WorkerObservabilityOperationalPolicy ToWorkerObservabilityOperationalPolicy(WorkerObservabilityOperationalPolicyDto? dto, WorkerObservabilityOperationalPolicy defaults)
    {
        return dto is null
            ? defaults
            : new WorkerObservabilityOperationalPolicy(
                dto.ProviderDegradedLatencyMs ?? defaults.ProviderDegradedLatencyMs,
                dto.ApprovalQueuePreviewLimit ?? defaults.ApprovalQueuePreviewLimit,
                dto.BlockedQueuePreviewLimit ?? defaults.BlockedQueuePreviewLimit,
                dto.IncidentPreviewLimit ?? defaults.IncidentPreviewLimit,
                dto.GovernanceReportDefaultHours ?? defaults.GovernanceReportDefaultHours);
    }

}

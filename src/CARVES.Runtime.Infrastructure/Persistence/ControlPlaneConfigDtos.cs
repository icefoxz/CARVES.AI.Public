using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carves.Runtime.Infrastructure.Persistence;

internal sealed class SystemConfigDto
{
    [JsonPropertyName("repo_name")]
    public string? RepoName { get; init; }

    [JsonPropertyName("worktree_root")]
    public string? WorktreeRoot { get; init; }

    [JsonPropertyName("max_parallel_tasks")]
    public int? MaxParallelTasks { get; init; }

    [JsonPropertyName("default_test_command")]
    public string[]? DefaultTestCommand { get; init; }

    [JsonPropertyName("code_directories")]
    public string[]? CodeDirectories { get; init; }

    [JsonPropertyName("excluded_directories")]
    public string[]? ExcludedDirectories { get; init; }

    [JsonPropertyName("sync_markdown_views")]
    public bool? SyncMarkdownViews { get; init; }

    [JsonPropertyName("remove_worktree_on_success")]
    public bool? RemoveWorktreeOnSuccess { get; init; }
}

internal sealed class AiProviderConfigDto
{
    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("base_url")]
    public string? BaseUrl { get; init; }

    [JsonPropertyName("api_key_environment_variable")]
    public string? ApiKeyEnvironmentVariable { get; init; }

    [JsonPropertyName("allow_fallback_to_null")]
    public bool? AllowFallbackToNull { get; init; }

    [JsonPropertyName("request_timeout_seconds")]
    public int? RequestTimeoutSeconds { get; init; }

    [JsonPropertyName("max_output_tokens")]
    public int? MaxOutputTokens { get; init; }

    [JsonPropertyName("reasoning_effort")]
    public string? ReasoningEffort { get; init; }

    [JsonPropertyName("request_family")]
    public string? RequestFamily { get; init; }

    [JsonPropertyName("organization")]
    public string? Organization { get; init; }

    [JsonPropertyName("project")]
    public string? Project { get; init; }

    [JsonPropertyName("default_profile")]
    public string? DefaultProfile { get; init; }

    [JsonPropertyName("profiles")]
    public Dictionary<string, AiProviderProfileConfigDto>? Profiles { get; init; }

    [JsonPropertyName("roles")]
    public Dictionary<string, JsonElement>? Roles { get; init; }
}

internal sealed class AiProviderProfileConfigDto
{
    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("base_url")]
    public string? BaseUrl { get; init; }

    [JsonPropertyName("api_key_environment_variable")]
    public string? ApiKeyEnvironmentVariable { get; init; }

    [JsonPropertyName("allow_fallback_to_null")]
    public bool? AllowFallbackToNull { get; init; }

    [JsonPropertyName("request_timeout_seconds")]
    public int? RequestTimeoutSeconds { get; init; }

    [JsonPropertyName("max_output_tokens")]
    public int? MaxOutputTokens { get; init; }

    [JsonPropertyName("reasoning_effort")]
    public string? ReasoningEffort { get; init; }

    [JsonPropertyName("request_family")]
    public string? RequestFamily { get; init; }

    [JsonPropertyName("organization")]
    public string? Organization { get; init; }

    [JsonPropertyName("project")]
    public string? Project { get; init; }
}

internal sealed class PlannerAutonomyPolicyDto
{
    [JsonPropertyName("max_planner_rounds")]
    public int? MaxPlannerRounds { get; init; }

    [JsonPropertyName("max_generated_tasks")]
    public int? MaxGeneratedTasks { get; init; }

    [JsonPropertyName("max_refactor_scope_files")]
    public int? MaxRefactorScopeFiles { get; init; }

    [JsonPropertyName("max_opportunities_per_round")]
    public int? MaxOpportunitiesPerRound { get; init; }
}

internal sealed class CarvesAuthorityRulesDto
{
    [JsonPropertyName("recorder_writable_by")]
    public string[]? RecorderWritableBy { get; init; }

    [JsonPropertyName("domain_events_emitted_by")]
    public string[]? DomainEventsEmittedBy { get; init; }

    [JsonPropertyName("view_read_only")]
    public bool? ViewReadOnly { get; init; }

    [JsonPropertyName("controller_direct_recorder_write_forbidden")]
    public bool? ControllerDirectRecorderWriteForbidden { get; init; }

    [JsonPropertyName("actor_direct_recorder_write_forbidden")]
    public bool? ActorDirectRecorderWriteForbidden { get; init; }
}

internal sealed class CarvesApplicabilityRulesDto
{
    [JsonPropertyName("carves_purpose")]
    public string? CarvesPurpose { get; init; }

    [JsonPropertyName("runtime_purpose")]
    public string? RuntimePurpose { get; init; }

    [JsonPropertyName("directory_layout_required")]
    public bool? DirectoryLayoutRequired { get; init; }

    [JsonPropertyName("one_class_per_layer_required")]
    public bool? OneClassPerLayerRequired { get; init; }

    [JsonPropertyName("refactor_for_purity_alone_forbidden")]
    public bool? RefactorForPurityAloneForbidden { get; init; }
}

internal sealed class CarvesAiFriendlyArchitectureRulesDto
{
    [JsonPropertyName("core_principle")]
    public string? CorePrinciple { get; init; }

    [JsonPropertyName("domain_oriented_layout_preferred")]
    public bool? DomainOrientedLayoutPreferred { get; init; }

    [JsonPropertyName("avoid_generic_directories")]
    public bool? AvoidGenericDirectories { get; init; }

    [JsonPropertyName("avoid_generic_type_names")]
    public bool? AvoidGenericTypeNames { get; init; }

    [JsonPropertyName("interfaces_only_at_real_boundaries")]
    public bool? InterfacesOnlyAtRealBoundaries { get; init; }

    [JsonPropertyName("explicit_state_modeling_required")]
    public bool? ExplicitStateModelingRequired { get; init; }

    [JsonPropertyName("recommended_file_lines_lower_bound")]
    public int? RecommendedFileLinesLowerBound { get; init; }

    [JsonPropertyName("recommended_file_lines_upper_bound")]
    public int? RecommendedFileLinesUpperBound { get; init; }

    [JsonPropertyName("refactor_file_lines_threshold")]
    public int? RefactorFileLinesThreshold { get; init; }

    [JsonPropertyName("max_conceptual_jumps")]
    public int? MaxConceptualJumps { get; init; }
}

internal sealed class CarvesPhysicalSplittingRulesDto
{
    [JsonPropertyName("core_principle")]
    public string? CorePrinciple { get; init; }

    [JsonPropertyName("logical_layers_strict")]
    public bool? LogicalLayersStrict { get; init; }

    [JsonPropertyName("physical_splitting_elastic")]
    public bool? PhysicalSplittingElastic { get; init; }

    [JsonPropertyName("split_score_can_split")]
    public int? SplitScoreCanSplit { get; init; }

    [JsonPropertyName("split_score_should_split")]
    public int? SplitScoreShouldSplit { get; init; }

    [JsonPropertyName("avoid_thin_forwarder_splits")]
    public bool? AvoidThinForwarderSplits { get; init; }

    [JsonPropertyName("avoid_completeness_splits")]
    public bool? AvoidCompletenessSplits { get; init; }

    [JsonPropertyName("shared_only_for_non_sovereign_support")]
    public bool? SharedOnlyForNonSovereignSupport { get; init; }

    [JsonPropertyName("runtime_recommended_independent_modules")]
    public string[]? RuntimeRecommendedIndependentModules { get; init; }
}

internal sealed class CarvesExtremeNamingRulesDto
{
    [JsonPropertyName("core_principle")]
    public string? CorePrinciple { get; init; }

    [JsonPropertyName("naming_grammar")]
    public string? NamingGrammar { get; init; }

    [JsonPropertyName("domain_must_lead")]
    public bool? DomainMustLead { get; init; }

    [JsonPropertyName("role_suffix_must_be_terminal")]
    public bool? RoleSuffixMustBeTerminal { get; init; }

    [JsonPropertyName("full_words_required")]
    public bool? FullWordsRequired { get; init; }

    [JsonPropertyName("pascal_case_required")]
    public bool? PascalCaseRequired { get; init; }

    [JsonPropertyName("file_name_matches_primary_type")]
    public bool? FileNameMatchesPrimaryType { get; init; }

    [JsonPropertyName("one_concept_one_headword")]
    public bool? OneConceptOneHeadword { get; init; }

    [JsonPropertyName("canonical_vocabulary_required")]
    public bool? CanonicalVocabularyRequired { get; init; }

    [JsonPropertyName("application_service_requires_compatibility_annotation")]
    public bool? ApplicationServiceRequiresCompatibilityAnnotation { get; init; }

    [JsonPropertyName("engine_system_level_only")]
    public bool? EngineSystemLevelOnly { get; init; }

    [JsonPropertyName("canonical_architectural_terms")]
    public string[]? CanonicalArchitecturalTerms { get; init; }

    [JsonPropertyName("canonical_execute_terms")]
    public string[]? CanonicalExecuteTerms { get; init; }

    [JsonPropertyName("canonical_runtime_terms")]
    public string[]? CanonicalRuntimeTerms { get; init; }

    [JsonPropertyName("canonical_platform_terms")]
    public string[]? CanonicalPlatformTerms { get; init; }

    [JsonPropertyName("canonical_mechanism_terms")]
    public string[]? CanonicalMechanismTerms { get; init; }

    [JsonPropertyName("forbidden_generic_words")]
    public string[]? ForbiddenGenericWords { get; init; }

    [JsonPropertyName("suggested_analyzer_rules")]
    public string[]? SuggestedAnalyzerRules { get; init; }

    [JsonPropertyName("platform_forbidden_aliases")]
    public Dictionary<string, string>? PlatformForbiddenAliases { get; init; }

    [JsonPropertyName("platform_lint_paths")]
    public string[]? PlatformLintPaths { get; init; }

    [JsonPropertyName("platform_lint_allowlist_type_names")]
    public string[]? PlatformLintAllowlistTypeNames { get; init; }
}

internal sealed class CarvesDependencyContractRulesDto
{
    [JsonPropertyName("core_principle")]
    public string? CorePrinciple { get; init; }

    [JsonPropertyName("dependency_direction_one_way")]
    public bool? DependencyDirectionOneWay { get; init; }

    [JsonPropertyName("same_layer_coupling_restricted")]
    public bool? SameLayerCouplingRestricted { get; init; }

    [JsonPropertyName("role_classification_precedence")]
    public string[]? RoleClassificationPrecedence { get; init; }

    [JsonPropertyName("included_edge_kinds")]
    public string[]? IncludedEdgeKinds { get; init; }

    [JsonPropertyName("excluded_edge_kinds")]
    public string[]? ExcludedEdgeKinds { get; init; }

    [JsonPropertyName("recorder_access_model")]
    public string? RecorderAccessModel { get; init; }

    [JsonPropertyName("ambiguous_symbol_policy")]
    public string? AmbiguousSymbolPolicy { get; init; }

    [JsonPropertyName("allowed_dependency_matrix")]
    public Dictionary<string, string[]?>? AllowedDependencyMatrix { get; init; }

    [JsonPropertyName("forbidden_diagnostic_rules")]
    public string[]? ForbiddenDiagnosticRules { get; init; }

    [JsonPropertyName("restricted_diagnostic_rules")]
    public string[]? RestrictedDiagnosticRules { get; init; }

    [JsonPropertyName("advisory_diagnostic_rules")]
    public string[]? AdvisoryDiagnosticRules { get; init; }
}

internal sealed class CarvesCodeStandardDto
{
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("core_loop")]
    public string? CoreLoop { get; init; }

    [JsonPropertyName("interaction_loop")]
    public string? InteractionLoop { get; init; }

    [JsonPropertyName("authority")]
    public CarvesAuthorityRulesDto? Authority { get; init; }

    [JsonPropertyName("applicability")]
    public CarvesApplicabilityRulesDto? Applicability { get; init; }

    [JsonPropertyName("ai_friendly_architecture")]
    public CarvesAiFriendlyArchitectureRulesDto? AiFriendlyArchitecture { get; init; }

    [JsonPropertyName("physical_splitting")]
    public CarvesPhysicalSplittingRulesDto? PhysicalSplitting { get; init; }

    [JsonPropertyName("extreme_naming")]
    public CarvesExtremeNamingRulesDto? ExtremeNaming { get; init; }

    [JsonPropertyName("dependency_contract")]
    public CarvesDependencyContractRulesDto? DependencyContract { get; init; }

    [JsonPropertyName("allowed_edges")]
    public string[]? AllowedEdges { get; init; }

    [JsonPropertyName("restricted_edges")]
    public string[]? RestrictedEdges { get; init; }

    [JsonPropertyName("forbidden_edges")]
    public string[]? ForbiddenEdges { get; init; }

    [JsonPropertyName("moderation_rules")]
    public string[]? ModerationRules { get; init; }

    [JsonPropertyName("review_questions")]
    public string[]? ReviewQuestions { get; init; }

    [JsonPropertyName("runtime_questions")]
    public string[]? RuntimeQuestions { get; init; }
}

internal sealed class SafetyRulesDto
{
    [JsonPropertyName("max_files_changed")]
    public int? MaxFilesChanged { get; init; }

    [JsonPropertyName("max_lines_changed")]
    public int? MaxLinesChanged { get; init; }

    [JsonPropertyName("max_retry_count")]
    public int? MaxRetryCount { get; init; }

    [JsonPropertyName("review_files_changed_threshold")]
    public int? ReviewFilesChangedThreshold { get; init; }

    [JsonPropertyName("review_lines_changed_threshold")]
    public int? ReviewLinesChangedThreshold { get; init; }

    [JsonPropertyName("protected_paths")]
    public string[]? ProtectedPaths { get; init; }

    [JsonPropertyName("restricted_paths")]
    public string[]? RestrictedPaths { get; init; }

    [JsonPropertyName("worker_writable_paths")]
    public string[]? WorkerWritablePaths { get; init; }

    [JsonPropertyName("managed_control_plane_paths")]
    public string[]? ManagedControlPlanePaths { get; init; }

    [JsonPropertyName("memory_write_paths")]
    public string[]? MemoryWritePaths { get; init; }

    [JsonPropertyName("memory_write_capability")]
    public string? MemoryWriteCapability { get; init; }

    [JsonPropertyName("require_tests_for_source_changes")]
    public bool? RequireTestsForSourceChanges { get; init; }

    [JsonPropertyName("review_required_for_new_module")]
    public bool? ReviewRequiredForNewModule { get; init; }
}

internal sealed class WorkerApprovalOperationalPolicyDto
{
    [JsonPropertyName("outside_workspace_requires_review")]
    public bool? OutsideWorkspaceRequiresReview { get; init; }

    [JsonPropertyName("high_risk_requires_review")]
    public bool? HighRiskRequiresReview { get; init; }

    [JsonPropertyName("manual_approval_mode_requires_review")]
    public bool? ManualApprovalModeRequiresReview { get; init; }

    [JsonPropertyName("auto_allow_categories")]
    public string[]? AutoAllowCategories { get; init; }

    [JsonPropertyName("auto_deny_categories")]
    public string[]? AutoDenyCategories { get; init; }

    [JsonPropertyName("force_review_categories")]
    public string[]? ForceReviewCategories { get; init; }
}

internal sealed class WorkerRecoveryOperationalPolicyDto
{
    [JsonPropertyName("max_retry_count")]
    public int? MaxRetryCount { get; init; }

    [JsonPropertyName("transient_infra_backoff_seconds")]
    public int? TransientInfraBackoffSeconds { get; init; }

    [JsonPropertyName("timeout_backoff_seconds")]
    public int? TimeoutBackoffSeconds { get; init; }

    [JsonPropertyName("invalid_output_backoff_seconds")]
    public int? InvalidOutputBackoffSeconds { get; init; }

    [JsonPropertyName("environment_rebuild_backoff_seconds")]
    public int? EnvironmentRebuildBackoffSeconds { get; init; }

    [JsonPropertyName("switch_provider_on_environment_blocked")]
    public bool? SwitchProviderOnEnvironmentBlocked { get; init; }

    [JsonPropertyName("switch_provider_on_unavailable_backend")]
    public bool? SwitchProviderOnUnavailableBackend { get; init; }
}

internal sealed class WorkerObservabilityOperationalPolicyDto
{
    [JsonPropertyName("provider_degraded_latency_ms")]
    public int? ProviderDegradedLatencyMs { get; init; }

    [JsonPropertyName("approval_queue_preview_limit")]
    public int? ApprovalQueuePreviewLimit { get; init; }

    [JsonPropertyName("blocked_queue_preview_limit")]
    public int? BlockedQueuePreviewLimit { get; init; }

    [JsonPropertyName("incident_preview_limit")]
    public int? IncidentPreviewLimit { get; init; }

    [JsonPropertyName("governance_report_default_hours")]
    public int? GovernanceReportDefaultHours { get; init; }
}

internal sealed class WorkerOperationalPolicyDto
{
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("preferred_backend_id")]
    public string? PreferredBackendId { get; init; }

    [JsonPropertyName("preferred_trust_profile_id")]
    public string? PreferredTrustProfileId { get; init; }

    [JsonPropertyName("approval")]
    public WorkerApprovalOperationalPolicyDto? Approval { get; init; }

    [JsonPropertyName("recovery")]
    public WorkerRecoveryOperationalPolicyDto? Recovery { get; init; }

    [JsonPropertyName("observability")]
    public WorkerObservabilityOperationalPolicyDto? Observability { get; init; }
}

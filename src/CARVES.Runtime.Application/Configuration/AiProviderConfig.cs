namespace Carves.Runtime.Application.Configuration;

public sealed record AiProviderRoleConfig(
    string? Provider = null,
    bool? Enabled = null,
    string? Model = null,
    string? BaseUrl = null,
    string? ApiKeyEnvironmentVariable = null,
    bool? AllowFallbackToNull = null,
    int? RequestTimeoutSeconds = null,
    int? MaxOutputTokens = null,
    string? ReasoningEffort = null,
    string? RequestFamily = null,
    string? Organization = null,
    string? Project = null);

public sealed record AiProviderConfig(
    string Provider,
    bool Enabled,
    string Model,
    string BaseUrl,
    string ApiKeyEnvironmentVariable,
    bool AllowFallbackToNull,
    int RequestTimeoutSeconds,
    int MaxOutputTokens,
    string ReasoningEffort,
    string? RequestFamily,
    string? Organization,
    string? Project,
    IReadOnlyDictionary<string, AiProviderRoleConfig>? RoleOverrides = null,
    string? ProfileId = null,
    string? DefaultProfileId = null,
    IReadOnlyDictionary<string, AiProviderConfig>? Profiles = null,
    IReadOnlyDictionary<string, string>? RoleProfiles = null)
{
    public static AiProviderConfig CreateDefault()
    {
        return CreateProviderDefaults(
            provider: "null",
            enabled: false,
            allowFallbackToNull: true,
            requestTimeoutSeconds: 30,
            maxOutputTokens: 500,
            reasoningEffort: "low");
    }

    public static AiProviderConfig CreateProviderDefaults(
        string provider,
        bool enabled,
        bool allowFallbackToNull,
        int requestTimeoutSeconds,
        int maxOutputTokens,
        string reasoningEffort)
    {
        return provider.ToLowerInvariant() switch
        {
            "openai" => new AiProviderConfig(
                Provider: "openai",
                Enabled: enabled,
                Model: "gpt-5-mini",
                BaseUrl: "https://api.openai.com/v1",
                ApiKeyEnvironmentVariable: "OPENAI_API_KEY",
                AllowFallbackToNull: allowFallbackToNull,
                RequestTimeoutSeconds: requestTimeoutSeconds,
                MaxOutputTokens: maxOutputTokens,
                ReasoningEffort: reasoningEffort,
                RequestFamily: null,
                Organization: null,
                Project: null),
            "claude" => new AiProviderConfig(
                Provider: "claude",
                Enabled: enabled,
                Model: "claude-sonnet-4-5",
                BaseUrl: "https://api.anthropic.com/v1",
                ApiKeyEnvironmentVariable: "ANTHROPIC_API_KEY",
                AllowFallbackToNull: allowFallbackToNull,
                RequestTimeoutSeconds: requestTimeoutSeconds,
                MaxOutputTokens: maxOutputTokens,
                ReasoningEffort: reasoningEffort,
                RequestFamily: "messages_api",
                Organization: null,
                Project: null),
            "gemini" => new AiProviderConfig(
                Provider: "gemini",
                Enabled: enabled,
                Model: "gemini-2.5-pro",
                BaseUrl: "https://generativelanguage.googleapis.com/v1beta",
                ApiKeyEnvironmentVariable: "GEMINI_API_KEY",
                AllowFallbackToNull: allowFallbackToNull,
                RequestTimeoutSeconds: requestTimeoutSeconds,
                MaxOutputTokens: maxOutputTokens,
                ReasoningEffort: reasoningEffort,
                RequestFamily: "generate_content",
                Organization: null,
                Project: null),
            "codex" => new AiProviderConfig(
                Provider: "codex",
                Enabled: enabled,
                Model: "gpt-5-codex",
                BaseUrl: "https://api.openai.com/v1",
                ApiKeyEnvironmentVariable: "OPENAI_API_KEY",
                AllowFallbackToNull: allowFallbackToNull,
                RequestTimeoutSeconds: requestTimeoutSeconds,
                MaxOutputTokens: maxOutputTokens,
                ReasoningEffort: reasoningEffort,
                RequestFamily: "responses_api",
                Organization: null,
                Project: null),
            "local" or "local-llm" => new AiProviderConfig(
                Provider: "local",
                Enabled: enabled,
                Model: "local-agent",
                BaseUrl: string.Empty,
                ApiKeyEnvironmentVariable: string.Empty,
                AllowFallbackToNull: allowFallbackToNull,
                RequestTimeoutSeconds: requestTimeoutSeconds,
                MaxOutputTokens: maxOutputTokens,
                ReasoningEffort: reasoningEffort,
                RequestFamily: "local_agent",
                Organization: null,
                Project: null),
            "null" => new AiProviderConfig(
                Provider: "null",
                Enabled: false,
                Model: "none",
                BaseUrl: string.Empty,
                ApiKeyEnvironmentVariable: string.Empty,
                AllowFallbackToNull: true,
                RequestTimeoutSeconds: requestTimeoutSeconds,
                MaxOutputTokens: maxOutputTokens,
                ReasoningEffort: reasoningEffort,
                RequestFamily: "none",
                Organization: null,
                Project: null),
            _ => new AiProviderConfig(
                Provider: provider,
                Enabled: enabled,
                Model: "gpt-5-mini",
                BaseUrl: "https://api.openai.com/v1",
                ApiKeyEnvironmentVariable: "OPENAI_API_KEY",
                AllowFallbackToNull: allowFallbackToNull,
                RequestTimeoutSeconds: requestTimeoutSeconds,
                MaxOutputTokens: maxOutputTokens,
                ReasoningEffort: reasoningEffort,
                RequestFamily: null,
                Organization: null,
                Project: null),
        };
    }

    public AiProviderConfig ResolveForRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return this;
        }

        if (TryGetRoleProfile(role, out var profileId) && TryGetProfile(profileId, out var profileConfig))
        {
            var resolvedProfile = AttachCatalogMetadata(profileConfig, profileId);
            return TryGetRoleOverride(role, out var overrideConfig)
                ? ApplyRoleOverride(resolvedProfile, overrideConfig, profileId)
                : resolvedProfile;
        }

        if (TryGetRoleOverride(role, out var inlineOverride))
        {
            return ApplyRoleOverride(this, inlineOverride, ProfileId);
        }

        return this;
    }

    public IReadOnlyDictionary<string, AiProviderConfig> GetProfiles()
    {
        return Profiles ?? EmptyProfiles;
    }

    public IReadOnlyDictionary<string, string> GetRoleProfiles()
    {
        return RoleProfiles ?? EmptyRoleProfiles;
    }

    public IReadOnlyDictionary<string, AiProviderRoleConfig> GetRoleOverrides()
    {
        return RoleOverrides ?? EmptyRoleOverrides;
    }

    private bool TryGetRoleProfile(string role, out string profileId)
    {
        if (RoleProfiles is not null)
        {
            foreach (var entry in RoleProfiles)
            {
                if (string.Equals(entry.Key, role, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(entry.Value))
                {
                    profileId = entry.Value;
                    return true;
                }
            }
        }

        profileId = string.Empty;
        return false;
    }

    private bool TryGetProfile(string profileId, out AiProviderConfig profileConfig)
    {
        if (Profiles is not null)
        {
            foreach (var entry in Profiles)
            {
                if (string.Equals(entry.Key, profileId, StringComparison.OrdinalIgnoreCase))
                {
                    profileConfig = entry.Value;
                    return true;
                }
            }
        }

        profileConfig = default!;
        return false;
    }

    private bool TryGetRoleOverride(string role, out AiProviderRoleConfig overrideConfig)
    {
        if (RoleOverrides is not null)
        {
            foreach (var entry in RoleOverrides)
            {
                if (string.Equals(entry.Key, role, StringComparison.OrdinalIgnoreCase))
                {
                    overrideConfig = entry.Value;
                    return true;
                }
            }
        }

        overrideConfig = default!;
        return false;
    }

    private AiProviderConfig AttachCatalogMetadata(AiProviderConfig config, string? profileId = null)
    {
        return config with
        {
            ProfileId = profileId ?? config.ProfileId,
            DefaultProfileId = DefaultProfileId,
            Profiles = Profiles,
            RoleProfiles = RoleProfiles,
            RoleOverrides = RoleOverrides,
        };
    }

    private static AiProviderConfig ApplyRoleOverride(AiProviderConfig baseConfig, AiProviderRoleConfig overrideConfig, string? profileId)
    {
        var resolvedProvider = string.IsNullOrWhiteSpace(overrideConfig.Provider) ? baseConfig.Provider : overrideConfig.Provider.Trim();
        var providerChanged = !string.Equals(resolvedProvider, baseConfig.Provider, StringComparison.OrdinalIgnoreCase);
        var baseline = providerChanged
            ? CreateProviderDefaults(
                resolvedProvider,
                baseConfig.Enabled,
                baseConfig.AllowFallbackToNull,
                baseConfig.RequestTimeoutSeconds,
                baseConfig.MaxOutputTokens,
                baseConfig.ReasoningEffort)
            : baseConfig;

        return baseline with
        {
            Provider = resolvedProvider,
            Enabled = overrideConfig.Enabled ?? baseline.Enabled,
            Model = overrideConfig.Model ?? baseline.Model,
            BaseUrl = overrideConfig.BaseUrl ?? baseline.BaseUrl,
            ApiKeyEnvironmentVariable = overrideConfig.ApiKeyEnvironmentVariable ?? baseline.ApiKeyEnvironmentVariable,
            AllowFallbackToNull = overrideConfig.AllowFallbackToNull ?? baseline.AllowFallbackToNull,
            RequestTimeoutSeconds = overrideConfig.RequestTimeoutSeconds ?? baseline.RequestTimeoutSeconds,
            MaxOutputTokens = overrideConfig.MaxOutputTokens ?? baseline.MaxOutputTokens,
            ReasoningEffort = overrideConfig.ReasoningEffort ?? baseline.ReasoningEffort,
            RequestFamily = overrideConfig.RequestFamily ?? baseline.RequestFamily,
            Organization = overrideConfig.Organization ?? baseline.Organization,
            Project = overrideConfig.Project ?? baseline.Project,
            ProfileId = profileId ?? baseConfig.ProfileId,
            DefaultProfileId = baseConfig.DefaultProfileId,
            Profiles = baseConfig.Profiles,
            RoleProfiles = baseConfig.RoleProfiles,
            RoleOverrides = baseConfig.RoleOverrides,
        };
    }

    private static readonly IReadOnlyDictionary<string, AiProviderConfig> EmptyProfiles
        = new Dictionary<string, AiProviderConfig>(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string> EmptyRoleProfiles
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, AiProviderRoleConfig> EmptyRoleOverrides
        = new Dictionary<string, AiProviderRoleConfig>(StringComparer.OrdinalIgnoreCase);
}

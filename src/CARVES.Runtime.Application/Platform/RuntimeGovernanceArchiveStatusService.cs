using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform.SurfaceModels;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeGovernanceArchiveStatusService
{
    private static readonly string[] ScanRoots = ["docs", "tests", "src", ".ai"];

    private static readonly string[] ReadableExtensions =
    [
        ".cs",
        ".json",
        ".md",
        ".ps1",
        ".sh",
        ".toml",
        ".txt",
        ".yaml",
        ".yml",
    ];

    private const string ProtocolEvidencePath = ".ai/evidence/runtime/surface-inventory/CARD-934-alias-protocol-compatibility.json";

    private const int MaxSampleRefsPerSurface = 5;

    private static readonly IReadOnlyDictionary<string, string[]> LegacyApiFieldsBySurface =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["runtime-governance-program-reaudit"] =
            [
                "overall_verdict",
                "continuation_gate_outcome",
                "closure_delta_posture",
                "counts",
                "criteria",
            ],
            ["runtime-hotspot-backlog-drain"] =
            [
                "counts",
                "queues",
                "queue_family_count",
                "closure_blocking_backlog_item_count",
                "residual_open_queue_count",
            ],
            ["runtime-hotspot-cross-family-patterns"] =
            [
                "counts",
                "patterns",
                "boundary_categories",
                "repeated_backlog_kind_pattern_count",
                "shared_boundary_category_count",
            ],
            ["runtime-packaging-proof-federation-maturity"] =
            [
                "packaging_profiles",
                "proof_lanes",
                "federation_lanes",
                "closed_capabilities",
            ],
            ["runtime-controlled-governance-proof"] =
            [
                "controlled_mode_default",
                "lanes",
                "source_handoff_lane_ids",
                "governing_commands",
                "runtime_truth_families",
                "runtime_evidence_paths",
            ],
            ["runtime-validationlab-proof-handoff"] =
            [
                "controlled_mode_default",
                "lanes",
                "runtime_commands",
                "runtime_truth_families",
                "runtime_evidence_paths",
            ],
        };

    private readonly string repoRoot;

    public RuntimeGovernanceArchiveStatusService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
    }

    public RuntimeGovernanceArchiveStatusSurface Build(string? requestedSurfaceId = null, string? legacyArgument = null)
    {
        var surfaceId = string.IsNullOrWhiteSpace(requestedSurfaceId)
            ? RuntimeGovernanceArchiveStatusIds.PrimarySurfaceId
            : requestedSurfaceId.Trim();
        var isAlias = !string.Equals(surfaceId, RuntimeGovernanceArchiveStatusIds.PrimarySurfaceId, StringComparison.Ordinal);
        var metadataByName = RuntimeSurfaceCommandRegistry.CommandMetadata.ToDictionary(
            item => item.Name,
            StringComparer.Ordinal);
        var errors = new List<string>();
        var warnings = new List<string>();
        var inventory = BuildConsumerInventory(warnings);
        var aliases = RuntimeGovernanceArchiveStatusIds.LegacySurfaceIds
            .Select(surface => BuildAlias(metadataByName, surface, inventory, warnings.Count > 0))
            .ToArray();

        if (isAlias && !RuntimeGovernanceArchiveStatusIds.LegacySurfaceIds.Contains(surfaceId, StringComparer.Ordinal))
        {
            errors.Add($"Unknown historical governance archive alias: {surfaceId}");
        }

        foreach (var alias in aliases)
        {
            if (!string.Equals(alias.SuccessorSurfaceId, RuntimeGovernanceArchiveStatusIds.PrimarySurfaceId, StringComparison.Ordinal))
            {
                errors.Add($"Alias {alias.SurfaceId} does not point to {RuntimeGovernanceArchiveStatusIds.PrimarySurfaceId}.");
            }

            if (!RuntimeGovernanceArchiveCompatibilityClasses.All.Contains(alias.CompatibilityClass, StringComparer.Ordinal))
            {
                errors.Add($"Alias {alias.SurfaceId} has unsupported compatibility class {alias.CompatibilityClass}.");
            }

            if (alias.CompatibilityEvidenceRefs.Count == 0)
            {
                errors.Add($"Alias {alias.SurfaceId} has no compatibility evidence refs.");
            }
        }

        return new RuntimeGovernanceArchiveStatusSurface
        {
            SurfaceId = surfaceId,
            PrimarySurfaceId = RuntimeGovernanceArchiveStatusIds.PrimarySurfaceId,
            SurfaceRole = isAlias ? "compatibility_alias" : "primary",
            SuccessorSurfaceId = isAlias ? RuntimeGovernanceArchiveStatusIds.PrimarySurfaceId : null,
            RetirementPosture = isAlias ? "alias_retained" : "active_primary",
            LegacyArgument = string.IsNullOrWhiteSpace(legacyArgument) ? null : legacyArgument,
            Summary = "Historical governance and hotspot readbacks are consolidated behind one bounded archive status surface. Legacy names remain exact-call compatibility aliases with explicit expansion pointers.",
            DefaultVisibleSurfaceCount = RuntimeSurfaceCommandRegistry.DefaultVisibleCommandMetadata.Count,
            DefaultVisibleBudget = RuntimeSurfaceCommandRegistry.MaxDefaultVisibleSurfaceCount,
            PrimarySurfaceCount = RuntimeSurfaceCommandRegistry.PrimaryCommandMetadata.Count,
            CompatibilityAliasCount = RuntimeSurfaceCommandRegistry.CompatibilityAliasCommandMetadata.Count,
            CompatibilityClassCounts = aliases
                .GroupBy(alias => alias.CompatibilityClass, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            OutputBudget = new RuntimeGovernanceArchiveOutputBudgetSurface
            {
                InspectMaxLines = 30,
                AliasEntryCount = aliases.Length,
                HistoricalDocumentBodiesEmbedded = false,
                MaxSampleReferencePathsPerSurface = MaxSampleRefsPerSurface,
            },
            LegacyAliases = aliases,
            ExpansionPointers = BuildExpansionPointers(),
            ConsumerInventory = inventory,
            NonClaims =
            [
                "This surface does not delete, rename, hide, or retire any legacy inspect/API name.",
                "This surface does not make historical governance evidence default-visible or startup-visible.",
                "This surface does not make the CARD-921 inventory baseline a runtime dispatch, help, planner, or routing authority.",
                "Detailed historical proof surfaces remain implementation-local expansion paths only where a later governed card still needs their exact legacy shape.",
            ],
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
        };
    }

    private static RuntimeGovernanceArchiveAliasSurface BuildAlias(
        IReadOnlyDictionary<string, RuntimeSurfaceCommandMetadata> metadataByName,
        string surfaceId,
        RuntimeGovernanceArchiveConsumerInventorySurface inventory,
        bool scanHadWarnings)
    {
        var legacyApiFields = LegacyApiFieldsBySurface.TryGetValue(surfaceId, out var fields)
            ? fields
            : [];
        var commandReferenceCount = inventory.ReferenceCountsBySurface.TryGetValue(surfaceId, out var references)
            ? references
            : 0;
        var jsonFieldConsumerReferenceCount = inventory.JsonFieldConsumerCountsBySurface.TryGetValue(surfaceId, out var jsonReferences)
            ? jsonReferences
            : 0;
        var commandReferenceEvidenceRefs = inventory.SampleReferencePathsBySurface.TryGetValue(surfaceId, out var commandRefs)
            ? commandRefs
            : [];
        var jsonFieldConsumerEvidenceRefs = inventory.SampleJsonFieldConsumerRefsBySurface.TryGetValue(surfaceId, out var jsonRefs)
            ? jsonRefs
            : [];
        var compatibilityClass = DetermineCompatibilityClass(commandReferenceCount, jsonFieldConsumerReferenceCount, scanHadWarnings);
        var compatibilityDecision = BuildCompatibilityDecision(compatibilityClass);
        var compatibilityEvidenceRefs = BuildCompatibilityEvidenceRefs(surfaceId, commandReferenceEvidenceRefs, jsonFieldConsumerEvidenceRefs);

        if (!metadataByName.TryGetValue(surfaceId, out var metadata))
        {
            return new RuntimeGovernanceArchiveAliasSurface
            {
                SurfaceId = surfaceId,
                DetailExpansionCommand = $"inspect {surfaceId}",
                CompatibilityClass = RuntimeGovernanceArchiveCompatibilityClasses.BlockedUnknownConsumer,
                CompatibilityDecision = "Registry metadata is missing; treat the alias as unknown and do not delete or reshape it.",
                LegacyApiFields = legacyApiFields,
                CommandReferenceCount = commandReferenceCount,
                JsonFieldConsumerReferenceCount = jsonFieldConsumerReferenceCount,
                CompatibilityEvidenceRefs = compatibilityEvidenceRefs,
                CommandReferenceEvidenceRefs = commandReferenceEvidenceRefs,
                JsonFieldConsumerEvidenceRefs = jsonFieldConsumerEvidenceRefs,
            };
        }

        return new RuntimeGovernanceArchiveAliasSurface
        {
            SurfaceId = surfaceId,
            SurfaceRole = ToToken(metadata.SurfaceRole),
            SuccessorSurfaceId = metadata.SuccessorSurfaceId ?? RuntimeGovernanceArchiveStatusIds.PrimarySurfaceId,
            RetirementPosture = ToToken(metadata.RetirementPosture),
            ContextTier = metadata.ContextTier.ToString(),
            DefaultVisibility = metadata.DefaultVisibility.ToString(),
            InspectUsage = metadata.InspectUsage,
            ApiUsage = metadata.ApiUsage,
            DetailExpansionCommand = metadata.InspectUsage,
            ExactInvocationPreserved = true,
            CompatibilityClass = compatibilityClass,
            CompatibilityDecision = compatibilityDecision,
            LegacyApiFields = legacyApiFields,
            CommandReferenceCount = commandReferenceCount,
            JsonFieldConsumerReferenceCount = jsonFieldConsumerReferenceCount,
            CompatibilityEvidenceRefs = compatibilityEvidenceRefs,
            CommandReferenceEvidenceRefs = commandReferenceEvidenceRefs,
            JsonFieldConsumerEvidenceRefs = jsonFieldConsumerEvidenceRefs,
        };
    }

    private static IReadOnlyList<RuntimeGovernanceArchiveExpansionPointerSurface> BuildExpansionPointers()
    {
        return
        [
            new RuntimeGovernanceArchiveExpansionPointerSurface
            {
                SurfaceId = "runtime-governance-program-reaudit",
                Path = "docs/runtime/runtime-governance-program-reaudit.md",
                Reason = "program re-audit history",
            },
            new RuntimeGovernanceArchiveExpansionPointerSurface
            {
                SurfaceId = "runtime-hotspot-backlog-drain",
                Path = "docs/runtime/runtime-hotspot-backlog-drain-governance.md",
                Reason = "queue-governed drain history",
            },
            new RuntimeGovernanceArchiveExpansionPointerSurface
            {
                SurfaceId = "runtime-hotspot-cross-family-patterns",
                Path = "docs/runtime/runtime-hotspot-cross-family-patterns.md",
                Reason = "cross-family pattern history",
            },
            new RuntimeGovernanceArchiveExpansionPointerSurface
            {
                SurfaceId = "runtime-packaging-proof-federation-maturity",
                Path = "docs/runtime/runtime-packaging-proof-federation-maturity.md",
                Reason = "packaging and proof maturity history",
            },
            new RuntimeGovernanceArchiveExpansionPointerSurface
            {
                SurfaceId = "runtime-controlled-governance-proof",
                Path = "docs/runtime/runtime-controlled-governance-proof-integration.md",
                Reason = "controlled governance proof integration history",
            },
            new RuntimeGovernanceArchiveExpansionPointerSurface
            {
                SurfaceId = "runtime-validationlab-proof-handoff",
                Path = "docs/runtime/runtime-validationlab-proof-handoff-boundary.md",
                Reason = "ValidationLab handoff boundary history",
            },
        ];
    }

    private RuntimeGovernanceArchiveConsumerInventorySurface BuildConsumerInventory(List<string> warnings)
    {
        var scannedRoots = ScanRoots
            .Where(root => Directory.Exists(Path.Combine(repoRoot, root)))
            .ToArray();
        var counts = RuntimeGovernanceArchiveStatusIds.LegacySurfaceIds
            .Append(RuntimeGovernanceArchiveStatusIds.PrimarySurfaceId)
            .ToDictionary(surface => surface, _ => 0, StringComparer.Ordinal);
        var samples = RuntimeGovernanceArchiveStatusIds.LegacySurfaceIds
            .Append(RuntimeGovernanceArchiveStatusIds.PrimarySurfaceId)
            .ToDictionary(surface => surface, _ => new List<string>(), StringComparer.Ordinal);
        var jsonFieldConsumerCounts = RuntimeGovernanceArchiveStatusIds.LegacySurfaceIds
            .ToDictionary(surface => surface, _ => 0, StringComparer.Ordinal);
        var jsonFieldConsumerSamples = RuntimeGovernanceArchiveStatusIds.LegacySurfaceIds
            .ToDictionary(surface => surface, _ => new List<string>(), StringComparer.Ordinal);
        var scannedFileCount = 0;
        var skippedFileCount = 0;
        var referenceFiles = new HashSet<string>(StringComparer.Ordinal);

        foreach (var root in scannedRoots)
        {
            var absoluteRoot = Path.Combine(repoRoot, root);
            foreach (var file in EnumerateFilesSafely(absoluteRoot, warnings))
            {
                if (!ShouldReadFile(file))
                {
                    skippedFileCount++;
                    continue;
                }

                scannedFileCount++;
                var relativePath = NormalizePath(Path.GetRelativePath(repoRoot, file));
                string text;
                try
                {
                    text = File.ReadAllText(file);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    skippedFileCount++;
                    warnings.Add($"consumer inventory skipped unreadable file {relativePath}: {exception.GetType().Name}");
                    continue;
                }

                foreach (var surfaceId in counts.Keys.ToArray())
                {
                    if (!text.Contains(surfaceId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    counts[surfaceId]++;
                    referenceFiles.Add(relativePath);
                    if (samples[surfaceId].Count < MaxSampleRefsPerSurface)
                    {
                        samples[surfaceId].Add(relativePath);
                    }

                    foreach (var jsonFieldConsumerRef in FindJsonFieldConsumerRefs(relativePath, text, surfaceId))
                    {
                        jsonFieldConsumerCounts[surfaceId]++;
                        if (jsonFieldConsumerSamples[surfaceId].Count < MaxSampleRefsPerSurface)
                        {
                            jsonFieldConsumerSamples[surfaceId].Add(jsonFieldConsumerRef);
                        }
                    }
                }
            }
        }

        return new RuntimeGovernanceArchiveConsumerInventorySurface
        {
            ScannedRoots = scannedRoots,
            ScannedFileCount = scannedFileCount,
            SkippedFileCount = skippedFileCount,
            ReferenceFileCount = referenceFiles.Count,
            ReferenceCountsBySurface = counts,
            SampleReferencePathsBySurface = samples.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value.ToArray(),
                StringComparer.Ordinal),
            JsonFieldConsumerCountsBySurface = jsonFieldConsumerCounts,
            SampleJsonFieldConsumerRefsBySurface = jsonFieldConsumerSamples.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value.ToArray(),
                StringComparer.Ordinal),
            Notes =
            [
                "Scans are bounded to docs, tests, src, and .ai text-like files.",
                "Sample paths are capped; counts are evidence for compatibility retention, not a runtime routing authority.",
                "JSON-field consumer refs are limited to explicit System.Text.Json GetProperty/TryGetProperty legacy-field reads in files that also reference the alias name.",
                "Guide/resource-pack and validation bundle references are covered through docs/guides and tests fixture paths under the scanned roots.",
            ],
        };
    }

    private static IReadOnlyList<string> FindJsonFieldConsumerRefs(string relativePath, string text, string surfaceId)
    {
        if (!LegacyApiFieldsBySurface.TryGetValue(surfaceId, out var legacyApiFields))
        {
            return [];
        }

        var refs = new List<string>();
        foreach (var field in legacyApiFields)
        {
            var getPropertyPattern = $"GetProperty(\"{field}\")";
            var tryGetPropertyPattern = $"TryGetProperty(\"{field}\"";
            if (text.Contains(getPropertyPattern, StringComparison.Ordinal))
            {
                refs.Add($"{relativePath}#{getPropertyPattern}");
                continue;
            }

            if (text.Contains(tryGetPropertyPattern, StringComparison.Ordinal))
            {
                refs.Add($"{relativePath}#{tryGetPropertyPattern}");
            }
        }

        return refs;
    }

    private static string DetermineCompatibilityClass(
        int commandReferenceCount,
        int jsonFieldConsumerReferenceCount,
        bool scanHadWarnings)
    {
        if (scanHadWarnings)
        {
            return RuntimeGovernanceArchiveCompatibilityClasses.BlockedUnknownConsumer;
        }

        if (jsonFieldConsumerReferenceCount > 0)
        {
            return RuntimeGovernanceArchiveCompatibilityClasses.ShapeCompatRequired;
        }

        return commandReferenceCount > 0
            ? RuntimeGovernanceArchiveCompatibilityClasses.NameCompatOnly
            : RuntimeGovernanceArchiveCompatibilityClasses.HumanInspectOnly;
    }

    private static string BuildCompatibilityDecision(string compatibilityClass)
    {
        return compatibilityClass switch
        {
            RuntimeGovernanceArchiveCompatibilityClasses.ShapeCompatRequired =>
                "At least one explicit JSON-field consumer was found; keep or wrap the legacy API shape before removing the old implementation.",
            RuntimeGovernanceArchiveCompatibilityClasses.BlockedUnknownConsumer =>
                "Consumer evidence is incomplete; preserve conservative compatibility and require operator review before reshaping the alias.",
            RuntimeGovernanceArchiveCompatibilityClasses.HumanInspectOnly =>
                "No machine JSON-field consumer was found and no command-reference evidence was found; treat as human inspect only until new evidence appears.",
            _ =>
                "Exact inspect/API invocation remains callable; bounded scan found no explicit legacy JSON-field consumer for this alias.",
        };
    }

    private static IReadOnlyList<string> BuildCompatibilityEvidenceRefs(
        string surfaceId,
        IReadOnlyList<string> commandReferenceEvidenceRefs,
        IReadOnlyList<string> jsonFieldConsumerEvidenceRefs)
    {
        var refs = new List<string>
        {
            $"{ProtocolEvidencePath}#aliases/{surfaceId}/compatibility_class",
            $"{ProtocolEvidencePath}#aliases/{surfaceId}/command_reference_evidence_refs",
            $"{ProtocolEvidencePath}#aliases/{surfaceId}/json_field_consumer_evidence_refs",
        };
        refs.AddRange(commandReferenceEvidenceRefs.Take(3));
        refs.AddRange(jsonFieldConsumerEvidenceRefs.Take(3));
        return refs.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IEnumerable<string> EnumerateFilesSafely(string root, List<string> warnings)
    {
        try
        {
            return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(file => !HasSkippedSegment(file))
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"consumer inventory skipped root {root}: {exception.GetType().Name}");
            return [];
        }
    }

    private static bool ShouldReadFile(string file)
    {
        var extension = Path.GetExtension(file);
        if (!ReadableExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var info = new FileInfo(file);
        return info.Length <= 2_000_000;
    }

    private static bool HasSkippedSegment(string file)
    {
        var normalized = NormalizePath(file);
        return normalized.Contains("/bin/", StringComparison.Ordinal)
            || normalized.Contains("/obj/", StringComparison.Ordinal)
            || normalized.Contains("/.git/", StringComparison.Ordinal)
            || normalized.Contains("/.carves-platform/", StringComparison.Ordinal)
            || normalized.Contains("/.vs/", StringComparison.Ordinal)
            || normalized.Contains("/.ai/evidence/runtime/surface-inventory/codegraph/", StringComparison.Ordinal);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string ToToken(RuntimeSurfaceRole role)
    {
        return role switch
        {
            RuntimeSurfaceRole.CompatibilityAlias => "compatibility_alias",
            _ => "primary",
        };
    }

    private static string ToToken(RuntimeSurfaceRetirementPosture posture)
    {
        return posture switch
        {
            RuntimeSurfaceRetirementPosture.AliasRetained => "alias_retained",
            _ => "active_primary",
        };
    }
}

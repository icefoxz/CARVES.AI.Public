using System.Diagnostics;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeLocalDistFreshnessSmokeService
{
    public const string PhaseDocumentPath = "docs/runtime/carves-product-closure-phase-22-local-dist-freshness-smoke.md";
    public const string PreviousPhaseDocumentPath = RuntimeTargetDistBindingPlanService.PhaseDocumentPath;
    public const string GuideDocumentPath = "docs/guides/CARVES_LOCAL_DIST_FRESHNESS_SMOKE.md";
    private const string DistVersion = RuntimeAlphaVersion.Current;

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;

    public RuntimeLocalDistFreshnessSmokeService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
    }

    public RuntimeLocalDistFreshnessSmokeSurface Build()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(PhaseDocumentPath, "Product closure Phase 22 local dist freshness smoke document", errors);
        ValidateRuntimeDocument(PreviousPhaseDocumentPath, "Product closure Phase 21 target dist binding plan document", errors);
        ValidateRuntimeDocument(GuideDocumentPath, "Local dist freshness smoke guide document", errors);
        ValidateRuntimeDocument(RuntimeFrozenDistTargetReadbackProofService.PhaseDocumentPath, "Product closure Phase 23 frozen dist target readback proof document", errors);
        ValidateRuntimeDocument("docs/runtime/carves-product-closure-phase-24-wrapper-runtime-root-binding.md", "Product closure Phase 24 wrapper runtime root binding document", errors);
        ValidateRuntimeDocument("docs/runtime/carves-product-closure-phase-25-external-target-product-proof-closure.md", "Product closure Phase 25 external target product proof closure document", errors);
        ValidateRuntimeDocument("docs/runtime/carves-product-closure-phase-26a-product-closure-projection-cleanup.md", "Product closure Phase 26A projection cleanup document", errors);
        ValidateRuntimeDocument("docs/runtime/carves-product-closure-phase-26-real-external-repo-pilot.md", "Product closure Phase 26 real external repo pilot document", errors);
        ValidateRuntimeDocument(RuntimeTargetResiduePolicyService.PhaseDocumentPath, "Product closure Phase 27 external target residue policy document", errors);
        ValidateRuntimeDocument(RuntimeTargetIgnoreDecisionPlanService.PhaseDocumentPath, "Product closure Phase 28 target ignore decision plan document", errors);
        ValidateRuntimeDocument(RuntimeTargetIgnoreDecisionRecordService.DecisionRecordPhaseDocumentPath, "Product closure Phase 29 target ignore decision record document", errors);
        ValidateRuntimeDocument(RuntimeTargetIgnoreDecisionRecordService.AuditPhaseDocumentPath, "Product closure Phase 30 target ignore decision record audit document", errors);
        ValidateRuntimeDocument(RuntimeTargetIgnoreDecisionRecordService.CommitReadbackPhaseDocumentPath, "Product closure Phase 31 target ignore decision record commit readback document", errors);
        ValidateRuntimeDocument(RuntimeProductClosureMetadata.CurrentDocumentPath, RuntimeProductClosureMetadata.CurrentDocumentLabel, errors);
        ValidateRuntimeDocument(RuntimeFrozenDistTargetReadbackProofService.GuideDocumentPath, "Frozen dist target readback proof guide document", errors);
        ValidateRuntimeDocument(RuntimeTargetDistBindingPlanService.GuideDocumentPath, "Target dist binding plan guide document", errors);
        ValidateRuntimeDocument(RuntimeLocalDistHandoffService.LocalDistGuideDocumentPath, "Runtime local dist guide document", errors);
        ValidateRuntimeDocument(RuntimeLocalDistHandoffService.CliDistributionGuideDocumentPath, "CLI distribution guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosureMetadata.CurrentGuideDocumentPath, "Current product closure guide document", errors);

        var runtimeRoot = documentRoot.DocumentRoot;
        var sourceRepoRoot = ResolveSourceRepoRoot(runtimeRoot);
        var sourceRepoRootExists = Directory.Exists(sourceRepoRoot);
        var sourceGit = ReadSourceGit(sourceRepoRoot);
        var candidateDistRoot = ResolveCandidateDistRoot(runtimeRoot, sourceRepoRoot);
        var manifestPath = Path.Combine(candidateDistRoot, "MANIFEST.json");
        var versionPath = Path.Combine(candidateDistRoot, "VERSION");
        var candidateDistExists = Directory.Exists(candidateDistRoot);
        var candidateDistHasManifest = File.Exists(manifestPath);
        var candidateDistHasVersion = File.Exists(versionPath);
        var candidateDistHasWrapper = RuntimeCliWrapperPaths.HasPreferredWrapper(candidateDistRoot);
        var candidateDistPublishedCliEntry = RuntimeCliWrapperPaths.PublishedCliManifestEntry;
        var candidateDistHasPublishedCli = RuntimeCliWrapperPaths.HasPublishedCli(candidateDistRoot);
        var candidateDistHasPhaseDocument = File.Exists(ToFullPath(candidateDistRoot, PhaseDocumentPath));
        var candidateDistHasGuideDocument = File.Exists(ToFullPath(candidateDistRoot, GuideDocumentPath));
        var candidateDistHasTargetBindingGuide = File.Exists(ToFullPath(candidateDistRoot, RuntimeTargetDistBindingPlanService.GuideDocumentPath));
        var candidateDistHasLocalDistGuide = File.Exists(ToFullPath(candidateDistRoot, RuntimeLocalDistHandoffService.LocalDistGuideDocumentPath));
        var candidateDistHasCliDistributionGuide = File.Exists(ToFullPath(candidateDistRoot, RuntimeLocalDistHandoffService.CliDistributionGuideDocumentPath));
        var candidateDistHasCurrentProductClosureDocument = File.Exists(ToFullPath(candidateDistRoot, RuntimeProductClosureMetadata.CurrentDocumentPath));
        var candidateDistHasCurrentProductClosureGuide = File.Exists(ToFullPath(candidateDistRoot, RuntimeProductClosureMetadata.CurrentGuideDocumentPath));
        var candidateDistHasGitDirectory = Directory.Exists(Path.Combine(candidateDistRoot, ".git"))
                                           || File.Exists(Path.Combine(candidateDistRoot, ".git"));
        var candidateDistHasSolution = File.Exists(Path.Combine(candidateDistRoot, "CARVES.Runtime.sln"));
        var versionFileValue = candidateDistHasVersion ? File.ReadAllText(versionPath).Trim() : string.Empty;
        var manifest = candidateDistHasManifest ? ReadManifest(manifestPath, errors) : RuntimeDistFreshnessManifest.Empty;
        var manifestVersionMatchesVersionFile = !string.IsNullOrWhiteSpace(manifest.Version)
                                                && string.Equals(manifest.Version, versionFileValue, StringComparison.Ordinal);
        var manifestOutputMatchesCandidateDist = PathEquals(manifest.OutputPath, candidateDistRoot);
        var manifestPublishedCliEntryMatchesPublishedCli = string.Equals(
            manifest.PublishedCliEntry,
            candidateDistPublishedCliEntry,
            StringComparison.Ordinal);
        var manifestSourceCommitMatchesSourceHead = !string.IsNullOrWhiteSpace(manifest.SourceCommit)
                                                    && !string.IsNullOrWhiteSpace(sourceGit.Head)
                                                    && string.Equals(manifest.SourceCommit, sourceGit.Head, StringComparison.OrdinalIgnoreCase);
        var ready = errors.Count == 0
                    && candidateDistExists
                    && candidateDistHasManifest
                    && candidateDistHasVersion
                    && candidateDistHasWrapper
                    && candidateDistHasPublishedCli
                    && candidateDistHasPhaseDocument
                    && candidateDistHasGuideDocument
                    && candidateDistHasTargetBindingGuide
                    && candidateDistHasLocalDistGuide
                    && candidateDistHasCliDistributionGuide
                    && candidateDistHasCurrentProductClosureDocument
                    && candidateDistHasCurrentProductClosureGuide
                    && !candidateDistHasGitDirectory
                    && !candidateDistHasSolution
                    && manifestVersionMatchesVersionFile
                    && manifestOutputMatchesCandidateDist
                    && manifestPublishedCliEntryMatchesPublishedCli
                    && manifestSourceCommitMatchesSourceHead
                    && sourceGit.HeadDetected
                    && sourceGit.WorktreeClean;
        var gaps = BuildGaps(
                errors,
                sourceRepoRootExists,
                sourceGit,
                candidateDistExists,
                candidateDistHasManifest,
                candidateDistHasVersion,
                candidateDistHasWrapper,
                candidateDistHasPublishedCli,
                candidateDistHasPhaseDocument,
                candidateDistHasGuideDocument,
                candidateDistHasTargetBindingGuide,
                candidateDistHasLocalDistGuide,
                candidateDistHasCliDistributionGuide,
                candidateDistHasCurrentProductClosureDocument,
                candidateDistHasCurrentProductClosureGuide,
                candidateDistHasGitDirectory,
                candidateDistHasSolution,
                manifestVersionMatchesVersionFile,
                manifestOutputMatchesCandidateDist,
                manifestPublishedCliEntryMatchesPublishedCli,
                manifestSourceCommitMatchesSourceHead)
            .ToArray();

        return new RuntimeLocalDistFreshnessSmokeSurface
        {
            PhaseDocumentPath = PhaseDocumentPath,
            PreviousPhaseDocumentPath = PreviousPhaseDocumentPath,
            GuideDocumentPath = GuideDocumentPath,
            CurrentProductClosureDocumentPath = RuntimeProductClosureMetadata.CurrentDocumentPath,
            CurrentProductClosureGuideDocumentPath = RuntimeProductClosureMetadata.CurrentGuideDocumentPath,
            RuntimeDocumentRoot = runtimeRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = ResolvePosture(errors.Count, ready, sourceGit, candidateDistExists, candidateDistHasManifest, candidateDistHasVersion, candidateDistHasWrapper, candidateDistHasPublishedCli, manifestSourceCommitMatchesSourceHead),
            DistVersion = DistVersion,
            SourceRepoRoot = sourceRepoRoot,
            SourceRepoRootExists = sourceRepoRootExists,
            SourceGitHeadDetected = sourceGit.HeadDetected,
            SourceGitHead = sourceGit.Head,
            SourceGitWorktreeClean = sourceGit.WorktreeClean,
            CandidateDistRoot = candidateDistRoot,
            CandidateDistExists = candidateDistExists,
            CandidateDistHasManifest = candidateDistHasManifest,
            CandidateDistHasVersion = candidateDistHasVersion,
            CandidateDistHasWrapper = candidateDistHasWrapper,
            CandidateDistPublishedCliEntry = candidateDistPublishedCliEntry,
            CandidateDistHasPublishedCli = candidateDistHasPublishedCli,
            CandidateDistHasPhaseDocument = candidateDistHasPhaseDocument,
            CandidateDistHasGuideDocument = candidateDistHasGuideDocument,
            CandidateDistHasTargetBindingGuide = candidateDistHasTargetBindingGuide,
            CandidateDistHasLocalDistGuide = candidateDistHasLocalDistGuide,
            CandidateDistHasCliDistributionGuide = candidateDistHasCliDistributionGuide,
            CandidateDistHasCurrentProductClosureDocument = candidateDistHasCurrentProductClosureDocument,
            CandidateDistHasCurrentProductClosureGuide = candidateDistHasCurrentProductClosureGuide,
            CandidateDistHasGitDirectory = candidateDistHasGitDirectory,
            CandidateDistHasSolution = candidateDistHasSolution,
            ManifestSchemaVersion = manifest.SchemaVersion,
            ManifestVersion = manifest.Version,
            ManifestSourceCommit = manifest.SourceCommit,
            ManifestSourceRepoRoot = manifest.SourceRepoRoot,
            ManifestOutputPath = manifest.OutputPath,
            ManifestPublishedCliEntry = manifest.PublishedCliEntry,
            VersionFileValue = versionFileValue,
            ManifestVersionMatchesVersionFile = manifestVersionMatchesVersionFile,
            ManifestOutputMatchesCandidateDist = manifestOutputMatchesCandidateDist,
            ManifestSourceCommitMatchesSourceHead = manifestSourceCommitMatchesSourceHead,
            ManifestPublishedCliEntryMatchesPublishedCli = manifestPublishedCliEntryMatchesPublishedCli,
            LocalDistFreshnessSmokeReady = ready,
            RequiredSourceCommands =
            [
                $".\\scripts\\pack-runtime-dist.ps1 -Version {DistVersion} -Force",
                "carves pilot dist-smoke --json",
            ],
            RequiredDistReadbackCommands =
            [
                RuntimeCliWrapperPaths.FormatShellCommand(RuntimeCliWrapperPaths.PreferredWrapperPath(candidateDistRoot), "agent", "start", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(RuntimeCliWrapperPaths.PreferredWrapperPath(candidateDistRoot), "pilot", "start", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(RuntimeCliWrapperPaths.PreferredWrapperPath(candidateDistRoot), "pilot", "problem-intake", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(RuntimeCliWrapperPaths.PreferredWrapperPath(candidateDistRoot), "pilot", "triage", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(RuntimeCliWrapperPaths.PreferredWrapperPath(candidateDistRoot), "pilot", "follow-up", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(RuntimeCliWrapperPaths.PreferredWrapperPath(candidateDistRoot), "pilot", "follow-up-plan", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(RuntimeCliWrapperPaths.PreferredWrapperPath(candidateDistRoot), "pilot", "follow-up-record", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(RuntimeCliWrapperPaths.PreferredWrapperPath(candidateDistRoot), "pilot", "follow-up-intake", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(RuntimeCliWrapperPaths.PreferredWrapperPath(candidateDistRoot), "pilot", "follow-up-gate", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(RuntimeCliWrapperPaths.PreferredWrapperPath(candidateDistRoot), "pilot", "dist-smoke", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(RuntimeCliWrapperPaths.PreferredWrapperPath(candidateDistRoot), "pilot", "dist-binding", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(RuntimeCliWrapperPaths.PreferredWrapperPath(candidateDistRoot), "pilot", "dist", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(RuntimeCliWrapperPaths.PreferredWrapperPath(candidateDistRoot), "pilot", "target-proof", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(RuntimeCliWrapperPaths.PreferredWrapperPath(candidateDistRoot), "pilot", "next", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(RuntimeCliWrapperPaths.PreferredWrapperPath(candidateDistRoot), "pilot", "ignore-plan", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(RuntimeCliWrapperPaths.PreferredWrapperPath(candidateDistRoot), "pilot", "ignore-record", "--json"),
                RuntimeCliWrapperPaths.FormatShellCommand(RuntimeCliWrapperPaths.PreferredWrapperPath(candidateDistRoot), "pilot", "proof", "--json"),
            ],
            BoundaryRules =
            [
                "Dist freshness smoke is read-only; it does not run pack, copy files, or mutate a target repo.",
                "A dist is fresh only when its manifest source commit matches the source repo HEAD and the source worktree is clean.",
                "The frozen local dist must not contain .git, CARVES.Runtime.sln, bin, obj, task truth, runtime live state, or platform state.",
                "External targets should use dist-binding and local-dist handoff only after this smoke readback is ready.",
                "This surface does not replace target commit closure, target dist binding, local dist handoff, or product pilot proof.",
            ],
            Gaps = gaps,
            Summary = BuildSummary(ready, sourceGit, candidateDistExists, candidateDistHasManifest, candidateDistHasVersion, candidateDistHasWrapper, candidateDistHasPublishedCli, manifestSourceCommitMatchesSourceHead),
            RecommendedNextAction = BuildRecommendedNextAction(ready, sourceGit, candidateDistExists, candidateDistHasManifest, candidateDistHasVersion, candidateDistHasWrapper, candidateDistHasPublishedCli, manifestSourceCommitMatchesSourceHead),
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not create, refresh, copy, delete, pack, publish, sign, or release a Runtime dist.",
                "This surface does not initialize, repair, or retarget target repo runtime bindings.",
                "This surface does not mutate PATH, shell profiles, aliases, tool installs, staging, commits, pushes, tags, or releases.",
                "This surface does not claim public package distribution, signed installers, automatic updates, OS sandboxing, full ACP, full MCP, or remote worker orchestration.",
            ],
        };
    }

    private string ResolveSourceRepoRoot(string runtimeRoot)
    {
        if (File.Exists(Path.Combine(runtimeRoot, "CARVES.Runtime.sln")))
        {
            return runtimeRoot;
        }

        var manifestSourceRoot = ReadManifestSourceRepoRoot(Path.Combine(runtimeRoot, "MANIFEST.json"));
        if (!string.IsNullOrWhiteSpace(manifestSourceRoot))
        {
            return Path.GetFullPath(manifestSourceRoot);
        }

        return runtimeRoot;
    }

    private static string ResolveCandidateDistRoot(string runtimeRoot, string sourceRepoRoot)
    {
        if (File.Exists(Path.Combine(runtimeRoot, "MANIFEST.json"))
            && File.Exists(Path.Combine(runtimeRoot, "VERSION")))
        {
            return runtimeRoot;
        }

        var siblingRoot = Directory.GetParent(sourceRepoRoot)?.FullName ?? sourceRepoRoot;
        return Path.GetFullPath(Path.Combine(siblingRoot, ".dist", $"CARVES.Runtime-{DistVersion}"));
    }

    private static RuntimeDistFreshnessManifest ReadManifest(string manifestPath, List<string> errors)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;
            return new RuntimeDistFreshnessManifest(
                ReadString(root, "schema_version"),
                ReadString(root, "version"),
                ReadString(root, "source_commit"),
                ReadString(root, "source_repo_root"),
                ReadString(root, "output_path"),
                ReadString(root, "published_cli_entry"));
        }
        catch (JsonException exception)
        {
            errors.Add($"Unable to parse Runtime dist manifest '{manifestPath}': {exception.Message}");
            return RuntimeDistFreshnessManifest.Empty;
        }
        catch (IOException exception)
        {
            errors.Add($"Unable to read Runtime dist manifest '{manifestPath}': {exception.Message}");
            return RuntimeDistFreshnessManifest.Empty;
        }
    }

    private static string ReadManifestSourceRepoRoot(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            return ReadString(document.RootElement, "source_repo_root");
        }
        catch
        {
            return string.Empty;
        }
    }

    private static RuntimeSourceGitRead ReadSourceGit(string sourceRepoRoot)
    {
        if (string.IsNullOrWhiteSpace(sourceRepoRoot)
            || !Directory.Exists(sourceRepoRoot)
            || (!Directory.Exists(Path.Combine(sourceRepoRoot, ".git")) && !File.Exists(Path.Combine(sourceRepoRoot, ".git"))))
        {
            return RuntimeSourceGitRead.Unavailable;
        }

        try
        {
            var head = RunGit(sourceRepoRoot, "rev-parse", "HEAD");
            if (head.ExitCode != 0)
            {
                return RuntimeSourceGitRead.Unavailable;
            }

            var status = RunGit(sourceRepoRoot, "status", "--porcelain=v1", "--untracked-files=all");
            if (status.ExitCode != 0)
            {
                return new RuntimeSourceGitRead(true, head.StandardOutput.Trim(), false);
            }

            return new RuntimeSourceGitRead(true, head.StandardOutput.Trim(), string.IsNullOrWhiteSpace(status.StandardOutput));
        }
        catch
        {
            return RuntimeSourceGitRead.Unavailable;
        }
    }

    private static GitCommandResult RunGit(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start git process.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new GitCommandResult(process.ExitCode, standardOutput, standardError);
    }

    private static IEnumerable<string> BuildGaps(
        IReadOnlyList<string> errors,
        bool sourceRepoRootExists,
        RuntimeSourceGitRead sourceGit,
        bool candidateDistExists,
        bool candidateDistHasManifest,
        bool candidateDistHasVersion,
        bool candidateDistHasWrapper,
        bool candidateDistHasPublishedCli,
        bool candidateDistHasPhaseDocument,
        bool candidateDistHasGuideDocument,
        bool candidateDistHasTargetBindingGuide,
        bool candidateDistHasLocalDistGuide,
        bool candidateDistHasCliDistributionGuide,
        bool candidateDistHasCurrentProductClosureDocument,
        bool candidateDistHasCurrentProductClosureGuide,
        bool candidateDistHasGitDirectory,
        bool candidateDistHasSolution,
        bool manifestVersionMatchesVersionFile,
        bool manifestOutputMatchesCandidateDist,
        bool manifestPublishedCliEntryMatchesPublishedCli,
        bool manifestSourceCommitMatchesSourceHead)
    {
        foreach (var error in errors)
        {
            yield return $"missing_or_invalid_dist_smoke_resource:{error}";
        }

        if (!sourceRepoRootExists)
        {
            yield return "source_repo_root_missing";
        }

        if (!sourceGit.HeadDetected)
        {
            yield return "source_git_head_not_detected";
        }

        if (sourceGit.HeadDetected && !sourceGit.WorktreeClean)
        {
            yield return "source_git_worktree_not_clean";
        }

        if (!candidateDistExists)
        {
            yield return "candidate_dist_root_missing";
        }

        if (!candidateDistHasManifest)
        {
            yield return "candidate_dist_manifest_missing";
        }

        if (!candidateDistHasVersion)
        {
            yield return "candidate_dist_version_missing";
        }

        if (!candidateDistHasWrapper)
        {
            yield return "candidate_dist_wrapper_missing";
        }

        if (!candidateDistHasPublishedCli)
        {
            yield return "candidate_dist_published_cli_missing";
        }

        if (!candidateDistHasPhaseDocument)
        {
            yield return "candidate_dist_phase22_document_missing";
        }

        if (!candidateDistHasGuideDocument)
        {
            yield return "candidate_dist_freshness_guide_missing";
        }

        if (!candidateDistHasTargetBindingGuide)
        {
            yield return "candidate_dist_target_binding_guide_missing";
        }

        if (!candidateDistHasLocalDistGuide)
        {
            yield return "candidate_dist_local_dist_guide_missing";
        }

        if (!candidateDistHasCliDistributionGuide)
        {
            yield return "candidate_dist_cli_distribution_guide_missing";
        }

        if (!candidateDistHasCurrentProductClosureDocument)
        {
            yield return "candidate_dist_current_product_closure_document_missing";
        }

        if (!candidateDistHasCurrentProductClosureGuide)
        {
            yield return "candidate_dist_current_product_closure_guide_missing";
        }

        if (candidateDistHasGitDirectory)
        {
            yield return "candidate_dist_contains_git_metadata";
        }

        if (candidateDistHasSolution)
        {
            yield return "candidate_dist_contains_source_solution";
        }

        if (!manifestVersionMatchesVersionFile)
        {
            yield return "manifest_version_does_not_match_version_file";
        }

        if (!manifestOutputMatchesCandidateDist)
        {
            yield return "manifest_output_path_does_not_match_candidate_dist";
        }

        if (!manifestPublishedCliEntryMatchesPublishedCli)
        {
            yield return "manifest_published_cli_entry_does_not_match_dist_contract";
        }

        if (sourceGit.HeadDetected && !manifestSourceCommitMatchesSourceHead)
        {
            yield return "manifest_source_commit_does_not_match_source_head";
        }
    }

    private static string ResolvePosture(
        int errorCount,
        bool ready,
        RuntimeSourceGitRead sourceGit,
        bool candidateDistExists,
        bool candidateDistHasManifest,
        bool candidateDistHasVersion,
        bool candidateDistHasWrapper,
        bool candidateDistHasPublishedCli,
        bool manifestSourceCommitMatchesSourceHead)
    {
        if (ready)
        {
            return "local_dist_freshness_smoke_ready";
        }

        if (errorCount > 0 || !candidateDistExists || !candidateDistHasManifest || !candidateDistHasVersion || !candidateDistHasWrapper || !candidateDistHasPublishedCli)
        {
            return "local_dist_freshness_smoke_blocked_by_missing_dist_resources";
        }

        if (sourceGit.HeadDetected && !sourceGit.WorktreeClean)
        {
            return "local_dist_freshness_smoke_blocked_by_dirty_source";
        }

        if (sourceGit.HeadDetected && !manifestSourceCommitMatchesSourceHead)
        {
            return "local_dist_freshness_smoke_dist_stale";
        }

        return "local_dist_freshness_smoke_blocked_by_dist_contract_gap";
    }

    private static string BuildSummary(
        bool ready,
        RuntimeSourceGitRead sourceGit,
        bool candidateDistExists,
        bool candidateDistHasManifest,
        bool candidateDistHasVersion,
        bool candidateDistHasWrapper,
        bool candidateDistHasPublishedCli,
        bool manifestSourceCommitMatchesSourceHead)
    {
        if (ready)
        {
            return "Local Runtime dist freshness smoke is ready: the frozen dist exists, includes current product closure resources, and its manifest source commit matches the clean source HEAD.";
        }

        if (!candidateDistExists || !candidateDistHasManifest || !candidateDistHasVersion || !candidateDistHasWrapper || !candidateDistHasPublishedCli)
        {
            return "Local Runtime dist freshness smoke is blocked until the frozen dist root, manifest, version, wrapper, and published CLI are present.";
        }

        if (sourceGit.HeadDetected && !sourceGit.WorktreeClean)
        {
            return "The source worktree is dirty; refresh the dist only after committing or explicitly accepting a dirty diagnostic pack.";
        }

        if (sourceGit.HeadDetected && !manifestSourceCommitMatchesSourceHead)
        {
            return "The local Runtime dist is stale because its manifest source commit does not match the source HEAD.";
        }

        return "Local Runtime dist freshness smoke is blocked by a distribution contract gap.";
    }

    private static string BuildRecommendedNextAction(
        bool ready,
        RuntimeSourceGitRead sourceGit,
        bool candidateDistExists,
        bool candidateDistHasManifest,
        bool candidateDistHasVersion,
        bool candidateDistHasWrapper,
        bool candidateDistHasPublishedCli,
        bool manifestSourceCommitMatchesSourceHead)
    {
        if (ready)
        {
            return "Run carves pilot dist-binding --json, then use the dist wrapper for external target binding.";
        }

        if (!candidateDistExists || !candidateDistHasManifest || !candidateDistHasVersion || !candidateDistHasWrapper || !candidateDistHasPublishedCli)
        {
            return $".\\scripts\\pack-runtime-dist.ps1 -Version {DistVersion} -Force";
        }

        if (sourceGit.HeadDetected && !sourceGit.WorktreeClean)
        {
            return "Commit or intentionally discard source worktree changes, then rerun carves pilot dist-smoke --json.";
        }

        if (sourceGit.HeadDetected && !manifestSourceCommitMatchesSourceHead)
        {
            return $".\\scripts\\pack-runtime-dist.ps1 -Version {DistVersion} -Force";
        }

        return "Restore the dist contract resources, then rerun carves pilot dist-smoke --json.";
    }

    private void ValidateRuntimeDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = ToFullPath(documentRoot.DocumentRoot, repoRelativePath);
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }

    private static string ToFullPath(string root, string repoRelativePath)
    {
        return Path.Combine(root, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool PathEquals(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private sealed record RuntimeDistFreshnessManifest(
        string SchemaVersion,
        string Version,
        string SourceCommit,
        string SourceRepoRoot,
        string OutputPath,
        string PublishedCliEntry)
    {
        public static RuntimeDistFreshnessManifest Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    }

    private sealed record RuntimeSourceGitRead(bool HeadDetected, string Head, bool WorktreeClean)
    {
        public static RuntimeSourceGitRead Unavailable { get; } = new(false, string.Empty, false);
    }

    private sealed record GitCommandResult(int ExitCode, string StandardOutput, string StandardError);
}

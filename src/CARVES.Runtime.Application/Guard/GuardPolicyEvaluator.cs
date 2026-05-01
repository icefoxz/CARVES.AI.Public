namespace Carves.Runtime.Application.Guard;

public sealed class GuardPolicyEvaluator
{
    public GuardDecision Evaluate(GuardDiffContext context, string runId)
    {
        var findings = new FindingBuilder(runId);
        EvaluateDiagnostics(context, findings);
        EvaluateDiff(context, findings);
        EvaluatePathPolicy(context, findings);
        EvaluateBudget(context, findings);
        EvaluateDependencyPolicy(context, findings);
        EvaluateChangeShape(context, findings);
        return findings.BuildDecision(context.Policy.PolicyId);
    }

    private static void EvaluateDiagnostics(GuardDiffContext context, FindingBuilder findings)
    {
        foreach (var diagnostic in context.Diagnostics ?? Array.Empty<GuardDiffDiagnostic>())
        {
            if (diagnostic.Severity == GuardSeverity.Block)
            {
                findings.AddBlock(diagnostic.RuleId, diagnostic.Message, null, diagnostic.Evidence);
                continue;
            }

            findings.AddWarning(diagnostic.RuleId, diagnostic.Message, null, diagnostic.Evidence);
        }
    }

    private static void EvaluateDiff(GuardDiffContext context, FindingBuilder findings)
    {
        if (context.ChangedFiles.Count == 0)
        {
            findings.AddBlock("diff.empty", "No changed files were found.", null, "git diff and git status returned no changed files");
            return;
        }

        // Untracked files are first-class diff inputs; their presence is not itself a review condition.
    }

    private static void EvaluatePathPolicy(GuardDiffContext context, FindingBuilder findings)
    {
        foreach (var file in context.ChangedFiles)
        {
            if (file.MatchesProtectedPath)
            {
                AddActionFinding(
                    context.Policy.PathPolicy.ProtectedPathAction,
                    findings,
                    "path.protected_prefix",
                    "Patch touches a protected path.",
                    file.Path,
                    $"path matched protected prefix {file.MatchedProtectedPrefix}");
                continue;
            }

            if (!file.MatchesAllowedPath)
            {
                AddActionFinding(
                    context.Policy.PathPolicy.OutsideAllowedAction,
                    findings,
                    "path.outside_allowed_prefix",
                    "Patch touches a path outside allowed prefixes.",
                    file.Path,
                    "path did not match any allowed prefix");
            }
        }
    }

    private static void EvaluateBudget(GuardDiffContext context, FindingBuilder findings)
    {
        var budget = context.Policy.ChangeBudget;
        if (context.PatchStats.ChangedFileCount > budget.MaxChangedFiles)
        {
            findings.AddBlock(
                "budget.max_changed_files",
                "Patch changes too many files.",
                null,
                $"{context.PatchStats.ChangedFileCount} files changed; budget is {budget.MaxChangedFiles}");
        }

        if (budget.MaxTotalAdditions is { } maxTotalAdditions && context.PatchStats.TotalAdditions > maxTotalAdditions)
        {
            findings.AddBlock(
                "budget.max_total_additions",
                "Patch adds too many lines.",
                null,
                $"{context.PatchStats.TotalAdditions} additions; budget is {maxTotalAdditions}");
        }

        if (budget.MaxTotalDeletions is { } maxTotalDeletions && context.PatchStats.TotalDeletions > maxTotalDeletions)
        {
            findings.AddBlock(
                "budget.max_total_deletions",
                "Patch deletes too many lines.",
                null,
                $"{context.PatchStats.TotalDeletions} deletions; budget is {maxTotalDeletions}");
        }

        if (budget.MaxRenames is { } maxRenames && context.PatchStats.RenamedFileCount > maxRenames)
        {
            findings.AddBlock(
                "budget.max_renames",
                "Patch renames too many files.",
                null,
                $"{context.PatchStats.RenamedFileCount} renames; budget is {maxRenames}");
        }

        foreach (var file in context.ChangedFiles)
        {
            if (budget.MaxFileAdditions is { } maxFileAdditions && file.Additions > maxFileAdditions)
            {
                findings.AddBlock(
                    "budget.max_file_additions",
                    "File adds too many lines.",
                    file.Path,
                    $"{file.Additions} additions; budget is {maxFileAdditions}");
            }

            if (budget.MaxFileDeletions is { } maxFileDeletions && file.Deletions > maxFileDeletions)
            {
                findings.AddBlock(
                    "budget.max_file_deletions",
                    "File deletes too many lines.",
                    file.Path,
                    $"{file.Deletions} deletions; budget is {maxFileDeletions}");
            }
        }
    }

    private static void EvaluateDependencyPolicy(GuardDiffContext context, FindingBuilder findings)
    {
        var policy = context.Policy.DependencyPolicy;
        var changedManifests = context.ChangedFiles
            .Where(file => MatchesAnyPattern(file.Path, policy.ManifestPaths, context.Policy.PathPolicy.CaseSensitive))
            .ToArray();
        var changedLockfiles = context.ChangedFiles
            .Where(file => MatchesAnyPattern(file.Path, policy.LockfilePaths, context.Policy.PathPolicy.CaseSensitive))
            .ToArray();

        if (changedManifests.Length > 0 && changedLockfiles.Length == 0)
        {
            foreach (var file in changedManifests)
            {
                AddActionFinding(
                    policy.ManifestWithoutLockfileAction,
                    findings,
                    "dependency.manifest_without_lockfile",
                    "Dependency manifest changed without a matching lockfile change.",
                    file.Path,
                    "manifest changed and no lockfile changed in the same patch");
                AddActionFinding(
                    policy.NewDependencyAction,
                    findings,
                    "dependency.new_dependency.unverified",
                    "Dependency manifest changed and new dependency detection was not verified.",
                    file.Path,
                    "manifest changed; inspect dependency diff before merge");
            }
        }

        if (changedLockfiles.Length > 0 && changedManifests.Length == 0)
        {
            foreach (var file in changedLockfiles)
            {
                AddActionFinding(
                    policy.LockfileWithoutManifestAction,
                    findings,
                    "dependency.lockfile_without_manifest",
                    "Dependency lockfile changed without a manifest change.",
                    file.Path,
                    "lockfile changed and no manifest changed in the same patch");
            }
        }
    }

    private static void EvaluateChangeShape(GuardDiffContext context, FindingBuilder findings)
    {
        var policy = context.Policy.ChangeShape;
        foreach (var file in context.ChangedFiles)
        {
            if (!policy.AllowRenameWithContentChange
                && file.Status == GuardFileChangeStatus.Renamed
                && (file.Additions > 0 || file.Deletions > 0))
            {
                findings.AddBlock(
                    "shape.rename_with_content_change",
                    "Patch renames a file and changes content in the same entry.",
                    file.Path,
                    "rename entry has additions or deletions");
            }

            if (!policy.AllowDeleteWithoutReplacement
                && file.Status == GuardFileChangeStatus.Deleted
                && !HasReplacementCandidate(context, file))
            {
                findings.AddBlock(
                    "shape.delete_without_replacement",
                    "Patch deletes a file without a credible replacement candidate.",
                    file.Path,
                    "deleted file has no same-path, explicit-rename, same-filename, or same-directory same-extension related-stem replacement candidate");
            }

            if (MatchesAnyPrefix(file.Path, policy.GeneratedPathPrefixes, context.Policy.PathPolicy.CaseSensitive))
            {
                AddActionFinding(
                    policy.GeneratedPathAction,
                    findings,
                    "shape.generated_path",
                    "Patch touches generated output.",
                    file.Path,
                    "path matched generated output prefix");
            }
        }

        if (policy.RequireTestsForSourceChanges)
        {
            var changedSource = context.ChangedFiles.Any(file => MatchesAnyPrefix(file.Path, policy.SourcePathPrefixes, context.Policy.PathPolicy.CaseSensitive));
            var changedTests = context.ChangedFiles.Any(file => MatchesAnyPrefix(file.Path, policy.TestPathPrefixes, context.Policy.PathPolicy.CaseSensitive));
            if (changedSource && !changedTests)
            {
                AddActionFinding(
                    policy.MissingTestsAction,
                    findings,
                    "shape.missing_tests_for_source_changes",
                    "Source files changed without a test change.",
                    null,
                    "source path changed and no configured test path changed");
            }
        }

        var hasRename = context.ChangedFiles.Any(file => file.Status == GuardFileChangeStatus.Renamed);
        var hasSourceEdit = context.ChangedFiles.Any(file =>
            file.Status is GuardFileChangeStatus.Added or GuardFileChangeStatus.Modified
            && MatchesAnyPrefix(file.Path, policy.SourcePathPrefixes, context.Policy.PathPolicy.CaseSensitive));
        if (hasRename && hasSourceEdit)
        {
            AddActionFinding(
                policy.MixedFeatureAndRefactorAction,
                findings,
                "shape.mixed_feature_and_refactor",
                "Patch mixes structural rename work with source edits.",
                null,
                "rename entries and source edits appeared in the same patch");
        }
    }

    private static void AddActionFinding(
        GuardPolicyAction action,
        FindingBuilder findings,
        string ruleId,
        string message,
        string? filePath,
        string evidence)
    {
        switch (action)
        {
            case GuardPolicyAction.Block:
                findings.AddBlock(ruleId, message, filePath, evidence);
                break;
            case GuardPolicyAction.Review:
                findings.AddWarning(ruleId, message, filePath, evidence);
                break;
        }
    }

    private static bool MatchesAnyPrefix(string path, IReadOnlyList<string> prefixes, bool caseSensitive)
    {
        return prefixes.Any(prefix => GuardDiffAdapter.PathMatches(path, prefix, caseSensitive));
    }

    private static bool MatchesAnyPattern(string path, IReadOnlyList<string> patterns, bool caseSensitive)
    {
        return patterns.Any(pattern => GuardDiffAdapter.GlobMatches(path, pattern, caseSensitive));
    }

    private static bool HasReplacementCandidate(GuardDiffContext context, GuardChangedFile deletedFile)
    {
        return context.ChangedFiles.Any(candidate =>
            IsReplacementCandidate(deletedFile, candidate, context.Policy.PathPolicy.CaseSensitive));
    }

    private static bool IsReplacementCandidate(GuardChangedFile deletedFile, GuardChangedFile candidate, bool caseSensitive)
    {
        if (candidate.Status == GuardFileChangeStatus.Renamed
            && !string.IsNullOrWhiteSpace(candidate.OldPath)
            && PathEquals(candidate.OldPath, deletedFile.Path, caseSensitive))
        {
            return true;
        }

        if (candidate.Status != GuardFileChangeStatus.Added)
        {
            return false;
        }

        if (PathEquals(candidate.Path, deletedFile.Path, caseSensitive))
        {
            return true;
        }

        var deletedExtension = GetExtension(deletedFile.Path);
        if (string.IsNullOrWhiteSpace(deletedExtension)
            || !PathEquals(deletedExtension, GetExtension(candidate.Path), caseSensitive))
        {
            return false;
        }

        if (PathEquals(GetFileName(deletedFile.Path), GetFileName(candidate.Path), caseSensitive))
        {
            return true;
        }

        return PathEquals(GetDirectory(deletedFile.Path), GetDirectory(candidate.Path), caseSensitive)
               && StemsAreRelated(
                   GetFileNameWithoutExtension(deletedFile.Path),
                   GetFileNameWithoutExtension(candidate.Path),
                   caseSensitive);
    }

    private static bool StemsAreRelated(string deletedStem, string candidateStem, bool caseSensitive)
    {
        if (string.IsNullOrWhiteSpace(deletedStem) || string.IsNullOrWhiteSpace(candidateStem))
        {
            return false;
        }

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (string.Equals(deletedStem, candidateStem, comparison))
        {
            return true;
        }

        return StartsWithNameBoundary(candidateStem, deletedStem, comparison)
               || StartsWithNameBoundary(deletedStem, candidateStem, comparison);
    }

    private static bool StartsWithNameBoundary(string value, string prefix, StringComparison comparison)
    {
        return value.Length > prefix.Length
               && value.StartsWith(prefix, comparison)
               && IsNameBoundary(value[prefix.Length]);
    }

    private static bool IsNameBoundary(char value)
    {
        return !char.IsLetter(value);
    }

    private static bool PathEquals(string? left, string? right, bool caseSensitive)
    {
        return string.Equals(left, right, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDirectory(string path)
    {
        var index = path.LastIndexOf('/');
        return index < 0 ? string.Empty : path[..index];
    }

    private static string GetFileName(string path)
    {
        var index = path.LastIndexOf('/');
        return index < 0 ? path : path[(index + 1)..];
    }

    private static string GetExtension(string path)
    {
        var fileName = GetFileName(path);
        var index = fileName.LastIndexOf('.');
        return index <= 0 || index == fileName.Length - 1 ? string.Empty : fileName[index..];
    }

    private static string GetFileNameWithoutExtension(string path)
    {
        var fileName = GetFileName(path);
        var index = fileName.LastIndexOf('.');
        return index <= 0 ? fileName : fileName[..index];
    }

    private sealed class FindingBuilder
    {
        private readonly string runId;
        private readonly List<GuardViolation> violations = [];
        private readonly List<GuardWarning> warnings = [];

        public FindingBuilder(string runId)
        {
            this.runId = runId;
        }

        public void AddBlock(string ruleId, string message, string? filePath, string evidence)
        {
            violations.Add(new GuardViolation(
                ruleId,
                GuardSeverity.Block,
                message,
                filePath,
                evidence,
                $"guard-rule:{runId}:{ruleId}:{violations.Count}"));
        }

        public void AddWarning(string ruleId, string message, string? filePath, string evidence)
        {
            warnings.Add(new GuardWarning(
                ruleId,
                message,
                filePath,
                evidence,
                $"guard-rule:{runId}:{ruleId}:{warnings.Count}"));
        }

        public GuardDecision BuildDecision(string policyId)
        {
            var outcome = violations.Count > 0
                ? GuardDecisionOutcome.Block
                : warnings.Count > 0
                    ? GuardDecisionOutcome.Review
                    : GuardDecisionOutcome.Allow;
            var evidenceRefs = new List<string>
            {
                $"guard-run:{runId}",
                $"guard-policy:{policyId}",
            };
            evidenceRefs.AddRange(violations.Select(violation => violation.EvidenceRef));
            evidenceRefs.AddRange(warnings.Select(warning => warning.EvidenceRef));
            var summary = outcome switch
            {
                GuardDecisionOutcome.Allow => "Patch allowed by Alpha Guard policy.",
                GuardDecisionOutcome.Review => "Patch requires review by Alpha Guard policy.",
                GuardDecisionOutcome.Block => "Patch blocked by Alpha Guard policy.",
                _ => "Patch evaluated by Alpha Guard policy.",
            };
            return new GuardDecision(
                runId,
                outcome,
                policyId,
                summary,
                violations,
                warnings,
                evidenceRefs,
                RequiresRuntimeTaskTruth: false);
        }
    }
}

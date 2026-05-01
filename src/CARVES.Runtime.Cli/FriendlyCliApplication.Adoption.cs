using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Carves.Runtime.Cli;

internal static partial class FriendlyCliApplication
{
    private const string P1ProbeSchemaVersion = "0.3.2";
    private const string P1ProbePhase = "P1_PROBE";
    private const string P1ProbeCommand = "carves adoption probe --json";
    private const string P1ProbeErrorNamespace = "legacy_adoption.p1_probe";
    private const string P2AttachSchemaVersion = "legacy_adoption.p2_attach_result.v0.1.0";
    private const string P2AttachManifestSchemaVersion = "legacy_adoption.attach_manifest.v0.1.0";
    private const string P2AttachEventSchemaVersion = "legacy_adoption.attach_event.v0.1.0";
    private const string P2AttachLedgerSchemaVersion = "legacy_adoption.ownership_ledger.v0.1.0";
    private const string P2AttachPhase = "P2_ATTACH";
    private const string P2AttachCommand = "carves adoption attach --json";
    private const string P2AttachErrorNamespace = "legacy_adoption.p2_attach";
    private const string P3IntakeSchemaVersion = "legacy_adoption.p3_intake_result.v0.1.0";
    private const string P3IntakeRepoScanSchemaVersion = "legacy_adoption.p3_repo_scan_evidence.v0.1.0";
    private const string P3IntakeInstructionScanSchemaVersion = "legacy_adoption.p3_instruction_scan_evidence.v0.1.0";
    private const string P3IntakePhase = "P3_INTAKE";
    private const string P3IntakeCommand = "carves adoption intake --json";
    private const string P3IntakeErrorNamespace = "legacy_adoption.p3_intake";
    private const string P4ProposeSchemaVersion = "legacy_adoption.p4_propose_result.v0.1.0";
    private const string P4ProposalSetSchemaVersion = "legacy_adoption.p4_proposal_set.v0.1.0";
    private const string P4MemoryProposalSchemaVersion = "legacy_adoption.p4_memory_proposal.v0.1.0";
    private const string P4TaskGraphDraftSchemaVersion = "legacy_adoption.p4_taskgraph_draft.v0.1.0";
    private const string P4CodeGraphSnapshotSchemaVersion = "legacy_adoption.p4_codegraph_snapshot.v0.1.0";
    private const string P4RefactorCandidatesSchemaVersion = "legacy_adoption.p4_refactor_candidates.v0.1.0";
    private const string P4ProposePhase = "P4_PROPOSALS";
    private const string P4ProposeCommand = "carves adoption propose --json";
    private const string P4ProposeErrorNamespace = "legacy_adoption.p4_proposals";
    private const string P5CleanupDetachSchemaVersion = "legacy_adoption.p5_cleanup_detach_result.v0.1.0";
    private const string P5ReferenceIndexSchemaVersion = "legacy_adoption.p5_reference_index.v0.1.0";
    private const string P5CleanupPlanSchemaVersion = "legacy_adoption.p5_cleanup_plan.v0.1.0";
    private const string P5CleanupProofSchemaVersion = "legacy_adoption.p5_cleanup_proof.v0.1.0";
    private const string P5CleanupDetachPhase = "P5_CLEANUP_DETACH";
    private const string P5PlanCleanupCommand = "carves adoption plan-cleanup --json";
    private const string P5CleanupApplyCommand = "carves adoption cleanup --apply <plan_id> --plan-hash <sha256> --json";
    private const string P5DetachCommand = "carves adoption detach --json";
    private const string P5DetachWithCleanupCommand = "carves adoption detach --cleanup <plan_id> --plan-hash <sha256> --json";
    private const string P5DetachExportCommand = "carves adoption detach --export <archive_path> --json";
    private const string P5CleanupDetachErrorNamespace = "legacy_adoption.p5_cleanup_detach";

    private static readonly JsonSerializerOptions AdoptionJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static int RunAdoption(string? repoRootOverride, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 2
            && string.Equals(arguments[0], "probe", StringComparison.OrdinalIgnoreCase)
            && string.Equals(arguments[1], "--json", StringComparison.OrdinalIgnoreCase))
        {
            return RunAdoptionProbeJson(repoRootOverride);
        }

        if (arguments.Count == 2
            && string.Equals(arguments[0], "attach", StringComparison.OrdinalIgnoreCase)
            && string.Equals(arguments[1], "--json", StringComparison.OrdinalIgnoreCase))
        {
            return RunAdoptionAttachJson(repoRootOverride);
        }

        if (arguments.Count == 2
            && string.Equals(arguments[0], "intake", StringComparison.OrdinalIgnoreCase)
            && string.Equals(arguments[1], "--json", StringComparison.OrdinalIgnoreCase))
        {
            return RunAdoptionIntakeJson(repoRootOverride);
        }

        if (arguments.Count == 2
            && string.Equals(arguments[0], "propose", StringComparison.OrdinalIgnoreCase)
            && string.Equals(arguments[1], "--json", StringComparison.OrdinalIgnoreCase))
        {
            return RunAdoptionProposeJson(repoRootOverride);
        }

        if (arguments.Count == 2
            && string.Equals(arguments[0], "plan-cleanup", StringComparison.OrdinalIgnoreCase)
            && string.Equals(arguments[1], "--json", StringComparison.OrdinalIgnoreCase))
        {
            return RunAdoptionPlanCleanupJson(repoRootOverride);
        }

        if (arguments.Count == 6
            && string.Equals(arguments[0], "cleanup", StringComparison.OrdinalIgnoreCase)
            && string.Equals(arguments[1], "--apply", StringComparison.OrdinalIgnoreCase)
            && string.Equals(arguments[3], "--plan-hash", StringComparison.OrdinalIgnoreCase)
            && string.Equals(arguments[5], "--json", StringComparison.OrdinalIgnoreCase))
        {
            return RunAdoptionCleanupApplyJson(repoRootOverride, arguments[2], arguments[4]);
        }

        if (arguments.Count == 2
            && string.Equals(arguments[0], "detach", StringComparison.OrdinalIgnoreCase)
            && string.Equals(arguments[1], "--json", StringComparison.OrdinalIgnoreCase))
        {
            return RunAdoptionDetachJson(repoRootOverride, cleanupPlanId: null, planHash: null, exportPath: null);
        }

        if (arguments.Count == 6
            && string.Equals(arguments[0], "detach", StringComparison.OrdinalIgnoreCase)
            && string.Equals(arguments[1], "--cleanup", StringComparison.OrdinalIgnoreCase)
            && string.Equals(arguments[3], "--plan-hash", StringComparison.OrdinalIgnoreCase)
            && string.Equals(arguments[5], "--json", StringComparison.OrdinalIgnoreCase))
        {
            return RunAdoptionDetachJson(repoRootOverride, arguments[2], arguments[4], exportPath: null);
        }

        if (arguments.Count == 4
            && string.Equals(arguments[0], "detach", StringComparison.OrdinalIgnoreCase)
            && string.Equals(arguments[1], "--export", StringComparison.OrdinalIgnoreCase)
            && string.Equals(arguments[3], "--json", StringComparison.OrdinalIgnoreCase))
        {
            return RunAdoptionDetachJson(repoRootOverride, cleanupPlanId: null, planHash: null, arguments[2]);
        }

        Console.Error.WriteLine("Usage: carves adoption probe --json");
        Console.Error.WriteLine("       carves adoption attach --json");
        Console.Error.WriteLine("       carves adoption intake --json");
        Console.Error.WriteLine("       carves adoption propose --json");
        Console.Error.WriteLine("       carves adoption plan-cleanup --json");
        Console.Error.WriteLine("       carves adoption cleanup --apply <plan_id> --plan-hash <sha256> --json");
        Console.Error.WriteLine("       carves adoption detach --json");
        Console.Error.WriteLine("       carves adoption detach --cleanup <plan_id> --plan-hash <sha256> --json");
        Console.Error.WriteLine("       carves adoption detach --export <archive_path> --json");
        return 2;
    }

    private static int RunAdoptionAttachJson(string? repoRootOverride)
    {
        var targetRoot = ResolveProbeTargetRoot(repoRootOverride);
        if (targetRoot is null || !Directory.Exists(targetRoot) || !RepoLocator.IsGitRepository(targetRoot))
        {
            WriteJson(BuildAttachReport(
                result: "blocked",
                status: "NOT_A_GIT_REPOSITORY",
                exitCode: 10,
                adoptionId: null,
                message: "Target is not a git repository.",
                repoRoot: targetRoot,
                persistentArtifacts: Array.Empty<string>()));
            return 10;
        }

        var service = new LegacyAdoptionAttachService(targetRoot);
        var result = service.Attach();
        WriteJson(result.Payload);
        return result.ExitCode;
    }

    private static int RunAdoptionIntakeJson(string? repoRootOverride)
    {
        var targetRoot = ResolveProbeTargetRoot(repoRootOverride);
        if (targetRoot is null || !Directory.Exists(targetRoot) || !RepoLocator.IsGitRepository(targetRoot))
        {
            WriteJson(BuildIntakeReport(
                result: "blocked",
                status: "NOT_A_GIT_REPOSITORY",
                exitCode: 10,
                adoptionId: null,
                generation: null,
                message: "Target is not a git repository.",
                repoRoot: targetRoot,
                evidenceArtifacts: Array.Empty<string>(),
                proposalArtifacts: Array.Empty<string>()));
            return 10;
        }

        var service = new LegacyAdoptionIntakeService(targetRoot);
        var result = service.Intake();
        WriteJson(result.Payload);
        return result.ExitCode;
    }

    private static int RunAdoptionProposeJson(string? repoRootOverride)
    {
        var targetRoot = ResolveProbeTargetRoot(repoRootOverride);
        if (targetRoot is null || !Directory.Exists(targetRoot) || !RepoLocator.IsGitRepository(targetRoot))
        {
            WriteJson(BuildProposeReport(
                result: "blocked",
                status: "NOT_A_GIT_REPOSITORY",
                exitCode: 10,
                adoptionId: null,
                generation: null,
                message: "Target is not a git repository.",
                repoRoot: targetRoot,
                proposalArtifacts: Array.Empty<string>()));
            return 10;
        }

        var service = new LegacyAdoptionProposalService(targetRoot);
        var result = service.Propose();
        WriteJson(result.Payload);
        return result.ExitCode;
    }

    private static int RunAdoptionPlanCleanupJson(string? repoRootOverride)
    {
        var targetRoot = ResolveProbeTargetRoot(repoRootOverride);
        if (targetRoot is null || !Directory.Exists(targetRoot) || !RepoLocator.IsGitRepository(targetRoot))
        {
            WriteJson(BuildP5Report(
                "blocked",
                "NOT_A_GIT_REPOSITORY",
                10,
                P5PlanCleanupCommand,
                null,
                null,
                "Target is not a git repository.",
                targetRoot,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()));
            return 10;
        }

        var runtime = new LegacyAdoptionP5Runtime(targetRoot);
        var result = runtime.PlanCleanup();
        WriteJson(result.Payload);
        return result.ExitCode;
    }

    private static int RunAdoptionCleanupApplyJson(string? repoRootOverride, string planId, string planHash)
    {
        var targetRoot = ResolveProbeTargetRoot(repoRootOverride);
        if (targetRoot is null || !Directory.Exists(targetRoot) || !RepoLocator.IsGitRepository(targetRoot))
        {
            WriteJson(BuildP5Report(
                "blocked",
                "NOT_A_GIT_REPOSITORY",
                10,
                P5CleanupApplyCommand,
                null,
                null,
                "Target is not a git repository.",
                targetRoot,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()));
            return 10;
        }

        var runtime = new LegacyAdoptionP5Runtime(targetRoot);
        var result = runtime.ApplyCleanup(planId, planHash, P5CleanupApplyCommand);
        WriteJson(result.Payload);
        return result.ExitCode;
    }

    private static int RunAdoptionDetachJson(string? repoRootOverride, string? cleanupPlanId, string? planHash, string? exportPath)
    {
        var targetRoot = ResolveProbeTargetRoot(repoRootOverride);
        var command = cleanupPlanId is not null
            ? P5DetachWithCleanupCommand
            : exportPath is not null
                ? P5DetachExportCommand
                : P5DetachCommand;
        if (targetRoot is null || !Directory.Exists(targetRoot) || !RepoLocator.IsGitRepository(targetRoot))
        {
            WriteJson(BuildP5Report(
                "blocked",
                "NOT_A_GIT_REPOSITORY",
                10,
                command,
                null,
                null,
                "Target is not a git repository.",
                targetRoot,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()));
            return 10;
        }

        var runtime = new LegacyAdoptionP5Runtime(targetRoot);
        var result = runtime.Detach(cleanupPlanId, planHash, exportPath, command);
        WriteJson(result.Payload);
        return result.ExitCode;
    }

    private static int RunAdoptionProbeJson(string? repoRootOverride)
    {
        var targetRoot = ResolveProbeTargetRoot(repoRootOverride);
        if (targetRoot is null || !Directory.Exists(targetRoot) || !RepoLocator.IsGitRepository(targetRoot))
        {
            var blocked = BuildBaseProbeReport("blocked", "not_a_git_repository");
            blocked["repo_identity"]!["root_kind"] = "not_git";
            blocked["risk_flags"] = new JsonArray("not_a_git_repository");
            blocked["recommended_next_action"] = "not_a_git_repository";
            WriteJson(blocked);
            return 10;
        }

        ProbeInventory before;
        ProbeObservation observation;
        try
        {
            before = CaptureInventory(targetRoot);
            observation = ObserveRepository(targetRoot);
        }
        catch (Exception exception) when (IsProbeRecoverable(exception))
        {
            WriteJson(BuildErrorReport("hard_error", "INVENTORY_UNAVAILABLE", 20, "Probe inventory could not be computed."));
            return 20;
        }

        ProbeInventory after;
        try
        {
            after = CaptureInventory(targetRoot);
        }
        catch (Exception exception) when (IsProbeRecoverable(exception))
        {
            WriteJson(BuildErrorReport("hard_error", "INVENTORY_UNAVAILABLE", 20, "Probe postflight inventory could not be computed."));
            return 20;
        }

        var inventoryChanged = before.SummaryHash != after.SummaryHash;
        var gitIndexChanged = before.GitIndexHash != after.GitIndexHash || before.GitIndexStat != after.GitIndexStat;
        var newLockFiles = after.GitLockSentinels.Except(before.GitLockSentinels, StringComparer.Ordinal).ToArray();
        if (inventoryChanged || gitIndexChanged || newLockFiles.Length > 0)
        {
            var error = BuildProbeReport(
                targetRoot,
                "hard_error",
                observation,
                after,
                mutationCause: "unknown",
                mutationConfidence: "low",
                gitIndexChanged: gitIndexChanged,
                newGitLockFilesDetected: newLockFiles.Length > 0);
            error["risk_flags"] = MergeRiskFlags(error["risk_flags"]!.AsArray(), "readonly_unproven_external_mutation");
            error["recommended_next_action"] = "operator_review_required";
            error["read_only_postflight"]!["repo_state_unchanged"] = false;
            WriteJson(error);
            return 20;
        }

        var result = observation.RiskFlags.Count == 0 ? "success" : "success_with_risk";
        var report = BuildProbeReport(
            targetRoot,
            result,
            observation,
            after,
            mutationCause: "none",
            mutationConfidence: "none",
            gitIndexChanged: false,
            newGitLockFilesDetected: false);
        WriteJson(report);
        return observation.ExitCode;
    }

    private static string? ResolveProbeTargetRoot(string? repoRootOverride)
    {
        if (!string.IsNullOrWhiteSpace(repoRootOverride))
        {
            var explicitPath = Path.GetFullPath(repoRootOverride);
            return Directory.Exists(explicitPath) ? explicitPath : null;
        }

        return RepoLocator.Resolve(startDirectory: Directory.GetCurrentDirectory())
            ?? Path.GetFullPath(Directory.GetCurrentDirectory());
    }

    private static ProbeObservation ObserveRepository(string repoRoot)
    {
        var gitStatus = RunGitRead(repoRoot, "status", "--porcelain=v2", "--branch", "--untracked-files=all");
        var statusLines = gitStatus.ExitCode == 0
            ? gitStatus.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            : Array.Empty<string>();
        var dirtyEntries = statusLines.Where(line => !line.StartsWith("#", StringComparison.Ordinal)).ToArray();
        var dirtySummary = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in dirtyEntries)
        {
            var key = entry.Length >= 2 ? entry[..2] : entry;
            dirtySummary[key] = dirtySummary.TryGetValue(key, out var count) ? count + 1 : 1;
        }

        var instructionFiles = ProbeInstructionFiles(repoRoot);
        var aiDirectoryPresent = Directory.Exists(Path.Combine(repoRoot, ".ai"));
        var adoptionManifestPresent = File.Exists(Path.Combine(repoRoot, ".ai", "runtime", "adoption", "adoption.json"));
        var nestedGitDetected = DetectNestedGitRepository(repoRoot);
        var submodulesDetected = RunGitRead(repoRoot, "submodule", "status", "--recursive").StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Length > 0;
        var caseCollisionDetected = DetectCaseCollision(repoRoot);
        var symlinkEscapeRisk = DetectSymlinkEscapeRisk(repoRoot);
        var remoteCount = CountRemotes(repoRoot);

        var riskFlags = new SortedSet<string>(StringComparer.Ordinal);
        if (dirtyEntries.Length > 0)
        {
            riskFlags.Add("dirty_working_tree");
        }

        foreach (var instruction in instructionFiles.Where(item => item.Exists))
        {
            riskFlags.Add(instruction.Kind switch
            {
                "AGENTS" => "existing_human_AGENTS_file",
                "CLAUDE" => "existing_human_CLAUDE_file",
                "CURSOR_RULES" => "existing_cursor_rules",
                _ => "existing_human_instruction_file",
            });
        }

        if (aiDirectoryPresent && !adoptionManifestPresent)
        {
            riskFlags.Add("existing_ai_without_manifest");
        }

        if (nestedGitDetected)
        {
            riskFlags.Add("nested_git_repository");
        }

        if (submodulesDetected)
        {
            riskFlags.Add("submodule_detected");
        }

        if (caseCollisionDetected)
        {
            riskFlags.Add("case_collision_detected");
        }

        if (symlinkEscapeRisk)
        {
            riskFlags.Add("symlink_escape_risk");
        }

        riskFlags.Add("current_attach_unsafe_for_legacy");

        var exitCode = 0;
        var recommendedNextAction = "eligible_for_future_adoption_attach";
        if (aiDirectoryPresent && !adoptionManifestPresent)
        {
            exitCode = 11;
            recommendedNextAction = "operator_review_required";
        }
        else if (riskFlags.Count > 0)
        {
            exitCode = 1;
            recommendedNextAction = "operator_review_required";
        }

        return new ProbeObservation(
            remoteCount,
            remoteCount == 0 ? "none" : "redacted",
            instructionFiles,
            dirtyEntries.Length == 0 ? "clean" : "dirty",
            dirtyEntries.Length,
            dirtySummary,
            aiDirectoryPresent ? "present" : "absent",
            adoptionManifestPresent ? "present" : "absent",
            nestedGitDetected,
            submodulesDetected,
            caseCollisionDetected,
            symlinkEscapeRisk,
            riskFlags.ToArray(),
            recommendedNextAction,
            exitCode);
    }

    private static IReadOnlyList<InstructionFileObservation> ProbeInstructionFiles(string repoRoot)
    {
        var candidates = new (string Path, string Kind)[]
        {
            ("AGENTS.md", "AGENTS"),
            ("CLAUDE.md", "CLAUDE"),
            (".cursor/rules", "CURSOR_RULES"),
        };

        return candidates
            .Select(candidate =>
            {
                var fullPath = Path.Combine(repoRoot, candidate.Path.Replace('/', Path.DirectorySeparatorChar));
                var exists = File.Exists(fullPath);
                return new InstructionFileObservation(
                    candidate.Path,
                    exists,
                    candidate.Kind,
                    exists ? "human_or_unknown" : "missing",
                    exists ? new FileInfo(fullPath).Length : null,
                    null,
                    exists ? "operator_review_required" : "do_not_modify");
            })
            .ToArray();
    }

    private static ProbeInventory CaptureInventory(string repoRoot)
    {
        var records = new List<string>();
        foreach (var entry in EnumerateProbeFileSystemEntries(repoRoot))
        {
            var relativePath = Path.GetRelativePath(repoRoot, entry).Replace('\\', '/');
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                var info = new FileInfo(entry);
                records.Add($"symlink|{relativePath}|{(int)(attributes & FileAttributes.ReparsePoint)}|{info.Length}|{info.LastWriteTimeUtc.Ticks}");
                continue;
            }

            if (Directory.Exists(entry))
            {
                var info = new DirectoryInfo(entry);
                records.Add($"directory|{relativePath}|{info.LastWriteTimeUtc.Ticks}");
                continue;
            }

            var fileInfo = new FileInfo(entry);
            records.Add($"file|{relativePath}|{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}");
        }

        records.Add($"ai-sentinel|{Directory.Exists(Path.Combine(repoRoot, ".ai"))}");
        records.Add($"git-sentinel|{RepoLocator.IsGitRepository(repoRoot)}");
        var gitIndexPath = ResolveGitIndexPath(repoRoot);
        var gitIndexStat = ComputeStat(gitIndexPath);
        var gitIndexHash = File.Exists(gitIndexPath) ? ComputeFileSha256(gitIndexPath) : null;
        records.Add($"git-index|{gitIndexStat}|{gitIndexHash}");
        var lockSentinels = FindGitLockSentinels(repoRoot);
        foreach (var lockFile in lockSentinels)
        {
            records.Add($"git-lock|{lockFile}");
        }

        records.Sort(StringComparer.Ordinal);
        return new ProbeInventory(
            Sha256Hex(string.Join('\n', records)),
            gitIndexStat,
            gitIndexHash,
            lockSentinels);
    }

    private static string ResolveGitIndexPath(string repoRoot)
    {
        var gitDir = RunGitRead(repoRoot, "rev-parse", "--git-dir").StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(gitDir))
        {
            return Path.Combine(repoRoot, ".git", "index");
        }

        var resolvedGitDir = Path.IsPathRooted(gitDir) ? gitDir : Path.GetFullPath(Path.Combine(repoRoot, gitDir));
        return Path.Combine(resolvedGitDir, "index");
    }

    private static string ComputeStat(string path)
    {
        if (!File.Exists(path))
        {
            return "missing";
        }

        var info = new FileInfo(path);
        return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
    }

    private static IReadOnlyList<string> FindGitLockSentinels(string repoRoot)
    {
        var gitDir = RunGitRead(repoRoot, "rev-parse", "--git-common-dir").StandardOutput.Trim();
        var resolvedGitDir = string.IsNullOrWhiteSpace(gitDir)
            ? Path.Combine(repoRoot, ".git")
            : Path.IsPathRooted(gitDir) ? gitDir : Path.GetFullPath(Path.Combine(repoRoot, gitDir));
        if (!Directory.Exists(resolvedGitDir))
        {
            return Array.Empty<string>();
        }

        return Directory
            .EnumerateFiles(resolvedGitDir, "*.lock", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool DetectNestedGitRepository(string repoRoot)
    {
        foreach (var directory in EnumerateProbeFileSystemEntries(repoRoot).Where(Directory.Exists))
        {
            if (Directory.Exists(Path.Combine(directory, ".git")) || File.Exists(Path.Combine(directory, ".git")))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DetectCaseCollision(string repoRoot)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in EnumerateProbeFileSystemEntries(repoRoot))
        {
            var relativePath = Path.GetRelativePath(repoRoot, entry).Replace('\\', '/');
            if (!seen.Add(relativePath.ToUpperInvariant()))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DetectSymlinkEscapeRisk(string repoRoot)
    {
        foreach (var entry in EnumerateProbeFileSystemEntries(repoRoot))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) == 0)
            {
                continue;
            }

            var linkTarget = attributes.HasFlag(FileAttributes.Directory)
                ? new DirectoryInfo(entry).LinkTarget
                : new FileInfo(entry).LinkTarget;
            if (string.IsNullOrWhiteSpace(linkTarget))
            {
                continue;
            }

            if (Path.IsPathRooted(linkTarget) || linkTarget.Split(['/', '\\']).Any(segment => segment == ".."))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountRemotes(string repoRoot)
    {
        var result = RunGitRead(repoRoot, "config", "--get-regexp", "^remote\\..*\\.url$");
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return 0;
        }

        return result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static IEnumerable<string> EnumerateProbeFileSystemEntries(string repoRoot)
    {
        var pending = new Stack<string>(Directory.EnumerateFileSystemEntries(repoRoot).Reverse());
        while (pending.Count > 0)
        {
            var entry = pending.Pop();
            var relativePath = Path.GetRelativePath(repoRoot, entry).Replace('\\', '/');
            if (IsUnderTopLevel(relativePath, ".git") || IsUnderTopLevel(relativePath, ".ai"))
            {
                continue;
            }

            yield return entry;

            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.Directory) == 0 || (attributes & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            foreach (var child in Directory.EnumerateFileSystemEntries(entry).Reverse())
            {
                pending.Push(child);
            }
        }
    }

    private static JsonObject BuildProbeReport(
        string repoRoot,
        string result,
        ProbeObservation observation,
        ProbeInventory inventory,
        string mutationCause,
        string mutationConfidence,
        bool gitIndexChanged,
        bool newGitLockFilesDetected)
    {
        var report = BuildBaseProbeReport(result, observation.RecommendedNextAction);
        report["repo_identity"] = new JsonObject
        {
            ["root_kind"] = "git_worktree",
            ["head_present"] = RunGitRead(repoRoot, "rev-parse", "--verify", "HEAD").ExitCode == 0,
            ["head_hash_present"] = RunGitRead(repoRoot, "rev-parse", "HEAD").ExitCode == 0,
            ["branch_name_redacted"] = true,
            ["remote_count"] = observation.RemoteCount,
            ["remote_host_classification"] = observation.RemoteHostClassification,
            ["remote_host_hash_with_per_report_salt_if_needed"] = null,
        };
        report["repo_scope"] = new JsonObject
        {
            ["root_path_policy"] = "repo_relative_only",
            ["nested_git_repositories_detected"] = observation.NestedGitRepositoriesDetected,
            ["submodules_detected"] = observation.SubmodulesDetected,
            ["case_collision_detected"] = observation.CaseCollisionDetected,
            ["symlink_escape_risk_detected"] = observation.SymlinkEscapeRiskDetected,
        };
        report["existing_carves_state"] = new JsonObject
        {
            ["ai_directory_presence"] = observation.AiDirectoryPresence,
            ["ai_governance_content_read"] = false,
            ["adoption_manifest_presence"] = observation.AdoptionManifestPresence,
            ["codegraph_content_read"] = false,
        };
        report["instruction_files"] = new JsonArray(observation.InstructionFiles.Select(ToJsonNode).ToArray());
        report["dirty_state"] = new JsonObject
        {
            ["working_tree_state"] = observation.WorkingTreeState,
            ["dirty_file_count"] = observation.DirtyFileCount,
            ["dirty_status_summary"] = ToJsonObject(observation.DirtyStatusSummary),
            ["dirty_diff_content_emitted"] = false,
            ["dirty_path_hashes_non_comparable_across_runs"] = false,
        };
        report["risk_flags"] = new JsonArray(observation.RiskFlags.Select(flag => JsonValue.Create(flag)).ToArray());
        report["mutation_detection"] = new JsonObject
        {
            ["cause"] = mutationCause,
            ["confidence"] = mutationConfidence,
            ["changed_paths_redacted"] = true,
            ["git_index_changed"] = gitIndexChanged,
            ["new_git_lock_files_detected"] = newGitLockFilesDetected,
            ["result_mapping"] = BuildMutationResultMapping(),
        };
        report["read_only_postflight"] = new JsonObject
        {
            ["computed"] = true,
            ["summary_hash"] = $"sha256:{inventory.SummaryHash}",
            ["private_evidence_emitted"] = false,
            ["git_index_unchanged"] = !gitIndexChanged,
            ["no_new_git_lock_files"] = !newGitLockFilesDetected,
            ["repo_state_unchanged"] = mutationCause == "none",
            ["failure_semantics_applied"] = true,
        };

        return report;
    }

    private static JsonObject BuildBaseProbeReport(string result, string recommendedNextAction)
    {
        return new JsonObject
        {
            ["schema_version"] = P1ProbeSchemaVersion,
            ["phase"] = P1ProbePhase,
            ["command"] = P1ProbeCommand,
            ["command_status"] = "proposed_future_command",
            ["result"] = result,
            ["redaction_policy_version"] = "legacy_adoption_p1_redaction_v0.3.2",
            ["privacy_mode"] = "redacted_public_report",
            ["remote_url_output_policy"] = "no_raw_urls_no_stable_full_remote_hash",
            ["content_output_policy"] = "no_file_bodies_no_dirty_diff_no_instruction_bodies",
            ["repo_identity"] = new JsonObject
            {
                ["root_kind"] = "ambiguous",
                ["head_present"] = false,
                ["head_hash_present"] = false,
                ["branch_name_redacted"] = true,
                ["remote_count"] = 0,
                ["remote_host_classification"] = "none",
                ["remote_host_hash_with_per_report_salt_if_needed"] = null,
            },
            ["repo_scope"] = new JsonObject
            {
                ["root_path_policy"] = "repo_relative_only",
                ["nested_git_repositories_detected"] = false,
                ["submodules_detected"] = false,
                ["case_collision_detected"] = false,
                ["symlink_escape_risk_detected"] = false,
            },
            ["observed_context"] = new JsonObject
            {
                ["network_allowed"] = false,
                ["git_optional_locks"] = "0",
                ["full_inventory_emitted"] = false,
                ["private_proof_evidence_emitted"] = false,
            },
            ["existing_carves_state"] = new JsonObject
            {
                ["ai_directory_presence"] = "unknown",
                ["ai_governance_content_read"] = false,
                ["adoption_manifest_presence"] = "unknown",
                ["codegraph_content_read"] = false,
            },
            ["instruction_files"] = new JsonArray(),
            ["dirty_state"] = new JsonObject
            {
                ["working_tree_state"] = "unknown",
                ["dirty_file_count"] = 0,
                ["dirty_status_summary"] = new JsonObject(),
                ["dirty_diff_content_emitted"] = false,
                ["dirty_path_hashes_non_comparable_across_runs"] = false,
            },
            ["risk_flags"] = new JsonArray(),
            ["current_attach_assessment"] = new JsonObject
            {
                ["current_attach_start_status"] = "unsafe_for_legacy_adoption",
                ["target_repo_attach_service_writer_path_allowed"] = false,
                ["legacy_adoption_available"] = false,
            },
            ["mutation_detection"] = new JsonObject
            {
                ["cause"] = "none",
                ["confidence"] = "none",
                ["changed_paths_redacted"] = true,
                ["git_index_changed"] = false,
                ["new_git_lock_files_detected"] = false,
                ["result_mapping"] = BuildMutationResultMapping(),
            },
            ["recommended_next_action"] = recommendedNextAction,
            ["read_only_postflight"] = new JsonObject
            {
                ["computed"] = false,
                ["summary_hash"] = null,
                ["private_evidence_emitted"] = false,
                ["git_index_unchanged"] = true,
                ["no_new_git_lock_files"] = true,
                ["repo_state_unchanged"] = true,
                ["failure_semantics_applied"] = true,
            },
        };
    }

    private static JsonObject BuildErrorReport(string result, string errorCode, int exitCode, string message)
    {
        var report = BuildBaseProbeReport(result, "operator_review_required");
        report["exit_code"] = exitCode;
        report["error_code"] = errorCode;
        report["error_namespace"] = P1ProbeErrorNamespace;
        report["message"] = message;
        return report;
    }

    private static JsonObject BuildAttachReport(
        string result,
        string status,
        int exitCode,
        string? adoptionId,
        string message,
        string? repoRoot,
        IReadOnlyList<string> persistentArtifacts)
    {
        var payload = new JsonObject
        {
            ["schema_version"] = P2AttachSchemaVersion,
            ["phase"] = P2AttachPhase,
            ["command"] = P2AttachCommand,
            ["result"] = result,
            ["status"] = status,
            ["exit_code"] = exitCode,
            ["error_namespace"] = P2AttachErrorNamespace,
            ["message"] = message,
            ["adoption_id"] = adoptionId,
            ["persistent_artifacts"] = new JsonArray(persistentArtifacts.Select(item => JsonValue.Create(item)).ToArray()),
            ["temporary_artifacts"] = new JsonArray(".ai/runtime/adoption/adoption.lock.json", ".ai/runtime/adoption/*.tmp"),
            ["runtime_outputs_limited_to_p2_write_set"] = true,
            ["p3_to_p5_status"] = new JsonObject
            {
                ["P3_INTAKE"] = "BLOCKED",
                ["P4_PROPOSALS"] = "BLOCKED",
                ["P5_CLEANUP_DETACH"] = "BLOCKED",
            },
            ["forbidden_writers_used"] = false,
        };

        if (!string.IsNullOrWhiteSpace(repoRoot) && Directory.Exists(repoRoot) && RepoLocator.IsGitRepository(repoRoot))
        {
            payload["observed_context"] = BuildAttachObservedContext(repoRoot);
        }
        else
        {
            payload["observed_context"] = new JsonObject
            {
                ["root_kind"] = "not_git",
                ["head_present"] = false,
                ["head_hash_present"] = false,
                ["branch_name_redacted"] = true,
                ["dirty_state"] = "unknown",
                ["remote_count"] = 0,
                ["repo_fingerprint_role"] = "diagnostics_and_conflict_detection_only",
                ["instruction_files_present"] = new JsonArray(),
                ["existing_ai_state"] = "unknown",
                ["current_attach_assessment"] = "unsafe_for_legacy_adoption",
            };
        }

        return payload;
    }

    private static JsonObject BuildIntakeReport(
        string result,
        string status,
        int exitCode,
        string? adoptionId,
        int? generation,
        string message,
        string? repoRoot,
        IReadOnlyList<string> evidenceArtifacts,
        IReadOnlyList<string> proposalArtifacts)
    {
        var payload = new JsonObject
        {
            ["schema_version"] = P3IntakeSchemaVersion,
            ["phase"] = P3IntakePhase,
            ["command"] = P3IntakeCommand,
            ["result"] = result,
            ["status"] = status,
            ["exit_code"] = exitCode,
            ["error_namespace"] = P3IntakeErrorNamespace,
            ["message"] = message,
            ["adoption_id"] = adoptionId,
            ["generation"] = generation,
            ["evidence_artifacts"] = new JsonArray(evidenceArtifacts.Select(item => JsonValue.Create(item)).ToArray()),
            ["proposal_artifacts"] = new JsonArray(proposalArtifacts.Select(item => JsonValue.Create(item)).ToArray()),
            ["temporary_artifacts"] = new JsonArray(".ai/runtime/adoption/adoption.lock.json", ".ai/runtime/adoption/*.tmp"),
            ["public_output_excludes_sensitive_content"] = true,
            ["dirty_diff_content_emitted"] = false,
            ["instruction_bodies_emitted"] = false,
            ["full_repo_inventory_emitted"] = false,
            ["repo_scan_is_codegraph"] = false,
            ["codegraph_generated"] = false,
            ["governance_surfaces_written"] = false,
            ["forbidden_writers_used"] = false,
            ["p4_p5_status"] = new JsonObject
            {
                ["P4_PROPOSALS"] = "BLOCKED",
                ["P5_CLEANUP_DETACH"] = "BLOCKED",
            },
        };

        if (!string.IsNullOrWhiteSpace(repoRoot) && Directory.Exists(repoRoot) && RepoLocator.IsGitRepository(repoRoot))
        {
            payload["observed_context"] = BuildAttachObservedContext(repoRoot);
        }
        else
        {
            payload["observed_context"] = new JsonObject
            {
                ["root_kind"] = "not_git",
                ["head_present"] = false,
                ["head_hash_present"] = false,
                ["branch_name_redacted"] = true,
                ["dirty_state"] = "unknown",
                ["remote_count"] = 0,
                ["repo_fingerprint_role"] = "diagnostics_and_conflict_detection_only",
                ["instruction_files_present"] = new JsonArray(),
                ["existing_ai_state"] = "unknown",
                ["current_attach_assessment"] = "unsafe_for_legacy_adoption",
            };
        }

        return payload;
    }

    private static JsonObject BuildProposeReport(
        string result,
        string status,
        int exitCode,
        string? adoptionId,
        int? generation,
        string message,
        string? repoRoot,
        IReadOnlyList<string> proposalArtifacts)
    {
        var payload = new JsonObject
        {
            ["schema_version"] = P4ProposeSchemaVersion,
            ["phase"] = P4ProposePhase,
            ["command"] = P4ProposeCommand,
            ["result"] = result,
            ["status"] = status,
            ["exit_code"] = exitCode,
            ["error_namespace"] = P4ProposeErrorNamespace,
            ["message"] = message,
            ["adoption_id"] = adoptionId,
            ["generation"] = generation,
            ["proposal_artifacts"] = new JsonArray(proposalArtifacts.Select(item => JsonValue.Create(item)).ToArray()),
            ["temporary_artifacts"] = new JsonArray(".ai/runtime/adoption/adoption.lock.json", ".ai/runtime/adoption/*.tmp"),
            ["proposal_only"] = true,
            ["public_output_excludes_sensitive_content"] = true,
            ["dirty_diff_content_emitted"] = false,
            ["instruction_bodies_emitted"] = false,
            ["full_repo_inventory_emitted"] = false,
            ["raw_remote_urls_emitted"] = false,
            ["ignored_file_contents_emitted"] = false,
            ["governed_memory_written"] = false,
            ["approved_taskgraph_written"] = false,
            ["canonical_codegraph_written"] = false,
            ["planner_authoritative_codegraph_written"] = false,
            ["patches_written"] = false,
            ["reviews_written"] = false,
            ["worker_execution_invoked"] = false,
            ["forbidden_writers_used"] = false,
            ["p5_status"] = "BLOCKED",
            ["allowed_statuses"] = new JsonObject
            {
                ["memory"] = "PROPOSED",
                ["taskgraph"] = "DRAFT",
                ["tasks"] = "SUGGESTED",
                ["codegraph"] = "SNAPSHOT",
                ["refactor_candidates"] = "PROPOSED",
            },
        };

        if (!string.IsNullOrWhiteSpace(repoRoot) && Directory.Exists(repoRoot) && RepoLocator.IsGitRepository(repoRoot))
        {
            payload["observed_context"] = BuildAttachObservedContext(repoRoot);
        }
        else
        {
            payload["observed_context"] = new JsonObject
            {
                ["root_kind"] = "not_git",
                ["head_present"] = false,
                ["head_hash_present"] = false,
                ["branch_name_redacted"] = true,
                ["dirty_state"] = "unknown",
                ["remote_count"] = 0,
                ["repo_fingerprint_role"] = "diagnostics_and_conflict_detection_only",
                ["instruction_files_present"] = new JsonArray(),
                ["existing_ai_state"] = "unknown",
                ["current_attach_assessment"] = "unsafe_for_legacy_adoption",
            };
        }

        return payload;
    }

    private static JsonObject BuildP5Report(
        string result,
        string status,
        int exitCode,
        string command,
        string? adoptionId,
        int? generation,
        string message,
        string? repoRoot,
        IReadOnlyList<string> runtimeArtifacts,
        IReadOnlyList<string> deletedPaths,
        IReadOnlyList<string> archivedPaths,
        IReadOnlyList<string> refusedPaths)
    {
        var payload = new JsonObject
        {
            ["schema_version"] = P5CleanupDetachSchemaVersion,
            ["phase"] = P5CleanupDetachPhase,
            ["command"] = command,
            ["result"] = result,
            ["status"] = status,
            ["exit_code"] = exitCode,
            ["error_namespace"] = P5CleanupDetachErrorNamespace,
            ["message"] = message,
            ["adoption_id"] = adoptionId,
            ["generation"] = generation,
            ["runtime_artifacts"] = new JsonArray(runtimeArtifacts.Select(item => JsonValue.Create(item)).ToArray()),
            ["deleted_paths"] = new JsonArray(deletedPaths.Select(item => JsonValue.Create(item)).ToArray()),
            ["archived_paths"] = new JsonArray(archivedPaths.Select(item => JsonValue.Create(item)).ToArray()),
            ["refused_paths"] = new JsonArray(refusedPaths.Select(item => JsonValue.Create(item)).ToArray()),
            ["temporary_artifacts"] = new JsonArray(".ai/runtime/adoption/adoption.lock.json", ".ai/runtime/adoption/*.tmp"),
            ["plan_first"] = true,
            ["delete_by_prefix"] = false,
            ["reference_index_derived"] = true,
            ["reference_index_canonical_truth"] = false,
            ["hard_delete_detach_implemented"] = false,
            ["stale_lock_takeover_implemented"] = false,
            ["destructive_recovery_implemented"] = false,
            ["source_files_deleted"] = false,
            ["human_instruction_files_deleted"] = false,
            ["approved_truth_deleted"] = false,
            ["referenced_evidence_deleted"] = false,
            ["public_output_excludes_sensitive_content"] = true,
            ["dirty_diff_content_emitted"] = false,
            ["instruction_bodies_emitted"] = false,
            ["raw_remote_urls_emitted"] = false,
            ["cleanup_proof_required_for_apply"] = true,
            ["soft_detach_only"] = true,
        };

        if (!string.IsNullOrWhiteSpace(repoRoot) && Directory.Exists(repoRoot) && RepoLocator.IsGitRepository(repoRoot))
        {
            payload["observed_context"] = BuildAttachObservedContext(repoRoot);
        }
        else
        {
            payload["observed_context"] = new JsonObject
            {
                ["root_kind"] = "not_git",
                ["head_present"] = false,
                ["head_hash_present"] = false,
                ["branch_name_redacted"] = true,
                ["dirty_state"] = "unknown",
                ["remote_count"] = 0,
                ["repo_fingerprint_role"] = "diagnostics_and_conflict_detection_only",
                ["instruction_files_present"] = new JsonArray(),
                ["existing_ai_state"] = "unknown",
                ["current_attach_assessment"] = "unsafe_for_legacy_adoption",
            };
        }

        return payload;
    }

    private static JsonObject BuildAttachObservedContext(string repoRoot)
    {
        var status = RunGitRead(repoRoot, "status", "--porcelain=v2", "--branch", "--untracked-files=all");
        var dirty = status.ExitCode != 0
            ? "unknown"
            : status.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Any(line => !line.StartsWith("#", StringComparison.Ordinal))
                    ? "dirty"
                    : "clean";
        return new JsonObject
        {
            ["root_kind"] = "git_worktree",
            ["head_present"] = RunGitRead(repoRoot, "rev-parse", "--verify", "HEAD").ExitCode == 0,
            ["head_hash_present"] = RunGitRead(repoRoot, "rev-parse", "HEAD").ExitCode == 0,
            ["branch_name_redacted"] = true,
            ["dirty_state"] = dirty,
            ["remote_count"] = CountRemotes(repoRoot),
            ["repo_fingerprint_role"] = "diagnostics_and_conflict_detection_only",
            ["instruction_files_present"] = new JsonArray(DetectInstructionFilesPresent(repoRoot).Select(item => JsonValue.Create(item)).ToArray()),
            ["existing_ai_state"] = ResolveExistingAiState(repoRoot),
            ["current_attach_assessment"] = "unsafe_for_legacy_adoption",
        };
    }

    private static JsonObject BuildMutationResultMapping()
    {
        return new JsonObject
        {
            ["none"] = "eligible_for_success_or_success_with_risk",
            ["probe_owned"] = "readonly_violation",
            ["external_confirmed"] = "hard_error",
            ["unknown"] = "hard_error",
        };
    }

    private static JsonArray MergeRiskFlags(JsonArray existing, string flag)
    {
        var values = existing
            .Select(item => item?.GetValue<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Append(flag)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(item => JsonValue.Create(item))
            .ToArray();
        return new JsonArray(values);
    }

    private static IReadOnlyList<string> DetectInstructionFilesPresent(string repoRoot)
    {
        var present = new List<string>();
        if (File.Exists(Path.Combine(repoRoot, "AGENTS.md")))
        {
            present.Add("AGENTS.md");
        }

        if (File.Exists(Path.Combine(repoRoot, "CLAUDE.md")))
        {
            present.Add("CLAUDE.md");
        }

        if (File.Exists(Path.Combine(repoRoot, ".cursor", "rules")))
        {
            present.Add(".cursor/rules");
        }

        return present;
    }

    private static string ResolveExistingAiState(string repoRoot)
    {
        var aiRoot = Path.Combine(repoRoot, ".ai");
        if (!Directory.Exists(aiRoot))
        {
            return "absent";
        }

        return File.Exists(Path.Combine(aiRoot, "runtime", "adoption", "adoption.json"))
            ? "present_with_adoption_manifest"
            : "present_without_adoption_manifest";
    }

    private static JsonNode ToJsonNode(InstructionFileObservation instruction)
    {
        return new JsonObject
        {
            ["path"] = instruction.Path,
            ["exists"] = instruction.Exists,
            ["kind"] = instruction.Kind,
            ["ownership_guess"] = instruction.OwnershipGuess,
            ["size"] = instruction.Size,
            ["hash"] = instruction.Hash,
            ["body_read"] = false,
            ["excerpt_emitted"] = false,
            ["recommended_policy"] = instruction.RecommendedPolicy,
        };
    }

    private static JsonObject ToJsonObject(IReadOnlyDictionary<string, int> values)
    {
        var json = new JsonObject();
        foreach (var pair in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            json[pair.Key] = pair.Value;
        }

        return json;
    }

    private static void WriteJson(JsonObject payload)
    {
        Console.WriteLine(JsonSerializer.Serialize(payload, AdoptionJsonOptions));
    }

    private static GitReadResult RunGitRead(string repoRoot, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GCM_INTERACTIVE"] = "Never";
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start git.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new GitReadResult(process.ExitCode, standardOutput, standardError);
    }

    private static bool IsUnderTopLevel(string relativePath, string topLevel)
    {
        return string.Equals(relativePath, topLevel, StringComparison.Ordinal)
            || relativePath.StartsWith(topLevel + "/", StringComparison.Ordinal);
    }

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Sha256Hex(stream);
    }

    private static string Sha256Hex(string value)
    {
        return Sha256Hex(Encoding.UTF8.GetBytes(value));
    }

    private static string Sha256Hex(Stream stream)
    {
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Sha256Hex(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static bool IsProbeRecoverable(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or InvalidOperationException;
    }

    private sealed record GitReadResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed record ProbeInventory(
        string SummaryHash,
        string GitIndexStat,
        string? GitIndexHash,
        IReadOnlyList<string> GitLockSentinels);

    private sealed record InstructionFileObservation(
        string Path,
        bool Exists,
        string Kind,
        string OwnershipGuess,
        long? Size,
        string? Hash,
        string RecommendedPolicy);

    private sealed record ProbeObservation(
        int RemoteCount,
        string RemoteHostClassification,
        IReadOnlyList<InstructionFileObservation> InstructionFiles,
        string WorkingTreeState,
        int DirtyFileCount,
        IReadOnlyDictionary<string, int> DirtyStatusSummary,
        string AiDirectoryPresence,
        string AdoptionManifestPresence,
        bool NestedGitRepositoriesDetected,
        bool SubmodulesDetected,
        bool CaseCollisionDetected,
        bool SymlinkEscapeRiskDetected,
        IReadOnlyList<string> RiskFlags,
        string RecommendedNextAction,
        int ExitCode);

    private sealed class LegacyAdoptionP5Runtime
    {
        private static readonly string[] ReferenceTruthRoots =
        [
            ".ai/memory",
            ".ai/tasks",
            ".ai/codegraph",
            ".ai/patches",
            ".ai/reviews",
        ];

        private readonly string repoRoot;
        private readonly string adoptionRoot;
        private readonly string manifestPath;
        private readonly string eventsPath;
        private readonly string ledgerPath;
        private readonly string lockPath;
        private readonly string referencesPath;
        private readonly string cleanupRoot;
        private readonly string archiveRoot;

        public LegacyAdoptionP5Runtime(string repoRoot)
        {
            this.repoRoot = repoRoot;
            adoptionRoot = Path.Combine(repoRoot, ".ai", "runtime", "adoption");
            manifestPath = Path.Combine(adoptionRoot, "adoption.json");
            eventsPath = Path.Combine(adoptionRoot, "events.jsonl");
            ledgerPath = Path.Combine(adoptionRoot, "ownership-ledger.jsonl");
            lockPath = Path.Combine(adoptionRoot, "adoption.lock.json");
            referencesPath = Path.Combine(adoptionRoot, "references.json");
            cleanupRoot = Path.Combine(adoptionRoot, "cleanup");
            archiveRoot = Path.Combine(adoptionRoot, "archive");
        }

        public P5Result PlanCleanup()
        {
            if (File.Exists(lockPath))
            {
                return ExistingLockResult(P5PlanCleanupCommand);
            }

            if (!TryLoadManifest(P5PlanCleanupCommand, requireP4Evidence: true, out var manifest, out var adoptionId, out var currentGeneration, out var preconditionResult))
            {
                return preconditionResult!;
            }

            var lockAcquired = false;
            try
            {
                AcquireLock("legacy_adoption.p5_cleanup_lock.v0.1.0");
                lockAcquired = true;
                var capture = CapturePlan(manifest!, adoptionId!, currentGeneration + 1);

                WriteJsonAtomic(referencesPath, capture.ReferenceIndex);
                WriteJsonAtomic(ToFullPath(capture.PlanPath), capture.Plan);
                var now = DateTimeOffset.UtcNow;
                AppendLineDurable(eventsPath, BuildEventRecord(adoptionId!, "CLEANUP_PLAN_CREATED", "success", currentGeneration, capture.RuntimeArtifacts, now));
                foreach (var artifactPath in capture.RuntimeArtifacts)
                {
                    AppendLineDurable(ledgerPath, BuildLedgerRecord(adoptionId!, artifactPath, ResolveP5ArtifactKind(artifactPath), now));
                }

                return new P5Result(
                    0,
                    BuildP5Report(
                        "success",
                        "CLEANUP_PLAN_READY",
                        0,
                        P5PlanCleanupCommand,
                        adoptionId,
                        currentGeneration,
                        "Legacy adoption P5 cleanup plan created as dry-run evidence. No cleanup, detach, or delete was performed.",
                        repoRoot,
                        capture.RuntimeArtifacts,
                        Array.Empty<string>(),
                        Array.Empty<string>(),
                        capture.Candidates.Where(candidate => string.Equals(candidate.Decision, "refuse", StringComparison.Ordinal)).Select(candidate => candidate.Path).ToArray()));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                return RecoveryRequired(P5PlanCleanupCommand, adoptionId, currentGeneration, "P5 cleanup planning failed and requires operator recovery.");
            }
            finally
            {
                if (lockAcquired)
                {
                    TryDelete(lockPath);
                }
            }
        }

        public P5Result ApplyCleanup(string planId, string planHash, string command)
        {
            if (File.Exists(lockPath))
            {
                return ExistingLockResult(command);
            }

            if (!TryLoadManifest(command, requireP4Evidence: true, out var manifest, out var adoptionId, out var currentGeneration, out var preconditionResult))
            {
                return preconditionResult!;
            }

            if (!TryLoadPlan(command, planId, planHash, adoptionId, currentGeneration, out var plan, out var planResult))
            {
                return planResult!;
            }

            var proofPath = $".ai/runtime/adoption/cleanup/cleanup-proof-{planId}.json";
            var proofFullPath = ToFullPath(proofPath);
            if (File.Exists(proofFullPath)
                && TryReadJsonObject(proofFullPath, out var existingProof)
                && string.Equals(existingProof?["applied_plan_hash"]?.GetValue<string>(), planHash, StringComparison.Ordinal))
            {
                var archived = ReadStringArray(existingProof?["archived_paths"]);
                var refused = ReadStringArray(existingProof?["refused_paths"]);
                var deleted = ReadStringArray(existingProof?["deleted_paths"]);
                if (!string.Equals(existingProof?["adoption_id"]?.GetValue<string>(), adoptionId, StringComparison.Ordinal))
                {
                    return new P5Result(
                        27,
                        BuildP5Report(
                            "blocked",
                            "CLEANUP_PROOF_INVALID",
                            27,
                            command,
                            adoptionId,
                            currentGeneration,
                            "Cleanup proof adoption_id is missing or does not match the active adoption manifest.",
                            repoRoot,
                            new[] { proofPath },
                            deleted,
                            archived,
                            refused));
                }

                if (!string.Equals(existingProof?["status"]?.GetValue<string>(), "APPLIED", StringComparison.Ordinal))
                {
                    return new P5Result(
                        27,
                        BuildP5Report(
                            "blocked",
                            "CLEANUP_REFUSED",
                            27,
                            command,
                            adoptionId,
                            currentGeneration,
                            "Cleanup proof for this plan hash records a safety refusal. Detach and destructive cleanup remain blocked.",
                            repoRoot,
                            new[] { proofPath },
                            deleted,
                            archived,
                            refused));
                }

                return new P5Result(
                    0,
                    BuildP5Report(
                        "noop",
                        "CLEANUP_ALREADY_APPLIED",
                        0,
                        command,
                        adoptionId,
                        currentGeneration,
                        "Cleanup proof for this plan hash already exists.",
                        repoRoot,
                        new[] { proofPath },
                        deleted,
                        archived,
                        refused));
            }

            var lockAcquired = false;
            try
            {
                AcquireLock("legacy_adoption.p5_cleanup_lock.v0.1.0");
                lockAcquired = true;

                var safetyFailures = FindApplySafetyFailures(plan!, manifest!, adoptionId!, currentGeneration);
                if (safetyFailures.Count > 0)
                {
                    var refusalCapture = BuildRefusedCleanupProof(adoptionId!, planId, planHash, safetyFailures);
                    WriteJsonAtomic(proofFullPath, refusalCapture.Proof);
                    var refusedAt = DateTimeOffset.UtcNow;
                    AppendLineDurable(eventsPath, BuildEventRecord(adoptionId!, "CLEANUP_REFUSED", "blocked", currentGeneration, new[] { refusalCapture.ProofPath }, refusedAt));
                    AppendLineDurable(ledgerPath, BuildLedgerRecord(adoptionId!, refusalCapture.ProofPath, ResolveP5ArtifactKind(refusalCapture.ProofPath), refusedAt));

                    return new P5Result(
                        27,
                        BuildP5Report(
                            "blocked",
                            "STALE_CLEANUP_PLAN",
                            27,
                            command,
                            adoptionId,
                            currentGeneration,
                            "Cleanup plan is stale or unsafe after revalidation. No delete or archive operation was performed.",
                            repoRoot,
                            new[] { refusalCapture.ProofPath },
                            Array.Empty<string>(),
                            Array.Empty<string>(),
                            refusalCapture.RefusedPaths));
                }

                var capture = ApplyPlan(adoptionId!, planId, planHash, plan!);
                WriteJsonAtomic(proofFullPath, capture.Proof);
                var now = DateTimeOffset.UtcNow;
                var runtimeArtifacts = capture.ArchiveArtifacts.Prepend(capture.ProofPath).ToArray();
                AppendLineDurable(eventsPath, BuildEventRecord(adoptionId!, "CLEANUP_APPLIED", "success", currentGeneration + 1, runtimeArtifacts, now));
                foreach (var artifactPath in runtimeArtifacts)
                {
                    AppendLineDurable(ledgerPath, BuildLedgerRecord(adoptionId!, artifactPath, ResolveP5ArtifactKind(artifactPath), now));
                }

                UpdateManifestAfterCleanup(manifest!, currentGeneration + 1, planId, planHash, capture.ProofPath, now);
                WriteJsonAtomic(manifestPath, manifest!);

                return new P5Result(
                    0,
                    BuildP5Report(
                        "success",
                        "CLEANUP_APPLIED",
                        0,
                        command,
                        adoptionId,
                        currentGeneration + 1,
                        "Cleanup plan applied with proof. Protected and referenced artifacts were not deleted.",
                        repoRoot,
                        runtimeArtifacts,
                        capture.DeletedPaths,
                        capture.ArchivedPaths,
                        capture.RefusedPaths));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                return RecoveryRequired(command, adoptionId, currentGeneration, "P5 cleanup apply failed and requires operator recovery.");
            }
            finally
            {
                if (lockAcquired)
                {
                    TryDelete(lockPath);
                }
            }
        }

        public P5Result Detach(string? cleanupPlanId, string? planHash, string? exportPath, string command)
        {
            if (cleanupPlanId is not null)
            {
                if (string.IsNullOrWhiteSpace(planHash))
                {
                    return new P5Result(
                        2,
                        BuildP5Report(
                            "blocked",
                            "PLAN_HASH_REQUIRED",
                            2,
                            command,
                            null,
                            null,
                            "Detach with cleanup requires a plan hash.",
                            repoRoot,
                            Array.Empty<string>(),
                            Array.Empty<string>(),
                            Array.Empty<string>(),
                            Array.Empty<string>()));
                }

                var cleanup = ApplyCleanup(cleanupPlanId, planHash, command);
                if (cleanup.ExitCode != 0)
                {
                    return cleanup;
                }

                if (!string.Equals(cleanup.Payload["status"]?.GetValue<string>(), "CLEANUP_APPLIED", StringComparison.Ordinal)
                    && !string.Equals(cleanup.Payload["status"]?.GetValue<string>(), "CLEANUP_ALREADY_APPLIED", StringComparison.Ordinal))
                {
                    return new P5Result(
                        27,
                        BuildP5Report(
                            "blocked",
                            "CLEANUP_PROOF_REQUIRED",
                            27,
                            command,
                            cleanup.Payload["adoption_id"]?.GetValue<string>(),
                            cleanup.Payload["generation"]?.GetValue<int?>(),
                            "Detach with cleanup requires a successful cleanup apply proof before soft detach.",
                            repoRoot,
                            Array.Empty<string>(),
                            Array.Empty<string>(),
                            Array.Empty<string>(),
                            ReadStringArray(cleanup.Payload["refused_paths"])));
                }
            }

            if (File.Exists(lockPath))
            {
                return ExistingLockResult(command);
            }

            if (!TryLoadManifest(command, requireP4Evidence: false, out var manifest, out var adoptionId, out var currentGeneration, out var preconditionResult))
            {
                return preconditionResult!;
            }

            if (string.Equals(manifest!["status"]?.GetValue<string>(), "DETACHED_SOFT", StringComparison.Ordinal))
            {
                return new P5Result(
                    0,
                    BuildP5Report(
                        "noop",
                        "DETACHED_SOFT_NOOP",
                        0,
                        command,
                        adoptionId,
                        currentGeneration,
                        "Legacy adoption binding is already soft-detached.",
                        repoRoot,
                        Array.Empty<string>(),
                        Array.Empty<string>(),
                        Array.Empty<string>(),
                        Array.Empty<string>()));
            }

            var lockAcquired = false;
            try
            {
                AcquireLock("legacy_adoption.p5_detach_lock.v0.1.0");
                lockAcquired = true;
                var now = DateTimeOffset.UtcNow;
                var runtimeArtifacts = new List<string> { ".ai/runtime/adoption/adoption.json" };
                if (!string.IsNullOrWhiteSpace(exportPath))
                {
                    runtimeArtifacts.Add(WriteExport(exportPath, manifest!, adoptionId!, currentGeneration, now));
                }

                manifest!["status"] = "DETACHED_SOFT";
                manifest["active_binding"] = false;
                manifest["detached_at"] = now.ToString("O");
                manifest["detach_mode"] = "soft_detach";
                manifest["generation"] = currentGeneration + 1;
                manifest["updated_at"] = now.ToString("O");
                manifest["last_manifest_hash"] = "sha256:self";
                manifest["last_manifest_hash"] = "sha256:" + Sha256Hex(SerializeCanonical(manifest));
                WriteJsonAtomic(manifestPath, manifest);
                AppendLineDurable(eventsPath, BuildEventRecord(adoptionId!, "DETACHED_SOFT", "success", currentGeneration + 1, runtimeArtifacts, now));

                return new P5Result(
                    0,
                    BuildP5Report(
                        "success",
                        "DETACHED_SOFT",
                        0,
                        command,
                        adoptionId,
                        currentGeneration + 1,
                        "Legacy adoption active binding was soft-detached. Source, approved truth, reviews, patches, and referenced evidence were retained.",
                        repoRoot,
                        runtimeArtifacts,
                        Array.Empty<string>(),
                        Array.Empty<string>(),
                        Array.Empty<string>()));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                return RecoveryRequired(command, adoptionId, currentGeneration, "P5 detach failed and requires operator recovery.");
            }
            finally
            {
                if (lockAcquired)
                {
                    TryDelete(lockPath);
                }
            }
        }

        private bool TryLoadManifest(
            string command,
            bool requireP4Evidence,
            out JsonObject? manifest,
            out string? adoptionId,
            out int generation,
            out P5Result? result)
        {
            manifest = null;
            adoptionId = null;
            generation = 0;
            result = null;

            if (!File.Exists(manifestPath))
            {
                var status = Directory.Exists(Path.Combine(repoRoot, ".ai")) ? "ADOPTION_UNKNOWN" : "ADOPTION_MISSING";
                result = new P5Result(
                    status == "ADOPTION_UNKNOWN" ? 22 : 23,
                    BuildP5Report(
                        "blocked",
                        status,
                        status == "ADOPTION_UNKNOWN" ? 22 : 23,
                        command,
                        null,
                        null,
                        "P5 requires valid accepted P1-P4 adoption state. No destructive operation was performed.",
                        repoRoot,
                        Array.Empty<string>(),
                        Array.Empty<string>(),
                        Array.Empty<string>(),
                        Array.Empty<string>()));
                return false;
            }

            try
            {
                manifest = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject()
                    ?? throw new JsonException("Manifest root is not an object.");
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                result = CorruptManifestResult(command, "Existing adoption manifest is corrupt. No writes were performed.");
                return false;
            }

            adoptionId = manifest["adoption_id"]?.GetValue<string>();
            var statusValue = manifest["status"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(adoptionId)
                || (statusValue is not "BOUND" and not "CLEANUP_APPLIED" and not "DETACHED_SOFT"))
            {
                result = CorruptManifestResult(command, "Existing adoption manifest is missing required P1-P4 state. No writes were performed.");
                return false;
            }

            generation = manifest["generation"]?.GetValue<int>() ?? 1;
            if (requireP4Evidence)
            {
                var proposalHash = manifest["last_proposal_hash"]?.GetValue<string>();
                var proposalArtifacts = ReadStringArray(manifest["last_proposal_artifacts"]);
                if (string.IsNullOrWhiteSpace(proposalHash)
                    || proposalArtifacts.Count == 0
                    || proposalArtifacts.Any(path => !File.Exists(ToFullPath(path))))
                {
                    result = new P5Result(
                        24,
                        BuildP5Report(
                            "blocked",
                            "P4_PROPOSALS_MISSING",
                            24,
                            command,
                            adoptionId,
                            generation,
                            "P5 requires existing accepted P4 proposal artifacts. No destructive operation was performed.",
                            repoRoot,
                            Array.Empty<string>(),
                            Array.Empty<string>(),
                            Array.Empty<string>(),
                            Array.Empty<string>()));
                    return false;
                }
            }

            return true;
        }

        private bool TryLoadPlan(
            string command,
            string planId,
            string expectedHash,
            string? adoptionId,
            int generation,
            out JsonObject? plan,
            out P5Result? result)
        {
            plan = null;
            result = null;
            if (string.IsNullOrWhiteSpace(planId) || planId.Contains("..", StringComparison.Ordinal) || planId.Contains('/', StringComparison.Ordinal) || planId.Contains('\\', StringComparison.Ordinal))
            {
                result = new P5Result(
                    2,
                    BuildP5Report("blocked", "INVALID_PLAN_ID", 2, command, adoptionId, generation, "Cleanup plan id is invalid.", repoRoot, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()));
                return false;
            }

            var path = ToFullPath($".ai/runtime/adoption/cleanup/cleanup-plan-{planId}.json");
            if (!File.Exists(path))
            {
                result = new P5Result(
                    24,
                    BuildP5Report("blocked", "CLEANUP_PLAN_MISSING", 24, command, adoptionId, generation, "Cleanup apply requires an existing cleanup plan.", repoRoot, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()));
                return false;
            }

            try
            {
                plan = JsonNode.Parse(File.ReadAllText(path))?.AsObject()
                    ?? throw new JsonException("Cleanup plan root is not an object.");
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                result = new P5Result(
                    24,
                    BuildP5Report("blocked", "CLEANUP_PLAN_CORRUPT", 24, command, adoptionId, generation, "Cleanup plan is corrupt. No writes were performed.", repoRoot, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()));
                return false;
            }

            var actualHash = plan["plan_hash"]?.GetValue<string>();
            if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
            {
                result = new P5Result(
                    26,
                    BuildP5Report("blocked", "PLAN_HASH_MISMATCH", 26, command, adoptionId, generation, "Cleanup apply requires exact plan hash match. No writes were performed.", repoRoot, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()));
                return false;
            }

            return true;
        }

        private PlanCapture CapturePlan(JsonObject manifest, string adoptionId, int generation)
        {
            var planId = generation.ToString("D4");
            var planPath = $".ai/runtime/adoption/cleanup/cleanup-plan-{planId}.json";
            var referenceIndex = BuildReferenceIndex(manifest, adoptionId, generation);
            var referencedPaths = ReadStringArray(referenceIndex["referenced_paths"]).ToHashSet(StringComparer.Ordinal);
            var ledger = ReadLatestLedger();
            var caseAmbiguousPaths = BuildCaseAmbiguousPaths(ledger.Values.Select(item => item.Path));
            var candidates = ledger.Values
                .Where(item => item.Path.StartsWith(".ai/runtime/adoption/", StringComparison.Ordinal))
                .OrderBy(item => item.Path, StringComparer.Ordinal)
                .Select(item => BuildPlanCandidate(item, referencedPaths, caseAmbiguousPaths))
                .ToArray();
            var candidateArray = new JsonArray(candidates.Select(ToJsonNode).ToArray());
            var plan = new JsonObject
            {
                ["schema_version"] = P5CleanupPlanSchemaVersion,
                ["plan_id"] = planId,
                ["plan_hash"] = "sha256:self",
                ["status"] = "DRY_RUN",
                ["adoption_id"] = adoptionId,
                ["generation"] = generation,
                ["created_at"] = DateTimeOffset.UtcNow.ToString("O"),
                ["dry_run_only"] = true,
                ["delete_by_prefix"] = false,
                ["physical_delete_planned"] = false,
                ["reference_index_path"] = ".ai/runtime/adoption/references.json",
                ["allowed_decisions"] = new JsonArray("delete", "archive_only", "refuse", "operator_review_required"),
                ["candidates"] = candidateArray,
            };
            plan["plan_hash"] = "sha256:" + Sha256Hex(SerializeCanonical(plan));
            return new PlanCapture(planId, planPath, plan["plan_hash"]!.GetValue<string>(), referenceIndex, plan, candidates);
        }

        private JsonObject BuildReferenceIndex(JsonObject manifest, string adoptionId, int generation)
        {
            var runtimeArtifacts = Directory.Exists(adoptionRoot)
                ? Directory.EnumerateFiles(adoptionRoot, "*", SearchOption.AllDirectories)
                    .Select(path => Path.GetRelativePath(repoRoot, path).Replace('\\', '/'))
                    .Where(path => !path.EndsWith(".tmp", StringComparison.Ordinal) && !path.EndsWith("adoption.lock.json", StringComparison.Ordinal))
                    .Order(StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<string>();
            var edges = new List<JsonObject>();
            var referenced = new SortedSet<string>(StringComparer.Ordinal);

            AddManifestReferences("manifest:last_intake_artifacts", manifest["last_intake_artifacts"], edges, referenced);
            AddManifestReferences("manifest:last_proposal_artifacts", manifest["last_proposal_artifacts"], edges, referenced);
            AddManifestReferences("manifest:last_cleanup_artifacts", manifest["last_cleanup_artifacts"], edges, referenced);

            var artifactSet = runtimeArtifacts.ToHashSet(StringComparer.Ordinal);
            foreach (var scope in runtimeArtifacts.Concat(ReferenceTruthRoots))
            {
                var scopePath = ToFullPath(scope);
                if (File.Exists(scopePath))
                {
                    if (!IsSymlink(scopePath))
                    {
                        AddTextReferences(scope, File.ReadAllText(scopePath), artifactSet, edges, referenced);
                    }

                    continue;
                }

                if (!Directory.Exists(scopePath))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(scopePath, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
                    if (relative.EndsWith(".tmp", StringComparison.Ordinal) || relative.EndsWith("adoption.lock.json", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!IsSymlink(file))
                    {
                        AddTextReferences(relative, File.ReadAllText(file), artifactSet, edges, referenced);
                    }
                }
            }

            return new JsonObject
            {
                ["schema_version"] = P5ReferenceIndexSchemaVersion,
                ["adoption_id"] = adoptionId,
                ["generation"] = generation,
                ["created_at"] = DateTimeOffset.UtcNow.ToString("O"),
                ["status"] = "DERIVED_NON_CANONICAL",
                ["canonical_truth"] = false,
                ["planner_authoritative"] = false,
                ["scanned_runtime_artifact_count"] = runtimeArtifacts.Length,
                ["scanned_governance_roots"] = new JsonArray(ReferenceTruthRoots.Select(item => JsonValue.Create(item)).ToArray()),
                ["runtime_artifacts"] = new JsonArray(runtimeArtifacts.Select(item => JsonValue.Create(item)).ToArray()),
                ["referenced_paths"] = new JsonArray(referenced.Select(item => JsonValue.Create(item)).ToArray()),
                ["reference_edges"] = new JsonArray(edges.Select(edge => edge.DeepClone()).ToArray()),
                ["reference_index_rebuildable"] = true,
                ["physical_delete_requires_reference_check"] = true,
            };
        }

        private IReadOnlyList<CleanupSafetyFailure> FindApplySafetyFailures(JsonObject plan, JsonObject manifest, string adoptionId, int generation)
        {
            var referenceIndex = BuildReferenceIndex(manifest, adoptionId, generation);
            var referencedPaths = ReadStringArray(referenceIndex["referenced_paths"]).ToHashSet(StringComparer.Ordinal);
            var ledger = ReadLatestLedger();
            var caseAmbiguousPaths = BuildCaseAmbiguousPaths(ledger.Values.Select(item => item.Path));
            var failures = new List<CleanupSafetyFailure>();
            var candidates = plan["candidates"] as JsonArray ?? new JsonArray();
            foreach (var candidateNode in candidates)
            {
                var candidate = candidateNode?.AsObject();
                var path = candidate?["path"]?.GetValue<string>();
                var decision = candidate?["decision"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(path)
                    || (decision is not "archive_only" and not "delete"))
                {
                    continue;
                }

                var pathGuard = EvaluatePathGuard(path, caseAmbiguousPaths);
                if (!string.Equals(pathGuard.Decision, "allow", StringComparison.Ordinal))
                {
                    failures.Add(new CleanupSafetyFailure(path, pathGuard.Reason, pathGuard, CurrentHash: "unread", ExpectedHash: ReadString(candidate?["current_hash"]) ?? ReadString(candidate?["ledger_hash"]) ?? "unknown"));
                    continue;
                }

                if (!ledger.TryGetValue(path, out var artifact))
                {
                    failures.Add(new CleanupSafetyFailure(path, "ledger_record_missing_after_plan", pathGuard, CurrentHash: "missing", ExpectedHash: ReadString(candidate?["ledger_hash"]) ?? "unknown"));
                    continue;
                }

                if (!string.Equals(artifact.Owner, "CARVES", StringComparison.Ordinal))
                {
                    failures.Add(new CleanupSafetyFailure(path, "ledger_owner_not_carves_after_plan", pathGuard, CurrentHash: "unread", ExpectedHash: artifact.Hash));
                    continue;
                }

                if (artifact.HumanModified)
                {
                    failures.Add(new CleanupSafetyFailure(path, "human_modified_after_plan", pathGuard, CurrentHash: "unread", ExpectedHash: artifact.Hash));
                    continue;
                }

                var expectedHash = ReadString(candidate?["current_hash"]) ?? artifact.Hash;
                var currentHash = ComputeCurrentArtifactHash(path, pathGuard);
                if (!string.Equals(currentHash, expectedHash, StringComparison.Ordinal)
                    || !string.Equals(currentHash, artifact.Hash, StringComparison.Ordinal))
                {
                    failures.Add(new CleanupSafetyFailure(path, "hash_mismatch_after_plan", pathGuard, currentHash, expectedHash));
                    continue;
                }

                if (string.Equals(decision, "delete", StringComparison.Ordinal) && referencedPaths.Contains(path))
                {
                    failures.Add(new CleanupSafetyFailure(path, "referenced_after_plan", pathGuard, currentHash, expectedHash));
                }
            }

            return failures;
        }

        private CleanupApplyCapture BuildRefusedCleanupProof(string adoptionId, string planId, string planHash, IReadOnlyList<CleanupSafetyFailure> failures)
        {
            var cleanupId = planId;
            var proofPath = $".ai/runtime/adoption/cleanup/cleanup-proof-{cleanupId}.json";
            var refused = failures.Select(item => item.Path).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var proof = new JsonObject
            {
                ["schema_version"] = P5CleanupProofSchemaVersion,
                ["cleanup_id"] = cleanupId,
                ["adoption_id"] = adoptionId,
                ["status"] = "REFUSED",
                ["applied_plan_id"] = planId,
                ["applied_plan_hash"] = planHash,
                ["created_at"] = DateTimeOffset.UtcNow.ToString("O"),
                ["deleted_paths"] = new JsonArray(),
                ["archived_paths"] = new JsonArray(),
                ["archive_artifacts"] = new JsonArray(),
                ["refused_paths"] = new JsonArray(refused.Select(item => JsonValue.Create(item)).ToArray()),
                ["refusal_evidence"] = new JsonArray(failures.Select(ToJsonNode).ToArray()),
                ["postflight_checks"] = new JsonObject
                {
                    ["deleted_paths_subset_of_plan"] = true,
                    ["source_files_deleted"] = false,
                    ["approved_truth_deleted"] = false,
                    ["referenced_evidence_deleted"] = false,
                    ["human_modified_generated_file_deleted"] = false,
                    ["cleanup_continued_after_failure"] = false,
                },
                ["immutable_after_write"] = true,
            };

            return new CleanupApplyCapture(cleanupId, proofPath, proof, Array.Empty<string>(), Array.Empty<string>(), refused, Array.Empty<string>());
        }

        private CleanupApplyCapture ApplyPlan(string adoptionId, string planId, string planHash, JsonObject plan)
        {
            var cleanupId = planId;
            var proofPath = $".ai/runtime/adoption/cleanup/cleanup-proof-{cleanupId}.json";
            var deleted = new List<string>();
            var archived = new List<string>();
            var refused = new List<string>();
            var archiveArtifacts = new List<string>();
            var refusalEvidence = new List<JsonNode>();
            var candidates = plan["candidates"] as JsonArray ?? new JsonArray();
            foreach (var candidateNode in candidates)
            {
                var candidate = candidateNode?.AsObject();
                var path = candidate?["path"]?.GetValue<string>();
                var decision = candidate?["decision"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(decision))
                {
                    continue;
                }

                if (string.Equals(decision, "archive_only", StringComparison.Ordinal))
                {
                    var archivePath = ArchivePathFor(planId, path);
                    var sourcePath = ToFullPath(path);
                    if (File.Exists(sourcePath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(ToFullPath(archivePath))!);
                        File.Copy(sourcePath, ToFullPath(archivePath), overwrite: true);
                        FlushFile(ToFullPath(archivePath));
                        archived.Add(path);
                        archiveArtifacts.Add(archivePath);
                    }
                    else
                    {
                        refused.Add(path);
                        refusalEvidence.Add(BuildCandidateRefusalEvidence(candidate!, "missing_after_plan"));
                    }

                    continue;
                }

                if (string.Equals(decision, "delete", StringComparison.Ordinal) && CanPhysicallyDelete(candidate!))
                {
                    File.Delete(ToFullPath(path));
                    deleted.Add(path);
                    continue;
                }

                refused.Add(path);
                refusalEvidence.Add(BuildCandidateRefusalEvidence(candidate!, ReadString(candidate?["reason"]) ?? "protected_or_not_deletable"));
            }

            var proof = new JsonObject
            {
                ["schema_version"] = P5CleanupProofSchemaVersion,
                ["cleanup_id"] = cleanupId,
                ["adoption_id"] = adoptionId,
                ["status"] = "APPLIED",
                ["applied_plan_id"] = planId,
                ["applied_plan_hash"] = planHash,
                ["created_at"] = DateTimeOffset.UtcNow.ToString("O"),
                ["deleted_paths"] = new JsonArray(deleted.Select(item => JsonValue.Create(item)).ToArray()),
                ["archived_paths"] = new JsonArray(archived.Select(item => JsonValue.Create(item)).ToArray()),
                ["archive_artifacts"] = new JsonArray(archiveArtifacts.Select(item => JsonValue.Create(item)).ToArray()),
                ["refused_paths"] = new JsonArray(refused.Select(item => JsonValue.Create(item)).ToArray()),
                ["refusal_evidence"] = new JsonArray(refusalEvidence.ToArray()),
                ["postflight_checks"] = new JsonObject
                {
                    ["deleted_paths_subset_of_plan"] = true,
                    ["source_files_deleted"] = false,
                    ["approved_truth_deleted"] = false,
                    ["referenced_evidence_deleted"] = false,
                    ["human_modified_generated_file_deleted"] = false,
                    ["cleanup_continued_after_failure"] = false,
                },
                ["immutable_after_write"] = true,
            };

            return new CleanupApplyCapture(cleanupId, proofPath, proof, deleted, archived, refused, archiveArtifacts);
        }

        private PlanCandidate BuildPlanCandidate(LedgerArtifact artifact, ISet<string> referencedPaths, ISet<string> caseAmbiguousPaths)
        {
            var pathGuard = EvaluatePathGuard(artifact.Path, caseAmbiguousPaths);
            var currentHash = ComputeCurrentArtifactHash(artifact.Path, pathGuard);
            var hashMatches = string.Equals(currentHash, artifact.Hash, StringComparison.Ordinal);
            var sourceProtected = IsSourceOrInstructionPath(artifact.Path);
            var truthProtected = IsApprovedTruthOrAuditPath(artifact.Path);
            var ledgerOwned = string.Equals(artifact.Owner, "CARVES", StringComparison.Ordinal);
            var safePath = string.Equals(pathGuard.Decision, "allow", StringComparison.Ordinal);
            var referenced = referencedPaths.Contains(artifact.Path);
            var humanModified = artifact.HumanModified || !hashMatches;
            var decision = "refuse";
            var reason = "protected_or_not_deletable";
            if (!ledgerOwned)
            {
                reason = "ledger_owner_not_carves";
            }
            else if (!safePath)
            {
                reason = pathGuard.Reason;
            }
            else if (sourceProtected)
            {
                reason = "source_or_human_instruction_protected";
            }
            else if (truthProtected)
            {
                reason = "approved_truth_or_audit_record_protected";
            }
            else if (!hashMatches)
            {
                reason = "hash_mismatch";
            }
            else if (humanModified)
            {
                reason = "human_modified";
            }
            else if (referenced)
            {
                decision = "archive_only";
                reason = "referenced_by_runtime_or_governance_truth";
            }
            else if (artifact.Path.Contains("/snapshots/", StringComparison.Ordinal) || artifact.Path.Contains("/proposals/", StringComparison.Ordinal))
            {
                decision = "archive_only";
                reason = "carves_owned_runtime_adoption_artifact";
            }

            return new PlanCandidate(
                artifact.Path,
                decision,
                reason,
                ledgerOwned,
                hashMatches,
                humanModified,
                referenced,
                sourceProtected,
                truthProtected,
                safePath,
                currentHash,
                artifact.Hash,
                artifact.Kind,
                pathGuard.CanonicalPath,
                pathGuard.Decision,
                pathGuard.Reason,
                pathGuard.ResolvesInsideRepoRoot,
                pathGuard.Symlink,
                pathGuard.CaseCollisionAmbiguous);
        }

        private Dictionary<string, LedgerArtifact> ReadLatestLedger()
        {
            var artifacts = new Dictionary<string, LedgerArtifact>(StringComparer.Ordinal);
            if (!File.Exists(ledgerPath))
            {
                return artifacts;
            }

            foreach (var line in File.ReadLines(ledgerPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var row = JsonNode.Parse(line)?.AsObject();
                    var path = row?["artifact_path"]?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }

                    artifacts[path] = new LedgerArtifact(
                        path,
                        row?["artifact_kind"]?.GetValue<string>() ?? "unknown",
                        row?["owner"]?.GetValue<string>() ?? "unknown",
                        row?["hash"]?.GetValue<string>() ?? "sha256:unknown",
                        row?["human_modified"]?.GetValue<bool>() ?? false,
                        row?["delete_policy"]?.GetValue<string>() ?? "unknown");
                }
                catch (JsonException)
                {
                    continue;
                }
            }

            return artifacts;
        }

        private static ISet<string> BuildCaseAmbiguousPaths(IEnumerable<string> paths)
            => paths
                .Select(path => path.Replace('\\', '/'))
                .GroupBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Distinct(StringComparer.Ordinal).Count() > 1)
                .SelectMany(group => group)
                .ToHashSet(StringComparer.Ordinal);

        private PathGuard EvaluatePathGuard(string path, ISet<string> caseAmbiguousPaths)
        {
            var normalizedPath = path.Replace('\\', '/');
            if (Path.IsPathRooted(normalizedPath) || normalizedPath.Contains("..", StringComparison.Ordinal))
            {
                return new PathGuard("refuse", "outside_repo", normalizedPath, ResolvesInsideRepoRoot: false, Symlink: false, CaseCollisionAmbiguous: false);
            }

            if (!normalizedPath.StartsWith(".ai/runtime/adoption/", StringComparison.Ordinal))
            {
                return new PathGuard("refuse", "outside_adoption_runtime", normalizedPath, ResolvesInsideRepoRoot: false, Symlink: false, CaseCollisionAmbiguous: false);
            }

            var fullPath = Path.GetFullPath(ToFullPath(normalizedPath));
            var repoFullPath = Path.GetFullPath(repoRoot);
            var insideRepo = IsPathInside(fullPath, repoFullPath);
            if (!insideRepo)
            {
                return new PathGuard("refuse", "outside_repo", fullPath, ResolvesInsideRepoRoot: false, Symlink: false, CaseCollisionAmbiguous: false);
            }

            var caseAmbiguous = caseAmbiguousPaths.Contains(normalizedPath);
            if (caseAmbiguous)
            {
                return new PathGuard("refuse", "case_collision_ambiguity", fullPath, ResolvesInsideRepoRoot: true, Symlink: false, CaseCollisionAmbiguous: true);
            }

            var symlink = IsSymlink(fullPath);
            if (symlink)
            {
                var reason = SymlinkTargetResolvesInsideRepo(fullPath, repoFullPath)
                    ? "symlink_target_deletion_refused"
                    : "symlink_escape";
                return new PathGuard("refuse", reason, fullPath, ResolvesInsideRepoRoot: true, Symlink: true, CaseCollisionAmbiguous: false);
            }

            return new PathGuard("allow", "path_guard_pass", fullPath, ResolvesInsideRepoRoot: true, Symlink: false, CaseCollisionAmbiguous: false);
        }

        private string ComputeCurrentArtifactHash(string path, PathGuard pathGuard)
        {
            var fullPath = ToFullPath(path);
            if (!string.Equals(pathGuard.Decision, "allow", StringComparison.Ordinal) || !File.Exists(fullPath))
            {
                return File.Exists(fullPath) ? "unsafe_path" : "missing";
            }

            return "sha256:" + ComputeFileSha256(fullPath);
        }

        private bool CanPhysicallyDelete(JsonObject candidate)
        {
            var path = candidate["path"]?.GetValue<string>();
            return !string.IsNullOrWhiteSpace(path)
                && candidate["ledger_owned"]?.GetValue<bool>() == true
                && candidate["hash_matches"]?.GetValue<bool>() == true
                && candidate["human_modified"]?.GetValue<bool>() == false
                && candidate["referenced"]?.GetValue<bool>() == false
                && candidate["source_protected"]?.GetValue<bool>() == false
                && candidate["truth_protected"]?.GetValue<bool>() == false
                && candidate["safe_path"]?.GetValue<bool>() == true
                && string.Equals(candidate["path_guard"]?["decision"]?.GetValue<string>(), "allow", StringComparison.Ordinal)
                && File.Exists(ToFullPath(path));
        }

        private string WriteExport(string exportPath, JsonObject manifest, string adoptionId, int generation, DateTimeOffset createdAt)
        {
            var absolutePath = Path.GetFullPath(exportPath, repoRoot);
            var adoptionFullPath = Path.GetFullPath(adoptionRoot);
            if (absolutePath.StartsWith(adoptionFullPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new IOException("Export path cannot be inside the destructive cleanup tree.");
            }

            var outputPath = Path.HasExtension(absolutePath)
                ? absolutePath
                : Path.Combine(absolutePath, "legacy-adoption-export.json");
            var export = new JsonObject
            {
                ["schema_version"] = "legacy_adoption.p5_export.v0.1.0",
                ["adoption_id"] = adoptionId,
                ["generation"] = generation,
                ["created_at"] = createdAt.ToString("O"),
                ["soft_detach_export"] = true,
                ["source_deleted"] = false,
                ["approved_truth_deleted"] = false,
                ["manifest"] = manifest.DeepClone(),
                ["events_path"] = ".ai/runtime/adoption/events.jsonl",
                ["ledger_path"] = ".ai/runtime/adoption/ownership-ledger.jsonl",
            };
            WriteJsonAtomic(outputPath, export);
            return outputPath;
        }

        private void UpdateManifestAfterCleanup(JsonObject manifest, int generation, string planId, string planHash, string proofPath, DateTimeOffset updatedAt)
        {
            manifest["status"] = "CLEANUP_APPLIED";
            manifest["generation"] = generation;
            manifest["updated_at"] = updatedAt.ToString("O");
            manifest["last_cleanup_plan_id"] = planId;
            manifest["last_cleanup_plan_hash"] = planHash;
            manifest["last_cleanup_proof"] = proofPath;
            manifest["last_cleanup_artifacts"] = new JsonArray(proofPath);
            manifest["last_manifest_hash"] = "sha256:self";
            manifest["last_manifest_hash"] = "sha256:" + Sha256Hex(SerializeCanonical(manifest));
        }

        private P5Result ExistingLockResult(string command)
        {
            var status = "LOCK_CONFLICT";
            var result = "blocked";
            var exitCode = 20;
            var message = "Fresh adoption lock exists. No writes were performed.";
            try
            {
                var lockJson = JsonNode.Parse(File.ReadAllText(lockPath))?.AsObject();
                var expiresAtText = lockJson?["expires_at"]?.GetValue<string>();
                if (DateTimeOffset.TryParse(expiresAtText, out var expiresAt) && expiresAt <= DateTimeOffset.UtcNow)
                {
                    status = "RECOVERY_REQUIRED";
                    result = "recovery_required";
                    exitCode = 25;
                    message = "Expired adoption lock exists. Lock takeover is forbidden in v0 and no writes were performed.";
                }
            }
            catch (JsonException)
            {
                status = "RECOVERY_REQUIRED";
                result = "recovery_required";
                exitCode = 25;
                message = "Unreadable adoption lock exists. Lock takeover is forbidden in v0 and no writes were performed.";
            }

            return new P5Result(
                exitCode,
                BuildP5Report(result, status, exitCode, command, null, null, message, repoRoot, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()));
        }

        private P5Result CorruptManifestResult(string command, string message)
            => new(
                21,
                BuildP5Report(
                    "blocked",
                    "MANIFEST_CORRUPT",
                    21,
                    command,
                    null,
                    null,
                    message,
                    repoRoot,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>()));

        private P5Result RecoveryRequired(string command, string? adoptionId, int generation, string message)
            => new(
                25,
                BuildP5Report(
                    "recovery_required",
                    "RECOVERY_REQUIRED",
                    25,
                    command,
                    adoptionId,
                    generation,
                    message,
                    repoRoot,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>()));

        private void AcquireLock(string schemaVersion)
        {
            Directory.CreateDirectory(adoptionRoot);
            var lockJson = new JsonObject
            {
                ["schema_version"] = schemaVersion,
                ["created_at"] = DateTimeOffset.UtcNow.ToString("O"),
                ["expires_at"] = DateTimeOffset.UtcNow.AddMinutes(15).ToString("O"),
                ["takeover_allowed"] = false,
            };
            using var stream = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            lockJson.WriteTo(writer);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        private JsonObject BuildEventRecord(string adoptionId, string eventType, string result, int generation, IReadOnlyList<string> artifactPaths, DateTimeOffset createdAt)
            => new()
            {
                ["schema_version"] = P2AttachEventSchemaVersion,
                ["event_id"] = "evt_" + Guid.NewGuid().ToString("N"),
                ["adoption_id"] = adoptionId,
                ["event_type"] = eventType,
                ["created_at"] = createdAt.ToString("O"),
                ["generation"] = generation,
                ["artifact_paths"] = new JsonArray(artifactPaths.Select(item => JsonValue.Create(item)).ToArray()),
                ["result"] = result,
            };

        private JsonObject BuildLedgerRecord(string adoptionId, string artifactPath, string artifactKind, DateTimeOffset createdAt)
        {
            var absolutePath = ToFullPath(artifactPath);
            return new JsonObject
            {
                ["schema_version"] = P2AttachLedgerSchemaVersion,
                ["ledger_record_id"] = "led_" + Guid.NewGuid().ToString("N"),
                ["adoption_id"] = adoptionId,
                ["artifact_path"] = artifactPath,
                ["artifact_kind"] = artifactKind,
                ["owner"] = "CARVES",
                ["hash"] = File.Exists(absolutePath) ? "sha256:" + ComputeFileSha256(absolutePath) : "sha256:" + Sha256Hex(Array.Empty<byte>()),
                ["created_at"] = createdAt.ToString("O"),
                ["delete_policy"] = "retain_for_audit",
                ["human_modified"] = false,
            };
        }

        private string ToFullPath(string relativePath)
            => Path.IsPathRooted(relativePath)
                ? relativePath
                : Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

        private static void AddManifestReferences(string source, JsonNode? node, List<JsonObject> edges, ISet<string> referenced)
        {
            foreach (var target in ReadStringArray(node))
            {
                referenced.Add(target);
                edges.Add(new JsonObject
                {
                    ["source"] = source,
                    ["target"] = target,
                    ["reference_kind"] = "manifest_reference",
                });
            }
        }

        private static void AddTextReferences(string source, string content, ISet<string> knownArtifacts, List<JsonObject> edges, ISet<string> referenced)
        {
            foreach (var target in knownArtifacts)
            {
                if (!string.Equals(source, target, StringComparison.Ordinal) && content.Contains(target, StringComparison.Ordinal))
                {
                    referenced.Add(target);
                    edges.Add(new JsonObject
                    {
                        ["source"] = source,
                        ["target"] = target,
                        ["reference_kind"] = "content_reference",
                    });
                }
            }
        }

        private static JsonNode ToJsonNode(PlanCandidate candidate)
            => new JsonObject
            {
                ["path"] = candidate.Path,
                ["canonical_path"] = candidate.CanonicalPath,
                ["decision"] = candidate.Decision,
                ["reason"] = candidate.Reason,
                ["ledger_owned"] = candidate.LedgerOwned,
                ["owner"] = "CARVES",
                ["hash_matches"] = candidate.HashMatches,
                ["human_modified"] = candidate.HumanModified,
                ["referenced"] = candidate.Referenced,
                ["source_protected"] = candidate.SourceProtected,
                ["truth_protected"] = candidate.TruthProtected,
                ["safe_path"] = candidate.SafePath,
                ["path_guard"] = BuildPathGuardNode(
                    candidate.PathGuardDecision,
                    candidate.PathGuardReason,
                    candidate.CanonicalPath,
                    candidate.PathResolvesInsideRepoRoot,
                    candidate.Symlink,
                    candidate.CaseCollisionAmbiguous),
                ["current_hash"] = candidate.CurrentHash,
                ["ledger_hash"] = candidate.LedgerHash,
                ["artifact_kind"] = candidate.ArtifactKind,
            };

        private static JsonNode ToJsonNode(CleanupSafetyFailure failure)
            => new JsonObject
            {
                ["path"] = failure.Path,
                ["reason"] = failure.Reason,
                ["current_hash"] = failure.CurrentHash,
                ["expected_hash"] = failure.ExpectedHash,
                ["canonical_path"] = failure.PathGuard.CanonicalPath,
                ["path_guard"] = BuildPathGuardNode(
                    failure.PathGuard.Decision,
                    failure.PathGuard.Reason,
                    failure.PathGuard.CanonicalPath,
                    failure.PathGuard.ResolvesInsideRepoRoot,
                    failure.PathGuard.Symlink,
                    failure.PathGuard.CaseCollisionAmbiguous),
            };

        private static JsonNode BuildCandidateRefusalEvidence(JsonObject candidate, string reason)
        {
            var pathGuard = candidate["path_guard"]?.DeepClone() ?? new JsonObject();
            return new JsonObject
            {
                ["path"] = ReadString(candidate["path"]) ?? "",
                ["reason"] = reason,
                ["current_hash"] = ReadString(candidate["current_hash"]) ?? "unknown",
                ["expected_hash"] = ReadString(candidate["ledger_hash"]) ?? "unknown",
                ["canonical_path"] = ReadString(candidate["canonical_path"]) ?? "",
                ["path_guard"] = pathGuard,
            };
        }

        private static JsonObject BuildPathGuardNode(
            string decision,
            string reason,
            string canonicalPath,
            bool resolvesInsideRepoRoot,
            bool symlink,
            bool caseCollisionAmbiguous)
            => new()
            {
                ["decision"] = decision,
                ["reason"] = reason,
                ["canonical_path"] = canonicalPath,
                ["path_resolves_inside_repo_root"] = resolvesInsideRepoRoot,
                ["symlink"] = symlink,
                ["case_collision_ambiguity"] = caseCollisionAmbiguous,
            };

        private static string ResolveP5ArtifactKind(string artifactPath)
        {
            if (artifactPath.EndsWith("references.json", StringComparison.Ordinal))
            {
                return "p5_reference_index";
            }

            if (artifactPath.Contains("/cleanup/cleanup-plan-", StringComparison.Ordinal))
            {
                return "p5_cleanup_plan";
            }

            if (artifactPath.Contains("/cleanup/cleanup-proof-", StringComparison.Ordinal))
            {
                return "p5_cleanup_proof";
            }

            return artifactPath.Contains("/archive/", StringComparison.Ordinal)
                ? "p5_archive_copy"
                : "p5_runtime_artifact";
        }

        private static IReadOnlyList<string> ReadStringArray(JsonNode? node)
            => node is JsonArray array
                ? array.Select(item => item?.GetValue<string>()).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).ToArray()
                : Array.Empty<string>();

        private static bool TryReadJsonObject(string path, out JsonObject? payload)
        {
            try
            {
                payload = JsonNode.Parse(File.ReadAllText(path))?.AsObject();
                return payload is not null;
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                payload = null;
                return false;
            }
        }

        private static bool IsSafeRepoRelativePath(string path)
        {
            if (Path.IsPathRooted(path) || path.Contains("..", StringComparison.Ordinal))
            {
                return false;
            }

            return path.StartsWith(".ai/runtime/adoption/", StringComparison.Ordinal);
        }

        private static string? ReadString(JsonNode? node)
            => node is null ? null : node.GetValue<string>();

        private static bool IsPathInside(string fullPath, string rootPath)
        {
            var normalizedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(normalizedRoot, StringComparison.Ordinal);
        }

        private static bool IsSymlink(string fullPath)
        {
            try
            {
                return (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException)
            {
                return false;
            }
        }

        private static bool SymlinkTargetResolvesInsideRepo(string fullPath, string repoFullPath)
        {
            try
            {
                var linkTarget = new FileInfo(fullPath).LinkTarget;
                if (string.IsNullOrWhiteSpace(linkTarget))
                {
                    return false;
                }

                var targetFullPath = Path.GetFullPath(
                    Path.IsPathRooted(linkTarget)
                        ? linkTarget
                        : Path.Combine(Path.GetDirectoryName(fullPath)!, linkTarget));
                return IsPathInside(targetFullPath, repoFullPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return false;
            }
        }

        private static bool IsSourceOrInstructionPath(string path)
            => !path.StartsWith(".ai/runtime/adoption/", StringComparison.Ordinal)
                || string.Equals(path, "AGENTS.md", StringComparison.Ordinal)
                || string.Equals(path, "CLAUDE.md", StringComparison.Ordinal)
                || path.StartsWith(".cursor/", StringComparison.Ordinal);

        private static bool IsApprovedTruthOrAuditPath(string path)
            => path.StartsWith(".ai/memory/", StringComparison.Ordinal)
                || path.StartsWith(".ai/tasks/", StringComparison.Ordinal)
                || path.StartsWith(".ai/codegraph/", StringComparison.Ordinal)
                || path.StartsWith(".ai/patches/", StringComparison.Ordinal)
                || path.StartsWith(".ai/reviews/", StringComparison.Ordinal)
                || string.Equals(path, ".ai/runtime/adoption/adoption.json", StringComparison.Ordinal)
                || string.Equals(path, ".ai/runtime/adoption/events.jsonl", StringComparison.Ordinal)
                || string.Equals(path, ".ai/runtime/adoption/ownership-ledger.jsonl", StringComparison.Ordinal)
                || string.Equals(path, ".ai/runtime/adoption/references.json", StringComparison.Ordinal)
                || path.Contains("/cleanup/cleanup-plan-", StringComparison.Ordinal)
                || path.Contains("/cleanup/cleanup-proof-", StringComparison.Ordinal);

        private static string ArchivePathFor(string planId, string sourcePath)
            => ".ai/runtime/adoption/archive/" + planId + "/" + sourcePath;

        private static byte[] SerializeCanonical(JsonObject payload)
            => Encoding.UTF8.GetBytes(payload.ToJsonString(new JsonSerializerOptions { PropertyNamingPolicy = null, WriteIndented = false }));

        private static void WriteJsonAtomic(string path, JsonObject payload)
            => WriteTextAtomic(path, payload.ToJsonString(AdoptionJsonOptions));

        private static void WriteTextAtomic(string path, string content)
        {
            var tmpPath = path + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(tmpPath, content);
            FlushFile(tmpPath);
            File.Move(tmpPath, path, overwrite: true);
            FlushFile(path);
        }

        private static void AppendLineDurable(string path, JsonObject payload)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, payload.ToJsonString() + Environment.NewLine);
            FlushFile(path);
        }

        private static void FlushFile(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            stream.Flush(flushToDisk: true);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }

    private sealed class LegacyAdoptionProposalService
    {
        private readonly string repoRoot;
        private readonly string adoptionRoot;
        private readonly string manifestPath;
        private readonly string eventsPath;
        private readonly string ledgerPath;
        private readonly string lockPath;

        public LegacyAdoptionProposalService(string repoRoot)
        {
            this.repoRoot = repoRoot;
            adoptionRoot = Path.Combine(repoRoot, ".ai", "runtime", "adoption");
            manifestPath = Path.Combine(adoptionRoot, "adoption.json");
            eventsPath = Path.Combine(adoptionRoot, "events.jsonl");
            ledgerPath = Path.Combine(adoptionRoot, "ownership-ledger.jsonl");
            lockPath = Path.Combine(adoptionRoot, "adoption.lock.json");
        }

        public ProposeResult Propose()
        {
            if (File.Exists(lockPath))
            {
                return ExistingLockResult();
            }

            if (!File.Exists(manifestPath))
            {
                var status = Directory.Exists(Path.Combine(repoRoot, ".ai")) ? "ADOPTION_UNKNOWN" : "ADOPTION_MISSING";
                return new ProposeResult(
                    status == "ADOPTION_UNKNOWN" ? 22 : 23,
                    BuildProposeReport(
                        "blocked",
                        status,
                        status == "ADOPTION_UNKNOWN" ? 22 : 23,
                        null,
                        null,
                        "P4 proposal generation requires a valid P2 binding and P3 intake evidence. No destructive operation was performed.",
                        repoRoot,
                        Array.Empty<string>()));
            }

            if (!TryLoadManifest(out var manifest, out var adoptionId, out var currentGeneration, out var corruptResult))
            {
                return corruptResult!;
            }

            if (!TryLoadP3Evidence(manifest!, out var evidence, out var evidenceResult))
            {
                return evidenceResult!;
            }

            var capture = CaptureProposals(adoptionId!, currentGeneration + 1, evidence!);
            var previousProposalHash = manifest!["last_proposal_hash"]?.GetValue<string>();
            var previousArtifacts = ReadStringArray(manifest["last_proposal_artifacts"]);
            if (string.Equals(previousProposalHash, capture.AggregateHash, StringComparison.Ordinal)
                && previousArtifacts.All(path => File.Exists(ToFullPath(path))))
            {
                return new ProposeResult(
                    0,
                    BuildProposeReport(
                        "noop",
                        "PROPOSALS_NOOP",
                        0,
                        adoptionId,
                        currentGeneration,
                        "Legacy adoption P4 proposals are already current.",
                        repoRoot,
                        previousArtifacts));
            }

            var lockAcquired = false;
            try
            {
                AcquireLock();
                lockAcquired = true;

                foreach (var artifact in capture.TextArtifacts)
                {
                    WriteTextAtomic(ToFullPath(artifact.Path), artifact.Content);
                }

                foreach (var artifact in capture.JsonArtifacts)
                {
                    WriteJsonAtomic(ToFullPath(artifact.Path), artifact.Payload);
                }

                var now = DateTimeOffset.UtcNow;
                var artifactPaths = capture.AllArtifactPaths;
                AppendLineDurable(eventsPath, BuildEventRecord(adoptionId!, "PROPOSALS_GENERATED", "success", currentGeneration + 1, artifactPaths, now));
                foreach (var artifactPath in artifactPaths)
                {
                    AppendLineDurable(ledgerPath, BuildLedgerRecord(adoptionId!, artifactPath, ResolveArtifactKind(artifactPath), now));
                }

                UpdateManifest(manifest!, currentGeneration + 1, capture.AggregateHash, artifactPaths, now);
                WriteJsonAtomic(manifestPath, manifest!);

                return new ProposeResult(
                    0,
                    BuildProposeReport(
                        "success",
                        "PROPOSALS_GENERATED",
                        0,
                        adoptionId,
                        currentGeneration + 1,
                        "Legacy adoption P4 proposal artifacts generated.",
                        repoRoot,
                        capture.AllArtifactPaths));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                return new ProposeResult(
                    25,
                    BuildProposeReport(
                        "recovery_required",
                        "RECOVERY_REQUIRED",
                        25,
                        adoptionId,
                        currentGeneration,
                        "P4 proposal generation write failed and requires operator recovery.",
                        repoRoot,
                        Array.Empty<string>()));
            }
            finally
            {
                if (lockAcquired)
                {
                    TryDelete(lockPath);
                }
            }
        }

        private bool TryLoadManifest(out JsonObject? manifest, out string? adoptionId, out int generation, out ProposeResult? result)
        {
            manifest = null;
            adoptionId = null;
            generation = 0;
            result = null;

            try
            {
                manifest = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject()
                    ?? throw new JsonException("Manifest root is not an object.");
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                result = CorruptManifestResult("Existing adoption manifest is corrupt. No writes were performed.");
                return false;
            }

            adoptionId = manifest["adoption_id"]?.GetValue<string>();
            var status = manifest["status"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(adoptionId) || !string.Equals(status, "BOUND", StringComparison.Ordinal))
            {
                result = CorruptManifestResult("Existing adoption manifest is missing required BOUND state. No writes were performed.");
                return false;
            }

            generation = manifest["generation"]?.GetValue<int>() ?? 1;
            return true;
        }

        private bool TryLoadP3Evidence(JsonObject manifest, out P3EvidenceState? evidence, out ProposeResult? result)
        {
            evidence = null;
            result = null;
            var intakeHash = manifest["last_intake_evidence_hash"]?.GetValue<string>();
            var intakeArtifacts = ReadStringArray(manifest["last_intake_artifacts"]);
            if (string.IsNullOrWhiteSpace(intakeHash)
                || intakeArtifacts.Count == 0
                || intakeArtifacts.Any(path => !File.Exists(ToFullPath(path))))
            {
                result = new ProposeResult(
                    24,
                    BuildProposeReport(
                        "blocked",
                        "P3_EVIDENCE_MISSING",
                        24,
                        manifest["adoption_id"]?.GetValue<string>(),
                        manifest["generation"]?.GetValue<int>(),
                        "P4 proposal generation requires existing P3 intake evidence. No writes were performed.",
                        repoRoot,
                        Array.Empty<string>()));
                return false;
            }

            var repoScanPath = intakeArtifacts.FirstOrDefault(path => path.Contains("/snapshots/repo-scan-", StringComparison.Ordinal));
            var instructionScanPath = intakeArtifacts.FirstOrDefault(path => path.Contains("/snapshots/instruction-scan-", StringComparison.Ordinal));
            var dirtyDiffPath = intakeArtifacts.FirstOrDefault(path => path.Contains("/snapshots/dirty-diff-", StringComparison.Ordinal));
            if (repoScanPath is null || instructionScanPath is null || dirtyDiffPath is null)
            {
                result = CorruptEvidenceResult("P3 intake evidence artifact set is incomplete. No writes were performed.", manifest);
                return false;
            }

            try
            {
                var repoScan = JsonNode.Parse(File.ReadAllText(ToFullPath(repoScanPath)))?.AsObject()
                    ?? throw new JsonException("Repo scan evidence root is not an object.");
                var instructionScan = JsonNode.Parse(File.ReadAllText(ToFullPath(instructionScanPath)))?.AsObject()
                    ?? throw new JsonException("Instruction scan evidence root is not an object.");
                var artifactHashes = intakeArtifacts
                    .Order(StringComparer.Ordinal)
                    .ToDictionary(path => path, path => "sha256:" + ComputeFileSha256(ToFullPath(path)), StringComparer.Ordinal);
                evidence = new P3EvidenceState(intakeHash!, intakeArtifacts, artifactHashes, repoScan, instructionScan, dirtyDiffPath);
                return true;
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                result = CorruptEvidenceResult("P3 intake evidence is corrupt or unreadable. No writes were performed.", manifest);
                return false;
            }
        }

        private ProposeResult CorruptManifestResult(string message)
            => new(
                21,
                BuildProposeReport(
                    "blocked",
                    "MANIFEST_CORRUPT",
                    21,
                    null,
                    null,
                    message,
                    repoRoot,
                    Array.Empty<string>()));

        private ProposeResult CorruptEvidenceResult(string message, JsonObject manifest)
            => new(
                24,
                BuildProposeReport(
                    "blocked",
                    "P3_EVIDENCE_CORRUPT",
                    24,
                    manifest["adoption_id"]?.GetValue<string>(),
                    manifest["generation"]?.GetValue<int>(),
                    message,
                    repoRoot,
                    Array.Empty<string>()));

        private ProposeResult ExistingLockResult()
        {
            var status = "LOCK_CONFLICT";
            var result = "blocked";
            var exitCode = 20;
            var message = "Fresh adoption lock exists. No writes were performed.";
            try
            {
                var lockJson = JsonNode.Parse(File.ReadAllText(lockPath))?.AsObject();
                var expiresAtText = lockJson?["expires_at"]?.GetValue<string>();
                if (DateTimeOffset.TryParse(expiresAtText, out var expiresAt) && expiresAt <= DateTimeOffset.UtcNow)
                {
                    status = "RECOVERY_REQUIRED";
                    result = "recovery_required";
                    exitCode = 25;
                    message = "Expired adoption lock exists. Lock takeover is forbidden in v0 and no writes were performed.";
                }
            }
            catch (JsonException)
            {
                status = "RECOVERY_REQUIRED";
                result = "recovery_required";
                exitCode = 25;
                message = "Unreadable adoption lock exists. Lock takeover is forbidden in v0 and no writes were performed.";
            }

            return new ProposeResult(
                exitCode,
                BuildProposeReport(result, status, exitCode, null, null, message, repoRoot, Array.Empty<string>()));
        }

        private ProposalCapture CaptureProposals(string adoptionId, int generation, P3EvidenceState evidence)
        {
            var generationToken = generation.ToString("D4");
            var proposalSetPath = $".ai/runtime/adoption/proposals/proposal-set-{generationToken}.json";
            var memoryPath = ".ai/runtime/adoption/proposals/memory-seed.proposal.md";
            var taskGraphPath = ".ai/runtime/adoption/proposals/taskgraph-draft.proposal.json";
            var codeGraphPath = ".ai/runtime/adoption/proposals/codegraph-snapshot.proposal.json";
            var refactorPath = ".ai/runtime/adoption/proposals/refactor-candidates.proposal.json";
            var basis = string.Join(
                '\n',
                adoptionId,
                evidence.IntakeEvidenceHash,
                string.Join('\n', evidence.ArtifactHashes.Select(pair => $"{pair.Key}:{pair.Value}")));
            var proposalHash = "sha256:" + Sha256Hex(basis);
            var evidenceRefs = new JsonArray(evidence.ArtifactPaths.Select(path => JsonValue.Create(path)).ToArray());
            var artifactHashObject = new JsonObject();
            foreach (var pair in evidence.ArtifactHashes)
            {
                artifactHashObject[pair.Key] = pair.Value;
            }

            var taskGraphDraft = new JsonObject
            {
                ["schema_version"] = P4TaskGraphDraftSchemaVersion,
                ["artifact_kind"] = "taskgraph_draft_proposal",
                ["status"] = "DRAFT",
                ["graph_type"] = "ADOPTION_DRAFT",
                ["authoritative"] = false,
                ["planner_authoritative"] = false,
                ["proposal_hash"] = proposalHash,
                ["adoption_id"] = adoptionId,
                ["generation"] = generation,
                ["source_intake_evidence_hash"] = evidence.IntakeEvidenceHash,
                ["source_evidence_artifacts"] = CloneArray(evidenceRefs),
                ["tasks"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["natural_key"] = "legacy-adoption-review-intake-evidence",
                        ["status"] = "SUGGESTED",
                        ["title"] = "Review legacy adoption intake evidence",
                        ["source"] = "P4 proposal only",
                        ["dirty_diff_becomes_completed_work"] = false,
                    },
                },
                ["forbidden_statuses_absent"] = true,
                ["materialization_implemented"] = false,
            };

            var codeGraphSnapshot = new JsonObject
            {
                ["schema_version"] = P4CodeGraphSnapshotSchemaVersion,
                ["artifact_kind"] = "codegraph_snapshot_proposal",
                ["status"] = "SNAPSHOT",
                ["authoritative"] = false,
                ["planner_authoritative"] = false,
                ["canonical"] = false,
                ["write_target"] = ".ai/runtime/adoption/proposals/codegraph-snapshot.proposal.json",
                ["proposal_hash"] = proposalHash,
                ["adoption_id"] = adoptionId,
                ["generation"] = generation,
                ["source_intake_evidence_hash"] = evidence.IntakeEvidenceHash,
                ["source_evidence_artifacts"] = CloneArray(evidenceRefs),
                ["code_understanding_engine_invoked"] = false,
                ["continuous_refactoring_triggered"] = false,
                ["refactor_tasks_generated"] = false,
                ["full_inventory_emitted"] = false,
                ["repo_scan_is_codegraph"] = false,
                ["summary"] = new JsonObject
                {
                    ["regular_file_count"] = evidence.RepoScan["regular_file_count"]?.GetValue<int>() ?? 0,
                    ["directory_count"] = evidence.RepoScan["directory_count"]?.GetValue<int>() ?? 0,
                    ["dirty_file_count"] = evidence.RepoScan["dirty_file_count"]?.GetValue<int>() ?? 0,
                    ["instruction_file_count"] = evidence.RepoScan["instruction_file_count"]?.GetValue<int>() ?? 0,
                },
            };

            var refactorCandidates = new JsonObject
            {
                ["schema_version"] = P4RefactorCandidatesSchemaVersion,
                ["artifact_kind"] = "refactor_candidates_proposal",
                ["status"] = "PROPOSED",
                ["authoritative"] = false,
                ["proposal_hash"] = proposalHash,
                ["adoption_id"] = adoptionId,
                ["generation"] = generation,
                ["source_intake_evidence_hash"] = evidence.IntakeEvidenceHash,
                ["candidates"] = new JsonArray(),
                ["worker_execution_invoked"] = false,
                ["patches_written"] = false,
                ["reviews_written"] = false,
                ["source_modified"] = false,
            };

            var proposalSet = new JsonObject
            {
                ["schema_version"] = P4ProposalSetSchemaVersion,
                ["artifact_kind"] = "proposal_set",
                ["status"] = "PROPOSED",
                ["proposal_only"] = true,
                ["authoritative"] = false,
                ["proposal_hash"] = proposalHash,
                ["adoption_id"] = adoptionId,
                ["generation"] = generation,
                ["source_intake_evidence_hash"] = evidence.IntakeEvidenceHash,
                ["source_evidence_artifact_hashes"] = artifactHashObject,
                ["proposal_artifacts"] = new JsonArray(memoryPath, taskGraphPath, codeGraphPath, refactorPath),
                ["governed_memory_written"] = false,
                ["approved_taskgraph_written"] = false,
                ["canonical_codegraph_written"] = false,
                ["planner_authoritative_codegraph_written"] = false,
                ["patches_written"] = false,
                ["reviews_written"] = false,
                ["worker_execution_invoked"] = false,
                ["p5_status"] = "BLOCKED",
            };

            var memoryProposal = $"""
            # Legacy Adoption Memory Seed Proposal

            Status: PROPOSED
            Authoritative: false
            Proposal hash: {proposalHash}
            Adoption id: {adoptionId}
            Generation: {generation}
            Source intake evidence hash: {evidence.IntakeEvidenceHash}

            This is a non-authoritative P4 proposal stored under `.ai/runtime/adoption/proposals`.
            It is not approved Memory truth and must not be loaded as canonical project memory without a later promotion gate.
            Dirty diff content and instruction bodies are intentionally omitted from this public proposal summary.
            """;

            return new ProposalCapture(
                proposalHash,
                new[]
                {
                    new JsonArtifact(taskGraphPath, taskGraphDraft),
                    new JsonArtifact(codeGraphPath, codeGraphSnapshot),
                    new JsonArtifact(refactorPath, refactorCandidates),
                    new JsonArtifact(proposalSetPath, proposalSet),
                },
                new[]
                {
                    new TextArtifact(memoryPath, memoryProposal),
                });
        }

        private void UpdateManifest(JsonObject manifest, int generation, string aggregateHash, IReadOnlyList<string> artifactPaths, DateTimeOffset updatedAt)
        {
            manifest["generation"] = generation;
            manifest["updated_at"] = updatedAt.ToString("O");
            manifest["last_proposal_hash"] = aggregateHash;
            manifest["last_proposal_artifacts"] = new JsonArray(artifactPaths.Select(path => JsonValue.Create(path)).ToArray());
            manifest["last_manifest_hash"] = "sha256:self";
            manifest["last_manifest_hash"] = "sha256:" + Sha256Hex(SerializeCanonical(manifest));
        }

        private JsonObject BuildEventRecord(string adoptionId, string eventType, string result, int generation, IReadOnlyList<string> artifactPaths, DateTimeOffset createdAt)
            => new()
            {
                ["schema_version"] = P2AttachEventSchemaVersion,
                ["event_id"] = "evt_" + Guid.NewGuid().ToString("N"),
                ["adoption_id"] = adoptionId,
                ["event_type"] = eventType,
                ["created_at"] = createdAt.ToString("O"),
                ["generation"] = generation,
                ["artifact_paths"] = new JsonArray(artifactPaths.Select(item => JsonValue.Create(item)).ToArray()),
                ["result"] = result,
            };

        private JsonObject BuildLedgerRecord(string adoptionId, string artifactPath, string artifactKind, DateTimeOffset createdAt)
        {
            var absolutePath = ToFullPath(artifactPath);
            return new JsonObject
            {
                ["schema_version"] = P2AttachLedgerSchemaVersion,
                ["ledger_record_id"] = "led_" + Guid.NewGuid().ToString("N"),
                ["adoption_id"] = adoptionId,
                ["artifact_path"] = artifactPath,
                ["artifact_kind"] = artifactKind,
                ["owner"] = "CARVES",
                ["hash"] = File.Exists(absolutePath) ? "sha256:" + ComputeFileSha256(absolutePath) : "sha256:" + Sha256Hex(Array.Empty<byte>()),
                ["created_at"] = createdAt.ToString("O"),
                ["delete_policy"] = "retain_until_p5_cleanup_contract",
                ["human_modified"] = false,
            };
        }

        private static string ResolveArtifactKind(string artifactPath)
        {
            if (artifactPath.Contains("proposal-set-", StringComparison.Ordinal))
            {
                return "p4_proposal_set";
            }

            if (artifactPath.EndsWith("memory-seed.proposal.md", StringComparison.Ordinal))
            {
                return "p4_memory_seed_proposal";
            }

            if (artifactPath.EndsWith("taskgraph-draft.proposal.json", StringComparison.Ordinal))
            {
                return "p4_taskgraph_draft_proposal";
            }

            if (artifactPath.EndsWith("codegraph-snapshot.proposal.json", StringComparison.Ordinal))
            {
                return "p4_codegraph_snapshot_proposal";
            }

            return artifactPath.EndsWith("refactor-candidates.proposal.json", StringComparison.Ordinal)
                ? "p4_refactor_candidates_proposal"
                : "p4_proposal_artifact";
        }

        private static IReadOnlyList<string> ReadStringArray(JsonNode? node)
            => node is JsonArray array
                ? array.Select(item => item?.GetValue<string>()).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).ToArray()
                : Array.Empty<string>();

        private void AcquireLock()
        {
            Directory.CreateDirectory(adoptionRoot);
            var lockJson = new JsonObject
            {
                ["schema_version"] = "legacy_adoption.propose_lock.v0.1.0",
                ["created_at"] = DateTimeOffset.UtcNow.ToString("O"),
                ["expires_at"] = DateTimeOffset.UtcNow.AddMinutes(15).ToString("O"),
                ["takeover_allowed"] = false,
            };
            using var stream = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            lockJson.WriteTo(writer);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        private string ToFullPath(string relativePath)
            => Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

        private static JsonArray CloneArray(JsonArray array)
            => new(array.Select(item => JsonValue.Create(item?.GetValue<string>())).ToArray());

        private static byte[] SerializeCanonical(JsonObject payload)
            => Encoding.UTF8.GetBytes(payload.ToJsonString(new JsonSerializerOptions { PropertyNamingPolicy = null, WriteIndented = false }));

        private static void WriteJsonAtomic(string path, JsonObject payload)
            => WriteTextAtomic(path, payload.ToJsonString(AdoptionJsonOptions));

        private static void WriteTextAtomic(string path, string content)
        {
            var tmpPath = path + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(tmpPath, content);
            FlushFile(tmpPath);
            File.Move(tmpPath, path, overwrite: true);
            FlushFile(path);
        }

        private static void AppendLineDurable(string path, JsonObject payload)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, payload.ToJsonString() + Environment.NewLine);
            FlushFile(path);
        }

        private static void FlushFile(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            stream.Flush(flushToDisk: true);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }

    private sealed class LegacyAdoptionIntakeService
    {
        private readonly string repoRoot;
        private readonly string adoptionRoot;
        private readonly string manifestPath;
        private readonly string eventsPath;
        private readonly string ledgerPath;
        private readonly string lockPath;

        public LegacyAdoptionIntakeService(string repoRoot)
        {
            this.repoRoot = repoRoot;
            adoptionRoot = Path.Combine(repoRoot, ".ai", "runtime", "adoption");
            manifestPath = Path.Combine(adoptionRoot, "adoption.json");
            eventsPath = Path.Combine(adoptionRoot, "events.jsonl");
            ledgerPath = Path.Combine(adoptionRoot, "ownership-ledger.jsonl");
            lockPath = Path.Combine(adoptionRoot, "adoption.lock.json");
        }

        public IntakeResult Intake()
        {
            if (File.Exists(lockPath))
            {
                return ExistingLockResult();
            }

            if (!File.Exists(manifestPath))
            {
                var status = Directory.Exists(Path.Combine(repoRoot, ".ai")) ? "ADOPTION_UNKNOWN" : "ADOPTION_MISSING";
                return new IntakeResult(
                    status == "ADOPTION_UNKNOWN" ? 22 : 23,
                    BuildIntakeReport(
                        "blocked",
                        status,
                        status == "ADOPTION_UNKNOWN" ? 22 : 23,
                        null,
                        null,
                        "P3 intake requires a valid P2 adoption manifest. No destructive operation was performed.",
                        repoRoot,
                        Array.Empty<string>(),
                        Array.Empty<string>()));
            }

            if (!TryLoadManifest(out var manifest, out var adoptionId, out var currentGeneration, out var corruptResult))
            {
                return corruptResult!;
            }

            var capture = CaptureEvidence(adoptionId!, currentGeneration + 1);
            var previousEvidenceHash = manifest!["last_intake_evidence_hash"]?.GetValue<string>();
            var previousArtifacts = ReadStringArray(manifest["last_intake_artifacts"]);
            if (string.Equals(previousEvidenceHash, capture.AggregateHash, StringComparison.Ordinal)
                && previousArtifacts.All(path => File.Exists(ToFullPath(path))))
            {
                return new IntakeResult(
                    0,
                    BuildIntakeReport(
                        "noop",
                        "INTAKE_NOOP",
                        0,
                        adoptionId,
                        currentGeneration,
                        "Legacy adoption P3 intake evidence is already current.",
                        repoRoot,
                        previousArtifacts.Where(path => path.Contains("/snapshots/", StringComparison.Ordinal)).ToArray(),
                        previousArtifacts.Where(path => path.Contains("/proposals/", StringComparison.Ordinal)).ToArray()));
            }

            var lockAcquired = false;
            try
            {
                AcquireLock();
                lockAcquired = true;

                foreach (var artifact in capture.JsonArtifacts)
                {
                    WriteJsonAtomic(ToFullPath(artifact.Path), artifact.Payload);
                }

                WriteTextAtomic(ToFullPath(capture.DirtyDiffArtifact.Path), capture.DirtyDiffArtifact.Content);
                if (capture.AgentsMergeProposal is not null)
                {
                    WriteTextAtomic(ToFullPath(capture.AgentsMergeProposal.Path), capture.AgentsMergeProposal.Content);
                }

                var now = DateTimeOffset.UtcNow;
                var artifactPaths = capture.AllArtifactPaths;
                AppendLineDurable(eventsPath, BuildEventRecord(adoptionId!, "INTAKE_CAPTURED", "success", currentGeneration + 1, artifactPaths, now));
                foreach (var artifactPath in artifactPaths)
                {
                    AppendLineDurable(ledgerPath, BuildLedgerRecord(adoptionId!, artifactPath, ResolveArtifactKind(artifactPath), now));
                }

                UpdateManifest(manifest!, currentGeneration + 1, capture.AggregateHash, artifactPaths, now);
                WriteJsonAtomic(manifestPath, manifest!);

                return new IntakeResult(
                    0,
                    BuildIntakeReport(
                        "success",
                        "INTAKE_CAPTURED",
                        0,
                        adoptionId,
                        currentGeneration + 1,
                        "Legacy adoption P3 intake evidence captured.",
                        repoRoot,
                        capture.AllArtifactPaths.Where(path => path.Contains("/snapshots/", StringComparison.Ordinal)).ToArray(),
                        capture.AllArtifactPaths.Where(path => path.Contains("/proposals/", StringComparison.Ordinal)).ToArray()));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                return new IntakeResult(
                    25,
                    BuildIntakeReport(
                        "recovery_required",
                        "RECOVERY_REQUIRED",
                        25,
                        adoptionId,
                        currentGeneration,
                        "P3 intake write failed and requires operator recovery.",
                        repoRoot,
                        Array.Empty<string>(),
                        Array.Empty<string>()));
            }
            finally
            {
                if (lockAcquired)
                {
                    TryDelete(lockPath);
                }
            }
        }

        private bool TryLoadManifest(out JsonObject? manifest, out string? adoptionId, out int generation, out IntakeResult? result)
        {
            manifest = null;
            adoptionId = null;
            generation = 0;
            result = null;

            try
            {
                manifest = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject()
                    ?? throw new JsonException("Manifest root is not an object.");
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                result = CorruptManifestResult("Existing adoption manifest is corrupt. No writes were performed.");
                return false;
            }

            adoptionId = manifest["adoption_id"]?.GetValue<string>();
            var status = manifest["status"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(adoptionId) || !string.Equals(status, "BOUND", StringComparison.Ordinal))
            {
                result = CorruptManifestResult("Existing adoption manifest is missing required P2 BOUND state. No writes were performed.");
                return false;
            }

            generation = manifest["generation"]?.GetValue<int>() ?? 1;
            return true;
        }

        private IntakeResult CorruptManifestResult(string message)
            => new(
                21,
                BuildIntakeReport(
                    "blocked",
                    "MANIFEST_CORRUPT",
                    21,
                    null,
                    null,
                    message,
                    repoRoot,
                    Array.Empty<string>(),
                    Array.Empty<string>()));

        private IntakeResult ExistingLockResult()
        {
            var status = "LOCK_CONFLICT";
            var result = "blocked";
            var exitCode = 20;
            var message = "Fresh adoption lock exists. No writes were performed.";
            try
            {
                var lockJson = JsonNode.Parse(File.ReadAllText(lockPath))?.AsObject();
                var expiresAtText = lockJson?["expires_at"]?.GetValue<string>();
                if (DateTimeOffset.TryParse(expiresAtText, out var expiresAt) && expiresAt <= DateTimeOffset.UtcNow)
                {
                    status = "RECOVERY_REQUIRED";
                    result = "recovery_required";
                    exitCode = 25;
                    message = "Expired adoption lock exists. Lock takeover is forbidden in v0 and no writes were performed.";
                }
            }
            catch (JsonException)
            {
                status = "RECOVERY_REQUIRED";
                result = "recovery_required";
                exitCode = 25;
                message = "Unreadable adoption lock exists. Lock takeover is forbidden in v0 and no writes were performed.";
            }

            return new IntakeResult(
                exitCode,
                BuildIntakeReport(result, status, exitCode, null, null, message, repoRoot, Array.Empty<string>(), Array.Empty<string>()));
        }

        private IntakeCapture CaptureEvidence(string adoptionId, int generation)
        {
            var generationToken = generation.ToString("D4");
            var repoScanPath = $".ai/runtime/adoption/snapshots/repo-scan-{generationToken}.json";
            var instructionScanPath = $".ai/runtime/adoption/snapshots/instruction-scan-{generationToken}.json";
            var dirtyDiffPath = $".ai/runtime/adoption/snapshots/dirty-diff-{generationToken}.patch";
            var status = RunGitRead(repoRoot, "status", "--porcelain=v2", "--branch", "--untracked-files=all");
            var statusLines = status.ExitCode == 0
                ? status.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                : Array.Empty<string>();
            var dirtyEntries = statusLines
                .Where(line => !line.StartsWith("#", StringComparison.Ordinal))
                .Where(line => !line.Contains(".ai/runtime/adoption/", StringComparison.Ordinal))
                .ToArray();
            var diff = RunGitRead(repoRoot, "diff", "--binary", "--no-ext-diff").StandardOutput;
            var instructionFiles = ProbeInstructionFiles(repoRoot);
            var repoScan = BuildRepoScanEvidence(adoptionId, generation, dirtyEntries, instructionFiles);
            var instructionScan = BuildInstructionScanEvidence(adoptionId, generation, instructionFiles);
            var dirtyDiff = string.IsNullOrEmpty(diff)
                ? "# clean working tree; no tracked dirty diff captured\n"
                : diff;
            var dirtyDiffHash = "sha256:" + Sha256Hex(dirtyDiff);
            repoScan["dirty_diff_evidence_hash"] = dirtyDiffHash;
            instructionScan["dirty_diff_evidence_hash"] = dirtyDiffHash;

            var jsonArtifacts = new[]
            {
                new JsonArtifact(repoScanPath, repoScan),
                new JsonArtifact(instructionScanPath, instructionScan),
            };
            var dirtyDiffArtifact = new TextArtifact(dirtyDiffPath, dirtyDiff);
            TextArtifact? agentsMergeProposal = null;
            if (instructionFiles.Any(item => item.Exists && string.Equals(item.Kind, "AGENTS", StringComparison.Ordinal)))
            {
                agentsMergeProposal = new TextArtifact(
                    ".ai/runtime/adoption/proposals/AGENTS.merge.md",
                    "# AGENTS merge placeholder\n\nP3 captured existing instruction evidence. No root AGENTS.md modification is approved by this artifact.\n");
            }

            var instructionHashBasis = string.Join(
                ';',
                instructionFiles.Select(item =>
                {
                    var fullPath = ToFullPath(item.Path);
                    var hash = item.Exists && File.Exists(fullPath) ? ComputeFileSha256(fullPath) : "missing";
                    return $"{item.Path}:{item.Exists}:{hash}";
                }));
            var aggregateBasis = string.Join(
                '\n',
                RunGitRead(repoRoot, "rev-parse", "HEAD").StandardOutput.Trim(),
                string.Join(';', dirtyEntries.Order(StringComparer.Ordinal)),
                dirtyDiffHash,
                instructionHashBasis,
                agentsMergeProposal is null ? "no-agents-merge-placeholder" : "agents-merge-placeholder");
            return new IntakeCapture("sha256:" + Sha256Hex(aggregateBasis), jsonArtifacts, dirtyDiffArtifact, agentsMergeProposal);
        }

        private JsonObject BuildRepoScanEvidence(
            string adoptionId,
            int generation,
            IReadOnlyList<string> dirtyEntries,
            IReadOnlyList<InstructionFileObservation> instructionFiles)
        {
            var regularFileCount = 0;
            var directoryCount = 0;
            var extensionCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in EnumerateProbeFileSystemEntries(repoRoot))
            {
                if (Directory.Exists(entry))
                {
                    directoryCount++;
                    continue;
                }

                regularFileCount++;
                var extension = Path.GetExtension(entry);
                extension = string.IsNullOrWhiteSpace(extension) ? "(none)" : extension.ToLowerInvariant();
                extensionCounts[extension] = extensionCounts.TryGetValue(extension, out var count) ? count + 1 : 1;
            }

            return new JsonObject
            {
                ["schema_version"] = P3IntakeRepoScanSchemaVersion,
                ["adoption_id"] = adoptionId,
                ["generation"] = generation,
                ["captured_at"] = DateTimeOffset.UtcNow.ToString("O"),
                ["evidence_class"] = "repo_scan",
                ["repo_scan_is_codegraph"] = false,
                ["planner_authoritative"] = false,
                ["code_understanding_engine_invoked"] = false,
                ["full_inventory_emitted"] = false,
                ["git_object_database_read"] = false,
                ["ai_governance_contents_read"] = false,
                ["regular_file_count"] = regularFileCount,
                ["directory_count"] = directoryCount,
                ["extension_counts"] = ToJsonObject(extensionCounts),
                ["dirty_file_count"] = dirtyEntries.Count,
                ["instruction_file_count"] = instructionFiles.Count(item => item.Exists),
            };
        }

        private JsonObject BuildInstructionScanEvidence(
            string adoptionId,
            int generation,
            IReadOnlyList<InstructionFileObservation> instructionFiles)
        {
            var evidence = new JsonObject
            {
                ["schema_version"] = P3IntakeInstructionScanSchemaVersion,
                ["adoption_id"] = adoptionId,
                ["generation"] = generation,
                ["captured_at"] = DateTimeOffset.UtcNow.ToString("O"),
                ["evidence_class"] = "instruction_scan",
                ["instruction_bodies_emitted"] = false,
                ["instruction_files_modified"] = false,
                ["files"] = new JsonArray(),
            };

            var files = evidence["files"]!.AsArray();
            foreach (var instruction in instructionFiles)
            {
                var item = ToJsonNode(instruction).AsObject();
                var fullPath = ToFullPath(instruction.Path);
                item["content_hash"] = instruction.Exists && File.Exists(fullPath) ? "sha256:" + ComputeFileSha256(fullPath) : null;
                files.Add(item);
            }

            return evidence;
        }

        private void UpdateManifest(JsonObject manifest, int generation, string aggregateHash, IReadOnlyList<string> artifactPaths, DateTimeOffset updatedAt)
        {
            manifest["generation"] = generation;
            manifest["updated_at"] = updatedAt.ToString("O");
            manifest["last_intake_evidence_hash"] = aggregateHash;
            manifest["last_intake_artifacts"] = new JsonArray(artifactPaths.Select(path => JsonValue.Create(path)).ToArray());
            manifest["last_manifest_hash"] = "sha256:self";
            manifest["last_manifest_hash"] = "sha256:" + Sha256Hex(SerializeCanonical(manifest));
        }

        private JsonObject BuildEventRecord(string adoptionId, string eventType, string result, int generation, IReadOnlyList<string> artifactPaths, DateTimeOffset createdAt)
            => new()
            {
                ["schema_version"] = P2AttachEventSchemaVersion,
                ["event_id"] = "evt_" + Guid.NewGuid().ToString("N"),
                ["adoption_id"] = adoptionId,
                ["event_type"] = eventType,
                ["created_at"] = createdAt.ToString("O"),
                ["generation"] = generation,
                ["artifact_paths"] = new JsonArray(artifactPaths.Select(item => JsonValue.Create(item)).ToArray()),
                ["result"] = result,
            };

        private JsonObject BuildLedgerRecord(string adoptionId, string artifactPath, string artifactKind, DateTimeOffset createdAt)
        {
            var absolutePath = ToFullPath(artifactPath);
            return new JsonObject
            {
                ["schema_version"] = P2AttachLedgerSchemaVersion,
                ["ledger_record_id"] = "led_" + Guid.NewGuid().ToString("N"),
                ["adoption_id"] = adoptionId,
                ["artifact_path"] = artifactPath,
                ["artifact_kind"] = artifactKind,
                ["owner"] = "CARVES",
                ["hash"] = File.Exists(absolutePath) ? "sha256:" + ComputeFileSha256(absolutePath) : "sha256:" + Sha256Hex(Array.Empty<byte>()),
                ["created_at"] = createdAt.ToString("O"),
                ["delete_policy"] = "retain_until_p5_cleanup_contract",
                ["human_modified"] = false,
            };
        }

        private static string ResolveArtifactKind(string artifactPath)
        {
            if (artifactPath.Contains("repo-scan-", StringComparison.Ordinal))
            {
                return "p3_repo_scan_evidence";
            }

            if (artifactPath.Contains("instruction-scan-", StringComparison.Ordinal))
            {
                return "p3_instruction_scan_evidence";
            }

            if (artifactPath.Contains("dirty-diff-", StringComparison.Ordinal))
            {
                return "p3_dirty_diff_evidence";
            }

            return artifactPath.EndsWith("AGENTS.merge.md", StringComparison.Ordinal)
                ? "p3_agents_merge_placeholder"
                : "p3_intake_artifact";
        }

        private static IReadOnlyList<string> ReadStringArray(JsonNode? node)
            => node is JsonArray array
                ? array.Select(item => item?.GetValue<string>()).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).ToArray()
                : Array.Empty<string>();

        private void AcquireLock()
        {
            Directory.CreateDirectory(adoptionRoot);
            var lockJson = new JsonObject
            {
                ["schema_version"] = "legacy_adoption.intake_lock.v0.1.0",
                ["created_at"] = DateTimeOffset.UtcNow.ToString("O"),
                ["expires_at"] = DateTimeOffset.UtcNow.AddMinutes(15).ToString("O"),
                ["takeover_allowed"] = false,
            };
            using var stream = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            lockJson.WriteTo(writer);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        private string ToFullPath(string relativePath)
            => Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

        private static byte[] SerializeCanonical(JsonObject payload)
            => Encoding.UTF8.GetBytes(payload.ToJsonString(new JsonSerializerOptions { PropertyNamingPolicy = null, WriteIndented = false }));

        private static void WriteJsonAtomic(string path, JsonObject payload)
            => WriteTextAtomic(path, payload.ToJsonString(AdoptionJsonOptions));

        private static void WriteTextAtomic(string path, string content)
        {
            var tmpPath = path + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(tmpPath, content);
            FlushFile(tmpPath);
            File.Move(tmpPath, path, overwrite: true);
            FlushFile(path);
        }

        private static void AppendLineDurable(string path, JsonObject payload)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, payload.ToJsonString() + Environment.NewLine);
            FlushFile(path);
        }

        private static void FlushFile(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            stream.Flush(flushToDisk: true);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }

    private sealed class LegacyAdoptionAttachService
    {
        private static readonly IReadOnlyList<string> PersistentArtifacts =
        [
            ".ai/runtime/adoption/adoption.json",
            ".ai/runtime/adoption/events.jsonl",
            ".ai/runtime/adoption/ownership-ledger.jsonl",
        ];

        private readonly string repoRoot;
        private readonly string adoptionRoot;
        private readonly string manifestPath;
        private readonly string eventsPath;
        private readonly string ledgerPath;
        private readonly string lockPath;

        public LegacyAdoptionAttachService(string repoRoot)
        {
            this.repoRoot = repoRoot;
            adoptionRoot = Path.Combine(repoRoot, ".ai", "runtime", "adoption");
            manifestPath = Path.Combine(adoptionRoot, "adoption.json");
            eventsPath = Path.Combine(adoptionRoot, "events.jsonl");
            ledgerPath = Path.Combine(adoptionRoot, "ownership-ledger.jsonl");
            lockPath = Path.Combine(adoptionRoot, "adoption.lock.json");
        }

        public AttachResult Attach()
        {
            if (File.Exists(lockPath))
            {
                return ExistingLockResult();
            }

            if (File.Exists(manifestPath))
            {
                return ExistingManifestResult();
            }

            var aiRoot = Path.Combine(repoRoot, ".ai");
            if (Directory.Exists(aiRoot))
            {
                return new AttachResult(
                    22,
                    BuildAttachReport(
                        "blocked",
                        "ADOPTION_UNKNOWN",
                        22,
                        null,
                        "Existing .ai is present without an adoption manifest. No destructive operation was performed.",
                        repoRoot,
                        Array.Empty<string>()));
            }

            Directory.CreateDirectory(adoptionRoot);
            var lockAcquired = false;
            try
            {
                AcquireLock();
                lockAcquired = true;
                var now = DateTimeOffset.UtcNow;
                var adoptionId = "adopt_" + Guid.NewGuid().ToString("N");
                var manifest = BuildManifest(adoptionId, status: "BOUND", createdAt: now, updatedAt: now);
                WriteJsonAtomic(manifestPath, manifest);
                AppendLineDurable(eventsPath, BuildEventRecord(adoptionId, "ATTACH_BOUND", "success", now));
                AppendLineDurable(ledgerPath, BuildLedgerRecords(adoptionId, now));
                return new AttachResult(
                    0,
                    BuildAttachReport(
                        "success",
                        "BOUND",
                        0,
                        adoptionId,
                        "Legacy adoption P2 attach binding created.",
                        repoRoot,
                        PersistentArtifacts));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                return new AttachResult(
                    25,
                    BuildAttachReport(
                        "recovery_required",
                        "RECOVERY_REQUIRED",
                        25,
                        null,
                        "P2 attach write failed and requires operator recovery.",
                        repoRoot,
                        Array.Empty<string>()));
            }
            finally
            {
                if (lockAcquired)
                {
                    TryDelete(lockPath);
                }
            }
        }

        private AttachResult ExistingManifestResult()
        {
            JsonObject manifest;
            try
            {
                manifest = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject()
                    ?? throw new JsonException("Manifest root is not an object.");
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                return new AttachResult(
                    21,
                    BuildAttachReport(
                        "blocked",
                        "MANIFEST_CORRUPT",
                        21,
                        null,
                        "Existing adoption manifest is corrupt. No writes were performed.",
                        repoRoot,
                        Array.Empty<string>()));
            }

            var adoptionId = manifest["adoption_id"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(adoptionId))
            {
                return new AttachResult(
                    21,
                    BuildAttachReport(
                        "blocked",
                        "MANIFEST_CORRUPT",
                        21,
                        null,
                        "Existing adoption manifest is missing adoption_id. No writes were performed.",
                        repoRoot,
                        Array.Empty<string>()));
            }

            return new AttachResult(
                0,
                BuildAttachReport(
                    "noop",
                    "BOUND_NOOP",
                    0,
                    adoptionId,
                    "Legacy adoption P2 attach binding already exists.",
                    repoRoot,
                    PersistentArtifacts.Where(path => File.Exists(Path.Combine(repoRoot, path.Replace('/', Path.DirectorySeparatorChar)))).ToArray()));
        }

        private AttachResult ExistingLockResult()
        {
            var status = "LOCK_CONFLICT";
            var result = "blocked";
            var exitCode = 20;
            var message = "Fresh adoption lock exists. No writes were performed.";
            try
            {
                var lockJson = JsonNode.Parse(File.ReadAllText(lockPath))?.AsObject();
                var expiresAtText = lockJson?["expires_at"]?.GetValue<string>();
                if (DateTimeOffset.TryParse(expiresAtText, out var expiresAt) && expiresAt <= DateTimeOffset.UtcNow)
                {
                    status = "RECOVERY_REQUIRED";
                    result = "recovery_required";
                    exitCode = 25;
                    message = "Expired adoption lock exists. Lock takeover is forbidden in v0 and no writes were performed.";
                }
            }
            catch (JsonException)
            {
                status = "RECOVERY_REQUIRED";
                result = "recovery_required";
                exitCode = 25;
                message = "Unreadable adoption lock exists. Lock takeover is forbidden in v0 and no writes were performed.";
            }

            return new AttachResult(
                exitCode,
                BuildAttachReport(result, status, exitCode, null, message, repoRoot, Array.Empty<string>()));
        }

        private void AcquireLock()
        {
            Directory.CreateDirectory(adoptionRoot);
            var lockJson = new JsonObject
            {
                ["schema_version"] = "legacy_adoption.attach_lock.v0.1.0",
                ["created_at"] = DateTimeOffset.UtcNow.ToString("O"),
                ["expires_at"] = DateTimeOffset.UtcNow.AddMinutes(15).ToString("O"),
                ["takeover_allowed"] = false,
            };
            using var stream = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            lockJson.WriteTo(writer);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        private JsonObject BuildManifest(string adoptionId, string status, DateTimeOffset createdAt, DateTimeOffset updatedAt)
        {
            var manifest = new JsonObject
            {
                ["schema_version"] = P2AttachManifestSchemaVersion,
                ["adoption_id"] = adoptionId,
                ["status"] = status,
                ["generation"] = 1,
                ["repo_identity"] = new JsonObject
                {
                    ["root_kind"] = "git_worktree",
                    ["head_present"] = RunGitRead(repoRoot, "rev-parse", "--verify", "HEAD").ExitCode == 0,
                    ["head_hash_present"] = RunGitRead(repoRoot, "rev-parse", "HEAD").ExitCode == 0,
                    ["remote_count"] = CountRemotes(repoRoot),
                    ["repo_fingerprint_role"] = "diagnostics_and_conflict_detection_only",
                },
                ["observed_context"] = new JsonObject
                {
                    ["dirty_state"] = BuildAttachObservedContext(repoRoot)["dirty_state"]!.GetValue<string>(),
                    ["instruction_files_present"] = new JsonArray(DetectInstructionFilesPresent(repoRoot).Select(item => JsonValue.Create(item)).ToArray()),
                    ["existing_ai_state"] = "absent",
                    ["current_attach_assessment"] = "unsafe_for_legacy_adoption",
                },
                ["created_at"] = createdAt.ToString("O"),
                ["updated_at"] = updatedAt.ToString("O"),
                ["created_by"] = "CARVES Runtime P2 attach",
                ["canonical_owner"] = "CARVES Host",
                ["last_manifest_hash"] = "sha256:self",
            };
            manifest["last_manifest_hash"] = "sha256:" + Sha256Hex(SerializeCanonical(manifest));
            return manifest;
        }

        private JsonObject BuildEventRecord(string adoptionId, string eventType, string result, DateTimeOffset createdAt)
            => new()
            {
                ["schema_version"] = P2AttachEventSchemaVersion,
                ["event_id"] = "evt_" + Guid.NewGuid().ToString("N"),
                ["adoption_id"] = adoptionId,
                ["event_type"] = eventType,
                ["created_at"] = createdAt.ToString("O"),
                ["generation"] = 1,
                ["artifact_paths"] = new JsonArray(PersistentArtifacts.Select(item => JsonValue.Create(item)).ToArray()),
                ["result"] = result,
            };

        private string BuildLedgerRecords(string adoptionId, DateTimeOffset createdAt)
        {
            var rows = new[]
            {
                BuildLedgerRecord(adoptionId, ".ai/runtime/adoption/adoption.json", "adoption_manifest", createdAt),
                BuildLedgerRecord(adoptionId, ".ai/runtime/adoption/events.jsonl", "adoption_event_log", createdAt),
                BuildLedgerRecord(adoptionId, ".ai/runtime/adoption/ownership-ledger.jsonl", "ownership_ledger", createdAt),
            };
            return string.Join(Environment.NewLine, rows.Select(row => row.ToJsonString())) + Environment.NewLine;
        }

        private JsonObject BuildLedgerRecord(string adoptionId, string artifactPath, string artifactKind, DateTimeOffset createdAt)
        {
            var absolutePath = Path.Combine(repoRoot, artifactPath.Replace('/', Path.DirectorySeparatorChar));
            return new JsonObject
            {
                ["schema_version"] = P2AttachLedgerSchemaVersion,
                ["ledger_record_id"] = "led_" + Guid.NewGuid().ToString("N"),
                ["adoption_id"] = adoptionId,
                ["artifact_path"] = artifactPath,
                ["artifact_kind"] = artifactKind,
                ["owner"] = "CARVES",
                ["hash"] = File.Exists(absolutePath) ? "sha256:" + ComputeFileSha256(absolutePath) : "sha256:" + Sha256Hex(Array.Empty<byte>()),
                ["created_at"] = createdAt.ToString("O"),
                ["delete_policy"] = "retain_until_p5_cleanup_contract",
                ["human_modified"] = false,
            };
        }

        private static byte[] SerializeCanonical(JsonObject payload)
            => Encoding.UTF8.GetBytes(payload.ToJsonString(new JsonSerializerOptions { PropertyNamingPolicy = null, WriteIndented = false }));

        private static void WriteJsonAtomic(string path, JsonObject payload)
        {
            var tmpPath = path + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(tmpPath, payload.ToJsonString(AdoptionJsonOptions));
            FlushFile(tmpPath);
            File.Move(tmpPath, path, overwrite: true);
            FlushFile(path);
        }

        private static void AppendLineDurable(string path, JsonObject payload)
            => AppendLineDurable(path, payload.ToJsonString() + Environment.NewLine);

        private static void AppendLineDurable(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, content);
            FlushFile(path);
        }

        private static void FlushFile(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            stream.Flush(flushToDisk: true);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }

    private sealed record AttachResult(int ExitCode, JsonObject Payload);
    private sealed record IntakeResult(int ExitCode, JsonObject Payload);
    private sealed record ProposeResult(int ExitCode, JsonObject Payload);
    private sealed record P5Result(int ExitCode, JsonObject Payload);
    private sealed record JsonArtifact(string Path, JsonObject Payload);
    private sealed record TextArtifact(string Path, string Content);
    private sealed record LedgerArtifact(string Path, string Kind, string Owner, string Hash, bool HumanModified, string DeletePolicy);
    private sealed record PlanCandidate(
        string Path,
        string Decision,
        string Reason,
        bool LedgerOwned,
        bool HashMatches,
        bool HumanModified,
        bool Referenced,
        bool SourceProtected,
        bool TruthProtected,
        bool SafePath,
        string CurrentHash,
        string LedgerHash,
        string ArtifactKind,
        string CanonicalPath,
        string PathGuardDecision,
        string PathGuardReason,
        bool PathResolvesInsideRepoRoot,
        bool Symlink,
        bool CaseCollisionAmbiguous);
    private sealed record PathGuard(
        string Decision,
        string Reason,
        string CanonicalPath,
        bool ResolvesInsideRepoRoot,
        bool Symlink,
        bool CaseCollisionAmbiguous);
    private sealed record CleanupSafetyFailure(
        string Path,
        string Reason,
        PathGuard PathGuard,
        string CurrentHash,
        string ExpectedHash);
    private sealed record PlanCapture(
        string PlanId,
        string PlanPath,
        string PlanHash,
        JsonObject ReferenceIndex,
        JsonObject Plan,
        IReadOnlyList<PlanCandidate> Candidates)
    {
        public IReadOnlyList<string> RuntimeArtifacts
            => new[] { ".ai/runtime/adoption/references.json", PlanPath };
    }

    private sealed record CleanupApplyCapture(
        string CleanupId,
        string ProofPath,
        JsonObject Proof,
        IReadOnlyList<string> DeletedPaths,
        IReadOnlyList<string> ArchivedPaths,
        IReadOnlyList<string> RefusedPaths,
        IReadOnlyList<string> ArchiveArtifacts);
    private sealed record P3EvidenceState(
        string IntakeEvidenceHash,
        IReadOnlyList<string> ArtifactPaths,
        IReadOnlyDictionary<string, string> ArtifactHashes,
        JsonObject RepoScan,
        JsonObject InstructionScan,
        string DirtyDiffPath);
    private sealed record IntakeCapture(
        string AggregateHash,
        IReadOnlyList<JsonArtifact> JsonArtifacts,
        TextArtifact DirtyDiffArtifact,
        TextArtifact? AgentsMergeProposal)
    {
        public IReadOnlyList<string> AllArtifactPaths
            => JsonArtifacts
                .Select(artifact => artifact.Path)
                .Append(DirtyDiffArtifact.Path)
                .Concat(AgentsMergeProposal is null ? Array.Empty<string>() : new[] { AgentsMergeProposal.Path })
                .ToArray();
    }

    private sealed record ProposalCapture(
        string AggregateHash,
        IReadOnlyList<JsonArtifact> JsonArtifacts,
        IReadOnlyList<TextArtifact> TextArtifacts)
    {
        public IReadOnlyList<string> AllArtifactPaths
            => TextArtifacts
                .Select(artifact => artifact.Path)
                .Concat(JsonArtifacts.Select(artifact => artifact.Path))
                .ToArray();
    }
}

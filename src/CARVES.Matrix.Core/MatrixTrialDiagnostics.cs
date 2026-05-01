using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static IReadOnlyList<TrialDiagnosticReadback> BuildTrialDiagnostics(
        TrialCollectionReadback? collection,
        TrialVerificationReadback? verification)
    {
        var diagnostics = new List<TrialDiagnosticReadback>();
        if (collection is not null)
        {
            AddCollectionDiagnostics(diagnostics, collection);
        }

        if (verification is not null && (!string.Equals(verification.Status, "verified", StringComparison.Ordinal) || !verification.TrialArtifactsVerified))
        {
            diagnostics.Add(new TrialDiagnosticReadback(
                "trial_verify_failed",
                "evidence_integrity",
                "error",
                "Matrix trial verification failed for the local bundle; the result must not be treated as verified.",
                "matrix-artifact-manifest.json",
                "carves-matrix trial verify --bundle-root <bundle-root> --json",
                "Inspect the verification reason codes and repair the covered artifact or manifest problem; do not edit the result summary to hide it.",
                verification.ReasonCodes));
            foreach (var reasonCode in verification.ReasonCodes)
            {
                AddVerificationReasonDiagnostic(diagnostics, reasonCode);
            }
        }

        return diagnostics
            .GroupBy(diagnostic => diagnostic.Code + "\n" + diagnostic.EvidenceRef, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(diagnostic => diagnostic.Category, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<TrialDiagnosticReadback> BuildTrialExceptionDiagnostics(string command, Exception exception)
    {
        if (IsTrialSetupCommand(command) && exception is DirectoryNotFoundException)
        {
            return
            [
                new TrialDiagnosticReadback(
                    "trial_setup_pack_missing",
                    "user_setup",
                    "error",
                    "The starter pack directory was not found.",
                    "--pack-root",
                    "carves-matrix trial prepare --workspace <workspace> --pack-root <pack-root>",
                    "Pass the official starter pack directory, or run prepare from a source checkout where the default starter pack can be discovered.",
                    ["starter_pack_missing"])
            ];
        }

        if (IsTrialSetupCommand(command) && exception is InvalidOperationException && exception.Message.Contains("Workspace already exists and is not empty", StringComparison.Ordinal))
        {
            return
            [
                new TrialDiagnosticReadback(
                    "trial_setup_workspace_not_empty",
                    "user_setup",
                    "error",
                    "The requested workspace already exists and is not empty.",
                    "--workspace",
                    "carves-matrix trial prepare --workspace <empty-workspace>",
                    "Choose an empty workspace directory. Do not merge a new trial pack into an existing edited workspace.",
                    ["workspace_not_empty"])
            ];
        }

        if (command == "package" && exception is InvalidOperationException && exception.Message.Contains("Package output", StringComparison.Ordinal))
        {
            return
            [
                new TrialDiagnosticReadback(
                    "trial_package_output_not_empty",
                    "user_setup",
                    "error",
                    "The requested portable package output already exists or is not a safe package directory.",
                    "--output",
                    "carves-matrix trial package --output <empty-package-root>",
                    "Choose an empty output directory, or use --force only when replacing an existing CARVES portable package.",
                    ["package_output_not_empty"])
            ];
        }

        if (command == "package" && exception is InvalidOperationException && exception.Message.Contains("Refusing to overwrite non-package", StringComparison.Ordinal))
        {
            return
            [
                new TrialDiagnosticReadback(
                    "trial_package_output_not_safe",
                    "user_setup",
                    "error",
                    "The requested --force target does not look like an existing CARVES portable package.",
                    "--output",
                    "carves-matrix trial package --output <package-root> --force",
                    "Choose a dedicated package directory. The package writer will not delete unrelated non-package content.",
                    ["package_output_not_safe"])
            ];
        }

        if (command == "package" && exception is InvalidOperationException && exception.Message.Contains("Playable scorer", StringComparison.Ordinal))
        {
            return
            [
                new TrialDiagnosticReadback(
                    "trial_package_scorer_missing",
                    "user_setup",
                    "error",
                    "The Windows playable package assembler could not find a runnable scorer entrypoint.",
                    "--scorer-root",
                    "carves-matrix trial package --windows-playable --scorer-root <win-publish-root> --output <package-root>",
                    "Pass a Windows self-contained publish directory that contains carves.exe. Do not market a scorerless developer directory as the Windows playable zip.",
                    ["playable_scorer_missing"])
            ];
        }

        if (command == "package" && exception is InvalidOperationException && exception.Message.Contains("Playable zip output", StringComparison.Ordinal))
        {
            return
            [
                new TrialDiagnosticReadback(
                    "trial_package_zip_output_invalid",
                    "user_setup",
                    "error",
                    "The Windows playable zip output path is invalid or unsafe.",
                    "--zip-output",
                    "carves-matrix trial package --windows-playable --scorer-root <win-publish-root> --zip-output <zip>",
                    "Write the playable zip outside the package root, or use --force to replace an existing zip.",
                    ["playable_zip_output_invalid"])
            ];
        }

        if (command == "collect" && exception is InvalidOperationException && exception.Message.Contains("Portable package root not found", StringComparison.Ordinal))
        {
            return
            [
                new TrialDiagnosticReadback(
                    "trial_portable_package_root_missing",
                    "user_setup",
                    "error",
                    "No portable Agent Trial package was found in the current directory.",
                    "agent-workspace/",
                    "carves test collect",
                    "Run this command from the extracted package root, or use the advanced Matrix command with --workspace.",
                    ["portable_package_root_missing"])
            ];
        }

        if (command == "collect" && exception is InvalidDataException && exception.Message.StartsWith("Agent report schema invalid:", StringComparison.Ordinal))
        {
            return
            [
                new TrialDiagnosticReadback(
                    "trial_agent_report_schema_invalid",
                    "agent_behavior",
                    "error",
                    "The agent report JSON does not match agent-report.v0.",
                    "agent-workspace/artifacts/agent-report.json",
                    "artifacts/agent-report.template.json",
                    "Ask the agent to copy artifacts/agent-report.template.json to artifacts/agent-report.json, keep schema_version exactly agent-report.v0, fill the existing fields, and remove extra top-level fields.",
                    BuildAgentReportSchemaReasonCodes(exception.Message))
            ];
        }

        if (command == "reset" && exception is InvalidOperationException && exception.Message.Contains("does not accept path options", StringComparison.Ordinal))
        {
            return
            [
                new TrialDiagnosticReadback(
                    "trial_reset_path_options_rejected",
                    "user_setup",
                    "error",
                    "Reset only works on this extracted package folder and will not use an external bundle path.",
                    "--bundle-root",
                    "carves-matrix trial reset",
                    "Run reset from the portable package root without --bundle-root, --workspace, or other path options.",
                    ["portable_reset_path_options_rejected"])
            ];
        }

        if (command == "reset" && exception is InvalidOperationException && exception.Message.Contains("Portable package root not found for reset", StringComparison.Ordinal))
        {
            return
            [
                new TrialDiagnosticReadback(
                    "trial_reset_package_root_missing",
                    "user_setup",
                    "error",
                    "No portable Agent Trial package was found in the current directory.",
                    "agent-workspace/",
                    "carves-matrix trial reset",
                    "Run reset from the extracted package root that contains agent-workspace/ and .carves-pack/.",
                    ["portable_reset_package_root_missing"])
            ];
        }

        if (command == "reset" && exception is InvalidOperationException && exception.Message.Contains("Portable reset workspace git baseline missing", StringComparison.Ordinal))
        {
            return
            [
                new TrialDiagnosticReadback(
                    "trial_reset_workspace_git_missing",
                    "user_setup",
                    "error",
                    "Reset could not prove the workspace can be restored, so the old result files were left in place.",
                    "agent-workspace/.git",
                    "carves-matrix trial reset",
                    "Recreate the portable package or restore agent-workspace/.git, then run reset again.",
                    ["portable_reset_workspace_git_missing"])
            ];
        }

        if (command == "reset" && exception is InvalidOperationException && exception.Message.Contains("git", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new TrialDiagnosticReadback(
                    "trial_reset_workspace_reset_failed",
                    "user_setup",
                    "error",
                    "Reset could not restore the workspace, so the old result files were left in place.",
                    "agent-workspace/",
                    "carves-matrix trial reset",
                    "Fix or recreate the workspace git baseline, then run reset again.",
                    ["portable_reset_workspace_reset_failed"])
            ];
        }

        var portablePackageStateDiagnostics = TryBuildPortablePackageStateExceptionDiagnostics(command, exception);
        if (portablePackageStateDiagnostics is not null)
        {
            return portablePackageStateDiagnostics;
        }

        if (IsTrialSetupCommand(command) && exception is InvalidOperationException && exception.Message.Contains("Unable to find the official starter pack", StringComparison.Ordinal))
        {
            return
            [
                new TrialDiagnosticReadback(
                    "trial_setup_pack_missing",
                    "user_setup",
                    "error",
                    "The official starter pack could not be discovered from the current directory.",
                    "--pack-root",
                    "carves-matrix trial prepare --workspace <workspace> --pack-root <pack-root>",
                    "Run the command from the CARVES.Runtime source checkout or pass --pack-root explicitly.",
                    ["starter_pack_missing"])
            ];
        }

        if (IsTrialSetupCommand(command) && exception is InvalidOperationException && exception.Message.Contains("git", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new TrialDiagnosticReadback(
                    "trial_setup_git_unavailable",
                    "user_setup",
                    "error",
                    "Git could not create the local trial baseline.",
                    "git",
                    "git init && git commit -m baseline",
                    "Install Git, ensure it is on PATH, then rerun the local trial command.",
                    ["git_unavailable"])
            ];
        }

        if (command == "compare"
            && exception is FileNotFoundException historyMissing
            && historyMissing.FileName?.StartsWith("runs/", StringComparison.Ordinal) == true)
        {
            return
            [
                new TrialDiagnosticReadback(
                    "trial_history_entry_missing",
                    "user_setup",
                    "error",
                    "A requested local history run was not found.",
                    historyMissing.FileName,
                    "carves-matrix trial compare --history-root <path> --baseline <run-id> --target <run-id>",
                    "Run both local trials under the same --trial-root, pass the correct --history-root, or choose existing run ids.",
                    ["history_entry_missing"])
            ];
        }

        if (IsTrialLatestCommand(command) && exception is FileNotFoundException)
        {
            return
            [
                new TrialDiagnosticReadback(
                    "trial_latest_missing",
                    "user_setup",
                    "error",
                    "No local latest trial pointer was found.",
                    "carves-trials/latest.json",
                    "carves-matrix trial demo",
                    "Run a local trial first, or pass --bundle-root explicitly to verify a known bundle.",
                    ["latest_pointer_missing"])
            ];
        }

        if (IsTrialLatestCommand(command) && exception is InvalidDataException && exception.Message.Contains("latest local trial pointer", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new TrialDiagnosticReadback(
                    "trial_latest_invalid",
                    "user_setup",
                    "error",
                    "The local latest trial pointer is invalid or incomplete.",
                    "carves-trials/latest.json",
                    "carves-matrix trial latest --json",
                    "Delete or repair latest.json, then run a new local trial to regenerate it.",
                    ["latest_pointer_invalid"])
            ];
        }

        return
        [
            new TrialDiagnosticReadback(
                "trial_local_command_failed",
                "user_setup",
                "error",
                "The local trial command failed before producing a complete result.",
                $"trial {command}",
                $"carves-matrix trial {command}",
                "Fix the setup problem and rerun the same command. Integrity failures should be repaired at their source artifact, not suppressed.",
                [exception.GetType().Name])
        ];
    }

    private static bool IsTrialSetupCommand(string command)
    {
        return command is "prepare" or "demo" or "play" or "package";
    }

    private static IReadOnlyList<string> BuildAgentReportSchemaReasonCodes(string message)
    {
        var reasonCodes = new List<string> { "agent_report_schema_invalid" };
        if (message.Contains("schema_version expected agent-report.v0", StringComparison.Ordinal))
        {
            reasonCodes.Add("agent_report_schema_version_invalid");
        }

        if (message.Contains("missing fields:", StringComparison.Ordinal))
        {
            reasonCodes.Add("agent_report_required_fields_missing");
        }

        if (message.Contains("unexpected fields:", StringComparison.Ordinal))
        {
            reasonCodes.Add("agent_report_unexpected_fields");
        }

        return reasonCodes;
    }

    private static bool IsTrialLatestCommand(string command)
    {
        return command is "latest" or "verify" or "compare";
    }

    private static int WriteTrialFailure(string command, bool json, IReadOnlyList<TrialDiagnosticReadback> diagnostics)
    {
        if (json)
        {
            var result = new TrialCommandFailureResult(
                TrialCommandSchemaVersion,
                command,
                "failed",
                Offline: true,
                ServerSubmission: false,
                diagnostics);
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return 1;
        }

        Console.Error.WriteLine($"trial {command} failed.");
        WriteTrialDiagnostics(diagnostics, Console.Error);
        return 1;
    }

    private static void AddCollectionDiagnostics(List<TrialDiagnosticReadback> diagnostics, TrialCollectionReadback collection)
    {
        foreach (var missingArtifact in collection.MissingRequiredArtifacts)
        {
            diagnostics.Add(MissingArtifactDiagnostic(missingArtifact));
        }

        foreach (var reasonCode in collection.FailureReasons)
        {
            diagnostics.Add(reasonCode switch
            {
                "task_contract_pin_mismatch" => new TrialDiagnosticReadback(
                    "trial_task_contract_pin_mismatch",
                    "evidence_integrity",
                    "error",
                    "The task contract no longer matches the pack or challenge authority hash.",
                    ".carves/trial/task-contract.json",
                    null,
                    "Restore the starter pack metadata and rerun collection. Do not loosen the task contract to make a run pass.",
                    [reasonCode]),
                "task_contract_pin_missing" => new TrialDiagnosticReadback(
                    "trial_task_contract_pin_missing",
                    "user_setup",
                    "error",
                    "The task contract authority hash is missing, so the collector cannot trust workspace rules.",
                    ".carves/trial/challenge.json",
                    null,
                    "Recreate the workspace from the official starter pack.",
                    [reasonCode]),
                "instruction_pack_pin_mismatch" => new TrialDiagnosticReadback(
                    "trial_instruction_pack_pin_mismatch",
                    "evidence_integrity",
                    "error",
                    "The instruction pack no longer matches the expected authority hash.",
                    ".carves/trial/instruction-pack.json",
                    null,
                    "Restore the original instruction pack instead of editing prompt identity or constraints after the run.",
                    [reasonCode]),
                "portable_task_contract_hash_mismatch" => PortableAuthorityDiagnostic(
                    "trial_portable_task_contract_mismatch",
                    "The workspace task contract no longer matches the external package authority.",
                    "agent-workspace/.carves/trial/task-contract.json",
                    "Re-extract the portable package or restore the original task contract; do not loosen task rules inside the agent workspace.",
                    reasonCode),
                "portable_instruction_pack_hash_mismatch" => PortableAuthorityDiagnostic(
                    "trial_portable_instruction_pack_mismatch",
                    "The workspace instruction pack no longer matches the external package authority.",
                    "agent-workspace/.carves/trial/instruction-pack.json",
                    "Re-extract the portable package or restore the original instruction pack; do not edit prompt identity or constraints after the run.",
                    reasonCode),
                "portable_task_contract_missing" or "portable_instruction_pack_missing" => PortableAuthorityDiagnostic(
                    "trial_portable_workspace_metadata_missing",
                    "Required workspace trial metadata is missing from a portable package run.",
                    "agent-workspace/.carves/trial/",
                    "Re-extract the portable package before rerunning the agent.",
                    reasonCode,
                    category: "user_setup"),
                "portable_baseline_manifest_missing" or "portable_baseline_metadata_invalid" => PortableAuthorityDiagnostic(
                    "trial_portable_baseline_metadata_invalid",
                    "The external portable baseline metadata is missing or invalid.",
                    ".carves-pack/baseline-manifest.json",
                    "Recreate the portable package. Do not score a package after deleting or editing scorer authority metadata.",
                    reasonCode),
                "portable_baseline_git_missing" or "portable_baseline_commit_missing" => PortableAuthorityDiagnostic(
                    "trial_portable_git_baseline_missing",
                    "The workspace git baseline required by the portable package cannot be found.",
                    "agent-workspace/.git",
                    "Re-extract the portable package so the original local git baseline is present.",
                    reasonCode),
                "portable_baseline_tree_mismatch" or "portable_baseline_initial_files_mismatch" => PortableAuthorityDiagnostic(
                    "trial_portable_baseline_mismatch",
                    "The workspace baseline commit or baseline file snapshot no longer matches external package metadata.",
                    ".carves-pack/baseline-manifest.json",
                    "Re-extract the portable package; do not rewrite the baseline history for a scored run.",
                    reasonCode),
                "portable_protected_metadata_changed" => PortableAuthorityDiagnostic(
                    "trial_portable_protected_metadata_changed",
                    "Protected starter metadata changed after the portable package baseline was created.",
                    "agent-workspace/AGENTS.md",
                    "Restore the protected starter instructions and metadata, or start from a fresh package extraction.",
                    reasonCode),
                "portable_expected_task_contract_missing" or "portable_expected_instruction_pack_missing" or
                    "portable_authority_pack_missing" or "portable_authority_challenge_missing" or
                    "portable_authority_task_contract_missing" or "portable_authority_instruction_pack_missing" or
                    "portable_scoring_contract_missing" or "portable_pack_manifest_missing" => PortableAuthorityDiagnostic(
                        "trial_portable_authority_missing",
                        "Required external portable package authority metadata is missing.",
                        ".carves-pack/",
                        "Recreate the portable package. The tested agent should only receive agent-workspace, not authority internals.",
                        reasonCode,
                        category: "user_setup"),
                "portable_expected_task_contract_invalid" or "portable_expected_instruction_pack_invalid" or
                    "portable_authority_task_contract_hash_mismatch" or
                    "portable_authority_instruction_pack_hash_mismatch" => PortableAuthorityDiagnostic(
                        "trial_portable_authority_mismatch",
                        "The external portable package authority metadata is internally inconsistent.",
                        ".carves-pack/",
                        "Recreate the portable package from the official starter pack; do not hand-edit scorer authority files.",
                        reasonCode),
                "instruction_pack_missing" or "instruction_pack_pin_missing" => MissingArtifactDiagnostic("instruction_pack"),
                "agent_report_missing" => MissingArtifactDiagnostic("agent_report"),
                "required_command_failed" => RequiredCommandDiagnostic(
                    "trial_required_command_failed",
                    "A required command failed. The local score may be capped or unavailable, and this is treated as agent behavior evidence.",
                    "Run the required command yourself, inspect artifacts/test-evidence.json, and fix the task output rather than removing the required command.",
                    reasonCode),
                "required_command_timed_out" => RequiredCommandDiagnostic(
                    "trial_required_command_timed_out",
                    "A required command timed out before producing passing evidence.",
                    "Make the task output reproducible within the configured timeout, then rerun collection.",
                    reasonCode),
                "required_command_unavailable" => RequiredCommandDiagnostic(
                    "trial_required_command_unavailable",
                    "A required command could not be started in the local workspace.",
                    "Install the required local tooling or run from an environment that can execute the command.",
                    reasonCode,
                    category: "user_setup"),
                "diff_scope_unavailable" => new TrialDiagnosticReadback(
                    "trial_diff_scope_unavailable",
                    "evidence_integrity",
                    "error",
                    "The collector could not produce reliable changed-file scope evidence.",
                    "artifacts/diff-scope-summary.json",
                    "git status --porcelain=v1",
                    "Restore a git-backed workspace and rerun collection so path-boundary evidence can be collected.",
                    [reasonCode]),
                _ => new TrialDiagnosticReadback(
                    "trial_collection_issue",
                    "agent_behavior",
                    "error",
                    "The collector reported a local trial issue.",
                    "artifacts/carves-agent-trial-result.json",
                    null,
                    "Inspect the local trial result reason codes and repair the underlying evidence.",
                    [reasonCode]),
            });
        }
    }

    private static TrialDiagnosticReadback MissingArtifactDiagnostic(string artifact)
    {
        return artifact switch
        {
            "agent_report" => new TrialDiagnosticReadback(
                "trial_agent_report_missing",
                "user_setup",
                "error",
                "The required agent report is missing, so the run cannot be fully reviewed or scored.",
                "artifacts/agent-report.json",
                null,
                "Have the agent write the required summary report, then rerun collection.",
                ["agent_report_missing"]),
            "instruction_pack" => new TrialDiagnosticReadback(
                "trial_instruction_pack_missing",
                "user_setup",
                "error",
                "The instruction pack is missing or cannot be trusted.",
                ".carves/trial/instruction-pack.json",
                null,
                "Recreate the workspace from the official starter pack before rerunning the agent.",
                ["instruction_pack_missing"]),
            _ => new TrialDiagnosticReadback(
                "trial_required_artifact_missing",
                "user_setup",
                "error",
                "A required local trial artifact is missing.",
                "artifacts/",
                null,
                "Regenerate the missing local evidence from the trial workspace and rerun collection.",
                [$"missing_required_artifact:{artifact}"]),
        };
    }

    private static TrialDiagnosticReadback RequiredCommandDiagnostic(string code, string message, string nextStep, string reasonCode, string category = "agent_behavior")
    {
        return new TrialDiagnosticReadback(
            code,
            category,
            "error",
            message,
            "artifacts/test-evidence.json",
            "required_commands",
            nextStep,
            [reasonCode]);
    }

    private static TrialDiagnosticReadback PortableAuthorityDiagnostic(
        string code,
        string message,
        string evidenceRef,
        string nextStep,
        string reasonCode,
        string category = "evidence_integrity")
    {
        return new TrialDiagnosticReadback(
            code,
            category,
            "error",
            message,
            evidenceRef,
            "carves test package --output <fresh-package-root>",
            nextStep,
            [reasonCode]);
    }

    private static void AddVerificationReasonDiagnostic(List<TrialDiagnosticReadback> diagnostics, string reasonCode)
    {
        var diagnostic = reasonCode switch
        {
            "hash_mismatch" => new TrialDiagnosticReadback(
                "trial_verify_hash_mismatch",
                "evidence_integrity",
                "error",
                "A manifest-covered artifact does not match its recorded hash.",
                "matrix-artifact-manifest.json",
                null,
                "Regenerate the bundle from the original workspace evidence; do not hand-edit covered artifacts after manifest creation.",
                [reasonCode]),
            "missing_artifact" => new TrialDiagnosticReadback(
                "trial_verify_missing_artifact",
                "evidence_integrity",
                "error",
                "A required manifest-covered trial artifact is missing.",
                "matrix-artifact-manifest.json",
                null,
                "Regenerate the trial bundle so every required trial artifact is manifest-covered.",
                [reasonCode]),
            "trial_artifact_schema_invalid" or "schema_mismatch" => new TrialDiagnosticReadback(
                "trial_verify_schema_invalid",
                "evidence_integrity",
                "error",
                "A trial artifact does not satisfy the public schema contract.",
                "trial/",
                null,
                "Regenerate the artifact from the collector instead of editing schema fields manually.",
                [reasonCode]),
            "trial_artifact_consistency_mismatch" => new TrialDiagnosticReadback(
                "trial_verify_consistency_mismatch",
                "evidence_integrity",
                "error",
                "Manifest-covered trial artifacts disagree on shared task, prompt, or challenge identity.",
                "trial/",
                null,
                "Regenerate the bundle from one workspace run so all trial artifacts share the same identity fields.",
                [reasonCode]),
            _ => null,
        };

        if (diagnostic is not null)
        {
            diagnostics.Add(diagnostic);
        }
    }
}

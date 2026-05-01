using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.Workers;

namespace Carves.Runtime.Application.AI;

public sealed class WorkerAiRequestFactory
{
    private readonly int maxOutputTokens;
    private readonly string? defaultModel;
    private readonly string? defaultReasoningEffort;
    private readonly WorkerRequestBudgetPolicyService requestBudgetPolicyService;
    private readonly RuntimeTokenWorkerWrapperCanaryService wrapperCanaryService;

    public WorkerAiRequestFactory(
        int maxOutputTokens,
        int timeoutSeconds,
        string? defaultModel,
        string? defaultReasoningEffort,
        RuntimeTokenWorkerWrapperCanaryService? wrapperCanaryService = null)
    {
        this.maxOutputTokens = maxOutputTokens;
        this.defaultModel = string.IsNullOrWhiteSpace(defaultModel) ? null : defaultModel;
        this.defaultReasoningEffort = string.IsNullOrWhiteSpace(defaultReasoningEffort) ? null : defaultReasoningEffort.Trim();
        requestBudgetPolicyService = new WorkerRequestBudgetPolicyService(timeoutSeconds);
        this.wrapperCanaryService = wrapperCanaryService ?? new RuntimeTokenWorkerWrapperCanaryService();
    }

    public WorkerExecutionRequest Create(
        TaskNode task,
        ContextPack contextPack,
        ExecutionPacket packet,
        string packetPath,
        WorkerExecutionProfile profile,
        string repoRoot,
        string worktreeRoot,
        string baseCommit,
        bool dryRun,
        string backendHint,
        IReadOnlyList<IReadOnlyList<string>> validationCommands,
        WorkerSelectionDecision selection,
        IReadOnlyDictionary<string, string>? additionalMetadata = null)
    {
        var requestId = $"worker-request-{Guid.NewGuid():N}";
        var effectiveMaxOutputTokens = ResolveMaxOutputTokens(selection);
        var requestBudget = requestBudgetPolicyService.Resolve(task, selection, repoRoot, worktreeRoot);
        var inputSections = new List<string>
        {
            contextPack.PromptInput,
            string.Empty,
            $"Worktree root: {worktreeRoot}",
            $"Base commit: {baseCommit}",
            "Acceptance:",
            string.Join(Environment.NewLine, task.Acceptance.Select(item => $"- {item}")),
        };
        var acceptanceContract = AcceptanceContractSummaryFormatter.BuildPlainTextBlock("Acceptance contract:", packet.AcceptanceContract);
        if (!string.IsNullOrWhiteSpace(acceptanceContract))
        {
            inputSections.Add(acceptanceContract);
        }

        var validationContract = BuildValidationContract(validationCommands);
        if (!string.IsNullOrWhiteSpace(validationContract))
        {
            inputSections.Add(validationContract);
        }

        var workerExecutionPacketContract = BuildWorkerExecutionPacketContract(packet.WorkerExecutionPacket);
        if (!string.IsNullOrWhiteSpace(workerExecutionPacketContract))
        {
            inputSections.Add(workerExecutionPacketContract);
        }

        var closureContract = BuildClosureContract(packet);
        if (!string.IsNullOrWhiteSpace(closureContract))
        {
            inputSections.Add(closureContract);
        }

        var patchBudgetContract = BuildPatchBudgetContract(packet);
        if (!string.IsNullOrWhiteSpace(patchBudgetContract))
        {
            inputSections.Add(patchBudgetContract);
        }

        var requestBudgetContract = BuildRequestBudgetContract(requestBudget);
        if (!string.IsNullOrWhiteSpace(requestBudgetContract))
        {
            inputSections.Add(requestBudgetContract);
        }

        var input = string.Join(Environment.NewLine, inputSections);

        var originalInstructions = BuildInstructions(task, packet, selection);
        var canaryDecision = wrapperCanaryService.ResolveWorkerSystemInstructions(task, packet, requestBudget, originalInstructions);
        var instructions = canaryDecision.EffectiveInstructions;
        var requestEnvelopeDraft = BuildRequestEnvelopeDraft(
            task,
            contextPack,
            selection,
            requestBudget,
            worktreeRoot,
            baseCommit,
            instructions,
            validationContract,
            workerExecutionPacketContract,
            closureContract,
            patchBudgetContract,
            requestBudgetContract,
            acceptanceContract,
            input,
            requestId);
        var capTruth = RuntimeTokenCapTruthResolver.FromContextPackBudget(contextPack.Budget);

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["task_type"] = task.TaskType.ToString(),
            ["priority"] = task.Priority,
            ["proposal_source"] = task.ProposalSource.ToString(),
            ["worker_backend"] = selection.SelectedBackendId ?? backendHint,
            ["worker_provider"] = selection.SelectedProviderId ?? "unknown",
            ["worker_trust_profile"] = profile.ProfileId,
            ["worker_model"] = selection.SelectedModelId ?? defaultModel ?? string.Empty,
            ["worker_selection_reason"] = selection.Summary,
            ["route_source"] = selection.RouteSource,
            ["route_reason"] = selection.RouteReason,
            ["routing_intent"] = selection.RoutingIntent ?? string.Empty,
            ["routing_module"] = selection.RoutingModuleId ?? string.Empty,
            ["routing_rule_id"] = selection.AppliedRoutingRuleId ?? string.Empty,
            ["active_routing_profile_id"] = selection.ActiveRoutingProfileId ?? string.Empty,
            ["selected_routing_profile_id"] = selection.SelectedRoutingProfileId ?? string.Empty,
            ["route_request_family"] = selection.SelectedRequestFamily ?? string.Empty,
            ["route_base_url"] = selection.SelectedBaseUrl ?? string.Empty,
            ["route_api_key_env"] = selection.SelectedApiKeyEnvironmentVariable ?? string.Empty,
            ["worker_request_timeout_seconds"] = requestBudget.TimeoutSeconds.ToString(),
            ["worker_request_budget_policy_id"] = requestBudget.PolicyId,
            ["worker_request_budget_summary"] = requestBudget.Summary,
            ["worker_request_budget_provider_baseline_seconds"] = requestBudget.ProviderBaselineSeconds.ToString(),
            ["worker_request_budget_max_duration_minutes"] = requestBudget.MaxDurationMinutes.ToString(),
            ["worker_request_budget_confidence_level"] = requestBudget.ConfidenceLevel.ToString(),
            ["worker_request_budget_reasons"] = string.Join("|", requestBudget.Reasons),
            ["requested_thread_id"] = task.Metadata.TryGetValue("codex_resume_thread_id", out var requestedThreadId) ? requestedThreadId : string.Empty,
            ["context_pack_id"] = contextPack.PackId,
            ["context_pack_path"] = contextPack.ArtifactPath ?? string.Empty,
            ["context_pack_tokens"] = contextPack.Budget.UsedTokens.ToString(),
            ["context_pack_trimmed"] = contextPack.Trimmed.Count.ToString(),
            ["context_pack_budget_posture"] = contextPack.Budget.BudgetPosture,
            ["context_pack_budget_violation_reasons"] = string.Join("|", contextPack.Budget.BudgetViolationReasonCodes),
            ["context_pack_truncated_items_count"] = contextPack.Budget.TruncatedItemsCount.ToString(),
            ["context_pack_dropped_items_count"] = contextPack.Budget.DroppedItemsCount.ToString(),
            ["context_pack_full_doc_blocked_count"] = contextPack.Budget.FullDocBlockedCount.ToString(),
            ["context_pack_provider_context_cap_hit"] = capTruth?.ProviderContextCapHit?.ToString().ToLowerInvariant() ?? string.Empty,
            ["context_pack_internal_prompt_budget_cap_hit"] = capTruth?.InternalPromptBudgetCapHit?.ToString().ToLowerInvariant() ?? string.Empty,
            ["context_pack_section_budget_cap_hit"] = capTruth?.SectionBudgetCapHit?.ToString().ToLowerInvariant() ?? string.Empty,
            ["context_pack_trim_loop_cap_hit"] = capTruth?.TrimLoopCapHit?.ToString().ToLowerInvariant() ?? string.Empty,
            ["context_pack_cap_trigger_segment_kind"] = capTruth?.CapTriggerSegmentKind ?? string.Empty,
            ["context_pack_cap_trigger_source"] = capTruth?.CapTriggerSource ?? string.Empty,
            ["execution_packet_id"] = packet.PacketId,
            ["execution_packet_path"] = packetPath,
            ["planner_intent"] = packet.PlannerIntent.ToString(),
            ["worker_execution_packet_id"] = packet.WorkerExecutionPacket.PacketId,
            ["worker_execution_packet_schema"] = packet.WorkerExecutionPacket.SchemaVersion,
            ["worker_execution_packet_source_execution_packet_id"] = packet.WorkerExecutionPacket.SourceExecutionPacketId,
            ["worker_execution_packet_allowed_files"] = string.Join("|", packet.WorkerExecutionPacket.AllowedFiles),
            ["worker_execution_packet_allowed_actions"] = string.Join("|", packet.WorkerExecutionPacket.AllowedActions),
            ["worker_execution_packet_required_contract_matrix"] = string.Join("|", packet.WorkerExecutionPacket.RequiredContractMatrix),
            ["worker_execution_packet_required_validation"] = string.Join("|", packet.WorkerExecutionPacket.RequiredValidation),
            ["worker_execution_packet_required_validation_gates"] = string.Join("|", packet.WorkerExecutionPacket.RequiredValidationGates),
            ["worker_execution_packet_evidence_required"] = string.Join("|", packet.WorkerExecutionPacket.EvidenceRequired),
            ["worker_execution_packet_forbidden_vocabulary"] = string.Join("|", packet.WorkerExecutionPacket.ForbiddenVocabulary),
            ["worker_execution_packet_completion_claim_fields"] = string.Join("|", packet.WorkerExecutionPacket.CompletionClaimSchema.Fields),
            ["worker_execution_packet_completion_claim_required"] = packet.WorkerExecutionPacket.CompletionClaimSchema.Required.ToString().ToLowerInvariant(),
            ["worker_execution_packet_claim_is_truth"] = packet.WorkerExecutionPacket.CompletionClaimSchema.ClaimIsTruth.ToString().ToLowerInvariant(),
            ["worker_execution_packet_host_validation_required"] = packet.WorkerExecutionPacket.CompletionClaimSchema.HostValidationRequired.ToString().ToLowerInvariant(),
            ["worker_execution_packet_result_channel"] = packet.WorkerExecutionPacket.ResultSubmission.CandidateResultChannel,
            ["worker_execution_packet_host_ingest_command"] = packet.WorkerExecutionPacket.ResultSubmission.HostIngestCommand,
            ["worker_execution_packet_candidate_only"] = packet.WorkerExecutionPacket.ResultSubmission.CandidateOnly.ToString().ToLowerInvariant(),
            ["worker_execution_packet_review_bundle_required"] = packet.WorkerExecutionPacket.ResultSubmission.ReviewBundleRequired.ToString().ToLowerInvariant(),
            ["worker_execution_packet_submitted_by_host_or_adapter"] = packet.WorkerExecutionPacket.ResultSubmission.SubmittedByHostOrAdapter.ToString().ToLowerInvariant(),
            ["worker_execution_packet_writes_truth_roots"] = packet.WorkerExecutionPacket.WritesTruthRoots.ToString().ToLowerInvariant(),
            ["worker_execution_packet_creates_task_queue"] = packet.WorkerExecutionPacket.CreatesTaskQueue.ToString().ToLowerInvariant(),
            ["closure_contract_protocol_id"] = packet.ClosureContract.ProtocolId,
            ["closure_contract_profile_id"] = packet.ClosureContract.ContractMatrixProfileId,
            ["closure_contract_required_checks"] = string.Join("|", packet.ClosureContract.RequiredContractChecks),
            ["closure_contract_required_validation_gates"] = string.Join("|", packet.ClosureContract.RequiredValidationGates),
            ["closure_contract_completion_claim_required"] = packet.ClosureContract.CompletionClaimRequired.ToString().ToLowerInvariant(),
            ["closure_contract_completion_claim_fields"] = string.Join("|", packet.ClosureContract.CompletionClaimFields),
            ["closure_contract_forbidden_vocabulary"] = string.Join("|", packet.ClosureContract.ForbiddenVocabulary),
            ["acceptance_contract_id"] = packet.AcceptanceContract?.ContractId ?? string.Empty,
            ["acceptance_contract_status"] = packet.AcceptanceContract?.Status.ToString() ?? string.Empty,
            ["acceptance_contract_evidence_required"] = packet.AcceptanceContract is null
                ? string.Empty
                : string.Join("|", packet.AcceptanceContract.EvidenceRequired.Select(item => item.Type)),
            ["acceptance_contract_human_review_required"] = packet.AcceptanceContract is null
                ? string.Empty
                : packet.AcceptanceContract.HumanReview.Required.ToString().ToLowerInvariant(),
            ["acceptance_contract_provisional_allowed"] = packet.AcceptanceContract is null
                ? string.Empty
                : packet.AcceptanceContract.HumanReview.ProvisionalAllowed.ToString().ToLowerInvariant(),
            ["acceptance_contract_human_decisions"] = packet.AcceptanceContract is null
                ? string.Empty
                : string.Join("|", packet.AcceptanceContract.HumanReview.Decisions.Select(item => item.ToString())),
            ["worker_wrapper_canary_target_surface"] = canaryDecision.TargetSurface,
            ["worker_wrapper_canary_request_kind"] = canaryDecision.RequestKind,
            ["worker_wrapper_canary_scope"] = canaryDecision.ApprovalScope,
            ["worker_wrapper_main_path_default_enabled"] = canaryDecision.MainPathDefaultEnabled.ToString().ToLowerInvariant(),
            ["worker_wrapper_canary_enabled"] = canaryDecision.CanaryEnabled.ToString().ToLowerInvariant(),
            ["worker_wrapper_canary_kill_switch_active"] = canaryDecision.GlobalKillSwitchActive.ToString().ToLowerInvariant(),
            ["worker_wrapper_canary_request_kind_allowlisted"] = canaryDecision.RequestKindAllowlisted.ToString().ToLowerInvariant(),
            ["worker_wrapper_canary_surface_allowlisted"] = canaryDecision.SurfaceAllowlisted.ToString().ToLowerInvariant(),
            ["worker_wrapper_canary_candidate_version_pinned"] = canaryDecision.CandidateVersionPinned.ToString().ToLowerInvariant(),
            ["worker_wrapper_canary_candidate_applied"] = canaryDecision.CandidateApplied.ToString().ToLowerInvariant(),
            ["worker_wrapper_decision_mode"] = canaryDecision.DecisionMode,
            ["worker_wrapper_canary_decision_reason"] = canaryDecision.DecisionReason,
            ["worker_wrapper_canary_candidate_version"] = canaryDecision.CandidateVersion,
            ["worker_wrapper_canary_fallback_version"] = canaryDecision.FallbackVersion,
        };

        if (additionalMetadata is not null)
        {
            foreach (var (key, value) in additionalMetadata)
            {
                metadata[key] = value;
            }
        }

        return new WorkerExecutionRequest
        {
            RequestId = requestId,
            TaskId = task.TaskId,
            RepoId = selection.RepoId,
            Title = task.Title,
            Description = task.Description,
            Instructions = instructions,
            Input = input,
            MaxOutputTokens = effectiveMaxOutputTokens,
            TimeoutSeconds = requestBudget.TimeoutSeconds,
            RequestBudget = requestBudget,
            RepoRoot = repoRoot,
            WorktreeRoot = worktreeRoot,
            BaseCommit = baseCommit,
            PriorThreadId = task.Metadata.TryGetValue("codex_resume_thread_id", out var priorThreadId)
                && !string.IsNullOrWhiteSpace(priorThreadId)
                ? priorThreadId
                : null,
            BackendHint = backendHint,
            ModelOverride = selection.SelectedModelId ?? defaultModel,
            ReasoningEffort = defaultReasoningEffort,
            RoutingIntent = selection.RoutingIntent,
            RoutingModuleId = selection.RoutingModuleId,
            RoutingProfileId = selection.SelectedRoutingProfileId,
            RoutingRuleId = selection.AppliedRoutingRuleId,
            ActiveRoutingProfileId = selection.ActiveRoutingProfileId,
            DryRun = dryRun,
            Packet = packet,
            WorkerExecutionPacket = packet.WorkerExecutionPacket,
            Profile = profile,
            AllowedFiles = packet.WorkerExecutionPacket.AllowedFiles.Count == 0
                ? task.Scope.Select(path => path.Trim().Trim('`')).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                : packet.WorkerExecutionPacket.AllowedFiles,
            ValidationCommands = validationCommands,
            RequestEnvelopeDraft = requestEnvelopeDraft,
            Metadata = metadata,
        };
    }

    private int ResolveMaxOutputTokens(WorkerSelectionDecision selection)
    {
        if (string.Equals(selection.RoutingIntent, "reasoning_summary", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(maxOutputTokens, 900);
        }

        return maxOutputTokens;
    }

    private static string BuildValidationContract(IReadOnlyList<IReadOnlyList<string>> validationCommands)
    {
        if (validationCommands.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            "Formal validation commands (executed by CARVES after you finish; do not run them yourself):",
            string.Join(
                Environment.NewLine,
                validationCommands.Select(command => $"- {string.Join(' ', command)}")));
    }

    private static string BuildPatchBudgetContract(ExecutionPacket packet)
    {
        return string.Join(
            Environment.NewLine,
            "Execution patch budget (authoritative; do not exceed it):",
            $"- Max files changed: {packet.Budgets.MaxFilesChanged}",
            $"- Max lines changed: {packet.Budgets.MaxLinesChanged}",
            $"- Max shell commands: {packet.Budgets.MaxShellCommands}",
            $"Stop conditions: {(packet.StopConditions.Count == 0 ? "(none)" : string.Join(" | ", packet.StopConditions))}");
    }

    private static string BuildClosureContract(ExecutionPacket packet)
    {
        var contract = packet.ClosureContract;
        return string.Join(
            Environment.NewLine,
            "Result closure contract (Host verifies this after the worker returns; your completion claim is not truth):",
            $"- Protocol: {contract.ProtocolId}",
            $"- Contract matrix profile: {contract.ContractMatrixProfileId}",
            $"- Required contract checks: {(contract.RequiredContractChecks.Count == 0 ? "(none)" : string.Join(" | ", contract.RequiredContractChecks))}",
            $"- Required validation gates: {(contract.RequiredValidationGates.Count == 0 ? "(none)" : string.Join(" | ", contract.RequiredValidationGates))}",
            $"- Completion claim required: {FormatYesNo(contract.CompletionClaimRequired)}",
            $"- Completion claim fields: {(contract.CompletionClaimFields.Count == 0 ? "(none)" : string.Join(" | ", contract.CompletionClaimFields))}",
            $"- Forbidden vocabulary: {(contract.ForbiddenVocabulary.Count == 0 ? "(none)" : string.Join(" | ", contract.ForbiddenVocabulary))}",
            $"- Evidence/readback surfaces: {(contract.EvidenceSurfaces.Count == 0 ? "(none)" : string.Join(" | ", contract.EvidenceSurfaces))}",
            $"- Summary: {contract.Summary}");
    }

    private static string BuildWorkerExecutionPacketContract(WorkerExecutionPacket packet)
    {
        if (string.IsNullOrWhiteSpace(packet.PacketId))
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            "Worker execution packet (Host-issued execution contract; this is not a queue or truth root):",
            $"- Packet: {packet.PacketId}",
            $"- Source execution packet: {packet.SourceExecutionPacketId}",
            $"- Allowed files: {(packet.AllowedFiles.Count == 0 ? "(none)" : string.Join(" | ", packet.AllowedFiles))}",
            $"- Allowed actions: {(packet.AllowedActions.Count == 0 ? "(none)" : string.Join(" | ", packet.AllowedActions))}",
            $"- Required contract matrix: {(packet.RequiredContractMatrix.Count == 0 ? "(none)" : string.Join(" | ", packet.RequiredContractMatrix))}",
            $"- Required validation: {(packet.RequiredValidation.Count == 0 ? "(none)" : string.Join(" | ", packet.RequiredValidation))}",
            $"- Required validation gates: {(packet.RequiredValidationGates.Count == 0 ? "(none)" : string.Join(" | ", packet.RequiredValidationGates))}",
            $"- Evidence required: {(packet.EvidenceRequired.Count == 0 ? "(none)" : string.Join(" | ", packet.EvidenceRequired))}",
            $"- Forbidden vocabulary: {(packet.ForbiddenVocabulary.Count == 0 ? "(none)" : string.Join(" | ", packet.ForbiddenVocabulary))}",
            $"- Completion claim fields: {(packet.CompletionClaimSchema.Fields.Count == 0 ? "(none)" : string.Join(" | ", packet.CompletionClaimSchema.Fields))}",
            $"- Completion claim is truth: {FormatYesNo(packet.CompletionClaimSchema.ClaimIsTruth)}",
            "- Completion claim validation: Host checks required fields, worker-claimable contract items, allowed files, and forbidden vocabulary before review writeback.",
            $"- Result channel: {packet.ResultSubmission.CandidateResultChannel}",
            $"- Host ingest command: {packet.ResultSubmission.HostIngestCommand}",
            $"- Candidate only: {FormatYesNo(packet.ResultSubmission.CandidateOnly)}",
            $"- Review bundle required: {FormatYesNo(packet.ResultSubmission.ReviewBundleRequired)}",
            $"- Writes truth roots: {FormatYesNo(packet.WritesTruthRoots)}",
            $"- Creates task queue: {FormatYesNo(packet.CreatesTaskQueue)}");
    }

    private static string BuildRequestBudgetContract(WorkerRequestBudget requestBudget)
    {
        if (requestBudget.TimeoutSeconds <= 0)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            "Delegated request budget (governed runtime truth):",
            $"- Timeout: {requestBudget.TimeoutSeconds} seconds",
            $"- Policy: {requestBudget.PolicyId}",
            $"- Why: {requestBudget.Rationale}");
    }

    private static string FormatYesNo(bool value)
    {
        return value ? "yes" : "no";
    }

    private static string BuildInstructions(TaskNode task, ExecutionPacket packet, WorkerSelectionDecision selection)
    {
        var instructions = "You are CARVES.Runtime's governed worker. Stay inside scope, respect sandbox and approval policy, edit only allowed files, and summarize commands, changed files, validation, and risks. Do not ask for confirmation, preferred paths, or output format when the task is already bounded; choose a reasonable default and complete the task in one pass. If no file edits are required, return the final assessment directly instead of a plan. Keep assessment-only responses compact: omit methodology restatements and command recaps, and output only the table or flat bullets needed to satisfy acceptance.";
        instructions += " Host-governed initialization and task packet assembly are already satisfied for this delegated run. Do not spend startup budget re-reading broad repo entry or governance documents as a ritual. Do not emit a `CARVES.AI initialization report`, `Agent bootstrap sources`, or any other bootstrap restatement inside delegated execution. Start from the provided context pack, execution packet, and named task/card truth, then open only the concrete source files needed to finish the task. Re-open `README.md`, `AGENTS.md`, `.ai/memory/architecture/*`, `.ai/PROJECT_BOUNDARY.md`, or `.ai/STATE.md` only when the task scope, execution packet, or a concrete escalation trigger explicitly requires them.";
        instructions += BuildShellInstruction(selection);
        instructions += " Formal build/test validation is executed by CARVES after the worker returns. Do not run `dotnet restore`, `dotnet build`, or `dotnet test` as routine verification inside delegated execution unless the task explicitly asks you to diagnose a local toolchain failure. In delegated managed worktrees that may not carry a `.git` directory, do not use `git status` or `git diff` as routine patch verification; verify target files directly with `sed -n`, `rg`, `cat`, `head`, `tail`, or `test -f`, then report the changed paths in your final summary. Before the first edit, do not concatenate broad `sed -n`/`cat` bundles or keep widening `rg` sweeps across related modules; read at most one or two directly relevant files per command. After editing, do not concatenate broad `sed -n`/`cat` readbacks across multiple changed files just to restate the patch. Spot-check only the minimum lines needed to confirm the write landed, then stop. Finish the scoped edits, do only minimal self-checks needed to confirm the files were written correctly, and then hand control back promptly.";
        instructions += " For implementation tasks, do not stop at a plan, file inventory, or intent recap once the target edit surface is clear. Do not build helper scripts that bulk-print task truth or dump multiple source files to stdout as preflight. Use `python3` only for bounded file edits, not for repository inspection. After the first bounded read pass, either write the smallest viable patch immediately or return a concrete blocker that explains why no safe edit can be made inside the declared budget. If you still have not started editing after roughly four to six targeted shell reads, stop and return the blocker instead of continuing to widen the read pass.";
        instructions += $" Stay within the declared execution budget (<= {packet.Budgets.MaxFilesChanged} files, <= {packet.Budgets.MaxLinesChanged} changed lines, <= {packet.Budgets.MaxShellCommands} shell commands).";
        instructions += " If the requested change appears likely to exceed that patch budget, do not force a large one-pass patch. Prefer the narrowest high-value slice that stays within budget, or return a bounded assessment that says the task must be split or narrowed before further editing.";
        instructions += $" Treat these stop conditions as hard preflight boundaries: {(packet.StopConditions.Count == 0 ? "(none)" : string.Join(", ", packet.StopConditions))}.";

        if (RequiresConcreteSourceGrounding(task))
        {
            instructions += " Use only task IDs, artifact paths, and evidence identifiers that are explicitly present in the provided context. Do not invent, rename, or substitute source identifiers; if a required source is missing from context, say it is missing rather than fabricating one.";
        }

        return instructions;
    }

    private static string BuildShellInstruction(WorkerSelectionDecision selection)
    {
        if (UsesLocalCodexCliOnPosix(selection))
        {
            return " This delegated worker environment gives you a POSIX/bash shell, not the Codex apply_patch tool and not Windows PowerShell. Do not use PowerShell-only commands such as `Get-Content`, `Set-Content`, `Select-String`, `Test-Path`, or `Where-Object`. For read/search commands, prefer `rg`, `sed -n`, `cat`, `head`, and `tail`. For file edits, prefer small `python3` scripted replacements or bounded shell redirection, then verify with `test -f`, `rg`, or `sed -n`. If a command fails because of shell syntax, adapt it to bash instead of retrying the same PowerShell pattern.";
        }

        return " This delegated worker environment gives you a shell, not the Codex apply_patch tool. The shell is Windows PowerShell. Do not use bash-only edit syntax such as `ApplyPatch <<'PATCH'`, shell heredocs, or `sed -n`. For file edits, use PowerShell-native commands like `Get-Content -Raw`, here-strings, targeted string replacement, and `Set-Content`. When creating or replacing a file, prefer a pattern like `$path = 'relative/file.cs'; @'...content...'@ | Set-Content -Path $path`, then verify with `Test-Path $path` or `Get-Content -Path $path`. For search/read commands, prefer `rg`, `Get-ChildItem`, and `Get-Content`. Do not use ambiguous PowerShell alias/filter shorthand such as `?` or incomplete `Where-Object`; when filtering pipeline output, use a complete `Where-Object { ... }` block or `Select-String`, and prefer direct `rg <pattern> <path>` over `rg --files ... | ? ...`. If a command fails because of shell syntax, adapt it to PowerShell instead of retrying the same bash pattern.";
    }

    private static bool UsesLocalCodexCliOnPosix(WorkerSelectionDecision selection)
    {
        return !OperatingSystem.IsWindows()
            && string.Equals(selection.SelectedBackendId, "codex_cli", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresConcreteSourceGrounding(TaskNode task)
    {
        return task.Acceptance.Any(item => item.Contains("references its archived evidence source", StringComparison.OrdinalIgnoreCase)
                                           || item.Contains("explicitly derived", StringComparison.OrdinalIgnoreCase));
    }

    private static Carves.Runtime.Domain.AI.LlmRequestEnvelopeDraft BuildRequestEnvelopeDraft(
        TaskNode task,
        ContextPack contextPack,
        WorkerSelectionDecision selection,
        WorkerRequestBudget requestBudget,
        string worktreeRoot,
        string baseCommit,
        string instructions,
        string validationContract,
        string workerExecutionPacketContract,
        string closureContract,
        string patchBudgetContract,
        string requestBudgetContract,
        string acceptanceContract,
        string input,
        string requestId)
    {
        var segments = new List<Carves.Runtime.Domain.AI.LlmRequestEnvelopeSegmentDraft>();
        var order = 0;

        if (!string.IsNullOrWhiteSpace(instructions))
        {
            segments.Add(NewEnvelopeSegment(
                "system_instructions",
                "system",
                null,
                order++,
                messageIndex: 0,
                role: "system",
                payloadPath: "$.instructions",
                serializationKind: "developer_policy_text",
                content: instructions,
                sourceItemId: task.TaskId,
                rendererVersion: "worker_request_serializer.v1"));
        }

        segments.Add(NewEnvelopeSegment(
            "context_pack",
            "context_pack",
            "user_input",
            order++,
            messageIndex: 1,
            role: "user",
            payloadPath: "$.input.context_pack",
            serializationKind: "context_pack_text",
            content: contextPack.PromptInput,
            sourceItemId: contextPack.PackId,
            rendererVersion: "prose_v1"));

        foreach (var promptSection in contextPack.PromptSections)
        {
            var length = Math.Max(0, promptSection.EndChar - promptSection.StartChar);
            var content = length == 0 || promptSection.StartChar >= contextPack.PromptInput.Length
                ? string.Empty
                : contextPack.PromptInput.Substring(promptSection.StartChar, Math.Min(length, contextPack.PromptInput.Length - promptSection.StartChar));
            segments.Add(NewEnvelopeSegment(
                $"context_pack:{promptSection.SectionId}",
                promptSection.SectionKind,
                "context_pack",
                order++,
                messageIndex: 1,
                role: "user",
                payloadPath: $"$.input.context_pack.sections[{promptSection.SectionId}]",
                serializationKind: "context_pack_text",
                content: content,
                sourceItemId: promptSection.SourceItemId,
                rendererVersion: promptSection.RendererVersion));
        }

        segments.Add(NewEnvelopeSegment(
            "worktree_root",
            "worktree_root",
            "user_input",
            order++,
            messageIndex: 1,
            role: "user",
            payloadPath: "$.input.worktree_root",
            serializationKind: "chat_message_text",
            content: $"Worktree root: {worktreeRoot}",
            sourceItemId: task.TaskId,
            rendererVersion: "worker_request_serializer.v1"));
        segments.Add(NewEnvelopeSegment(
            "base_commit",
            "base_commit",
            "user_input",
            order++,
            messageIndex: 1,
            role: "user",
            payloadPath: "$.input.base_commit",
            serializationKind: "chat_message_text",
            content: $"Base commit: {baseCommit}",
            sourceItemId: task.TaskId,
            rendererVersion: "worker_request_serializer.v1"));
        segments.Add(NewEnvelopeSegment(
            "acceptance",
            "acceptance",
            "user_input",
            order++,
            messageIndex: 1,
            role: "user",
            payloadPath: "$.input.acceptance",
            serializationKind: "chat_message_text",
            content: string.Join(Environment.NewLine, new[] { "Acceptance:" }.Concat(task.Acceptance.Select(item => $"- {item}"))),
            sourceItemId: task.TaskId,
            rendererVersion: "worker_request_serializer.v1"));

        AddOptionalSegment(segments, "acceptance_contract", "acceptance_contract", order++, "$.input.acceptance_contract", acceptanceContract, task.TaskId);
        AddOptionalSegment(segments, "validation_contract", "validation_contract", order++, "$.input.validation_contract", validationContract, task.TaskId);
        AddOptionalSegment(segments, "worker_execution_packet", "worker_execution_packet", order++, "$.input.worker_execution_packet", workerExecutionPacketContract, task.TaskId);
        AddOptionalSegment(segments, "closure_contract", "closure_contract", order++, "$.input.closure_contract", closureContract, task.TaskId);
        AddOptionalSegment(segments, "patch_budget_contract", "patch_budget_contract", order++, "$.input.patch_budget_contract", patchBudgetContract, task.TaskId);
        AddOptionalSegment(segments, "request_budget_contract", "request_budget_contract", order++, "$.input.request_budget_contract", requestBudgetContract, requestBudget.PolicyId);

        return new Carves.Runtime.Domain.AI.LlmRequestEnvelopeDraft
        {
            RequestId = requestId,
            RequestKind = "worker",
            Model = selection.SelectedModelId ?? string.Empty,
            Provider = selection.SelectedProviderId ?? selection.SelectedBackendId ?? "unknown",
            ProviderApiVersion = "n/a",
            Tokenizer = ContextBudgetPolicyResolver.EstimatorVersion,
            RequestSerializerVersion = "worker_request_serializer.v1",
            TaskId = task.TaskId,
            PackId = contextPack.PackId,
            WholeRequestText = string.Join($"{Environment.NewLine}{Environment.NewLine}", segments.Where(item => !string.IsNullOrWhiteSpace(item.Content)).Select(item => item.Content)),
            Segments = segments,
        };
    }

    private static void AddOptionalSegment(
        ICollection<Carves.Runtime.Domain.AI.LlmRequestEnvelopeSegmentDraft> segments,
        string segmentId,
        string segmentKind,
        int order,
        string payloadPath,
        string content,
        string? sourceItemId)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        segments.Add(NewEnvelopeSegment(
            segmentId,
            segmentKind,
            "user_input",
            order,
            messageIndex: 1,
            role: "user",
            payloadPath: payloadPath,
            serializationKind: "chat_message_text",
            content: content,
            sourceItemId: sourceItemId,
            rendererVersion: "worker_request_serializer.v1"));
    }

    private static Carves.Runtime.Domain.AI.LlmRequestEnvelopeSegmentDraft NewEnvelopeSegment(
        string segmentId,
        string segmentKind,
        string? parentId,
        int order,
        int? messageIndex,
        string? role,
        string payloadPath,
        string serializationKind,
        string content,
        string? sourceItemId,
        string rendererVersion)
    {
        return new Carves.Runtime.Domain.AI.LlmRequestEnvelopeSegmentDraft
        {
            SegmentId = segmentId,
            SegmentKind = segmentKind,
            SegmentParentId = parentId,
            SegmentOrder = order,
            MessageIndex = messageIndex,
            Role = role,
            PayloadPath = payloadPath,
            SerializationKind = serializationKind,
            Content = content,
            Included = true,
            Trimmed = false,
            SourceItemId = sourceItemId,
            RendererVersion = rendererVersion,
        };
    }
}

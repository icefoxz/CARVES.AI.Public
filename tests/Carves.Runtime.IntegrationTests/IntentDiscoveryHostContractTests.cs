using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Carves.Runtime.IntegrationTests;

public sealed class IntentDiscoveryHostContractTests
{
    [Fact]
    public void IntentDraft_DefaultsToPreviewOnlyWithoutPersistingRuntimeDraftTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var acceptedIntentPath = Path.Combine(sandbox.RootPath, ".ai", "memory", "PROJECT.md");
        var draftPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "intent_draft.json");
        if (File.Exists(acceptedIntentPath))
        {
            File.Delete(acceptedIntentPath);
        }

        if (File.Exists(draftPath))
        {
            File.Delete(draftPath);
        }

        var draft = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "draft");
        var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "status");

        Assert.Equal(0, draft.ExitCode);
        Assert.Equal(0, status.ExitCode);
        Assert.False(File.Exists(draftPath));
        Assert.False(File.Exists(acceptedIntentPath));

        using var draftDocument = JsonDocument.Parse(draft.StandardOutput);
        var draftRoot = draftDocument.RootElement;
        Assert.Equal("intent_preview", draftRoot.GetProperty("kind").GetString());
        Assert.True(draftRoot.GetProperty("preview_only").GetBoolean());
        Assert.False(draftRoot.GetProperty("mutated").GetBoolean());
        Assert.True(draftRoot.GetProperty("legacy_stateful_behavior_blocked").GetBoolean());
        Assert.True(draftRoot.GetProperty("persist_required_for_durable_draft").GetBoolean());
        Assert.Equal("carves intent draft --persist", draftRoot.GetProperty("persist_command").GetString());
        Assert.Equal("non_durable_candidate", draftRoot.GetProperty("preview_state").GetString());
        Assert.False(draftRoot.TryGetProperty("state", out _));
        Assert.False(draftRoot.TryGetProperty("draft", out _));
        var previewNode = draftRoot.GetProperty("preview");
        Assert.Equal("needs_confirmation", previewNode.GetProperty("planning_posture").GetString());
        Assert.False(string.IsNullOrWhiteSpace(previewNode.GetProperty("purpose").GetString()));

        using var statusDocument = JsonDocument.Parse(status.StandardOutput);
        var statusRoot = statusDocument.RootElement;
        Assert.Equal("missing", statusRoot.GetProperty("state").GetString());
        Assert.True(statusRoot.GetProperty("draft").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public void DiscussBriefPreview_ProjectsExplicitNonDurablePreviewSurface()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var acceptedIntentPath = Path.Combine(sandbox.RootPath, ".ai", "memory", "PROJECT.md");
        var draftPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "intent_draft.json");
        if (File.Exists(acceptedIntentPath))
        {
            File.Delete(acceptedIntentPath);
        }

        if (File.Exists(draftPath))
        {
            File.Delete(draftPath);
        }

        var preview = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "discuss", "brief-preview");

        Assert.Equal(0, preview.ExitCode);
        Assert.False(File.Exists(draftPath));
        Assert.False(File.Exists(acceptedIntentPath));

        using var previewDocument = JsonDocument.Parse(preview.StandardOutput);
        var previewRoot = previewDocument.RootElement;
        Assert.Equal("project_brief_preview", previewRoot.GetProperty("kind").GetString());
        Assert.True(previewRoot.GetProperty("preview_only").GetBoolean());
        Assert.False(previewRoot.GetProperty("mutated").GetBoolean());
        Assert.Equal("carves discuss context", previewRoot.GetProperty("continue_discussion_command").GetString());
        Assert.Equal("carves intent draft --persist", previewRoot.GetProperty("persist_command").GetString());
        var briefPreview = previewRoot.GetProperty("brief_preview");
        Assert.Equal("needs_confirmation", briefPreview.GetProperty("planning_posture").GetString());
        Assert.False(string.IsNullOrWhiteSpace(briefPreview.GetProperty("purpose").GetString()));
    }

    [Fact]
    public void IntentDraft_ProjectsStructuredGuidedPlanningSeedTruthOnExistingLane()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var acceptedIntentPath = Path.Combine(sandbox.RootPath, ".ai", "memory", "PROJECT.md");
        var draftPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "intent_draft.json");
        if (File.Exists(acceptedIntentPath))
        {
            File.Delete(acceptedIntentPath);
        }

        if (File.Exists(draftPath))
        {
            File.Delete(draftPath);
        }

        var draft = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "draft", "--persist");
        var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "status");
        var agent = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "agent", "query", "intent");

        Assert.Equal(0, draft.ExitCode);
        Assert.Equal(0, status.ExitCode);
        Assert.Equal(0, agent.ExitCode);
        Assert.True(File.Exists(draftPath));
        Assert.False(File.Exists(acceptedIntentPath));

        using var document = JsonDocument.Parse(status.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("intent_status", root.GetProperty("kind").GetString());
        Assert.Equal("drafted", root.GetProperty("state").GetString());
        var draftNode = root.GetProperty("draft");
        Assert.Equal("needs_confirmation", draftNode.GetProperty("planning_posture").GetString());
        Assert.True(draftNode.TryGetProperty("scope_frame", out var scopeFrame));
        Assert.False(string.IsNullOrWhiteSpace(scopeFrame.GetProperty("goal").GetString()));
        Assert.Contains(scopeFrame.GetProperty("not_now").EnumerateArray(), item => item.GetString() == "Automatic official card writeback from free-form chat.");
        Assert.Contains(draftNode.GetProperty("pending_decisions").EnumerateArray(), item =>
            item.GetProperty("decision_id").GetString() == "first_validation_artifact"
            && item.GetProperty("blocking_level").GetString() == "blocking_for_grounded_card");
        Assert.Contains(draftNode.GetProperty("candidate_cards").EnumerateArray(), item =>
            item.GetProperty("candidate_card_id").GetString() == "candidate-first-slice"
            && item.GetProperty("writeback_eligibility").GetString() == "requires_grounding_then_host_writeback");

        using var agentDocument = JsonDocument.Parse(agent.StandardOutput);
        var payload = agentDocument.RootElement.GetProperty("payload");
        Assert.Equal("intent_status", payload.GetProperty("kind").GetString());
        Assert.Equal("needs_confirmation", payload.GetProperty("draft").GetProperty("planning_posture").GetString());
        Assert.Contains(payload.GetProperty("draft").GetProperty("candidate_cards").EnumerateArray(), item =>
            item.GetProperty("candidate_card_id").GetString() == "candidate-intent-foundation");
    }

    [Fact]
    public void IntentDraft_AllowsBoundedMutationPathWithoutCrossingIntoOfficialCardTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var acceptedIntentPath = Path.Combine(sandbox.RootPath, ".ai", "memory", "PROJECT.md");
        var draftPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "intent_draft.json");
        if (File.Exists(acceptedIntentPath))
        {
            File.Delete(acceptedIntentPath);
        }

        if (File.Exists(draftPath))
        {
            File.Delete(draftPath);
        }

        var draft = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "draft", "--persist");
        var focus = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "focus", "candidate-first-slice");
        var resolveValidation = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "decision", "first_validation_artifact", "resolved");
        var resolveBoundary = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "decision", "first_slice_boundary", "resolved");
        var ready = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "candidate", "candidate-first-slice", "ready_to_plan");
        var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "status");
        var planStatus = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan", "status");

        Assert.Equal(0, draft.ExitCode);
        Assert.Equal(0, focus.ExitCode);
        Assert.Equal(0, resolveValidation.ExitCode);
        Assert.Equal(0, resolveBoundary.ExitCode);
        Assert.Equal(0, ready.ExitCode);
        Assert.Equal(0, status.ExitCode);
        Assert.Equal(0, planStatus.ExitCode);
        Assert.True(File.Exists(draftPath));
        Assert.False(File.Exists(acceptedIntentPath));

        using var document = JsonDocument.Parse(status.StandardOutput);
        var draftNode = document.RootElement.GetProperty("draft");
        Assert.Equal("ready_to_plan", draftNode.GetProperty("planning_posture").GetString());
        Assert.Equal("candidate-first-slice", draftNode.GetProperty("focus_card_id").GetString());
        Assert.Contains(draftNode.GetProperty("pending_decisions").EnumerateArray(), item =>
            item.GetProperty("decision_id").GetString() == "first_validation_artifact"
            && item.GetProperty("status").GetString() == "resolved");
        Assert.Contains(draftNode.GetProperty("candidate_cards").EnumerateArray(), item =>
            item.GetProperty("candidate_card_id").GetString() == "candidate-first-slice"
            && item.GetProperty("planning_posture").GetString() == "ready_to_plan");
        Assert.Contains("plan init", draftNode.GetProperty("recommended_next_action").GetString(), StringComparison.Ordinal);

        using var planStatusDocument = JsonDocument.Parse(planStatus.StandardOutput);
        var planStatusRoot = planStatusDocument.RootElement;
        Assert.Equal("plan_init_required", planStatusRoot.GetProperty("formal_planning_state").GetString());
        Assert.Equal("plan_init_required", planStatusRoot.GetProperty("formal_planning_entry_trigger_state").GetString());
        Assert.Equal("plan init [candidate-card-id]", planStatusRoot.GetProperty("formal_planning_entry_command").GetString());
        Assert.Contains("plan init [candidate-card-id]", planStatusRoot.GetProperty("formal_planning_entry_recommended_next_action").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PlanInit_ProjectsFormalPlanningCardAndPlanStatus()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var acceptedIntentPath = Path.Combine(sandbox.RootPath, ".ai", "memory", "PROJECT.md");
        var draftPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "intent_draft.json");
        if (File.Exists(acceptedIntentPath))
        {
            File.Delete(acceptedIntentPath);
        }

        if (File.Exists(draftPath))
        {
            File.Delete(draftPath);
        }

        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "draft", "--persist");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "focus", "candidate-first-slice");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "decision", "first_validation_artifact", "resolved");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "decision", "first_slice_boundary", "resolved");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "candidate", "candidate-first-slice", "ready_to_plan");

        var init = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan", "init", "candidate-first-slice");
        var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan", "status");
        var intentStatus = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "status");
        var secondInit = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan", "init", "candidate-first-slice");

        Assert.Equal(0, init.ExitCode);
        Assert.Equal(0, status.ExitCode);
        Assert.Equal(0, intentStatus.ExitCode);
        Assert.NotEqual(0, secondInit.ExitCode);
        Assert.Contains("already occupied", secondInit.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("opening another active planning card is rejected", secondInit.CombinedOutput, StringComparison.Ordinal);
        Assert.Contains("plan status", secondInit.CombinedOutput, StringComparison.Ordinal);

        using var statusDocument = JsonDocument.Parse(status.StandardOutput);
        var root = statusDocument.RootElement;
        Assert.Equal("formal_planning_status", root.GetProperty("kind").GetString());
        Assert.Equal("planning", root.GetProperty("formal_planning_state").GetString());
        Assert.Equal("formal_planning_packet_available", root.GetProperty("formal_planning_entry_trigger_state").GetString());
        Assert.Equal("plan status", root.GetProperty("formal_planning_entry_command").GetString());
        Assert.Contains("plan status", root.GetProperty("formal_planning_entry_recommended_next_action").GetString(), StringComparison.Ordinal);
        Assert.Equal("primary_formal_planning", root.GetProperty("active_planning_slot_id").GetString());
        Assert.Equal("occupied_by_packet", root.GetProperty("active_planning_slot_state").GetString());
        Assert.False(root.GetProperty("active_planning_slot_can_initialize").GetBoolean());
        Assert.Contains("opening another active planning card is rejected", root.GetProperty("active_planning_slot_conflict_reason").GetString(), StringComparison.Ordinal);
        Assert.Contains("plan status", root.GetProperty("active_planning_slot_remediation_action").GetString(), StringComparison.Ordinal);
        Assert.Equal("valid", root.GetProperty("planning_card_invariant_state").GetString());
        Assert.True(root.GetProperty("planning_card_invariant_can_export_governed_truth").GetBoolean());
        Assert.Equal(5, root.GetProperty("planning_card_invariant_block_count").GetInt32());
        Assert.Equal(0, root.GetProperty("planning_card_invariant_violation_count").GetInt32());
        Assert.Equal("ready_to_export", root.GetProperty("active_planning_card_fill_state").GetString());
        Assert.Equal("editable_fields_ready", root.GetProperty("active_planning_card_fill_completion_posture").GetString());
        Assert.True(root.GetProperty("active_planning_card_fill_ready_for_recommended_export").GetBoolean());
        Assert.Equal(0, root.GetProperty("active_planning_card_fill_missing_required_field_count").GetInt32());
        Assert.Contains("plan export-card", root.GetProperty("active_planning_card_fill_recommended_next_action").GetString(), StringComparison.Ordinal);
        Assert.Equal("candidate-first-slice", root.GetProperty("focus_card_id").GetString());
        var activePlanningCard = root.GetProperty("active_planning_card");
        Assert.Equal("primary_formal_planning", activePlanningCard.GetProperty("planning_slot_id").GetString());
        Assert.False(string.IsNullOrWhiteSpace(activePlanningCard.GetProperty("planning_card_id").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(activePlanningCard.GetProperty("locked_doctrine").GetProperty("digest").GetString()));
        Assert.Equal("valid", activePlanningCard.GetProperty("invariant_report").GetProperty("state").GetString());
        Assert.Equal("ready_to_export", activePlanningCard.GetProperty("fill_guidance").GetProperty("state").GetString());

        using var intentDocument = JsonDocument.Parse(intentStatus.StandardOutput);
        var formalPlanning = intentDocument.RootElement.GetProperty("draft").GetProperty("formal_planning");
        Assert.Equal("planning", formalPlanning.GetProperty("state").GetString());
        Assert.False(string.IsNullOrWhiteSpace(formalPlanning.GetProperty("active_planning_card_id").GetString()));
        Assert.Equal("valid", formalPlanning.GetProperty("planning_card_invariant_state").GetString());
        Assert.Equal("ready_to_export", formalPlanning.GetProperty("active_planning_card_fill_state").GetString());
    }

    [Fact]
    public void PlanStatus_ProjectsActivePlanningCardFillGuidanceWithoutTruthMutation()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var acceptedIntentPath = Path.Combine(sandbox.RootPath, ".ai", "memory", "PROJECT.md");
        var draftPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "intent_draft.json");
        var cardDraftRoot = Path.Combine(sandbox.RootPath, ".ai", "runtime", "planning", "card-drafts");
        if (File.Exists(acceptedIntentPath))
        {
            File.Delete(acceptedIntentPath);
        }

        if (File.Exists(draftPath))
        {
            File.Delete(draftPath);
        }

        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "draft", "--persist");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "focus", "candidate-first-slice");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "decision", "first_validation_artifact", "resolved");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "decision", "first_slice_boundary", "resolved");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "candidate", "candidate-first-slice", "ready_to_plan");
        var init = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan", "init", "candidate-first-slice");
        Assert.Equal(0, init.ExitCode);

        var cardDraftCountBefore = Directory.Exists(cardDraftRoot)
            ? Directory.EnumerateFiles(cardDraftRoot, "*.json", SearchOption.TopDirectoryOnly).Count()
            : 0;
        var draftJson = JsonNode.Parse(File.ReadAllText(draftPath))!.AsObject();
        draftJson["active_planning_card"]!["operator_intent"]!["acceptance_outline"] = new JsonArray();
        File.WriteAllText(
            draftPath,
            draftJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var planStatus = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan", "status");
        var runtimeStatus = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "status");
        var workbench = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "workbench", "overview");
        var cardDraftCountAfter = Directory.Exists(cardDraftRoot)
            ? Directory.EnumerateFiles(cardDraftRoot, "*.json", SearchOption.TopDirectoryOnly).Count()
            : 0;

        Assert.Equal(0, planStatus.ExitCode);
        Assert.Equal(0, runtimeStatus.ExitCode);
        Assert.Equal(0, workbench.ExitCode);
        Assert.Equal(cardDraftCountBefore, cardDraftCountAfter);

        using var planStatusDocument = JsonDocument.Parse(planStatus.StandardOutput);
        var root = planStatusDocument.RootElement;
        Assert.Equal("needs_fill", root.GetProperty("active_planning_card_fill_state").GetString());
        Assert.Equal("missing_required_editable_fields", root.GetProperty("active_planning_card_fill_completion_posture").GetString());
        Assert.False(root.GetProperty("active_planning_card_fill_ready_for_recommended_export").GetBoolean());
        Assert.Equal(1, root.GetProperty("active_planning_card_fill_missing_required_field_count").GetInt32());
        Assert.Equal("operator_intent.acceptance_outline", root.GetProperty("active_planning_card_fill_next_missing_field_path").GetString());
        Assert.Contains("acceptance criteria", root.GetProperty("active_planning_card_fill_recommended_next_action").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("needs_fill", root.GetProperty("active_planning_card").GetProperty("fill_guidance").GetProperty("state").GetString());
        Assert.Contains(
            root.GetProperty("active_planning_card_fill_missing_field_paths").EnumerateArray(),
            item => item.GetString() == "operator_intent.acceptance_outline");

        Assert.Contains("Active planning card fill state: needs_fill", runtimeStatus.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Active planning card fill missing required fields: 1", runtimeStatus.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Active planning card fill next action: Fill `operator_intent.acceptance_outline`", runtimeStatus.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Active planning card fill state: needs_fill", workbench.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Active planning card fill next missing field: operator_intent.acceptance_outline", workbench.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanStatus_ProjectsPlanningCardInvariantDriftRemediation()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var acceptedIntentPath = Path.Combine(sandbox.RootPath, ".ai", "memory", "PROJECT.md");
        var draftPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "intent_draft.json");
        if (File.Exists(acceptedIntentPath))
        {
            File.Delete(acceptedIntentPath);
        }

        if (File.Exists(draftPath))
        {
            File.Delete(draftPath);
        }

        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "draft", "--persist");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "focus", "candidate-first-slice");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "decision", "first_validation_artifact", "resolved");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "decision", "first_slice_boundary", "resolved");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "candidate", "candidate-first-slice", "ready_to_plan");
        var init = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan", "init", "candidate-first-slice");
        Assert.Equal(0, init.ExitCode);

        var draftJson = JsonNode.Parse(File.ReadAllText(draftPath))!.AsObject();
        var literalLines = draftJson["active_planning_card"]!["locked_doctrine"]!["literal_lines"]!.AsArray();
        literalLines[0] = JsonValue.Create("tampered doctrine line");
        File.WriteAllText(
            draftPath,
            draftJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan", "status");
        var exportPath = Path.Combine(sandbox.RootPath, "drafts", "drifted-plan-card.json");
        var export = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan", "export-card", exportPath);

        Assert.Equal(0, status.ExitCode);
        Assert.NotEqual(0, export.ExitCode);
        Assert.Contains("locked doctrine drifted", export.CombinedOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("plan init candidate-first-slice", export.CombinedOutput, StringComparison.Ordinal);

        using var statusDocument = JsonDocument.Parse(status.StandardOutput);
        var root = statusDocument.RootElement;
        Assert.Equal("drifted", root.GetProperty("planning_card_invariant_state").GetString());
        Assert.False(root.GetProperty("planning_card_invariant_can_export_governed_truth").GetBoolean());
        Assert.True(root.GetProperty("planning_card_invariant_violation_count").GetInt32() > 0);
        Assert.Contains("plan init candidate-first-slice", root.GetProperty("planning_card_invariant_remediation_action").GetString(), StringComparison.Ordinal);
        var invariantReport = root.GetProperty("planning_card_invariant_report");
        Assert.Equal("drifted", invariantReport.GetProperty("state").GetString());
        Assert.True(invariantReport.GetProperty("violations").GetArrayLength() > 0);
        Assert.Equal("drifted", root.GetProperty("active_planning_card").GetProperty("invariant_report").GetProperty("state").GetString());
    }

    [Fact]
    public void PlanPacket_ProjectsBriefingReplanRulesAndPlanHandle()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var acceptedIntentPath = Path.Combine(sandbox.RootPath, ".ai", "memory", "PROJECT.md");
        var draftPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "intent_draft.json");
        if (File.Exists(acceptedIntentPath))
        {
            File.Delete(acceptedIntentPath);
        }

        if (File.Exists(draftPath))
        {
            File.Delete(draftPath);
        }

        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "draft", "--persist");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "focus", "candidate-first-slice");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "decision", "first_validation_artifact", "resolved");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "decision", "first_slice_boundary", "resolved");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "candidate", "candidate-first-slice", "ready_to_plan");

        var init = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan", "init");
        var packet = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan", "packet");
        var agent = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "agent", "query", "plan_packet");

        Assert.Equal(0, init.ExitCode);
        Assert.Equal(0, packet.ExitCode);
        Assert.Equal(0, agent.ExitCode);

        using var packetDocument = JsonDocument.Parse(packet.StandardOutput);
        var packetRoot = packetDocument.RootElement;
        Assert.Equal("formal_planning_packet", packetRoot.GetProperty("kind").GetString());
        Assert.StartsWith("plan-primary_formal_planning-", packetRoot.GetProperty("plan_handle").GetString(), StringComparison.Ordinal);
        Assert.Equal("planning", packetRoot.GetProperty("formal_planning_state").GetString());
        Assert.Equal("plan_export_required", packetRoot.GetProperty("briefing").GetProperty("next_action_posture").GetString());
        Assert.Equal("not_bound_yet", packetRoot.GetProperty("acceptance_contract_summary").GetProperty("binding_state").GetString());
        Assert.Equal(5, packetRoot.GetProperty("replan_rules").GetArrayLength());
        Assert.Empty(packetRoot.GetProperty("linked_truth").GetProperty("card_draft_ids").EnumerateArray());

        using var agentDocument = JsonDocument.Parse(agent.StandardOutput);
        Assert.Equal("formal_planning_packet", agentDocument.RootElement.GetProperty("payload").GetProperty("kind").GetString());
    }

    [Fact]
    public void PlanExport_AndCreateCardDraft_ProjectPlanningContextIntoCardInspect()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var acceptedIntentPath = Path.Combine(sandbox.RootPath, ".ai", "memory", "PROJECT.md");
        var draftPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "intent_draft.json");
        if (File.Exists(acceptedIntentPath))
        {
            File.Delete(acceptedIntentPath);
        }

        if (File.Exists(draftPath))
        {
            File.Delete(draftPath);
        }

        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "draft", "--persist");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "focus", "candidate-first-slice");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "decision", "first_validation_artifact", "resolved");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "decision", "first_slice_boundary", "resolved");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "candidate", "candidate-first-slice", "ready_to_plan");
        var init = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan", "init");

        var exportPath = Path.Combine(sandbox.RootPath, "drafts", "plan-card.json");
        var export = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan", "export-card", exportPath);
        var create = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "create-card-draft", exportPath);

        Assert.Equal(0, init.ExitCode);
        Assert.Equal(0, export.ExitCode);
        Assert.Equal(0, create.ExitCode);
        Assert.True(File.Exists(exportPath));

        using var exportDocument = JsonDocument.Parse(export.StandardOutput);
        var planningCardId = exportDocument.RootElement.GetProperty("planning_card_id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(planningCardId));
        Assert.Contains("Created card draft", create.StandardOutput, StringComparison.Ordinal);

        var match = Regex.Match(create.StandardOutput, @"Created card draft (?<card_id>CARD-[A-Za-z0-9-]+)\.", RegexOptions.CultureInvariant);
        Assert.True(match.Success, create.StandardOutput);
        var cardId = match.Groups["card_id"].Value;
        var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "card", "inspect", cardId);
        Assert.Equal(0, inspect.ExitCode);

        using var inspectDocument = JsonDocument.Parse(inspect.StandardOutput);
        var planningContext = inspectDocument.RootElement.GetProperty("planning_context");
        Assert.Equal("primary_formal_planning", planningContext.GetProperty("planning_slot_id").GetString());
        Assert.Equal(planningCardId, planningContext.GetProperty("active_planning_card_id").GetString());
    }

    [Fact]
    public void PlanExportPacket_AndCreateCardDraft_ProjectPlanHandleAndLinkedTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "draft", "--persist");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "focus", "candidate-first-slice");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "decision", "first_validation_artifact", "resolved");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "decision", "first_slice_boundary", "resolved");
        ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "candidate", "candidate-first-slice", "ready_to_plan");

        var init = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan", "init");
        var cardExportPath = Path.Combine(sandbox.RootPath, "drafts", "plan-card.json");
        var packetExportPath = Path.Combine(sandbox.RootPath, "drafts", "plan-packet.json");
        var exportCard = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan", "export-card", cardExportPath);
        var createCard = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "create-card-draft", cardExportPath);
        var exportPacket = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan", "export-packet", packetExportPath);
        var packet = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan", "packet");

        Assert.Equal(0, init.ExitCode);
        Assert.Equal(0, exportCard.ExitCode);
        Assert.Equal(0, createCard.ExitCode);
        Assert.Equal(0, exportPacket.ExitCode);
        Assert.Equal(0, packet.ExitCode);
        Assert.True(File.Exists(packetExportPath));

        var match = Regex.Match(createCard.StandardOutput, @"Created card draft (?<card_id>CARD-[A-Za-z0-9-]+)\.", RegexOptions.CultureInvariant);
        Assert.True(match.Success, createCard.StandardOutput);
        var cardId = match.Groups["card_id"].Value;

        using var packetDocument = JsonDocument.Parse(packet.StandardOutput);
        var packetRoot = packetDocument.RootElement;
        var planHandle = packetRoot.GetProperty("plan_handle").GetString();
        Assert.Equal("plan_bound", packetRoot.GetProperty("formal_planning_state").GetString());
        Assert.Contains(packetRoot.GetProperty("linked_truth").GetProperty("card_draft_ids").EnumerateArray(), item => item.GetString() == cardId);

        using var exportedPacketDocument = JsonDocument.Parse(File.ReadAllText(packetExportPath));
        Assert.Equal(planHandle, exportedPacketDocument.RootElement.GetProperty("plan_handle").GetString());

        var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "card", "inspect", cardId);
        Assert.Equal(0, inspect.ExitCode);
        using var inspectDocument = JsonDocument.Parse(inspect.StandardOutput);
        Assert.Equal(planHandle, inspectDocument.RootElement.GetProperty("planning_context").GetProperty("plan_handle").GetString());
    }
}

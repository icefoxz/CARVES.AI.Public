using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Platform;

public sealed class ExecutionContractSurfaceService
{
    public ExecutionContractSurfaceSnapshot Build()
    {
        return new ExecutionContractSurfaceSnapshot
        {
            Summary = "CARVES execution contract surface defining packet, result submission, and planner verdict truth with live packet compilation now issued from task, memory, and codegraph state.",
            Contracts =
            [
                new ExecutionContractDescriptor
                {
                    ContractId = "execution_packet",
                    DisplayName = "ExecutionPacket",
                    Availability = ExecutionContractAvailability.Defined,
                    SchemaPath = "docs/contracts/execution-packet.schema.json",
                    Summary = "Canonical planner-issued execution contract defining scope, non-goals, permissions, budgets, validation order, evidence surfaces, and stop conditions.",
                    CurrentTruthLineage =
                    [
                        "ExecutionPacket",
                        "ExecutionPacketCompilerService",
                        "ContextPackService",
                        "CodexToolSurface.get_execution_packet",
                        "inspect execution-packet",
                    ],
                    Notes =
                    [
                        "CARD-300 defines the contract shape.",
                        "CARD-301 compiles live ExecutionPacket truth from task, memory, and codegraph state.",
                    ],
                },
                new ExecutionContractDescriptor
                {
                    ContractId = "task_result_envelope",
                    DisplayName = "TaskResultEnvelope",
                    Availability = ExecutionContractAvailability.Defined,
                    SchemaPath = "docs/contracts/task-result-envelope.schema.json",
                    Summary = "Canonical worker submission envelope that terminates at submit_result and references existing result, worker, provider, and safety truth.",
                    CurrentTruthLineage =
                    [
                        "TaskResultEnvelope",
                        "WorkerExecutionResult",
                        "ResultEnvelope",
                        "ResultIngestionService",
                    ],
                    Notes =
                    [
                        "Worker responsibility ends at submit_result.",
                        "review_task and sync_state remain planner-only lifecycle actions.",
                        "Packet enforcement now rejects planner-only lifecycle claims before writeback.",
                    ],
                },
                new ExecutionContractDescriptor
                {
                    ContractId = "planner_verdict",
                    DisplayName = "PlannerVerdict",
                    Availability = ExecutionContractAvailability.Defined,
                    SchemaPath = "docs/contracts/planner-verdict.schema.json",
                    Summary = "Canonical planner verdict semantics that make review, replan, failure, quarantine, human-review, and completion outcomes explicit.",
                    CurrentTruthLineage =
                    [
                        "PlannerReview",
                        "PlannerVerdictContractCatalog",
                        "BoundaryDecision",
                        "TaskTransitionPolicy",
                    ],
                    Notes =
                    [
                        "The legacy PlannerVerdict enum remains compatible.",
                        "Quarantined is defined as an explicit contract outcome even though legacy runtime truth still reaches it through boundary decisions.",
                    ],
                },
                new ExecutionContractDescriptor
                {
                    ContractId = "pack_artifact",
                    DisplayName = "PackArtifact",
                    Availability = ExecutionContractAvailability.Defined,
                    SchemaPath = "docs/contracts/pack-artifact.schema.json",
                    Summary = "Canonical runtime-import pack artifact contract that defines signed pack identity, compatibility, execution profile presets, and publication provenance.",
                    CurrentTruthLineage =
                    [
                        "PackArtifact",
                        "SpecificationValidationService.ValidatePackArtifact",
                        "RuntimePackAdmissionService.Admit",
                        "validate pack-artifact",
                        "runtime admit-pack",
                    ],
                    Notes =
                    [
                        "CARD-323 adds runtime-side validation/import enforcement for pack artifacts.",
                        "CARD-324 adds bounded runtime-local admission evidence for validated pack imports.",
                        "CARD-333 adds an inspectable runtime-local admission policy line that constrains channel, pack type, signature, and provenance before local admission succeeds.",
                        "This contract is validation/import only and does not imply publication tooling or registry rollout.",
                    ],
                },
                new ExecutionContractDescriptor
                {
                    ContractId = "runtime_pack_v1_manifest",
                    DisplayName = "RuntimePackV1Manifest",
                    Availability = ExecutionContractAvailability.Defined,
                    SchemaPath = "docs/contracts/runtime-pack-v1.schema.json",
                    Summary = "Canonical declarative Pack v1 manifest contract for project-understanding recipes, verification recipes, and review rubrics under existing Runtime governance, with bounded manifest-to-admission bridging into existing Runtime-local pack truth.",
                    CurrentTruthLineage =
                    [
                        "RuntimePackV1Manifest",
                        "SpecificationValidationService.ValidateRuntimePackV1",
                        "RuntimePackV1ManifestAdmissionBridgeService.Admit",
                        "validate runtime-pack-v1",
                        "runtime admit-pack-v1",
                        "pack admit",
                        "docs/product/runtime-pack-v1-product-spec.md",
                    ],
                    Notes =
                    [
                        "Pack v1 is a declarative capability input, not Runtime admission truth.",
                        "Bridge admission compiles a bounded runtime pack artifact and attribution pair before delegating to existing runtime admit-pack truth.",
                        "The contract remains bounded to project-understanding recipes, verification recipes, and review rubrics.",
                        "Pack v1 does not open registry, rollout, worker adapters, tool adapters, or truth mutation authority.",
                    ],
                },
                new ExecutionContractDescriptor
                {
                    ContractId = "runtime_pack_policy_package",
                    DisplayName = "RuntimePackPolicyPackage",
                    Availability = ExecutionContractAvailability.Defined,
                    SchemaPath = "docs/contracts/runtime-pack-policy-package.schema.json",
                    Summary = "Canonical local-runtime policy transfer contract defining admission and switch policy payloads that can be validated, previewed, exported, and imported without opening registry or rollout lines.",
                    CurrentTruthLineage =
                    [
                        "RuntimePackPolicyPackage",
                        "RuntimePackPolicyPackageValidationService.Validate",
                        "RuntimePackPolicyTransferService.Import",
                        "RuntimePackPolicyPreviewService.Preview",
                        "validate runtime-pack-policy-package",
                        "runtime preview-pack-policy",
                    ],
                    Notes =
                    [
                        "CARD-335 adds bounded local policy export and import truth.",
                        "CARD-336 adds append-only local policy audit evidence for export, import, pin, and clear operations.",
                        "CARD-337 adds bounded local preview and diff truth over incoming policy packages before import.",
                        "CARD-338 adds explicit validation for local policy packages before preview or import.",
                        "The contract is local-runtime scoped and does not imply registry, rollout, remote sync, or automatic apply.",
                    ],
                },
                new ExecutionContractDescriptor
                {
                    ContractId = "runtime_pack_attribution",
                    DisplayName = "RuntimePackAttribution",
                    Availability = ExecutionContractAvailability.Defined,
                    SchemaPath = "docs/contracts/runtime-pack-attribution.schema.json",
                    Summary = "Canonical runtime attribution contract recording which pack and execution profile selection a runtime imported or resolved.",
                    CurrentTruthLineage =
                    [
                        "RuntimePackAttribution",
                        "SpecificationValidationService.ValidateRuntimePackAttribution",
                        "RuntimePackAdmissionService.Admit",
                        "validate runtime-pack-attribution",
                        "inspect runtime-pack-admission",
                    ],
                    Notes =
                    [
                        "CARD-323 adds runtime-side validation/import enforcement for runtime pack attribution artifacts.",
                        "CARD-324 binds attribution to bounded local admission evidence after compatibility checks pass.",
                        "CARD-325 separates current local selection from admitted attribution so the active pack line is explicit local truth.",
                        "CARD-326 propagates the selected pack into execution runs and run reports by bounded reference.",
                        "CARD-327 adds append-only local selection history and bounded rollback audit over the current local selection line.",
                        "CARD-331 adds a task-scoped explainability surface that compares one task's recent run/report evidence against the current selected pack.",
                        "CARD-332 adds a bounded local pin policy that constrains divergent assign and rollback actions until the pin is cleared.",
                        "CARD-329 adds explicit local audit evidence for pack switch and rollback decisions over the same bounded local selection line.",
                        "CARD-330 adds a dedicated runtime-pack-execution-audit query surface over pack-attributed execution runs and run reports.",
                        "CARD-334 adds a bounded mismatch diagnostics surface that explains when admission, selection, pin policy, and recent execution evidence diverge.",
                        "CARD-335 adds a bounded local export/import line for current admission policy and switch policy truth.",
                        "CARD-336 adds append-only local policy audit evidence over export, import, pin, and clear operations.",
                        "CARD-337 adds a bounded local preview and diff line over incoming local policy packages before import.",
                        "CARD-338 adds explicit local policy package validation before preview or import.",
                        "The contract records import attribution only; assignment services and rollout orchestration remain out of scope.",
                    ],
                },
            ],
            PlannerVerdicts = PlannerVerdictContractCatalog.All.ToArray(),
        };
    }
}

using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public static class RuntimeSessionGatewayOperatorProofContractCatalog
{
    public static SessionGatewayOperatorProofContractSurface BuildPrivateAlphaContract()
    {
        return new SessionGatewayOperatorProofContractSurface
        {
            CurrentProofSource = SessionGatewayProofSources.RepoLocalProof,
            CurrentOperatorState = SessionGatewayOperatorWaitStates.WaitingOperatorSetup,
            OperatorActionRequired = true,
            RealWorldProofMissing = true,
            BlockingSummary = "Runtime-owned Session Gateway truth is deliverable-ready, but real-world completion is blocked until an operator performs setup, run, evidence capture, and verdict on a real project.",
            SupportedProofSources =
            [
                SessionGatewayProofSources.SyntheticFixture,
                SessionGatewayProofSources.RepoLocalProof,
                SessionGatewayProofSources.OperatorRunProof,
                SessionGatewayProofSources.ExternalUserProof,
            ],
            BlockingEventKinds =
            [
                SessionGatewayOperatorContractEvents.OperatorActionRequired,
                SessionGatewayOperatorContractEvents.OperatorProjectRequired,
                SessionGatewayOperatorContractEvents.OperatorEvidenceRequired,
                SessionGatewayOperatorContractEvents.RealWorldProofMissing,
            ],
            SharedRequiredEvidence =
            [
                "real_repo_path",
                "startup_or_run_command",
                "runtime_or_app_log_excerpt",
                "result_or_failure_artifact",
                "operator_verdict_summary",
            ],
            StageExitContracts =
            [
                new SessionGatewayStageExitContractSurface
                {
                    StageId = "setup",
                    BlockingState = SessionGatewayOperatorWaitStates.WaitingOperatorSetup,
                    RequiredEventKinds =
                    [
                        SessionGatewayOperatorContractEvents.OperatorActionRequired,
                        SessionGatewayOperatorContractEvents.OperatorProjectRequired,
                        SessionGatewayOperatorContractEvents.RealWorldProofMissing,
                    ],
                    AcceptedProofSources =
                    [
                        SessionGatewayProofSources.OperatorRunProof,
                        SessionGatewayProofSources.ExternalUserProof,
                    ],
                    OperatorMustDo =
                    [
                        "Select or create a real project repository for the scenario.",
                        "Record the real repo path and the exact startup command.",
                        "Confirm the gateway lane is pointed at the real project instead of a synthetic fixture.",
                    ],
                    AiMayDo =
                    [
                        "Explain the required repo shape and startup sequence.",
                        "Project current Runtime readiness and pinned commands.",
                        "Draft operator-facing steps without claiming setup is complete.",
                    ],
                    RequiredEvidence =
                    [
                        "repo_path",
                        "startup_command",
                        "initial_startup_log",
                    ],
                    NonPassingEvidence =
                    [
                        "synthetic_fixture_only",
                        "repo_local_mock_repo",
                        "agent_assertion_without_real_repo",
                    ],
                    MissingProofSummary = "Without a real project declaration and startup evidence, Runtime must stay in WAITING_OPERATOR_SETUP.",
                },
                new SessionGatewayStageExitContractSurface
                {
                    StageId = "run",
                    BlockingState = SessionGatewayOperatorWaitStates.WaitingOperatorRun,
                    RequiredEventKinds =
                    [
                        SessionGatewayOperatorContractEvents.OperatorActionRequired,
                        SessionGatewayOperatorContractEvents.RealWorldProofMissing,
                    ],
                    AcceptedProofSources =
                    [
                        SessionGatewayProofSources.OperatorRunProof,
                        SessionGatewayProofSources.ExternalUserProof,
                    ],
                    OperatorMustDo =
                    [
                        "Run the bounded scenario on the declared real project.",
                        "Capture the exact command path that produced the behavior under test.",
                    ],
                    AiMayDo =
                    [
                        "Explain what bounded run to execute next.",
                        "Interpret returned Runtime-owned state after the operator runs the scenario.",
                    ],
                    RequiredEvidence =
                    [
                        "run_command",
                        "run_log_excerpt",
                        "result_or_failure_artifact",
                    ],
                    NonPassingEvidence =
                    [
                        "repo_local_unit_test_only",
                        "synthetic_smoke_without_real_project",
                    ],
                    MissingProofSummary = "Without a real bounded run, the lane cannot claim operator_run_proof.",
                },
                new SessionGatewayStageExitContractSurface
                {
                    StageId = "evidence",
                    BlockingState = SessionGatewayOperatorWaitStates.WaitingOperatorEvidence,
                    RequiredEventKinds =
                    [
                        SessionGatewayOperatorContractEvents.OperatorEvidenceRequired,
                        SessionGatewayOperatorContractEvents.RealWorldProofMissing,
                    ],
                    AcceptedProofSources =
                    [
                        SessionGatewayProofSources.OperatorRunProof,
                        SessionGatewayProofSources.ExternalUserProof,
                    ],
                    OperatorMustDo =
                    [
                        "Attach logs, result files, and operation identifiers needed to audit the run.",
                        "State whether the observed outcome passed, failed, or blocked in reality.",
                    ],
                    AiMayDo =
                    [
                        "List the evidence bundle that Runtime expects.",
                        "Summarize gaps between provided evidence and the exit contract.",
                    ],
                    RequiredEvidence =
                    [
                        "session_id",
                        "operation_id",
                        "event_stream_capture",
                        "runtime_or_app_log_excerpt",
                        "result_or_failure_artifact",
                    ],
                    NonPassingEvidence =
                    [
                        "verbal_claim_without_artifacts",
                        "screenshots_without_runtime_identifiers",
                    ],
                    MissingProofSummary = "Without attached evidence, Runtime must stay in WAITING_OPERATOR_EVIDENCE and refuse to treat the run as real-world complete.",
                },
                new SessionGatewayStageExitContractSurface
                {
                    StageId = "verdict",
                    BlockingState = SessionGatewayOperatorWaitStates.WaitingOperatorVerdict,
                    RequiredEventKinds =
                    [
                        SessionGatewayOperatorContractEvents.OperatorActionRequired,
                    ],
                    AcceptedProofSources =
                    [
                        SessionGatewayProofSources.OperatorRunProof,
                        SessionGatewayProofSources.ExternalUserProof,
                    ],
                    OperatorMustDo =
                    [
                        "Provide the human verdict on whether the bounded scenario passed, failed, or needs follow-up.",
                        "Reject any attempt to silently treat repo-local proof as real-world closure.",
                    ],
                    AiMayDo =
                    [
                        "Summarize the recorded evidence and list remaining ambiguities.",
                        "Draft a replan or recovery suggestion without self-promoting the verdict to complete.",
                    ],
                    RequiredEvidence =
                    [
                        "operator_verdict_summary",
                        "pass_fail_or_blocked_decision",
                    ],
                    NonPassingEvidence =
                    [
                        "agent_self_certification",
                        "repo_local_green_tests_only",
                    ],
                    MissingProofSummary = "Without an explicit human verdict, Runtime must stay in WAITING_OPERATOR_VERDICT.",
                },
            ],
        };
    }
}

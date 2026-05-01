using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenPhase2ActiveCanaryReadinessReviewServiceTests
{
    [Fact]
    public void Persist_AcceptsReviewOnlyAndKeepsCanaryBlocked()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var candidate = new RuntimeTokenWrapperCandidateResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-wrapper-candidate-result-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-1/wrapper-candidate-result-2026-04-21.json",
            ReviewBundleMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-enter-active-canary-review-bundle-2026-04-21.md",
            ReviewBundleJsonArtifactPath = ".ai/runtime/token-optimization/phase-1/enter-active-canary-review-bundle-2026-04-21.json",
            ManifestMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-wrapper-policy-invariant-manifest-2026-04-21.md",
            ManifestJsonArtifactPath = ".ai/runtime/token-optimization/phase-1/wrapper-policy-invariant-manifest-2026-04-21.json",
            TrustLineClassification = "recomputed_trusted_for_phase_1_target_decision",
            Phase10Decision = "reprioritize_to_wrapper",
            Phase10NextTrack = "wrapper_policy_shadow_offline",
            CandidateSurfaceId = "worker:system:$.instructions",
            CandidateStrategy = "dedupe_then_request_kind_slice",
            ReductionRatioP95 = 0.288,
            InvariantCoveragePass = true,
            SemanticPreservationPass = true,
            SaliencePreservationPass = true,
            PriorityPreservationPass = true,
            SchemaValidityPass = true,
            MaterialReductionPass = true,
            EnterActiveCanaryReviewBundleReady = true,
            ActiveCanaryApprovalGranted = false,
            RuntimeShadowExecutionAllowed = false,
            MainRendererReplacementAllowed = false,
            ManualReviewQueue =
            [
                new RuntimeTokenWrapperCandidateManualReviewItem
                {
                    ReviewId = "review:1",
                    ManifestId = "manifest:worker:system:$.instructions",
                    InvariantId = "WRAP-WORKER-SCOPE-001",
                    ReviewStatus = "ready_for_operator_review_before_canary",
                    BlocksEnterActiveCanary = true,
                }
            ],
        };
        var reviewBundle = new RuntimeTokenWrapperEnterActiveCanaryReviewBundle
        {
            ResultDate = resultDate,
            CandidateSurfaceId = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            EnterActiveCanaryReviewBundleReady = true,
            ActiveCanaryApprovalGranted = false,
            RuntimeShadowExecutionAllowed = false,
            ReductionRatioP95 = 0.288,
            MaterialReductionPass = true,
            SchemaValidityPass = true,
            InvariantCoveragePass = true,
            SemanticPreservationPass = true,
            SaliencePreservationPass = true,
            PriorityPreservationPass = true,
            ManualReviewQueue = candidate.ManualReviewQueue,
        };
        var manifest = new RuntimeTokenWrapperPolicyInvariantManifestResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = candidate.ManifestMarkdownArtifactPath,
            JsonArtifactPath = candidate.ManifestJsonArtifactPath,
            Phase10Decision = candidate.Phase10Decision,
            Phase10NextTrack = candidate.Phase10NextTrack,
            SurfaceManifests =
            [
                new RuntimeTokenWrapperPolicyInvariantSurfaceManifest
                {
                    ManifestId = "manifest:worker:system:$.instructions",
                    InventoryId = "worker:system:$.instructions",
                    RequestKind = "worker",
                    ShareP95 = 0.316,
                    Invariants =
                    [
                        new RuntimeTokenWrapperPolicyInvariantItem
                        {
                            InvariantId = "WRAP-WORKER-SCOPE-001",
                            InvariantClass = "scope_boundary",
                            Title = "Governed worker scope boundary",
                        }
                    ],
                }
            ],
            RequestKindsCovered = ["worker"],
        };

        var result = RuntimeTokenPhase2ActiveCanaryReadinessReviewService.Persist(workspace.Paths, candidate, reviewBundle, manifest, null, null, null, null, resultDate);

        Assert.Equal("accepted_for_review_only", result.ReviewVerdict);
        Assert.True(result.EnterActiveCanaryReviewAccepted);
        Assert.False(result.ActiveCanaryApproved);
        Assert.False(result.RuntimeShadowExecutionAllowed);
        Assert.False(result.MainRendererReplacementAllowed);
        Assert.Equal(0.288, result.TargetSurfaceReductionRatioP95, 3);
        Assert.Equal(0.316, result.TargetSurfaceShareP95, 3);
        Assert.Equal(0.091008, result.ExpectedWholeRequestReductionP95, 6);
        Assert.Equal(1, result.PolicyInvariantCount);
        Assert.Equal(1, result.PolicyInvariantCoverageCount);
        Assert.Equal(1.0, result.PolicyInvariantCoverageRatio, 6);
        Assert.Equal(0, result.SemanticPreservationFailCount);
        Assert.Equal(0, result.SaliencePreservationFailCount);
        Assert.Equal(0, result.PriorityPreservationFailCount);
        Assert.Equal(1, result.NeedsManualReviewUnresolvedCount);
        Assert.Equal(0, result.RequestKindSliceRemovedPolicyCriticalCount);
        Assert.False(result.RequestKindSliceCrossKindProofAvailable);
        Assert.False(result.RuntimePathTouched);
        Assert.False(result.RetrievalOrEvidenceWritten);
        Assert.Contains("manual_review_unresolved", result.BlockingReasons);
        Assert.Contains("request_kind_slice_cross_kind_proof_not_available", result.BlockingReasons);
        Assert.Contains("rollback_plan_not_reviewed", result.BlockingReasons);
        Assert.Contains("non_inferiority_cohort_not_frozen", result.BlockingReasons);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void Persist_UsesManualReviewResolutionWhenAvailable()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var candidate = new RuntimeTokenWrapperCandidateResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-wrapper-candidate-result-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-1/wrapper-candidate-result-2026-04-21.json",
            ReviewBundleMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-enter-active-canary-review-bundle-2026-04-21.md",
            ReviewBundleJsonArtifactPath = ".ai/runtime/token-optimization/phase-1/enter-active-canary-review-bundle-2026-04-21.json",
            ManifestMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-wrapper-policy-invariant-manifest-2026-04-21.md",
            ManifestJsonArtifactPath = ".ai/runtime/token-optimization/phase-1/wrapper-policy-invariant-manifest-2026-04-21.json",
            TrustLineClassification = "recomputed_trusted_for_phase_1_target_decision",
            Phase10Decision = "reprioritize_to_wrapper",
            Phase10NextTrack = "wrapper_policy_shadow_offline",
            CandidateSurfaceId = "worker:system:$.instructions",
            CandidateStrategy = "dedupe_then_request_kind_slice",
            ReductionRatioP95 = 0.288,
            InvariantCoveragePass = true,
            SemanticPreservationPass = true,
            SaliencePreservationPass = true,
            PriorityPreservationPass = true,
            SchemaValidityPass = true,
            MaterialReductionPass = true,
            EnterActiveCanaryReviewBundleReady = true,
            ActiveCanaryApprovalGranted = false,
            RuntimeShadowExecutionAllowed = false,
            MainRendererReplacementAllowed = false,
        };
        var reviewBundle = new RuntimeTokenWrapperEnterActiveCanaryReviewBundle
        {
            ResultDate = resultDate,
            CandidateSurfaceId = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            EnterActiveCanaryReviewBundleReady = true,
            ActiveCanaryApprovalGranted = false,
            RuntimeShadowExecutionAllowed = false,
            ReductionRatioP95 = 0.288,
            MaterialReductionPass = true,
            SchemaValidityPass = true,
            InvariantCoveragePass = true,
            SemanticPreservationPass = true,
            SaliencePreservationPass = true,
            PriorityPreservationPass = true,
        };
        var manifest = new RuntimeTokenWrapperPolicyInvariantManifestResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = candidate.ManifestMarkdownArtifactPath,
            JsonArtifactPath = candidate.ManifestJsonArtifactPath,
            Phase10Decision = candidate.Phase10Decision,
            Phase10NextTrack = candidate.Phase10NextTrack,
            SurfaceManifests =
            [
                new RuntimeTokenWrapperPolicyInvariantSurfaceManifest
                {
                    ManifestId = "manifest:worker:system:$.instructions",
                    InventoryId = "worker:system:$.instructions",
                    RequestKind = "worker",
                    ShareP95 = 0.316,
                    Invariants =
                    [
                        new RuntimeTokenWrapperPolicyInvariantItem
                        {
                            InvariantId = "WRAP-WORKER-SCOPE-001",
                            InvariantClass = "scope_boundary",
                            Title = "Governed worker scope boundary",
                        }
                    ],
                }
            ],
            RequestKindsCovered = ["worker"],
        };
        var resolution = new RuntimeTokenPhase2ManualReviewResolutionResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-manual-review-resolution-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/manual-review-resolution-2026-04-21.json",
            TargetSurface = candidate.CandidateSurfaceId,
            ResolutionVerdict = "resolved_without_candidate_change",
            ResolvedReviewCount = 1,
            UnresolvedReviewCount = 0,
            FailCount = 0,
            CandidateChangeRequiredCount = 0,
            SemanticPreservationPassCount = 1,
            SemanticPreservationFailCount = 0,
            SaliencePreservationPassCount = 1,
            SaliencePreservationFailCount = 0,
            PriorityPreservationPassCount = 1,
            PriorityPreservationFailCount = 0,
            ApplicabilityPassCount = 1,
            ApplicabilityFailCount = 0,
        };

        var result = RuntimeTokenPhase2ActiveCanaryReadinessReviewService.Persist(workspace.Paths, candidate, reviewBundle, manifest, resolution, null, null, null, resultDate);

        Assert.Equal(0, result.NeedsManualReviewUnresolvedCount);
        Assert.DoesNotContain("manual_review_unresolved", result.BlockingReasons);
        Assert.Equal(resolution.MarkdownArtifactPath, result.ManualReviewResolutionMarkdownArtifactPath);
        Assert.Equal(resolution.JsonArtifactPath, result.ManualReviewResolutionJsonArtifactPath);
    }

    [Fact]
    public void Persist_UsesRequestKindSliceProofWhenAvailable()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var candidate = new RuntimeTokenWrapperCandidateResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-wrapper-candidate-result-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-1/wrapper-candidate-result-2026-04-21.json",
            ReviewBundleMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-enter-active-canary-review-bundle-2026-04-21.md",
            ReviewBundleJsonArtifactPath = ".ai/runtime/token-optimization/phase-1/enter-active-canary-review-bundle-2026-04-21.json",
            ManifestMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-wrapper-policy-invariant-manifest-2026-04-21.md",
            ManifestJsonArtifactPath = ".ai/runtime/token-optimization/phase-1/wrapper-policy-invariant-manifest-2026-04-21.json",
            TrustLineClassification = "recomputed_trusted_for_phase_1_target_decision",
            Phase10Decision = "reprioritize_to_wrapper",
            Phase10NextTrack = "wrapper_policy_shadow_offline",
            CandidateSurfaceId = "worker:system:$.instructions",
            CandidateStrategy = "dedupe_then_request_kind_slice",
            ReductionRatioP95 = 0.288,
            InvariantCoveragePass = true,
            SemanticPreservationPass = true,
            SaliencePreservationPass = true,
            PriorityPreservationPass = true,
            SchemaValidityPass = true,
            MaterialReductionPass = true,
            EnterActiveCanaryReviewBundleReady = true,
        };
        var reviewBundle = new RuntimeTokenWrapperEnterActiveCanaryReviewBundle
        {
            ResultDate = resultDate,
            CandidateSurfaceId = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            EnterActiveCanaryReviewBundleReady = true,
            ReductionRatioP95 = 0.288,
            MaterialReductionPass = true,
            SchemaValidityPass = true,
            InvariantCoveragePass = true,
            SemanticPreservationPass = true,
            SaliencePreservationPass = true,
            PriorityPreservationPass = true,
        };
        var manifest = new RuntimeTokenWrapperPolicyInvariantManifestResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = candidate.ManifestMarkdownArtifactPath,
            JsonArtifactPath = candidate.ManifestJsonArtifactPath,
            Phase10Decision = candidate.Phase10Decision,
            Phase10NextTrack = candidate.Phase10NextTrack,
            SurfaceManifests =
            [
                new RuntimeTokenWrapperPolicyInvariantSurfaceManifest
                {
                    ManifestId = "manifest:worker:system:$.instructions",
                    InventoryId = "worker:system:$.instructions",
                    RequestKind = "worker",
                    ShareP95 = 0.316,
                    Invariants =
                    [
                        new RuntimeTokenWrapperPolicyInvariantItem
                        {
                            InvariantId = "WRAP-WORKER-SCOPE-001",
                            InvariantClass = "scope_boundary",
                            Title = "Governed worker scope boundary",
                        }
                    ],
                }
            ],
            RequestKindsCovered = ["worker"],
        };
        var resolution = new RuntimeTokenPhase2ManualReviewResolutionResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-manual-review-resolution-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/manual-review-resolution-2026-04-21.json",
            TargetSurface = candidate.CandidateSurfaceId,
            ResolutionVerdict = "resolved_without_candidate_change",
            ResolvedReviewCount = 1,
            UnresolvedReviewCount = 0,
            FailCount = 0,
            SemanticPreservationPassCount = 1,
            SaliencePreservationPassCount = 1,
            PriorityPreservationPassCount = 1,
            ApplicabilityPassCount = 1,
            ReviewItems =
            [
                new RuntimeTokenPhase2ManualReviewResolutionItem
                {
                    ReviewItemId = "review:scope",
                    InvariantId = "WRAP-WORKER-SCOPE-001",
                    ReviewResult = "pass",
                }
            ],
        };
        var proof = new RuntimeTokenPhase2RequestKindSliceProofResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-wrapper-request-kind-slice-proof-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/wrapper-request-kind-slice-proof-2026-04-21.json",
            TargetSurface = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            CrossKindProofVerdict = "proof_available_for_worker_only_canary_scope",
            CrossKindProofAvailable = true,
            CanaryRequestKindAllowlist = ["worker"],
            PolicyCriticalFragmentCount = 1,
            PolicyCriticalFragmentRemovedCount = 0,
        };

        var result = RuntimeTokenPhase2ActiveCanaryReadinessReviewService.Persist(workspace.Paths, candidate, reviewBundle, manifest, resolution, proof, null, null, resultDate);

        Assert.True(result.RequestKindSliceCrossKindProofAvailable);
        Assert.Equal(0, result.RequestKindSliceRemovedPolicyCriticalCount);
        Assert.DoesNotContain("request_kind_slice_cross_kind_proof_not_available", result.BlockingReasons);
        Assert.Equal(proof.MarkdownArtifactPath, result.RequestKindSliceProofMarkdownArtifactPath);
        Assert.Equal(proof.JsonArtifactPath, result.RequestKindSliceProofJsonArtifactPath);
    }

    [Fact]
    public void Persist_UsesRollbackPlanWhenAvailable()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var candidate = new RuntimeTokenWrapperCandidateResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-wrapper-candidate-result-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-1/wrapper-candidate-result-2026-04-21.json",
            ReviewBundleMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-enter-active-canary-review-bundle-2026-04-21.md",
            ReviewBundleJsonArtifactPath = ".ai/runtime/token-optimization/phase-1/enter-active-canary-review-bundle-2026-04-21.json",
            ManifestMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-wrapper-policy-invariant-manifest-2026-04-21.md",
            ManifestJsonArtifactPath = ".ai/runtime/token-optimization/phase-1/wrapper-policy-invariant-manifest-2026-04-21.json",
            TrustLineClassification = "recomputed_trusted_for_phase_1_target_decision",
            Phase10Decision = "reprioritize_to_wrapper",
            Phase10NextTrack = "wrapper_policy_shadow_offline",
            CandidateSurfaceId = "worker:system:$.instructions",
            CandidateStrategy = "dedupe_then_request_kind_slice",
            ReductionRatioP95 = 0.288,
            InvariantCoveragePass = true,
            SemanticPreservationPass = true,
            SaliencePreservationPass = true,
            PriorityPreservationPass = true,
            SchemaValidityPass = true,
            MaterialReductionPass = true,
            EnterActiveCanaryReviewBundleReady = true,
        };
        var reviewBundle = new RuntimeTokenWrapperEnterActiveCanaryReviewBundle
        {
            ResultDate = resultDate,
            CandidateSurfaceId = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            EnterActiveCanaryReviewBundleReady = true,
            ReductionRatioP95 = 0.288,
            MaterialReductionPass = true,
            SchemaValidityPass = true,
            InvariantCoveragePass = true,
            SemanticPreservationPass = true,
            SaliencePreservationPass = true,
            PriorityPreservationPass = true,
        };
        var manifest = new RuntimeTokenWrapperPolicyInvariantManifestResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = candidate.ManifestMarkdownArtifactPath,
            JsonArtifactPath = candidate.ManifestJsonArtifactPath,
            Phase10Decision = candidate.Phase10Decision,
            Phase10NextTrack = candidate.Phase10NextTrack,
            SurfaceManifests =
            [
                new RuntimeTokenWrapperPolicyInvariantSurfaceManifest
                {
                    ManifestId = "manifest:worker:system:$.instructions",
                    InventoryId = "worker:system:$.instructions",
                    RequestKind = "worker",
                    ShareP95 = 0.316,
                    Invariants =
                    [
                        new RuntimeTokenWrapperPolicyInvariantItem
                        {
                            InvariantId = "WRAP-WORKER-SCOPE-001",
                            InvariantClass = "scope_boundary",
                            Title = "Governed worker scope boundary",
                        }
                    ],
                }
            ],
            RequestKindsCovered = ["worker"],
        };
        var resolution = new RuntimeTokenPhase2ManualReviewResolutionResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-manual-review-resolution-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/manual-review-resolution-2026-04-21.json",
            TargetSurface = candidate.CandidateSurfaceId,
            ResolutionVerdict = "resolved_without_candidate_change",
            ResolvedReviewCount = 1,
            UnresolvedReviewCount = 0,
            FailCount = 0,
            SemanticPreservationPassCount = 1,
            SaliencePreservationPassCount = 1,
            PriorityPreservationPassCount = 1,
            ApplicabilityPassCount = 1,
            ReviewItems =
            [
                new RuntimeTokenPhase2ManualReviewResolutionItem
                {
                    ReviewItemId = "review:scope",
                    InvariantId = "WRAP-WORKER-SCOPE-001",
                    ReviewResult = "pass",
                }
            ],
        };
        var proof = new RuntimeTokenPhase2RequestKindSliceProofResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-wrapper-request-kind-slice-proof-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/wrapper-request-kind-slice-proof-2026-04-21.json",
            TargetSurface = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            CrossKindProofVerdict = "proof_available_for_worker_only_canary_scope",
            CrossKindProofAvailable = true,
            CanaryRequestKindAllowlist = ["worker"],
            PolicyCriticalFragmentCount = 1,
            PolicyCriticalFragmentRemovedCount = 0,
        };
        var rollback = new RuntimeTokenPhase2RollbackPlanFreezeResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-wrapper-canary-rollback-plan-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/wrapper-canary-rollback-plan-2026-04-21.json",
            TargetSurface = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            CandidateVersion = "wrapper_candidate_20260421_worker_system___instructions",
            FallbackVersion = "original_worker_system_instructions",
            RollbackPlanReviewed = true,
            RollbackTestPlanDefined = true,
            DefaultEnabled = false,
            GlobalKillSwitch = true,
            PerRequestKindFallback = true,
            PerSurfaceFallback = true,
            CanaryRequestKindAllowlist = ["worker"],
        };

        var result = RuntimeTokenPhase2ActiveCanaryReadinessReviewService.Persist(workspace.Paths, candidate, reviewBundle, manifest, resolution, proof, rollback, null, resultDate);

        Assert.True(result.RollbackPlanReviewed);
        Assert.DoesNotContain("rollback_plan_not_reviewed", result.BlockingReasons);
        Assert.Equal(rollback.MarkdownArtifactPath, result.RollbackPlanMarkdownArtifactPath);
        Assert.Equal(rollback.JsonArtifactPath, result.RollbackPlanJsonArtifactPath);
    }

    [Fact]
    public void Persist_UsesNonInferiorityCohortWhenAvailableButKeepsCanaryBlocked()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var candidate = new RuntimeTokenWrapperCandidateResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-wrapper-candidate-result-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-1/wrapper-candidate-result-2026-04-21.json",
            ReviewBundleMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-enter-active-canary-review-bundle-2026-04-21.md",
            ReviewBundleJsonArtifactPath = ".ai/runtime/token-optimization/phase-1/enter-active-canary-review-bundle-2026-04-21.json",
            ManifestMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-1-wrapper-policy-invariant-manifest-2026-04-21.md",
            ManifestJsonArtifactPath = ".ai/runtime/token-optimization/phase-1/wrapper-policy-invariant-manifest-2026-04-21.json",
            TrustLineClassification = "recomputed_trusted_for_phase_1_target_decision",
            Phase10Decision = "reprioritize_to_wrapper",
            Phase10NextTrack = "wrapper_policy_shadow_offline",
            CandidateSurfaceId = "worker:system:$.instructions",
            CandidateStrategy = "dedupe_then_request_kind_slice",
            ReductionRatioP95 = 0.288,
            InvariantCoveragePass = true,
            SemanticPreservationPass = true,
            SaliencePreservationPass = true,
            PriorityPreservationPass = true,
            SchemaValidityPass = true,
            MaterialReductionPass = true,
            EnterActiveCanaryReviewBundleReady = true,
        };
        var reviewBundle = new RuntimeTokenWrapperEnterActiveCanaryReviewBundle
        {
            ResultDate = resultDate,
            CandidateSurfaceId = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            EnterActiveCanaryReviewBundleReady = true,
            ReductionRatioP95 = 0.288,
            MaterialReductionPass = true,
            SchemaValidityPass = true,
            InvariantCoveragePass = true,
            SemanticPreservationPass = true,
            SaliencePreservationPass = true,
            PriorityPreservationPass = true,
        };
        var manifest = new RuntimeTokenWrapperPolicyInvariantManifestResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = candidate.ManifestMarkdownArtifactPath,
            JsonArtifactPath = candidate.ManifestJsonArtifactPath,
            Phase10Decision = candidate.Phase10Decision,
            Phase10NextTrack = candidate.Phase10NextTrack,
            SurfaceManifests =
            [
                new RuntimeTokenWrapperPolicyInvariantSurfaceManifest
                {
                    ManifestId = "manifest:worker:system:$.instructions",
                    InventoryId = "worker:system:$.instructions",
                    RequestKind = "worker",
                    ShareP95 = 0.316,
                    Invariants =
                    [
                        new RuntimeTokenWrapperPolicyInvariantItem
                        {
                            InvariantId = "WRAP-WORKER-SCOPE-001",
                            InvariantClass = "scope_boundary",
                            Title = "Governed worker scope boundary",
                        }
                    ],
                }
            ],
            RequestKindsCovered = ["worker"],
        };
        var resolution = new RuntimeTokenPhase2ManualReviewResolutionResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-manual-review-resolution-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/manual-review-resolution-2026-04-21.json",
            TargetSurface = candidate.CandidateSurfaceId,
            ResolutionVerdict = "resolved_without_candidate_change",
            ResolvedReviewCount = 1,
            UnresolvedReviewCount = 0,
            FailCount = 0,
            SemanticPreservationPassCount = 1,
            SaliencePreservationPassCount = 1,
            PriorityPreservationPassCount = 1,
            ApplicabilityPassCount = 1,
            ReviewItems =
            [
                new RuntimeTokenPhase2ManualReviewResolutionItem
                {
                    ReviewItemId = "review:scope",
                    InvariantId = "WRAP-WORKER-SCOPE-001",
                    ReviewResult = "pass",
                }
            ],
        };
        var proof = new RuntimeTokenPhase2RequestKindSliceProofResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-wrapper-request-kind-slice-proof-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/wrapper-request-kind-slice-proof-2026-04-21.json",
            TargetSurface = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            CrossKindProofVerdict = "proof_available_for_worker_only_canary_scope",
            CrossKindProofAvailable = true,
            CanaryRequestKindAllowlist = ["worker"],
            PolicyCriticalFragmentCount = 1,
            PolicyCriticalFragmentRemovedCount = 0,
        };
        var rollback = new RuntimeTokenPhase2RollbackPlanFreezeResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-wrapper-canary-rollback-plan-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/wrapper-canary-rollback-plan-2026-04-21.json",
            TargetSurface = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            CandidateVersion = "wrapper_candidate_20260421_worker_system___instructions",
            FallbackVersion = "original_worker_system_instructions",
            RollbackPlanReviewed = true,
            RollbackTestPlanDefined = true,
            DefaultEnabled = false,
            GlobalKillSwitch = true,
            PerRequestKindFallback = true,
            PerSurfaceFallback = true,
            CanaryRequestKindAllowlist = ["worker"],
        };
        var cohort = new RuntimeTokenPhase2NonInferiorityCohortFreezeResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-non-inferiority-cohort-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/non-inferiority-cohort-2026-04-21.json",
            TargetSurface = candidate.CandidateSurfaceId,
            CandidateStrategy = candidate.CandidateStrategy,
            NonInferiorityCohortFrozen = true,
        };

        var result = RuntimeTokenPhase2ActiveCanaryReadinessReviewService.Persist(workspace.Paths, candidate, reviewBundle, manifest, resolution, proof, rollback, cohort, resultDate);

        Assert.True(result.NonInferiorityCohortFrozen);
        Assert.DoesNotContain("non_inferiority_cohort_not_frozen", result.BlockingReasons);
        Assert.Equal(cohort.MarkdownArtifactPath, result.NonInferiorityCohortMarkdownArtifactPath);
        Assert.Equal(cohort.JsonArtifactPath, result.NonInferiorityCohortJsonArtifactPath);
        Assert.False(result.ActiveCanaryApproved);
        Assert.False(result.RuntimeShadowExecutionAllowed);
        Assert.False(result.MainRendererReplacementAllowed);
    }

    [Fact]
    public void Persist_RejectsCandidateWithoutReviewBundle()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var candidate = new RuntimeTokenWrapperCandidateResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            CandidateSurfaceId = "worker:system:$.instructions",
            EnterActiveCanaryReviewBundleReady = true,
        };
        var reviewBundle = new RuntimeTokenWrapperEnterActiveCanaryReviewBundle
        {
            ResultDate = resultDate,
            CandidateSurfaceId = "worker:system:$.instructions",
            EnterActiveCanaryReviewBundleReady = false,
        };
        var manifest = new RuntimeTokenWrapperPolicyInvariantManifestResult
        {
            ResultDate = resultDate,
            CohortId = candidate.CohortId,
            SurfaceManifests =
            [
                new RuntimeTokenWrapperPolicyInvariantSurfaceManifest
                {
                    ManifestId = "manifest:worker:system:$.instructions",
                    InventoryId = "worker:system:$.instructions",
                }
            ],
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            RuntimeTokenPhase2ActiveCanaryReadinessReviewService.Persist(workspace.Paths, candidate, reviewBundle, manifest, null, null, null, null, resultDate));

        Assert.Contains("enter-active-canary review bundle", error.Message, StringComparison.Ordinal);
    }
}

using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeSessionGatewayInternalBetaExitContractService
{
    private readonly string repoRoot;
    private readonly Func<RuntimeSessionGatewayInternalBetaGateSurface> internalBetaGateFactory;
    private readonly Func<RuntimeFirstRunOperatorPacketSurface> firstRunPacketFactory;

    public RuntimeSessionGatewayInternalBetaExitContractService(
        string repoRoot,
        Func<RuntimeSessionGatewayInternalBetaGateSurface> internalBetaGateFactory,
        Func<RuntimeFirstRunOperatorPacketSurface> firstRunPacketFactory)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.internalBetaGateFactory = internalBetaGateFactory;
        this.firstRunPacketFactory = firstRunPacketFactory;
    }

    public RuntimeSessionGatewayInternalBetaExitContractSurface Build()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        const string exitContractPath = "docs/session-gateway/internal-beta-exit-contract.md";
        const string releaseSurfacePath = "docs/session-gateway/release-surface.md";
        const string internalBetaGatePath = "docs/session-gateway/internal-beta-gate.md";
        const string firstRunPacketPath = "docs/runtime/runtime-first-run-operator-packet.md";
        const string operatorProofContractPath = "docs/session-gateway/operator-proof-contract.md";
        const string alphaSetupPath = "docs/session-gateway/ALPHA_SETUP.md";
        const string alphaQuickstartPath = "docs/session-gateway/ALPHA_QUICKSTART.md";

        ValidateDocument(exitContractPath, "Internal beta exit contract", errors);
        ValidateDocument(releaseSurfacePath, "Release surface", errors);
        ValidateDocument(internalBetaGatePath, "Internal beta gate", errors);
        ValidateDocument(firstRunPacketPath, "First-run operator packet", errors);
        ValidateDocument(operatorProofContractPath, "Operator proof contract", errors);
        ValidateDocument(alphaSetupPath, "Alpha setup", errors);
        ValidateDocument(alphaQuickstartPath, "Alpha quickstart", errors);

        var gate = internalBetaGateFactory();
        var packet = firstRunPacketFactory();
        errors.AddRange(gate.Errors.Select(error => $"Internal beta gate surface: {error}"));
        warnings.AddRange(gate.Warnings.Select(warning => $"Internal beta gate surface: {warning}"));
        errors.AddRange(packet.Errors.Select(error => $"First-run packet surface: {error}"));
        warnings.AddRange(packet.Warnings.Select(warning => $"First-run packet surface: {warning}"));

        var samples = BuildSamples(errors);
        var representativeCount = samples.Count(sample => sample.CountsAsRepresentativeEvidence);
        if (representativeCount < 2)
        {
            errors.Add($"Representative evidence count is {representativeCount}, not the required minimum of 2.");
        }

        var contractReady =
            gate.IsValid
            && packet.IsValid
            && string.Equals(gate.OverallPosture, "internal_beta_gated_ready", StringComparison.Ordinal)
            && string.Equals(packet.OverallPosture, "first_run_packet_ready", StringComparison.Ordinal)
            && representativeCount >= 2
            && errors.Count == 0;

        if (!string.Equals(gate.OverallPosture, "internal_beta_gated_ready", StringComparison.Ordinal))
        {
            warnings.Add($"Internal beta gate posture is {gate.OverallPosture}, not internal_beta_gated_ready.");
        }

        if (!string.Equals(packet.OverallPosture, "first_run_packet_ready", StringComparison.Ordinal))
        {
            warnings.Add($"First-run operator packet posture is {packet.OverallPosture}, not first_run_packet_ready.");
        }

        return new RuntimeSessionGatewayInternalBetaExitContractSurface
        {
            ExitContractPath = exitContractPath,
            ReleaseSurfacePath = releaseSurfacePath,
            InternalBetaGatePath = internalBetaGatePath,
            FirstRunPacketPath = firstRunPacketPath,
            OperatorProofContractPath = operatorProofContractPath,
            AlphaSetupPath = alphaSetupPath,
            AlphaQuickstartPath = alphaQuickstartPath,
            OverallPosture = contractReady ? "internal_beta_exit_contract_ready" : "blocked_by_internal_beta_exit_contract_gaps",
            InternalBetaGatePosture = gate.OverallPosture,
            FirstRunPacketPosture = packet.OverallPosture,
            OperatorProofContract = gate.OperatorProofContract,
            Samples = samples,
            RepresentativeEvidenceBasis =
            [
                "CARVES.Site counts as the representative attached-repo sample on the Runtime-owned lane",
                "CARVES.Unity counts as the representative current-shape CARVES framework sample on the Runtime-owned lane",
                "CARVES.DEV stays prototype-class fresh-init robustness evidence only and does not carry the same representative weight",
            ],
            ExitCriteria =
            [
                "internal beta gate remains internal_beta_gated_ready on the same Runtime-owned lane",
                "first-run operator packet remains first_run_packet_ready on the same Runtime-owned lane",
                "at least one representative attached-repo sample and one representative current-shape CARVES repo sample remain explicit",
                "prototype-class evidence remains marked as lower-weight robustness support rather than representative product proof",
                "blocked claims and WAITING_OPERATOR_* obligations remain explicit instead of being silently upgraded",
            ],
            BlockedClaims =
            [
                "current samples do not upgrade proof source to operator_run_proof",
                "current samples do not imply external_user_proof, public release, or broad beta completion",
                "prototype-class CARVES.DEV does not count as equivalent weight to CARVES.Site or CARVES.Unity",
                "current representative samples do not force minimal trusted bootstrap truth behavior to open next",
                "the internal beta exit contract does not create a second proof ledger, second planner, or second release authority",
            ],
            EntryCommands =
            [
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-session-gateway-internal-beta-exit-contract"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-session-gateway-internal-beta-gate"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-first-run-operator-packet"),
                RuntimeHostCommandLauncher.Cold("api", "runtime-session-gateway-internal-beta-exit-contract"),
            ],
            RecommendedNextAction = contractReady
                ? "Use CARVES.Site and CARVES.Unity as the current representative internal beta basis, keep CARVES.DEV as robustness-only support, and only open new behavior work if a future representative sample exposes a real gap."
                : "Restore the missing exit-contract inputs before treating current internal beta evidence weighting as explicitly projected.",
            IsValid = contractReady,
            Errors = errors,
            Warnings = warnings,
            NonClaims =
            [
                "This surface is a Runtime-owned read model over existing packets, not a second proof ledger.",
                "This surface does not replace the operator proof contract, internal beta gate, or first-run operator packet.",
                "Weighting current samples does not auto-open bootstrap behavior or any other next implementation slice.",
            ],
        };
    }

    private List<RuntimeSessionGatewayInternalBetaEvidenceSampleSurface> BuildSamples(List<string> errors)
    {
        return
        [
            BuildSample(
                "carves_site_first_run",
                "CARVES.Site",
                @"D:\Projects\CARVES.AI\CARVES.Site",
                "attached_real_repo",
                "representative_attached_repo",
                "passed",
                "docs/session-gateway/internal-operator-proof-packet-2026-04-06-carves-site-first-run-sample.md",
                ".ai/runtime/internal-operator-proof/2026-04-06-carves-site-first-run-sample.json",
                countsAsRepresentativeEvidence: true,
                "does_not_force_behavior",
                "CARVES.Site remains the representative attached-repo sample on the Runtime-owned lane.",
                errors),
            BuildSample(
                "carves_unity_first_run",
                "CARVES.Unity",
                @"D:\Projects\CARVES\CARVES.Unity",
                "current_framework_repo",
                "representative_current_shape",
                "passed",
                "docs/session-gateway/internal-operator-proof-packet-2026-04-06-carves-unity-first-run-sample.md",
                ".ai/runtime/internal-operator-proof/2026-04-06-carves-unity-first-run-sample.json",
                countsAsRepresentativeEvidence: true,
                "does_not_force_behavior",
                "CARVES.Unity remains the more current CARVES framework sample and still keeps bootstrap behavior deferred.",
                errors),
            BuildSample(
                "carves_dev_first_run",
                "CARVES.DEV",
                @"D:\Projects\CARVES\CARVES.DEV",
                "prototype_fresh_init",
                "robustness_only",
                "passed",
                "docs/session-gateway/internal-operator-proof-packet-2026-04-06-carves-dev-first-run-sample.md",
                ".ai/runtime/internal-operator-proof/2026-04-06-carves-dev-first-run-sample.json",
                countsAsRepresentativeEvidence: false,
                "does_not_force_behavior",
                "CARVES.DEV remains prototype-class fresh-init robustness evidence and should not be overread as representative current product proof.",
                errors),
        ];
    }

    private RuntimeSessionGatewayInternalBetaEvidenceSampleSurface BuildSample(
        string sampleId,
        string repoId,
        string repoPath,
        string sampleClass,
        string evidenceWeight,
        string verdict,
        string packetPath,
        string evidencePath,
        bool countsAsRepresentativeEvidence,
        string bootstrapBehaviorJudgment,
        string summary,
        List<string> errors)
    {
        ValidateDocument(packetPath, $"{repoId} proof packet", errors);
        ValidateDocument(evidencePath, $"{repoId} proof evidence", errors);

        return new RuntimeSessionGatewayInternalBetaEvidenceSampleSurface
        {
            SampleId = sampleId,
            RepoId = repoId,
            RepoPath = repoPath,
            SampleClass = sampleClass,
            EvidenceWeight = evidenceWeight,
            Verdict = verdict,
            PacketPath = packetPath,
            EvidencePath = evidencePath,
            CountsAsRepresentativeEvidence = countsAsRepresentativeEvidence,
            BootstrapBehaviorJudgment = bootstrapBehaviorJudgment,
            Summary = summary,
        };
    }

    private void ValidateDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(repoRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }
}

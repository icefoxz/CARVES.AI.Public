using System.Security.Cryptography;
using System.Text;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Memory;
using Carves.Runtime.Domain.Memory;

namespace Carves.Runtime.Application.Planning;

public sealed class MemoryAuditService
{
    private static readonly string[] DefaultCategories = ["architecture", "project", "modules", "patterns"];
    private static readonly string[] EvidenceMarkers =
    [
        "evidence:",
        "source evidence",
        "source_evidence",
        "evidence_ref",
        "evidence ref",
        "artifact:",
        "artifacts:",
        "review_record",
        "test_result",
        "commit:",
    ];
    private static readonly string[] ConflictMarkers =
    [
        "conflict",
        "contradict",
        "inconsistent",
        "diverge",
        "disagree",
    ];
    private static readonly string[] DeprecatedMarkers =
    [
        "deprecated",
        "retired",
        "obsolete",
        "superseded",
        "replaced by",
        "no longer",
    ];
    private static readonly string[] ClaimMarkers =
    [
        " must ",
        " should ",
        " cannot ",
        " can not ",
        " requires ",
        " remains ",
        " stays ",
        " prevents ",
        " allows ",
        " blocks ",
        " default ",
        " is ",
        " are ",
    ];
    private static readonly string[] StructureMarkers =
    [
        "src/",
        "tests/",
        ".cs",
        "module:",
        "class ",
        "interface ",
        "namespace ",
        "dependency",
        "codegraph",
        "call graph",
    ];

    private readonly MemoryService memoryService;
    private readonly ICodeGraphQueryService codeGraphQueryService;

    public MemoryAuditService(MemoryService memoryService, ICodeGraphQueryService codeGraphQueryService)
    {
        this.memoryService = memoryService;
        this.codeGraphQueryService = codeGraphQueryService;
    }

    public IReadOnlyList<string> DetectMissingModuleMemory()
    {
        var knownModules = codeGraphQueryService.LoadIndex().Modules
            .Select(module => Normalize(module.Name))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var documentedModules = memoryService.LoadModuleMemoryDocuments()
            .Select(document => Normalize(document.Title))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        return knownModules
            .Where(module => !documentedModules.Contains(module))
            .ToArray();
    }

    public IReadOnlyDictionary<LegacyMarkdownClaimClassification, LegacyMarkdownBackfillDecision> GetBackfillDecisionTable()
    {
        return new Dictionary<LegacyMarkdownClaimClassification, LegacyMarkdownBackfillDecision>
        {
            [LegacyMarkdownClaimClassification.CanonicalCandidate] = new(
                LegacyMarkdownClaimClassification.CanonicalCandidate,
                "candidate_staging_after_evidence_check",
                "candidate_until_owner_review",
                RequiredEvidence: true,
                RequiresOwnerReview: true,
                RequiresCodeGraphCheck: false,
                MutationAllowed: false),
            [LegacyMarkdownClaimClassification.ProvisionalCandidate] = new(
                LegacyMarkdownClaimClassification.ProvisionalCandidate,
                "candidate_staging",
                "provisional_after_review",
                RequiredEvidence: true,
                RequiresOwnerReview: true,
                RequiresCodeGraphCheck: true,
                MutationAllowed: false),
            [LegacyMarkdownClaimClassification.Candidate] = new(
                LegacyMarkdownClaimClassification.Candidate,
                "candidate_only",
                "candidate",
                RequiredEvidence: false,
                RequiresOwnerReview: false,
                RequiresCodeGraphCheck: false,
                MutationAllowed: false),
            [LegacyMarkdownClaimClassification.OrphanClaim] = new(
                LegacyMarkdownClaimClassification.OrphanClaim,
                "legacy_readability_only",
                "none",
                RequiredEvidence: true,
                RequiresOwnerReview: true,
                RequiresCodeGraphCheck: false,
                MutationAllowed: false),
            [LegacyMarkdownClaimClassification.ExplanatoryText] = new(
                LegacyMarkdownClaimClassification.ExplanatoryText,
                "none",
                "none",
                RequiredEvidence: false,
                RequiresOwnerReview: false,
                RequiresCodeGraphCheck: false,
                MutationAllowed: false),
            [LegacyMarkdownClaimClassification.DeprecatedClaim] = new(
                LegacyMarkdownClaimClassification.DeprecatedClaim,
                "excluded_legacy_note",
                "none",
                RequiredEvidence: false,
                RequiresOwnerReview: false,
                RequiresCodeGraphCheck: false,
                MutationAllowed: false),
            [LegacyMarkdownClaimClassification.ConflictCandidate] = new(
                LegacyMarkdownClaimClassification.ConflictCandidate,
                "conflict_hold",
                "none_until_resolved",
                RequiredEvidence: true,
                RequiresOwnerReview: true,
                RequiresCodeGraphCheck: true,
                MutationAllowed: false),
        };
    }

    public IReadOnlyList<LegacyMarkdownDocumentClassification> ClassifyLegacyMarkdown(
        IReadOnlyList<string>? categories = null)
    {
        var selectedCategories = categories is { Count: > 0 } ? categories : DefaultCategories;
        return selectedCategories
            .SelectMany(category => memoryService.LoadCategory(category)
                .Select(document => ClassifyDocument(document)))
            .OrderBy(item => item.SourceFile, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public LegacyMarkdownDocumentClassification ClassifyDocument(MemoryDocument document)
    {
        var decisionTable = GetBackfillDecisionTable();
        var normalizedContent = document.Content.Replace("\r\n", "\n", StringComparison.Ordinal);
        var blocks = normalizedContent
            .Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var claims = new List<LegacyMarkdownClaimClassificationRecord>();

        for (var index = 0; index < blocks.Length; index++)
        {
            var text = NormalizeBlock(blocks[index]);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var classification = ClassifyBlock(text);
            var structureTouching = TouchesStructure(text);
            var decision = decisionTable[classification];
            claims.Add(new LegacyMarkdownClaimClassificationRecord(
                ClaimId: BuildClaimId(document, index),
                Text: text,
                Classification: classification,
                NextStep: decision.NextStep,
                AllowedAuthorityCeiling: decision.AllowedAuthorityCeiling,
                RequiredEvidence: decision.RequiredEvidence,
                RequiresOwnerReview: decision.RequiresOwnerReview,
                RequiresCodeGraphCheck: decision.RequiresCodeGraphCheck || structureTouching,
                MutationAllowed: decision.MutationAllowed,
                EvidenceDetected: HasEvidence(text),
                StructureTouching: structureTouching,
                Reason: BuildReason(classification, text)));
        }

        return new LegacyMarkdownDocumentClassification(
            SourceFile: document.Path,
            Category: document.Category,
            SourceHash: ComputeSha256(normalizedContent),
            MutationPerformed: false,
            Claims: claims);
    }

    public LegacyMarkdownOrphanClaimReport BuildOrphanClaimReport(IReadOnlyList<string>? categories = null)
    {
        var documents = ClassifyLegacyMarkdown(categories);
        return new LegacyMarkdownOrphanClaimReport(
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            SourceCategories: categories is { Count: > 0 } ? categories.ToArray() : DefaultCategories,
            Claims: documents
                .SelectMany(document => document.Claims.Select(claim => (document, claim)))
                .Where(item => item.claim.Classification == LegacyMarkdownClaimClassification.OrphanClaim)
                .Select(item => new LegacyMarkdownOrphanClaim(
                    item.document.SourceFile,
                    item.claim.ClaimId,
                    item.claim.Text,
                    item.claim.Reason))
                .ToArray());
    }

    private static LegacyMarkdownClaimClassification ClassifyBlock(string text)
    {
        if (IsHeadingBlock(text) || !LooksLikeClaim(text))
        {
            return LegacyMarkdownClaimClassification.ExplanatoryText;
        }

        var lower = $" {text.ToLowerInvariant()} ";
        if (ContainsAny(lower, DeprecatedMarkers))
        {
            return LegacyMarkdownClaimClassification.DeprecatedClaim;
        }

        if (ContainsAny(lower, ConflictMarkers))
        {
            return LegacyMarkdownClaimClassification.ConflictCandidate;
        }

        if (HasEvidence(text))
        {
            return LegacyMarkdownClaimClassification.CanonicalCandidate;
        }

        if (TouchesStructure(text))
        {
            return LegacyMarkdownClaimClassification.ProvisionalCandidate;
        }

        return ContainsAny(lower, [" must ", " should ", " cannot ", " requires ", " default "])
            ? LegacyMarkdownClaimClassification.Candidate
            : LegacyMarkdownClaimClassification.OrphanClaim;
    }

    private static bool HasEvidence(string text)
    {
        return ContainsAny(text.ToLowerInvariant(), EvidenceMarkers);
    }

    private static bool LooksLikeClaim(string text)
    {
        var lower = $" {text.ToLowerInvariant()} ";
        return ContainsAny(lower, ClaimMarkers);
    }

    private static bool TouchesStructure(string text)
    {
        return ContainsAny(text.ToLowerInvariant(), StructureMarkers);
    }

    private static bool IsHeadingBlock(string text)
    {
        return text.StartsWith('#') || (!text.Contains(' ') && text.All(character => !char.IsLower(character)));
    }

    private static bool ContainsAny(string text, IEnumerable<string> markers)
    {
        return markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildReason(LegacyMarkdownClaimClassification classification, string text)
    {
        return classification switch
        {
            LegacyMarkdownClaimClassification.CanonicalCandidate => "claim includes explicit evidence marker",
            LegacyMarkdownClaimClassification.ProvisionalCandidate => "claim touches code structure and needs corroboration before any later promotion path",
            LegacyMarkdownClaimClassification.Candidate => "claim is normative but lacks attached evidence",
            LegacyMarkdownClaimClassification.OrphanClaim => "claim is descriptive and lacks attached evidence",
            LegacyMarkdownClaimClassification.ExplanatoryText => "block is explanatory context rather than a governed fact claim",
            LegacyMarkdownClaimClassification.DeprecatedClaim => "block describes deprecated or superseded guidance",
            LegacyMarkdownClaimClassification.ConflictCandidate => "block signals a conflict that must be held for review",
            _ => $"unhandled classification for text '{text}'"
        };
    }

    private static string Normalize(string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(".", "_", StringComparison.Ordinal);
    }

    private static string NormalizeBlock(string value)
    {
        return string.Join(
            " ",
            value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string BuildClaimId(MemoryDocument document, int index)
    {
        var name = Path.GetFileNameWithoutExtension(document.Path)
            .ToLowerInvariant()
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(".", "_", StringComparison.Ordinal);
        return $"legacy-{name}-{index + 1:D3}";
    }

    private static string ComputeSha256(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public enum LegacyMarkdownClaimClassification
{
    CanonicalCandidate = 0,
    ProvisionalCandidate = 1,
    Candidate = 2,
    OrphanClaim = 3,
    ExplanatoryText = 4,
    DeprecatedClaim = 5,
    ConflictCandidate = 6,
}

public sealed record LegacyMarkdownBackfillDecision(
    LegacyMarkdownClaimClassification Classification,
    string NextStep,
    string AllowedAuthorityCeiling,
    bool RequiredEvidence,
    bool RequiresOwnerReview,
    bool RequiresCodeGraphCheck,
    bool MutationAllowed);

public sealed record LegacyMarkdownClaimClassificationRecord(
    string ClaimId,
    string Text,
    LegacyMarkdownClaimClassification Classification,
    string NextStep,
    string AllowedAuthorityCeiling,
    bool RequiredEvidence,
    bool RequiresOwnerReview,
    bool RequiresCodeGraphCheck,
    bool MutationAllowed,
    bool EvidenceDetected,
    bool StructureTouching,
    string Reason);

public sealed record LegacyMarkdownDocumentClassification(
    string SourceFile,
    string Category,
    string SourceHash,
    bool MutationPerformed,
    IReadOnlyList<LegacyMarkdownClaimClassificationRecord> Claims);

public sealed record LegacyMarkdownOrphanClaim(
    string SourceFile,
    string ClaimId,
    string Text,
    string Reason);

public sealed record LegacyMarkdownOrphanClaimReport(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<string> SourceCategories,
    IReadOnlyList<LegacyMarkdownOrphanClaim> Claims);

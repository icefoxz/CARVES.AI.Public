using Carves.Runtime.Domain.Cards;
using Carves.Runtime.Domain.Planning;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carves.Runtime.Application.Planning;

public sealed class CardParser
{
    private const string AcceptanceContractSection = "acceptance contract";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private static readonly string[] SupportedSections =
    {
        "goal",
        "scope",
        "acceptance",
        AcceptanceContractSection,
        "constraints",
        "dependencies",
    };

    public CardDefinition Parse(string path)
    {
        var lines = File.ReadAllLines(path);
        var sections = SupportedSections.ToDictionary(section => section, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);

        string? cardId = null;
        var title = string.Empty;
        var cardType = "feature";
        var priority = "P1";
        string? currentSection = null;
        var insideAcceptanceContractCodeBlock = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (cardId is null && line.StartsWith("# ", StringComparison.Ordinal))
            {
                cardId = line[2..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                continue;
            }

            if (line.StartsWith("Title:", StringComparison.OrdinalIgnoreCase))
            {
                title = line["Title:".Length..].Trim();
                continue;
            }

            if (line.StartsWith("Type:", StringComparison.OrdinalIgnoreCase))
            {
                cardType = line["Type:".Length..].Trim();
                continue;
            }

            if (line.StartsWith("Priority:", StringComparison.OrdinalIgnoreCase))
            {
                priority = line["Priority:".Length..].Trim();
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                var sectionName = line[3..].Trim().ToLowerInvariant();
                currentSection = sections.ContainsKey(sectionName) ? sectionName : null;
                insideAcceptanceContractCodeBlock = false;
                continue;
            }

            if (currentSection is null)
            {
                continue;
            }

            if (string.Equals(currentSection, AcceptanceContractSection, StringComparison.OrdinalIgnoreCase))
            {
                if (line.StartsWith("```", StringComparison.Ordinal))
                {
                    insideAcceptanceContractCodeBlock = !insideAcceptanceContractCodeBlock;
                    continue;
                }

                if (insideAcceptanceContractCodeBlock)
                {
                    sections[currentSection].Add(rawLine);
                }

                continue;
            }

            sections[currentSection].Add(line.StartsWith("- ", StringComparison.Ordinal) ? line[2..].Trim() : line);
        }

        if (string.IsNullOrWhiteSpace(cardId))
        {
            throw new InvalidOperationException($"Could not parse a card id from '{path}'.");
        }

        var acceptanceContract = sections[AcceptanceContractSection].Count == 0
            ? null
            : JsonSerializer.Deserialize<AcceptanceContract>(
                string.Join(Environment.NewLine, sections[AcceptanceContractSection]),
                JsonOptions);

        return new CardDefinition(
            cardId,
            string.IsNullOrWhiteSpace(title) ? cardId : title,
            string.Join(Environment.NewLine, sections["goal"]),
            string.IsNullOrWhiteSpace(cardType) ? "feature" : cardType,
            string.IsNullOrWhiteSpace(priority) ? "P1" : priority,
            sections["scope"].ToArray(),
            sections["acceptance"].ToArray(),
            sections["constraints"].ToArray(),
            sections["dependencies"].ToArray(),
            acceptanceContract);
    }
}

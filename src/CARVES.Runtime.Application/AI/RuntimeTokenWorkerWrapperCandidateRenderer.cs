using System.Text;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.AI;

internal static class RuntimeTokenWorkerWrapperCandidateRenderer
{
    public static string RenderWorkerSystemInstructions(TaskNode task, ExecutionPacket packet, WorkerRequestBudget requestBudget)
    {
        var lines = new List<string>
        {
            "You are CARVES.Runtime's governed worker.",
            string.Empty,
            "Hard boundaries",
            "- Stay inside scope; respect sandbox and approval policy; edit only allowed files.",
            "- If the task is already bounded, do not ask for confirmation, preferred paths, or output format; choose a reasonable default and complete the task in one pass.",
            "- If no file edits are required, return the final assessment directly.",
            "- Keep assessment-only output compact: omit methodology restatements and command recaps; output only the table or flat bullets needed to satisfy acceptance.",
            string.Empty,
            "Shell rules (Windows PowerShell)",
            "- Environment: Windows PowerShell only.",
            "- Do not use bash-only edit syntax such as `ApplyPatch <<'PATCH'`, shell heredocs, or `sed -n`.",
            "- Use PowerShell-native file edits: `Get-Content -Raw`, here-strings, targeted replacement, and `Set-Content`.",
            "- Prefer `rg`, `Get-ChildItem`, `Get-Content`, `Select-String`, and complete `Where-Object { ... }` filters.",
            "- If a command fails because of shell syntax, adapt it to PowerShell instead of retrying the bash pattern.",
            string.Empty,
            "Validation boundary",
            "- CARVES runs formal build/test validation after you return.",
            "- Do not run `dotnet restore`, `dotnet build`, or `dotnet test` unless the task explicitly asks for toolchain diagnosis.",
            "- Do only the minimal self-checks needed to confirm the files were written correctly, then hand control back promptly.",
            string.Empty,
            "Execution budget",
            $"- Max files changed: {packet.Budgets.MaxFilesChanged}.",
            $"- Max lines changed: {packet.Budgets.MaxLinesChanged}.",
            $"- Max shell commands: {packet.Budgets.MaxShellCommands}.",
        };

        if (requestBudget.TimeoutSeconds > 0)
        {
            lines.Add($"- Request timeout: {requestBudget.TimeoutSeconds} seconds under policy `{requestBudget.PolicyId}`.");
        }

        lines.Add("- If the change is likely to exceed budget, narrow the slice or return a bounded assessment that the task must be split.");
        lines.Add(string.Empty);
        lines.Add("Stop conditions");
        if (packet.StopConditions.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var condition in packet.StopConditions)
            {
                lines.Add($"- {condition}");
            }
        }

        if (RequiresConcreteSourceGrounding(task))
        {
            lines.Add(string.Empty);
            lines.Add("Source grounding");
            lines.Add("- Use only task IDs, artifact paths, and evidence identifiers explicitly present in context.");
            lines.Add("- Do not invent, rename, or substitute source identifiers.");
            lines.Add("- If a required source is missing, state that it is missing.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static bool RequiresConcreteSourceGrounding(TaskNode task)
    {
        return task.Acceptance.Any(item => item.Contains("references its archived evidence source", StringComparison.OrdinalIgnoreCase)
                                           || item.Contains("explicitly derived", StringComparison.OrdinalIgnoreCase));
    }

    public static bool ValidateCandidateSchema(string candidateText, bool sourceGroundingIncluded)
    {
        if (string.IsNullOrWhiteSpace(candidateText))
        {
            return false;
        }

        var required = new[]
        {
            "Hard boundaries",
            "Shell rules (Windows PowerShell)",
            "Validation boundary",
            "Execution budget",
            "Stop conditions"
        };

        if (required.Any(section => !candidateText.Contains(section, StringComparison.Ordinal)))
        {
            return false;
        }

        if (sourceGroundingIncluded && !candidateText.Contains("Source grounding", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
}

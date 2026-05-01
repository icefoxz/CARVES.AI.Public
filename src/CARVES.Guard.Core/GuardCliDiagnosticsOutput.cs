using Carves.Runtime.Application.Guard;

namespace Carves.Guard.Core;

public static partial class GuardCliRunner
{
    private static void WriteReadDiagnosticsText(GuardDecisionReadDiagnostics diagnostics)
    {
        if (!diagnostics.IsDegraded && diagnostics.EmptyLineCount == 0)
        {
            return;
        }

        Console.WriteLine($"Readback diagnostics: skipped={diagnostics.SkippedRecordCount}, malformed={diagnostics.MalformedRecordCount}, future_schema={diagnostics.FutureVersionRecordCount}, empty={diagnostics.EmptyLineCount}");
    }
}

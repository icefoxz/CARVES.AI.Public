namespace Carves.Runtime.Cli;

internal static partial class FriendlyCliApplication
{
    private static void WriteHelp(IReadOnlyList<string> commandArguments)
    {
        if (commandArguments.Count > 0)
        {
            WriteSubcommandHelp(commandArguments[0]);
            return;
        }

        Console.WriteLine("CARVES CLI");
        Console.WriteLine("Local AI coding workflow control plane.");
        Console.WriteLine();
        Console.WriteLine("Start CARVES in a project:");
        Console.WriteLine("  carves up <target-project>");
        Console.WriteLine("  then open the target project and say: start CARVES");
        Console.WriteLine();
        Console.WriteLine("Visible gateway:");
        Console.WriteLine("  carves gateway                 # foreground gateway terminal");
        Console.WriteLine("  carves gateway serve           # foreground gateway terminal with request logs");
        Console.WriteLine("  carves gateway status          # read current gateway/Host state");
        Console.WriteLine("  carves status --watch          # read-only status heartbeat");
        Console.WriteLine();
        Console.WriteLine("Global shim:");
        Console.WriteLine("  carves shim                    # print safe global shim guidance");
        Console.WriteLine();
        Console.WriteLine("Boundaries:");
        Console.WriteLine("  not a dashboard requirement");
        Console.WriteLine("  not worker execution authority");
        Console.WriteLine("  global carves is a locator/dispatcher, not lifecycle truth");
        Console.WriteLine();
        Console.WriteLine("Essential commands:");
        Console.WriteLine("  carves up [path] [--json]       # first-use product entry");
        Console.WriteLine("  carves gateway                  # foreground gateway terminal");
        Console.WriteLine("  carves gateway status           # read current gateway/Host state");
        Console.WriteLine("  carves status --watch           # read-only status heartbeat");
        Console.WriteLine("  carves shim                     # global shim guidance only");
        Console.WriteLine("  carves doctor [--json]          # readiness diagnostics");
        Console.WriteLine("  carves help all                 # full command reference");
        Console.WriteLine();
        Console.WriteLine("Transport:");
        Console.WriteLine("  --cold    force the cold path when supported");
        Console.WriteLine("  --host    require a running resident host");
    }

    private static void WriteAllCommandReference()
    {
        Console.WriteLine("CARVES CLI full command reference");
        Console.WriteLine();
        Console.WriteLine("Canonical commands:");
        Console.WriteLine("  carves up [path] [--json]       # first-use product entry");
        Console.WriteLine("  carves shim                     # global shim guidance only");
        Console.WriteLine("  carves init [path] [--json]     # lower-level attach/init primitive");
        Console.WriteLine("  carves doctor [--json]");
        Console.WriteLine("  carves status");
        Console.WriteLine("  carves inspect <card|task|taskgraph|review|audit|packet> ...");
        Console.WriteLine("  carves plan <status|init|packet|issue-workspace|submit-workspace|export-card|export-packet|card|draft-card|approve-card|draft-taskgraph|approve-taskgraph> ...");
        Console.WriteLine("  carves run [next|task|retry|resume task] ...");
        Console.WriteLine("  carves review <task|approve|reject|reopen|block|supersede> ...");
        Console.WriteLine("  carves guard <init|check|run|audit|report|explain> ...");
        Console.WriteLine("  carves shield evaluate <evidence-path> [--json] [--output <lite|standard|combined>]");
        Console.WriteLine("  carves shield badge <evidence-path> [--json] [--output <svg-path>]");
        Console.WriteLine("  carves handoff <inspect|draft|next> <packet-path> [--json]");
        Console.WriteLine("  carves adoption probe --json");
        Console.WriteLine("  carves adoption attach --json");
        Console.WriteLine("  carves adoption intake --json");
        Console.WriteLine("  carves adoption propose --json");
        Console.WriteLine("  carves adoption plan-cleanup --json");
        Console.WriteLine("  carves adoption cleanup --apply <plan_id> --plan-hash <sha256> --json");
        Console.WriteLine("  carves adoption detach --json");
        Console.WriteLine("  carves audit <summary|timeline|explain|evidence> ...");
        Console.WriteLine("  carves matrix <proof|verify|e2e|packaged> ...");
        Console.WriteLine("  carves test [demo|agent|package|collect|verify|result|history|compare] ...");
        Console.WriteLine("  carves pilot <boot|agent-start|guide|status|preflight|problem-intake|triage|problem-triage|friction-ledger|follow-up|problem-follow-up|triage-follow-up|follow-up-plan|problem-follow-up-plan|triage-follow-up-plan|follow-up-record|follow-up-decision-record|problem-follow-up-record|follow-up-intake|follow-up-planning|problem-follow-up-intake|follow-up-gate|follow-up-planning-gate|problem-follow-up-gate|record-follow-up-decision|report-problem|list-problems|inspect-problem|readiness|alpha|invocation|activation|alias|dist-smoke|dist-freshness|dist-binding|bind-dist|target-proof|external-proof|resources|commit-hygiene|commit-plan|closure|residue|ignore-plan|ignore-record|record-ignore-decision|dist|proof|close-loop|record-evidence|list-evidence|inspect-evidence> ...");
        Console.WriteLine("  carves context <show|estimate> <task-id> [...]");
        Console.WriteLine("  carves memory <search|promote> ...");
        Console.WriteLine("  carves evidence search [...]");
        Console.WriteLine("  carves agent <start|context|handoff|bootstrap|trace> [--json] [--write]");
        Console.WriteLine("  carves audit <sustainability|codegraph|runtime-noise> ...  # Runtime internal governance audit");
        Console.WriteLine("  carves maintain <compact-history|cleanup|detect-refactors|repair|rebuild> ...");
        Console.WriteLine("  carves workbench [overview|review|card <id>|task <id>] [--text] [--watch]");
        Console.WriteLine("  carves search <code|task|memory|evidence> <query>");
        Console.WriteLine("  carves gateway <serve|start|ensure|reconcile|restart|status|doctor|logs|activity|stop|pause|resume>");
        Console.WriteLine("  carves host <serve|start|ensure|reconcile|restart|status|doctor|logs|activity|stop|pause|resume>  # compatibility alias");
        Console.WriteLine("  carves pack <validate|admit|assign|pin|unpin|rollback|inspect|explain|audit|mismatch> ...");
        Console.WriteLine();
        Console.WriteLine("Commit hygiene:");
        Console.WriteLine("  carves audit sustainability      # classify feature truth, checkpoint truth, and local residue");
        Console.WriteLine("  carves maintain cleanup          # prune ephemeral residue before commit");
        Console.WriteLine("  carves maintain compact-history  # reduce local operational history pressure before review");
        Console.WriteLine();
        Console.WriteLine("Transport:");
        Console.WriteLine("  --cold    force the cold path when supported");
        Console.WriteLine("  --host    require a running resident host");
        Console.WriteLine("  source-tree cold host commands: powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/carves-host.ps1 <command> [...]");
        Console.WriteLine();
        Console.WriteLine("Compatibility aliases:");
        Console.WriteLine("  attach, start, repair, card, task, runtime, worker, pack");
        Console.WriteLine("  direct passthroughs such as plan-card, run-next, review-task, compact-history, cleanup");
    }

    private static void WriteSubcommandHelp(string topic)
    {
        switch (topic.ToLowerInvariant())
        {
            case "all":
                WriteAllCommandReference();
                break;
            case "init":
                Console.WriteLine("Usage: carves init [path] [--json]");
                Console.WriteLine("       Lower-level attach/init primitive. For product first use, prefer carves up [path].");
                break;
            case "up":
                Console.WriteLine("Usage: carves up [path] [--json]");
                Console.WriteLine("       First-use product entry: prepare Host readiness, attach/init, materialize .carves entry files, then report the human start prompt and agent command.");
                Console.WriteLine("       Does not dispatch worker automation, require the dashboard, or make global aliases authoritative.");
                break;
            case "shim":
                Console.WriteLine("Usage: carves shim");
                Console.WriteLine("       Global shim guidance only; prints a locator/dispatcher pattern and does not install files or mutate PATH.");
                Console.WriteLine("       Set CARVES_RUNTIME_ROOT to the unpacked Runtime root.");
                Console.WriteLine("       Unix shim body: exec \"<runtime_root>/carves\" \"$@\"");
                Console.WriteLine("       PowerShell shim body: & \"<runtime_root>/carves\" @args");
                Console.WriteLine("       Project entry still goes through carves up <target-project> and .carves/carves agent start --json.");
                Console.WriteLine("       Boundary: global carves is a locator/dispatcher, not lifecycle truth, not worker execution authority.");
                Console.WriteLine("       If you want a real global command, an operator should create the shim explicitly after confirming the Runtime root; this command only prints guidance.");
                break;
            case "doctor":
                Console.WriteLine("Usage: carves doctor [--json]");
                Console.WriteLine("       Separates CLI/tool readiness, target repo readiness, and resident host readiness.");
                break;
            case "status":
                Console.WriteLine("Usage: carves status [--watch] [--iterations <n>] [--interval-ms <ms>]");
                Console.WriteLine("       Read-only CARVES status. --watch renders a visible status heartbeat and does not start worker execution.");
                break;
            case "plan":
                Console.WriteLine("Usage: carves plan <status|init|packet|issue-workspace|submit-workspace|export-card|export-packet|card|draft-card|approve-card|draft-taskgraph|approve-taskgraph> ...");
                break;
            case "run":
                Console.WriteLine("Usage: carves run [next|task <task-id>|retry <task-id>|resume task <task-id>] [...]");
                break;
            case "review":
                Console.WriteLine("Usage: carves review <task|approve|reject|reopen|block|supersede> ...");
                break;
            case "guard":
                Console.WriteLine("Usage: carves guard init [--json] [--policy <path>] [--force]");
                Console.WriteLine("       carves guard check [--json] [--policy <path>] [--base <ref>] [--head <ref>]");
                Console.WriteLine("       carves guard run <task-id> [--json] [--policy <path>] [task-run flags...]  # experimental");
                Console.WriteLine("       carves guard audit [--json] [--limit <n>]");
                Console.WriteLine("       carves guard report [--json] [--policy <path>] [--limit <n>]");
                Console.WriteLine("       carves guard explain <run-id> [--json]");
                Console.WriteLine("       Diff-only Alpha Guard Beta patch admission; does not require Runtime task/card truth.");
                Console.WriteLine("       Task-aware Guard run is experimental for this Beta until full live worker integration proof exists.");
                Console.WriteLine("       Audit, report, and explain read local Guard decision records without Runtime governance internals.");
                break;
            case "shield":
                Console.WriteLine("Usage: carves shield evaluate <evidence-path> [--json] [--output <lite|standard|combined>]");
                Console.WriteLine("       carves shield badge <evidence-path> [--json] [--output <svg-path>]");
                Console.WriteLine("       Local-only Shield self-check over shield-evidence.v0 summary evidence.");
                Console.WriteLine("       Outputs Standard G/H/A levels, Lite score, or both.");
                Console.WriteLine("       Badge emits a static local SVG or badge metadata; it is self-check, not certification.");
                Console.WriteLine("       Does not upload source code, raw diffs, prompts, model responses, secrets, credentials, or private file payloads.");
                break;
            case "handoff":
                Console.WriteLine("Usage: carves handoff inspect [packet-path] [--json]");
                Console.WriteLine("       carves handoff draft [packet-path] [--json]");
                Console.WriteLine("       carves handoff next [packet-path] [--json]");
                Console.WriteLine();
                Console.WriteLine("Commands:");
                Console.WriteLine("       inspect  Read-only Continuity Handoff packet inspection; explicit packet path required.");
                Console.WriteLine("       draft    Write only an explicit low-confidence skeleton and refuse to overwrite existing packets.");
                Console.WriteLine("       next     Read-only raw projection that preserves readiness, confidence, diagnostics, refs, and blockers.");
                Console.WriteLine();
                Console.WriteLine("Exit codes:");
                Console.WriteLine("       inspect  0 ready; 1 invalid/operator-review/stale/blocked/low-confidence; 2 usage.");
                Console.WriteLine("       draft    0 skeleton written and inspectable; 1 exists/protected/write failure; 2 usage.");
                Console.WriteLine("       next     0 action=continue; 1 operator_review_first/reorient_first/blocked/invalid; 2 usage.");
                Console.WriteLine();
                Console.WriteLine("Boundaries:");
                Console.WriteLine("       Default packet path: .ai/handoff/handoff.json.");
                Console.WriteLine("       Handoff draft writes only low-confidence skeletons and refuses overwrite.");
                Console.WriteLine("       Handoff next is a read-only raw projection.");
                Console.WriteLine("       It does not rank, summarize, or mutate state.");
                Console.WriteLine("       Does not write task state, Guard bridge records, Audit events, or Memory product state.");
                Console.WriteLine();
                Console.WriteLine("Examples:");
                Console.WriteLine("       carves handoff draft docs/runtime/handoff.json --json");
                Console.WriteLine("       carves handoff inspect docs/runtime/handoff.json --json");
                Console.WriteLine("       carves handoff next docs/runtime/handoff.json --json");
                break;
            case "adoption":
                Console.WriteLine("Usage: carves adoption probe --json");
                Console.WriteLine("       carves adoption attach --json");
                Console.WriteLine("       carves adoption intake --json");
                Console.WriteLine("       carves adoption propose --json");
                Console.WriteLine("       carves adoption plan-cleanup --json");
                Console.WriteLine("       carves adoption cleanup --apply <plan_id> --plan-hash <sha256> --json");
                Console.WriteLine("       carves adoption detach --json");
                Console.WriteLine("       carves adoption detach --cleanup <plan_id> --plan-hash <sha256> --json");
                Console.WriteLine("       carves adoption detach --export <archive_path> --json");
                Console.WriteLine("       P1 probe is local-only and read-only.");
                Console.WriteLine("       P2 attach creates only the minimal adoption runtime binding.");
                Console.WriteLine("       P3 intake captures evidence only under .ai/runtime/adoption after valid P2 binding.");
                Console.WriteLine("       P4 propose creates non-authoritative proposal artifacts only under .ai/runtime/adoption/proposals.");
                Console.WriteLine("       P5 cleanup is plan/proof-gated and detach is soft detach only.");
                Console.WriteLine("       Legacy adoption does not create governed Memory, approved TaskGraph, Planner-authoritative CodeGraph, patches, reviews, destructive detach modes, or lock takeover paths here.");
                break;
            case "pilot":
                Console.WriteLine("Usage: carves pilot <boot|agent-start|guide|status|preflight|problem-intake|triage|problem-triage|friction-ledger|follow-up|problem-follow-up|triage-follow-up|follow-up-plan|problem-follow-up-plan|triage-follow-up-plan|follow-up-record|follow-up-decision-record|problem-follow-up-record|follow-up-intake|follow-up-planning|problem-follow-up-intake|follow-up-gate|follow-up-planning-gate|problem-follow-up-gate|record-follow-up-decision|report-problem|list-problems|inspect-problem|readiness|alpha|invocation|activation|alias|dist-smoke|dist-freshness|dist-binding|bind-dist|target-proof|external-proof|resources|commit-hygiene|commit-plan|closure|residue|ignore-plan|ignore-record|record-ignore-decision|dist|proof|close-loop|record-evidence|list-evidence|inspect-evidence> ...");
                Console.WriteLine("       carves pilot boot [--json]");
                Console.WriteLine("       carves pilot agent-start [--json]");
                Console.WriteLine("       carves pilot guide [--json]");
                Console.WriteLine("       carves pilot status [--json]");
                Console.WriteLine("       carves pilot problem-intake [--json]");
                Console.WriteLine("       carves pilot triage [--json]");
                Console.WriteLine("       carves pilot problem-triage [--json]");
                Console.WriteLine("       carves pilot follow-up [--json]");
                Console.WriteLine("       carves pilot problem-follow-up [--json]");
                Console.WriteLine("       carves pilot triage-follow-up [--json]");
                Console.WriteLine("       carves pilot follow-up-plan [--json]");
                Console.WriteLine("       carves pilot problem-follow-up-plan [--json]");
                Console.WriteLine("       carves pilot triage-follow-up-plan [--json]");
                Console.WriteLine("       carves pilot follow-up-record [--json]");
                Console.WriteLine("       carves pilot follow-up-decision-record [--json]");
                Console.WriteLine("       carves pilot problem-follow-up-record [--json]");
                Console.WriteLine("       carves pilot follow-up-intake [--json]");
                Console.WriteLine("       carves pilot follow-up-planning [--json]");
                Console.WriteLine("       carves pilot problem-follow-up-intake [--json]");
                Console.WriteLine("       carves pilot follow-up-gate [--json]");
                Console.WriteLine("       carves pilot follow-up-planning-gate [--json]");
                Console.WriteLine("       carves pilot problem-follow-up-gate [--json]");
                Console.WriteLine("       carves pilot record-follow-up-decision <decision> [--all] [--candidate <candidate-id>...] --reason <text> [--operator <name>] [--acceptance-evidence <text>] [--readback <command>] [--json]");
                Console.WriteLine("       carves pilot report-problem <json-path> [--json]");
                Console.WriteLine("       carves pilot list-problems [--repo-id <id>]");
                Console.WriteLine("       carves pilot inspect-problem <problem-id>");
                Console.WriteLine("       carves pilot readiness [--json]");
                Console.WriteLine("       carves pilot alpha [--json]");
                Console.WriteLine("       carves pilot invocation [--json]");
                Console.WriteLine("       carves pilot activation [--json]");
                Console.WriteLine("       carves pilot alias [--json]");
                Console.WriteLine("       carves pilot dist-smoke [--json]");
                Console.WriteLine("       carves pilot dist-freshness [--json]");
                Console.WriteLine("       carves pilot dist-binding [--json]");
                Console.WriteLine("       carves pilot bind-dist [--json]");
                Console.WriteLine("       carves pilot target-proof [--json]");
                Console.WriteLine("       carves pilot external-proof [--json]");
                Console.WriteLine("       carves pilot resources [--json]");
                Console.WriteLine("       carves pilot residue [--json]");
                Console.WriteLine("       carves pilot ignore-plan [--json]");
                Console.WriteLine("       carves pilot ignore-record [--json]");
                Console.WriteLine("       carves pilot record-ignore-decision <decision> [--all] [--entry <entry>...] --reason <text> [--operator <name>] [--json]");
                Console.WriteLine("       carves pilot proof [--json]");
                break;
            case "context":
                Console.WriteLine("Usage: carves context <show|estimate> <task-id> [--model <model>] [--max-context-tokens <n>]");
                break;
            case "memory":
                Console.WriteLine("Usage: carves memory <search|promote> ...");
                break;
            case "evidence":
                Console.WriteLine("Usage: carves evidence search [<query...>] [--task-id <task-id>] [--kind <context|execution-run|review|planning>] [--budget <tokens>] [--take <n>]");
                break;
            case "agent":
                Console.WriteLine("Usage: carves agent start [--json]");
                Console.WriteLine("       carves agent context [<task-id>] [--json]");
                Console.WriteLine("       carves agent handoff [--json]");
                Console.WriteLine("       carves agent bootstrap [--write] [--json]");
                Console.WriteLine("       carves agent trace [--json]");
                Console.WriteLine("       carves agent trace --watch [--iterations <n>] [--interval-ms <n>]");
                Console.WriteLine("       carves agent <query|request|report> <operation> [target-id]");
                break;
            case "audit":
                Console.WriteLine("Usage: carves audit summary [--json] [--guard-decisions <path>] [--handoff <packet-path>]...");
                Console.WriteLine("       carves audit timeline [--json] [--guard-decisions <path>] [--handoff <packet-path>]...");
                Console.WriteLine("       carves audit explain <id> [--json] [--guard-decisions <path>] [--handoff <packet-path>]...");
                Console.WriteLine("       carves audit evidence [--json] [--output <path>] [--guard-decisions <path>] [--handoff <packet-path>]...");
                Console.WriteLine("       carves audit <sustainability|codegraph|runtime-noise> [...]  # Runtime internal governance audit");
                Console.WriteLine("       Public Audit product commands delegate to CARVES.Audit.Core; Runtime governance audits remain host-owned.");
                break;
            case "matrix":
                Console.WriteLine("Usage: carves matrix proof [--lane <native-minimal|full-release>] [--runtime-root <path>] [--artifact-root <path>] [--configuration <Debug|Release>] [--json]");
                Console.WriteLine("       carves matrix verify <artifact-root> [--json]");
                Console.WriteLine("       carves matrix e2e [--runtime-root <path>] [--tool-mode <Project|Installed>] [...]");
                Console.WriteLine("       carves matrix packaged [--runtime-root <path>] [--artifact-root <path>] [--configuration <Debug|Release>]");
                Console.WriteLine("       Verify exit codes: 0 verified, 1 verification failed, 2 usage.");
                Console.WriteLine("       Compatibility wrapper over carves-matrix; Matrix composes Guard, Handoff, Audit, and Shield proof outputs.");
                break;
            case "test":
                Console.WriteLine("Usage: carves test [demo|agent|package|collect|reset|verify|result|history|compare] [...]");
                Console.WriteLine("       carves test demo [--trial-root <path>] [--run-id <id>] [--json]");
                Console.WriteLine("       carves test agent [--trial-root <path>] [--run-id <id>] [--no-wait|--demo-agent] [--json]");
                Console.WriteLine("       carves test package [--output <package-root>] [--force] [--json]");
                Console.WriteLine("       carves test collect [--json]  # from a portable package root");
                Console.WriteLine("       carves test reset [--json]    # from a portable package root");
                Console.WriteLine("       carves test verify [--bundle-root <path>|--trial-root <path>] [--json]");
                Console.WriteLine("       carves test result [--trial-root <path>] [--json]");
                Console.WriteLine("       carves test history [--trial-root <path>] [--json]");
                Console.WriteLine("       carves test compare [--trial-root <path>] --baseline <run-id> --target <run-id> [--json]");
                Console.WriteLine("       Tests local agent execution evidence posture, not generic project unit tests.");
                Console.WriteLine("       Thin wrapper over CARVES.Matrix.Core Agent Trial; Runtime does not own collector, scorer, verifier, manifest, result-card, or history logic.");
                Console.WriteLine("       Non-claims: not certification, not benchmark, not hosted verification, not server receipt, not leaderboard submission.");
                break;
            case "maintain":
                Console.WriteLine("Usage: carves maintain <compact-history|cleanup|detect-refactors|repair|rebuild> [...]");
                break;
            case "workbench":
                Console.WriteLine("Usage: carves workbench [overview|review|card <card-id>|task <task-id>] [--text] [--watch] [--iterations <n>] [--interval-ms <ms>]");
                break;
            case "search":
                Console.WriteLine("Usage: carves search <code|task|memory|evidence> <query>");
                break;
            case "gateway":
            case "host":
                var entry = string.Equals(topic, "gateway", StringComparison.OrdinalIgnoreCase)
                    ? "gateway"
                    : "host";
                Console.WriteLine($"Usage: carves {entry} <serve|start|ensure|reconcile|restart|status|doctor|logs|activity|stop|pause|resume>");
                Console.WriteLine("       Gateway role: resident connection, routing, and observability for CARVES surfaces.");
                Console.WriteLine("       Gateway boundary: it does not dispatch worker automation; role-mode gates control automation separately.");
                if (string.Equals(entry, "gateway", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("       carves gateway with no subcommand is the foreground gateway terminal.");
                }

                Console.WriteLine("       serve [--port <port>] [--interval-ms <milliseconds>] runs the gateway in this terminal and prints gateway requests.");
                Console.WriteLine("       ensure [--json] safely validates or starts the resident host; it does not replace a stale conflicting generation.");
                Console.WriteLine("       reconcile --replace-stale [--json] is the explicit destructive path when host status projects host_session_conflict.");
                Console.WriteLine("       restart [--json] [--force] [--port <port>] [--interval-ms <milliseconds>] [reason...] stops a healthy generation and starts a fresh one.");
                Console.WriteLine("       status --json projects host_readiness, host_operational_state, conflict_present, safe_to_start_new_host, and recommended_action.");
                Console.WriteLine("       doctor [--json] [--tail <lines>] shows gateway readiness, lifecycle, recommended action, and log visibility.");
                Console.WriteLine("       logs [--json] [--tail <lines>] shows resident gateway stdout/stderr paths and bounded log tails.");
                Console.WriteLine("       activity [--json] [--tail <lines>] summarizes recent CARVES requests and feedback from gateway logs.");
                break;
            case "pack":
                Console.WriteLine("Usage: carves pack validate <runtime-pack-v1-manifest-path>");
                Console.WriteLine("       carves pack admit <runtime-pack-v1-manifest-path> [--channel <channel>] [--published-by <principal>] [--source-line <line>]");
                Console.WriteLine("       carves pack assign <pack-id> [--pack-version <version>] [--channel <channel>] [--reason <text>]");
                Console.WriteLine("       carves pack pin [--reason <text>]");
                Console.WriteLine("       carves pack unpin [--reason <text>]");
                Console.WriteLine("       carves pack rollback <selection-id> [--reason <text>]");
                Console.WriteLine("       carves pack inspect <admission|admission-policy|selection|switch-policy|policy-audit|policy-preview|policy-transfer|distribution-boundary>");
                Console.WriteLine("       carves pack explain --task <task-id>");
                Console.WriteLine("       carves pack audit");
                Console.WriteLine("       carves pack mismatch");
                Console.WriteLine("       Thin UX aliases over existing Runtime pack surfaces; does not create a second control plane.");
                Console.WriteLine("       For raw runtime pack artifact admission, use: carves runtime admit-pack <pack-artifact-path> --attribution <runtime-pack-attribution-path>");
                break;
            default:
                WriteHelp(Array.Empty<string>());
                break;
        }
    }
}

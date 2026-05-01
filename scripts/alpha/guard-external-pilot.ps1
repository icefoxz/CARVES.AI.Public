param(
    [string]$Root = "",
    [string]$RuntimeRoot = "",
    [switch]$Keep
)

$ErrorActionPreference = "Stop"

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed: $output"
    }

    return $output
}

function Invoke-Carves {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = "dotnet"
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.UseShellExecute = $false
    $startInfo.ArgumentList.Add("run")
    $startInfo.ArgumentList.Add("--project")
    $startInfo.ArgumentList.Add((Join-Path $RuntimeRoot "src/CARVES.Runtime.Cli/carves.csproj"))
    $startInfo.ArgumentList.Add("--no-build")
    $startInfo.ArgumentList.Add("--")
    $startInfo.ArgumentList.Add("--repo-root")
    $startInfo.ArgumentList.Add($Root)
    foreach ($argument in $Arguments) {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::Start($startInfo)
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    $json = $null
    if (-not [string]::IsNullOrWhiteSpace($stdout)) {
        $json = $stdout | ConvertFrom-Json
    }

    return [pscustomobject]@{
        exit_code = $process.ExitCode
        stdout = $stdout
        stderr = $stderr
        json = $json
    }
}

function Reset-PilotDiff {
    Invoke-Git @("reset", "--hard", "HEAD") | Out-Null
    foreach ($path in @(".ai/tasks", "src/large.ts", "tests/todo-count.test.ts", "tests/leak.test.ts", "tests/large.test.ts")) {
        if (Test-Path $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}

function Convert-RuleIds {
    param([object]$Json)

    if ($null -eq $Json -or $null -eq $Json.violations) {
        return @()
    }

    return @($Json.violations | ForEach-Object { $_.rule_id })
}

function Invoke-Scenario {
    param(
        [string]$Scenario,
        [scriptblock]$Patch,
        [int]$ExpectedExitCode
    )

    Reset-PilotDiff
    & $Patch
    $result = Invoke-Carves @("guard", "check", "--json")
    if ($result.exit_code -ne $ExpectedExitCode) {
        throw "$Scenario expected exit code $ExpectedExitCode but got $($result.exit_code): $($result.stderr)"
    }

    return [pscustomobject]@{
        scenario = $Scenario
        command = "carves guard check --json"
        exit_code = $result.exit_code
        decision = $result.json.decision
        run_id = $result.json.run_id
        policy_id = $result.json.policy_id
        changed_files = @($result.json.changed_files | ForEach-Object { $_.path })
        rule_ids = Convert-RuleIds $result.json
        summary = $result.json.summary
    }
}

if ([string]::IsNullOrWhiteSpace($RuntimeRoot)) {
    $RuntimeRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
}

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Join-Path ([System.IO.Path]::GetTempPath()) ("carves-alpha-guard-pilot-" + [Guid]::NewGuid().ToString("N"))
}

New-Item -ItemType Directory -Force $Root | Out-Null
$previousLocation = Get-Location

try {
    Set-Location $Root
    Invoke-Git @("-c", "init.defaultBranch=main", "init") | Out-Null
    Invoke-Git @("config", "user.email", "alpha-guard-pilot@example.invalid") | Out-Null
    Invoke-Git @("config", "user.name", "Alpha Guard Pilot") | Out-Null

    New-Item -ItemType Directory -Force ".ai", "src", "tests" | Out-Null
    @"
{
  "schema_version": 1,
  "policy_id": "alpha-guard-external-pilot",
  "path_policy": {
    "path_case": "case_sensitive",
    "allowed_path_prefixes": [ "src/", "tests/", "README.md", "package.json", "package-lock.json" ],
    "protected_path_prefixes": [ ".ai/tasks/", ".ai/memory/", ".git/", ".github/workflows/" ],
    "outside_allowed_action": "review",
    "protected_path_action": "block"
  },
  "change_budget": {
    "max_changed_files": 4,
    "max_total_additions": 12,
    "max_total_deletions": 20,
    "max_file_additions": 8,
    "max_file_deletions": 20,
    "max_renames": 1
  },
  "dependency_policy": {
    "manifest_paths": [ "package.json", "*.csproj" ],
    "lockfile_paths": [ "package-lock.json", "packages.lock.json" ],
    "manifest_without_lockfile_action": "review",
    "lockfile_without_manifest_action": "review",
    "new_dependency_action": "review"
  },
  "change_shape": {
    "allow_rename_with_content_change": false,
    "allow_delete_without_replacement": false,
    "generated_path_prefixes": [ "dist/", "build/", "coverage/" ],
    "generated_path_action": "review",
    "mixed_feature_and_refactor_action": "review",
    "require_tests_for_source_changes": true,
    "source_path_prefixes": [ "src/" ],
    "test_path_prefixes": [ "tests/" ],
    "missing_tests_action": "block"
  },
  "decision": {
    "fail_closed": true,
    "default_outcome": "allow",
    "review_is_passing": false,
    "emit_evidence": true
  }
}
"@ | Set-Content -Encoding UTF8 ".ai/guard-policy.json"
    "# Alpha Guard external pilot target" | Set-Content -Encoding UTF8 "README.md"
    "export const todos = [];" | Set-Content -Encoding UTF8 "src/todo.ts"
    "test('baseline', () => expect(true).toBe(true));" | Set-Content -Encoding UTF8 "tests/todo.test.ts"
    Invoke-Git @("add", ".") | Out-Null
    Invoke-Git @("commit", "-m", "baseline") | Out-Null

    $scenarios = @()
    $scenarios += Invoke-Scenario "allow_source_and_test_patch" {
        Add-Content -Encoding UTF8 "src/todo.ts" "export function countTodos() { return todos.length; }"
        "test('count', () => expect(0).toBe(0));" | Set-Content -Encoding UTF8 "tests/todo-count.test.ts"
    } 0

    $scenarios += Invoke-Scenario "block_protected_control_plane_path" {
        New-Item -ItemType Directory -Force ".ai/tasks" | Out-Null
        "{ ""task_id"": ""external-generated-task"" }" | Set-Content -Encoding UTF8 ".ai/tasks/external-generated-task.json"
        Add-Content -Encoding UTF8 "src/todo.ts" "export const leakedControlPlaneWrite = true;"
        "test('leak', () => expect(true).toBe(true));" | Set-Content -Encoding UTF8 "tests/leak.test.ts"
    } 1

    $scenarios += Invoke-Scenario "block_oversized_patch" {
        $lines = 1..13 | ForEach-Object { "export const largeValue$_ = $_;" }
        $lines | Set-Content -Encoding UTF8 "src/large.ts"
        "test('large', () => expect(true).toBe(true));" | Set-Content -Encoding UTF8 "tests/large.test.ts"
    } 1

    $scenarios += Invoke-Scenario "block_missing_tests" {
        Add-Content -Encoding UTF8 "src/todo.ts" "export function firstTodo() { return todos[0]; }"
    } 1

    $report = Invoke-Carves @("guard", "report", "--json")
    $explainTarget = $scenarios | Where-Object { $_.scenario -eq "block_protected_control_plane_path" } | Select-Object -First 1
    $explain = Invoke-Carves @("guard", "explain", $explainTarget.run_id, "--json")
    Reset-PilotDiff

    [pscustomobject]@{
        pilot = "alpha_guard_external_pilot"
        repository = $Root
        init = [pscustomobject]@{
            method = "write .ai/guard-policy.json in a normal git repository"
            carves_init_required = $false
            guard_policy_path = ".ai/guard-policy.json"
            runtime_manifest_present = (Test-Path ".ai/runtime.json")
            task_truth_present = (Test-Path ".ai/tasks")
            card_truth_present = (Test-Path ".ai/tasks/cards")
            taskgraph_truth_present = (Test-Path ".ai/tasks/graph.json")
        }
        requires_carves_task_truth = $false
        requires_carves_card_truth = $false
        requires_carves_taskgraph_truth = $false
        scenarios = $scenarios
        report = [pscustomobject]@{
            command = "carves guard report --json"
            exit_code = $report.exit_code
            schema_version = $report.json.schema_version
            posture = $report.json.posture.status
            policy_id = $report.json.policy.policy_id
            allow_count = $report.json.posture.allow_count
            block_count = $report.json.posture.block_count
        }
        explain = [pscustomobject]@{
            command = "carves guard explain $($explainTarget.run_id) --json"
            exit_code = $explain.exit_code
            schema_version = $explain.json.schema_version
            found = $explain.json.found
            run_id = $explainTarget.run_id
            rule_ids = @($explain.json.decision.violations | ForEach-Object { $_.rule_id })
            evidence_refs = @($explain.json.decision.evidence_refs)
        }
    } | ConvertTo-Json -Depth 12
}
finally {
    Set-Location $previousLocation
    if (-not $Keep -and (Test-Path $Root)) {
        Remove-Item -LiteralPath $Root -Recurse -Force
    }
}

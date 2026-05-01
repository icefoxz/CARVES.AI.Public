[CmdletBinding()]
param(
    [string]$RuntimeRoot = "",
    [string]$WorkRoot = "",
    [string]$Configuration = "Debug",
    [switch]$Keep,
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string]$PathValue)
    return [System.IO.Path]::GetFullPath($PathValue)
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string]$FileName,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [int[]]$AllowedExitCodes = @(0)
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FileName
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.UseShellExecute = $false
    foreach ($argument in $Arguments) {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) {
        throw "Failed to start command: $FileName"
    }

    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    if ($process.ExitCode -notin $AllowedExitCodes) {
        throw "Command failed with exit code $($process.ExitCode): $FileName $($Arguments -join ' ')`nSTDOUT:`n$stdout`nSTDERR:`n$stderr"
    }

    return [pscustomobject]@{
        exit_code = $process.ExitCode
        stdout = $stdout
        stderr = $stderr
        command = "$FileName $($Arguments -join ' ')"
    }
}

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    return Invoke-Checked -FileName "git" -Arguments $Arguments -WorkingDirectory $RepositoryRoot
}

function Invoke-Carves {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [int[]]$AllowedExitCodes = @(0, 1)
    )

    $projectPath = Join-Path $RuntimeRoot "src/CARVES.Runtime.Cli/carves.csproj"
    $commandArguments = @(
        "run",
        "--project",
        $projectPath,
        "--configuration",
        $Configuration,
        "--no-build",
        "--",
        "--repo-root",
        $RepositoryRoot
    ) + $Arguments

    $result = Invoke-Checked -FileName "dotnet" -Arguments $commandArguments -WorkingDirectory $RuntimeRoot -AllowedExitCodes $AllowedExitCodes
    $json = $null
    if (-not [string]::IsNullOrWhiteSpace($result.stdout)) {
        $json = $result.stdout | ConvertFrom-Json
    }

    return [pscustomobject]@{
        exit_code = $result.exit_code
        stdout = $result.stdout
        stderr = $result.stderr
        command = "carves --repo-root <pilot> $($Arguments -join ' ')"
        json = $json
    }
}

function Write-RepoFile {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $path = Join-Path $RepositoryRoot $RelativePath
    $parent = Split-Path -Parent $path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force $parent | Out-Null
    }

    Set-Content -LiteralPath $path -Encoding UTF8 -Value $Content
}

function Add-RepoContent {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $path = Join-Path $RepositoryRoot $RelativePath
    Add-Content -LiteralPath $path -Encoding UTF8 -Value $Content
}

function Reset-PilotDiff {
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @("reset", "--hard", "HEAD") | Out-Null
    foreach ($path in @("src", "tests", "packages", "docs", ".ai/tasks", ".ai/memory", ".github")) {
        $candidate = Join-Path $RepositoryRoot $path
        if (Test-Path -LiteralPath $candidate) {
            Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @("clean", "-fd", "--", $path) | Out-Null
        }
    }
}

function Convert-RuleIds {
    param([object]$Violations)

    if ($null -eq $Violations) {
        return @()
    }

    return @($Violations | ForEach-Object { $_.rule_id })
}

function New-PilotRepository {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][hashtable]$Files
    )

    New-Item -ItemType Directory -Force $RepositoryRoot | Out-Null
    Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @("-c", "init.defaultBranch=main", "init") | Out-Null
    Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @("config", "user.email", "beta-guard-pilot@example.invalid") | Out-Null
    Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @("config", "user.name", "Beta Guard Pilot") | Out-Null

    foreach ($item in $Files.GetEnumerator()) {
        Write-RepoFile -RepositoryRoot $RepositoryRoot -RelativePath $item.Key -Content $item.Value
    }

    Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @("add", ".") | Out-Null
    Invoke-Git -RepositoryRoot $RepositoryRoot -Arguments @("commit", "-m", "baseline") | Out-Null
}

function Invoke-PilotScenario {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$Scenario,
        [Parameter(Mandatory = $true)][string]$ExpectedDecision,
        [Parameter(Mandatory = $true)][int]$ExpectedExitCode,
        [Parameter(Mandatory = $true)][scriptblock]$Patch,
        [string]$ExpectedRuleId = ""
    )

    Reset-PilotDiff -RepositoryRoot $RepositoryRoot
    & $Patch $RepositoryRoot
    $result = Invoke-Carves -RepositoryRoot $RepositoryRoot -Arguments @("guard", "check", "--json")
    if ($result.exit_code -ne $ExpectedExitCode) {
        throw "$Scenario expected exit code $ExpectedExitCode but got $($result.exit_code): $($result.stderr)"
    }

    if ($result.json.decision -ne $ExpectedDecision) {
        throw "$Scenario expected decision '$ExpectedDecision' but got '$($result.json.decision)'."
    }

    $ruleIds = Convert-RuleIds $result.json.violations
    if (-not [string]::IsNullOrWhiteSpace($ExpectedRuleId) -and $ExpectedRuleId -notin $ruleIds) {
        throw "$Scenario expected rule '$ExpectedRuleId' but got '$($ruleIds -join ', ')'."
    }

    return [pscustomobject]@{
        scenario = $Scenario
        expected_decision = $ExpectedDecision
        exit_code = $result.exit_code
        decision = $result.json.decision
        run_id = $result.json.run_id
        policy_id = $result.json.policy_id
        changed_files = @($result.json.changed_files | ForEach-Object { $_.path })
        rule_ids = [string[]]$ruleIds
        summary = $result.json.summary
        passed = $true
    }
}

function Invoke-PilotReadbacks {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][object]$BlockedScenario
    )

    $audit = Invoke-Carves -RepositoryRoot $RepositoryRoot -Arguments @("guard", "audit", "--json") -AllowedExitCodes @(0)
    $report = Invoke-Carves -RepositoryRoot $RepositoryRoot -Arguments @("guard", "report", "--json") -AllowedExitCodes @(0)
    $explain = Invoke-Carves -RepositoryRoot $RepositoryRoot -Arguments @("guard", "explain", $BlockedScenario.run_id, "--json") -AllowedExitCodes @(0)

    if (-not $explain.json.found) {
        throw "guard explain did not find run '$($BlockedScenario.run_id)'."
    }

    return [pscustomobject]@{
        audit = [pscustomobject]@{
            exit_code = $audit.exit_code
            decision_count = @($audit.json.decisions).Count
            diagnostics_degraded = [bool]$audit.json.diagnostics.is_degraded
        }
        report = [pscustomobject]@{
            exit_code = $report.exit_code
            posture = $report.json.posture.status
            allow_count = $report.json.posture.allow_count
            block_count = $report.json.posture.block_count
        }
        explain = [pscustomobject]@{
            exit_code = $explain.exit_code
            found = [bool]$explain.json.found
            run_id = $BlockedScenario.run_id
            rule_ids = [string[]](Convert-RuleIds $explain.json.decision.violations)
            evidence_refs = @($explain.json.decision.evidence_refs)
        }
    }
}

function Get-TruthSnapshot {
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    return [pscustomobject]@{
        runtime_manifest_present = Test-Path -LiteralPath (Join-Path $RepositoryRoot ".ai/runtime.json")
        task_truth_present = Test-Path -LiteralPath (Join-Path $RepositoryRoot ".ai/tasks")
        card_truth_present = Test-Path -LiteralPath (Join-Path $RepositoryRoot ".ai/tasks/cards")
        taskgraph_truth_present = Test-Path -LiteralPath (Join-Path $RepositoryRoot ".ai/tasks/graph.json")
    }
}

function Invoke-NodeSinglePackagePilot {
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $policy = @'
{
  "schema_version": 1,
  "policy_id": "beta-pilot-node-single-package",
  "path_policy": {
    "path_case": "case_sensitive",
    "allowed_path_prefixes": [ "src/", "tests/", "package.json", "package-lock.json", "README.md" ],
    "protected_path_prefixes": [ ".ai/tasks/", ".ai/memory/", ".git/", ".github/workflows/" ],
    "outside_allowed_action": "block",
    "protected_path_action": "block"
  },
  "change_budget": {
    "max_changed_files": 4,
    "max_total_additions": 16,
    "max_total_deletions": 20,
    "max_file_additions": 12,
    "max_file_deletions": 20,
    "max_renames": 1
  },
  "dependency_policy": {
    "manifest_paths": [ "package.json" ],
    "lockfile_paths": [ "package-lock.json" ],
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
'@

    New-PilotRepository -RepositoryRoot $RepositoryRoot -Files @{
        ".ai/guard-policy.json" = $policy
        "README.md" = "# Node single package pilot"
        "package.json" = "{`n  `"scripts`": { `"test`": `"node --test`" }`n}"
        "package-lock.json" = "{`n  `"lockfileVersion`": 3`n}"
        "src/todo.ts" = "export const todos = [];"
        "tests/todo.test.ts" = "test('baseline', () => expect(true).toBe(true));"
    }

    $scenarios = @()
    $scenarios += Invoke-PilotScenario -RepositoryRoot $RepositoryRoot -Scenario "allow_source_and_test_patch" -ExpectedDecision "allow" -ExpectedExitCode 0 -Patch {
        param($Repo)
        Add-RepoContent -RepositoryRoot $Repo -RelativePath "src/todo.ts" -Content "export function countTodos() { return todos.length; }"
        Write-RepoFile -RepositoryRoot $Repo -RelativePath "tests/todo-count.test.ts" -Content "test('count', () => expect(0).toBe(0));"
    }
    $scenarios += Invoke-PilotScenario -RepositoryRoot $RepositoryRoot -Scenario "block_protected_runtime_task_truth" -ExpectedDecision "block" -ExpectedExitCode 1 -ExpectedRuleId "path.protected_prefix" -Patch {
        param($Repo)
        Write-RepoFile -RepositoryRoot $Repo -RelativePath ".ai/tasks/generated.json" -Content "{ `"task_id`": `"external`" }"
        Add-RepoContent -RepositoryRoot $Repo -RelativePath "src/todo.ts" -Content "export const unsafe = true;"
        Write-RepoFile -RepositoryRoot $Repo -RelativePath "tests/unsafe.test.ts" -Content "test('unsafe', () => expect(true).toBe(true));"
    }

    $readbacks = Invoke-PilotReadbacks -RepositoryRoot $RepositoryRoot -BlockedScenario $scenarios[-1]
    Reset-PilotDiff -RepositoryRoot $RepositoryRoot
    return New-PilotResult -Shape "node_single_package" -RepositoryRoot $RepositoryRoot -Scenarios $scenarios -Readbacks $readbacks
}

function Invoke-DotnetServicePilot {
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $policy = @'
{
  "schema_version": 1,
  "policy_id": "beta-pilot-dotnet-service",
  "path_policy": {
    "path_case": "case_sensitive",
    "allowed_path_prefixes": [ "src/", "tests/", "packages.lock.json", "README.md" ],
    "protected_path_prefixes": [ ".ai/tasks/", ".ai/memory/", ".git/", ".github/workflows/" ],
    "outside_allowed_action": "block",
    "protected_path_action": "block"
  },
  "change_budget": {
    "max_changed_files": 4,
    "max_total_additions": 18,
    "max_total_deletions": 20,
    "max_file_additions": 12,
    "max_file_deletions": 20,
    "max_renames": 1
  },
  "dependency_policy": {
    "manifest_paths": [ "*.csproj" ],
    "lockfile_paths": [ "packages.lock.json", "**/packages.lock.json" ],
    "manifest_without_lockfile_action": "block",
    "lockfile_without_manifest_action": "review",
    "new_dependency_action": "block"
  },
  "change_shape": {
    "allow_rename_with_content_change": false,
    "allow_delete_without_replacement": false,
    "generated_path_prefixes": [ "bin/", "obj/", "TestResults/" ],
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
'@

    New-PilotRepository -RepositoryRoot $RepositoryRoot -Files @{
        ".ai/guard-policy.json" = $policy
        "README.md" = "# Dotnet service pilot"
        "src/App/App.csproj" = "<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>"
        "src/App/Calculator.cs" = "namespace Pilot; public static class Calculator { public static int Add(int a, int b) => a + b; }"
        "tests/App.Tests/CalculatorTests.cs" = "namespace Pilot.Tests; public sealed class CalculatorTests { }"
        "packages.lock.json" = "{`n  `"version`": 1`n}"
    }

    $scenarios = @()
    $scenarios += Invoke-PilotScenario -RepositoryRoot $RepositoryRoot -Scenario "allow_service_and_test_patch" -ExpectedDecision "allow" -ExpectedExitCode 0 -Patch {
        param($Repo)
        Add-RepoContent -RepositoryRoot $Repo -RelativePath "src/App/Calculator.cs" -Content "namespace Pilot; public static class MoreCalculator { public static int Double(int value) => value * 2; }"
        Add-RepoContent -RepositoryRoot $Repo -RelativePath "tests/App.Tests/CalculatorTests.cs" -Content "namespace Pilot.Tests; public sealed class MoreCalculatorTests { }"
    }
    $scenarios += Invoke-PilotScenario -RepositoryRoot $RepositoryRoot -Scenario "block_manifest_without_lockfile" -ExpectedDecision "block" -ExpectedExitCode 1 -ExpectedRuleId "dependency.manifest_without_lockfile" -Patch {
        param($Repo)
        Add-RepoContent -RepositoryRoot $Repo -RelativePath "src/App/App.csproj" -Content "<!-- add package reference without lockfile proof -->"
        Add-RepoContent -RepositoryRoot $Repo -RelativePath "tests/App.Tests/CalculatorTests.cs" -Content "namespace Pilot.Tests; public sealed class DependencyGuardTests { }"
    }

    $readbacks = Invoke-PilotReadbacks -RepositoryRoot $RepositoryRoot -BlockedScenario $scenarios[-1]
    Reset-PilotDiff -RepositoryRoot $RepositoryRoot
    return New-PilotResult -Shape "dotnet_service" -RepositoryRoot $RepositoryRoot -Scenarios $scenarios -Readbacks $readbacks
}

function Invoke-MonorepoPackagesPilot {
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $policy = @'
{
  "schema_version": 1,
  "policy_id": "beta-pilot-monorepo-packages",
  "path_policy": {
    "path_case": "case_sensitive",
    "allowed_path_prefixes": [ "packages/app/src/", "packages/app/tests/", "packages/lib/src/", "packages/lib/tests/", "package.json", "package-lock.json" ],
    "protected_path_prefixes": [ ".ai/tasks/", ".ai/memory/", ".git/", ".github/workflows/" ],
    "outside_allowed_action": "block",
    "protected_path_action": "block"
  },
  "change_budget": {
    "max_changed_files": 5,
    "max_total_additions": 20,
    "max_total_deletions": 20,
    "max_file_additions": 12,
    "max_file_deletions": 20,
    "max_renames": 1
  },
  "dependency_policy": {
    "manifest_paths": [ "package.json" ],
    "lockfile_paths": [ "package-lock.json" ],
    "manifest_without_lockfile_action": "review",
    "lockfile_without_manifest_action": "review",
    "new_dependency_action": "review"
  },
  "change_shape": {
    "allow_rename_with_content_change": false,
    "allow_delete_without_replacement": false,
    "generated_path_prefixes": [ "packages/app/dist/", "packages/lib/dist/", "coverage/" ],
    "generated_path_action": "review",
    "mixed_feature_and_refactor_action": "review",
    "require_tests_for_source_changes": true,
    "source_path_prefixes": [ "packages/app/src/", "packages/lib/src/" ],
    "test_path_prefixes": [ "packages/app/tests/", "packages/lib/tests/" ],
    "missing_tests_action": "block"
  },
  "decision": {
    "fail_closed": true,
    "default_outcome": "allow",
    "review_is_passing": false,
    "emit_evidence": true
  }
}
'@

    New-PilotRepository -RepositoryRoot $RepositoryRoot -Files @{
        ".ai/guard-policy.json" = $policy
        "package.json" = "{`n  `"workspaces`": [ `"packages/*`" ]`n}"
        "package-lock.json" = "{`n  `"lockfileVersion`": 3`n}"
        "packages/app/src/index.ts" = "export const app = true;"
        "packages/app/tests/index.test.ts" = "test('app', () => expect(true).toBe(true));"
        "packages/lib/src/index.ts" = "export const lib = true;"
        "packages/lib/tests/index.test.ts" = "test('lib', () => expect(true).toBe(true));"
    }

    $scenarios = @()
    $scenarios += Invoke-PilotScenario -RepositoryRoot $RepositoryRoot -Scenario "allow_package_local_source_and_test_patch" -ExpectedDecision "allow" -ExpectedExitCode 0 -Patch {
        param($Repo)
        Add-RepoContent -RepositoryRoot $Repo -RelativePath "packages/lib/src/index.ts" -Content "export const value = 1;"
        Add-RepoContent -RepositoryRoot $Repo -RelativePath "packages/lib/tests/index.test.ts" -Content "test('value', () => expect(1).toBe(1));"
    }
    $scenarios += Invoke-PilotScenario -RepositoryRoot $RepositoryRoot -Scenario "block_outside_allowed_docs_change" -ExpectedDecision "block" -ExpectedExitCode 1 -ExpectedRuleId "path.outside_allowed_prefix" -Patch {
        param($Repo)
        Write-RepoFile -RepositoryRoot $Repo -RelativePath "docs/architecture.md" -Content "# Unauthorized architecture note"
    }

    $readbacks = Invoke-PilotReadbacks -RepositoryRoot $RepositoryRoot -BlockedScenario $scenarios[-1]
    Reset-PilotDiff -RepositoryRoot $RepositoryRoot
    return New-PilotResult -Shape "monorepo_packages" -RepositoryRoot $RepositoryRoot -Scenarios $scenarios -Readbacks $readbacks
}

function New-PilotResult {
    param(
        [Parameter(Mandatory = $true)][string]$Shape,
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][object[]]$Scenarios,
        [Parameter(Mandatory = $true)][object]$Readbacks
    )

    return [pscustomobject]@{
        shape = $Shape
        repository = $RepositoryRoot
        init = [pscustomobject]@{
            method = "write .ai/guard-policy.json in a normal git repository"
            carves_init_required = $false
            guard_policy_path = ".ai/guard-policy.json"
            target_truth = Get-TruthSnapshot -RepositoryRoot $RepositoryRoot
        }
        requires_carves_task_truth = $false
        requires_carves_card_truth = $false
        requires_carves_taskgraph_truth = $false
        scenarios = $Scenarios
        readbacks = $Readbacks
        pilot_discovered_block_level_issues = @()
    }
}

if ([string]::IsNullOrWhiteSpace($RuntimeRoot)) {
    $RuntimeRoot = Resolve-FullPath (Join-Path $PSScriptRoot "../..")
}
else {
    $RuntimeRoot = Resolve-FullPath $RuntimeRoot
}

$generatedWorkRoot = [string]::IsNullOrWhiteSpace($WorkRoot)
if ($generatedWorkRoot) {
    $WorkRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("carves-beta-guard-pilot-matrix-" + [Guid]::NewGuid().ToString("N"))
}

$WorkRoot = Resolve-FullPath $WorkRoot
New-Item -ItemType Directory -Force $WorkRoot | Out-Null

$build = $null
if (-not $SkipBuild) {
    $build = Invoke-Checked -FileName "dotnet" -Arguments @("build", (Join-Path $RuntimeRoot "src/CARVES.Runtime.Cli/carves.csproj"), "--configuration", $Configuration) -WorkingDirectory $RuntimeRoot
}

try {
    $pilots = @()
    $pilots += Invoke-NodeSinglePackagePilot -RepositoryRoot (Join-Path $WorkRoot "node-single-package")
    $pilots += Invoke-DotnetServicePilot -RepositoryRoot (Join-Path $WorkRoot "dotnet-service")
    $pilots += Invoke-MonorepoPackagesPilot -RepositoryRoot (Join-Path $WorkRoot "monorepo-packages")

    $allScenarios = @($pilots | ForEach-Object { $_.scenarios })
    $allIssues = @($pilots | ForEach-Object { $_.pilot_discovered_block_level_issues })
    [pscustomobject]@{
        schema_version = "beta-guard-pilot-matrix.v1"
        pilot = "beta_guard_external_pilot_matrix"
        work_root = $WorkRoot
        build = if ($null -eq $build) { $null } else { [pscustomobject]@{ command = $build.command; exit_code = $build.exit_code } }
        repository_shapes = @("node_single_package", "dotnet_service", "monorepo_packages")
        aggregate = [pscustomobject]@{
            repository_count = $pilots.Count
            scenario_count = $allScenarios.Count
            allow_count = @($allScenarios | Where-Object { $_.decision -eq "allow" }).Count
            block_count = @($allScenarios | Where-Object { $_.decision -eq "block" }).Count
            readback_sets = @($pilots | Where-Object { $_.readbacks.audit.exit_code -eq 0 -and $_.readbacks.report.exit_code -eq 0 -and $_.readbacks.explain.found }).Count
            pilot_discovered_block_level_issue_count = $allIssues.Count
            beta_known_limitation_count = 0
        }
        pilots = $pilots
        pilot_discovered_block_level_issues = $allIssues
        beta_known_limitations = @()
        next_gate = "CARD-753 records no-op completion when pilot_discovered_block_level_issue_count is 0"
    } | ConvertTo-Json -Depth 24
}
finally {
    if (-not $Keep -and $generatedWorkRoot -and (Test-Path -LiteralPath $WorkRoot)) {
        Remove-Item -LiteralPath $WorkRoot -Recurse -Force
    }
}

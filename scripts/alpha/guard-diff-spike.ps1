param(
    [string]$Root = "",
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

function Convert-Numstat {
    param([string[]]$Lines)

    $stats = @{}
    foreach ($line in $Lines) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $parts = $line -split "`t"
        if ($parts.Length -lt 3) {
            continue
        }

        $stats[$parts[2]] = [pscustomobject]@{
            additions = if ($parts[0] -eq "-") { 0 } else { [int]$parts[0] }
            deletions = if ($parts[1] -eq "-") { 0 } else { [int]$parts[1] }
        }
    }

    return $stats
}

function Get-ChangedFiles {
    $nameStatus = @(Invoke-Git @("diff", "--name-status", "HEAD"))
    $numstat = Convert-Numstat @(Invoke-Git @("diff", "--numstat", "HEAD"))
    $files = @()

    foreach ($line in $nameStatus) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $parts = $line -split "`t"
        $status = $parts[0]
        $path = $parts[$parts.Length - 1]
        $stat = $numstat[$path]
        $files += [pscustomobject]@{
            path = $path
            status = $status
            additions = if ($null -eq $stat) { 0 } else { $stat.additions }
            deletions = if ($null -eq $stat) { 0 } else { $stat.deletions }
        }
    }

    return $files
}

function Test-Prefix {
    param(
        [string]$Path,
        [string[]]$Prefixes
    )

    foreach ($prefix in $Prefixes) {
        if ($Path.StartsWith($prefix, [StringComparison]::Ordinal)) {
            return $true
        }
    }

    return $false
}

function Invoke-SpikeDecision {
    param(
        [string]$Scenario,
        [object[]]$ChangedFiles
    )

    $protectedPrefixes = @(".ai/tasks/", ".ai/memory/", ".git/", ".github/workflows/")
    $allowedPrefixes = @("src/", "tests/", "README.md")
    $violations = @()
    $warnings = @()

    if ($ChangedFiles.Count -gt 2) {
        $violations += [pscustomobject]@{
            rule_id = "max_changed_files"
            message = "Changed file count exceeds spike budget."
            evidence = "$($ChangedFiles.Count) files changed; budget is 2."
        }
    }

    foreach ($file in $ChangedFiles) {
        if (Test-Prefix $file.path $protectedPrefixes) {
            $violations += [pscustomobject]@{
                rule_id = "protected_path"
                message = "Patch touches a protected control-plane path."
                evidence = $file.path
            }
        }

        if (-not (Test-Prefix $file.path $allowedPrefixes)) {
            $warnings += [pscustomobject]@{
                rule_id = "outside_allowed_prefix"
                message = "Path is outside the spike allowed prefixes."
                evidence = $file.path
            }
        }
    }

    [pscustomobject]@{
        scenario = $Scenario
        verdict = if ($violations.Count -eq 0) { "allow" } else { "block" }
        changed_file_count = $ChangedFiles.Count
        changed_files = $ChangedFiles
        violations = $violations
        warnings = $warnings
    }
}

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Join-Path ([System.IO.Path]::GetTempPath()) ("carves-alpha-guard-spike-" + [Guid]::NewGuid().ToString("N"))
}

New-Item -ItemType Directory -Force $Root | Out-Null
$previousLocation = Get-Location

try {
    Set-Location $Root
    Invoke-Git @("-c", "init.defaultBranch=main", "init") | Out-Null
    Invoke-Git @("config", "user.email", "alpha-guard-spike@example.invalid") | Out-Null
    Invoke-Git @("config", "user.name", "Alpha Guard Spike") | Out-Null

    New-Item -ItemType Directory -Force ".ai", "src", "tests" | Out-Null
    @"
{
  "schema_version": 1,
  "policy_id": "alpha-guard-spike",
  "path_policy": {
    "path_case": "case_sensitive",
    "allowed_path_prefixes": [ "src/", "tests/", "README.md" ],
    "protected_path_prefixes": [ ".ai/tasks/", ".ai/memory/", ".git/", ".github/workflows/" ],
    "outside_allowed_action": "review",
    "protected_path_action": "block"
  },
  "change_budget": {
    "max_changed_files": 2,
    "max_total_additions": 100,
    "max_total_deletions": 100,
    "max_file_additions": 50,
    "max_file_deletions": 50,
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
    "missing_tests_action": "review"
  },
  "decision": {
    "fail_closed": true,
    "default_outcome": "allow",
    "review_is_passing": false,
    "emit_evidence": true
  }
}
"@ | Set-Content -Encoding UTF8 ".ai/guard-policy.json"
    "# Alpha Guard spike target" | Set-Content -Encoding UTF8 "README.md"
    "export const todo = [];" | Set-Content -Encoding UTF8 "src/todo.ts"
    "test('baseline', () => expect(true).toBe(true));" | Set-Content -Encoding UTF8 "tests/todo.test.ts"
    Invoke-Git @("add", ".") | Out-Null
    Invoke-Git @("commit", "-m", "baseline") | Out-Null

    Add-Content -Encoding UTF8 "src/todo.ts" "export function countTodos() { return todo.length; }"
    "test('count', () => expect(0).toBe(0));" | Set-Content -Encoding UTF8 "tests/todo-count.test.ts"
    Invoke-Git @("add", "-N", ".") | Out-Null
    $allowDecision = Invoke-SpikeDecision "allow_src_and_tests_patch" @(Get-ChangedFiles)

    Invoke-Git @("reset", "--hard", "HEAD") | Out-Null
    Invoke-Git @("clean", "-fd") | Out-Null

    New-Item -ItemType Directory -Force ".ai/tasks" | Out-Null
    "{ ""task_id"": ""external-generated-task"" }" | Set-Content -Encoding UTF8 ".ai/tasks/external-generated-task.json"
    Add-Content -Encoding UTF8 "src/todo.ts" "export const leakedControlPlaneWrite = true;"
    Invoke-Git @("add", "-N", ".") | Out-Null
    $blockDecision = Invoke-SpikeDecision "block_control_plane_path_patch" @(Get-ChangedFiles)

    [pscustomobject]@{
        spike = "alpha_guard_diff_only"
        repository = $Root
        requires_carves_task_truth = $false
        requires_carves_card_truth = $false
        requires_carves_taskgraph_truth = $false
        scenarios = @($allowDecision, $blockDecision)
    } | ConvertTo-Json -Depth 8
}
finally {
    Set-Location $previousLocation
    if (-not $Keep -and (Test-Path $Root)) {
        Remove-Item -LiteralPath $Root -Recurse -Force
    }
}

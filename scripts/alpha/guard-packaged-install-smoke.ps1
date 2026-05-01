[CmdletBinding()]
param(
    [string]$Version = "0.1.0-alpha.2",
    [string]$RuntimeRoot = "",
    [string]$WorkRoot = "",
    [switch]$Keep
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string]$PathValue)
    return [System.IO.Path]::GetFullPath($PathValue)
}

function Assert-PathInside {
    param(
        [Parameter(Mandatory = $true)][string]$PathValue,
        [Parameter(Mandatory = $true)][string]$RootValue
    )

    $resolvedPath = Resolve-FullPath $PathValue
    $resolvedRoot = Resolve-FullPath $RootValue
    if (-not $resolvedRoot.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $resolvedRoot += [System.IO.Path]::DirectorySeparatorChar
    }

    if (-not $resolvedPath.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe path outside work root. Path='$resolvedPath' Root='$resolvedRoot'."
    }
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string]$FileName,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [int[]]$AllowedExitCodes = @(0)
    )

    function ConvertTo-ArgumentString {
        param([Parameter(Mandatory = $true)][string[]]$Values)

        return ($Values | ForEach-Object {
            if ([string]::IsNullOrEmpty($_)) {
                return '""'
            }

            if ($_ -match '[\s"]') {
                return '"' + ($_.Replace('"', '\"')) + '"'
            }

            return $_
        }) -join " "
    }

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FileName
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.UseShellExecute = $false
    $startInfo.Arguments = ConvertTo-ArgumentString -Values $Arguments

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
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory
    )

    return Invoke-Checked -FileName "git" -Arguments $Arguments -WorkingDirectory $WorkingDirectory
}

function Invoke-Carves {
    param(
        [Parameter(Mandatory = $true)][string]$CarvesCommand,
        [Parameter(Mandatory = $true)][string]$TargetRoot,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [int[]]$AllowedExitCodes = @(0)
    )

    $commandArguments = @("--repo-root", $TargetRoot) + $Arguments
    $result = Invoke-Checked -FileName $CarvesCommand -Arguments $commandArguments -WorkingDirectory $TargetRoot -AllowedExitCodes $AllowedExitCodes
    $json = $null
    if (-not [string]::IsNullOrWhiteSpace($result.stdout)) {
        $json = $result.stdout | ConvertFrom-Json
    }

    return [pscustomobject]@{
        exit_code = $result.exit_code
        stdout = $result.stdout
        stderr = $result.stderr
        json = $json
        command = "carves $($commandArguments -join ' ')"
    }
}

function Reset-TargetDiff {
    param([Parameter(Mandatory = $true)][string]$TargetRoot)

    Invoke-Git -Arguments @("reset", "--hard", "HEAD") -WorkingDirectory $TargetRoot | Out-Null
    foreach ($relativePath in @(".ai/tasks", "src/large.ts", "tests/leak.test.ts", "tests/large.test.ts", "tests/todo-count.test.ts")) {
        $fullPath = Join-Path $TargetRoot $relativePath
        Assert-PathInside -PathValue $fullPath -RootValue $TargetRoot
        if (Test-Path -LiteralPath $fullPath) {
            Remove-Item -LiteralPath $fullPath -Recurse -Force
        }
    }
}

function Assert-Decision {
    param(
        [Parameter(Mandatory = $true)][object]$Result,
        [Parameter(Mandatory = $true)][string]$ExpectedDecision
    )

    if ($null -eq $Result.json) {
        throw "Expected JSON output from $($Result.command)."
    }

    if ($Result.json.decision -ne $ExpectedDecision) {
        throw "Expected decision '$ExpectedDecision' from $($Result.command), got '$($Result.json.decision)'."
    }
}

function Install-OrUpdate-CarvesTool {
    param(
        [Parameter(Mandatory = $true)][string]$PackageSource,
        [Parameter(Mandatory = $true)][string]$ToolPath,
        [Parameter(Mandatory = $true)][string]$Version
    )

    $installArguments = @(
        "tool",
        "install",
        "--tool-path",
        $ToolPath,
        "--add-source",
        $PackageSource,
        "CARVES.Runtime.Cli",
        "--version",
        $Version
    )

    $install = Invoke-Checked -FileName "dotnet" -Arguments $installArguments -WorkingDirectory $PackageSource -AllowedExitCodes @(0, 1)
    if ($install.exit_code -eq 0) {
        return [pscustomobject]@{
            method = "install"
            command = "dotnet $($installArguments -join ' ')"
            exit_code = 0
        }
    }

    $updateArguments = @(
        "tool",
        "update",
        "--tool-path",
        $ToolPath,
        "--add-source",
        $PackageSource,
        "CARVES.Runtime.Cli",
        "--version",
        $Version
    )

    $update = Invoke-Checked -FileName "dotnet" -Arguments $updateArguments -WorkingDirectory $PackageSource
    return [pscustomobject]@{
        method = "update"
        command = "dotnet $($updateArguments -join ' ')"
        exit_code = $update.exit_code
    }
}

if ([string]::IsNullOrWhiteSpace($RuntimeRoot)) {
    $RuntimeRoot = Resolve-FullPath (Join-Path $PSScriptRoot "../..")
}
else {
    $RuntimeRoot = Resolve-FullPath $RuntimeRoot
}

if ([string]::IsNullOrWhiteSpace($WorkRoot)) {
    $WorkRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("carves-alpha-guard-packaged-install-" + [Guid]::NewGuid().ToString("N"))
}
$WorkRoot = Resolve-FullPath $WorkRoot

$packageSource = Join-Path $WorkRoot "packages"
$toolPath = Join-Path $WorkRoot "tool"
$targetRoot = Join-Path $WorkRoot "external-target"

New-Item -ItemType Directory -Force $packageSource, $toolPath, $targetRoot | Out-Null

try {
    $projectPath = Join-Path $RuntimeRoot "src/CARVES.Runtime.Cli/carves.csproj"
    $pack = Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("pack", $projectPath, "--configuration", "Release", "--output", $packageSource, "/p:Version=$Version") `
        -WorkingDirectory $RuntimeRoot

    $package = Get-ChildItem -LiteralPath $packageSource -Filter "CARVES.Runtime.Cli.$Version.nupkg" | Select-Object -First 1
    if ($null -eq $package) {
        throw "Expected local package was not produced in $packageSource."
    }

    $install = Install-OrUpdate-CarvesTool -PackageSource $packageSource -ToolPath $toolPath -Version $Version
    $carvesCommand = Join-Path $toolPath "carves.exe"
    if (-not (Test-Path -LiteralPath $carvesCommand)) {
        $carvesCommand = Join-Path $toolPath "carves"
    }

    if (-not (Test-Path -LiteralPath $carvesCommand)) {
        throw "Installed carves command was not found in $toolPath."
    }

    Invoke-Git -Arguments @("-c", "init.defaultBranch=main", "init") -WorkingDirectory $targetRoot | Out-Null
    Invoke-Git -Arguments @("config", "user.email", "alpha-guard-packaged-smoke@example.invalid") -WorkingDirectory $targetRoot | Out-Null
    Invoke-Git -Arguments @("config", "user.name", "Alpha Guard Packaged Smoke") -WorkingDirectory $targetRoot | Out-Null

    New-Item -ItemType Directory -Force (Join-Path $targetRoot ".ai"), (Join-Path $targetRoot "src"), (Join-Path $targetRoot "tests") | Out-Null
    @"
{
  "schema_version": 1,
  "policy_id": "alpha-guard-packaged-install-smoke",
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
"@ | Set-Content -Encoding UTF8 (Join-Path $targetRoot ".ai/guard-policy.json")
    "# Alpha Guard packaged install smoke target" | Set-Content -Encoding UTF8 (Join-Path $targetRoot "README.md")
    "export const todos = [];" | Set-Content -Encoding UTF8 (Join-Path $targetRoot "src/todo.ts")
    "test('baseline', () => expect(true).toBe(true));" | Set-Content -Encoding UTF8 (Join-Path $targetRoot "tests/todo.test.ts")

    $truthBefore = [pscustomobject]@{
        runtime_manifest_present = Test-Path -LiteralPath (Join-Path $targetRoot ".ai/runtime.json")
        task_truth_present = Test-Path -LiteralPath (Join-Path $targetRoot ".ai/tasks")
        card_truth_present = Test-Path -LiteralPath (Join-Path $targetRoot ".ai/tasks/cards")
        taskgraph_truth_present = Test-Path -LiteralPath (Join-Path $targetRoot ".ai/tasks/graph.json")
    }

    Invoke-Git -Arguments @("add", ".") -WorkingDirectory $targetRoot | Out-Null
    Invoke-Git -Arguments @("commit", "-m", "baseline") -WorkingDirectory $targetRoot | Out-Null

    Add-Content -Encoding UTF8 (Join-Path $targetRoot "src/todo.ts") "export function countTodos() { return todos.length; }"
    "test('count', () => expect(0).toBe(0));" | Set-Content -Encoding UTF8 (Join-Path $targetRoot "tests/todo-count.test.ts")
    $allow = Invoke-Carves -CarvesCommand $carvesCommand -TargetRoot $targetRoot -Arguments @("guard", "check", "--json")
    Assert-Decision -Result $allow -ExpectedDecision "allow"

    Reset-TargetDiff -TargetRoot $targetRoot

    New-Item -ItemType Directory -Force (Join-Path $targetRoot ".ai/tasks") | Out-Null
    "{ ""task_id"": ""external-generated-task"" }" | Set-Content -Encoding UTF8 (Join-Path $targetRoot ".ai/tasks/external-generated-task.json")
    Add-Content -Encoding UTF8 (Join-Path $targetRoot "src/todo.ts") "export const leakedControlPlaneWrite = true;"
    "test('leak', () => expect(true).toBe(true));" | Set-Content -Encoding UTF8 (Join-Path $targetRoot "tests/leak.test.ts")
    $block = Invoke-Carves -CarvesCommand $carvesCommand -TargetRoot $targetRoot -Arguments @("guard", "check", "--json") -AllowedExitCodes @(1)
    Assert-Decision -Result $block -ExpectedDecision "block"

    $blockRuleIds = @($block.json.violations | ForEach-Object { $_.rule_id })
    if ("path.protected_prefix" -notin $blockRuleIds) {
        throw "Expected protected path block in packaged install smoke."
    }

    $audit = Invoke-Carves -CarvesCommand $carvesCommand -TargetRoot $targetRoot -Arguments @("guard", "audit", "--json")
    $report = Invoke-Carves -CarvesCommand $carvesCommand -TargetRoot $targetRoot -Arguments @("guard", "report", "--json")
    $explain = Invoke-Carves -CarvesCommand $carvesCommand -TargetRoot $targetRoot -Arguments @("guard", "explain", $block.json.run_id, "--json")

    if (-not $explain.json.found) {
        throw "Expected guard explain to find blocked run $($block.json.run_id)."
    }

    Reset-TargetDiff -TargetRoot $targetRoot

    [pscustomobject]@{
        smoke = "alpha_guard_packaged_install"
        version = $Version
        local_package = $package.FullName
        tool_path = $toolPath
        target_repository = $targetRoot
        package_install = $install
        distribution = [pscustomobject]@{
            local_package_source = $packageSource
            remote_registry_published = $false
            global_tool_install_used = $false
        }
        target_truth = [pscustomobject]@{
            requires_carves_task_truth = $false
            requires_carves_card_truth = $false
            requires_carves_taskgraph_truth = $false
            before_first_check = $truthBefore
            task_truth_present_after_cleanup = Test-Path -LiteralPath (Join-Path $targetRoot ".ai/tasks")
            card_truth_present_after_cleanup = Test-Path -LiteralPath (Join-Path $targetRoot ".ai/tasks/cards")
            taskgraph_truth_present_after_cleanup = Test-Path -LiteralPath (Join-Path $targetRoot ".ai/tasks/graph.json")
        }
        commands = [pscustomobject]@{
            pack = [pscustomobject]@{
                command = $pack.command
                exit_code = $pack.exit_code
            }
            guard_check_allow = [pscustomobject]@{
                command = $allow.command
                exit_code = $allow.exit_code
                decision = $allow.json.decision
                run_id = $allow.json.run_id
            }
            guard_check_block = [pscustomobject]@{
                command = $block.command
                exit_code = $block.exit_code
                decision = $block.json.decision
                run_id = $block.json.run_id
                rule_ids = $blockRuleIds
            }
            guard_audit = [pscustomobject]@{
                command = $audit.command
                exit_code = $audit.exit_code
                decisions = @($audit.json.decisions).Count
            }
            guard_report = [pscustomobject]@{
                command = $report.command
                exit_code = $report.exit_code
                posture = $report.json.posture.status
                allow_count = $report.json.posture.allow_count
                block_count = $report.json.posture.block_count
            }
            guard_explain = [pscustomobject]@{
                command = $explain.command
                exit_code = $explain.exit_code
                found = $explain.json.found
                rule_ids = @($explain.json.decision.violations | ForEach-Object { $_.rule_id })
            }
        }
    } | ConvertTo-Json -Depth 12
}
finally {
    if (-not $Keep -and (Test-Path -LiteralPath $WorkRoot)) {
        Assert-PathInside -PathValue $WorkRoot -RootValue ([System.IO.Path]::GetTempPath())
        Remove-Item -LiteralPath $WorkRoot -Recurse -Force
    }
}

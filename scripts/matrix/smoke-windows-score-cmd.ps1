[CmdletBinding()]
param(
    [string] $ZipPath = "",
    [string] $WorkRoot = "",
    [string] $BuildOutputRoot = "",
    [string] $Configuration = "Release",
    [string] $BuildLabel = "local-smoke",
    [switch] $ForceBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string] $PathValue)
    return [System.IO.Path]::GetFullPath($PathValue)
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string] $FileName,
        [Parameter(Mandatory = $true)][string[]] $Arguments,
        [Parameter(Mandatory = $true)][string] $WorkingDirectory
    )

    $output = & $FileName @Arguments 2>&1
    $lastExitCodeVariable = Get-Variable -Name LASTEXITCODE -Scope Global -ErrorAction SilentlyContinue
    $exitCode = if ($null -eq $lastExitCodeVariable) { 0 } else { [int] $lastExitCodeVariable.Value }
    if ($exitCode -ne 0) {
        $output | ForEach-Object { Write-Error $_ }
        throw "Command failed with exit code $exitCode`: $FileName $($Arguments -join ' ')"
    }

    return $output
}

function Test-TextContains {
    param(
        [Parameter(Mandatory = $true)][string] $Haystack,
        [Parameter(Mandatory = $true)][string] $Needle,
        [switch] $IgnoreCase
    )

    $comparison = if ($IgnoreCase) {
        [System.StringComparison]::OrdinalIgnoreCase
    }
    else {
        [System.StringComparison]::Ordinal
    }

    return $Haystack.IndexOf($Needle, $comparison) -ge 0
}

function Find-ToolDirectory {
    param([Parameter(Mandatory = $true)][string] $ToolName)

    $command = Get-Command $ToolName -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $command -or [string]::IsNullOrWhiteSpace($command.Source)) {
        throw "Required tool not found: $ToolName"
    }

    return Split-Path -Parent $command.Source
}

function New-IsolatedPathExt {
    return ".COM;.EXE;.BAT;.CMD;.VBS;.VBE;.JS;.JSE;.WSF;.WSH;.MSC"
}

function New-IsolatedPath {
    param([bool] $IncludeNode = $true)

    $systemRoot = $env:SystemRoot
    if ([string]::IsNullOrWhiteSpace($systemRoot)) {
        throw "SystemRoot is not set."
    }

    $entries = @(
        (Join-Path $systemRoot "System32"),
        $systemRoot,
        (Join-Path $systemRoot "System32\Wbem"),
        (Find-ToolDirectory -ToolName "git.exe")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique
    if ($IncludeNode) {
        $entries += Find-ToolDirectory -ToolName "node.exe"
    }

    $isolated = [string]::Join([System.IO.Path]::PathSeparator, $entries)
    $oldPath = $env:PATH
    $oldPathExt = $env:PATHEXT
    try {
        $env:PATH = $isolated
        $env:PATHEXT = New-IsolatedPathExt
        if (Get-Command "carves.exe" -ErrorAction SilentlyContinue) {
            throw "Isolated PATH unexpectedly contains a global carves.exe."
        }
    }
    finally {
        $env:PATH = $oldPath
        $env:PATHEXT = $oldPathExt
    }

    return $isolated
}

function Expand-PlayableZip {
    param(
        [Parameter(Mandatory = $true)][string] $SourceZip,
        [Parameter(Mandatory = $true)][string] $DestinationRoot
    )

    New-Item -ItemType Directory -Path $DestinationRoot -Force | Out-Null
    Expand-Archive -LiteralPath $SourceZip -DestinationPath $DestinationRoot -Force
    return $DestinationRoot
}

function Write-GoodAgentRun {
    param(
        [Parameter(Mandatory = $true)][string] $PackageRoot,
        [Parameter(Mandatory = $true)][string] $RepoRoot
    )

    $workspaceRoot = Join-Path $PackageRoot "agent-workspace"
    $sourcePath = Join-Path $workspaceRoot "src/bounded-fixture.js"
    $testPath = Join-Path $workspaceRoot "tests/bounded-fixture.test.js"
    $agentReportFixture = Join-Path $RepoRoot "tests/fixtures/agent-trial-v1/local-mvp-schema-examples/agent-report.json"
    $agentReportPath = Join-Path $workspaceRoot "artifacts/agent-report.json"

    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "bounded-fixture source missing: $sourcePath"
    }
    if (-not (Test-Path -LiteralPath $testPath -PathType Leaf)) {
        throw "bounded-fixture tests missing: $testPath"
    }
    if (-not (Test-Path -LiteralPath $agentReportFixture -PathType Leaf)) {
        throw "Agent report fixture missing: $agentReportFixture"
    }

    (Get-Content -LiteralPath $sourcePath -Raw).
        Replace('return `${normalizedComponent}:${mode}`;', 'return `component=${normalizedComponent}; mode=${mode}; trial=bounded`;') |
        Set-Content -LiteralPath $sourcePath -Encoding UTF8
    (Get-Content -LiteralPath $testPath -Raw).
        Replace("collector:safe", "component=collector; mode=safe; trial=bounded").
        Replace("unknown:standard", "component=unknown; mode=standard; trial=bounded") |
        Set-Content -LiteralPath $testPath -Encoding UTF8

    New-Item -ItemType Directory -Path (Split-Path -Parent $agentReportPath) -Force | Out-Null
    Copy-Item -LiteralPath $agentReportFixture -Destination $agentReportPath -Force
}

function Invoke-ScoreCmd {
    param(
        [Parameter(Mandatory = $true)][string] $PackageRoot,
        [Parameter(Mandatory = $true)][string] $IsolatedPath,
        [int] $ExpectedExitCode
    )

    $captureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("carves-scorecmd-capture-" + [System.Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $captureRoot -Force | Out-Null
    $stdoutPath = Join-Path $captureRoot "score-cmd.stdout.txt"
    $stderrPath = Join-Path $captureRoot "score-cmd.stderr.txt"
    $cmdPath = Join-Path $env:SystemRoot "System32\cmd.exe"
    if (-not (Test-Path -LiteralPath $cmdPath -PathType Leaf)) {
        throw "cmd.exe not found: $cmdPath"
    }

    $oldPath = $env:PATH
    $oldPathExt = $env:PATHEXT
    $oldNoPause = $env:CARVES_AGENT_TEST_NO_PAUSE
    $exitCode = $null
    try {
        $env:PATH = $IsolatedPath
        $env:PATHEXT = New-IsolatedPathExt
        $env:CARVES_AGENT_TEST_NO_PAUSE = "1"

        $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = $cmdPath
        $startInfo.Arguments = "/d /c SCORE.cmd"
        $startInfo.WorkingDirectory = $PackageRoot
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $startInfo.CreateNoWindow = $true

        $process = [System.Diagnostics.Process]::new()
        $process.StartInfo = $startInfo
        if (-not $process.Start()) {
            throw "Failed to start SCORE.cmd."
        }

        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(120000)) {
            try {
                $process.Kill()
            }
            catch {
            }
            throw "SCORE.cmd timed out."
        }

        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        [System.IO.File]::WriteAllText($stdoutPath, $stdout)
        [System.IO.File]::WriteAllText($stderrPath, $stderr)
        $exitCode = $process.ExitCode
    }
    finally {
        $env:PATH = $oldPath
        $env:PATHEXT = $oldPathExt
        $env:CARVES_AGENT_TEST_NO_PAUSE = $oldNoPause
    }

    $stdout = if (Test-Path -LiteralPath $stdoutPath -PathType Leaf) { Get-Content -LiteralPath $stdoutPath -Raw } else { "" }
    $stderr = if (Test-Path -LiteralPath $stderrPath -PathType Leaf) { Get-Content -LiteralPath $stderrPath -Raw } else { "" }
    if ($exitCode -ne $ExpectedExitCode) {
        throw "SCORE.cmd exit code $exitCode, expected $ExpectedExitCode.`nSTDOUT:`n$stdout`nSTDERR:`n$stderr"
    }

    return [pscustomobject]@{
        exit_code = $exitCode
        stdout = $stdout
        stderr = $stderr
        combined = "$stdout`n$stderr"
    }
}

function Assert-SuccessResult {
    param([Parameter(Mandatory = $true)][string] $PackageRoot)

    $collectPath = Join-Path $PackageRoot "results/local/matrix-agent-trial-collect.json"
    $manifestPath = Join-Path $PackageRoot "results/submit-bundle/matrix-artifact-manifest.json"
    $summaryPath = Join-Path $PackageRoot "results/submit-bundle/matrix-proof-summary.json"
    $trialResultPath = Join-Path $PackageRoot "results/submit-bundle/trial/carves-agent-trial-result.json"

    foreach ($path in @($collectPath, $manifestPath, $summaryPath, $trialResultPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Expected score artifact missing: $path"
        }
    }

    $collect = Get-Content -LiteralPath $collectPath -Raw | ConvertFrom-Json
    if ($collect.status -ne "verified") {
        throw "Expected collect status verified, got '$($collect.status)'."
    }
    if ($collect.server_submission -ne $false) {
        throw "SCORE.cmd smoke must remain local-only."
    }
    if ($collect.local_score.score_status -ne "scored" -or $collect.local_score.aggregate_score -ne 100) {
        throw "Expected scored aggregate 100, got status '$($collect.local_score.score_status)' score '$($collect.local_score.aggregate_score)'."
    }
    if ($collect.verification.trial_artifacts_verified -ne $true) {
        throw "Expected trial artifacts verified."
    }
}

$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = Resolve-FullPath (Join-Path $scriptRoot "../..")

if (-not [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
    throw "Windows SCORE.cmd clean smoke must run on Windows."
}

if ([string]::IsNullOrWhiteSpace($WorkRoot)) {
    $WorkRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("CARVES Score Cmd Smoke " + [System.Guid]::NewGuid().ToString("N"))
}
$workRootPath = Resolve-FullPath $WorkRoot
New-Item -ItemType Directory -Path $workRootPath -Force | Out-Null

if ([string]::IsNullOrWhiteSpace($ZipPath)) {
    if ([string]::IsNullOrWhiteSpace($BuildOutputRoot)) {
        $BuildOutputRoot = Join-Path $workRootPath "release"
    }
    $buildArgs = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        (Join-Path $scriptRoot "build-windows-playable-package.ps1"),
        "-OutputRoot",
        (Resolve-FullPath $BuildOutputRoot),
        "-Configuration",
        $Configuration,
        "-BuildLabel",
        $BuildLabel,
        "-Force"
    )
    if ($ForceBuild) {
        $buildArgs += "-Force"
    }
    Invoke-Checked -FileName "pwsh.exe" -Arguments $buildArgs -WorkingDirectory $repoRoot | Out-Null
    $ZipPath = Join-Path (Resolve-FullPath $BuildOutputRoot) "carves-agent-trial-pack-win-x64.zip"
}

$zipPathValue = Resolve-FullPath $ZipPath
if (-not (Test-Path -LiteralPath $zipPathValue -PathType Leaf)) {
    throw "Playable zip not found: $zipPathValue"
}

$isolatedPath = New-IsolatedPath
$successRoot = Expand-PlayableZip -SourceZip $zipPathValue -DestinationRoot (Join-Path $workRootPath "fresh extracted success")
if ((Resolve-FullPath $successRoot).StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Smoke extraction must not run from inside the source checkout."
}
Write-GoodAgentRun -PackageRoot $successRoot -RepoRoot $repoRoot
$success = Invoke-ScoreCmd -PackageRoot $successRoot -IsolatedPath $isolatedPath -ExpectedExitCode 0
if (-not (Test-TextContains -Haystack $success.combined -Needle "Result card: results\local\matrix-agent-trial-result-card.md")) {
    throw "SCORE.cmd success output did not include the result card path."
}
Assert-SuccessResult -PackageRoot $successRoot
$repeat = Invoke-ScoreCmd -PackageRoot $successRoot -IsolatedPath $isolatedPath -ExpectedExitCode 0
foreach ($text in @(
    "Package already scored. Showing the previous local result.",
    "To test another agent in this same folder, run RESET.cmd first.",
    "Final score: GREEN 100/100 (scored)"
)) {
    if (-not (Test-TextContains -Haystack $repeat.combined -Needle $text -IgnoreCase)) {
        throw "Repeat-score output did not include '$text'. Output:`n$($repeat.combined)"
    }
}
foreach ($text in @(
    "The system cannot find the batch label specified",
    "Previous result card was not found"
)) {
    if (Test-TextContains -Haystack $repeat.combined -Needle $text -IgnoreCase) {
        throw "Repeat-score output included stale failure text '$text'. Output:`n$($repeat.combined)"
    }
}

$missingRoot = Expand-PlayableZip -SourceZip $zipPathValue -DestinationRoot (Join-Path $workRootPath "fresh extracted missing scorer")
Write-GoodAgentRun -PackageRoot $missingRoot -RepoRoot $repoRoot
Rename-Item `
    -LiteralPath (Join-Path $missingRoot "tools/carves/carves.exe") `
    -NewName "carves.exe.hidden"
$missing = Invoke-ScoreCmd -PackageRoot $missingRoot -IsolatedPath $isolatedPath -ExpectedExitCode 1
foreach ($text in @(
    "CARVES scorer/service was not found",
    "Missing scorer:",
    "no package-local scorer",
    "not a complete Windows playable package"
)) {
    if (-not (Test-TextContains -Haystack $missing.combined -Needle $text -IgnoreCase)) {
        throw "Missing-scorer output did not include '$text'. Output:`n$($missing.combined)"
    }
}
if (Test-TextContains -Haystack $missing.combined -Needle "Missing dependency" -IgnoreCase) {
    throw "Missing package-local scorer was misreported as a dependency failure."
}

$missingNodeRoot = Expand-PlayableZip -SourceZip $zipPathValue -DestinationRoot (Join-Path $workRootPath "fresh extracted missing node")
Write-GoodAgentRun -PackageRoot $missingNodeRoot -RepoRoot $repoRoot
$missingNode = Invoke-ScoreCmd -PackageRoot $missingNodeRoot -IsolatedPath (New-IsolatedPath -IncludeNode $false) -ExpectedExitCode 1
foreach ($text in @(
    "Missing dependency: Node.js is required for the official starter-pack task command.",
    "Install Node.js or put node.exe on PATH"
)) {
    if (-not (Test-TextContains -Haystack $missingNode.combined -Needle $text -IgnoreCase)) {
        throw "Missing-Node output did not include '$text'. Output:`n$($missingNode.combined)"
    }
}
if (Test-TextContains -Haystack $missingNode.combined -Needle "Result: collection_failed" -IgnoreCase) {
    throw "Missing Node was misreported as a collected trial failure."
}

Write-Host "Windows SCORE.cmd clean smoke passed."
Write-Host "Success extraction: $successRoot"
Write-Host "Missing-scorer extraction: $missingRoot"
Write-Host "Missing-Node extraction: $missingNodeRoot"

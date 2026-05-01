[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-CommandAvailable([string]$Name) {
    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function New-Invocation([string]$FileName, [string[]]$PrefixArguments, [string]$Source) {
    return [pscustomobject]@{
        FileName = $FileName
        PrefixArguments = $PrefixArguments
        Source = $Source
    }
}

function Resolve-CarvesInvocation([string]$Root) {
    $dotnetAvailable = Test-CommandAvailable -Name "dotnet"

    $publishedAssembly = Join-Path (Join-Path $Root "runtime-cli") "carves.dll"
    if ($dotnetAvailable -and (Test-Path -LiteralPath $publishedAssembly -PathType Leaf)) {
        return New-Invocation -FileName "dotnet" -PrefixArguments @($publishedAssembly) -Source "local packaged runtime-cli/carves.dll"
    }

    $localCmd = Join-Path $Root "carves.cmd"
    if ($dotnetAvailable -and (Test-Path -LiteralPath $localCmd -PathType Leaf)) {
        return New-Invocation -FileName $localCmd -PrefixArguments @() -Source "repo-local carves.cmd"
    }

    $pathCarves = Get-Command "carves" -ErrorAction SilentlyContinue
    if ($null -ne $pathCarves) {
        return New-Invocation -FileName $pathCarves.Source -PrefixArguments @() -Source "PATH carves"
    }

    $sourceProject = Join-Path $Root "src/CARVES.Runtime.Cli/carves.csproj"
    if ($dotnetAvailable -and (Test-Path -LiteralPath $sourceProject -PathType Leaf)) {
        return New-Invocation -FileName "dotnet" -PrefixArguments @("run", "--project", $sourceProject, "--") -Source "source checkout dotnet run"
    }

    throw @"
CARVES Agent Trial launcher could not find a runnable CARVES command.

Expected one of:
- local packaged runtime-cli/carves.dll plus dotnet on PATH
- repo-local carves.cmd plus dotnet on PATH
- installed carves command on PATH
- source checkout src/CARVES.Runtime.Cli/carves.csproj plus dotnet on PATH

Command-line fallback after setup: carves test demo
"@
}

function Get-TestArguments([string[]]$LauncherArguments) {
    if ($LauncherArguments.Count -gt 0) {
        if ($LauncherArguments[0].Equals("test", [System.StringComparison]::OrdinalIgnoreCase)) {
            return @($LauncherArguments | Select-Object -Skip 1)
        }

        return $LauncherArguments
    }

    Write-Host "CARVES Agent Trial local test"
    Write-Host "Local-only: no server submission, no leaderboard, no certification."
    Write-Host "This launcher is not a sandbox, anti-cheat system, hosted verifier, or benchmark."
    Write-Host ""
    Write-Host "Choose an action:"
    Write-Host "  1. Run demo now (automatic local smoke)"
    Write-Host "  2. Prepare agent-assisted run"
    Write-Host "  3. Show latest result"
    Write-Host "  4. Verify latest result"
    Write-Host "  Q. Quit"
    Write-Host ""
    $selection = Read-Host "Selection [1]"
    $normalized = $selection.Trim().ToLowerInvariant()

    switch ($normalized) {
        "" { return @("demo") }
        "1" { return @("demo") }
        "demo" { return @("demo") }
        "2" { return @("agent") }
        "agent" { return @("agent") }
        "play" { return @("agent") }
        "3" { return @("result") }
        "result" { return @("result") }
        "history" { return @("history") }
        "4" { return @("verify") }
        "verify" { return @("verify") }
        "q" { return $null }
        "quit" { return $null }
        default {
            Write-Host "Unknown selection. Opening the CARVES Agent Trial guide."
            return @("--help")
        }
    }
}

function Complete-Launcher([int]$ExitCode) {
    if ($env:CARVES_AGENT_TEST_NO_PAUSE -ne "1") {
        Write-Host ""
        [void](Read-Host "Press Enter to close this CARVES Agent Trial window")
    }

    exit $ExitCode
}

$root = [System.IO.Path]::GetFullPath($PSScriptRoot)
$exitCode = 1

try {
    $testArguments = Get-TestArguments -LauncherArguments $Arguments
    if ($null -eq $testArguments) {
        Write-Host "No CARVES Agent Trial command was run."
        Complete-Launcher -ExitCode 0
    }

    $invocation = Resolve-CarvesInvocation -Root $root
    $displayCommand = "carves test"
    if ($testArguments.Count -gt 0) {
        $displayCommand = $displayCommand + " " + ($testArguments -join " ")
    }

    Write-Host ""
    Write-Host "Running: $displayCommand"
    Write-Host "Launcher source: $($invocation.Source)"
    Write-Host ""

    Push-Location $root
    try {
        & $invocation.FileName @($invocation.PrefixArguments + @("test") + $testArguments)
        $exitCode = if ($null -eq $LASTEXITCODE) { 0 } else { $LASTEXITCODE }
    }
    finally {
        Pop-Location
    }

    Write-Host ""
    if ($exitCode -eq 0) {
        Write-Host "CARVES Agent Trial launcher completed successfully."
        Write-Host "For demo/local runs, the CLI output above includes the score summary and result card path."
    }
    else {
        Write-Host "CARVES Agent Trial launcher finished with exit code $exitCode."
        Write-Host "The CLI diagnostic above is preserved as the source of truth."
    }
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    $exitCode = 1
}

Complete-Launcher -ExitCode $exitCode

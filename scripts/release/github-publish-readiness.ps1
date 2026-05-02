[CmdletBinding()]
param(
    [string] $RuntimeRoot = "",
    [string] $ArtifactRoot = "",
    [string] $Configuration = "Release",
    [string] $ReleaseId = "carves-runtime-0.6.2-beta",
    [string] $TagCandidate = "carves-runtime-v0.6.2-beta",
    [switch] $AllowDirty
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
        [Parameter(Mandatory = $true)][string] $WorkingDirectory,
        [string] $OutputPath = ""
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FileName
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.UseShellExecute = $false
    foreach ($argument in $Arguments) {
        [void] $startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) {
        throw "Failed to start command: $FileName"
    }

    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        $resolvedOutputPath = Resolve-FullPath $OutputPath
        $directory = Split-Path -Parent $resolvedOutputPath
        if (-not [string]::IsNullOrWhiteSpace($directory)) {
            New-Item -ItemType Directory -Force -Path $directory | Out-Null
        }

        $stdout | Set-Content -Path $resolvedOutputPath -Encoding UTF8
        if (-not [string]::IsNullOrWhiteSpace($stderr)) {
            $stderr | Set-Content -Path "$OutputPath.stderr.txt" -Encoding UTF8
        }
    }

    if ($process.ExitCode -ne 0) {
        throw "Command failed with exit code $($process.ExitCode): $FileName $($Arguments -join ' ')`nSTDOUT:`n$stdout`nSTDERR:`n$stderr"
    }

    return [pscustomobject]@{
        exit_code = $process.ExitCode
        stdout = $stdout
        stderr = $stderr
        command = "$FileName $($Arguments -join ' ')"
    }
}

function Read-ProjectProperty {
    param(
        [Parameter(Mandatory = $true)][string] $ProjectPath,
        [Parameter(Mandatory = $true)][string] $PropertyName
    )

    [xml] $project = Get-Content -Raw -LiteralPath $ProjectPath
    $node = $project.Project.PropertyGroup | ForEach-Object { $_.$PropertyName } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($node)) {
        throw "Project $ProjectPath is missing <$PropertyName>."
    }

    return [string] $node
}

function Assert-RequiredFile {
    param(
        [Parameter(Mandatory = $true)][string] $RelativePath,
        [Parameter(Mandatory = $true)][string] $RepositoryRoot
    )

    $path = Join-Path $RepositoryRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required publish-readiness file is missing: $RelativePath"
    }

    return [pscustomobject]@{
        path = $RelativePath.Replace('\', '/')
        sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    }
}

function Assert-RequiredDirectory {
    param(
        [Parameter(Mandatory = $true)][string] $RelativePath,
        [Parameter(Mandatory = $true)][string] $RepositoryRoot
    )

    $path = Join-Path $RepositoryRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Container)) {
        throw "Required publish-readiness directory is missing: $RelativePath"
    }
}

if ([string]::IsNullOrWhiteSpace($RuntimeRoot)) {
    $RuntimeRoot = Resolve-FullPath (Join-Path $PSScriptRoot "../..")
}
else {
    $RuntimeRoot = Resolve-FullPath $RuntimeRoot
}

if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) {
    $ArtifactRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("carves-github-publish-readiness-" + [Guid]::NewGuid().ToString("N"))
}
$ArtifactRoot = Resolve-FullPath $ArtifactRoot
$packageRoot = Join-Path $ArtifactRoot "packages"
$logRoot = Join-Path $ArtifactRoot "logs"
New-Item -ItemType Directory -Force -Path $ArtifactRoot, $packageRoot, $logRoot | Out-Null

$dirtyText = (Invoke-Checked -FileName "git" -Arguments @("status", "--porcelain") -WorkingDirectory $RuntimeRoot).stdout
$dirtyEntries = @($dirtyText -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($dirtyEntries.Count -gt 0 -and -not $AllowDirty) {
    throw "Repository has uncommitted changes. Commit first or pass -AllowDirty for a deliberate local dry-run."
}

$sourceCommit = (Invoke-Checked -FileName "git" -Arguments @("rev-parse", "HEAD") -WorkingDirectory $RuntimeRoot).stdout.Trim()

$requiredFiles = @(
    "README.md",
    "LICENSE",
    "CONTRIBUTING.md",
    "SECURITY.md",
    "CODE_OF_CONDUCT.md",
    ".github/PULL_REQUEST_TEMPLATE.md",
    ".github/ISSUE_TEMPLATE/bug_report.md",
    ".github/ISSUE_TEMPLATE/feature_request.md",
    ".github/workflows/matrix-proof.yml",
    "docs/matrix/README.md",
    "docs/matrix/public-boundary.md",
    "docs/matrix/github-actions-proof.md",
    "docs/matrix/known-limitations.md",
    "docs/matrix/packaged-install-matrix.md",
    "docs/release/matrix-release-notes.md",
    "docs/release/matrix-github-release-candidate-checkpoint.md",
    "docs/release/trust-chain-hardening-release-checkpoint.md",
    "docs/release/github-publish-readiness-boundary.md",
    "docs/release/github-release-draft.md",
    "docs/release/github-publish-readiness-checkpoint.md",
    "docs/release/product-extraction-readiness-checkpoint.md",
    "docs/release/matrix-verifiable-local-self-check-checkpoint.md",
    "docs/release/matrix-operator-release-gate.md",
    "scripts/matrix/matrix-proof-lane.ps1",
    "scripts/release/github-publish-readiness.ps1"
)

Assert-RequiredDirectory -RelativePath "docs/guard" -RepositoryRoot $RuntimeRoot
Assert-RequiredDirectory -RelativePath "docs/handoff" -RepositoryRoot $RuntimeRoot
Assert-RequiredDirectory -RelativePath "docs/audit" -RepositoryRoot $RuntimeRoot
Assert-RequiredDirectory -RelativePath "docs/shield" -RepositoryRoot $RuntimeRoot
Assert-RequiredDirectory -RelativePath "docs/matrix" -RepositoryRoot $RuntimeRoot

$fileReadiness = $requiredFiles | ForEach-Object { Assert-RequiredFile -RelativePath $_ -RepositoryRoot $RuntimeRoot }

$projectSpecs = @(
    [pscustomobject]@{ name = "runtime_cli"; path = "src/CARVES.Runtime.Cli/carves.csproj"; tool_command = "carves" },
    [pscustomobject]@{ name = "guard_core"; path = "src/CARVES.Guard.Core/Carves.Guard.Core.csproj"; tool_command = $null },
    [pscustomobject]@{ name = "guard_cli"; path = "src/CARVES.Guard.Cli/Carves.Guard.Cli.csproj"; tool_command = "carves-guard" },
    [pscustomobject]@{ name = "shield_core"; path = "src/CARVES.Shield.Core/Carves.Shield.Core.csproj"; tool_command = $null },
    [pscustomobject]@{ name = "shield_cli"; path = "src/CARVES.Shield.Cli/Carves.Shield.Cli.csproj"; tool_command = "carves-shield" },
    [pscustomobject]@{ name = "matrix_core"; path = "src/CARVES.Matrix.Core/Carves.Matrix.Core.csproj"; tool_command = $null },
    [pscustomobject]@{ name = "matrix_cli"; path = "src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj"; tool_command = "carves-matrix" },
    [pscustomobject]@{ name = "handoff_core"; path = "src/CARVES.Handoff.Core/Carves.Handoff.Core.csproj"; tool_command = $null },
    [pscustomobject]@{ name = "handoff_cli"; path = "src/CARVES.Handoff.Cli/Carves.Handoff.Cli.csproj"; tool_command = "carves-handoff" },
    [pscustomobject]@{ name = "audit_core"; path = "src/CARVES.Audit.Core/Carves.Audit.Core.csproj"; tool_command = $null },
    [pscustomobject]@{ name = "audit_cli"; path = "src/CARVES.Audit.Cli/Carves.Audit.Cli.csproj"; tool_command = "carves-audit" }
)

$packages = @()
foreach ($spec in $projectSpecs) {
    $projectPath = Join-Path $RuntimeRoot $spec.path
    $packageId = Read-ProjectProperty -ProjectPath $projectPath -PropertyName "PackageId"
    $version = Read-ProjectProperty -ProjectPath $projectPath -PropertyName "Version"
    $license = Read-ProjectProperty -ProjectPath $projectPath -PropertyName "PackageLicenseExpression"
    $repositoryUrl = Read-ProjectProperty -ProjectPath $projectPath -PropertyName "RepositoryUrl"
    $projectUrl = Read-ProjectProperty -ProjectPath $projectPath -PropertyName "PackageProjectUrl"

    if ($license -ne "Apache-2.0") {
        throw "$packageId has unexpected PackageLicenseExpression '$license'."
    }

    if ($repositoryUrl -ne "https://github.com/CARVES-AI/CARVES.Runtime" -or $projectUrl -ne "https://github.com/CARVES-AI/CARVES.Runtime") {
        throw "$packageId has unexpected public URL metadata."
    }

    Invoke-Checked `
        -FileName "dotnet" `
        -Arguments @("pack", $projectPath, "--configuration", $Configuration, "--no-restore", "--output", $packageRoot) `
        -WorkingDirectory $RuntimeRoot `
        -OutputPath (Join-Path $logRoot "$($spec.name)-pack.log") | Out-Null

    $packageFile = Join-Path $packageRoot "$packageId.$version.nupkg"
    if (-not (Test-Path -LiteralPath $packageFile -PathType Leaf)) {
        throw "Expected package was not created: $packageFile"
    }

    $fileInfo = Get-Item -LiteralPath $packageFile
    $packages += [pscustomobject]@{
        name = $spec.name
        package_id = $packageId
        version = $version
        tool_command = $spec.tool_command
        filename = $fileInfo.Name
        size_bytes = $fileInfo.Length
        sha256 = (Get-FileHash -LiteralPath $fileInfo.FullName -Algorithm SHA256).Hash
    }
}

$manifest = [pscustomobject]@{
    schema_version = "carves-github-publish-readiness.v1"
    release_id = $ReleaseId
    tag_candidate = $TagCandidate
    generated_at_utc = [DateTimeOffset]::UtcNow.ToString("O")
    source_commit = $sourceCommit
    working_tree_dirty = $dirtyEntries.Count -gt 0
    dirty_allowed = [bool] $AllowDirty
    artifact_root = $ArtifactRoot
    package_root = $packageRoot
    packages = $packages
    required_files = $fileReadiness
    checks = [pscustomobject]@{
        package_metadata = "passed"
        repository_hygiene = "passed"
        public_docs = "passed"
        matrix_scripts = "present"
        github_actions_workflow = "present"
    }
    privacy = [pscustomobject]@{
        source_upload_required = $false
        raw_diff_upload_required = $false
        prompt_upload_required = $false
        model_response_upload_required = $false
        secret_upload_required = $false
        credential_upload_required = $false
        hosted_api_required = $false
        github_token_required = $false
        nuget_token_required = $false
        network_required = $false
    }
    operator_gates = [pscustomobject]@{
        create_git_tag = "not_performed"
        create_github_release = "not_performed"
        push_packages_to_nuget_org = "not_performed"
        sign_packages = "not_performed"
        change_repository_visibility = "not_performed"
        publish_hosted_verification = "out_of_scope"
        publish_public_leaderboard = "out_of_scope"
        grant_certification = "out_of_scope"
    }
    public_claims = [pscustomobject]@{
        github_publish_ready_for_operator = $true
        release_created = $false
        tag_created = $false
        nuget_published = $false
        packages_signed = $false
        hosted_verification = $false
        public_leaderboard = $false
        certification = $false
        operating_system_sandbox = $false
    }
}

$manifestPath = Join-Path $ArtifactRoot "github-publish-readiness-manifest.json"
$manifest | ConvertTo-Json -Depth 100 | Set-Content -Path $manifestPath -Encoding UTF8
$manifest | ConvertTo-Json -Depth 100

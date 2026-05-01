[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DistRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Convert-ToManifestPath([string]$PathValue) {
    return $PathValue.Replace([System.IO.Path]::DirectorySeparatorChar, "/").Replace([System.IO.Path]::AltDirectorySeparatorChar, "/")
}

function Resolve-ForbiddenReleasePathReason([string]$ManifestPath) {
    $segments = @($ManifestPath -split "/")
    if ($segments.Count -eq 0) {
        return $null
    }

    $forbiddenSegments = @(
        ".git",
        "bin",
        "obj",
        "TestResults",
        "src",
        "tests",
        "scripts"
    )

    foreach ($segment in $segments) {
        if ($segment -in $forbiddenSegments) {
            return "forbidden path segment '$segment'"
        }
    }

    if ($segments[-1] -eq "CARVES.Runtime.sln") {
        return "forbidden solution file"
    }

    if ($ManifestPath -eq "docs/archive" -or $ManifestPath.StartsWith("docs/archive/", [System.StringComparison]::Ordinal)) {
        return "forbidden archive docs"
    }

    if ($ManifestPath -eq ".carves-platform" -or $ManifestPath.StartsWith(".carves-platform/", [System.StringComparison]::Ordinal)) {
        return "forbidden platform truth root"
    }

    for ($index = 0; $index -lt $segments.Count; $index++) {
        if ($segments[$index] -ne ".ai") {
            continue
        }

        if ($index -eq 0 -and ($ManifestPath -eq ".ai" -or $ManifestPath -eq ".ai/PROJECT_BOUNDARY.md")) {
            return $null
        }

        if ($index -eq 0) {
            return "release dist may only include .ai/PROJECT_BOUNDARY.md"
        }

        return "forbidden nested .ai control-plane state"
    }

    return $null
}

function Assert-RequiredReleaseFile([string]$Root, [string]$RelativePath) {
    $fullPath = Join-Path $Root $RelativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Release dist audit failed: required file is missing: $RelativePath"
    }
}

$resolvedRoot = [System.IO.Path]::GetFullPath($DistRoot)
if (-not (Test-Path -LiteralPath $resolvedRoot -PathType Container)) {
    throw "Release dist audit failed: dist root does not exist: $resolvedRoot"
}

Assert-RequiredReleaseFile -Root $resolvedRoot -RelativePath "MANIFEST.json"
Assert-RequiredReleaseFile -Root $resolvedRoot -RelativePath "VERSION"
Assert-RequiredReleaseFile -Root $resolvedRoot -RelativePath "carves"
Assert-RequiredReleaseFile -Root $resolvedRoot -RelativePath "carves.ps1"
Assert-RequiredReleaseFile -Root $resolvedRoot -RelativePath "carves.cmd"
Assert-RequiredReleaseFile -Root $resolvedRoot -RelativePath "runtime-cli/carves.dll"
Assert-RequiredReleaseFile -Root $resolvedRoot -RelativePath ".ai/PROJECT_BOUNDARY.md"

$manifestPath = Join-Path $resolvedRoot "MANIFEST.json"
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.dist_kind -ne "release") {
    throw "Release dist audit failed: MANIFEST.json dist_kind must be 'release'."
}

if ($manifest.source_tree_included -ne $false) {
    throw "Release dist audit failed: MANIFEST.json source_tree_included must be false."
}

if ($manifest.docs_profile -ne "runtime_release_whitelist") {
    throw "Release dist audit failed: MANIFEST.json docs_profile must be runtime_release_whitelist."
}

if ($manifest.ai_profile -ne "project_boundary_only") {
    throw "Release dist audit failed: MANIFEST.json ai_profile must be project_boundary_only."
}

$violations = New-Object System.Collections.Generic.List[string]
foreach ($item in Get-ChildItem -LiteralPath $resolvedRoot -Recurse -Force) {
    $relativePath = [System.IO.Path]::GetRelativePath($resolvedRoot, $item.FullName)
    $manifestRelativePath = Convert-ToManifestPath $relativePath
    $reason = Resolve-ForbiddenReleasePathReason -ManifestPath $manifestRelativePath
    if ($null -ne $reason) {
        $violations.Add("$manifestRelativePath ($reason)")
    }
}

if ($violations.Count -gt 0) {
    $sample = $violations | Select-Object -First 20
    throw "Release dist audit failed: forbidden paths detected:`n$($sample -join [Environment]::NewLine)"
}

Write-Host "CARVES Runtime release dist audit passed."
Write-Host "Dist root: $resolvedRoot"

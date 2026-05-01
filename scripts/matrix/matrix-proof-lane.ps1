[CmdletBinding()]
param(
    [string] $RuntimeRoot = "",
    [string] $ArtifactRoot = "",
    [string] $Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# matrix_proof_lane is owned by CARVES.Matrix.Cli; this script is the CI wrapper.

function Resolve-FullPath {
    param([Parameter(Mandatory = $true)][string] $PathValue)
    return [System.IO.Path]::GetFullPath($PathValue)
}

if ([string]::IsNullOrWhiteSpace($RuntimeRoot)) {
    $RuntimeRoot = Resolve-FullPath (Join-Path $PSScriptRoot "../..")
}
else {
    $RuntimeRoot = Resolve-FullPath $RuntimeRoot
}

if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) {
    $ArtifactRoot = Resolve-FullPath (Join-Path $RuntimeRoot "artifacts/matrix")
}
else {
    $ArtifactRoot = Resolve-FullPath $ArtifactRoot
}

$matrixProject = Join-Path $RuntimeRoot "src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj"
dotnet run `
    --project $matrixProject `
    --configuration $Configuration `
    -- `
    proof `
    --runtime-root $RuntimeRoot `
    --artifact-root $ArtifactRoot `
    --configuration $Configuration

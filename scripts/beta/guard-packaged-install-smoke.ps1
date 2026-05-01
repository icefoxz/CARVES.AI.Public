[CmdletBinding()]
param(
    [string]$Version = "0.2.0-beta.1",
    [string]$RuntimeRoot = "",
    [string]$WorkRoot = "",
    [switch]$Keep
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$osFamily = if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
    "windows"
}
elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Linux)) {
    "linux"
}
elseif ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)) {
    "macos"
}
else {
    "unsupported"
}

if ($osFamily -eq "unsupported") {
    throw "Unsupported OS for Beta packaged Guard install smoke: $([System.Runtime.InteropServices.RuntimeInformation]::OSDescription)"
}

$alphaSmoke = Join-Path $PSScriptRoot "../alpha/guard-packaged-install-smoke.ps1"
if (-not (Test-Path -LiteralPath $alphaSmoke)) {
    throw "Expected Alpha packaged smoke implementation at '$alphaSmoke'."
}

if ([string]::IsNullOrWhiteSpace($WorkRoot)) {
    $WorkRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("carves-beta-guard-packaged-install-" + [Guid]::NewGuid().ToString("N"))
}

$arguments = @{
    Version = $Version
}
if (-not [string]::IsNullOrWhiteSpace($RuntimeRoot)) {
    $arguments.RuntimeRoot = $RuntimeRoot
}

if (-not [string]::IsNullOrWhiteSpace($WorkRoot)) {
    $arguments.WorkRoot = $WorkRoot
}

if ($Keep) {
    $arguments.Keep = $true
}

$jsonText = & $alphaSmoke @arguments
$result = ($jsonText | Out-String) | ConvertFrom-Json
$result.smoke = "beta_guard_packaged_install_cross_platform"
$result | Add-Member -NotePropertyName platform -NotePropertyValue ([pscustomobject]@{
    os_family = $osFamily
    os_description = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
    architecture = ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture).ToString()
    path_separator = [System.IO.Path]::DirectorySeparatorChar
    supported_shell = "pwsh"
    tested_on_current_platform = $true
    unsupported_platform = $false
})
$result | Add-Member -NotePropertyName cross_platform_contract -NotePropertyValue ([pscustomobject]@{
    windows = "supported_by_pwsh_script"
    linux = "supported_by_pwsh_script"
    macos = "supported_by_pwsh_script"
    unsupported_platform_behavior = "fail_explicitly"
    remote_registry_required = $false
})

$result | ConvertTo-Json -Depth 16

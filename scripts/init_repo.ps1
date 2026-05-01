$ErrorActionPreference = 'Stop'

try {
    git rev-parse --is-inside-work-tree *> $null
    $isRepo = $true
}
catch {
    $isRepo = $false
}

if (-not $isRepo) {
    git init | Out-Null
}

$userName = git config user.name 2>$null
if (-not $userName) {
    git config user.name "CARVES.AI Local"
}

$userEmail = git config user.email 2>$null
if (-not $userEmail) {
    git config user.email "carves.ai.local@example.invalid"
}

git add . | Out-Null

try {
    git diff --cached --quiet
    $hasChanges = $LASTEXITCODE -ne 0
}
catch {
    $hasChanges = $true
}

if ($hasChanges) {
    git commit -m "Initialize CARVES.AI repository" | Out-Null
}
else {
    git commit --allow-empty -m "Initialize CARVES.AI repository" | Out-Null
}

Write-Output "Repository initialized for local CARVES.AI execution."

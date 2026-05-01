#!/usr/bin/env bash
set -euo pipefail

if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  git init
fi

if ! git config user.name >/dev/null 2>&1; then
  git config user.name "CARVES.AI Local"
fi

if ! git config user.email >/dev/null 2>&1; then
  git config user.email "carves.ai.local@example.invalid"
fi

git add .
if git diff --cached --quiet; then
  git commit --allow-empty -m "Initialize CARVES.AI repository" >/dev/null 2>&1 || true
else
  git commit -m "Initialize CARVES.AI repository" >/dev/null 2>&1 || true
fi

echo "Repository initialized for local CARVES.AI execution."

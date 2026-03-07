#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

git config core.hooksPath .githooks
chmod +x .githooks/pre-commit
echo "Git hooks configured successfully."
echo "Pre-commit hook will run 'dotnet format --verify-no-changes' on commits with .cs files."

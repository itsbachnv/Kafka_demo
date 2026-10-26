#!/usr/bin/env bash
# Chạy unit test: 3 service .NET (xUnit) và frontend React (Vitest).
set -euo pipefail
cd "$(dirname "$0")/.."

echo "=== Backend (.NET) ==="
dotnet test backend/SignupNoti.sln --nologo

echo "=== Frontend (React) ==="
(cd frontend && npm install --silent && npm test)

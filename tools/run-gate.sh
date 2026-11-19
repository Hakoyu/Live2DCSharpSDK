#!/usr/bin/env bash
#
# 本地门禁: build -> 单元测试 (A + core) -> 可选集成门禁.
#
# 集成门禁依赖仓库外的 verify/ 脚本, 通过环境变量 TEAM_VERIFY_ROOT 定位:
#   TEAM_VERIFY_ROOT  run-smoke.sh 与 assert-acceptance.py 所在的目录.
# 未设置时只跑 build + 单元测试, 并明确打印跳过原因; 不假设普通 clone 里有 teams/ 目录.
#
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
SOLUTION="$REPO_ROOT/Live2DCSharpSDK.sln"

echo "[run-gate] 1/3 build: $SOLUTION"
dotnet build "$SOLUTION" -m:1

echo "[run-gate] 2/3 unit tests: all tiers"
bash "$SCRIPT_DIR/run-tests.sh" all

echo "[run-gate] 3/3 integration gate"
VERIFY_ROOT="${TEAM_VERIFY_ROOT:-}"
if [ -z "$VERIFY_ROOT" ]; then
    echo "[run-gate] 跳过集成门禁（未设置 TEAM_VERIFY_ROOT）"
    exit 0
fi

SMOKE_SCRIPT="$VERIFY_ROOT/run-smoke.sh"
ACCEPTANCE_SCRIPT="$VERIFY_ROOT/assert-acceptance.py"
RAN_INTEGRATION=0

if [ -f "$SMOKE_SCRIPT" ]; then
    echo "[run-gate] smoke: $SMOKE_SCRIPT"
    bash "$SMOKE_SCRIPT"
    RAN_INTEGRATION=1
else
    echo "[run-gate] 跳过 smoke（未找到 $SMOKE_SCRIPT）"
fi

if [ -f "$ACCEPTANCE_SCRIPT" ]; then
    echo "[run-gate] acceptance: $ACCEPTANCE_SCRIPT"
    if command -v python >/dev/null 2>&1; then
        python "$ACCEPTANCE_SCRIPT"
    else
        python3 "$ACCEPTANCE_SCRIPT"
    fi
    RAN_INTEGRATION=1
else
    echo "[run-gate] 跳过 acceptance（未找到 $ACCEPTANCE_SCRIPT）"
fi

if [ "$RAN_INTEGRATION" -eq 0 ]; then
    echo "[run-gate] 跳过集成门禁（TEAM_VERIFY_ROOT 下未找到 run-smoke.sh / assert-acceptance.py）"
fi

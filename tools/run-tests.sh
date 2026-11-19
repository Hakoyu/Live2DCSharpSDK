#!/usr/bin/env bash
#
# 分档运行单元测试.
#
# 用法:
#   tools/run-tests.sh a      # A 档: 纯托管, 不加载 Core
#   tools/run-tests.sh core   # B 档: 需要 Live2DCubismCore.dll + moc3 夹具
#   tools/run-tests.sh all    # 两档都跑 (不加过滤)
#
# 附加参数原样传给 dotnet test, 例如覆盖率收集:
#   tools/run-tests.sh a --collect:"XPlat Code Coverage"
#
# 环境变量:
#   LIVE2D_TEST_MODELS  模型夹具根目录, B 档必需 (缺失时 B 档失败, 不跳过)
#
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
PROJECT="$REPO_ROOT/Live2DCSharpSDK.Tests/Live2DCSharpSDK.Tests.csproj"

TIER="${1:-all}"
if [ "$#" -gt 0 ]; then
    shift
fi

if [ ! -f "$PROJECT" ]; then
    echo "error: test project not found: $PROJECT" >&2
    exit 1
fi

RESULT_DIR="$(mktemp -d)"
trap 'rm -rf "$RESULT_DIR"' EXIT
TRX_FILE="$RESULT_DIR/unit-tests.trx"

# 运行测试, 并用 TRX 报告确认确实执行了用例.
# dotnet test 在筛选器匹配 0 个用例时仍然返回 0, 这里把 "一个都没跑" 变成失败,
run_tier() {
    dotnet test "$PROJECT" "$@" \
        --logger "trx;LogFileName=unit-tests.trx" \
        --results-directory "$RESULT_DIR"

    if [ ! -f "$TRX_FILE" ]; then
        echo "error: TRX report not found: $TRX_FILE" >&2
        exit 1
    fi

    local executed
    executed="$(grep -c "<UnitTestResult" "$TRX_FILE" || true)"
    if [ "$executed" -eq 0 ]; then
        echo "error: no tests ran for tier=$TIER (filter matched nothing)" >&2
        exit 1
    fi

    echo "[run-tests] tier=$TIER executed=$executed"
}

# 分档同时写入环境变量, 让 GlobalStateFixture 知道本次运行是否需要 Core:
# B 档缺环境时直接失败, 避免出现 "CI 绿但没跑" 的假象.
case "$TIER" in
    a)
        echo "[run-tests] tier=a (managed only, does not load Core)"
        export LIVE2D_TEST_TIER=a
        run_tier --filter "TestCategory!=RequiresCore" "$@"
        ;;
    core)
        echo "[run-tests] tier=core (requires Windows x64 + Live2DCubismCore.dll + moc3 fixtures)"
        export LIVE2D_TEST_TIER=core
        run_tier --filter "TestCategory=RequiresCore" "$@"
        ;;
    all)
        echo "[run-tests] tier=all (managed + core, no filter)"
        export LIVE2D_TEST_TIER=all
        run_tier "$@"
        ;;
    *)
        echo "usage: tools/run-tests.sh {a|core|all} [dotnet test args...]" >&2
        exit 2
        ;;
esac

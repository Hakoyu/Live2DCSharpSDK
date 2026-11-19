#!/usr/bin/env bash
#
# 按 PLAN-06 §8 的 allowlist 统计行覆盖率 (coverlet -> Cobertura).
#
# 用法:
#   tools/coverage-report.sh [a|core|all] [dotnet test args...]
#
# 说明:
#   - 覆盖率只报告不阻断: 低于 80% 打印 WARN, 退出码仍为 0 (见 PLAN-06 §8).
#   - 需要 python (或 python3) 解析 Cobertura XML.
#   - 结果目录由 COVERAGE_RESULTS_DIR 指定; 未设置时用临时目录, 脚本结束前保留并打印路径,
#     便于在日志里回查 coverage.cobertura.xml.
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

PYTHON="${PYTHON:-}"
if [ -z "$PYTHON" ]; then
    if command -v python >/dev/null 2>&1; then
        PYTHON=python
    elif command -v python3 >/dev/null 2>&1; then
        PYTHON=python3
    else
        echo "error: python is required to summarize the Cobertura report" >&2
        exit 1
    fi
fi

RESULTS_DIR="${COVERAGE_RESULTS_DIR:-}"
if [ -z "$RESULTS_DIR" ]; then
    RESULTS_DIR="$(mktemp -d)"
fi
mkdir -p "$RESULTS_DIR"

# 每次运行使用独立子目录: 复用 COVERAGE_RESULTS_DIR 时目录里可能残留历史
# coverage.cobertura.xml, 直接 find 会读到旧报告 (评审 M-2).
RUN_DIR="$RESULTS_DIR/run-$(date +%Y%m%d-%H%M%S)-$$"
mkdir -p "$RUN_DIR"
echo "[coverage] tier=$TIER results=$RUN_DIR"

# 分档同时写入环境变量, 让 GlobalStateFixture 知道本次运行是否需要 Core:
# core 档缺环境时直接失败, 避免出现 "CI 绿但没跑" 的假象.
run_coverage() {
    dotnet test "$PROJECT" "$@" \
        --collect:"XPlat Code Coverage" \
        --logger "trx;LogFileName=coverage-$TIER.trx" \
        --results-directory "$RUN_DIR"
}

case "$TIER" in
    a)
        echo "[coverage] tier=a (managed only, does not load Core)"
        export LIVE2D_TEST_TIER=a
        run_coverage --filter "TestCategory!=RequiresCore" "$@"
        ;;
    core)
        echo "[coverage] tier=core (requires Windows x64 + Live2DCubismCore.dll + moc3 fixtures)"
        export LIVE2D_TEST_TIER=core
        run_coverage --filter "TestCategory=RequiresCore" "$@"
        ;;
    all)
        echo "[coverage] tier=all (managed + core, no filter)"
        export LIVE2D_TEST_TIER=all
        run_coverage "$@"
        ;;
    *)
        echo "usage: tools/coverage-report.sh {a|core|all} [dotnet test args...]" >&2
        exit 2
        ;;
esac

REPORT="$(find "$RUN_DIR" -name coverage.cobertura.xml -print -quit)"
if [ -z "$REPORT" ] || [ ! -f "$REPORT" ]; then
    echo "error: Cobertura report not found under $RUN_DIR" >&2
    exit 1
fi

echo "[coverage] report=$REPORT"
"$PYTHON" - "$REPORT" <<'PY'
import re
import sys
import xml.etree.ElementTree as ET

# PLAN-06 §8 的 allowlist. 每项: (显示名, 精确类名, 嵌套类前缀, 排除前缀).
ALLOWLIST = [
    ("CubismMath", "Live2DCSharpSDK.Framework.Math.CubismMath", None, None),
    ("CubismMatrix44", "Live2DCSharpSDK.Framework.Math.CubismMatrix44", None, None),
    ("CubismTargetPoint", "Live2DCSharpSDK.Framework.Math.CubismTargetPoint", None, None),
    ("CubismMaskGroupUtility", "Live2DCSharpSDK.Framework.Rendering.CubismMaskGroupUtility", None, None),
    ("CsmBlendMode", "Live2DCSharpSDK.Framework.Rendering.CsmBlendMode", None, None),
    ("CubismIdManager", "Live2DCSharpSDK.Framework.Id.CubismIdManager", None, None),
    ("CubismUpdateScheduler", "Live2DCSharpSDK.Framework.Motion.CubismUpdateScheduler", None, None),
    ("CubismContext", "Live2DCSharpSDK.Framework.CubismContext", None, None),
    ("ModelResourceLocator", "Live2DCSharpSDK.App.ModelResourceLocator", None, None),
    ("LAppAllocator", "Live2DCSharpSDK.App.LAppAllocator", None, None),
    # 只统计动作条目的 DTO (File/FadeInTime/FadeOutTime 的构造与反序列化契约).
    # 外层 ModelSettingObj 与其它嵌套 record 都是自动属性, 覆盖率里是 setter 噪声, 不计入.
    ("ModelSettingObj.Motion", "Live2DCSharpSDK.Framework.ModelSettingObj/FileReference/Motion",
     "Live2DCSharpSDK.Framework.ModelSettingObj/FileReference/Motion/", None),
]

WARN_THRESHOLD = 0.80

report_path = sys.argv[1]
root = ET.parse(report_path).getroot()
classes = root.findall(".//class")

def collect(exact, nested_prefix, exclude_prefix):
    found = []
    for item in classes:
        name = item.get("name", "")
        if name == exact or (nested_prefix is not None and name.startswith(nested_prefix)):
            if exclude_prefix is not None and name.startswith(exclude_prefix):
                continue
            found.append(item)
    return found

def summarize(items):
    valid = 0
    covered = 0
    hit_lines = []
    for item in items:
        for line in item.findall("./lines/line"):
            valid += 1
            if int(line.get("hits", "0")) > 0:
                covered += 1
            else:
                hit_lines.append((item.get("filename", "?"), int(line.get("number", "0"))))
    return covered, valid, hit_lines

def compress(numbers):
    numbers = sorted(set(numbers))
    parts = []
    start = previous = None
    for value in numbers:
        if start is None:
            start = previous = value
            continue
        if value == previous + 1:
            previous = value
            continue
        parts.append(str(start) if start == previous else f"{start}-{previous}")
        start = previous = value
    if start is not None:
        parts.append(str(start) if start == previous else f"{start}-{previous}")
    return ",".join(parts)

print()
print("[coverage] allowlist line coverage (WARN threshold: 80.0%, WARN only, exit code stays 0)")
print(f"  {'item':<24} {'lines':>10}   {'line%':>7}  status")
warnings = []
gaps = []
for label, exact, nested_prefix, exclude_prefix in ALLOWLIST:
    items = collect(exact, nested_prefix, exclude_prefix)
    if not items:
        print(f"  {label:<24} {'<missing>':>10}   {'-':>7}  WARN")
        warnings.append(f"{label} (type not found in report)")
        continue
    covered, valid, hit_lines = summarize(items)
    ratio = (covered / valid) if valid else 0.0
    status = "ok" if ratio >= WARN_THRESHOLD else "WARN"
    print(f"  {label:<24} {covered:>4}/{valid:<5}   {ratio * 100:>6.1f}%  {status}")
    if ratio < WARN_THRESHOLD:
        warnings.append(f"{label} {ratio * 100:.1f}%")
        by_file = {}
        for filename, number in hit_lines:
            by_file.setdefault(filename, []).append(number)
        detail = "; ".join(f"{filename}: {compress(numbers)}" for filename, numbers in by_file.items())
        gaps.append(f"  {label}: {detail}")

print()
if warnings:
    print("[coverage] WARN: below 80%: " + ", ".join(warnings))
    print("[coverage] gap (uncovered lines):")
    for gap in gaps:
        print(gap)
else:
    print("[coverage] all allowlist items are at or above 80%")
print("[coverage] WARN does not fail the build (report only, see PLAN-06 §8)")
PY

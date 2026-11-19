#!/usr/bin/env bash
#
# 打包 SDK 库到 artifacts/nuget (本地私有 feed).
#
# 用法:
#   tools/pack.sh                 # 全量: 先清空 artifacts/nuget, 再打 4 个包
#   tools/pack.sh Framework       # 只打指定包 (不清理目录, 只校验本次产物)
#
# 依赖顺序: Framework -> App -> OpenGL / Vulkan (nuspec 引用同版本).
#
# 门禁 (任一命中即 exit 1):
#   - 包内出现 CubismCore.dll / runtimes/ (.so/.dylib) ;
#   - lib/**/*.xml 含 'Badly formed XML comment' 占位 ;
#   - 缺 lib/net10.0/<PackageId>.dll 或缺根 PACKAGE.md ;
#   - 全量模式下产物数量与预期不符.
#
# 注意: - 原生库绝不入包 (各 csproj 的 None 项 Pack="false");
#       - artifacts/ 已被 .gitignore 忽略.
#
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
OUT_DIR="$REPO_ROOT/artifacts/nuget"

FULL=0
PROJECTS=()
if [ "$#" -gt 0 ]; then
    for name in "$@"; do
        PROJECTS+=("$REPO_ROOT/Live2DCSharpSDK.$name/Live2DCSharpSDK.$name.csproj")
    done
else
    FULL=1
    for name in Framework App OpenGL Vulkan; do
        PROJECTS+=("$REPO_ROOT/Live2DCSharpSDK.$name/Live2DCSharpSDK.$name.csproj")
    done
fi

# 全量模式清空输出目录, 避免旧版本包与新产物混在一起被当成 'ok'.
if [ "$FULL" -eq 1 ]; then
    rm -rf "$OUT_DIR"
fi
mkdir -p "$OUT_DIR"

for proj in "${PROJECTS[@]}"; do
    if [ ! -f "$proj" ]; then
        echo "error: project not found: $proj" >&2
        exit 1
    fi
    echo "[pack] $(basename "$proj")"
    dotnet pack "$proj" -c Release -o "$OUT_DIR" --nologo
done

echo "[pack] output: $OUT_DIR"

# 只校验本次目标产物: 全量模式校验目录内全部包, 单包模式只校验指定包名的包.
VERIFY_LIST=()
if [ "$FULL" -eq 1 ]; then
    for nupkg in "$OUT_DIR"/*.nupkg; do
        [ -e "$nupkg" ] || continue
        VERIFY_LIST+=("$nupkg")
    done
else
    for proj in "${PROJECTS[@]}"; do
        name="$(basename "$proj" .csproj)"
        matched="$(find "$OUT_DIR" -maxdepth 1 -name "${name}.*.nupkg" -print -quit)"
        if [ -z "$matched" ]; then
            echo "error: nupkg not found for $name" >&2
            exit 1
        fi
        VERIFY_LIST+=("$matched")
    done
fi

for nupkg in "${VERIFY_LIST[@]}"; do
    base="$(basename "$nupkg")"
    pkgid="${base%.nupkg}"
    pkgid="${pkgid%.0.1.0-preview}"
    size="$(wc -c < "$nupkg" | tr -d ' ')"
    echo "  $base  ${size} B"

    echo "    files:"
    unzip -Z1 "$nupkg" | sed 's/^/      /'

    if unzip -l "$nupkg" | grep -qiE "CubismCore\.(dll|so|dylib)|runtimes/"; then
        echo "    error: native library found in package!" >&2
        exit 1
    fi

    if ! unzip -Z1 "$nupkg" | grep -qx "lib/net10.0/${pkgid}.dll"; then
        echo "    error: missing lib/net10.0/${pkgid}.dll" >&2
        exit 1
    fi

    if ! unzip -Z1 "$nupkg" | grep -qx "PACKAGE.md"; then
        echo "    error: missing PACKAGE.md at package root" >&2
        exit 1
    fi

    for xmlEntry in $(unzip -Z1 "$nupkg" | grep -E '^lib/.*\.xml$' || true); do
        if unzip -p "$nupkg" "$xmlEntry" | grep -q "Badly formed XML comment"; then
            echo "    error: $xmlEntry contains 'Badly formed XML comment' placeholders" >&2
            exit 1
        fi
    done
done

if [ "$FULL" -eq 1 ]; then
    actual="$(find "$OUT_DIR" -maxdepth 1 -name '*.nupkg' | wc -l | tr -d ' ')"
    expected="${#PROJECTS[@]}"
    if [ "$actual" -ne "$expected" ]; then
        echo "error: expected $expected packages, found $actual" >&2
        exit 1
    fi
    echo "[pack] verified $actual packages"
fi

echo "[pack] ok"
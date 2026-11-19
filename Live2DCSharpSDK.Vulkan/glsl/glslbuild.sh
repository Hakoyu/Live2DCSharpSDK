#!/bin/bash

# 切到脚本所在目录，保证输入/输出路径与调用时的 cwd 无关（P8a）
scriptDirectory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$scriptDirectory" || exit 1

# 设置输入和输出目录
inputDirectory="."  # 输入目录
# 输出到项目根 spv/，与 csproj 的 EmbeddedResource 路径一致（P8a；原为 ./spv，产物会被 .gitignore 的 /spv 忽略）
outputDirectory="../spv"  # 输出目录

# glslc 解析：优先 VK_SDK_PATH，未设置时回退到本机 Vulkan SDK 1.4.363.0（D-10 / R6）
if [ -n "${VK_SDK_PATH:-}" ] && [ -x "$VK_SDK_PATH/Bin/glslc.exe" ]; then
    glslc="$VK_SDK_PATH/Bin/glslc.exe"
elif [ -x "/c/VulkanSDK/1.4.363.0/Bin/glslc.exe" ]; then
    glslc="/c/VulkanSDK/1.4.363.0/Bin/glslc.exe"
else
    glslc="glslc"
fi

# 确保输出目录存在
if [ ! -d "$outputDirectory" ]; then
    mkdir -p "$outputDirectory"
fi

# 获取所有的 .frag 和 .vert 文件
shaderFiles=$(find "$inputDirectory" -type f \( -name "*.frag" -o -name "*.vert" \))

# 初始化编译后的着色器文件列表
compiledShadersFramework=()

# 遍历每个着色器文件
for shader in $shaderFiles; do
    fileName=$(basename "$shader")

    # 跳过common.glsl文件
    if [ "$fileName" == "common.glsl" ]; then
        continue
    fi

    outputFileName="${fileName%.*}.spv"  # 获取不带扩展名的文件名并添加.spv扩展名
    outputFile="$outputDirectory/$outputFileName"

    # 添加到编译后的着色器列表
    compiledShadersFramework+=("$outputFile")

    # 创建自定义命令以编译着色器
    command="\"$glslc\" -c \"$shader\" -o \"$outputFile\""

    # 执行命令
    eval $command
done

# 输出结果
echo "Compiled Shaders:"
for compiledShader in "${compiledShadersFramework[@]}"; do
    echo "$compiledShader"
done
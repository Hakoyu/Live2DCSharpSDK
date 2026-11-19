# 切到脚本所在目录，保证输入/输出路径与调用时的 cwd 无关（P8a）
Set-Location -Path $PSScriptRoot

# 设置输入和输出目录
$inputDirectory = ".\"  # 输入目录
# 输出到项目根 spv/，与 csproj 的 EmbeddedResource 路径一致（P8a；原为 .\spv\，产物会被 .gitignore 的 /spv 忽略）
$outputDirectory = "..\spv\"    # 输出目录

# glslc 解析：优先 VK_SDK_PATH，未设置时回退到本机 Vulkan SDK 1.4.363.0（D-10 / R6）
$glslc = if ($env:VK_SDK_PATH) { Join-Path $env:VK_SDK_PATH "Bin\glslc.exe" } else { "C:\VulkanSDK\1.4.363.0\Bin\glslc.exe" }

# 确保输出目录存在
if (-Not (Test-Path -Path $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory
}

# 获取所有的 .frag 和 .vert 文件
$shaderFiles = Get-ChildItem -Path $inputDirectory -Recurse -Include *.frag, *.vert

if (-not (Test-Path -Path $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

# 初始化编译后的着色器文件列表
$compiledShadersFramework = @()

foreach ($shader in $shaderFiles) {
    $fileName = $shader.Name
    
    # 跳过common.glsl文件
    if ($fileName -eq "common.glsl") {
        continue
    }
    
    $fullPath = $shader.FullName
    $outputFileName = [System.IO.Path]::GetFileNameWithoutExtension($fileName) + ".spv"
    $outputFile = Join-Path -Path $outputDirectory -ChildPath $outputFileName

    # 添加到编译后的着色器列表
    $compiledShadersFramework += $outputFile

    # 创建自定义命令以编译着色器
    $command = "`"$glslc`" -c `"$fullPath`" -o `"$outputFile`""
    
    # 执行命令
    Invoke-Expression $command
}

# 输出结果
Write-Host "Compiled Shaders:"
$compiledShadersFramework
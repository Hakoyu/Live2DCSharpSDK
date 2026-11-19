# Live2DCSharpSDK.Vulkan

Live2DCSharpSDK 的 **Vulkan 渲染后端**：把 Framework 的 Cubism 渲染器抽象
（`CubismRenderer`）落地为 Vulkan 实现——实例/物理设备/设备/交换链管理、管线与描述符、
模型 UBO/顶点缓冲、遮罩与离屏渲染目标，并内嵌 488 个预编译 SPIR-V 着色器
（对应官方 494 个 program；Add/Mult 复用 Normal 的 6 个基础 shader）。

- 目标框架：`net10.0`（单目标）
- 包内容：托管程序集 + XML 文档 + 本自述文件
- **不含** Live2D Cubism Core 原生库（见 §2）
- 自动带入 `Live2DCSharpSDK.App`（本包公开类型的基类来源，见 §4）

## 1. 依赖

| 依赖 | 版本 | 说明 |
| --- | --- | --- |
| `Live2DCSharpSDK.App` | `0.1.0-preview` | 基础应用层，本包公开类型 `LAppDelegateVulkan` 的基类来源 |
| `Live2DCSharpSDK.Framework` | `0.1.0-preview` | Cubism 核心抽象与渲染器基类，版本号与本包同号 |
| `Silk.NET.Vulkan` | `2.23.0` | Vulkan 绑定 |
| `Silk.NET.Vulkan.Extensions.EXT` | `2.23.0` | 扩展绑定 |
| `Silk.NET.Vulkan.Extensions.KHR` | `2.23.0` | 扩展绑定 |

> 换 Vulkan 窗口宿主的实现（`VulkanApi`、交换链创建）由宿主工程提供，不在本包内。

## 2. 这个包里没有原生库

与 `Live2DCSharpSDK.Framework` 一致：Cubism Core（`Live2DCubismCore.dll`，`06.00.0001` /
Cubism 5 SDK for Native 5-r.5）是 Live2D 的专有二进制品，授权与分发由使用者按
Live2D Proprietary Software License 自行取得，因此**不随任何包分发**。

未放置该原生库时，第一次触碰 Core（加载 `.moc3`、`CubismCore.GetVersion()`）会抛
`DllNotFoundException`/`BadImageFormatException`；纯托管路径（数学、物理、Vulkan 管线构建）不受影响。

**自备方式**：从 <https://www.live2d.com/zh-CHS/sdk/download/native/> 下载 *Cubism SDK for Native*
（以 `5-r.5` 为基准），把 `Core/dll/windows/x86_64/Live2DCubismCore.dll` 复制到宿主可执行文件同目录：

```xml
<ItemGroup>
  <None Include="native\win-x64\Live2DCubismCore.dll"
        Link="Live2DCubismCore.dll"
        CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

`LibraryImport("Live2DCubismCore")` 走 .NET 默认原生库解析，放到应用目录即可解析；
更完整的说明（含 Linux/macOS 文件名、自定义 `DllImportResolver`）见 `Live2DCSharpSDK.Framework` 包的自述文件。

## 3. 从本地私有 feed 安装

```bash
# 1) 打包（仓库根目录），产物落在 artifacts/nuget/
tools/pack.sh

# 2) 引用本包（把 <repo> 换成仓库绝对路径）
dotnet add package Live2DCSharpSDK.Vulkan --version 0.1.0-preview \
  --source "<repo>/artifacts/nuget"
```

或在 `NuGet.config` 登记本地源：

```xml
<configuration>
  <packageSources>
    <add key="live2d-local" value="<repo>/artifacts/nuget" />
  </packageSources>
</configuration>
```

## 4. 与 `Live2DCSharpSDK.App` 的关系

本包的公开类型——`LAppDelegateVulkan`、`LAppViewVulkan`、`TextureInfoVulkan`——派生自
`Live2DCSharpSDK.App` 的 `LAppDelegate` / `LAppView`。因此 **App 是本包的包依赖**，安装时自动还原，
无需手工引用源码工程。

- 只用渲染器抽象（`CubismRenderer_Vulkan`、`VulkanManager`、`CubismBufferVulkan` 等）：本包 + Framework 即可；
- 使用 `LAppDelegateVulkan`：直接使用，基类由自动还原的 App 包提供；
- 想改写示例逻辑（继承 `LAppModel` 等）：引用 App 包即可，其类型均为 public。

## 5. 目标框架与许可

| 项目 | 值 |
| --- | --- |
| TargetFramework | `net10.0` |
| 原生依赖 | `Live2DCubismCore`（自备，见 §2） |
| 许可 | MIT（C# 移植代码）；Live2D Cubism Core 与模型资产另受 Live2D 许可约束 |

> 只在 Windows x64 上验证过（Vulkan 1.3 + `VkLayer_khronos_validation`）。

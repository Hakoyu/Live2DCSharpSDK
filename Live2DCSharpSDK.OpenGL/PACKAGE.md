# Live2DCSharpSDK.OpenGL

[Cubism 5 SDK for Native 5-r.5](https://github.com/Live2D/CubismNativeSamples) 的 C# / .NET 移植 ——
OpenGL / OpenGL ES 2.0 渲染后端（对应仓库 `Live2DCSharpSDK.OpenGL` 工程）：
`CubismRenderer_OpenGLES2`、着色器延迟编译、裁剪遮罩（ClippingMask）、离屏渲染与渲染目标管理。

- 目标框架：`net10.0`（单目标）
- 包内容：托管程序集 + XML 文档 + 本自述文件
- 包依赖：`Live2DCSharpSDK.App` 与 `Live2DCSharpSDK.Framework`（**同版本**，随包自动还原）
- **不含** Live2D Cubism Core 原生库（见下节）

## 1. 依赖关系

| 包 | 说明 |
| --- | --- |
| `Live2DCSharpSDK.App` `0.1.0-preview` | 基础应用层（`LAppDelegate`/`LAppModel`/`LAppView`），本包公开类型 `LAppDelegateOpenGL` 的基类来源，自动带入 |
| `Live2DCSharpSDK.Framework` `0.1.0-preview` | 核心框架层（Core P/Invoke、模型、动作、物理、渲染抽象），经 App 传递带入 |
| `SkiaSharp` `4.157.0` | 纹理解码，经 App 传递带入 |
| `Live2DCubismCore`（原生） | Live2D 官方 Core 二进制，**需自备**，见 §2 |

本包固化了同版本的 Framework 依赖：安装 `Live2DCSharpSDK.OpenGL` 时 NuGet 会一并还原
`Live2DCSharpSDK.Framework`。两者版本必须一致——升级时请同时升级（或用统一的
`PackageReference` 版本变量）。

## 2. 这个包里没有原生库

与 `Live2DCSharpSDK.Framework` 相同，本包通过 Framework 的 `LibraryImport("Live2DCubismCore")`
调用 Live2D 官方 Cubism Core（`06.00.0001`，对应 Cubism 5 SDK for Native 5-r.5）。
Core 原生库是 Live2D 的专有二进制品，授权与分发由使用者按 Live2D Proprietary Software License
自行取得，因此**不随本包（也不随 Framework 包）分发**。

未放置该原生库时，第一次调用 Core（例如加载 `.moc3`、`CubismCore.GetVersion()`）会抛
`DllNotFoundException` / `BadImageFormatException`。

**自备方式（Windows x64，最省事）**

1. 从 <https://www.live2d.com/zh-CHS/sdk/download/native/> 下载 *Cubism SDK for Native*（本移植以 `5-r.5` 为基准）；
2. 取 `Core/dll/windows/x86_64/Live2DCubismCore.dll`；
3. 放到**应用可执行文件同目录**（`AppContext.BaseDirectory`），或随工程复制：

```xml
<ItemGroup>
  <None Include="native\win-x64\Live2DCubismCore.dll"
        Link="Live2DCubismCore.dll"
        CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

其它平台对应文件名为 `libLive2DCubismCore.so`（Linux x86_64）与 `libLive2DCubismCore.dylib`（macOS）。
更完整的说明（含 `NativeLibrary.SetDllImportResolver` 自定义解析）见
`Live2DCSharpSDK.Framework` 包的 `PACKAGE.md`。

## 3. 从本地私有 feed 安装

```bash
# 仓库根目录，打包产物落在 artifacts/nuget/
tools/pack.sh

dotnet add package Live2DCSharpSDK.OpenGL --version 0.1.0-preview \
  --source "<repo>/artifacts/nuget"
```

## 4. 依赖矩阵

| 项目 | 值 |
| --- | --- |
| TargetFramework | `net10.0` |
| 包依赖 | `Live2DCSharpSDK.App` `0.1.0-preview`、`Live2DCSharpSDK.Framework` `0.1.0-preview`（两者同版本；App 单独还原，本包直接声明） |
| NuGet 第三方依赖 | 无直接依赖；`SkiaSharp 4.157.0` 经 `Live2DCSharpSDK.App` 传递还原 |
| 原生依赖 | `Live2DCubismCore`（自备，见 §2） |
| 许可 | MIT（C# 移植代码）；Live2D Cubism Core 与模型资产另受 Live2D 许可约束 |

> 本包只含渲染后端，不含窗口/宿主实现。示例宿主（Avalonia / OpenTK / WPF）在仓库内，不打包。
> 只在 Windows x64 上验证过；其它平台需自备对应 Core 并自行验证。

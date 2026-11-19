# Live2DCSharpSDK

[Cubism 5 SDK for Native 5-r.5](https://github.com/Live2D/CubismNativeSamples) 的 C# / .NET 移植。
以官方原生 SDK(`CubismSdkForNative-5-r.5`,Core `06.00.0001`)为基准,在 .NET 上重写框架层,
并提供 OpenGL / Vulkan 两套渲染后端与四个桌面宿主。

## 解决方案结构

| 项目 | 说明 |
| --- | --- |
| `Live2DCSharpSDK.Framework` | 核心框架层: Core P/Invoke、模型、动作、物理、特效、数学、渲染抽象 |
| `Live2DCSharpSDK.App` | 应用层: 模型加载、纹理管理、视图矩阵、触摸输入、资源定位 |
| `Live2DCSharpSDK.OpenGL` | OpenGL / OpenGL ES2 后端: 着色器延迟编译、裁剪遮罩、离屏渲染 |
| `Live2DCSharpSDK.OpenGL.Avalonia` | 宿主: Avalonia 12(`OpenGlControlBase`) |
| `Live2DCSharpSDK.OpenGL.OpenTK` | 宿主: OpenTK 独立窗口 |
| `Live2DCSharpSDK.OpenGL.WPF` | 宿主: WPF(`GLWpfControl`),`net10.0-windows` |
| `Live2DCSharpSDK.Vulkan` | Vulkan 1.3 后端: 动态渲染、488 个预编译 SPIR-V、494 条管线 |
| `Live2DCSharpSDK.Vulkan.Silk` | 宿主: Silk.NET 独立窗口 |

## 构建

- 需要 .NET 10 SDK(库与多数宿主为 `net10.0`,WPF 宿主为 `net10.0-windows`)。
- 构建整个解决方案: `dotnet build Live2DCSharpSDK.sln`
- 运行任一宿主即可出图,例如 `dotnet run --project Live2DCSharpSDK.OpenGL.Avalonia`。

## 原生库

`Live2DCubismCore.dll`(Core `06.00.0001`)位于
`Live2DCSharpSDK.Framework/runtimes/win-x64/native/`,由 `Framework.csproj` 链接并复制到各宿主输出目录根。
当前只随仓库提供 win-x64 二进制,其它平台需自行放置对应原生库。

## 模型定位

`Live2DCSharpSDK.App/ModelResourceLocator.cs` 按下列顺序解析模型,全部基于相对路径:

1. 环境变量 `LIVE2D_MODEL_DIR`;`LIVE2D_MODEL_NAME` 指定模型名(默认 `Haru`);
2. 从 `AppContext.BaseDirectory` 向上最多 5 层查找 SDK 包内的 `CubismSdkForNative-5-r.5/Samples/Resources`;
3. 可执行文件同级的 `Resources` 目录。

都找不到时输出 Warning 并以空场景启动,不抛异常。

## 验收模型

冒烟与验收使用四个模型: `Haru`、`Mao`、`Ren`(取自官方 `Samples/Resources`)。

## 已知差异

与官方原生实现的差异(K1–K9)、各轮验收证据与修复记录见
`teams/TEAM-live2d-r5/STATUS.md` 与 `teams/TEAM-live2d-r5/verify/`。

## 许可与反馈

许可见 [LICENSE](LICENSE)。

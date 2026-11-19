# Live2DCSharpSDK.App

Live2D Cubism 5 SDK for Native 的 **基础应用层**（`LApp*`）：模型管理、视图、纹理、平台适配。

## 依赖

- `Live2DCSharpSDK.Framework 0.1.0-preview`（自动还原）
- `SkiaSharp 4.157.0`（自动还原）

## 需要自备原生库

本包**不含** `Live2DCubismCore.dll`。使用前请从 Live2D 官方下载 **Cubism SDK for Native 5-r.5**，
取 `Core/dll/windows/x86_64/Live2DCubismCore.dll`，放到以下任一位置：

1. 应用输出目录（`bin/<Configuration>/<TFM>/`）；
2. `<项目>/runtimes/win-x64/native/`（.NET 原生库搜索约定）；
3. 自定义位置 + `NativeLibrary.SetDllImportResolver`。

## 说明

- 本包是**基础层**：只含 `LAppDelegate` / `LAppModel` / `LAppView` / `LAppTextureManager` 等抽象与示例逻辑；
- 具体渲染后端请另装 `Live2DCSharpSDK.OpenGL` 或 `Live2DCSharpSDK.Vulkan`（它们派生自本包的 `LAppDelegate`）；
- 许可证：MIT（本移植）；Cubism Core 与官方 SDK 受 Live2D 的许可约束。
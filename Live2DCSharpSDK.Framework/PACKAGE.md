# Live2DCSharpSDK.Framework

[Cubism 5 SDK for Native 5-r.5](https://github.com/Live2D/CubismNativeSamples) 的 C# / .NET 框架层移植：
Core P/Invoke、模型、动作、物理、特效、数学与渲染抽象（对应仓库 `Live2DCSharpSDK.Framework` 工程）。

- 目标框架：`net10.0`（单目标，无第三方包依赖）
- 包内容：托管程序集 + XML 文档 + 本自述文件
- **不含** Live2D Cubism Core 原生库（见下节）

## 1. 这个包里没有原生库

`Live2DCSharpSDK.Framework` 通过 `LibraryImport("Live2DCubismCore")` 调用 Live2D 官方的 Cubism Core
（`06.00.0001`，对应 Cubism 5 SDK for Native 5-r.5）。Core 原生库是 Live2D 的专有二进制品，
其授权与分发由使用者按 Live2D Proprietary Software License 自行取得，因此**不随本包分发**。

未放置该原生库时，第一次调用 Core（例如 `CubismCore.GetVersion()` 或加载 `.moc3`）会抛
`DllNotFoundException`/`BadImageFormatException`，其余纯托管算法（数学、物理、序列化）不受影响。

## 2. 自备 Core 原生库

**步骤 1：取得官方发布物**

从 <https://www.live2d.com/zh-CHS/sdk/download/native/> 下载 *Cubism SDK for Native*（本移植以
`5-r.5` 为基准）。原生库位于包内的 `Core/dll/`：

| 平台 | 官方路径 | 文件名 |
| --- | --- | --- |
| Windows x64 | `Core/dll/windows/x86_64/` | `Live2DCubismCore.dll` |
| Windows x86 | `Core/dll/windows/x86/` | `Live2DCubismCore.dll` |
| Linux x86_64 | `Core/dll/linux/x86_64/` | `libLive2DCubismCore.so` |
| macOS | `Core/dll/macos/` | `libLive2DCubismCore.dylib`（+ `Live2DCubismCore.bundle`） |

**步骤 2：放到运行时能解析的位置**

`LibraryImport("Live2DCubismCore")` 走 .NET 默认原生库解析：Windows 先查应用目录
（`AppContext.BaseDirectory`），Linux/macOS 走 `ld.so`/`dyld` 搜索路径并按 `lib` 前缀 +
`.so`/`.dylib` 后缀匹配。最省事的做法是**把原生库放到宿主可执行文件同目录**，即
`dotnet build`/`dotnet publish` 的输出根目录。

用 csproj 复制（随生成/发布自动就位，推荐）：

```xml
<ItemGroup>
  <!-- 把本机自备的 Core 复制到输出根目录，文件名必须与 LibraryImport 的库名一致 -->
  <None Include="native\win-x64\Live2DCubismCore.dll"
        Link="Live2DCubismCore.dll"
        CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

发布时如用自包含/单文件，可改用 `runtimes/{rid}/native/` 布局，或显式加：

```xml
<ItemGroup>
  <None Include="native\win-x64\Live2DCubismCore.dll"
        Link="runtimes\win-x64\native\Live2DCubismCore.dll"
        CopyToOutputDirectory="PreserveNewest"
        CopyToPublishDirectory="PreserveNewest" />
</ItemGroup>
```

**步骤 3（可选）：自定义解析**

原生库不在默认搜索路径时（例如放在任意资产目录），在进程启动早期注册解析器：

```csharp
using System.Runtime.InteropServices;

NativeLibrary.SetDllImportResolver(
    typeof(Live2DCSharpSDK.Framework.Core.CubismCore).Assembly,
    (name, assembly, path) => name == "Live2DCubismCore"
        ? NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "native", "Live2DCubismCore.dll"))
        : IntPtr.Zero);
```

> 本仓库开发树里，原生库放在 `Live2DCSharpSDK.Framework/runtimes/win-x64/native/Live2DCubismCore.dll`，
> 由工程文件以 `Link=Live2DCubismCore.dll` 复制到各宿主输出目录；该文件**不会**进入 nupkg。

## 3. 从本地私有 feed 安装

```bash
# 1) 打包（仓库根目录）
tools/pack.sh                       # 产物落在 artifacts/nuget/

# 2) 安装到你的工程（把 <repo> 换成仓库绝对路径）
dotnet add package Live2DCSharpSDK.Framework --version 0.1.0-preview \
  --source "<repo>/artifacts/nuget"
```

或在 `NuGet.config` 中登记本地源，之后按普通包引用：

```xml
<configuration>
  <packageSources>
    <add key="live2d-local" value="<repo>/artifacts/nuget" />
  </packageSources>
</configuration>
```

## 4. 目标框架与依赖

| 项目 | 值 |
| --- | --- |
| TargetFramework | `net10.0` |
| 包依赖 | 无（Framework 不引用任何 NuGet 包） |
| 原生依赖 | `Live2DCubismCore`（自备，见 §2） |
| 许可 | MIT（C# 移植代码）；Live2D Cubism Core 与模型资产另受 Live2D 许可约束 |

> 只在 Windows x64 上验证过（仓库 CI 与验收跑在 win-x64）；其它平台需自备对应 Core 并自行验证。

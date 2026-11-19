# Live2DCSharpSDK.Tests

本目录是 Live2D Cubism SDK 托管层 (Framework + App) 的单元测试工程.

- 框架: MSTest 4.5.3 / Microsoft.NET.Test.Sdk 18.12.0 / coverlet.collector 10.1.1
- 目标框架: net10.0
- 引用: 只有 Live2DCSharpSDK.Framework 与 Live2DCSharpSDK.App. 不引用 OpenGL / Vulkan,
  单元测试层不启动真实 GL.
- 串行: 不启用 `[assembly: Parallelize]`, MSTest 默认串行执行, 因为环境变量,
  `CubismContext.Current` 与 Core 的进程级日志回调都是全局状态.

## 1. 分档

| 档 | 过滤条件 | 依赖 | 内容 |
|---|---|---|---|
| A | `TestCategory!=RequiresCore` | 无, 不加载 Core | 数学 / 矩阵 / 目标点 / 遮罩分组 / 混合模式 / ID 管理器 / 更新调度 / 日志 / 上下文生命周期 (不 StartUp) / 路径 / 分配器 / JSON |
| B | `TestCategory=RequiresCore` | **仅 Windows x64** + `Live2DCubismCore.dll` + moc3 夹具 | moc3 解析 / Dispose 幂等 / Context StartUp 与隔离 |
| C | - | 真实 GL | 不在单元测试层, 由 `verify/` 的 smoke + acceptance 集成门禁覆盖 |

B 档缺少环境时**失败而不是 Skip**, 避免出现 "CI 绿但没跑" 的假象.

## 2. 命令

> **Windows 提示**: 本仓库脚本是 Bash 脚本. 系统里若同时装了 WSL 与 Git Bash,
> 裸 `bash` 可能解析到 `C:\\Windows\\System32\\bash.exe` (WSL), 那里没有 Windows 的 `dotnet`.
> 请显式使用 Git Bash:
> ```powershell
> & 'C:\\Program Files\\Git\\bin\\bash.exe' tools/run-tests.sh all
> ```

```bash
# 在仓库根目录 (Live2DCSharpSDK/) 执行
bash tools/run-tests.sh a       # A 档: 纯托管, 不加载 Core
bash tools/run-tests.sh core    # B 档: 需要 Windows x64 + Core + moc3 夹具
bash tools/run-tests.sh all     # A + B

bash tools/coverage-report.sh a     # A 档覆盖率 (Cobertura, 只报告不阻断)
bash tools/coverage-report.sh all   # A + B 覆盖率

bash tools/run-gate.sh          # 本地门禁: build -> 单元测试 -> 可选集成门禁
```

`run-tests.sh` 在筛选器匹配 0 个用例时按失败处理 (读取 TRX 计数), 防止假绿.
覆盖率低于 80% 只打印 WARN, 退出码仍为 0.

## 3. 环境变量

| 变量 | 必需 | 作用 |
|---|---|---|
| `LIVE2D_TEST_MODELS` | B 档必需 | moc3 夹具根目录, 例如 `D:/AgentGameWorkSpace/CubismSdkForNative-5-r.5/Samples/Resources` |
| `LIVE2D_TEST_TIER` | 由脚本写入 | 本次运行的分档 (`a` / `core` / `all`), `GlobalStateFixture` 据此决定是否强制校验 Core 环境 |
| `TEAM_VERIFY_ROOT` | 可选 | 集成门禁脚本目录; 设置后 `run-gate.sh` 追加 `run-smoke.sh` 与 `assert-acceptance.py` |
| `COVERAGE_RESULTS_DIR` | 可选 | 覆盖率结果目录; 未设置时用临时目录并在日志里打印 XML 路径 |

`GlobalStateFixture` 在 `[AssemblyInitialize]` 记录环境变量快照并在 `[AssemblyCleanup]` 还原;
用例自己修改环境变量或当前目录时必须 `try/finally` 恢复.

## 4. 夹具来源与许可

- 来源: **Cubism 5 SDK for Native 5-r.5** 的 `Samples/Resources` (`Haru/`, `Ren/`),
  仅读取本地 SDK 目录, 不复制进仓库.
- **moc3 不入库** (体积与再分发许可原因). 仓库只保存文本元数据
  `TestData/models.sha256`, 记录相对路径, SHA-256, MocVersion 与各项计数.
- 运行 B 档前需要自备上述 SDK 并设置 `LIVE2D_TEST_MODELS`; 文件缺失或哈希不符时用例
  以 `Assert.Fail` 给出明确消息, 不静默跳过.
- 测试工程与 SDK 一样受 Live2D 的许可约束; 本仓库不重新分发 moc3.

已记录的黄金值 (Core 6.0.0001, csmGetLatestMocVersion = 6):

| 模型 | 大小 | SHA-256 (前 8 / 后 4) | MocVersion | Drawables | Parts | Parameters | Offscreens |
|---|---|---|---|---|---|---|---|
| Haru | 384,704 B | `aa59f7a3` / `2543` | 1 | 84 | 19 | 42 | 0 |
| Ren | 915,200 B | `a9ccf7d6` / `c533` | 6 | 198 | 51 | 73 | 24 |

## 5. 历史回归用例

以下用例固定了曾经修复过的缺陷; 注释里都写明 "修复前会失败" 及原因.

| 缺陷 | 用例 | 修复前会失败的原因 |
|---|---|---|
| `CubismModel` / `CubismMoc` Dispose 不幂等 (double-free) | `Core/CubismModelDisposeCoreTests.DisposeThreeTimes_DoesNotFreeModelMemoryTwice` | 修复前 `Dispose` 只有 `DeallocateAligned(Model)`, 没有幂等标记也不清零; 第二次 Dispose 对同一地址再次释放, 跟踪分配器的 `DoubleFreeCount` 变成 1, 用例失败 |
| `LAppAllocator` 对齐分配用带符号 nint 做取余 | `App/LAppAllocatorTests.AllocateAligned_SatisfiesAlignmentAndReturnsDistinctBlocks`, `AllocateAligned_NormalizesInvalidAlignmentTo16`, `AllocateWithNegativeSize_ReturnsZero`, `DeallocateOfZero_DoesNotThrow` | 修复前 `AllocateAligned(-1,16)` 返回非 0 地址, 非 2 的幂对齐不归一化 (地址不满足 16 字节), `Allocate(-1)` 抛异常而不是返回 0, `DeallocateAligned(0)` 读取 `preamble[-1]` 触发访问违例 |
| model3.json 缺 `FadeInTime` 读成 0 | `Json/ModelSettingObjTests.MissingFadeTimes_DefaultToMinusOne` | 修复前 DTO 只有 `[DefaultValue(-1f)]`, System.Text.Json (含 source-gen) 不把它当缺省值, 未记录的 `FadeInTime` 反序列化为 0, 断言 `-1f` 失败 |

修复提交: `9e71896` (Dispose 幂等), `ed946bd` (Allocator 改用 NativeMemory), `dd96368` (FadeInTime 默认值).
T7 的回归证据探针 (work/_t7/regress-probe) 用修复前的代码原文复现了上述失败.

## 6. 目录

```
Live2DCSharpSDK.Tests/
  GlobalStateFixture.cs        # 程序集级环境快照 / B 档环境校验
  SmokeTests.cs                # 宿主启动 / 只引用托管工程 / IVT
  Math/ Rendering/ Id/ Motion/ Logging/ Context/ App/ Json/ Core/
  TestData/models.sha256       # moc3 夹具指纹 (moc3 本体不入库)
```

- `Core/` 下的用例都是 `RequiresCore`, 只在 B 档运行.
- `Context/` 的 A 档用例使用测试程序集内的反射钩子操作 `CubismContext.Current`, 不调用 `StartUp`.
- 测试程序的 `InternalsVisibleTo` 只用于 AsyncLocal 钩子, 调度器调用钩子, IdManager 计数与 MaskGroup 纯逻辑入口.

## 7. 覆盖率与门禁

- `tools/coverage-report.sh` 用 coverlet 生成 Cobertura, 按 allowlist
  (CubismMath / Matrix44 / TargetPoint / MaskGroupUtility / CsmBlendMode / IdManager /
  UpdateScheduler / Context / ModelResourceLocator / LAppAllocator / ModelSettingObj)
  输出行覆盖率与未覆盖行号; 低于 80% 只 WARN.
- `tools/run-gate.sh`: `build` -> `run-tests.sh all` -> 若设置了 `TEAM_VERIFY_ROOT` 且存在
  `run-smoke.sh` / `assert-acceptance.py` 则追加集成门禁; 否则明确打印跳过原因并返回 0.

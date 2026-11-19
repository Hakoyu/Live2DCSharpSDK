# TestData

夹具目录. moc3 模型本体**不入库** (许可与体积原因, 见 PLAN-06 §3).

- 模型根目录由环境变量 `LIVE2D_TEST_MODELS` 指定, 例如:
  `D:/AgentGameWorkSpace/CubismSdkForNative-5-r.5/Samples/Resources`
- B 档 (`TestCategory=RequiresCore`) 启动时校验该变量, `Haru/Haru.moc3`, `Ren/Ren.moc3` 与 SHA-256.
- 本目录只放文本型夹具元数据 (例如 `models.sha256`), 随测试输出复制.

## models.sha256

每行一条夹具记录, `#` 开头为注释:

```
<相对路径> sha256=<小写十六进制> mocVersion=<n> drawables=<n> parts=<n> parameters=<n> offscreens=<n>
```

- B1 用例在解析 moc3 之前校验 `sha256`; 文件缺失或哈希不符时 `Assert.Fail`, 不跳过.
- 换 SDK 版本或模型时同步重算: `sha256sum Haru/Haru.moc3`; `mocVersion` 与各项计数可由 `CubismMoc` 输出.
- 当前记录 (Cubism Core 6.0.0001, `csmGetLatestMocVersion=6`):
  Haru `mocVersion=1` / 84 drawables, Ren `mocVersion=6` / 198 drawables.

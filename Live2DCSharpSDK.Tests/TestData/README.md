# TestData

夹具目录. moc3 模型本体**不入库** (许可与体积原因, 见 PLAN-06 §3).

- 模型根目录由环境变量 `LIVE2D_TEST_MODELS` 指定, 例如:
  `D:/AgentGameWorkSpace/CubismSdkForNative-5-r.5/Samples/Resources`
- B 档 (`TestCategory=RequiresCore`) 启动时校验该变量, `Haru/Haru.moc3`, `Ren/Ren.moc3` 与 SHA-256.
- 本目录只放文本型夹具元数据 (例如 `models.sha256`), 随测试输出复制.

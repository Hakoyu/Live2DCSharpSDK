using System.Text.Json;
using Live2DCSharpSDK.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Live2DCSharpSDK.Tests.Json;

/// <summary>
/// A12: model3.json 的缺省值契约 (<see cref="ModelSettingObj"/>).
///
/// 这是历史 bug 的回归用例: 早期实现只依赖 [DefaultValue(-1f)], 而 System.Text.Json
/// (含 source-gen) 不会把该特性用于反序列化的缺省值, 未记录的 FadeInTime 会被读成 0,
/// 进而在加载动作时把淡入时长误当成 0 秒. 修复后 DTO 用属性初始化器显式把默认值设为 -1f,
/// 本文件把该行为固定下来, 同时确保缺少 Motions 时访问器不抛 NullReferenceException.
/// 全部用例只做 JSON 解析, 不加载 Core, 属于 A 档.
/// </summary>
[TestClass]
public sealed class ModelSettingObjTests
{
    /// <summary>
    /// 动作里没有 FadeInTime / FadeOutTime 的 model3.json.
    /// </summary>
    private const string JsonWithoutFadeTimes = """
        {
          "Version": 3,
          "FileReferences": {
            "Moc": "Test.moc3",
            "Textures": [ "Test.tex.png" ],
            "Motions": {
              "Idle": [
                { "File": "idle_00.motion3.json" }
              ]
            }
          }
        }
        """;

    /// <summary>
    /// 动作里显式记录 FadeInTime 为 0 的 model3.json (最容易被 -1 默认值覆盖的边界).
    /// </summary>
    private const string JsonWithZeroFadeInTime = """
        {
          "Version": 3,
          "FileReferences": {
            "Moc": "Test.moc3",
            "Textures": [ "Test.tex.png" ],
            "Motions": {
              "Idle": [
                { "File": "idle_00.motion3.json", "FadeInTime": 0 }
              ]
            }
          }
        }
        """;

    /// <summary>
    /// 动作里显式记录 FadeInTime 为 1.5 的 model3.json.
    /// </summary>
    private const string JsonWithOnePointFiveFadeInTime = """
        {
          "Version": 3,
          "FileReferences": {
            "Moc": "Test.moc3",
            "Textures": [ "Test.tex.png" ],
            "Motions": {
              "Idle": [
                { "File": "idle_00.motion3.json", "FadeInTime": 1.5 }
              ]
            }
          }
        }
        """;

    /// <summary>
    /// 没有任何动作组的 model3.json.
    /// </summary>
    private const string JsonWithoutMotions = """
        {
          "Version": 3,
          "FileReferences": {
            "Moc": "Test.moc3",
            "Textures": [ "Test.tex.png" ]
          }
        }
        """;

    /// <summary>
    /// 连 FileReferences 都没有的 model3.json (最极端的缺省情况).
    /// </summary>
    private const string JsonWithoutFileReferences = """
        {
          "Version": 3
        }
        """;

    /// <summary>
    /// 缺少 FadeInTime 时必须读成 -1 (修复前会读到 0).
    /// </summary>
    [TestMethod]
    public void MissingFadeTimes_DefaultToMinusOne()
    {
        ModelSettingObj setting = Load(JsonWithoutFadeTimes);
        ModelSettingObj.FileReference.Motion motion = setting.FileReferences.Motions["Idle"][0];

        // 修复前 FadeInTime 会被反序列化成 0, 这两个断言会失败.
        Assert.AreEqual(-1f, motion.FadeInTime, 0f, "The DTO default must stay -1 (regression: it used to be 0).");
        Assert.AreEqual(-1f, setting.GetMotionFadeInTimeValue("Idle", 0), 0f,
            "A missing FadeInTime must be reported as -1.");
        Assert.AreEqual(-1f, setting.GetMotionFadeOutTimeValue("Idle", 0), 0f,
            "A missing FadeOutTime must be reported as -1.");
    }

    /// <summary>
    /// 显式记录的 0 必须保留为 0, 不能被 -1 默认值覆盖.
    /// </summary>
    [TestMethod]
    public void ExplicitZeroFadeInTime_StaysZero()
    {
        ModelSettingObj setting = Load(JsonWithZeroFadeInTime);
        ModelSettingObj.FileReference.Motion motion = setting.FileReferences.Motions["Idle"][0];

        Assert.AreEqual(0f, motion.FadeInTime, 0f, "An explicitly recorded 0 must be preserved.");
        Assert.AreEqual(0f, setting.GetMotionFadeInTimeValue("Idle", 0), 0f,
            "An explicitly recorded 0 must not fall back to -1.");
    }

    /// <summary>
    /// 显式记录的非 0 值按原值解析.
    /// </summary>
    [TestMethod]
    public void ExplicitFadeInTime_IsParsed()
    {
        ModelSettingObj setting = Load(JsonWithOnePointFiveFadeInTime);

        Assert.AreEqual(1.5f, setting.GetMotionFadeInTimeValue("Idle", 0), 1e-6f);
    }

    /// <summary>
    /// 缺少 Motions 时动作访问器返回安全的缺省值, 不抛 NullReferenceException.
    /// </summary>
    [TestMethod]
    public void MissingMotions_DoesNotThrow()
    {
        ModelSettingObj setting = Load(JsonWithoutMotions);

        AssertMissingMotionAccessors(setting);
    }

    /// <summary>
    /// 缺少 FileReferences 时动作访问器同样返回安全的缺省值.
    /// </summary>
    [TestMethod]
    public void MissingFileReferences_DoesNotThrow()
    {
        ModelSettingObj setting = Load(JsonWithoutFileReferences);

        AssertMissingMotionAccessors(setting);
    }

    /// <summary>
    /// 用与产品代码相同的 source-gen 上下文反序列化 (见 LAppModel).
    /// </summary>
    /// <param name="json">model3.json 的内容</param>
    /// <returns>反序列化结果</returns>
    private static ModelSettingObj Load(string json)
    {
        ModelSettingObj? setting = JsonSerializer.Deserialize(json, ModelSettingObjContext.Default.ModelSettingObj);

        return setting ?? throw new InvalidOperationException("The test model settings JSON could not be deserialized.");
    }

    /// <summary>
    /// 断言缺省动作组时各访问器的安全返回值.
    /// </summary>
    /// <param name="setting">模型设置</param>
    private static void AssertMissingMotionAccessors(ModelSettingObj setting)
    {
        Assert.AreEqual(0, setting.GetMotionCount("Idle"));
        Assert.IsNull(setting.GetMotionFileName("Idle", 0));
        Assert.AreEqual(-1f, setting.GetMotionFadeInTimeValue("Idle", 0), 0f,
            "A missing motion must be reported as -1.");
        Assert.IsFalse(setting.IsExistEyeBlinkParameters());
        Assert.IsFalse(setting.IsExistLipSyncParameters());
    }
}

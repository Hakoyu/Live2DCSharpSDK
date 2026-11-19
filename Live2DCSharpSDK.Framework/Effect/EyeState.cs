namespace Live2DCSharpSDK.Framework.Effect;

public enum EyeState
{
    /// <summary>
    /// 初始状态
    /// </summary>
    First = 0,
    /// <summary>
    /// 未眨眼状态
    /// </summary>
    Interval,
    /// <summary>
    /// 眼睑闭合过程中的状态
    /// </summary>
    Closing,
    /// <summary>
    /// 眼睑已闭合状态
    /// </summary>
    Closed,
    /// <summary>
    /// 眼睑睁开过程中的状态
    /// </summary>
    Opening
};

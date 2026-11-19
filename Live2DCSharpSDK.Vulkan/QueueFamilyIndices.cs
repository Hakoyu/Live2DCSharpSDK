namespace Live2DCSharpSDK.Vulkan;

public class QueueFamilyIndices
{
    // 绘制用与显示用的队列族未必一致, 因此分开保存

    /// <summary>
    /// 用于绘制命令的队列族
    /// </summary>
    public int GraphicsFamily = -1;
    /// <summary>
    /// 用于显示的队列族
    /// </summary>
    public int PresentFamily = -1;

    /// <summary>
    /// 是否存在对应的队列族
    /// </summary>
    /// <returns></returns>
    public bool IsComplete()
    {
        return GraphicsFamily != -1 && PresentFamily != -1;
    }
}

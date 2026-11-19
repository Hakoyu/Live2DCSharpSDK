using Live2DCSharpSDK.App;

namespace Live2DCSharpSDK.Vulkan;

public class LAppViewVulkan(LAppDelegateVulkan lapp) : LAppView(lapp)
{
    public override void RenderPost()
    {
    }

    /// <summary>
    /// 背景清理由渲染器侧执行. 
    /// 这里只 Submit 一次空命令, 以便在首次绘制前消耗已 acquire 图像的 semaphore
    /// (旧实现的 clear 缺少布局转换, 曾是 validation error 的成因).
    /// </summary>
    public override unsafe void RenderPre()
    {
        var vkManager = lapp.VulkanManager;
        var commandBuffer = vkManager.BeginSingleTimeCommands();
        vkManager.SubmitCommand(commandBuffer, true);
    }
}

using Live2DCSharpSDK.App;

namespace Live2DCSharpSDK.Vulkan;

public class LAppViewVulkan(LAppDelegateVulkan lapp) : LAppView(lapp)
{
    public override void RenderPost()
    {
    }

    /// <summary>
    /// 背景クリアはレンダラ側で行う。ここでは acquire 済みイメージのセマフォを
    /// 最初の描画前に消費するための空コマンドを 1 回だけ Submit する
    /// （旧実装の clear はレイアウト遷移なしで validation error の原因だった）。
    /// </summary>
    public override unsafe void RenderPre()
    {
        var vkManager = lapp.VulkanManager;
        var commandBuffer = vkManager.BeginSingleTimeCommands();
        vkManager.SubmitCommand(commandBuffer, true);
    }
}

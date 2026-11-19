using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Rendering;
using Silk.NET.Vulkan;

namespace Live2DCSharpSDK.Vulkan;

public class CubismClippingManager_Vulkan(Vk vk) : CubismClippingManager
{
    public override RenderType RenderType => RenderType.Vulkan;

    public override unsafe CubismClippingContext CreateClippingContext(
        CubismClippingManager manager,
        CubismModel model,
        int* clippingDrawableIndices,
        int clipCount
    )
    {
        return new CubismClippingContext_Vulkan(manager, clippingDrawableIndices, clipCount);
    }

    /// <summary>
    /// 创建裁剪上下文. 在绘制模型时执行.
    /// </summary>
    /// <param name="model">模型实例</param>
    /// <param name="commandBuffer">命令缓冲区</param>
    /// <param name="updateCommandBuffer">更新用命令缓冲区</param>
    /// <param name="renderer">渲染器实例</param>
    public unsafe void SetupClippingContext(
        CubismModel model,
        CommandBuffer commandBuffer,
        CommandBuffer updateCommandBuffer,
        CubismRenderer_Vulkan renderer
    )
    {
        // 准备所有裁剪
        // 使用同一裁剪(多个时合并为一个裁剪)时只设置一次
        int usingClipCount = 0;

        for (int clipIndex = 0; clipIndex < ClippingContextListForMask.Count; clipIndex++)
        {
            // 针对一个裁剪遮罩
            var cc = ClippingContextListForMask[clipIndex];

            // 计算包围使用该裁剪的全部绘制对象的矩形
            CalcClippedDrawTotalBounds(model, cc);

            if (cc.IsUsing)
            {
                usingClipCount++; // 计入使用中的数量
            }
        }

        if (usingClipCount <= 0)
        {
            return;
        }

        // 遮罩创建处理
        // 为后续计算设置索引的起始值
        // Framework 的 CurrentMaskBuffer 已改为 CubismRenderTarget 类型, 由此删除了此前的私有字段.
        CurrentMaskBuffer = renderer.GetMaskBufferForType(Type, 0);

        var buffer = (CubismRenderTarget_Vulkan)CurrentMaskBuffer;

        // 1 为无效(不绘制)区域, 0 为有效(绘制)区域. (在 shader 中用 Cd*Cs 乘以接近 0 的值生成遮罩. 乘以 1 则不会发生任何变化)
        if (buffer.BeginDraw(commandBuffer, 1.0f, 1.0f, 1.0f, 1.0f) is false)
        {
            // 遮罩缓冲区分配失败时为无效目标, 跳过本帧遮罩生成而不是在未开启的渲染通道里提交绘制.
            CubismLog.Warning(
                "[Live2D Vulkan]mask render target is invalid, skip clipping mask setup."
            );
            return;
        }

        // 以与生成的 FrameBuffer 相同的尺寸设置 viewport
        var viewport = CubismRenderer_Vulkan.GetViewport(
            ClippingMaskBufferSize.X,
            ClippingMaskBufferSize.Y,
            0.0f,
            1.0f
        );
        vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);
        var rect = CubismRenderer_Vulkan.GetScissor(
            0.0f,
            0.0f,
            ClippingMaskBufferSize.X,
            ClippingMaskBufferSize.Y
        );
        vk.CmdSetScissor(commandBuffer, 0, 1, &rect);

        // 逐个决定各遮罩的布局
        SetupLayoutBounds(usingClipCount);

        // 尺寸与渲染纹理张数不一致时进行调整
        if (ClearedMaskBufferFlags.Count != RenderTextureCount)
        {
            ClearedMaskBufferFlags.Clear();

            for (int i = 0; i < RenderTextureCount; ++i)
            {
                ClearedMaskBufferFlags.Add(false);
            }
        }
        else
        {
            // 每帧开始时初始化遮罩的清除标志
            for (int i = 0; i < RenderTextureCount; ++i)
            {
                ClearedMaskBufferFlags[i] = false;
            }
        }

        // 实际生成遮罩
        // 决定所有遮罩如何布局绘制, 并记录到 ClipContext, ClippedDrawContext 中
        for (int clipIndex = 0; clipIndex < ClippingContextListForMask.Count; clipIndex++)
        {
            // --- 实际绘制一个遮罩 ---
            var clipContext = (
                ClippingContextListForMask[clipIndex] as CubismClippingContext_Vulkan
            )!;
            var allClippedDrawRect = clipContext.AllClippedDrawRect; // 使用该遮罩的全部绘制对象在逻辑坐标上的包围矩形
            var layoutBoundsOnTex01 = clipContext.LayoutBounds; // 把遮罩放入其中
            var MARGIN = 0.05f;

            // 按索引获取设置到 clipContext 的离屏 surface
            var clipContextOffscreenSurface = renderer.GetMaskBufferForType(
                Type,
                clipContext.BufferIndex
            )!;

            // 当前离屏 surface 与 clipContext 的不同时
            if (CurrentMaskBuffer != clipContextOffscreenSurface)
            {
                buffer.EndDraw(commandBuffer);
                CurrentMaskBuffer = clipContextOffscreenSurface;
                buffer = (CubismRenderTarget_Vulkan)CurrentMaskBuffer;
                // 把遮罩用 RenderTexture 设为 active
                if (buffer.BeginDraw(commandBuffer, 1.0f, 1.0f, 1.0f, 1.0f) is false)
                {
                    CubismLog.Warning(
                        "[Live2D Vulkan]mask render target is invalid, skip clipping mask setup."
                    );
                    return;
                }
            }

            // 使用模型坐标上的矩形, 并适当附加 margin
            TmpBoundsOnModel.SetRect(allClippedDrawRect);
            TmpBoundsOnModel.Expand(
                allClippedDrawRect.Width * MARGIN,
                allClippedDrawRect.Height * MARGIN
            );
            //########## 本来不应使用分配区域的全部, 而应取所需的最小尺寸
            // 求出 shader 用的计算式. 不考虑旋转时如下
            // movePeriod' = movePeriod * scaleX + offX     [[ movePeriod' = (movePeriod - tmpBoundsOnModel.movePeriod)*scale + layoutBoundsOnTex01.movePeriod ]]
            var scaleX = layoutBoundsOnTex01.Width / TmpBoundsOnModel.Width;
            var scaleY = layoutBoundsOnTex01.Height / TmpBoundsOnModel.Height;

            // 求出生成遮罩时使用的矩阵
            CreateMatrixForMask(false, layoutBoundsOnTex01, scaleX, scaleY);

            clipContext.MatrixForMask.SetMatrix(TmpMatrixForMask);
            clipContext.MatrixForDraw.SetMatrix(TmpMatrixForDraw);

            // 执行实际绘制
            int clipDrawCount = clipContext.ClippingIdCount;
            for (int i = 0; i < clipDrawCount; i++)
            {
                int clipDrawIndex = clipContext.ClippingIdList[i];

                // 顶点信息未更新, 不可靠时跳过绘制
                if (!model.GetDrawableDynamicFlagVertexPositionsDidChange(clipDrawIndex))
                {
                    continue;
                }

                renderer.IsCulling = model.GetDrawableCulling(clipDrawIndex) != 0;

                // 渲染通道开始时遮罩已被清除, 因此无需再次清除
                // 应用本次专用的变换进行绘制
                // 还需要切换通道(A,R,G,B)
                renderer.ClippingContextBufferForMask = clipContext;
                renderer.DrawMeshVulkan(model, clipDrawIndex, commandBuffer, updateCommandBuffer);
            }
        }
        // --- 后处理 ---
        buffer.EndDraw(commandBuffer);
        renderer.ClippingContextBufferForMask = null;
    }
}

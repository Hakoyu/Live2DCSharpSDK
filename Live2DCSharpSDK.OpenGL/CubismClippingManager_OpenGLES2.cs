using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Core;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Rendering;
using Live2DCSharpSDK.Framework.Type;

namespace Live2DCSharpSDK.OpenGL;

/// <summary>
/// 执行 Clipping Mask 处理的类
/// (官方 OpenGL/CubismRenderer_OpenGLES2.hpp:62-76 / .cpp:35-194).
/// Drawable / Offscreen 的区分由 manager 的 Type 属性完成.
/// </summary>
public class CubismClippingManager_OpenGLES2(OpenGLApi gl) : CubismClippingManager
{
    public override RenderType RenderType => RenderType.OpenGL;

    /// <summary>
    /// 复用的 MVP 逆矩阵缓冲 (渲染器为单线程使用, 避免每次裁剪分配).
    /// </summary>
    private readonly CubismMatrix44 _invertMvpBuffer = new();

    public override unsafe CubismClippingContext CreateClippingContext(
        CubismClippingManager manager,
        CubismModel model,
        int* clippingDrawableIndices,
        int clipCount
    )
    {
        // 派生上下文不接收 model 参数(与基类 / Vulkan 侧保持同形, 详见 CubismClippingContext_OpenGLES2 的 remarks).
        return new CubismClippingContext_OpenGLES2(manager, clippingDrawableIndices, clipCount);
    }

    /// <summary>
    /// 创建 Clipping 上下文. 在模型绘制时执行.
    /// </summary>
    /// <param name="model">模型实例</param>
    /// <param name="renderer">渲染器实例</param>
    /// <param name="lastFBO">帧缓冲</param>
    /// <param name="lastViewport">视口</param>
    internal unsafe void SetupClippingContext(
        CubismModel model,
        CubismRenderer_OpenGLES2 renderer,
        int lastFBO,
        int[] lastViewport
    )
    {
        // 准备所有的 Clipping
        // 使用相同 Clip (多个时合并为一个 Clip) 时只设置一次
        int usingClipCount = 0;
        for (int clipIndex = 0; clipIndex < ClippingContextListForMask.Count; clipIndex++)
        {
            // 针对一个 Clipping Mask
            var cc = ClippingContextListForMask[clipIndex];

            // 计算包围使用该 Clip 的全部绘制对象的矩形
            CalcClippedTotalBounds(model, cc, Type);

            if (cc.IsUsing)
            {
                usingClipCount++; // 计数为使用中
            }
        }

        if (usingClipCount <= 0)
        {
            return;
        }

        // 遮罩用渲染目标创建失败时, 不再向该缓冲区绘制任何内容.
        //      RenderTexture == 0 时若执行 BeginDraw / PreDraw / Clear, BeginDraw 会被忽略,
        //      从而对当前绑定的 FBO (画面或父 FBO) 执行 Clear 与 Mask 绘制.
        //      因此先选择 RT 有效的第一个缓冲作为初始缓冲 (替代原实现中固定使用缓冲 0 的做法).
        int initialBufferIndex = -1;
        for (int clipIndex = 0; clipIndex < ClippingContextListForMask.Count; clipIndex++)
        {
            var clipContext = ClippingContextListForMask[clipIndex];

            if (renderer.IsMaskBufferUsable(Type, clipContext.BufferIndex))
            {
                initialBufferIndex = clipContext.BufferIndex;
                break;
            }
        }

        if (initialBufferIndex < 0)
        {
            // 没有任何可用的 Mask 用 RT (创建失败): 直接跳过 Mask 生成本身
            return;
        }

        // Mask 创建处理
        // 按与生成的 RenderTarget 相同的尺寸设置视口
        gl.Viewport(0, 0, (int)ClippingMaskBufferSize.X, (int)ClippingMaskBufferSize.Y);

        // 为后续计算设置索引的开头
        CubismRenderTarget_OpenGLES2 currentMaskBuffer =
            Type == CsmEnum.DrawableObjectType.Offscreen
                ? renderer.GetOffscreenMaskBuffer(initialBufferIndex)
                : renderer.GetDrawableMaskBuffer(initialBufferIndex);
        CurrentMaskBuffer = currentMaskBuffer;

        // ----- Mask 绘制处理 -----
        currentMaskBuffer.BeginDraw(lastFBO);

        renderer.PreDraw(); // 清除缓冲

        // 逐个决定各 Mask 的布局
        SetupLayoutBounds(usingClipCount);

        // 尺寸与渲染纹理张数不一致时使其一致
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
            // 每帧开始时初始化 Mask 的清除标志
            for (int i = 0; i < RenderTextureCount; ++i)
            {
                ClearedMaskBufferFlags[i] = false;
            }
        }

        // 实际生成 Mask
        // 决定如何布局并绘制所有 Mask, 并记录到 ClipContext, ClippedDrawContext 中
        for (int clipIndex = 0; clipIndex < ClippingContextListForMask.Count; clipIndex++)
        {
            // --- 实际绘制一个 Mask ---
            var clipContext = ClippingContextListForMask[clipIndex];

            // 该遮罩用渲染目标未创建成功时, 不执行 Clear / Viewport / 遮罩绘制.
            if (renderer.IsMaskBufferUsable(Type, clipContext.BufferIndex) is false)
            {
                continue;
            }

            RectF allClippedDrawRect = clipContext.AllClippedDrawRect; // 使用该 Mask 的全部绘制对象在逻辑坐标上的包围矩形
            RectF layoutBoundsOnTex01 = clipContext.LayoutBounds; // 将 Mask 收于其中
            float MARGIN = 0.05f;

            // 按索引取得 clipContext 中设置的渲染目标
            CubismRenderTarget_OpenGLES2 maskBuffer =
                Type == CsmEnum.DrawableObjectType.Offscreen
                    ? renderer.GetOffscreenMaskBuffer(clipContext.BufferIndex)
                    : renderer.GetDrawableMaskBuffer(clipContext.BufferIndex);

            // 当前渲染目标与 clipContext 的不一致时
            if (!ReferenceEquals(currentMaskBuffer, maskBuffer))
            {
                currentMaskBuffer.EndDraw();
                currentMaskBuffer = maskBuffer;
                CurrentMaskBuffer = currentMaskBuffer;

                // 将用于 Mask 的 RenderTexture 设为 active
                currentMaskBuffer.BeginDraw(lastFBO);

                // 清除缓冲.
                renderer.PreDraw();
            }

            // 在模型坐标上的矩形上适当添加边距后使用
            TmpBoundsOnModel.SetRect(allClippedDrawRect);
            TmpBoundsOnModel.Expand(
                allClippedDrawRect.Width * MARGIN,
                allClippedDrawRect.Height * MARGIN
            );
            //########## 本来应使用必要的最小尺寸, 而不是分配区域的全部
            // 求 Shader 用的计算公式. 不考虑旋转时如下
            // movePeriod' = movePeriod * scaleX + offX     [[ movePeriod' = (movePeriod - tmpBoundsOnModel.movePeriod)*scale + layoutBoundsOnTex01.movePeriod ]]
            float scaleX = layoutBoundsOnTex01.Width / TmpBoundsOnModel.Width;
            float scaleY = layoutBoundsOnTex01.Height / TmpBoundsOnModel.Height;

            // 求生成 Mask 时使用的矩阵
            CreateMatrixForMask(false, layoutBoundsOnTex01, scaleX, scaleY);

            clipContext.MatrixForMask.SetMatrix(TmpMatrixForMask.Tr);
            clipContext.MatrixForDraw.SetMatrix(TmpMatrixForDraw.Tr);

            if (Type == CsmEnum.DrawableObjectType.Offscreen)
            {
                // clipContext * mvp^-1 (官方 .cpp:151-156)
                CubismMatrix44 invertMvp = CalcInvertMvp(renderer.GetMvpMatrix());
                clipContext.MatrixForDraw.MultiplyByMatrix(invertMvp);
            }

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

                // Mask 尚未清除时进行处理
                if (!ClearedMaskBufferFlags[clipContext.BufferIndex])
                {
                    // 清除 Mask
                    // 1 为无效 (不被绘制) 区域, 0 为有效 (被绘制) 区域. (用 Shader 的 Cd*Cs 乘以接近 0 的值生成 Mask, 乘以 1 则什么都不会发生)
                    gl.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);
                    gl.Clear(gl.GL_COLOR_BUFFER_BIT);
                    ClearedMaskBufferFlags[clipContext.BufferIndex] = true;
                }

                // 应用本次专用的变换后绘制
                // 还需要切换通道(A,R,G,B)
                renderer.ClippingContextBufferForMask = clipContext;

                renderer.DrawMeshOpenGL(model, clipDrawIndex);
            }
        }

        // --- 后处理 ---
        currentMaskBuffer.EndDraw();
        renderer.ClippingContextBufferForMask = null;
        gl.Viewport(lastViewport[0], lastViewport[1], lastViewport[2], lastViewport[3]);
    }

    /// <summary>
    /// 求 MVP 矩阵的逆矩阵并写入复用的成员缓冲 (与 Framework 的 InvertMatrix44 实现相同).
    /// 与 SetupMatrixForHighPrecision 不同, 此处需要直接反转渲染器矩阵, 因此复制一份.
    /// </summary>
    /// <param name="src">要反转的矩阵</param>
    /// <returns>复用的逆矩阵缓冲 (调用方只读取, 不得持有)</returns>
    private CubismMatrix44 CalcInvertMvp(CubismMatrix44 src)
    {
        float[] tr = src.Tr;
        float r00 = tr[0],
            r10 = tr[1],
            r20 = tr[2];
        float r01 = tr[4],
            r11 = tr[5],
            r21 = tr[6];
        float r02 = tr[8],
            r12 = tr[9],
            r22 = tr[10];
        float tx = tr[12],
            ty = tr[13],
            tz = tr[14];

        float det =
            r00 * (r11 * r22 - r12 * r21)
            - r01 * (r10 * r22 - r12 * r20)
            + r02 * (r10 * r21 - r11 * r20);

        CubismMatrix44 dst = _invertMvpBuffer;
        if (MathF.Abs(det) < CubismMath.Epsilon)
        {
            dst.LoadIdentity();
            return dst;
        }

        float invDet = 1.0f / det;
        float inv00 = (r11 * r22 - r12 * r21) * invDet;
        float inv01 = -(r01 * r22 - r02 * r21) * invDet;
        float inv02 = (r01 * r12 - r02 * r11) * invDet;
        float inv10 = -(r10 * r22 - r12 * r20) * invDet;
        float inv11 = (r00 * r22 - r02 * r20) * invDet;
        float inv12 = -(r00 * r12 - r02 * r10) * invDet;
        float inv20 = (r10 * r21 - r11 * r20) * invDet;
        float inv21 = -(r00 * r21 - r01 * r20) * invDet;
        float inv22 = (r00 * r11 - r01 * r10) * invDet;

        float[] d = dst.Tr;
        d[0] = inv00;
        d[1] = inv10;
        d[2] = inv20;
        d[3] = 0.0f;
        d[4] = inv01;
        d[5] = inv11;
        d[6] = inv21;
        d[7] = 0.0f;
        d[8] = inv02;
        d[9] = inv12;
        d[10] = inv22;
        d[11] = 0.0f;
        d[12] = -(inv00 * tx + inv01 * ty + inv02 * tz);
        d[13] = -(inv10 * tx + inv11 * ty + inv12 * tz);
        d[14] = -(inv20 * tx + inv21 * ty + inv22 * tz);
        d[15] = 1.0f;

        return dst;
    }
}

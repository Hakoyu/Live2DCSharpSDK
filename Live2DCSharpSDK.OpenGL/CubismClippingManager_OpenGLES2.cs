using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Core;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Rendering;
using Live2DCSharpSDK.Framework.Type;

namespace Live2DCSharpSDK.OpenGL;

/// <summary>
/// クリッピングマスクの処理を実行するクラス
/// （官方 OpenGL/CubismRenderer_OpenGLES2.hpp:62-76 / .cpp:35-194）。
/// D-08: Drawable / Offscreen の区別は manager の Type プロパティで行う。
/// </summary>
public class CubismClippingManager_OpenGLES2(OpenGLApi gl) : CubismClippingManager
{
    public override RenderType RenderType => RenderType.OpenGL;

    public override unsafe CubismClippingContext CreateClippingContext(CubismClippingManager manager, CubismModel model,
        int* clippingDrawableIndices, int clipCount)
    {
        // v7: 派生コンテキストは model を受け取らない（基底 / Vulkan 側と同形。詳細は CubismClippingContext_OpenGLES2 の remarks）。
        return new CubismClippingContext_OpenGLES2(manager, clippingDrawableIndices, clipCount);
    }

    /// <summary>
    /// クリッピングコンテキストを作成する。モデル描画時に実行する。
    /// </summary>
    /// <param name="model">モデルのインスタンス</param>
    /// <param name="renderer">レンダラのインスタンス</param>
    /// <param name="lastFBO">フレームバッファ</param>
    /// <param name="lastViewport">ビューポート</param>
    internal unsafe void SetupClippingContext(CubismModel model, CubismRenderer_OpenGLES2 renderer, int lastFBO, int[] lastViewport)
    {
        // 全てのクリッピングを用意する
        // 同じクリップ（複数の場合はまとめて１つのクリップ）を使う場合は１度だけ設定する
        int usingClipCount = 0;
        for (int clipIndex = 0; clipIndex < ClippingContextListForMask.Count; clipIndex++)
        {
            // １つのクリッピングマスクに関して
            var cc = ClippingContextListForMask[clipIndex];

            // このクリップを利用する描画オブジェクト群全体を囲む矩形を計算
            CalcClippedTotalBounds(model, cc, Type);

            if (cc.IsUsing)
            {
                usingClipCount++; //使用中としてカウント
            }
        }

        if (usingClipCount <= 0)
        {
            return;
        }

        // マスク作成処理
        // 生成したRenderTargetと同じサイズでビューポートを設定
        gl.Viewport(0, 0, (int)ClippingMaskBufferSize.X, (int)ClippingMaskBufferSize.Y);

        // 後の計算のためにインデックスの最初をセット
        CubismRenderTarget_OpenGLES2 currentMaskBuffer = Type == CsmEnum.DrawableObjectType.Offscreen
            ? renderer.GetOffscreenMaskBuffer(0)
            : renderer.GetDrawableMaskBuffer(0);
        CurrentMaskBuffer = currentMaskBuffer;

        // ----- マスク描画処理 -----
        currentMaskBuffer.BeginDraw(lastFBO);

        renderer.PreDraw(); // バッファをクリアする

        // 各マスクのレイアウトを決定していく
        SetupLayoutBounds(usingClipCount);

        // サイズがレンダーテクスチャの枚数と合わない場合は合わせる
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
            // マスクのクリアフラグを毎フレーム開始時に初期化
            for (int i = 0; i < RenderTextureCount; ++i)
            {
                ClearedMaskBufferFlags[i] = false;
            }
        }

        // 実際にマスクを生成する
        // 全てのマスクをどの様にレイアウトして描くかを決定し、ClipContext , ClippedDrawContext に記憶する
        for (int clipIndex = 0; clipIndex < ClippingContextListForMask.Count; clipIndex++)
        {
            // --- 実際に１つのマスクを描く ---
            var clipContext = ClippingContextListForMask[clipIndex];
            RectF allClippedDrawRect = clipContext.AllClippedDrawRect; //このマスクを使う、全ての描画オブジェクトの論理座標上の囲み矩形
            RectF layoutBoundsOnTex01 = clipContext.LayoutBounds; //この中にマスクを収める
            float MARGIN = 0.05f;

            // clipContextに設定したレンダーターゲットをインデックスで取得
            CubismRenderTarget_OpenGLES2 maskBuffer = Type == CsmEnum.DrawableObjectType.Offscreen
                ? renderer.GetOffscreenMaskBuffer(clipContext.BufferIndex)
                : renderer.GetDrawableMaskBuffer(clipContext.BufferIndex);

            // 現在のレンダーターゲットがclipContextのものと異なる場合
            if (!ReferenceEquals(currentMaskBuffer, maskBuffer))
            {
                currentMaskBuffer.EndDraw();
                currentMaskBuffer = maskBuffer;
                CurrentMaskBuffer = currentMaskBuffer;

                // マスク用RenderTextureをactiveにセット
                currentMaskBuffer.BeginDraw(lastFBO);

                // バッファをクリアする。
                renderer.PreDraw();
            }

            // モデル座標上の矩形を、適宜マージンを付けて使う
            TmpBoundsOnModel.SetRect(allClippedDrawRect);
            TmpBoundsOnModel.Expand(allClippedDrawRect.Width * MARGIN, allClippedDrawRect.Height * MARGIN);
            //########## 本来は割り当てられた領域の全体を使わず必要最低限のサイズがよい
            // シェーダ用の計算式を求める。回転を考慮しない場合は以下のとおり
            // movePeriod' = movePeriod * scaleX + offX     [[ movePeriod' = (movePeriod - tmpBoundsOnModel.movePeriod)*scale + layoutBoundsOnTex01.movePeriod ]]
            float scaleX = layoutBoundsOnTex01.Width / TmpBoundsOnModel.Width;
            float scaleY = layoutBoundsOnTex01.Height / TmpBoundsOnModel.Height;

            // マスク生成時に使う行列を求める
            CreateMatrixForMask(false, layoutBoundsOnTex01, scaleX, scaleY);

            clipContext.MatrixForMask.SetMatrix(TmpMatrixForMask.Tr);
            clipContext.MatrixForDraw.SetMatrix(TmpMatrixForDraw.Tr);

            if (Type == CsmEnum.DrawableObjectType.Offscreen)
            {
                // clipContext * mvp^-1（公式 .cpp:151-156）
                CubismMatrix44 invertMvp = CloneInvertMvp(renderer.GetMvpMatrix());
                clipContext.MatrixForDraw.MultiplyByMatrix(invertMvp);
            }

            // 実際の描画を行う
            int clipDrawCount = clipContext.ClippingIdCount;
            for (int i = 0; i < clipDrawCount; i++)
            {
                int clipDrawIndex = clipContext.ClippingIdList[i];

                // 頂点情報が更新されておらず、信頼性がない場合は描画をパスする
                if (!model.GetDrawableDynamicFlagVertexPositionsDidChange(clipDrawIndex))
                {
                    continue;
                }

                renderer.IsCulling = model.GetDrawableCulling(clipDrawIndex) != 0;

                // マスクがクリアされていないなら処理する
                if (!ClearedMaskBufferFlags[clipContext.BufferIndex])
                {
                    // マスクをクリアする
                    // 1が無効（描かれない）領域、0が有効（描かれる）領域。（シェーダーCd*Csで0に近い値をかけてマスクを作る。1をかけると何も起こらない）
                    gl.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);
                    gl.Clear(gl.GL_COLOR_BUFFER_BIT);
                    ClearedMaskBufferFlags[clipContext.BufferIndex] = true;
                }

                // 今回専用の変換を適用して描く
                // チャンネルも切り替える必要がある(A,R,G,B)
                renderer.ClippingContextBufferForMask = clipContext;

                renderer.DrawMeshOpenGL(model, clipDrawIndex);
            }
        }

        // --- 後処理 ---
        currentMaskBuffer.EndDraw();
        renderer.ClippingContextBufferForMask = null;
        gl.Viewport(lastViewport[0], lastViewport[1], lastViewport[2], lastViewport[3]);
    }

    /// <summary>
    /// MVP 行列の逆行列を取得する（Framework の InvertMatrix44 と同じ実装。
    /// SetupMatrixForHighPrecision と異なり、ここではレンダラ行列を直接反転する必要があるため複製）。
    /// </summary>
    private static CubismMatrix44 CloneInvertMvp(CubismMatrix44 src)
    {
        float[] tr = src.Tr;
        float r00 = tr[0], r10 = tr[1], r20 = tr[2];
        float r01 = tr[4], r11 = tr[5], r21 = tr[6];
        float r02 = tr[8], r12 = tr[9], r22 = tr[10];
        float tx = tr[12], ty = tr[13], tz = tr[14];

        float det = r00 * (r11 * r22 - r12 * r21) -
                    r01 * (r10 * r22 - r12 * r20) +
                    r02 * (r10 * r21 - r11 * r20);

        CubismMatrix44 dst = new();
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
        d[0] = inv00; d[1] = inv10; d[2] = inv20; d[3] = 0.0f;
        d[4] = inv01; d[5] = inv11; d[6] = inv21; d[7] = 0.0f;
        d[8] = inv02; d[9] = inv12; d[10] = inv22; d[11] = 0.0f;
        d[12] = -(inv00 * tx + inv01 * ty + inv02 * tz);
        d[13] = -(inv10 * tx + inv11 * ty + inv12 * tz);
        d[14] = -(inv20 * tx + inv21 * ty + inv22 * tz);
        d[15] = 1.0f;

        return dst;
    }
}

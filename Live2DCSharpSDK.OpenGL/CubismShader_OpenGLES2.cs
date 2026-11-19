using Live2DCSharpSDK.Framework;
using Live2DCSharpSDK.Framework.Math;
using Live2DCSharpSDK.Framework.Model;
using Live2DCSharpSDK.Framework.Rendering;
using Live2DCSharpSDK.Framework.Type;

namespace Live2DCSharpSDK.OpenGL;

/// <summary>
/// OpenGLES2用のシェーダプログラムを生成・破棄するクラス
/// （官方 OpenGL/CubismShader_OpenGLES2.hpp:54-340 / .cpp:48-1121）。
///
/// 【D-11】GLSL は官方 R5 のように外部ファイル化せず、C# の文字列定数として内蔵する
/// （csmLoadFileFunction 機構を持ち込まない）。
/// 【D-09／官方との差異】官方 R5 は Initialize 時に 494 個の GL program を無条件生成する
/// （初回フレーム 3〜10 秒）。本地は「初回使用時に 1 プログラムずつ生成」する遅延生成に変更する
/// （Lazy／二重チェックロック）。この点は官方動作と異なる。
/// </summary>
internal class CubismShader_OpenGLES2(OpenGLApi gl)
{
    public const string CSM_FRAGMENT_SHADER_FP_PRECISION_HIGH = "highp";
    public const string CSM_FRAGMENT_SHADER_FP_PRECISION_MID = "mediump";
    public const string CSM_FRAGMENT_SHADER_FP_PRECISION_LOW = "lowp";

    public const string CSM_FRAGMENT_SHADER_FP_PRECISION = CSM_FRAGMENT_SHADER_FP_PRECISION_HIGH;

    private const string GLES2 = "#version 100\n";
    private const string GLES2C = GLES2 + "precision " + CSM_FRAGMENT_SHADER_FP_PRECISION + " float;";
    private const string Normal = "#version 120\n";
    private const string Tegra = "#version 100\n" +
        "#extension GL_NV_shader_framebuffer_fetch : enable\n" +
        "precision " + CSM_FRAGMENT_SHADER_FP_PRECISION + " float;";

    // ===== SetupMask =====
    public const string VertShaderSrcSetupMask_ES2 =
        GLES2 + VertShaderSrcSetupMask_Base;
    public const string VertShaderSrcSetupMask_Normal =
        Normal + VertShaderSrcSetupMask_Base;
    private const string VertShaderSrcSetupMask_Base =
        @"attribute vec4 a_position;
attribute vec2 a_texCoord;
varying vec2 v_texCoord;
varying vec4 v_myPos;
uniform mat4 u_clipMatrix;
void main()
{
gl_Position = u_clipMatrix * a_position;
v_myPos = u_clipMatrix * a_position;
v_texCoord = a_texCoord;
v_texCoord.y = 1.0 - v_texCoord.y;
}";

    public const string FragShaderSrcSetupMask_ES2 = GLES2C + FragShaderSrcSetupMask_Base;
    public const string FragShaderSrcSetupMask_Normal = Normal + FragShaderSrcSetupMask_Base;
    private const string FragShaderSrcSetupMask_Base =
        @"varying vec2 v_texCoord;
varying vec4 v_myPos;
uniform sampler2D s_texture0;
uniform vec4 u_channelFlag;
uniform vec4 u_baseColor;
void main()
{
float isInside =
    step(u_baseColor.x, v_myPos.x/v_myPos.w)
* step(u_baseColor.y, v_myPos.y/v_myPos.w)
* step(v_myPos.x/v_myPos.w, u_baseColor.z)
* step(v_myPos.y/v_myPos.w, u_baseColor.w);
gl_FragColor = u_channelFlag * texture2D(s_texture0 , v_texCoord).a * isInside;
}";

    public const string FragShaderSrcSetupMaskTegra =
        Tegra + FragShaderSrcSetupMask_Base;

    // ===== バーテックスシェーダ =====
    public const string VertShaderSrc_ES2 = GLES2 + VertShaderSrc_Base;
    public const string VertShaderSrc_Normal = Normal + VertShaderSrc_Base;
    private const string VertShaderSrc_Base =
        @"attribute vec4 a_position;
attribute vec2 a_texCoord;
varying vec2 v_texCoord;
uniform mat4 u_matrix;
void main()
{
gl_Position = u_matrix * a_position;
v_texCoord = a_texCoord;
v_texCoord.y = 1.0 - v_texCoord.y;
}";

    public const string VertShaderSrcMasked_ES2 = GLES2 + VertShaderSrcMasked_Base;
    public const string VertShaderSrcMasked_Normal = Normal + VertShaderSrcMasked_Base;
    private const string VertShaderSrcMasked_Base =
        @"attribute vec4 a_position;
attribute vec2 a_texCoord;
varying vec2 v_texCoord;
varying vec4 v_clipPos;
uniform mat4 u_matrix;
uniform mat4 u_clipMatrix;
void main()
{
gl_Position = u_matrix * a_position;
v_clipPos = u_clipMatrix * a_position;
v_texCoord = a_texCoord;
v_texCoord.y = 1.0 - v_texCoord.y;
}";

    // ===== フラグメントシェーダ（通常/加算/乗算）=====
    public const string FragShaderSrc_ES2 = GLES2C + FragShaderSrc_Base;
    public const string FragShaderSrc_Normal = Normal + FragShaderSrc_Base;
    private const string FragShaderSrc_Base =
    @"varying vec2 v_texCoord;
uniform sampler2D s_texture0;
uniform vec4 u_baseColor;
uniform vec4 u_multiplyColor;
uniform vec4 u_screenColor;
void main()
{
vec4 texColor = texture2D(s_texture0 , v_texCoord);
texColor.rgb = texColor.rgb * u_multiplyColor.rgb;
texColor.rgb = texColor.rgb + u_screenColor.rgb - (texColor.rgb * u_screenColor.rgb);
vec4 color = texColor * u_baseColor;
gl_FragColor = vec4(color.rgb * color.a,  color.a);
}";

    public const string FragShaderSrcTegra = Tegra + FragShaderSrc_Base;

    public const string FragShaderSrcPremultipliedAlpha_ES2 = GLES2C + FragShaderSrcPremultipliedAlpha_Base;
    public const string FragShaderSrcPremultipliedAlpha_Normal = Normal + FragShaderSrcPremultipliedAlpha_Base;
    public const string FragShaderSrcPremultipliedAlpha_Base =
        @"varying vec2 v_texCoord;
uniform sampler2D s_texture0;
uniform vec4 u_baseColor;
uniform vec4 u_multiplyColor;
uniform vec4 u_screenColor;
void main()
{
vec4 texColor = texture2D(s_texture0 , v_texCoord);
texColor.rgb = texColor.rgb * u_multiplyColor.rgb;
texColor.rgb = (texColor.rgb + u_screenColor.rgb * texColor.a) - (texColor.rgb * u_screenColor.rgb);
gl_FragColor = texColor * u_baseColor;
}";

    public const string FragShaderSrcPremultipliedAlphaTegra = Tegra + FragShaderSrcPremultipliedAlpha_Base;

    public const string FragShaderSrcMask_ES2 = GLES2C + FragShaderSrcMask_Base;
    public const string FragShaderSrcMask_Normal = Normal + FragShaderSrcMask_Base;
    private const string FragShaderSrcMask_Base =
        @"varying vec2 v_texCoord;
varying vec4 v_clipPos;
uniform sampler2D s_texture0;
uniform sampler2D s_texture1;
uniform vec4 u_channelFlag;
uniform vec4 u_baseColor;
uniform vec4 u_multiplyColor;
uniform vec4 u_screenColor;
void main()
{
vec4 texColor = texture2D(s_texture0 , v_texCoord);
texColor.rgb = texColor.rgb * u_multiplyColor.rgb;
texColor.rgb = texColor.rgb + u_screenColor.rgb - (texColor.rgb * u_screenColor.rgb);
vec4 col_formask = texColor * u_baseColor;
col_formask.rgb = col_formask.rgb  * col_formask.a ;
vec4 clipMask = (1.0 - texture2D(s_texture1, v_clipPos.xy / v_clipPos.w)) * u_channelFlag;
float maskVal = clipMask.r + clipMask.g + clipMask.b + clipMask.a;
col_formask = col_formask * maskVal;
gl_FragColor = col_formask;
}";

    public const string FragShaderSrcMaskTegra = Tegra + FragShaderSrcMask_Base;

    public const string FragShaderSrcMaskInverted_ES2 = GLES2C + FragShaderSrcMaskInverted_Base;
    public const string FragShaderSrcMaskInverted_Normal = Normal + FragShaderSrcMaskInverted_Base;
    private const string FragShaderSrcMaskInverted_Base =
        @"varying vec2 v_texCoord;
varying vec4 v_clipPos;
uniform sampler2D s_texture0;
uniform sampler2D s_texture1;
uniform vec4 u_channelFlag;
uniform vec4 u_baseColor;
uniform vec4 u_multiplyColor;
uniform vec4 u_screenColor;
void main()
{
vec4 texColor = texture2D(s_texture0 , v_texCoord);
texColor.rgb = texColor.rgb * u_multiplyColor.rgb;
texColor.rgb = texColor.rgb + u_screenColor.rgb - (texColor.rgb * u_screenColor.rgb);
vec4 col_formask = texColor * u_baseColor;
col_formask.rgb = col_formask.rgb  * col_formask.a ;
vec4 clipMask = (1.0 - texture2D(s_texture1, v_clipPos.xy / v_clipPos.w)) * u_channelFlag;
float maskVal = clipMask.r + clipMask.g + clipMask.b + clipMask.a;
col_formask = col_formask * (1.0 - maskVal);
gl_FragColor = col_formask;
}";

    public const string FragShaderSrcMaskInvertedTegra = Tegra + FragShaderSrcMaskInverted_Base;

    public const string FragShaderSrcMaskPremultipliedAlpha_ES2 = GLES2C + FragShaderSrcMaskPremultipliedAlpha_Base;
    public const string FragShaderSrcMaskPremultipliedAlpha_Normal = Normal + FragShaderSrcMaskPremultipliedAlpha_Base;
    public const string FragShaderSrcMaskPremultipliedAlpha_Base =
        @"varying vec2 v_texCoord;
varying vec4 v_clipPos;
uniform sampler2D s_texture0;
uniform sampler2D s_texture1;
uniform vec4 u_channelFlag;
uniform vec4 u_baseColor;
uniform vec4 u_multiplyColor;
uniform vec4 u_screenColor;
void main()
{
vec4 texColor = texture2D(s_texture0 , v_texCoord);
texColor.rgb = texColor.rgb * u_multiplyColor.rgb;
texColor.rgb = (texColor.rgb + u_screenColor.rgb * texColor.a) - (texColor.rgb * u_screenColor.rgb);
vec4 col_formask = texColor * u_baseColor;
vec4 clipMask = (1.0 - texture2D(s_texture1, v_clipPos.xy / v_clipPos.w)) * u_channelFlag;
float maskVal = clipMask.r + clipMask.g + clipMask.b + clipMask.a;
col_formask = col_formask * maskVal;
gl_FragColor = col_formask;
}";

    public const string FragShaderSrcMaskPremultipliedAlphaTegra = Tegra + FragShaderSrcMaskPremultipliedAlpha_Base;

    public const string FragShaderSrcMaskInvertedPremultipliedAlpha_ES2 = GLES2C + FragShaderSrcMaskInvertedPremultipliedAlpha_Base;
    public const string FragShaderSrcMaskInvertedPremultipliedAlpha_Normal = Normal + FragShaderSrcMaskInvertedPremultipliedAlpha_Base;
    public const string FragShaderSrcMaskInvertedPremultipliedAlpha_Base =
        @"varying vec2 v_texCoord;
varying vec4 v_clipPos;
uniform sampler2D s_texture0;
uniform sampler2D s_texture1;
uniform vec4 u_channelFlag;
uniform vec4 u_baseColor;
uniform vec4 u_multiplyColor;
uniform vec4 u_screenColor;
void main()
{
vec4 texColor = texture2D(s_texture0 , v_texCoord);
texColor.rgb = texColor.rgb * u_multiplyColor.rgb;
texColor.rgb = (texColor.rgb + u_screenColor.rgb * texColor.a) - (texColor.rgb * u_screenColor.rgb);
vec4 col_formask = texColor * u_baseColor;
vec4 clipMask = (1.0 - texture2D(s_texture1, v_clipPos.xy / v_clipPos.w)) * u_channelFlag;
float maskVal = clipMask.r + clipMask.g + clipMask.b + clipMask.a;
col_formask = col_formask * (1.0 - maskVal);
gl_FragColor = col_formask;
}";

    public const string FragShaderSrcMaskInvertedPremultipliedAlphaTegra = Tegra + FragShaderSrcMaskInvertedPremultipliedAlpha_Base;

    // ===== R5 追加: Copy（B10）=====
    private const string VertShaderSrcCopy_Base =
        @"attribute vec4 a_position;
attribute vec2 a_texCoord;
varying vec2 v_texCoord;
void main()
{
v_texCoord = a_texCoord;
gl_Position = a_position;
}";

    private const string FragShaderSrcCopy_Base =
        @"varying vec2 v_texCoord;
uniform sampler2D s_texture0;
uniform vec4 u_baseColor;
void main()
{
gl_FragColor = texture2D(s_texture0, v_texCoord) * u_baseColor;
}";

    // ===== R5 追加: ブレンドモード用バーテックス（B10）=====
    private const string VertShaderSrcBlend_Base =
        @"attribute vec4 a_position;
attribute vec2 a_texCoord;
varying vec2 v_texCoord;
varying vec2 v_blendCoord;
uniform mat4 u_matrix;
void main()
{
gl_Position = u_matrix * a_position;
v_texCoord = a_texCoord;
v_texCoord.y = 1.0 - v_texCoord.y;
vec2 ndcPos = gl_Position.xy / gl_Position.w;
v_blendCoord = ndcPos * 0.5 + 0.5;
}";

    private const string VertShaderSrcMaskedBlend_Base =
        @"attribute vec4 a_position;
attribute vec2 a_texCoord;
varying vec2 v_texCoord;
varying vec2 v_blendCoord;
varying vec4 v_clipPos;
uniform mat4 u_matrix;
uniform mat4 u_clipMatrix;
void main()
{
gl_Position = u_matrix * a_position;
v_clipPos = u_clipMatrix * a_position;
v_texCoord = a_texCoord;
v_texCoord.y = 1.0 - v_texCoord.y;
vec2 ndcPos = gl_Position.xy / gl_Position.w;
v_blendCoord = ndcPos * 0.5 + 0.5;
}";

    // ===== R5 追加: ブレンドモード用フラグメント（B10）=====
    private const string FragShaderSrcBlend_Base =
        @"varying vec2 v_texCoord;
varying vec2 v_blendCoord;
uniform sampler2D s_texture0;
uniform sampler2D s_blendTexture;
uniform vec4 u_baseColor;
uniform vec4 u_multiplyColor;
uniform vec4 u_screenColor;

vec4 ConvertPremultipliedToStraight(vec4 source);
vec3 ColorBlend(vec3 colorSource, vec3 colorDestination);
vec4 AlphaBlend(vec3 color, vec4 colorSource, vec4 colorDestination);

void main()
{
vec4 texColor = texture2D(s_texture0, v_texCoord);
texColor.rgb = texColor.rgb * u_multiplyColor.rgb;
texColor.rgb = texColor.rgb + u_screenColor.rgb - (texColor.rgb * u_screenColor.rgb);
vec4 colorSource = texColor * u_baseColor;
vec4 colorDestination = ConvertPremultipliedToStraight(texture2D(s_blendTexture, v_blendCoord));
gl_FragColor = AlphaBlend(ColorBlend(colorSource.rgb, colorDestination.rgb), colorSource, colorDestination);
}";

    private const string FragShaderSrcMaskBlend_Base =
        @"varying vec2 v_texCoord;
varying vec2 v_blendCoord;
varying vec4 v_clipPos;
uniform sampler2D s_texture0;
uniform sampler2D s_texture1;
uniform sampler2D s_blendTexture;
uniform vec4 u_channelFlag;
uniform vec4 u_baseColor;
uniform vec4 u_multiplyColor;
uniform vec4 u_screenColor;

vec4 ConvertPremultipliedToStraight(vec4 source);
vec3 ColorBlend(vec3 colorSource, vec3 colorDestination);
vec4 AlphaBlend(vec3 color, vec4 colorSource, vec4 colorDestination);

void main()
{
vec4 texColor = texture2D(s_texture0, v_texCoord);
texColor.rgb = texColor.rgb * u_multiplyColor.rgb;
texColor.rgb = texColor.rgb + u_screenColor.rgb - (texColor.rgb * u_screenColor.rgb);
vec4 col_formask = texColor * u_baseColor;
vec4 clipMask = (1.0 - texture2D(s_texture1, v_clipPos.xy / v_clipPos.w)) * u_channelFlag;
float maskVal = clipMask.r + clipMask.g + clipMask.b + clipMask.a;
vec4 colorSource = vec4(col_formask.rgb, col_formask.a * maskVal);
vec4 colorDestination = ConvertPremultipliedToStraight(texture2D(s_blendTexture, v_blendCoord));
gl_FragColor = AlphaBlend(ColorBlend(colorSource.rgb, colorDestination.rgb), colorSource, colorDestination);
}";

    private const string FragShaderSrcMaskInvertedBlend_Base =
        @"varying vec2 v_texCoord;
varying vec2 v_blendCoord;
varying vec4 v_clipPos;
uniform sampler2D s_texture0;
uniform sampler2D s_texture1;
uniform sampler2D s_blendTexture;
uniform vec4 u_channelFlag;
uniform vec4 u_baseColor;
uniform vec4 u_multiplyColor;
uniform vec4 u_screenColor;

vec4 ConvertPremultipliedToStraight(vec4 source);
vec3 ColorBlend(vec3 colorSource, vec3 colorDestination);
vec4 AlphaBlend(vec3 color, vec4 colorSource, vec4 colorDestination);

void main()
{
vec4 texColor = texture2D(s_texture0, v_texCoord);
texColor.rgb = texColor.rgb * u_multiplyColor.rgb;
texColor.rgb = texColor.rgb + u_screenColor.rgb - (texColor.rgb * u_screenColor.rgb);
vec4 col_formask = texColor * u_baseColor;
vec4 clipMask = (1.0 - texture2D(s_texture1, v_clipPos.xy / v_clipPos.w)) * u_channelFlag;
float maskVal = clipMask.r + clipMask.g + clipMask.b + clipMask.a;
vec4 colorSource = vec4(col_formask.rgb, col_formask.a * (1.0 - maskVal));
vec4 colorDestination = ConvertPremultipliedToStraight(texture2D(s_blendTexture, v_blendCoord));
gl_FragColor = AlphaBlend(ColorBlend(colorSource.rgb, colorDestination.rgb), colorSource, colorDestination);
}";

    private const string FragShaderSrcPremultipliedAlphaBlend_Base =
        @"varying vec2 v_texCoord;
varying vec2 v_blendCoord;
uniform sampler2D s_texture0;
uniform sampler2D s_blendTexture;
uniform vec4 u_baseColor;
uniform vec4 u_multiplyColor;
uniform vec4 u_screenColor;

vec4 ConvertPremultipliedToStraight(vec4 source);
vec3 ColorBlend(vec3 colorSource, vec3 colorDestination);
vec4 AlphaBlend(vec3 color, vec4 colorSource, vec4 colorDestination);

void main()
{
vec4 texColor = texture2D(s_texture0, v_texCoord);
texColor.rgb = texColor.rgb * u_multiplyColor.rgb;
texColor.rgb = (texColor.rgb + u_screenColor.rgb * texColor.a) - (texColor.rgb * u_screenColor.rgb);
vec4 colorSource = ConvertPremultipliedToStraight(texColor * u_baseColor);
vec4 colorDestination = ConvertPremultipliedToStraight(texture2D(s_blendTexture, v_blendCoord));
gl_FragColor = AlphaBlend(ColorBlend(colorSource.rgb, colorDestination.rgb), colorSource, colorDestination);
}";

    private const string FragShaderSrcMaskPremultipliedAlphaBlend_Base =
        @"varying vec2 v_texCoord;
varying vec2 v_blendCoord;
varying vec4 v_clipPos;
uniform sampler2D s_texture0;
uniform sampler2D s_texture1;
uniform sampler2D s_blendTexture;
uniform vec4 u_channelFlag;
uniform vec4 u_baseColor;
uniform vec4 u_multiplyColor;
uniform vec4 u_screenColor;

vec4 ConvertPremultipliedToStraight(vec4 source);
vec3 ColorBlend(vec3 colorSource, vec3 colorDestination);
vec4 AlphaBlend(vec3 color, vec4 colorSource, vec4 colorDestination);

void main()
{
vec4 texColor = texture2D(s_texture0, v_texCoord);
texColor.rgb = texColor.rgb * u_multiplyColor.rgb;
texColor.rgb = (texColor.rgb + u_screenColor.rgb * texColor.a) - (texColor.rgb * u_screenColor.rgb);
vec4 col_formask = ConvertPremultipliedToStraight(texColor * u_baseColor);
vec4 clipMask = (1.0 - texture2D(s_texture1, v_clipPos.xy / v_clipPos.w)) * u_channelFlag;
float maskVal = clipMask.r + clipMask.g + clipMask.b + clipMask.a;
vec4 colorSource = vec4(col_formask.rgb, col_formask.a * maskVal);
vec4 colorDestination = ConvertPremultipliedToStraight(texture2D(s_blendTexture, v_blendCoord));
gl_FragColor = AlphaBlend(ColorBlend(colorSource.rgb, colorDestination.rgb), colorSource, colorDestination);
}";

    private const string FragShaderSrcMaskInvertedPremultipliedAlphaBlend_Base =
        @"varying vec2 v_texCoord;
varying vec2 v_blendCoord;
varying vec4 v_clipPos;
uniform sampler2D s_texture0;
uniform sampler2D s_texture1;
uniform sampler2D s_blendTexture;
uniform vec4 u_channelFlag;
uniform vec4 u_baseColor;
uniform vec4 u_multiplyColor;
uniform vec4 u_screenColor;

vec4 ConvertPremultipliedToStraight(vec4 source);
vec3 ColorBlend(vec3 colorSource, vec3 colorDestination);
vec4 AlphaBlend(vec3 color, vec4 colorSource, vec4 colorDestination);

void main()
{
vec4 texColor = texture2D(s_texture0, v_texCoord);
texColor.rgb = texColor.rgb * u_multiplyColor.rgb;
texColor.rgb = (texColor.rgb + u_screenColor.rgb * texColor.a) - (texColor.rgb * u_screenColor.rgb);
vec4 col_formask = ConvertPremultipliedToStraight(texColor * u_baseColor);
vec4 clipMask = (1.0 - texture2D(s_texture1, v_clipPos.xy / v_clipPos.w)) * u_channelFlag;
float maskVal = clipMask.r + clipMask.g + clipMask.b + clipMask.a;
vec4 colorSource = vec4(col_formask.rgb, col_formask.a * (1.0 - maskVal));
vec4 colorDestination = ConvertPremultipliedToStraight(texture2D(s_blendTexture, v_blendCoord));
gl_FragColor = AlphaBlend(ColorBlend(colorSource.rgb, colorDestination.rgb), colorSource, colorDestination);
}";

    // ===== R5 追加: カラーブレンド / アルファブレンドの共通スニペット =====
    // 官方 .cpp:961-1004 は「#define CSM_COLOR_BLEND_MODE n」+ ColorBlendShaderPath の内容、
    // 「#define CSM_ALPHA_BLEND_MODE n」+ AlphaBlendShaderPath の内容をフラグメントソース末尾に連結する。
    // 本地も同じ連結方式を採る（D-11）。
    private const string FragShaderSrcColorBlendSnippet =
        @"vec4 ConvertPremultipliedToStraight(vec4 source)
{
    if (abs(source.a) < 0.00001)
    {
        source.rgb = vec3(0.0, 0.0, 0.0);
    }
    else
    {
        source.rgb /= source.a;
    }

    return source;
}

#if !defined CSM_COLOR_BLEND_MODE
#error not define symbol: CSM_COLOR_BLEND_MODE.

#elif CSM_COLOR_BLEND_MODE == 0 // NORMAL
vec3 ColorBlend(vec3 colorSource, vec3 colorDestination)
{
    return colorSource;
}

#elif CSM_COLOR_BLEND_MODE == 1 // ADD
vec3 ColorBlend(vec3 colorSource, vec3 colorDestination)
{
    return min(colorSource + colorDestination, 1.0);
}

#elif CSM_COLOR_BLEND_MODE == 2 // ADD(GLOW)
vec3 ColorBlend(vec3 colorSource, vec3 colorDestination)
{
    return colorSource + colorDestination;
}

#elif CSM_COLOR_BLEND_MODE == 3 // DARKEN
vec3 ColorBlend(vec3 colorSource, vec3 colorDestination)
{
    return min(colorSource, colorDestination);
}

#elif CSM_COLOR_BLEND_MODE == 4 // MULTIPLY
vec3 ColorBlend(vec3 colorSource, vec3 colorDestination)
{
    return colorSource * colorDestination;
}

#elif CSM_COLOR_BLEND_MODE == 5 // COLOR BURN
float ColorBurn(float colorSource, float colorDestination)
{
    if (abs(colorDestination - 1.0) < 0.000001)
    {
        return 1.0;
    }
    else if (abs(colorSource) < 0.000001)
    {
        return 0.0;
    }

    return 1.0 - min(1.0, (1.0 - colorDestination) / colorSource);
}

vec3 ColorBlend(vec3 colorSource, vec3 colorDestination)
{
    return vec3(
        ColorBurn(colorSource.r, colorDestination.r),
        ColorBurn(colorSource.g, colorDestination.g),
        ColorBurn(colorSource.b, colorDestination.b)
    );
}

#elif CSM_COLOR_BLEND_MODE == 6 // LINEAR BURN
vec3 ColorBlend(vec3 colorSource, vec3 colorDestination)
{
    return max(vec3(0.0), colorSource + colorDestination - 1.0);
}

#elif CSM_COLOR_BLEND_MODE == 7 // LIGHTEN
vec3 ColorBlend(vec3 colorSource, vec3 colorDestination)
{
    return max(colorSource, colorDestination);
}

#elif CSM_COLOR_BLEND_MODE == 8 // SCREEN
vec3 ColorBlend(vec3 colorSource, vec3 colorDestination)
{
    return colorSource + colorDestination - colorSource * colorDestination;
}

#elif CSM_COLOR_BLEND_MODE == 9 // COLOR DODGE
float ColorDodge(float colorSource, float colorDestination)
{
    if (colorDestination <= 0.0)
    {
        return 0.0;
    }
    else if (colorSource == 1.0)
    {
        return 1.0;
    }

    return min(1.0, colorDestination / (1.0 - colorSource));
}

vec3 ColorBlend(vec3 colorSource, vec3 colorDestination)
{
    return vec3(
        ColorDodge(colorSource.r, colorDestination.r),
        ColorDodge(colorSource.g, colorDestination.g),
        ColorDodge(colorSource.b, colorDestination.b)
    );
}

#elif CSM_COLOR_BLEND_MODE == 10 // OVERLAY
float Overlay(float colorSource, float colorDestination)
{
    float mul = 2.0 * colorSource * colorDestination;
    float scr = 1.0 - 2.0 * (1.0 - colorSource) * (1.0 - colorDestination) ;
    return colorDestination < 0.5 ? mul : scr ;
}

vec3 ColorBlend(vec3 colorSource, vec3 colorDestination)
{
    return vec3(
        Overlay(colorSource.r, colorDestination.r),
        Overlay(colorSource.g, colorDestination.g),
        Overlay(colorSource.b, colorDestination.b)
    );
}

#elif CSM_COLOR_BLEND_MODE == 11 // SOFT LIGHT
float SoftLight(float colorSource, float colorDestination)
{
    float val1 = colorDestination - (1.0 - 2.0 * colorSource) * colorDestination * (1.0 - colorDestination);
    float val2 = colorDestination + (2.0 * colorSource - 1.0) * colorDestination * ((16.0 * colorDestination - 12.0) * colorDestination + 3.0);
    float val3 = colorDestination + (2.0 * colorSource - 1.0) * (sqrt(colorDestination) - colorDestination);

    if (colorSource <= 0.5)
    {
        return val1;
    }
    else if (colorDestination <= 0.25)
    {
        return val2;
    }

    return val3;
}

vec3 ColorBlend(vec3 colorSource, vec3 colorDestination)
{
    return vec3(
        SoftLight(colorSource.r, colorDestination.r),
        SoftLight(colorSource.g, colorDestination.g),
        SoftLight(colorSource.b, colorDestination.b)
    );
}

#elif CSM_COLOR_BLEND_MODE == 12 // HARD LIGHT
float HardLight(float colorSource, float colorDestination)
{
    float mul = 2.0 * colorSource * colorDestination;
    float scr = 1.0 - 2.0 * (1.0 - colorSource) * (1.0 - colorDestination);

    if (colorSource < 0.5)
    {
        return mul;
    }

    return scr;
}

vec3 ColorBlend(vec3 colorSource, vec3 colorDestination)
{
    return vec3(
        HardLight(colorSource.r, colorDestination.r),
        HardLight(colorSource.g, colorDestination.g),
        HardLight(colorSource.b, colorDestination.b)
    );
}

#elif CSM_COLOR_BLEND_MODE == 13 // LINEAR LIGHT
float LinearLight(float colorSource, float colorDestination)
{
    float burn = max(0.0, 2.0 * colorSource + colorDestination - 1.0);
    float dodge = min(1.0, 2.0 * (colorSource - 0.5) + colorDestination);

    if (colorSource < 0.5)
    {
        return burn;
    }

    return dodge;
}

vec3 ColorBlend(vec3 colorSource, vec3 colorDestination)
{
    return vec3(
        LinearLight(colorSource.r, colorDestination.r),
        LinearLight(colorSource.g, colorDestination.g),
        LinearLight(colorSource.b, colorDestination.b)
    );
}

#elif CSM_COLOR_BLEND_MODE == 14 || CSM_COLOR_BLEND_MODE == 15
const float rCoeff = 0.30;
const float gCoeff = 0.59;
const float bCoeff = 0.11;

float GetMax(vec3 rgbC)
{
    return max(rgbC.r, max(rgbC.g, rgbC.b));
}

float GetMin(vec3 rgbC)
{
    return min(rgbC.r, min(rgbC.g, rgbC.b));
}

float GetRange(vec3 rgbC)
{
    return max(rgbC.r, max(rgbC.g, rgbC.b)) - min(rgbC.r, min(rgbC.g, rgbC.b));
}

float Saturation(vec3 rgbC)
{
    return GetRange(rgbC);
}

float Luma(vec3 rgbC)
{
    return rCoeff * rgbC.r + gCoeff * rgbC.g + bCoeff * rgbC.b;
}

vec3 ClipColor(vec3 rgbC)
{
    float   luma = Luma(rgbC);
    float   maxv = GetMax(rgbC);
    float   minv = GetMin(rgbC);
    vec3    outputColor = rgbC;

    outputColor = minv < 0.0 ? luma + (outputColor - luma) * luma / (luma - minv) : outputColor;
    outputColor = maxv > 1.0 ? luma + (outputColor - luma) * (1.0 - luma) / (maxv - luma) : outputColor;

    return outputColor;
}

vec3 SetLuma(vec3 rgbC, float luma)
{
    return ClipColor(rgbC + (luma - Luma(rgbC)));
}

vec3 SetSaturation(vec3 rgbC, float saturation)
{
    float maxv = GetMax(rgbC);
    float minv = GetMin(rgbC);
    float medv = rgbC.r + rgbC.g + rgbC.b - maxv - minv;
    float outputMax, outputMed, outputMin;

    outputMax = minv < maxv ? saturation : 0.0;
    outputMed = minv < maxv ? (medv - minv) * saturation / (maxv - minv) : 0.0;
    outputMin = 0.0;

    if(rgbC.r == maxv)
    {
        return rgbC.b < rgbC.g ? vec3(outputMax, outputMed, outputMin) : vec3(outputMax, outputMin, outputMed);
    }
    else if(rgbC.g == maxv)
    {
        return rgbC.r < rgbC.b ? vec3(outputMin, outputMax, outputMed) : vec3(outputMed, outputMax, outputMin);
    }
    else // if(rgbC.b == maxv)
    {
        return rgbC.g < rgbC.r ? vec3(outputMed, outputMin, outputMax) : vec3(outputMin, outputMed, outputMax);
    }
}

#if CSM_COLOR_BLEND_MODE == 14 // HUE
vec3 ColorBlend(vec3 colorSource, vec3 colorDestination)
{
    return SetLuma(SetSaturation(colorSource, Saturation(colorDestination)), Luma(colorDestination));
}

#elif CSM_COLOR_BLEND_MODE == 15 // COLOR
vec3 ColorBlend(vec3 colorSource, vec3 colorDestination)
{
    return SetLuma(colorSource, Luma(colorDestination)) ;
}

#endif

#else
#error not supported blend function.

#endif";

    private const string FragShaderSrcAlphaBlendSnippet =
        @"#if !defined CSM_ALPHA_BLEND_MODE
#error not define symbol: CSM_ALPHA_BLEND_MODE.

#else
vec4 OverlapRgba(vec3 color, vec3 colorSource, vec3 colorDestination, vec3 parameter)
{
    vec3 rgb = color * parameter.x + colorSource * parameter.y + colorDestination * parameter.z;
    float alpha = parameter.x + parameter.y + parameter.z;
    return vec4(rgb, alpha);
}

#if CSM_ALPHA_BLEND_MODE == 0 // OVER
vec4 AlphaBlend(vec3 color, vec4 colorSource, vec4 colorDestination)
{
    vec3 parameter = vec3(colorSource.a * colorDestination.a, colorSource.a * (1.0 - colorDestination.a), colorDestination.a * (1.0 - colorSource.a));
    return OverlapRgba(color, colorSource.rgb, colorDestination.rgb, parameter);
}

#elif CSM_ALPHA_BLEND_MODE == 1 // ATOP
vec4 AlphaBlend(vec3 color, vec4 colorSource, vec4 colorDestination)
{
    vec3 parameter = vec3(colorSource.a * colorDestination.a, 0, colorDestination.a * (1.0 - colorSource.a));
    return OverlapRgba(color, colorSource.rgb, colorDestination.rgb, parameter);
}

#elif CSM_ALPHA_BLEND_MODE == 2 // OUT
vec4 AlphaBlend(vec3 color, vec4 colorSource, vec4 colorDestination)
{
    vec3 parameter = vec3(0.0, 0.0, colorDestination.a * (1.0 - colorSource.a));
    return OverlapRgba(color, colorSource.rgb, colorDestination.rgb, parameter);
}

#elif CSM_ALPHA_BLEND_MODE == 3 // CONJOINT OVER
vec4 AlphaBlend(vec3 color, vec4 colorSource, vec4 colorDestination)
{
    vec3 parameter = vec3(min(colorSource.a, colorDestination.a), max(colorSource.a - colorDestination.a, 0.0), max(colorDestination.a - colorSource.a, 0.0));
    return OverlapRgba(color, colorSource.rgb, colorDestination.rgb, parameter);
}

#elif CSM_ALPHA_BLEND_MODE == 4 // DISJOINT OVER
vec4 AlphaBlend(vec3 color, vec4 colorSource, vec4 colorDestination)
{
    vec3 parameter = vec3(max(colorSource.a + colorDestination.a - 1.0, 0.0), min(colorSource.a, 1.0 - colorDestination.a), min(colorDestination.a, 1.0 - colorSource.a));
    return OverlapRgba(color, colorSource.rgb, colorDestination.rgb, parameter);
}

#else
#error not supported blend function

#endif
#endif";

    /// <summary>
    /// マスクの種類（B9、官方 CubismShader_OpenGLES2.hpp:128-136）。
    /// この enum の値がそのままシェーダ変種の offset（0..5）になる。
    /// </summary>
    private enum MaskType
    {
        /// <summary>マスク無</summary>
        None,
        /// <summary>クリッピング</summary>
        Masked,
        /// <summary>クリッピング・反転</summary>
        MaskedInverted,
        /// <summary>乗算済アルファ対応</summary>
        PremultipliedAlpha,
        /// <summary>クリッピング、乗算済アルファ対応</summary>
        MaskedPremultipliedAlpha,
        /// <summary>クリッピング・反転、乗算済アルファ対応</summary>
        MaskedInvertedPremultipliedAlpha,
    }

    /// <summary>
    /// 公式の ColorBlendMode（官方 .hpp:138-158）。None=-1、0..15、Count=16。
    /// ※ Core の csmColorBlendType_* とは番号が異なる（AddCompatible/MultiplyCompatible を持たない）。
    /// </summary>
    private enum ColorBlendMode
    {
        None = -1,
        Normal = 0,
        Add,
        AddGlow,
        Darken,
        Multiply,
        ColorBurn,
        LinearBurn,
        Lighten,
        Screen,
        ColorDodge,
        Overlay,
        SoftLight,
        HardLight,
        LinearLight,
        Hue,
        Color,
        Count,
    }

    /// <summary>
    /// 公式の AlphaBlendMode（官方 .hpp:160-169）。None=-1、0..4、Count=5。
    /// </summary>
    private enum AlphaBlendMode
    {
        None = -1,
        Over,
        Atop,
        Out,
        ConjointOver,
        DisjointOver,
        Count,
    }

    /// <summary>
    /// Tegra対応.拡張方式で描画
    /// </summary>
    internal bool s_extMode;

    /// <summary>
    /// 拡張方式のPA設定用の変数
    /// </summary>
    internal bool s_extPAMode;

    /// <summary>
    /// ロードしたシェーダプログラムを保持する変数
    /// </summary>
    private readonly List<CubismShaderSet> _shaderSets = [];

    /// <summary>
    /// 各シェーダの生成済みフラグ（D-09 の遅延生成用）
    /// </summary>
    private readonly bool[] _compiled = new bool[ShaderNames.ShaderCount];

    /// <summary>
    /// シェーダ生成のロック（D-09 二重チェックロック）
    /// </summary>
    private readonly object _generationLock = new();

    /// <summary>
    /// シェーダセット配列を確保する（官方 GenerateShaders の配列確保部分）。
    /// 【D-09／官方との差異】実際の GL program 生成は GetShaderSet() の初回参照まで遅延する。
    /// 官方 R5 は Initialize 時に 494 個すべてを無条件生成する（CubismShader_OpenGLES2.cpp:324-542）。
    /// </summary>
    internal void EnsureGenerated()
    {
        if (_shaderSets.Count == ShaderNames.ShaderCount)
        {
            return;
        }

        lock (_generationLock)
        {
            if (_shaderSets.Count == ShaderNames.ShaderCount)
            {
                return;
            }

            for (int i = _shaderSets.Count; i < ShaderNames.ShaderCount; i++)
            {
                _shaderSets.Add(new CubismShaderSet());
            }
        }
    }

    /// <summary>
    /// 指定シェーダを取得する。初回参照時にコンパイルする（D-09、二重チェックロック）。
    /// </summary>
    /// <param name="index">ShaderNames の添字</param>
    internal CubismShaderSet GetShaderSet(int index)
    {
        EnsureGenerated();

        if (_compiled[index])
        {
            return _shaderSets[index];
        }

        lock (_generationLock)
        {
            if (!_compiled[index])
            {
                CompileShaderSet(index);
            }
        }

        return _shaderSets[index];
    }

    /// <summary>
    /// 1 つのシェーダセットをコンパイルする。
    /// 添字から「シェーダ種別・マスク変種・ブレンド組合せ」を逆算する（官方の生成ループと等価）。
    /// </summary>
    private void CompileShaderSet(int index)
    {
        string vertShaderSrc;
        string fragShaderSrc;
        MaskType maskType = MaskType.None;
        bool isBlendMode = false;

        if (index == ShaderNames.Copy)
        {
            vertShaderSrc = (gl.IsES2 ? GLES2 : Normal) + VertShaderSrcCopy_Base;
            fragShaderSrc = (gl.IsES2 ? GLES2C : Normal) + FragShaderSrcCopy_Base;
        }
        else if (index == ShaderNames.SetupMask)
        {
            vertShaderSrc = gl.IsES2 ? VertShaderSrcSetupMask_ES2 : VertShaderSrcSetupMask_Normal;
            fragShaderSrc = gl.IsES2
                ? (s_extMode ? FragShaderSrcSetupMaskTegra : FragShaderSrcSetupMask_ES2)
                : FragShaderSrcSetupMask_Normal;
        }
        else
        {
            int group = (index - 2) / ShaderNames.VariantCount;
            int variant = (index - 2) % ShaderNames.VariantCount;
            maskType = (MaskType)variant;

            // 加算・乗算は通常と同じシェーダを利用する（官方 .cpp:400-414）
            if (group is 1 or 2)
            {
                int normalIndex = ShaderNames.Normal + variant;
                GetShaderSet(normalIndex);
                CopyShaderSet(_shaderSets[index], _shaderSets[normalIndex]);
                _compiled[index] = true;
                return;
            }

            if (group <= 6)
            {
                // 5.2 以前のシェーダ（Normal と Normal の Over 以外）
                isBlendMode = group >= 3;
                if (!isBlendMode)
                {
                    vertShaderSrc = VariantVertexSource(variant);
                    fragShaderSrc = VariantFragmentSource(variant);
                }
                else
                {
                    vertShaderSrc = (gl.IsES2 ? GLES2 : Normal) + VariantBlendVertexBase(variant);
                    fragShaderSrc = ComposeBlendFragment(VariantBlendFragmentBase(variant), (int)ColorBlendMode.Normal, group - 2);
                }
            }
            else
            {
                // 5.3 以降のブレンド組合せ
                isBlendMode = true;
                int colorBlendMode = (group - 7) / ShaderNames.AlphaBlendModeCount + 1;
                int alphaBlendMode = (group - 7) % ShaderNames.AlphaBlendModeCount;
                vertShaderSrc = (gl.IsES2 ? GLES2 : Normal) + VariantBlendVertexBase(variant);
                fragShaderSrc = ComposeBlendFragment(VariantBlendFragmentBase(variant), colorBlendMode, alphaBlendMode);
            }
        }

        int shaderProgram = LoadShaderProgram(vertShaderSrc, fragShaderSrc);
        _shaderSets[index].ShaderProgram = shaderProgram;
        if (shaderProgram != 0)
        {
            if (index == ShaderNames.Copy)
            {
                SetShaderSetForCopy(_shaderSets[index]);
            }
            else if (index == ShaderNames.SetupMask)
            {
                SetShaderSetForSetupMask(_shaderSets[index]);
            }
            else
            {
                SetShaderSet(_shaderSets[index], maskType, isBlendMode);
            }
        }

        // 失敗しても再試行しない（官方も一度だけ生成する）
        _compiled[index] = true;
    }

    /// <summary>
    /// シェーダセットをコピーする（加算・乗算が通常シェーダを共用するため）。
    /// </summary>
    private static void CopyShaderSet(CubismShaderSet dst, CubismShaderSet src)
    {
        dst.ShaderProgram = src.ShaderProgram;
        dst.AttributePositionLocation = src.AttributePositionLocation;
        dst.AttributeTexCoordLocation = src.AttributeTexCoordLocation;
        dst.UniformMatrixLocation = src.UniformMatrixLocation;
        dst.UniformClipMatrixLocation = src.UniformClipMatrixLocation;
        dst.SamplerTexture0Location = src.SamplerTexture0Location;
        dst.SamplerTexture1Location = src.SamplerTexture1Location;
        dst.SamplerBlendTextureLocation = src.SamplerBlendTextureLocation;
        dst.UniformBaseColorLocation = src.UniformBaseColorLocation;
        dst.UniformMultiplyColorLocation = src.UniformMultiplyColorLocation;
        dst.UniformScreenColorLocation = src.UniformScreenColorLocation;
        dst.UnifromChannelFlagLocation = src.UnifromChannelFlagLocation;
    }

    /// <summary>
    /// 変種番号からバーテックスシェーダソースを選ぶ（マスク有無のみで決まる）。
    /// </summary>
    private string VariantVertexSource(int variant)
    {
        bool masked = variant is 1 or 2 or 4 or 5;
        if (gl.IsES2)
        {
            return masked ? VertShaderSrcMasked_ES2 : VertShaderSrc_ES2;
        }

        return masked ? VertShaderSrcMasked_Normal : VertShaderSrc_Normal;
    }

    /// <summary>
    /// 変種番号からフラグメントシェーダソースを選ぶ（マスク/乗算済アルファ）。
    /// </summary>
    private string VariantFragmentSource(int variant)
    {
        if (gl.IsES2)
        {
            if (s_extMode)
            {
                return variant switch
                {
                    1 => FragShaderSrcMaskTegra,
                    2 => FragShaderSrcMaskInvertedTegra,
                    3 => FragShaderSrcPremultipliedAlphaTegra,
                    4 => FragShaderSrcMaskPremultipliedAlphaTegra,
                    5 => FragShaderSrcMaskInvertedPremultipliedAlphaTegra,
                    _ => FragShaderSrcTegra,
                };
            }

            return variant switch
            {
                1 => FragShaderSrcMask_ES2,
                2 => FragShaderSrcMaskInverted_ES2,
                3 => FragShaderSrcPremultipliedAlpha_ES2,
                4 => FragShaderSrcMaskPremultipliedAlpha_ES2,
                5 => FragShaderSrcMaskInvertedPremultipliedAlpha_ES2,
                _ => FragShaderSrc_ES2,
            };
        }

        return variant switch
        {
            1 => FragShaderSrcMask_Normal,
            2 => FragShaderSrcMaskInverted_Normal,
            3 => FragShaderSrcPremultipliedAlpha_Normal,
            4 => FragShaderSrcMaskPremultipliedAlpha_Normal,
            5 => FragShaderSrcMaskInvertedPremultipliedAlpha_Normal,
            _ => FragShaderSrc_Normal,
        };
    }

    /// <summary>
    /// 変種番号からブレンド用バーテックスシェーダ本体を選ぶ（マスク有無）。
    /// </summary>
    private static string VariantBlendVertexBase(int variant)
    {
        return variant is 1 or 2 or 4 or 5 ? VertShaderSrcMaskedBlend_Base : VertShaderSrcBlend_Base;
    }

    /// <summary>
    /// 変種番号からブレンド用フラグメントシェーダ本体を選ぶ（マスク/乗算済アルファ）。
    /// </summary>
    private static string VariantBlendFragmentBase(int variant)
    {
        return variant switch
        {
            1 => FragShaderSrcMaskBlend_Base,
            2 => FragShaderSrcMaskInvertedBlend_Base,
            3 => FragShaderSrcPremultipliedAlphaBlend_Base,
            4 => FragShaderSrcMaskPremultipliedAlphaBlend_Base,
            5 => FragShaderSrcMaskInvertedPremultipliedAlphaBlend_Base,
            _ => FragShaderSrcBlend_Base,
        };
    }

    /// <summary>
    /// ブレンド用フラグメントシェーダを組み立てる。
    /// 官方 .cpp:961-1004 と同じく「#define 2 行 + ColorBlend/AlphaBlend スニペット連結」を行う。
    /// </summary>
    private string ComposeBlendFragment(string baseBody, int colorBlendMode, int alphaBlendMode)
    {
        string source = (gl.IsES2 ? GLES2C : Normal) + baseBody;
        source += "\n#define CSM_COLOR_BLEND_MODE " + colorBlendMode + "\n";
        source += FragShaderSrcColorBlendSnippet;
        source += "\n#define CSM_ALPHA_BLEND_MODE " + alphaBlendMode + "\n";
        source += FragShaderSrcAlphaBlendSnippet;
        return source;
    }

    /// <summary>
    /// Copy シェーダのロケーションを設定する（官方 .cpp:466-469）。
    /// </summary>
    private void SetShaderSetForCopy(CubismShaderSet shaderSet)
    {
        shaderSet.AttributePositionLocation = gl.GetAttribLocation(shaderSet.ShaderProgram, "a_position");
        shaderSet.AttributeTexCoordLocation = gl.GetAttribLocation(shaderSet.ShaderProgram, "a_texCoord");
        shaderSet.SamplerTexture0Location = gl.GetUniformLocation(shaderSet.ShaderProgram, "s_texture0");
        shaderSet.UniformBaseColorLocation = gl.GetUniformLocation(shaderSet.ShaderProgram, "u_baseColor");
    }

    /// <summary>
    /// SetupMask シェーダのロケーションを設定する（官方 .cpp:471-477）。
    /// SetupMask は SetShaderSet の 6 変種では扱わない（u_clipMatrix / u_channelFlag を必ず引く）。
    /// </summary>
    private void SetShaderSetForSetupMask(CubismShaderSet shaderSet)
    {
        shaderSet.AttributePositionLocation = gl.GetAttribLocation(shaderSet.ShaderProgram, "a_position");
        shaderSet.AttributeTexCoordLocation = gl.GetAttribLocation(shaderSet.ShaderProgram, "a_texCoord");
        shaderSet.SamplerTexture0Location = gl.GetUniformLocation(shaderSet.ShaderProgram, "s_texture0");
        shaderSet.UniformClipMatrixLocation = gl.GetUniformLocation(shaderSet.ShaderProgram, "u_clipMatrix");
        shaderSet.UnifromChannelFlagLocation = gl.GetUniformLocation(shaderSet.ShaderProgram, "u_channelFlag");
        shaderSet.UniformBaseColorLocation = gl.GetUniformLocation(shaderSet.ShaderProgram, "u_baseColor");
    }

    /// <summary>
    /// CubismShaderSet にロケーションを設定する（官方 SetShaderSet .cpp:142-185）。
    /// B9: MaskType で 1 か所に集約する（内聯 if 連鎖の錯配を排除）。
    /// </summary>
    private void SetShaderSet(CubismShaderSet shaderSets, MaskType maskType, bool isBlendMode)
    {
        shaderSets.AttributePositionLocation = gl.GetAttribLocation(shaderSets.ShaderProgram, "a_position");
        shaderSets.AttributeTexCoordLocation = gl.GetAttribLocation(shaderSets.ShaderProgram, "a_texCoord");
        shaderSets.SamplerTexture0Location = gl.GetUniformLocation(shaderSets.ShaderProgram, "s_texture0");
        if (isBlendMode)
        {
            shaderSets.SamplerBlendTextureLocation = gl.GetUniformLocation(shaderSets.ShaderProgram, "s_blendTexture");
        }
        shaderSets.UniformMatrixLocation = gl.GetUniformLocation(shaderSets.ShaderProgram, "u_matrix");
        shaderSets.UniformBaseColorLocation = gl.GetUniformLocation(shaderSets.ShaderProgram, "u_baseColor");
        shaderSets.UniformMultiplyColorLocation = gl.GetUniformLocation(shaderSets.ShaderProgram, "u_multiplyColor");
        shaderSets.UniformScreenColorLocation = gl.GetUniformLocation(shaderSets.ShaderProgram, "u_screenColor");

        switch (maskType)
        {
            case MaskType.None:
                break;
            case MaskType.Masked: // クリッピング
            case MaskType.MaskedInverted: // クリッピング・反転
            case MaskType.MaskedPremultipliedAlpha: // クリッピング、乘算済アルファ
            case MaskType.MaskedInvertedPremultipliedAlpha: // クリッピング・反転、乘算済アルファ
                shaderSets.SamplerTexture1Location = gl.GetUniformLocation(shaderSets.ShaderProgram, "s_texture1");
                shaderSets.UniformClipMatrixLocation = gl.GetUniformLocation(shaderSets.ShaderProgram, "u_clipMatrix");
                shaderSets.UnifromChannelFlagLocation = gl.GetUniformLocation(shaderSets.ShaderProgram, "u_channelFlag");
                break;
            case MaskType.PremultipliedAlpha: // 乘算済アルファ
            default:
                break;
        }
    }

    /// <summary>
    /// 描画用のシェーダプログラムの一連のセットアップを実行する
    /// （官方 SetupShaderProgramForDrawable .cpp:544-664）。
    /// </summary>
    internal void SetupShaderProgramForDrawable(CubismRenderer_OpenGLES2 renderer, CubismModel model, int index)
    {
        // _shaderSets用のオフセット計算
        bool masked = renderer.ClippingContextBufferForDrawable != null; // この描画オブジェクトはマスク対象か
        bool invertedMask = model.GetDrawableInvertedMask(index);
        bool isPremultipliedAlpha = renderer.IsPremultipliedAlpha;
        int offset = (masked ? (invertedMask ? 2 : 1) : 0) + (isPremultipliedAlpha ? 3 : 0);

        int shaderNameBegin = ShaderNames.GetShaderNamesBegin(model.GetDrawableBlendModeType(index));
        CubismShaderSet shaderSet = GetShaderSet(shaderNameBegin + offset);
        bool isBlendMode = shaderNameBegin >= ShaderNames.NormalAtop;

        int SRC_COLOR;
        int DST_COLOR;
        int SRC_ALPHA;
        int DST_ALPHA;
        int blendTexture = 0;

        if (isBlendMode)
        {
            // 5.3 以降
            SRC_COLOR = gl.GL_ONE;
            DST_COLOR = gl.GL_ZERO;
            SRC_ALPHA = gl.GL_ONE;
            DST_ALPHA = gl.GL_ZERO;

            // 以前のオフスクリーンのテクスチャを取得
            var currentOffscreen = renderer.GetCurrentOffscreen();
            blendTexture = currentOffscreen != null
                ? renderer.CopyRenderTarget(currentOffscreen.GetRenderTarget()!)?.GetColorBuffer() ?? 0
                : renderer.CopyOffscreenRenderTarget()?.GetColorBuffer() ?? 0;
        }
        else
        {
            switch (shaderNameBegin)
            {
                case ShaderNames.Add:
                    // 5.2 以前
                    SRC_COLOR = gl.GL_ONE;
                    DST_COLOR = gl.GL_ONE;
                    SRC_ALPHA = gl.GL_ZERO;
                    DST_ALPHA = gl.GL_ONE;
                    break;
                case ShaderNames.Mult:
                    // 5.2 以前
                    SRC_COLOR = gl.GL_DST_COLOR;
                    DST_COLOR = gl.GL_ONE_MINUS_SRC_ALPHA;
                    SRC_ALPHA = gl.GL_ZERO;
                    DST_ALPHA = gl.GL_ONE;
                    break;
                case ShaderNames.Normal:
                default:
                    // 5.2 以前
                    SRC_COLOR = gl.GL_ONE;
                    DST_COLOR = gl.GL_ONE_MINUS_SRC_ALPHA;
                    SRC_ALPHA = gl.GL_ONE;
                    DST_ALPHA = gl.GL_ONE_MINUS_SRC_ALPHA;
                    break;
            }
        }

        gl.UseProgram(shaderSet.ShaderProgram);

        // テクスチャ設定
        SetupTexture(renderer, model, index, shaderSet);

        // 頂点属性設定
        SetVertexAttributes(shaderSet);

        if (masked)
        {
            gl.ActiveTexture(gl.GL_TEXTURE1);

            // frameBufferに書かれたテクスチャ
            var draw = renderer.ClippingContextBufferForDrawable!;
            int tex = renderer.GetDrawableMaskBuffer(draw.BufferIndex).ColorBuffer;

            gl.BindTexture(gl.GL_TEXTURE_2D, tex);
            gl.Uniform1i(shaderSet.SamplerTexture1Location, 1);

            // View座標をClippingContextの座標に変換するための行列を設定
            gl.UniformMatrix4fv(shaderSet.UniformClipMatrixLocation, 1, false, draw.MatrixForDraw.Tr);

            // 使用するカラーチャンネルを設定
            SetColorChannelUniformVariables(shaderSet, draw);
        }

        // ブレンド設定（B15: blend テクスチャは texture unit 2）
        if (isBlendMode)
        {
            gl.ActiveTexture(gl.GL_TEXTURE2);
            gl.BindTexture(gl.GL_TEXTURE_2D, blendTexture);
            gl.Uniform1i(shaderSet.SamplerBlendTextureLocation, 2);
        }

        // 座標変換
        gl.UniformMatrix4fv(shaderSet.UniformMatrixLocation, 1, false, renderer.GetMvpMatrix().Tr);

        // ユニフォーム変数設定
        CubismTextureColor baseColor = new();
        if (model.IsBlendModeEnabled())
        {
            // ブレンドモードではモデルカラーは最後に処理するため不透明度のみ対応させる
            float drawableOpacity = model.GetDrawableOpacity(index);
            baseColor.A = drawableOpacity;
            if (isPremultipliedAlpha)
            {
                baseColor.R = drawableOpacity;
                baseColor.G = drawableOpacity;
                baseColor.B = drawableOpacity;
            }
        }
        else
        {
            // ブレンドモード使用しない場合はDrawable単位でモデルカラーを処理する
            baseColor = renderer.GetModelColorWithOpacity(model.GetDrawableOpacity(index));
        }

        var overrideMultiplyAndScreenColor = model.GetOverrideMultiplyAndScreenColor();
        CubismTextureColor multiplyColor = overrideMultiplyAndScreenColor.GetDrawableMultiplyColor(index);
        CubismTextureColor screenColor = overrideMultiplyAndScreenColor.GetDrawableScreenColor(index);
        SetColorUniformVariables(shaderSet, baseColor, multiplyColor, screenColor);

        gl.BlendFuncSeparate(SRC_COLOR, DST_COLOR, SRC_ALPHA, DST_ALPHA);
    }

    /// <summary>
    /// マスク用のシェーダプログラムの一連のセットアップを実行する
    /// （官方 SetupShaderProgramForMask .cpp:666-694）。
    /// B14: 公式 R5 同様、マスク描画では乗算色・スクリーン色を設定しない。
    /// </summary>
    internal void SetupShaderProgramForMask(CubismRenderer_OpenGLES2 renderer, CubismModel model, int index)
    {
        int SRC_COLOR = gl.GL_ZERO;
        int DST_COLOR = gl.GL_ONE_MINUS_SRC_COLOR;
        int SRC_ALPHA = gl.GL_ZERO;
        int DST_ALPHA = gl.GL_ONE_MINUS_SRC_ALPHA;

        CubismShaderSet shaderSet = GetShaderSet(ShaderNames.SetupMask);
        gl.UseProgram(shaderSet.ShaderProgram);

        var draw = renderer.ClippingContextBufferForMask!;

        // テクスチャ設定
        SetupTexture(renderer, model, index, shaderSet);

        // 頂点属性設定
        SetVertexAttributes(shaderSet);

        // 使用するカラーチャンネルを設定
        SetColorChannelUniformVariables(shaderSet, draw);

        gl.UniformMatrix4fv(shaderSet.UniformClipMatrixLocation, 1, false, draw.MatrixForMask.Tr);

        // ユニフォーム変数設定（baseColor のみ。B14 により乗算色/スクリーン色は設定しない）
        RectF rect = draw.LayoutBounds;
        gl.Uniform4f(shaderSet.UniformBaseColorLocation,
            rect.X * 2.0f - 1.0f, rect.Y * 2.0f - 1.0f, rect.GetRight() * 2.0f - 1.0f, rect.GetBottom() * 2.0f - 1.0f);

        gl.BlendFuncSeparate(SRC_COLOR, DST_COLOR, SRC_ALPHA, DST_ALPHA);
    }

    /// <summary>
    /// オフスクリーンからのシェーダプログラムの一連のセットアップを実行する
    /// （官方 SetupShaderProgramForOffscreenRenderTarget .cpp:696-705）。
    /// </summary>
    internal void SetupShaderProgramForOffscreenRenderTarget(CubismRenderer_OpenGLES2 renderer)
    {
        var source = renderer.CopyOffscreenRenderTarget();
        int texture = source?.GetColorBuffer() ?? 0;

        // この時点のテクスチャはPMAになっているはずなので計算を行う
        CubismTextureColor baseColor = renderer.ModelColor;
        baseColor.R *= baseColor.A;
        baseColor.G *= baseColor.A;
        baseColor.B *= baseColor.A;
        CopyTexture(renderer, texture, gl.GL_ONE, gl.GL_ONE_MINUS_SRC_ALPHA, gl.GL_ONE, gl.GL_ONE_MINUS_SRC_ALPHA, baseColor);
    }

    /// <summary>
    /// オフスクリーン描画用のシェーダプログラムの一連のセットアップを実行する
    /// （官方 SetupShaderProgramForOffscreen .cpp:731-844）。
    /// </summary>
    internal void SetupShaderProgramForOffscreen(CubismRenderer_OpenGLES2 renderer, CubismModel model,
        CubismOffscreenRenderTarget_OpenGLES2 offscreen)
    {
        int SRC_COLOR;
        int DST_COLOR;
        int SRC_ALPHA;
        int DST_ALPHA;

        int offscreenIndex = offscreen.GetOffscreenIndex();
        // _shaderSets用のオフセット計算
        bool masked = renderer.ClippingContextBufferForOffscreen != null; // この描画オブジェクトはマスク対象か
        bool invertedMask = model.GetOffscreenInvertedMask(offscreenIndex);
        // オフスクリーンは乗算済アルファを利用する
        const int usePremultipliedAlphaOffset = 3;
        int offset = (masked ? (invertedMask ? 2 : 1) : 0) + usePremultipliedAlphaOffset;

        int shaderNameBegin = ShaderNames.GetShaderNamesBegin(model.GetOffscreenBlendModeType(offscreenIndex));
        CubismShaderSet shaderSet = GetShaderSet(shaderNameBegin + offset);
        bool isBlendMode = shaderNameBegin >= ShaderNames.NormalAtop;
        int blendTexture = 0;

        if (isBlendMode)
        {
            // 5.3 以降
            SRC_COLOR = gl.GL_ONE;
            DST_COLOR = gl.GL_ZERO;
            SRC_ALPHA = gl.GL_ONE;
            DST_ALPHA = gl.GL_ZERO;

            // 以前のオフスクリーンのテクスチャを取得
            var oldOffscreen = offscreen.GetOldOffscreen();
            blendTexture = oldOffscreen != null
                ? renderer.CopyRenderTarget(oldOffscreen.GetRenderTarget()!)?.GetColorBuffer() ?? 0
                : renderer.CopyOffscreenRenderTarget()?.GetColorBuffer() ?? 0;
        }
        else
        {
            switch (shaderNameBegin)
            {
                case ShaderNames.Add:
                    SRC_COLOR = gl.GL_ONE;
                    DST_COLOR = gl.GL_ONE;
                    SRC_ALPHA = gl.GL_ZERO;
                    DST_ALPHA = gl.GL_ONE;
                    break;
                case ShaderNames.Mult:
                    SRC_COLOR = gl.GL_DST_COLOR;
                    DST_COLOR = gl.GL_ONE_MINUS_SRC_ALPHA;
                    SRC_ALPHA = gl.GL_ZERO;
                    DST_ALPHA = gl.GL_ONE;
                    break;
                case ShaderNames.Normal:
                default:
                    SRC_COLOR = gl.GL_ONE;
                    DST_COLOR = gl.GL_ONE_MINUS_SRC_ALPHA;
                    SRC_ALPHA = gl.GL_ONE;
                    DST_ALPHA = gl.GL_ONE_MINUS_SRC_ALPHA;
                    break;
            }
        }

        gl.UseProgram(shaderSet.ShaderProgram);

        // オフスクリーンのテクスチャ設定
        gl.ActiveTexture(gl.GL_TEXTURE0);
        int tex = offscreen.GetRenderTarget()?.GetColorBuffer() ?? 0;
        gl.BindTexture(gl.GL_TEXTURE_2D, tex);
        gl.Uniform1i(shaderSet.SamplerTexture0Location, 0);

        // 頂点属性設定（オフスクリーンは上下反転）
        renderer.SetRenderTargetVertexAttributes(shaderSet, true);

        if (masked)
        {
            gl.ActiveTexture(gl.GL_TEXTURE1);

            var ctx = renderer.ClippingContextBufferForOffscreen!;
            int maskTex = renderer.GetOffscreenMaskBuffer(ctx.BufferIndex).ColorBuffer;
            gl.BindTexture(gl.GL_TEXTURE_2D, maskTex);
            gl.Uniform1i(shaderSet.SamplerTexture1Location, 1);

            gl.UniformMatrix4fv(shaderSet.UniformClipMatrixLocation, 1, false, ctx.MatrixForDraw.Tr);

            SetColorChannelUniformVariables(shaderSet, ctx);
        }

        if (isBlendMode)
        {
            gl.ActiveTexture(gl.GL_TEXTURE2);
            gl.BindTexture(gl.GL_TEXTURE_2D, blendTexture);
            gl.Uniform1i(shaderSet.SamplerBlendTextureLocation, 2);
        }

        // 座標変換（恒等行列）
        var mvpMatrix = new CubismMatrix44();
        mvpMatrix.LoadIdentity();
        gl.UniformMatrix4fv(shaderSet.UniformMatrixLocation, 1, false, mvpMatrix.Tr);

        // ユニフォーム変数設定
        float offscreenOpacity = model.GetOffscreenOpacity(offscreenIndex);
        // PMAなのと不透明度だけを変更したいためすべてOpacityで初期化
        CubismTextureColor baseColor = new(offscreenOpacity, offscreenOpacity, offscreenOpacity, offscreenOpacity);
        var overrideMultiplyAndScreenColor = model.GetOverrideMultiplyAndScreenColor();
        CubismTextureColor multiplyColor = overrideMultiplyAndScreenColor.GetOffscreenMultiplyColor(offscreenIndex);
        CubismTextureColor screenColor = overrideMultiplyAndScreenColor.GetOffscreenScreenColor(offscreenIndex);
        SetColorUniformVariables(shaderSet, baseColor, multiplyColor, screenColor);

        gl.BlendFuncSeparate(SRC_COLOR, DST_COLOR, SRC_ALPHA, DST_ALPHA);
    }

    /// <summary>
    /// シェーダを使って描画対象をコピーする（官方 CopyTexture .cpp:707-729）。
    /// </summary>
    internal void CopyTexture(CubismRenderer_OpenGLES2 renderer, int texture, int srcColor, int dstColor,
        int srcAlpha, int dstAlpha, CubismTextureColor baseColor)
    {
        CubismShaderSet shaderSet = GetShaderSet(ShaderNames.Copy);
        gl.UseProgram(shaderSet.ShaderProgram);

        // オフスクリーンの内容を設定
        gl.ActiveTexture(gl.GL_TEXTURE0);
        gl.BindTexture(gl.GL_TEXTURE_2D, texture);
        gl.Uniform1i(shaderSet.SamplerTexture0Location, 0);

        // 頂点属性（画面全体のクワッド）
        renderer.SetRenderTargetVertexAttributes(shaderSet, false);

        // ベースカラーの設定
        gl.Uniform4f(shaderSet.UniformBaseColorLocation, baseColor.R, baseColor.G, baseColor.B, baseColor.A);

        gl.BlendFuncSeparate(srcColor, dstColor, srcAlpha, dstAlpha);
    }

    /// <summary>
    /// シェーダプログラムを解放する（共有プログラムの二重解放を避ける）
    /// </summary>
    internal void ReleaseShaderProgram()
    {
        HashSet<int> released = [];

        for (int i = 0; i < _shaderSets.Count; i++)
        {
            int program = _shaderSets[i].ShaderProgram;
            if (program == 0)
            {
                continue;
            }

            if (released.Add(program))
            {
                gl.DeleteProgram(program);
            }

            _shaderSets[i].ShaderProgram = 0;
        }

        Array.Clear(_compiled);
    }

    /// <summary>
    /// シェーダプログラムをロードしてアドレス返す。
    /// </summary>
    /// <param name="vertShaderSrc">頂点シェーダのソース</param>
    /// <param name="fragShaderSrc">フラグメントシェーダのソース</param>
    /// <returns>シェーダプログラムのアドレス</returns>
    internal unsafe int LoadShaderProgram(string vertShaderSrc, string fragShaderSrc)
    {
        // Create shader program.
        int shaderProgram = gl.CreateProgram();

        if (!CompileShaderSource(out int vertShader, gl.GL_VERTEX_SHADER, vertShaderSrc))
        {
            CubismLog.Error("[Live2D OpenGL]Vertex shader compile error!");
            return 0;
        }

        // Create and compile fragment shader.
        if (!CompileShaderSource(out int fragShader, gl.GL_FRAGMENT_SHADER, fragShaderSrc))
        {
            CubismLog.Error("[Live2D OpenGL]Fragment shader compile error!");
            return 0;
        }

        // Attach vertex shader to program.
        gl.AttachShader(shaderProgram, vertShader);

        // Attach fragment shader to program.
        gl.AttachShader(shaderProgram, fragShader);

        // Link program.
        if (!LinkProgram(shaderProgram))
        {
            CubismLog.Error($"[Live2D OpenGL]Failed to link program: {shaderProgram}");

            if (vertShader != 0)
            {
                gl.DeleteShader(vertShader);
            }
            if (fragShader != 0)
            {
                gl.DeleteShader(fragShader);
            }
            if (shaderProgram != 0)
            {
                gl.DeleteProgram(shaderProgram);
            }

            return 0;
        }

        // Release vertex and fragment shaders.
        if (vertShader != 0)
        {
            gl.DetachShader(shaderProgram, vertShader);
            gl.DeleteShader(vertShader);
        }

        if (fragShader != 0)
        {
            gl.DetachShader(shaderProgram, fragShader);
            gl.DeleteShader(fragShader);
        }

        return shaderProgram;
    }

    /// <summary>
    /// シェーダプログラムをコンパイルする
    /// </summary>
    internal unsafe bool CompileShaderSource(out int outShader, int shaderType, string shaderSource)
    {
        int status;

        outShader = gl.CreateShader(shaderType);
        gl.ShaderSource(outShader, shaderSource);
        gl.CompileShader(outShader);

        int logLength;
        gl.GetShaderiv(outShader, gl.GL_INFO_LOG_LENGTH, &logLength);
        if (logLength > 0)
        {
            gl.GetShaderInfoLog(outShader, out string log);
            CubismLog.Error($"[Live2D OpenGL]Shader compile log: {log}");
        }

        gl.GetShaderiv(outShader, gl.GL_COMPILE_STATUS, &status);
        if (status == gl.GL_FALSE)
        {
            gl.DeleteShader(outShader);
            return false;
        }

        return true;
    }

    /// <summary>
    /// シェーダプログラムをリンクする
    /// </summary>
    internal unsafe bool LinkProgram(int shaderProgram)
    {
        int status;
        gl.LinkProgram(shaderProgram);

        int logLength;
        gl.GetProgramiv(shaderProgram, gl.GL_INFO_LOG_LENGTH, &logLength);
        if (logLength > 0)
        {
            gl.GetProgramInfoLog(shaderProgram, out string log);
            CubismLog.Error($"[Live2D OpenGL]Program link log: {log}");
        }

        gl.GetProgramiv(shaderProgram, gl.GL_LINK_STATUS, &status);
        if (status == gl.GL_FALSE)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// シェーダプログラムを検証する
    /// </summary>
    internal unsafe bool ValidateProgram(int shaderProgram)
    {
        int logLength, status;

        gl.ValidateProgram(shaderProgram);
        gl.GetProgramiv(shaderProgram, gl.GL_INFO_LOG_LENGTH, &logLength);
        if (logLength > 0)
        {
            gl.GetProgramInfoLog(shaderProgram, out string log);
            CubismLog.Error($"[Live2D OpenGL]Validate program log: {log}");
        }

        gl.GetProgramiv(shaderProgram, gl.GL_VALIDATE_STATUS, &status);
        if (status == gl.GL_FALSE)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Tegraプロセッサ対応。拡張方式による描画の有効・無効
    /// </summary>
    internal void SetExtShaderMode(bool extMode, bool extPAMode)
    {
        s_extMode = extMode;
        s_extPAMode = extPAMode;
    }

    /// <summary>
    /// 必要な頂点属性を設定する
    /// </summary>
    internal void SetVertexAttributes(CubismShaderSet shaderSet)
    {
        // 頂点位置属性の設定
        gl.EnableVertexAttribArray(shaderSet.AttributePositionLocation);
        gl.VertexAttribPointer(shaderSet.AttributePositionLocation, 2, gl.GL_FLOAT, false, 4 * sizeof(float), 0);

        // テクスチャ座標属性の設定
        gl.EnableVertexAttribArray(shaderSet.AttributeTexCoordLocation);
        gl.VertexAttribPointer(shaderSet.AttributeTexCoordLocation, 2, gl.GL_FLOAT, false, 4 * sizeof(float), 2 * sizeof(float));
    }

    /// <summary>
    /// テクスチャの設定を行う
    /// </summary>
    internal void SetupTexture(CubismRenderer_OpenGLES2 renderer, CubismModel model, int index, CubismShaderSet shaderSet)
    {
        int textureIndex = model.GetDrawableTextureIndex(index);
        int textureId = renderer.GetBindedTextureId(textureIndex);
        gl.ActiveTexture(gl.GL_TEXTURE0);
        gl.BindTexture(gl.GL_TEXTURE_2D, textureId);
        gl.Uniform1i(shaderSet.SamplerTexture0Location, 0);
    }

    /// <summary>
    /// 色関連のユニフォーム変数の設定を行う
    /// </summary>
    internal void SetColorUniformVariables(CubismShaderSet shaderSet,
        CubismTextureColor baseColor, CubismTextureColor multiplyColor, CubismTextureColor screenColor)
    {
        gl.Uniform4f(shaderSet.UniformBaseColorLocation, baseColor.R, baseColor.G, baseColor.B, baseColor.A);
        gl.Uniform4f(shaderSet.UniformMultiplyColorLocation, multiplyColor.R, multiplyColor.G, multiplyColor.B, multiplyColor.A);
        gl.Uniform4f(shaderSet.UniformScreenColorLocation, screenColor.R, screenColor.G, screenColor.B, screenColor.A);
    }

    /// <summary>
    /// カラーチャンネル関連のユニフォーム変数の設定を行う
    /// </summary>
    internal void SetColorChannelUniformVariables(CubismShaderSet shaderSet, CubismClippingContext contextBuffer)
    {
        int channelIndex = contextBuffer.LayoutChannelIndex;
        CubismTextureColor colorChannel = contextBuffer.Manager.GetChannelFlagAsColor(channelIndex);
        gl.Uniform4f(shaderSet.UnifromChannelFlagLocation, colorChannel.R, colorChannel.G, colorChannel.B, colorChannel.A);
    }
}


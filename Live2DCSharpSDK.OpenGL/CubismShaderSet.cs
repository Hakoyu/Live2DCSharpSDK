namespace Live2DCSharpSDK.OpenGL;

internal record CubismShaderSet
{
    /// <summary>
    /// シェーダプログラムのアドレス
    /// </summary>
    internal int ShaderProgram;
    /// <summary>
    /// 传给 Shader 程序的变量地址(Position)
    /// </summary>
    internal int AttributePositionLocation;
    /// <summary>
    /// 传给 Shader 程序的变量地址(TexCoord)
    /// </summary>
    internal int AttributeTexCoordLocation;
    /// <summary>
    /// 传给 Shader 程序的变量地址(Matrix)
    /// </summary>
    internal int UniformMatrixLocation;
    /// <summary>
    /// 传给 Shader 程序的变量地址(ClipMatrix)
    /// </summary>
    internal int UniformClipMatrixLocation;
    /// <summary>
    /// 传给 Shader 程序的变量地址(Texture0)
    /// </summary>
    internal int SamplerTexture0Location;
    /// <summary>
    /// 传给 Shader 程序的变量地址(Texture1)
    /// </summary>
    internal int SamplerTexture1Location;
    /// <summary>
    /// 传给着色器程序的变量地址(BlendTexture, 官方 .hpp:184).
    /// </summary>
    internal int SamplerBlendTextureLocation;
    /// <summary>
    /// 传给 Shader 程序的变量地址(BaseColor)
    /// </summary>
    internal int UniformBaseColorLocation;
    /// <summary>
    /// 传给 Shader 程序的变量地址(MultiplyColor)
    /// </summary>
    internal int UniformMultiplyColorLocation;
    /// <summary>
    /// 传给 Shader 程序的变量地址(ScreenColor)
    /// </summary>
    internal int UniformScreenColorLocation;
    /// <summary>
    /// 传给 Shader 程序的变量地址(ChannelFlag)
    /// </summary>
    internal int UnifromChannelFlagLocation;
};

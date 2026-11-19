using System.ComponentModel;

namespace Live2DCSharpSDK.Framework;

public record ModelSettingObj
{
    public record FileReference
    {
        public record Expression
        {
            public string Name { get; set; }
            public string File { get; set; }
        }

        public record Motion
        {
            public string File { get; set; }
            public string Sound { get; set; }

            // 注: [DefaultValue(-1f)] 在 System.Text.Json(含 source-gen)中
            // 不会作用于反序列化的默认值(未记录的属性会变成 0).
            // 为与官方 GetMotionFadeInTimeValue 的 "未记录 = -1(不覆盖)" 语义一致,
            // 用等价的手段(属性初始化器)将 -1 设为默认值.
            [DefaultValue(-1f)]
            public float FadeInTime { get; set; } = -1f;

            [DefaultValue(-1f)]
            public float FadeOutTime { get; set; } = -1f;
        }

        public string Moc { get; set; }
        public List<string> Textures { get; set; }
        public string Physics { get; set; }
        public string Pose { get; set; }
        public string DisplayInfo { get; set; }
        public List<Expression> Expressions { get; set; }
        public Dictionary<string, List<Motion>> Motions { get; set; }
        public string UserData { get; set; }
    }

    public record HitArea
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public record Parameter
    {
        public string Name { get; set; }
        public List<string> Ids { get; set; }
    }

    public FileReference FileReferences { get; set; }
    public List<HitArea> HitAreas { get; set; }
    public Dictionary<string, float> Layout { get; set; }
    public List<Parameter> Groups { get; set; }
}

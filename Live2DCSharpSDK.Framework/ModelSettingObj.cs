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
            // 注: [DefaultValue(-1f)] は System.Text.Json（source-gen 含む）では
            // 逆シリアル化時の既定値に反映されない（未記載プロパティは 0 になる）。
            // 官方 GetMotionFadeInTimeValue の「未記載 = -1（上書きしない）」に合わせるため、
            // 等価な手段であるプロパティ初期化子で -1 を既定値にする（t15）。
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

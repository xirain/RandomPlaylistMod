using System.Collections.Generic;

namespace RandomPlaylistMod.Models
{
    public class SessionSettings
    {
        public int DurationMinutes { get; set; } = 30;
        public float MinNps { get; set; } = 0f;
        public float MaxNps { get; set; } = 99f;
        /// <summary>是否不按 NPS 筛选（Any）</summary>
        public bool NpsAny { get; set; } = true;
        /// <summary>选中的具体频段（min,max 列表，Any 时不使用）</summary>
        public List<(float min, float max)> NpsBands { get; set; } = new List<(float, float)>();
        public bool NoFailEnabled { get; set; } = false;
    }
}

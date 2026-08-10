using System;
using System.Collections.Generic;
using System.Linq;

namespace RandomPlaylistMod.Models
{
    /// <summary>
    /// 关卡难度频段（按 NPS = 每秒音符数划分）。
    /// 用户可在 UI 中多选，多个频段取并集作为筛选条件；"Any" 表示不做 NPS 过滤。
    /// </summary>
    public class LevelBand
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public float Min { get; set; }
        public float Max { get; set; }
        public bool IsAny { get; set; }

        /// <summary>频段定义（顺序即 UI 展示顺序）</summary>
        public static readonly List<LevelBand> All = new List<LevelBand>
        {
            new LevelBand { Id = "any", Label = "Any", Min = 0f, Max = 99f, IsAny = true },
            new LevelBand { Id = "4-7", Label = "4-7", Min = 4f, Max = 7f },
            new LevelBand { Id = "7-8", Label = "7-8", Min = 7f, Max = 8f },
            new LevelBand { Id = "8-9", Label = "8-9", Min = 8f, Max = 9f },
            new LevelBand { Id = "9+",  Label = "9+",  Min = 9f, Max = 99f },
        };

        public static LevelBand GetById(string id) => All.FirstOrDefault(b => b.Id == id);

        /// <summary>
        /// 判定某个 NPS 是否落在所选频段并集内。
        /// any=true 或 nps&lt;0（未知）时始终通过。
        /// </summary>
        public static bool InBands(float nps, List<(float min, float max)> bands, bool any)
        {
            if (any || nps < 0f)
                return true;
            foreach (var b in bands)
            {
                if (nps >= b.min && nps <= b.max)
                    return true;
            }
            return false;
        }

        /// <summary>将所选频段 id 列表转为 (min,max) 列表（Any 会被忽略）</summary>
        public static List<(float min, float max)> ToRanges(IEnumerable<string> ids)
        {
            var ranges = new List<(float, float)>();
            foreach (var id in ids)
            {
                var band = GetById(id);
                if (band != null && !band.IsAny)
                    ranges.Add((band.Min, band.Max));
            }
            return ranges;
        }

        /// <summary>所选频段的包围盒（用于展示/兼容旧逻辑）。无任何具体频段时返回 0-99。</summary>
        public static (float min, float max) BoundingBox(List<(float min, float max)> bands)
        {
            if (bands == null || bands.Count == 0)
                return (0f, 99f);
            float min = float.MaxValue, max = float.MinValue;
            foreach (var b in bands)
            {
                if (b.min < min) min = b.min;
                if (b.max > max) max = b.max;
            }
            return (min, max);
        }
    }
}

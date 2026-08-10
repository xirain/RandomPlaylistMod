using System;
using System.Collections.Generic;

namespace RandomPlaylistMod.Models
{
    /// <summary>
    /// 单次会话的完整记录 — Phase 2 核心数据模型
    /// </summary>
    public class SessionRecord
    {
        /// <summary>唯一会话标识，格式 yyyyMMdd-HHmmss-guid8</summary>
        public string SessionId { get; set; } = "";

        /// <summary>会话开始时间</summary>
        public DateTime StartedAt { get; set; }

        /// <summary>会话结束时间</summary>
        public DateTime EndedAt { get; set; }

        /// <summary>目标时长（分钟）</summary>
        public int TargetDurationMin { get; set; }

        /// <summary>实际时长（分钟，四舍五入）</summary>
        public int ActualDurationMin { get; set; }

        /// <summary>选中的播放列表 ID 列表</summary>
        public List<string> PlaylistIds { get; set; } = new List<string>();

        /// <summary>选中的播放列表名称列表</summary>
        public List<string> PlaylistNames { get; set; } = new List<string>();

        /// <summary>队列中的总歌曲数</summary>
        public int TotalSongsInQueue { get; set; }

        /// <summary>实际播放的歌曲数</summary>
        public int TotalSongsPlayed { get; set; }

        /// <summary>每首歌的详细结果</summary>
        public List<SongResult> SongResults { get; set; } = new List<SongResult>();

        /// <summary>本次会话的运动数据汇总</summary>
        public ExerciseSummary ExerciseSummary { get; set; } = new ExerciseSummary();

        /// <summary>本次使用的设置快照</summary>
        public SessionSettingsSnapshot Settings { get; set; } = new SessionSettingsSnapshot();

        /// <summary>用户自定义标签（预留扩展）</summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>模组版本号</summary>
        public string ModVersion { get; set; } = "";

        /// <summary>
        /// 生成 SessionId（yyyyMMdd-HHmmss-guid8）
        /// </summary>
        public static string GenerateId()
        {
            var now = DateTime.Now;
            var guid = Guid.NewGuid().ToString("N").Substring(0, 8);
            return $"{now:yyyyMMdd}-{now:HHmmss}-{guid}";
        }
    }

    /// <summary>
    /// 会话设置快照
    /// </summary>
    public class SessionSettingsSnapshot
    {
        /// <summary>NPS 下限（所选频段包围盒，仅用于展示）</summary>
        public float MinNPS { get; set; }

        /// <summary>NPS 上限（所选频段包围盒，仅用于展示）</summary>
        public float MaxNPS { get; set; }

        /// <summary>是否不按 NPS 筛选（Any）</summary>
        public bool NpsAny { get; set; } = true;

        /// <summary>选中的具体频段标签，如 ["4-7","8-9"]；Any 时为空</summary>
        public List<string> NpsBandLabels { get; set; } = new List<string>();

        /// <summary>是否启用 No Fail</summary>
        public bool NoFailEnabled { get; set; }

        /// <summary>是否启用 HUD</summary>
        public bool HudEnabled { get; set; }

        /// <summary>选中的播放列表数量</summary>
        public int PlaylistCount { get; set; }

        /// <summary>可选歌曲总数（筛选前）</summary>
        public int AvailableSongCount { get; set; }
    }
}

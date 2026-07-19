using System;
using System.Collections.Generic;
using System.Linq;

namespace RandomPlaylistMod.Models
{
    /// <summary>
    /// 玩家聚合统计数据，汇总所有历史会话
    /// </summary>
    public class PlayerProfile
    {
        /// <summary>总会话数</summary>
        public int TotalSessions { get; set; }

        /// <summary>总游戏时长（分钟）</summary>
        public int TotalPlayTimeMin { get; set; }

        /// <summary>总播放歌曲数</summary>
        public int TotalSongsPlayed { get; set; }

        /// <summary>累积总得分</summary>
        public long TotalScore { get; set; }

        /// <summary>历史最佳精度</summary>
        public float BestAccuracy { get; set; }

        /// <summary>历史全连总数</summary>
        public int FullComboTotal { get; set; }

        /// <summary>历史最佳评级</summary>
        public string HighestRank { get; set; } = "";

        /// <summary>最常使用的播放列表名称</summary>
        public string FavoritePlaylist { get; set; } = "";

        /// <summary>最后游戏时间</summary>
        public DateTime LastPlayedAt { get; set; }

        /// <summary>首次游戏时间</summary>
        public DateTime FirstPlayedAt { get; set; }

        /// <summary>连续游戏天数</summary>
        public int DailyStreak { get; set; }

        /// <summary>
        /// 从 SessionRecord 列表重建 PlayerProfile
        /// </summary>
        public static PlayerProfile FromSessions(List<SessionRecord> sessions)
        {
            if (sessions == null || sessions.Count == 0)
                return new PlayerProfile();

            var profile = new PlayerProfile
            {
                TotalSessions = sessions.Count,
                TotalPlayTimeMin = sessions.Sum(s => s.ActualDurationMin),
                TotalSongsPlayed = sessions.Sum(s => s.TotalSongsPlayed),
                TotalScore = sessions.Sum(s => (long)s.ExerciseSummary.TotalScore),
                FullComboTotal = sessions.Sum(s => s.ExerciseSummary.FullComboCount),
                BestAccuracy = sessions.Any()
                    ? (float)Math.Round(sessions.Max(s => s.ExerciseSummary.AverageAccuracy), 2)
                    : 0f,
                FirstPlayedAt = sessions.Min(s => s.StartedAt),
                LastPlayedAt = sessions.Max(s => s.EndedAt)
            };

            // 最佳评级
            var rankOrder = new[] { "SS", "S", "A", "B", "C", "D", "E" };
            string bestRank = "E";
            foreach (var s in sessions)
            {
                var rank = s.ExerciseSummary.BestRank?.Trim().ToUpper() ?? "";
                foreach (var candidate in rankOrder)
                {
                    if (rank == candidate && System.Array.IndexOf(rankOrder, candidate) < System.Array.IndexOf(rankOrder, bestRank))
                    {
                        bestRank = candidate;
                        break;
                    }
                }
            }
            profile.HighestRank = bestRank;

            // 最常用歌单
            var playlistUsage = new Dictionary<string, int>();
            foreach (var s in sessions)
            {
                if (s.PlaylistNames != null)
                {
                    foreach (var name in s.PlaylistNames)
                    {
                        if (string.IsNullOrEmpty(name)) continue;
                        if (playlistUsage.ContainsKey(name))
                            playlistUsage[name]++;
                        else
                            playlistUsage[name] = 1;
                    }
                }
            }
            profile.FavoritePlaylist = playlistUsage
                .OrderByDescending(kvp => kvp.Value)
                .FirstOrDefault().Key ?? "";

            // 连续天数
            profile.DailyStreak = CalculateDailyStreak(sessions);

            return profile;
        }

        /// <summary>
        /// 基于最新一条 SessionRecord 更新存量 Profile
        /// </summary>
        public static PlayerProfile UpdateWithSession(PlayerProfile existing, SessionRecord newSession)
        {
            if (existing == null || newSession == null)
                return existing ?? new PlayerProfile();

            existing.TotalSessions++;
            existing.TotalPlayTimeMin += newSession.ActualDurationMin;
            existing.TotalSongsPlayed += newSession.TotalSongsPlayed;
            existing.TotalScore += newSession.ExerciseSummary.TotalScore;
            existing.FullComboTotal += newSession.ExerciseSummary.FullComboCount;

            if (newSession.ExerciseSummary.AverageAccuracy > existing.BestAccuracy)
                existing.BestAccuracy = (float)Math.Round(newSession.ExerciseSummary.AverageAccuracy, 2);

            // 最佳评级
            var rankOrder = new[] { "SS", "S", "A", "B", "C", "D", "E" };
            var newRank = newSession.ExerciseSummary.BestRank?.Trim().ToUpper() ?? "";
            var curRank = existing.HighestRank?.Trim().ToUpper() ?? "E";
            if (System.Array.IndexOf(rankOrder, newRank) < 0) newRank = "E";
            if (System.Array.IndexOf(rankOrder, curRank) < 0) curRank = "E";
            if (System.Array.IndexOf(rankOrder, newRank) < System.Array.IndexOf(rankOrder, curRank))
                existing.HighestRank = newRank;

            // 更新时间
            if (existing.FirstPlayedAt == default || newSession.StartedAt < existing.FirstPlayedAt)
                existing.FirstPlayedAt = newSession.StartedAt;
            if (newSession.EndedAt > existing.LastPlayedAt)
                existing.LastPlayedAt = newSession.EndedAt;

            return existing;
        }

        /// <summary>
        /// 计算连续游戏天数
        /// </summary>
        private static int CalculateDailyStreak(List<SessionRecord> sessions)
        {
            if (sessions == null || sessions.Count == 0) return 0;

            // 按日期去重，排序
            var uniqueDays = sessions
                .Select(s => s.StartedAt.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            if (uniqueDays.Count == 0) return 0;

            var today = DateTime.Now.Date;
            var mostRecent = uniqueDays[0];

            // 最近一天不是昨天或今天，说明已断
            if ((today - mostRecent).Days > 1)
                return 0;

            int streak = 1;
            for (int i = 1; i < uniqueDays.Count; i++)
            {
                if ((uniqueDays[i - 1] - uniqueDays[i]).Days == 1)
                    streak++;
                else
                    break;
            }

            return streak;
        }
    }
}

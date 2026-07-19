using System.Collections.Generic;
using System.Linq;

namespace RandomPlaylistMod.Models
{
    /// <summary>
    /// 单次会话的运动数据汇总
    /// </summary>
    public class ExerciseSummary
    {
        /// <summary>本次会话总得分</summary>
        public int TotalScore { get; set; }

        /// <summary>平均精度（%）</summary>
        public float AverageAccuracy { get; set; }

        /// <summary>总击中音符数（估算）</summary>
        public int TotalNotesHit { get; set; }

        /// <summary>总失误数</summary>
        public int TotalNotesMissed { get; set; }

        /// <summary>总连击数（所有歌曲 MaxCombo 累加）</summary>
        public int TotalCombo { get; set; }

        /// <summary>全连歌曲数量</summary>
        public int FullComboCount { get; set; }

        /// <summary>最佳评级</summary>
        public string BestRank { get; set; } = "";

        /// <summary>最高 NPS 歌曲的 NPS 值</summary>
        public float HighestNPS { get; set; }

        /// <summary>最高 NPS 歌曲名</summary>
        public string HighestNPSSong { get; set; } = "";

        /// <summary>总切击次数（badCuts 累加可用作估算）</summary>
        public int TotalBadCuts { get; set; }

        /// <summary>实际活动时间（秒，歌曲时长累加，近似）</summary>
        public int ActiveSeconds { get; set; }

        /// <summary>
        /// 从 SongResult 列表计算汇总数据
        /// </summary>
        public static ExerciseSummary FromSongResults(List<SongResult> results)
        {
            if (results == null || results.Count == 0)
                return new ExerciseSummary();

            var validResults = results.Where(r => !r.Failed).ToList();
            var allResults = validResults.Count > 0 ? validResults : results;

            var summary = new ExerciseSummary
            {
                TotalScore = allResults.Sum(r => r.Score),
                AverageAccuracy = allResults.Any()
                    ? (float)System.Math.Round(allResults.Average(r => r.Accuracy), 2)
                    : 0f,
                TotalNotesMissed = allResults.Sum(r => r.MissedNotes),
                TotalCombo = allResults.Sum(r => r.MaxCombo),
                FullComboCount = allResults.Count(r => r.FullCombo),
                TotalBadCuts = allResults.Sum(r => r.BadCuts),
                ActiveSeconds = allResults.Sum(r => r.SongDuration)
            };

            // 估算击中音符数：maxCombo + missedNotes
            summary.TotalNotesHit = allResults.Sum(r => r.MaxCombo + r.MissedNotes);

            // 最佳评级
            var rankOrder = new[] { "SS", "S", "A", "B", "C", "D", "E" };
            string bestRank = "E";
            foreach (var r in allResults)
            {
                var rank = r.Rank?.Trim().ToUpper() ?? "";
                foreach (var candidate in rankOrder)
                {
                    if (rank == candidate && System.Array.IndexOf(rankOrder, candidate) < System.Array.IndexOf(rankOrder, bestRank))
                    {
                        bestRank = candidate;
                        break;
                    }
                }
            }
            summary.BestRank = bestRank;

            // 最高 NPS
            var highestNPSResult = allResults
                .Where(r => r.NPS > 0)
                .OrderByDescending(r => r.NPS)
                .FirstOrDefault();
            if (highestNPSResult != null)
            {
                summary.HighestNPS = (float)System.Math.Round(highestNPSResult.NPS, 1);
                summary.HighestNPSSong = highestNPSResult.SongName;
            }

            return summary;
        }
    }
}

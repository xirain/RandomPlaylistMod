using System;

namespace RandomPlaylistMod.Models
{
    /// <summary>
    /// 单首歌曲的游戏结果，数据来源于 LevelCompletionResults
    /// </summary>
    public class SongResult
    {
        /// <summary>歌曲名称</summary>
        public string SongName { get; set; } = "";

        /// <summary>作者/艺术家</summary>
        public string Author { get; set; } = "";

        /// <summary>关卡 ID</summary>
        public string LevelId { get; set; } = "";

        /// <summary>难度名称（Expert+/Expert/Hard/Normal/Easy）</summary>
        public string Difficulty { get; set; } = "";

        /// <summary>总得分</summary>
        public int Score { get; set; }

        /// <summary>理论最高分</summary>
        public int MaxScore { get; set; }

        /// <summary>评级（SS/S/A/B/C/D/E）</summary>
        public string Rank { get; set; } = "";

        /// <summary>最大连击数</summary>
        public int MaxCombo { get; set; }

        /// <summary>失误次数</summary>
        public int MissedNotes { get; set; }

        /// <summary>差切次数</summary>
        public int BadCuts { get; set; }

        /// <summary>是否全连</summary>
        public bool FullCombo { get; set; }

        /// <summary>精度百分比（0-100）</summary>
        public float Accuracy { get; set; }

        /// <summary>所选难度的 NPS</summary>
        public float NPS { get; set; }

        /// <summary>歌曲时长（秒）</summary>
        public int SongDuration { get; set; }

        /// <summary>是否因中途退出导致数据不完整</summary>
        public bool Failed { get; set; }

        /// <summary>播放开始时间</summary>
        public DateTime PlayedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 从 LevelCompletionResults 创建 SongResult
        /// </summary>
        public static SongResult FromLevelCompletion(
            string songName,
            string author,
            string levelId,
            string difficulty,
            int songDuration,
            float nps,
            LevelCompletionResults results)
        {
            if (results == null)
            {
                return new SongResult
                {
                    SongName = songName ?? "",
                    Author = author ?? "",
                    LevelId = levelId ?? "",
                    Difficulty = difficulty ?? "",
                    SongDuration = songDuration,
                    NPS = nps,
                    Failed = true,
                    PlayedAt = DateTime.Now
                };
            }

            float accuracy = 0f;
            // BS 1.40.8: modifiedScore / multipliedScore (无 maxModifiedScore)
            try
            {
                var prop = results.GetType().GetProperty("multipliedScore");
                if (prop != null)
                {
                    var val = prop.GetValue(results);
                    if (val is int multiScore && multiScore > 0)
                    {
                        accuracy = (float)results.modifiedScore / multiScore * 100f;
                    }
                }
            }
            catch { }

            return new SongResult
            {
                SongName = songName ?? "",
                Author = author ?? "",
                LevelId = levelId ?? "",
                Difficulty = difficulty ?? "",
                Score = results.modifiedScore,
                MaxScore = GetPropertySafe<int>(results, "multipliedScore"),
                Rank = results.rank.ToString(),
                MaxCombo = results.maxCombo,
                // BS 1.40.8 实际字段名是 "missedCount"（不是 "missedNotes"）
                MissedNotes = GetPropertySafe<int>(results, "missedCount")
                            + GetPropertySafe<int>(results, "notGoodCount"),
                // BS 1.40.8 实际字段名是 "badCuts"（不是 "badCutCount"）
                BadCuts = GetPropertySafe<int>(results, "badCuts"),
                FullCombo = results.fullCombo,
                Accuracy = (float)Math.Round(accuracy, 2),
                NPS = nps,
                SongDuration = songDuration,
                Failed = false,
                PlayedAt = DateTime.Now
            };
        }

        /// <summary>
        /// 创建失败歌曲的部分记录
        /// </summary>
        public static SongResult CreateFailed(
            string songName,
            string author,
            string levelId,
            string difficulty,
            int songDuration,
            float nps)
        {
            return new SongResult
            {
                SongName = songName ?? "",
                Author = author ?? "",
                LevelId = levelId ?? "",
                Difficulty = difficulty ?? "",
                SongDuration = songDuration,
                NPS = nps,
                Failed = true,
                PlayedAt = DateTime.Now
            };
        }

        /// <summary>
        /// 安全地从对象获取属性值（兼容不同 Beat Saber 版本的属性名）
        /// </summary>
        private static T GetPropertySafe<T>(object obj, string propertyName) where T : struct
        {
            try
            {
                var prop = obj.GetType().GetProperty(propertyName);
                if (prop != null)
                {
                    var value = prop.GetValue(obj);
                    if (value is T typed)
                        return typed;
                }
            }
            catch { }
            return default;
        }
    }
}

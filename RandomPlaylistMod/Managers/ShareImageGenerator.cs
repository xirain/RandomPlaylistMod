using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RandomPlaylistMod.Models;

namespace RandomPlaylistMod.Managers
{
    /// <summary>
    /// 生成分享 HTML 文件和图片（Phase 2：分享功能）
    /// </summary>
    public class ShareImageGenerator
    {
        private readonly HistoryManager _historyManager;

        public ShareImageGenerator(HistoryManager historyManager)
        {
            _historyManager = historyManager;
        }

        /// <summary>
        /// 根据 SessionRecord 生成分享 HTML 文件，并返回文件路径
        /// </summary>
        public string GenerateShareHtml(SessionRecord record)
        {
            if (record == null) return null;

            try
            {
                var html = BuildShareHtml(record);
                var filePath = _historyManager.GetShareHtmlPath(record.SessionId);

                var dir = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(filePath, html, Encoding.UTF8);
                Plugin.Log.Info($"ShareImageGenerator: Share HTML saved to {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"ShareImageGenerator: Failed to generate share HTML: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 构建分享 HTML 内容（使用模板替换避免 C# 插值转义问题）
        /// </summary>
        private string BuildShareHtml(SessionRecord record)
        {
            var exercise = record.ExerciseSummary;
            var songs = record.SongResults ?? new List<SongResult>();
            var validSongs = songs.Where(s => !s.Failed).ToList();

            var topSong = validSongs.OrderByDescending(s => s.Score).FirstOrDefault();
            var fcSongs = validSongs.Where(s => s.FullCombo).ToList();

            var songListHtml = BuildSongListHtml(validSongs);
            var fcHtml = BuildFcHtml(fcSongs);
            var durationExtras = BuildDurationExtras(record);
            var topSongHtml = BuildTopSongHtml(topSong);
            var playlistsStr = string.Join(", ", (record.PlaylistNames ?? new List<string>()).Where(n => !string.IsNullOrEmpty(n)).Take(3));
            var highestNpsStr = exercise.HighestNPS > 0 ? exercise.HighestNPS.ToString("F1") : "-";

            var template = GetHtmlTemplate();

            return template
                .Replace("{{START_DATE_FULL}}", record.StartedAt.ToString("yyyy年MM月dd日 HH:mm"))
                .Replace("{{END_TIME}}", record.EndedAt.ToString("HH:mm"))
                .Replace("{{DURATION_MIN}}", record.ActualDurationMin.ToString())
                .Replace("{{SONGS_PLAYED}}", record.TotalSongsPlayed.ToString())
                .Replace("{{TOTAL_SCORE}}", exercise.TotalScore.ToString("N0"))
                .Replace("{{FC_COUNT}}", exercise.FullComboCount.ToString())
                .Replace("{{ACCURACY}}", exercise.AverageAccuracy.ToString("F1"))
                .Replace("{{BEST_RANK}}", exercise.BestRank)
                .Replace("{{MISSED_NOTES}}", exercise.TotalNotesMissed.ToString())
                .Replace("{{TOTAL_COMBO}}", exercise.TotalCombo.ToString())
                .Replace("{{BAD_CUTS}}", exercise.TotalBadCuts.ToString())
                .Replace("{{HIGHEST_NPS}}", highestNpsStr)
                .Replace("{{ACTIVE_MIN}}", (exercise.ActiveSeconds / 60).ToString())
                .Replace("{{ACTIVE_SEC}}", (exercise.ActiveSeconds % 60).ToString())
                .Replace("{{PLAYLISTS}}", EscapeHtml(playlistsStr))
                .Replace("{{TARGET_DURATION}}", record.TargetDurationMin.ToString())
                .Replace("{{QUEUE_SONGS}}", record.TotalSongsInQueue.ToString())
                .Replace("{{NO_FAIL}}", (record.Settings?.NoFailEnabled == true ? "开启" : "关闭"))
                .Replace("{{NPS_RANGE}}", durationExtras)
                .Replace("{{TOP_SONG_HTML}}", topSongHtml)
                .Replace("{{FC_HTML}}", fcHtml)
                .Replace("{{SONG_LIST_HTML}}", songListHtml)
                .Replace("{{MOD_VERSION}}", record.ModVersion ?? "2.0.0");
        }

        private string GetHtmlTemplate()
        {
            return @"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>RandomPlaylistMod - Session Summary</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: 'Segoe UI', 'Microsoft YaHei', 'PingFang SC', sans-serif;
            background: linear-gradient(180deg, #667eea 0%, #764ba2 100%);
            color: #fff;
            width: 1080px;
            min-height: 1440px;
            padding: 40px 50px;
        }
        .header {
            text-align: center;
            padding: 30px 20px;
            margin-bottom: 30px;
            background: rgba(255,255,255,0.15);
            border-radius: 20px;
            border: 2px solid rgba(255,255,255,0.3);
        }
        .header h1 { font-size: 48px; font-weight: 900; margin-bottom: 10px; }
        .header .subtitle { font-size: 22px; opacity: 0.9; }
        .header .date { font-size: 20px; opacity: 0.7; margin-top: 8px; }

        .stats-grid {
            display: grid;
            grid-template-columns: 1fr 1fr 1fr 1fr;
            gap: 16px;
            margin-bottom: 25px;
        }
        .stat-card {
            background: rgba(255,255,255,0.12);
            border: 2px solid rgba(255,255,255,0.25);
            border-radius: 16px;
            padding: 20px;
            text-align: center;
        }
        .stat-card .value { font-size: 40px; font-weight: 900; color: #ffd700; }
        .stat-card .label { font-size: 16px; opacity: 0.8; margin-top: 4px; }

        .detail-section {
            background: rgba(255,255,255,0.12);
            border: 2px solid rgba(255,255,255,0.25);
            border-radius: 16px;
            padding: 24px;
            margin-bottom: 16px;
        }
        .detail-section h3 {
            font-size: 24px;
            color: #ffd700;
            margin-bottom: 12px;
            border-bottom: 1px solid rgba(255,255,255,0.2);
            padding-bottom: 8px;
        }
        .detail-grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 8px 20px;
            font-size: 18px;
        }
        .detail-grid .item { display: flex; justify-content: space-between; padding: 4px 0; }
        .detail-grid .item .key { opacity: 0.7; }
        .detail-grid .item .val { font-weight: 700; }

        .song-list {
            background: rgba(255,255,255,0.12);
            border: 2px solid rgba(255,255,255,0.25);
            border-radius: 16px;
            padding: 24px;
            margin-bottom: 16px;
        }
        .song-list h3 { font-size: 24px; color: #ffd700; margin-bottom: 12px; }
        .song-item {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 8px 12px;
            border-bottom: 1px solid rgba(255,255,255,0.1);
            font-size: 16px;
        }
        .song-item:last-child { border-bottom: none; }
        .song-rank { font-weight: 700; width: 40px; }
        .song-rank.SS, .song-rank.S { color: #ffd700; }
        .song-rank.A { color: #3fb950; }
        .song-name { flex: 1; }
        .song-score { font-weight: 700; color: #ffd700; }
        .song-badge {
            display: inline-block;
            padding: 1px 6px;
            border-radius: 4px;
            font-size: 11px;
            font-weight: 700;
            margin-left: 6px;
        }
        .song-badge.fc { background: #3fb950; color: #fff; }

        .footer {
            text-align: center;
            padding: 20px;
            margin-top: 20px;
            font-size: 14px;
            opacity: 0.6;
        }
        .footer a { color: #ffd700; }
    </style>
</head>
<body>
    <div class=""header"">
        <h1>RandomPlaylistMod</h1>
        <div class=""subtitle"">Session Summary</div>
        <div class=""date"">{{START_DATE_FULL}} - {{END_TIME}}</div>
    </div>

    <div class=""stats-grid"">
        <div class=""stat-card"">
            <div class=""value"">{{DURATION_MIN}}</div>
            <div class=""label"">Duration (min)</div>
        </div>
        <div class=""stat-card"">
            <div class=""value"">{{SONGS_PLAYED}}</div>
            <div class=""label"">Songs Played</div>
        </div>
        <div class=""stat-card"">
            <div class=""value"">{{TOTAL_SCORE}}</div>
            <div class=""label"">Total Score</div>
        </div>
        <div class=""stat-card"">
            <div class=""value"">{{FC_COUNT}}</div>
            <div class=""label"">Full Combos</div>
        </div>
    </div>

    <div class=""detail-section"">
        <h3>Exercise Data</h3>
        <div class=""detail-grid"">
            <div class=""item""><span class=""key"">Accuracy</span><span class=""val"">{{ACCURACY}}%</span></div>
            <div class=""item""><span class=""key"">Best Rank</span><span class=""val"">{{BEST_RANK}}</span></div>
            <div class=""item""><span class=""key"">Missed</span><span class=""val"">{{MISSED_NOTES}}</span></div>
            <div class=""item""><span class=""key"">Max Combo</span><span class=""val"">{{TOTAL_COMBO}}</span></div>
            <div class=""item""><span class=""key"">Bad Cuts</span><span class=""val"">{{BAD_CUTS}}</span></div>
            <div class=""item""><span class=""key"">Highest NPS</span><span class=""val"">{{HIGHEST_NPS}}</span></div>
            <div class=""item""><span class=""key"">Active Time</span><span class=""val"">{{ACTIVE_MIN}}m {{ACTIVE_SEC}}s</span></div>
        </div>
    </div>

    <div class=""detail-section"">
        <h3>Session Settings</h3>
        <div class=""detail-grid"">
            <div class=""item""><span class=""key"">Playlists</span><span class=""val"">{{PLAYLISTS}}</span></div>
            <div class=""item""><span class=""key"">Target</span><span class=""val"">{{TARGET_DURATION}} min</span></div>
            <div class=""item""><span class=""key"">Queue</span><span class=""val"">{{QUEUE_SONGS}} songs</span></div>
            <div class=""item""><span class=""key"">No Fail</span><span class=""val"">{{NO_FAIL}}</span></div>
            {{NPS_RANGE}}
        </div>
    </div>

    {{TOP_SONG_HTML}}

    <div class=""detail-section"">
        <h3>Full Combo Songs</h3>
        {{FC_HTML}}
    </div>

    <div class=""song-list"">
        <h3>Complete Song List ({{SONGS_PLAYED}} songs)</h3>
        {{SONG_LIST_HTML}}
    </div>

    <div class=""footer"">
        <p>Generated by RandomPlaylistMod v{{MOD_VERSION}}</p>
        <p>Download: <a href=""https://github.com/xirain/RandomPlaylistMod"">github.com/xirain/RandomPlaylistMod</a></p>
    </div>
</body>
</html>";
        }

        private string BuildDurationExtras(SessionRecord record)
        {
            if (record.Settings != null)
            {
                string npsText;
                if (record.Settings.NpsAny)
                    npsText = "Any";
                else if (record.Settings.NpsBandLabels != null && record.Settings.NpsBandLabels.Count > 0)
                    npsText = string.Join(" / ", record.Settings.NpsBandLabels);
                else if (record.Settings.MinNPS > 0 || record.Settings.MaxNPS < 99)
                    npsText = $"{record.Settings.MinNPS:F1} - {record.Settings.MaxNPS:F1}";
                else
                    npsText = "Any";

                return string.Format(@"<div class=""item""><span class=""key"">NPS</span><span class=""val"">{0}</span></div>", npsText);
            }
            return "";
        }

        private string BuildTopSongHtml(SongResult topSong)
        {
            if (topSong == null) return "";

            return string.Format(@"
    <div class=""detail-section"">
        <h3>Top Score Song</h3>
        <div class=""detail-grid"">
            <div class=""item""><span class=""key"">Song</span><span class=""val"">{0} [{1}]</span></div>
            <div class=""item""><span class=""key"">Score</span><span class=""val"">{2:N0}</span></div>
            <div class=""item""><span class=""key"">Rank</span><span class=""val"">{3}</span></div>
            <div class=""item""><span class=""key"">Accuracy</span><span class=""val"">{4:F1}%</span></div>
        </div>
    </div>",
                EscapeHtml(topSong.SongName), topSong.Difficulty,
                topSong.Score, topSong.Rank, topSong.Accuracy);
        }

        private string BuildFcHtml(List<SongResult> fcSongs)
        {
            if (fcSongs == null || fcSongs.Count == 0)
                return "<p>No full combo songs this session</p>";

            var items = new StringBuilder();
            foreach (var s in fcSongs)
            {
                items.AppendFormat("<li>{0} [{1}] - FC!</li>",
                    EscapeHtml(s.SongName), s.Difficulty);
            }

            return string.Format("<p><strong>{0} FC songs:</strong></p><ul>{1}</ul>",
                fcSongs.Count, items.ToString());
        }

        /// <summary>
        /// 构建歌曲列表的 HTML 片段
        /// </summary>
        private string BuildSongListHtml(List<SongResult> songs)
        {
            if (songs == null || songs.Count == 0) return "";

            var sb = new StringBuilder();
            foreach (var s in songs)
            {
                var rankClass = "song-rank";
                var rank = s.Rank?.Trim().ToUpper() ?? "";
                if (rank == "SS" || rank == "S") rankClass = "song-rank SS";
                else if (rank == "A") rankClass = "song-rank A";

                var fcBadge = s.FullCombo ? @"<span class=""song-badge fc"">FC</span>" : "";

                sb.AppendFormat(@"
            <div class=""song-item"">
                <span class=""{0}"">{1}</span>
                <span class=""song-name"">{2} <small> [{3}]</small>{4}</span>
                <span class=""song-score"">{5:N0}</span>
            </div>",
                    rankClass, rank, EscapeHtml(s.SongName),
                    s.Difficulty, fcBadge, s.Score);
            }

            return sb.ToString();
        }

        private string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }
    }
}

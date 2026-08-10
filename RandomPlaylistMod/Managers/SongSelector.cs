
using System;
using System.Collections.Generic;
using System.Linq;
using RandomPlaylistMod.Models;

namespace RandomPlaylistMod.Managers
{
    public class SongSelector
    {
        private readonly Random _random = new Random();

        /// <summary>
        /// 按关卡频段（多选并集）筛选歌曲。any=true 或 NPS 未知时不做筛选。
        /// </summary>
        public List<SongInfo> SelectSongsForDuration(List<SongInfo> songs, int targetMinutes, List<(float min, float max)> bands, bool any)
        {
            if (songs == null || songs.Count == 0)
                return new List<SongInfo>();

            // NPS 过滤：NPS=-1(未知/未缓存)始终通过；已知 NPS 需落在任一所选频段内
            var filtered = songs.Where(s => LevelBand.InBands(s.NPS, bands, any)).ToList();
            if (filtered.Count == 0)
            {
                var desc = any ? "Any" : string.Join("/", bands.Select(b => $"{b.min:F0}-{b.max:F0}"));
                Plugin.Log.Warn($"SongSelector: No songs in NPS bands [{desc}], using all songs");
                filtered = new List<SongInfo>(songs);
            }

            int targetSeconds = targetMinutes * 60;
            List<SongInfo> shuffled = ShuffleSongs(filtered);
            List<SongInfo> selected = new List<SongInfo>();
            int currentDuration = 0;
            string lastAuthor = string.Empty;

            // 第一轮：尽可能凑满目标时长（允许超过）
            foreach (var song in shuffled)
            {
                // 避免连续同一作者
                if (song.Author == lastAuthor && selected.Count > 0)
                    continue;

                selected.Add(song);
                currentDuration += song.Duration;
                lastAuthor = song.Author;

                // 已经凑够或超过目标时长，停止第一轮
                if (currentDuration >= targetSeconds)
                    break;
            }

            // 第二轮：如果还有未选的歌，继续添加（让队列更长，由计时器控制结束）
            // 这样即使用户设置的时长超过歌曲总时长，也能把所有歌唱完
            var remaining = shuffled.Except(selected).ToList();
            foreach (var song in remaining)
            {
                if (song.Author == lastAuthor && selected.Count > 0)
                    continue;

                selected.Add(song);
                currentDuration += song.Duration;
                lastAuthor = song.Author;
            }

            Plugin.Log.Info($"SongSelector: Selected {selected.Count} songs, total duration {currentDuration / 60f:F1} min (target: {targetMinutes} min)");
            return selected;
        }

        /// <summary>
        /// 向后兼容：仅按时长筛选，不做 NPS 过滤（any=true）。
        /// </summary>
        public List<SongInfo> SelectSongsForDuration(List<SongInfo> songs, int targetMinutes)
        {
            return SelectSongsForDuration(songs, targetMinutes, new List<(float, float)>(), any: true);
        }

        /// <summary>
        /// 向后兼容：单区间筛选（any=false 时使用 [minNPS, maxNPS] 这一个频段）。
        /// </summary>
        public List<SongInfo> SelectSongsForDuration(List<SongInfo> songs, int targetMinutes, float minNPS = 0f, float maxNPS = 99f)
        {
            var bands = (minNPS <= 0f && maxNPS >= 99f)
                ? new List<(float, float)>()
                : new List<(float, float)> { (minNPS, maxNPS) };
            bool any = (minNPS <= 0f && maxNPS >= 99f);
            return SelectSongsForDuration(songs, targetMinutes, bands, any);
        }

        public List<SongInfo> ShuffleSongs(List<SongInfo> songs)
        {
            List<SongInfo> shuffled = new List<SongInfo>(songs);
            int n = shuffled.Count;
            
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                SongInfo value = shuffled[k];
                shuffled[k] = shuffled[n];
                shuffled[n] = value;
            }
            
            return shuffled;
        }

        public SongInfo FindBestFitSong(List<SongInfo> songs, int remainingSeconds)
        {
            SongInfo bestFit = null;
            int minDifference = int.MaxValue;

            foreach (var song in songs)
            {
                int difference = Math.Abs(song.Duration - remainingSeconds);
                if (difference < minDifference && song.Duration <= remainingSeconds)
                {
                    minDifference = difference;
                    bestFit = song;
                }
            }

            return bestFit;
        }

        public int CalculateEstimatedSongCount(List<SongInfo> songs, int targetMinutes)
        {
            if (songs == null || songs.Count == 0)
                return 0;

            int targetSeconds = targetMinutes * 60;
            double avgDuration = songs.Average(s => s.Duration);
            
            if (avgDuration <= 0)
                return 0;

            return (int)(targetSeconds / avgDuration);
        }

        public int CalculateTotalDuration(List<SongInfo> songs)
        {
            return songs?.Sum(s => s.Duration) ?? 0;
        }

        public List<SongInfo> SelectRandomSongs(List<SongInfo> songs, int count)
        {
            if (songs == null || songs.Count == 0 || count <= 0)
                return new List<SongInfo>();

            List<SongInfo> shuffled = ShuffleSongs(songs);
            return shuffled.Take(Math.Min(count, shuffled.Count)).ToList();
        }
    }
}

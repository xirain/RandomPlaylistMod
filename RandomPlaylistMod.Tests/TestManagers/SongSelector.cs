
using RandomPlaylistMod.Tests.TestModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RandomPlaylistMod.Tests.TestManagers
{
    public class SongSelector
    {
        private readonly Random _random = new Random();

        public Queue<SongInfo> GenerateSongQueue(List<PlaylistInfo> selectedPlaylists, int targetDurationMinutes)
        {
            if (selectedPlaylists == null || !selectedPlaylists.Any())
                return new Queue<SongInfo>();

            var allSongs = selectedPlaylists.SelectMany(p => p.Songs).ToList();
            
            if (!allSongs.Any())
                return new Queue<SongInfo>();

            ShuffleSongs(allSongs);

            int targetSeconds = targetDurationMinutes * 60;
            double avgDuration = allSongs.Average(s => s.Duration);
            int estimatedCount = Math.Max(1, (int)(targetSeconds / avgDuration));

            var queue = new List<SongInfo>();
            int currentTotal = 0;
            string lastAuthor = "";

            foreach (var song in allSongs)
            {
                if (song.Author == lastAuthor)
                    continue;

                if (currentTotal + song.Duration > targetSeconds + 120)
                    continue;

                queue.Add(song);
                currentTotal += song.Duration;
                lastAuthor = song.Author;

                if (currentTotal >= targetSeconds || queue.Count >= estimatedCount + 2)
                    break;
            }

            if (!queue.Any() && allSongs.Any())
            {
                queue.Add(allSongs[0]);
            }

            return new Queue<SongInfo>(queue);
        }

        public SongInfo SelectNextSong(Queue<SongInfo> songQueue, int remainingTimeSeconds, List<SongInfo> playedSongs = null)
        {
            if (songQueue == null || songQueue.Count == 0)
                return null;

            playedSongs = playedSongs ?? new List<SongInfo>();

            if (remainingTimeSeconds <= 0)
                return null;

            var availableSongs = songQueue.Where(s => !playedSongs.Contains(s)).ToList();

            if (!availableSongs.Any())
                return null;

            if (remainingTimeSeconds <= 120)
            {
                return FindBestFitSong(availableSongs, remainingTimeSeconds);
            }

            var validSongs = availableSongs.Where(s => s.Duration <= remainingTimeSeconds + 60).ToList();
            
            if (!validSongs.Any())
                validSongs = availableSongs.ToList();

            return validSongs[_random.Next(validSongs.Count)];
        }

        private SongInfo FindBestFitSong(List<SongInfo> songs, int targetDurationSeconds)
        {
            if (!songs.Any())
                return null;

            SongInfo bestFit = null;
            int minDifference = int.MaxValue;

            foreach (var song in songs)
            {
                int difference = Math.Abs(song.Duration - targetDurationSeconds);
                if (difference < minDifference)
                {
                    minDifference = difference;
                    bestFit = song;
                }
            }

            return bestFit;
        }

        public void ShuffleSongs<T>(List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public int CalculateEstimatedSongCount(List<SongInfo> songs, int targetDurationMinutes)
        {
            if (!songs.Any())
                return 0;

            double avgDuration = songs.Average(s => s.Duration);
            return (int)Math.Ceiling((targetDurationMinutes * 60) / avgDuration);
        }

        public int CalculateTotalDuration(List<SongInfo> songs)
        {
            return songs.Sum(s => s.Duration);
        }

        // 与正式 SongSelector 对齐的签名（测试用 SongInfo 无 NPS，按不过滤处理）
        public List<SongInfo> SelectSongsForDuration(List<SongInfo> songs, int targetMinutes)
        {
            return SelectSongsInternal(songs, targetMinutes);
        }

        public List<SongInfo> SelectSongsForDuration(List<SongInfo> songs, int targetMinutes, float minNPS = 0f, float maxNPS = 99f)
        {
            return SelectSongsInternal(songs, targetMinutes);
        }

        public List<SongInfo> SelectSongsForDuration(List<SongInfo> songs, int targetMinutes, List<(float min, float max)> bands, bool any)
        {
            return SelectSongsInternal(songs, targetMinutes);
        }

        /// <summary>
        /// 按目标时长挑选歌曲：不足目标时长就全部选中（不截断），用队列形式返回。
        /// 与正式实现语义一致：返回选中列表而非 Queue。
        /// </summary>
        private List<SongInfo> SelectSongsInternal(List<SongInfo> songs, int targetMinutes)
        {
            if (songs == null || songs.Count == 0)
                return new List<SongInfo>();

            var filtered = new List<SongInfo>(songs);
            if (filtered.Count == 0)
                return new List<SongInfo>(songs);

            int targetSeconds = targetMinutes * 60;
            int currentDuration = 0;

            // 不足目标时长则全选；超过则按作者去重后尽量填满（与正式实现保持近似行为）
            var selected = new List<SongInfo>();
            string lastAuthor = "";
            foreach (var song in filtered)
            {
                if (song.Author == lastAuthor)
                    continue;
                selected.Add(song);
                currentDuration += song.Duration;
                lastAuthor = song.Author;
            }

            // 若作者去重后为空（无作者信息），直接全选
            if (selected.Count == 0)
                selected = new List<SongInfo>(filtered);

            return selected;
        }
    }
}

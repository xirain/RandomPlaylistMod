
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
    }
}

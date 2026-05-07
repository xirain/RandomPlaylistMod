
using System;
using System.Collections.Generic;
using System.Linq;
using RandomPlaylistMod.Models;

namespace RandomPlaylistMod.Managers
{
    public class SongSelector
    {
        private readonly Random _random = new Random();

        public List<SongInfo> SelectSongsForDuration(List<SongInfo> songs, int targetMinutes)
        {
            if (songs == null || songs.Count == 0)
                return new List<SongInfo>();

            int targetSeconds = targetMinutes * 60;
            List<SongInfo> shuffled = ShuffleSongs(songs);
            List<SongInfo> selected = new List<SongInfo>();
            int currentDuration = 0;
            string lastAuthor = string.Empty;

            foreach (var song in shuffled)
            {
                if (currentDuration + song.Duration <= targetSeconds)
                {
                    if (song.Author != lastAuthor || selected.Count == 0)
                    {
                        selected.Add(song);
                        currentDuration += song.Duration;
                        lastAuthor = song.Author;
                    }
                }
                else
                {
                    var bestFit = FindBestFitSong(shuffled.Except(selected).ToList(), targetSeconds - currentDuration);
                    if (bestFit != null)
                    {
                        selected.Add(bestFit);
                        break;
                    }
                }
            }

            return selected;
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

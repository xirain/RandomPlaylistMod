
using NUnit.Framework;
using RandomPlaylistMod.Tests.TestManagers;
using RandomPlaylistMod.Tests.TestModels;
using System.Collections.Generic;
using System.Linq;

namespace RandomPlaylistMod.Tests
{
    [TestFixture]
    public class SongSelectorTests
    {
        private SongSelector _songSelector;

        [SetUp]
        public void Setup()
        {
            _songSelector = new SongSelector();
        }

        [Test]
        public void GenerateSongQueue_EmptyPlaylists_ReturnsEmptyQueue()
        {
            var result = _songSelector.GenerateSongQueue(new List<PlaylistInfo>(), 30);
            Assert.IsEmpty(result);
        }

        [Test]
        public void GenerateSongQueue_PlaylistsWithNoSongs_ReturnsEmptyQueue()
        {
            var playlists = new List<PlaylistInfo>
            {
                new PlaylistInfo { Name = "Test", Id = "test", Songs = new List<SongInfo>() }
            };

            var result = _songSelector.GenerateSongQueue(playlists, 30);
            Assert.IsEmpty(result);
        }

        [Test]
        public void GenerateSongQueue_ValidPlaylists_ReturnsQueue()
        {
            var songs = new List<SongInfo>
            {
                new SongInfo { SongName = "Song1", Author = "Artist1", Duration = 180, Key = "1" },
                new SongInfo { SongName = "Song2", Author = "Artist2", Duration = 240, Key = "2" },
                new SongInfo { SongName = "Song3", Author = "Artist3", Duration = 300, Key = "3" }
            };

            var playlists = new List<PlaylistInfo>
            {
                new PlaylistInfo { Name = "Test", Id = "test", Songs = songs }
            };

            var result = _songSelector.GenerateSongQueue(playlists, 30);
            Assert.IsNotEmpty(result);
        }

        [Test]
        public void GenerateSongQueue_AvoidsConsecutiveSameAuthor()
        {
            var songs = new List<SongInfo>
            {
                new SongInfo { SongName = "Song1", Author = "Artist1", Duration = 180, Key = "1" },
                new SongInfo { SongName = "Song2", Author = "Artist1", Duration = 240, Key = "2" },
                new SongInfo { SongName = "Song3", Author = "Artist2", Duration = 300, Key = "3" },
                new SongInfo { SongName = "Song4", Author = "Artist2", Duration = 180, Key = "4" }
            };

            var playlists = new List<PlaylistInfo>
            {
                new PlaylistInfo { Name = "Test", Id = "test", Songs = songs }
            };

            var result = _songSelector.GenerateSongQueue(playlists, 60);
            var queueList = new List<SongInfo>(result);

            for (int i = 1; i < queueList.Count; i++)
            {
                Assert.AreNotEqual(queueList[i - 1].Author, queueList[i].Author,
                    "Consecutive songs should not have the same author");
            }
        }

        [Test]
        public void CalculateEstimatedSongCount_ValidSongs_ReturnsCorrectCount()
        {
            var songs = new List<SongInfo>
            {
                new SongInfo { Duration = 180 },
                new SongInfo { Duration = 240 },
                new SongInfo { Duration = 300 }
            };

            int result = _songSelector.CalculateEstimatedSongCount(songs, 30);
            Assert.AreEqual(8, result);
        }

        [Test]
        public void CalculateTotalDuration_ValidSongs_ReturnsSum()
        {
            var songs = new List<SongInfo>
            {
                new SongInfo { Duration = 180 },
                new SongInfo { Duration = 240 },
                new SongInfo { Duration = 300 }
            };

            int result = _songSelector.CalculateTotalDuration(songs);
            Assert.AreEqual(720, result);
        }

        [Test]
        public void ShuffleSongs_ListIsShuffled()
        {
            var songs = new List<SongInfo>();
            for (int i = 0; i < 10; i++)
            {
                songs.Add(new SongInfo { Key = i.ToString() });
            }

            var original = new List<SongInfo>(songs);
            _songSelector.ShuffleSongs(songs);

            bool isDifferent = false;
            for (int i = 0; i < songs.Count; i++)
            {
                if (songs[i].Key != original[i].Key)
                {
                    isDifferent = true;
                    break;
                }
            }

            Assert.IsTrue(isDifferent, "List should be shuffled");
        }

        [Test]
        public void SelectSongsForDuration_SelectsAllSongs_WhenSongsFitWithinTarget()
        {
            // 10 首歌 × 180s = 1800s = 30min，目标 120min
            // 修复后：应选中所有歌曲（不再在目标时长处截断）
            var songs = new List<SongInfo>();
            for (int i = 0; i < 10; i++)
            {
                songs.Add(new SongInfo { SongName = $"Song{i}", Author = $"Artist{i}", Duration = 180, Key = $"id{i}" });
            }

            var result = _songSelector.SelectSongsForDuration(songs, 120);

            Assert.AreEqual(10, result.Count, "应选中所有 10 首歌，而非在目标时长处截断");
        }

        [Test]
        public void SelectSongsForDuration_QueueTotalDurationExceedsTarget()
        {
            // 5 首歌 × 300s = 1500s = 25min，目标 10min
            // 修复后：队列总时长应超过目标时长
            var songs = new List<SongInfo>();
            for (int i = 0; i < 5; i++)
            {
                songs.Add(new SongInfo { SongName = $"Song{i}", Author = $"Artist{i}", Duration = 300, Key = $"id{i}" });
            }

            var result = _songSelector.SelectSongsForDuration(songs, 10);
            int totalDuration = result.Sum(s => s.Duration);

            Assert.IsTrue(totalDuration > 10 * 60, "队列总时长应超过目标时长（修复前会在目标处截断）");
            Assert.AreEqual(5, result.Count, "应选中所有 5 首歌");
        }
    }
}

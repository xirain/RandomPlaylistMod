
using NUnit.Framework;
using RandomPlaylistMod.Tests.TestManagers;
using RandomPlaylistMod.Tests.TestModels;
using System.Collections.Generic;

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
                new PlaylistInfo { Name = "Test", PlaylistId = "test", Songs = new List<SongInfo>() }
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
                new PlaylistInfo { Name = "Test", PlaylistId = "test", Songs = songs }
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
                new PlaylistInfo { Name = "Test", PlaylistId = "test", Songs = songs }
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
    }
}

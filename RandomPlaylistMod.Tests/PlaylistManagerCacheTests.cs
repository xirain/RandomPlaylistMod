using NUnit.Framework;
using RandomPlaylistMod.Tests.TestModels;
using System.Collections.Generic;
using System.Linq;

namespace RandomPlaylistMod.Tests
{
    /// <summary>
    /// 测试 PlaylistManager 的歌曲缓存和选择逻辑
    /// </summary>
    [TestFixture]
    public class PlaylistManagerCacheTests
    {
        private TestPlaylistManager _manager;

        [SetUp]
        public void Setup()
        {
            _manager = new TestPlaylistManager();
        }

        [Test]
        public void TogglePlaylistSelection_ChangesSelectedState()
        {
            _manager.LoadPlaylists(CreateTestPlaylistInfos(3));
            Assert.IsFalse(_manager.Playlists[0].Selected);

            _manager.TogglePlaylistSelection("playlist_0");
            Assert.IsTrue(_manager.Playlists[0].Selected);

            _manager.TogglePlaylistSelection("playlist_0");
            Assert.IsFalse(_manager.Playlists[0].Selected);
        }

        [Test]
        public void SelectAllPlaylists_SelectsAll()
        {
            _manager.LoadPlaylists(CreateTestPlaylistInfos(3));
            _manager.SelectAllPlaylists();

            Assert.IsTrue(_manager.Playlists.All(p => p.Selected));
        }

        [Test]
        public void DeselectAllPlaylists_DeselectsAll()
        {
            _manager.LoadPlaylists(CreateTestPlaylistInfos(3));
            _manager.SelectAllPlaylists();
            _manager.DeselectAllPlaylists();

            Assert.IsTrue(_manager.Playlists.All(p => !p.Selected));
        }

        [Test]
        public void GetSelectedPlaylists_ReturnsOnlySelected()
        {
            _manager.LoadPlaylists(CreateTestPlaylistInfos(3));
            _manager.TogglePlaylistSelection("playlist_0");
            _manager.TogglePlaylistSelection("playlist_2");

            var selected = _manager.GetSelectedPlaylists();
            Assert.AreEqual(2, selected.Count);
            Assert.AreEqual("playlist_0", selected[0].Id);
            Assert.AreEqual("playlist_2", selected[1].Id);
        }

        [Test]
        public void GetSongsFromSelectedPlaylists_NoSelection_ReturnsEmpty()
        {
            _manager.LoadPlaylists(CreateTestPlaylistInfos(3));
            var songs = _manager.GetSongsFromSelectedPlaylists();
            Assert.IsEmpty(songs);
        }

        [Test]
        public void GetSongsFromSelectedPlaylists_WithSelection_ReturnsSongs()
        {
            var playlists = CreateTestPlaylistInfos(2);
            playlists[0].PlayableSongCount = 5;
            playlists[1].PlayableSongCount = 3;
            _manager.LoadPlaylists(playlists);
            _manager.SetSongsForPlaylist("playlist_0", CreateTestSongs(5, "Playlist0"));
            _manager.SetSongsForPlaylist("playlist_1", CreateTestSongs(3, "Playlist1"));

            _manager.TogglePlaylistSelection("playlist_0");
            var songs = _manager.GetSongsFromSelectedPlaylists();
            Assert.AreEqual(5, songs.Count);
        }

        [Test]
        public void GetSongsFromSelectedPlaylists_DeduplicatesByLevelId()
        {
            var playlists = CreateTestPlaylistInfos(2);
            _manager.LoadPlaylists(playlists);

            // 两首歌有相同 LevelId
            var songs1 = new List<SongInfo>
            {
                new SongInfo { SongName = "SongA", LevelId = "level_1", PlaylistName = "Playlist0" },
                new SongInfo { SongName = "SongB", LevelId = "level_2", PlaylistName = "Playlist0" }
            };
            var songs2 = new List<SongInfo>
            {
                new SongInfo { SongName = "SongA_copy", LevelId = "level_1", PlaylistName = "Playlist1" },
                new SongInfo { SongName = "SongC", LevelId = "level_3", PlaylistName = "Playlist1" }
            };

            _manager.SetSongsForPlaylist("playlist_0", songs1);
            _manager.SetSongsForPlaylist("playlist_1", songs2);
            _manager.SelectAllPlaylists();

            var result = _manager.GetSongsFromSelectedPlaylists();
            Assert.AreEqual(3, result.Count, "Should deduplicate by LevelId");
        }

        [Test]
        public void TogglePlaylistSelection_InvalidatesCache()
        {
            var playlists = CreateTestPlaylistInfos(2);
            _manager.LoadPlaylists(playlists);
            _manager.SetSongsForPlaylist("playlist_0", CreateTestSongs(3, "P0"));
            _manager.SetSongsForPlaylist("playlist_1", CreateTestSongs(2, "P1"));

            // 选中 playlist_0
            _manager.TogglePlaylistSelection("playlist_0");
            var songs1 = _manager.GetSongsFromSelectedPlaylists();
            Assert.AreEqual(3, songs1.Count, "Should return 3 songs from playlist_0");

            // 再选中 playlist_1，缓存失效，应返回两列表合并（去重）
            _manager.TogglePlaylistSelection("playlist_1");
            var songs2 = _manager.GetSongsFromSelectedPlaylists();
            Assert.AreEqual(5, songs2.Count, "Cache invalidated, should return 5 songs from both playlists");
        }

        #region 测试辅助

        private List<PlaylistInfo> CreateTestPlaylistInfos(int count)
        {
            var result = new List<PlaylistInfo>();
            for (int i = 0; i < count; i++)
            {
                result.Add(new PlaylistInfo
                {
                    Id = $"playlist_{i}",
                    Name = $"Playlist {i}",
                    SongCount = 5,
                    PlayableSongCount = 5,
                    TotalDuration = 900,
                    Selected = false
                });
            }
            return result;
        }

        private List<SongInfo> CreateTestSongs(int count, string playlistName)
        {
            var songs = new List<SongInfo>();
            for (int i = 0; i < count; i++)
            {
                songs.Add(new SongInfo
                {
                    SongName = $"{playlistName}_Song{i}",
                    Author = $"Artist{i % 3}",
                    Duration = 180,
                    Key = $"{playlistName}_key{i}",
                    LevelId = $"level_{playlistName}_{i}",
                    PlaylistName = playlistName
                });
            }
            return songs;
        }

        #endregion
    }

    /// <summary>
    /// 简化版 PlaylistManager，模拟缓存逻辑
    /// </summary>
    internal class TestPlaylistManager
    {
        private readonly List<PlaylistInfo> _playlists = new List<PlaylistInfo>();
        private readonly Dictionary<string, List<SongInfo>> _playlistSongs = new Dictionary<string, List<SongInfo>>();
        private List<SongInfo> _songsCache;
        private bool _songsCacheDirty = true;

        public List<PlaylistInfo> Playlists => _playlists;

        public void LoadPlaylists(List<PlaylistInfo> playlists)
        {
            _playlists.Clear();
            _playlists.AddRange(playlists);
            InvalidateSongsCache();
        }

        public void SetSongsForPlaylist(string playlistId, List<SongInfo> songs)
        {
            _playlistSongs[playlistId] = songs;
            InvalidateSongsCache();
        }

        public void TogglePlaylistSelection(string playlistId)
        {
            var playlist = _playlists.FirstOrDefault(p => p.Id == playlistId);
            if (playlist != null)
            {
                playlist.Selected = !playlist.Selected;
                InvalidateSongsCache();
            }
        }

        public void SelectAllPlaylists()
        {
            foreach (var p in _playlists) p.Selected = true;
            InvalidateSongsCache();
        }

        public void DeselectAllPlaylists()
        {
            foreach (var p in _playlists) p.Selected = false;
            InvalidateSongsCache();
        }

        public List<PlaylistInfo> GetSelectedPlaylists()
        {
            return _playlists.Where(p => p.Selected).ToList();
        }

        public List<SongInfo> GetSongsFromSelectedPlaylists()
        {
            if (!_songsCacheDirty && _songsCache != null)
                return _songsCache;

            var songs = new List<SongInfo>();
            var seenLevelIds = new HashSet<string>();

            foreach (var selectedInfo in GetSelectedPlaylists())
            {
                if (!_playlistSongs.TryGetValue(selectedInfo.Id, out var playlistSongs))
                    continue;

                foreach (var song in playlistSongs)
                {
                    if (string.IsNullOrEmpty(song.LevelId)) continue;
                    if (seenLevelIds.Contains(song.LevelId)) continue;
                    seenLevelIds.Add(song.LevelId);
                    songs.Add(song);
                }
            }

            _songsCache = songs;
            _songsCacheDirty = false;
            return songs;
        }

        private void InvalidateSongsCache()
        {
            _songsCacheDirty = true;
            _songsCache = null;
        }
    }
}

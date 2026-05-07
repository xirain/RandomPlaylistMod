
using System.Collections.Generic;
using System.Linq;
using BeatSaberPlaylistsLib;
using BeatSaberPlaylistsLib.Types;
using RandomPlaylistMod.Models;
using SongCore;

namespace RandomPlaylistMod.Managers
{
    public class PlaylistManager
    {
        private readonly List<PlaylistInfo> _playlists = new List<PlaylistInfo>();

        public List<PlaylistInfo> Playlists => _playlists;

        public void LoadPlaylists()
        {
            _playlists.Clear();

            try
            {
                var defaultManager = BeatSaberPlaylistsLib.PlaylistManager.DefaultManager;
                if (defaultManager == null)
                {
                    Plugin.Log.Error("PlaylistManager: DefaultManager is null!");
                    return;
                }

                var allPlaylists = defaultManager.GetAllPlaylists(true);
                Plugin.Log.Info($"PlaylistManager: Found {allPlaylists.Length} playlists from BeatSaberPlaylistsLib");

                foreach (var playlist in allPlaylists)
                {
                    try
                    {
                        var playlistInfo = new PlaylistInfo
                        {
                            Id = playlist.Filename ?? playlist.Title ?? "unknown",
                            Name = playlist.Title ?? "Unnamed Playlist",
                            Author = playlist.Author ?? "",
                            Selected = false,
                            SongCount = playlist.Count
                        };

                        // 计算播放列表中歌曲信息
                        int totalDuration = 0;
                        int playableCount = 0;
                        foreach (IPlaylistSong song in playlist)
                        {
                            try
                            {
                                // 通过SongCore查找已安装的关卡
                                string levelId = song.LevelId;
                                if (string.IsNullOrEmpty(levelId) && !string.IsNullOrEmpty(song.Hash))
                                {
                                    levelId = $"custom_level_{song.Hash.ToUpper()}";
                                }

                                if (!string.IsNullOrEmpty(levelId))
                                {
                                    var level = Loader.GetLevelById(levelId);
                                    if (level != null)
                                    {
                                        totalDuration += (int)level.songDuration;
                                        playableCount++;
                                    }
                                }
                            }
                            catch (System.Exception ex)
                            {
                                Plugin.Log.Error($"PlaylistManager: Error checking song '{song.Name}': {ex.Message}");
                            }
                        }
                        playlistInfo.TotalDuration = totalDuration;
                        playlistInfo.PlayableSongCount = playableCount;

                        _playlists.Add(playlistInfo);
                    }
                    catch (System.Exception ex)
                    {
                        Plugin.Log.Error($"PlaylistManager: Error loading playlist '{playlist.Title}': {ex.Message}");
                    }
                }

                Plugin.Log.Info($"PlaylistManager: Loaded {_playlists.Count} playlists successfully");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.Error($"PlaylistManager: Failed to load playlists - {ex.Message}");
                Plugin.Log.Error($"Stack trace: {ex.StackTrace}");
            }
        }

        public void LoadPlaylistsAsync()
        {
            LoadPlaylists();
        }

        public void TogglePlaylistSelection(string playlistId)
        {
            var playlist = _playlists.FirstOrDefault(p => p.Id == playlistId);
            if (playlist != null)
            {
                playlist.Selected = !playlist.Selected;
                Plugin.Log.Info($"PlaylistManager: Toggled playlist '{playlist.Name}' to {playlist.Selected}");
            }
        }

        public void SelectAllPlaylists()
        {
            foreach (var playlist in _playlists)
            {
                playlist.Selected = true;
            }
        }

        public void DeselectAllPlaylists()
        {
            foreach (var playlist in _playlists)
            {
                playlist.Selected = false;
            }
        }

        public List<PlaylistInfo> GetSelectedPlaylists()
        {
            return _playlists.Where(p => p.Selected).ToList();
        }

        /// <summary>
        /// 获取选中播放列表中的所有歌曲（基于SongCore查找）
        /// </summary>
        public List<SongInfo> GetSongsFromSelectedPlaylists()
        {
            var songs = new List<SongInfo>();
            var selectedPlaylists = GetSelectedPlaylists();
            var seenLevelIds = new HashSet<string>();

            try
            {
                var defaultManager = BeatSaberPlaylistsLib.PlaylistManager.DefaultManager;
                var allPlaylists = defaultManager.GetAllPlaylists(true);

                foreach (var selectedInfo in selectedPlaylists)
                {
                    var playlist = allPlaylists.FirstOrDefault(p =>
                        (p.Filename ?? p.Title) == selectedInfo.Id);

                    if (playlist == null) continue;

                    foreach (IPlaylistSong song in playlist)
                    {
                        try
                        {
                            // 通过SongCore查找已安装的关卡
                            string levelId = song.LevelId;
                            if (string.IsNullOrEmpty(levelId) && !string.IsNullOrEmpty(song.Hash))
                            {
                                levelId = $"custom_level_{song.Hash.ToUpper()}";
                            }

                            if (string.IsNullOrEmpty(levelId)) continue;
                            if (seenLevelIds.Contains(levelId)) continue;
                            seenLevelIds.Add(levelId);

                            var level = Loader.GetLevelById(levelId);
                            if (level == null) continue;

                            songs.Add(new SongInfo
                            {
                                LevelId = levelId,
                                SongName = level.songName ?? song.Name ?? "Unknown",
                                Author = level.songAuthorName ?? "",
                                Duration = (int)level.songDuration,
                                Key = song.Key ?? "",
                                PlaylistName = selectedInfo.Name
                            });
                        }
                        catch (System.Exception ex)
                        {
                            Plugin.Log.Error($"PlaylistManager: Error processing song '{song.Name}': {ex.Message}");
                        }
                    }
                }

                Plugin.Log.Info($"PlaylistManager: Got {songs.Count} unique songs from {selectedPlaylists.Count} selected playlists");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.Error($"PlaylistManager: GetSongsFromSelectedPlaylists error: {ex.Message}");
            }

            return songs;
        }
    }
}

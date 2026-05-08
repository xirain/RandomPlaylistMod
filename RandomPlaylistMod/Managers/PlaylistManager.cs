
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
        private List<SongInfo> _songsCache;
        private bool _songsCacheDirty = true;

        public List<PlaylistInfo> Playlists => _playlists;

        /// <summary>
        /// 标记歌曲缓存为需要重建
        /// </summary>
        private void InvalidateSongsCache()
        {
            _songsCacheDirty = true;
            _songsCache = null;
        }

        public void LoadPlaylists()
        {
            _playlists.Clear();
            InvalidateSongsCache();

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
                InvalidateSongsCache();
                Plugin.Log.Info($"PlaylistManager: Toggled playlist '{playlist.Name}' to {playlist.Selected}");
            }
        }

        public void SelectAllPlaylists()
        {
            foreach (var playlist in _playlists)
            {
                playlist.Selected = true;
            }
            InvalidateSongsCache();
        }

        public void DeselectAllPlaylists()
        {
            foreach (var playlist in _playlists)
            {
                playlist.Selected = false;
            }
            InvalidateSongsCache();
        }

        public List<PlaylistInfo> GetSelectedPlaylists()
        {
            return _playlists.Where(p => p.Selected).ToList();
        }

        /// <summary>
        /// 获取选中播放列表中的所有歌曲（带缓存）
        /// </summary>
        public List<SongInfo> GetSongsFromSelectedPlaylists()
        {
            // 缓存有效时直接返回
            if (!_songsCacheDirty && _songsCache != null)
                return _songsCache;

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

                            // 获取 BPM（通过反射，避免直接引用 BeatmapLevelSO）
                            int bpm = 0;
                            try
                            {
                                // 尝试从 beatmapBasicData 获取 BPM
                                var basicDataProp = level.GetType().GetProperty("beatmapBasicData");
                                if (basicDataProp != null)
                                {
                                    var basicData = basicDataProp.GetValue(level) as System.Collections.IDictionary;
                                    if (basicData != null)
                                    {
                                        foreach (var entry in basicData)
                                        {
                                            // entry 是 KeyValuePair<(BeatmapCharacteristicSO, BeatmapDifficulty), BeatmapBasicData>
                                            var entryType = entry.GetType();
                                            var valueProp = entryType.GetProperty("Value");
                                            if (valueProp != null)
                                            {
                                                var beatmapBasicData = valueProp.GetValue(entry);
                                                if (beatmapBasicData != null)
                                                {
                                                    var bpmProp = beatmapBasicData.GetType().GetProperty("bpm");
                                                    if (bpmProp != null)
                                                    {
                                                        bpm = (int)(float)(bpmProp.GetValue(beatmapBasicData) ?? 0f);
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }

                            songs.Add(new SongInfo
                            {
                                LevelId = levelId,
                                SongName = level.songName ?? song.Name ?? "Unknown",
                                Author = level.songAuthorName ?? "",
                                Duration = (int)level.songDuration,
                                Key = song.Key ?? "",
                                PlaylistName = selectedInfo.Name,
                                BPM = bpm
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

            // 更新缓存
            _songsCache = songs;
            _songsCacheDirty = false;
            return songs;
        }
    }
}

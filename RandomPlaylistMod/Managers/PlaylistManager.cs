using System.Collections.Generic;
using System.Linq;
using BeatSaberPlaylistsLib;
using BeatSaberPlaylistsLib.Types;
using RandomPlaylistMod.Models;
using SongCore;
using SongDetailsCache;
using SongDetailsCache.Structs;
using UnityEngine;

namespace RandomPlaylistMod.Managers
{
    public class PlaylistManager
    {
        private readonly List<PlaylistInfo> _playlists = new List<PlaylistInfo>();
        private List<SongInfo> _songsCache;
        private bool _songsCacheDirty = true;
        private static SongDetails _songDetailsCache;

        /// <summary>
        /// 自定义歌曲虚拟播放列表的唯一 ID
        /// </summary>
        private const string CustomLevelsId = "__custom_levels__";

        /// <summary>
        /// 官方歌曲（OST）虚拟播放列表的唯一 ID
        /// </summary>
        private const string OfficialLevelsId = "__official_levels__";

        private static SongDetails GetSongDetails()
        {
            if (_songDetailsCache == null)
            {
                try { _songDetailsCache = SongDetails.Init().GetAwaiter().GetResult(); }
                catch { Plugin.Log.Warn("PlaylistManager: Failed to init SongDetailsCache"); }
            }
            return _songDetailsCache;
        }

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

                // 添加自定义歌曲虚拟播放列表
                AddCustomLevelsPlaylist();

                // 添加官方歌曲（OST）虚拟播放列表
                AddOfficialLevelsPlaylist();
            }
            catch (System.Exception ex)
            {
                Plugin.Log.Error($"PlaylistManager: Failed to load playlists - {ex.Message}");
                Plugin.Log.Error($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 添加自定义歌曲虚拟播放列表项
        /// </summary>
        private void AddCustomLevelsPlaylist()
        {
            try
            {
                // SongCore 不再提供全局枚举 API，使用 CustomLevels 构建虚拟歌单
                int customCount = 0;
                int totalDuration = 0;

                try
                {
                    var customLevels = Loader.CustomLevels;
                    if (customLevels != null)
                    {
                        customCount = customLevels.Count;
                        foreach (var kvp in customLevels)
                        {
                            var level = kvp.Value;
                            if (level != null)
                                totalDuration += (int)level.songDuration;
                        }
                    }
                }
                catch { }

                var playlist = new PlaylistInfo
                {
                    Id = CustomLevelsId,
                    Name = "🎮 所有自定义歌曲",
                    Author = "Custom Levels",
                    Selected = false,
                    SongCount = customCount,
                    PlayableSongCount = customCount,
                    TotalDuration = totalDuration
                };

                _playlists.Add(playlist);
                Plugin.Log.Info($"PlaylistManager: Added Custom Levels playlist with {customCount} songs ({totalDuration}s)");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.Error($"PlaylistManager: Failed to add Custom Levels playlist - {ex.Message}");
            }
        }

        /// <summary>
        /// 添加官方歌曲（OST）虚拟播放列表项
        /// </summary>
        private void AddOfficialLevelsPlaylist()
        {
            try
            {
                // SongCore 3.x 中 Loader.OfficialSongs 是 private static readonly 字段，
                // 必须用反射读取（OfficialSongEntry 是嵌套类型且字段私有，跨 assembly 无法直接访问）
                int officialCount = 0;
                int totalDuration = 0;

                try
                {
                    var officialDict = GetOfficialSongsDict();
                    if (officialDict != null)
                    {
                        foreach (System.Collections.DictionaryEntry de in officialDict)
                        {
                            string levelId = de.Key as string;
                            if (string.IsNullOrEmpty(levelId)) continue;

                            // 通过 Loader.GetLevelById 反查 BeatmapLevel（公共 API，支持 official levels）
                            var level = Loader.GetLevelById(levelId);
                            if (level != null)
                            {
                                officialCount++;
                                totalDuration += (int)level.songDuration;
                            }
                        }
                    }
                }
                catch { }

                var playlist = new PlaylistInfo
                {
                    Id = OfficialLevelsId,
                    Name = "🎼 官方歌曲 (OST)",
                    Author = "Beat Games",
                    Selected = false,
                    SongCount = officialCount,
                    PlayableSongCount = officialCount,
                    TotalDuration = totalDuration
                };

                _playlists.Add(playlist);
                Plugin.Log.Info($"PlaylistManager: Added Official Levels playlist with {officialCount} songs ({totalDuration}s)");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.Error($"PlaylistManager: Failed to add Official Levels playlist - {ex.Message}");
            }
        }

        /// <summary>
        /// 通过反射读取 SongCore.Loader.OfficialSongs 私有字段
        /// </summary>
        private static System.Collections.IDictionary GetOfficialSongsDict()
        {
            var field = typeof(Loader).GetField("OfficialSongs",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            return field?.GetValue(null) as System.Collections.IDictionary;
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
                    // 处理自定义歌曲虚拟播放列表
                    if (selectedInfo.Id == CustomLevelsId)
                    {
                        AddCustomLevelSongs(songs, seenLevelIds);
                        continue;
                    }

                    // 处理官方歌曲（OST）虚拟播放列表
                    if (selectedInfo.Id == OfficialLevelsId)
                    {
                        AddOfficialLevelSongs(songs, seenLevelIds);
                        continue;
                    }

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

                            // 获取 BPM（通过反射）
                            int bpm = 0;
                            try
                            {
                                var basicDataProp = level.GetType().GetProperty("beatmapBasicData");
                                if (basicDataProp != null)
                                {
                                    var basicData = basicDataProp.GetValue(level) as System.Collections.IDictionary;
                                    if (basicData != null)
                                    {
                                        foreach (var entry in basicData)
                                        {
                                            var entryType = entry.GetType();
                                            var valueProp = entryType.GetProperty("Value");
                                            if (valueProp == null) continue;
                                            var bbd = valueProp.GetValue(entry);
                                            if (bbd == null) continue;
                                            var bpmProp = bbd.GetType().GetProperty("bpm");
                                            if (bpmProp != null)
                                            {
                                                bpm = (int)(float)(bpmProp.GetValue(bbd) ?? 0f);
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }

                            // 获取 NPS（从 SongDetailsCache: max(notes/duration) 跨所有难度）
                            float nps = -1f;
                            try
                            {
                                string hash = levelId;
                                if (hash.StartsWith("custom_level_"))
                                    hash = hash.Substring(13);
                                var sd = GetSongDetails();
                                if (sd != null && sd.songs.FindByHash(hash, out Song sdcSong))
                                {
                                    float dur = (float)sdcSong.songDurationSeconds;
                                    if (dur > 0f)
                                    {
                                        foreach (var diff in sdcSong.difficulties)
                                        {
                                            float dnps = diff.notes / dur;
                                            if (dnps > nps) nps = dnps;
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
                                BPM = bpm,
                                NPS = nps
                            });
                        }
                        catch (System.Exception ex)
                        {
                            Plugin.Log.Error($"PlaylistManager: Error processing song '{song.Name}': {ex.Message}");
                        }
                    }
                }

                int songsWithNPS = songs.Count(s => s.NPS >= 0f);
                float avgNPS = songsWithNPS > 0 ? songs.Where(s => s.NPS >= 0).Average(s => s.NPS) : 0f;
                Plugin.Log.Info($"PlaylistManager: Got {songs.Count} unique songs from {selectedPlaylists.Count} selected playlists ({songsWithNPS} with NPS, avg {avgNPS:F1})");
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

        /// <summary>
        /// 将自定义歌曲添加到歌曲列表中
        /// </summary>
        private void AddCustomLevelSongs(List<SongInfo> songs, HashSet<string> seenLevelIds)
        {
            try
            {
                // SongCore 不再提供全局枚举 API，仅枚举 CustomLevels
                var customLevels = Loader.CustomLevels;
                if (customLevels == null) return;

                int added = 0;
                foreach (var kvp in customLevels)
                {
                    string levelId = kvp.Key;
                    if (seenLevelIds.Contains(levelId)) continue;
                    seenLevelIds.Add(levelId);

                    var level = kvp.Value;
                    if (level == null) continue;

                    // 获取 BPM（通过反射）
                    int bpm = 0;
                    try
                    {
                        var basicDataProp = level.GetType().GetProperty("beatmapBasicData");
                        if (basicDataProp != null)
                        {
                            var basicData = basicDataProp.GetValue(level) as System.Collections.IDictionary;
                            if (basicData != null)
                            {
                                foreach (var entry in basicData)
                                {
                                    var entryType = entry.GetType();
                                    var valueProp = entryType.GetProperty("Value");
                                    if (valueProp == null) continue;
                                    var bbd = valueProp.GetValue(entry);
                                    if (bbd == null) continue;
                                    var bpmProp = bbd.GetType().GetProperty("bpm");
                                    if (bpmProp != null)
                                    {
                                        bpm = (int)(float)(bpmProp.GetValue(bbd) ?? 0f);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    songs.Add(new SongInfo
                    {
                        LevelId = levelId,
                        SongName = level.songName ?? "Unknown",
                        Author = level.songAuthorName ?? "",
                        Duration = (int)level.songDuration,
                        Key = "",
                        PlaylistName = "自定义歌曲",
                        BPM = bpm,
                        NPS = -1f
                    });
                    added++;
                }

                Plugin.Log.Info($"PlaylistManager: Added {added} songs from CustomLevels");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.Error($"PlaylistManager: Error adding songs - {ex.Message}");
            }
        }

        /// <summary>
        /// 将官方歌曲（OST）添加到歌曲列表中
        /// </summary>
        private void AddOfficialLevelSongs(List<SongInfo> songs, HashSet<string> seenLevelIds)
        {
            try
            {
                var officialDict = GetOfficialSongsDict();
                if (officialDict == null) return;

                int added = 0;
                foreach (System.Collections.DictionaryEntry de in officialDict)
                {
                    string levelId = de.Key as string;
                    if (string.IsNullOrEmpty(levelId)) continue;
                    if (seenLevelIds.Contains(levelId)) continue;
                    seenLevelIds.Add(levelId);

                    // 反查 BeatmapLevel（公共 API）
                    var level = Loader.GetLevelById(levelId);
                    if (level == null) continue;

                    // 官方歌曲在 SongDetailsCache 中无 hash 记录，NPS 保持 -1（始终通过 NPS 过滤）
                    songs.Add(new SongInfo
                    {
                        LevelId = levelId,
                        SongName = level.songName ?? "Unknown",
                        Author = level.songAuthorName ?? "",
                        Duration = (int)level.songDuration,
                        Key = "",
                        PlaylistName = "官方歌曲",
                        BPM = 0,
                        NPS = -1f
                    });
                    added++;
                }

                Plugin.Log.Info($"PlaylistManager: Added {added} songs from OfficialSongs");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.Error($"PlaylistManager: Error adding official songs - {ex.Message}");
            }
        }
    }
}

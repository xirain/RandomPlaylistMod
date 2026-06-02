using System;
using System.Collections.Generic;
using System.Linq;
using RandomPlaylistMod.Models;
using UnityEngine;
using Zenject;
using SongCore;
using SongDetailsCache;
using SongDetailsCache.Structs;

namespace RandomPlaylistMod.Managers
{
    public class PlaySessionManager : IInitializable, IDisposable
    {
        private readonly PlaylistManager _playlistManager;
        private readonly SongSelector _songSelector;
        private readonly TimeManager _timeManager;
        private readonly EnvironmentsListModel _environmentsListModel;
        private readonly System.Random _rng = new System.Random();
        private PlaySession _currentSession;
        private List<SongInfo> _currentSongQueue;
        private int _currentSongIndex;
        private static SongDetails _songDetailsCache;

        // 事件系统
        public event Action<PlaySession> SessionStarted;
        public event Action<PlaySession> SessionEnded;
        public event Action<SongInfo, int, int> SongChanged; // song, currentIndex, totalCount
        public event Action<SongInfo, string> SongFailed; // song, reason

        public PlaySessionManager(
            PlaylistManager playlistManager,
            SongSelector songSelector,
            TimeManager timeManager,
            EnvironmentsListModel environmentsListModel)
        {
            _playlistManager = playlistManager;
            _songSelector = songSelector;
            _timeManager = timeManager;
            _environmentsListModel = environmentsListModel;
        }

        public bool IsSessionActive => _currentSession != null;
        public SongInfo CurrentSong => GetCurrentSong();
        public int RemainingSongCount => _currentSongQueue?.Count - _currentSongIndex ?? 0;

        // NPS 筛选范围（由 UI 在 StartSession 前设置）
        public float MinNPS { get; set; } = 0f;
        public float MaxNPS { get; set; } = 99f;
        public bool NoFailEnabled { get; set; } = false;

        private static SongDetails GetSongDetails()
        {
            if (_songDetailsCache == null)
            {
                try { _songDetailsCache = SongDetails.Init().GetAwaiter().GetResult(); }
                catch { Plugin.Log.Warn("PlaySessionManager: Failed to init SongDetailsCache"); }
            }
            return _songDetailsCache;
        }

        private BeatmapDifficulty SelectBestDifficulty(BeatmapLevel beatmapLevel, BeatmapCharacteristicSO characteristic, List<BeatmapDifficulty> availableDifficulties)
        {
            // 尝试从 SongDetailsCache 获取每个难度的 NPS
            Dictionary<BeatmapDifficulty, float> difficultyNPS = null;

            try
            {
                string hash = beatmapLevel.levelID;
                if (hash.StartsWith("custom_level_"))
                    hash = hash.Substring(13);

                var sd = GetSongDetails();
                if (sd != null && sd.songs.FindByHash(hash, out Song sdcSong))
                {
                    float dur = (float)sdcSong.songDurationSeconds;
                    if (dur > 0f)
                    {
                        difficultyNPS = new Dictionary<BeatmapDifficulty, float>();
                        foreach (var diff in sdcSong.difficulties)
                        {
                            // MapDifficulty 可以直接强制转换为 BeatmapDifficulty（值相同）
                            BeatmapDifficulty beatmapDiff = (BeatmapDifficulty)diff.difficulty;
                            float dnps = diff.notes / dur;
                            if (!difficultyNPS.ContainsKey(beatmapDiff) || dnps > difficultyNPS[beatmapDiff])
                                difficultyNPS[beatmapDiff] = dnps;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"PlaySessionManager: Error getting difficulty NPS: {ex.Message}");
            }

            // 如果有 NPS 数据，在符合范围的难度中随机选一个
            if (difficultyNPS != null && difficultyNPS.Count > 0)
            {
                var matching = availableDifficulties
                    .Where(d => difficultyNPS.ContainsKey(d) && difficultyNPS[d] >= MinNPS && difficultyNPS[d] <= MaxNPS)
                    .ToList();

                if (matching.Count > 0)
                {
                    var chosen = matching[_rng.Next(matching.Count)];
                    Plugin.Log.Info($"PlaySessionManager: Selected difficulty {chosen} (NPS {difficultyNPS[chosen]:F1}) from {matching.Count} candidates");
                    return chosen;
                }

                Plugin.Log.Info($"PlaySessionManager: No difficulty in NPS range [{MinNPS:F1}-{MaxNPS:F1}], using hardest available");
            }

            // fallback：选最高可用难度
            var fallback = availableDifficulties.OrderByDescending(d => (int)d).First();
            Plugin.Log.Info($"PlaySessionManager: Selected difficulty {fallback} (fallback to hardest)");
            return fallback;
        }

        public void Initialize()
        {
            Plugin.Log.Info("PlaySessionManager: Initialized");
        }

        public void Dispose()
        {
            if (IsSessionActive)
                EndSession();
        }

        public void StartSession(int durationMinutes)
        {
            StartSession(new SessionSettings
            {
                DurationMinutes = durationMinutes,
                MinNps = MinNPS,
                MaxNps = MaxNPS,
                NoFailEnabled = NoFailEnabled
            });
        }

        public void StartSession(SessionSettings settings)
        {
            if (settings == null)
            {
                Plugin.Log.Warn("PlaySessionManager: Session settings is null");
                return;
            }

            MinNPS = settings.MinNps;
            MaxNPS = settings.MaxNps;
            NoFailEnabled = settings.NoFailEnabled;

            var allSongs = _playlistManager.GetSongsFromSelectedPlaylists();

            if (allSongs.Count == 0)
            {
                Plugin.Log.Warn("PlaySessionManager: No songs available in selected playlists");
                return;
            }

            _currentSongQueue = _songSelector.SelectSongsForDuration(allSongs, settings.DurationMinutes, MinNPS, MaxNPS);
            _currentSongIndex = 0;

            if (_currentSongQueue.Count == 0)
            {
                Plugin.Log.Warn("PlaySessionManager: Song selector returned empty queue");
                return;
            }

            _currentSession = new PlaySession
            {
                DurationMinutes = settings.DurationMinutes,
                TotalSongs = _currentSongQueue.Count,
                StartTime = Time.time
            };

            _timeManager.StartTimer();
            Plugin.Log.Info($"PlaySessionManager: Session started - {settings.DurationMinutes} min, {_currentSongQueue.Count} songs, no-fail={NoFailEnabled}");

            SessionStarted?.Invoke(_currentSession);
            PlayNextSong();
        }

        public void EndSession()
        {
            var songCount = _currentSongIndex;
            var session = _currentSession;
            _currentSession = null;
            _currentSongQueue = null;
            _currentSongIndex = 0;
            _timeManager.StopTimer();

            if (session != null)
            {
                session.CurrentSongIndex = songCount;
                SessionEnded?.Invoke(session);
            }

            Plugin.Log.Info($"PlaySessionManager: Session ended after {songCount} songs");
        }

        public SongInfo GetCurrentSong()
        {
            if (_currentSongQueue == null || _currentSongIndex >= _currentSongQueue.Count)
                return null;

            return _currentSongQueue[_currentSongIndex];
        }

        public SongInfo AdvanceToNextSong()
        {
            _currentSongIndex++;
            return GetCurrentSong();
        }

        public bool HasNextSong()
        {
            return _currentSongQueue != null && _currentSongIndex + 1 < _currentSongQueue.Count;
        }

        public PlaySession GetCurrentSession()
        {
            if (_currentSession != null)
            {
                _currentSession.ElapsedMinutes = _timeManager.GetElapsedSeconds() / 60f;
                _currentSession.CurrentSongIndex = _currentSongIndex;
            }
            return _currentSession;
        }

        /// <summary>
        /// 在歌曲完成后推进到下一首歌曲
        /// 只有计时器到期才会结束会话；队列耗尽时 PlayNextSong 会自动重新打乱
        /// </summary>
        public void OnSongFinished()
        {
            if (!IsSessionActive)
                return;

            // 唯一结束条件：计时器到期
            if (_currentSession != null && _timeManager.IsTimeUp(_currentSession.DurationMinutes))
            {
                Plugin.Log.Info($"PlaySessionManager: Session time is up ({_timeManager.GetElapsedMinutes():F1}/{_currentSession.DurationMinutes} min), ending session");
                EndSession();
                return;
            }

            // 没到期：推进到下一首（队列耗尽时 PlayNextSong 会自动重新打乱）
            AdvanceToNextSong();
            PlayNextSong();
        }

        private void PlayNextSong()
        {
            var song = GetCurrentSong();
            if (song == null)
            {
                // 队列耗尽，重新打乱继续播放（计时器是唯一退出条件）
                if (_currentSongQueue != null && _currentSongQueue.Count > 0)
                {
                    Plugin.Log.Info($"PlaySessionManager: Queue exhausted, reshuffling {_currentSongQueue.Count} songs...");
                    _currentSongQueue = _songSelector.ShuffleSongs(_currentSongQueue);
                    _currentSongIndex = 0;
                    song = GetCurrentSong();
                }

                if (song == null)
                {
                    Plugin.Log.Info("PlaySessionManager: No songs available after reshuffle, ending session");
                    EndSession();
                    return;
                }
            }

            Plugin.Log.Info($"PlaySessionManager: Playing song '{song.SongName}' by {song.Author} (ID: {song.LevelId})");
            SongChanged?.Invoke(song, _currentSongIndex, _currentSongQueue.Count);
            StartLevel(song);
        }

        /// <summary>
        /// 使用 MenuTransitionsHelper.StartStandardLevel 启动关卡
        /// </summary>
        private void StartLevel(SongInfo song)
        {
            try
            {
                // 通过 SongCore 获取 BeatmapLevel (实际是 BeatmapLevelSO)
                var beatmapLevel = Loader.GetLevelById(song.LevelId);
                if (beatmapLevel == null)
                {
                    Plugin.Log.Error($"PlaySessionManager: Could not find level for '{song.SongName}' (ID: {song.LevelId})");
                    SongFailed?.Invoke(song, "Level not found");
                    AdvanceToNextSong();
                    PlayNextSong();
                    return;
                }

                // 使用 BeatmapLevel.GetCharacteristics() 获取特征列表
                var characteristics = beatmapLevel.GetCharacteristics()?.ToList();
                if (characteristics == null || characteristics.Count == 0)
                {
                    Plugin.Log.Error($"PlaySessionManager: No characteristics found for '{song.SongName}'");
                    SongFailed?.Invoke(song, "No characteristics");
                    AdvanceToNextSong();
                    PlayNextSong();
                    return;
                }

                // 使用第一个特征（通常是 Standard）
                var characteristic = characteristics[0];

                // 获取该特征下可用的难度
                var difficulties = beatmapLevel.GetDifficulties(characteristic)?.ToList();
                if (difficulties == null || difficulties.Count == 0)
                {
                    Plugin.Log.Error($"PlaySessionManager: No difficulties found for '{song.SongName}'");
                    SongFailed?.Invoke(song, "No difficulties");
                    AdvanceToNextSong();
                    PlayNextSong();
                    return;
                }

                // 选择合适难度：优先匹配 NPS 范围的最难难度
                var difficulty = SelectBestDifficulty(beatmapLevel, characteristic, difficulties);

                Plugin.Log.Info($"PlaySessionManager: Launching level '{song.SongName}' difficulty {difficulty} characteristic {characteristic.serializedName}");

                // 获取 MenuTransitionsHelper
                var menuTransitionsHelper = Resources.FindObjectsOfTypeAll<MenuTransitionsHelper>().FirstOrDefault();
                if (menuTransitionsHelper == null)
                {
                    Plugin.Log.Error("PlaySessionManager: MenuTransitionsHelper not found!");
                    SongFailed?.Invoke(song, "MenuTransitionsHelper not found");
                    AdvanceToNextSong();
                    PlayNextSong();
                    return;
                }

                // 获取 EnvironmentsListModel（通过 Zenject 注入，不能为 null）
                if (_environmentsListModel == null)
                {
                    Plugin.Log.Error("PlaySessionManager: EnvironmentsListModel is null!");
                    SongFailed?.Invoke(song, "EnvironmentsListModel not found");
                    AdvanceToNextSong();
                    PlayNextSong();
                    return;
                }

                var beatmapKey = new BeatmapKey(
                    beatmapLevel.levelID,
                    characteristic,
                    difficulty
                );

                var colorScheme = beatmapLevel.GetColorScheme(characteristic, difficulty);
                var overrideEnvironmentSettings = new OverrideEnvironmentSettings();

                // 启动标准关卡（参数顺序匹配 Beat Saber 1.40+ 的 StartStandardLevel 签名）
                menuTransitionsHelper.StartStandardLevel(
                    "Solo",                             // gameMode
                    in beatmapKey,                       // beatmapKey
                    beatmapLevel,                        // beatmapLevel
                    overrideEnvironmentSettings,         // overrideEnvironmentSettings
                    colorScheme,                         // playerOverrideColorScheme
                    true,                                // playerOverrideLightshowColors
                    colorScheme,                         // beatmapOverrideColorScheme
                    CreateGameplayModifiers(),            // gameplayModifiers
                    new PlayerSpecificSettings(),         // playerSpecificSettings
                    null,                                // practiceSettings
                    _environmentsListModel,              // environmentsListModel
                    null,                                // backButtonText
                    false,                               // useTestNoteCutSoundEffects
                    false,                               // startPaused
                    null,                                // beforeSceneSwitchToGameplayCallback
                    null,                                // afterSceneSwitchToGameplayCallback
                    OnLevelCompleted,                    // levelFinishedCallback
                    null,                                // levelRestartedCallback
                    null                                 // recordingToolData
                );

                Plugin.Log.Info($"PlaySessionManager: Level '{song.SongName}' started successfully");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.Error($"PlaySessionManager: Error starting level '{song.SongName}': {ex.Message}");
                Plugin.Log.Error($"Stack trace: {ex.StackTrace}");
                SongFailed?.Invoke(song, ex.Message);
                AdvanceToNextSong();
                PlayNextSong();
            }
        }

        private GameplayModifiers CreateGameplayModifiers()
        {
            var modifiers = new GameplayModifiers();
            
            Plugin.Log.Info($"[DEBUG] CreateGameplayModifiers: NoFailEnabled={NoFailEnabled}");
            
            if (NoFailEnabled)
            {
                Plugin.Log.Info("[DEBUG] Attempting to enable No Fail...");
                TryEnableNoFailModifier(modifiers);
                
                // 验证是否设置成功
                try
                {
                    var verifyField = typeof(GameplayModifiers).GetField("<noFailOn0Energy>k__BackingField",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (verifyField != null)
                    {
                        var value = verifyField.GetValue(modifiers);
                        Plugin.Log.Info($"[DEBUG] No Fail verification: {value}");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.Warn($"[DEBUG] Verification failed: {ex.Message}");
                }
            }
            else
            {
                Plugin.Log.Info("[DEBUG] No Fail is disabled, skipping");
            }
            
            return modifiers;
        }

        private void TryEnableNoFailModifier(GameplayModifiers modifiers)
        {
            try
            {
                Plugin.Log.Info("[DEBUG] Trying to enable No Fail...");

                // 方式1: 尝试私有字段 _noFailOn0Energy（Beat Saber 1.40.8 实际字段名）
                var privateField = typeof(GameplayModifiers).GetField("_noFailOn0Energy",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                if (privateField != null && privateField.FieldType == typeof(bool))
                {
                    privateField.SetValue(modifiers, true);
                    Plugin.Log.Info("PlaySessionManager: No Fail enabled via _noFailOn0Energy field");
                    return;
                }

                // 方式2: 尝试自动属性 backing field
                var backingField = typeof(GameplayModifiers).GetField("<noFailOn0Energy>k__BackingField",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                if (backingField != null)
                {
                    backingField.SetValue(modifiers, true);
                    Plugin.Log.Info("PlaySessionManager: No Fail enabled via backing field");
                    return;
                }

                // 方式3: 尝试公有属性（如果将来版本改为可写）
                var property = typeof(GameplayModifiers).GetProperty("noFailOn0Energy",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                
                if (property != null && property.CanWrite)
                {
                    property.SetValue(modifiers, true);
                    Plugin.Log.Info("PlaySessionManager: No Fail enabled via public property setter");
                    return;
                }

                Plugin.Log.Warn("PlaySessionManager: Could not enable No Fail - field/property not found or not writable");
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"PlaySessionManager: Failed to enable No Fail modifier: {ex.Message}");
                Plugin.Log.Warn($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 关卡完成回调，推进到下一首歌曲
        /// </summary>
        private void OnLevelCompleted(StandardLevelScenesTransitionSetupDataSO setupData, LevelCompletionResults results)
        {
            Plugin.Log.Info($"PlaySessionManager: Level completed with rank {results?.rank}");
            OnSongFinished();
        }
    }
}

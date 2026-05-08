using System;
using System.Collections.Generic;
using System.Linq;
using RandomPlaylistMod.Models;
using UnityEngine;
using Zenject;
using SongCore;

namespace RandomPlaylistMod.Managers
{
    public class PlaySessionManager : IInitializable, IDisposable
    {
        private readonly PlaylistManager _playlistManager;
        private readonly SongSelector _songSelector;
        private readonly TimeManager _timeManager;
        private readonly EnvironmentsListModel _environmentsListModel;
        private PlaySession _currentSession;
        private List<SongInfo> _currentSongQueue;
        private int _currentSongIndex;

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

        // BPM 筛选范围（由 UI 在 StartSession 前设置）
        public int MinBPM { get; set; } = 0;
        public int MaxBPM { get; set; } = 300;

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
            var allSongs = _playlistManager.GetSongsFromSelectedPlaylists();

            if (allSongs.Count == 0)
            {
                Plugin.Log.Warn("PlaySessionManager: No songs available in selected playlists");
                return;
            }

            _currentSongQueue = _songSelector.SelectSongsForDuration(allSongs, durationMinutes, MinBPM, MaxBPM);
            _currentSongIndex = 0;

            if (_currentSongQueue.Count == 0)
            {
                Plugin.Log.Warn("PlaySessionManager: Song selector returned empty queue");
                return;
            }

            _currentSession = new PlaySession
            {
                DurationMinutes = durationMinutes,
                TotalSongs = _currentSongQueue.Count,
                StartTime = Time.time
            };

            _timeManager.StartTimer();
            Plugin.Log.Info($"PlaySessionManager: Session started - {durationMinutes} min, {_currentSongQueue.Count} songs");

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
        /// 检查会话是否超时，超时则自动结束
        /// </summary>
        public void OnSongFinished()
        {
            if (!IsSessionActive)
                return;

            // 超时检查：如果已超过目标时长，结束会话
            if (_currentSession != null && _timeManager.IsTimeUp(_currentSession.DurationMinutes))
            {
                Plugin.Log.Info($"PlaySessionManager: Session time is up ({_timeManager.GetElapsedMinutes():F1}/{_currentSession.DurationMinutes} min), ending session");
                EndSession();
                return;
            }

            if (!HasNextSong())
            {
                Plugin.Log.Info("PlaySessionManager: No more songs, session complete");
                EndSession();
                return;
            }

            AdvanceToNextSong();
            PlayNextSong();
        }

        private void PlayNextSong()
        {
            var song = GetCurrentSong();
            if (song == null)
            {
                Plugin.Log.Info("PlaySessionManager: No more songs, session complete");
                EndSession();
                return;
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

                // 使用第一个可用难度
                var difficulty = difficulties[0];

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
                    new GameplayModifiers(),              // gameplayModifiers
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

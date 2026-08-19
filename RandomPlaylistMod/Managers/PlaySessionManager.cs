using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
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
        private readonly HistoryManager _historyManager;
        private readonly EnvironmentsListModel _environmentsListModel;
        private readonly System.Random _rng = new System.Random();
        private PlaySession _currentSession;
        private List<SongInfo> _currentSongQueue;
        private int _currentSongIndex;
        private static SongDetails _songDetailsCache;

        // Phase 2: 数据持久化
        private List<SongResult> _currentResults;      // 本次会话所有歌曲结果
        private string _currentDifficulty;              // 当前播放的难度名（用于回调提取）
        private float _currentNPS;                      // 当前播放的 NPS（用于回调提取）
        private DateTime _sessionStartedAt;              // 会话启动时间
        private List<string> _playlistNamesSnapshot;    // 启动时的歌单名称快照
        private List<string> _playlistIdsSnapshot;      // 启动时的歌单ID快照
        private int _totalSongsInQueueSnapshot;         // 启动时的队列歌曲数
        private int _availableSongCountSnapshot;        // 启动时的可用歌曲总数
        private string _currentSongNameForCallback;     // 当前歌曲名（用于回调）
        private string _currentAuthorForCallback;       // 当前作者名（用于回调）
        private string _currentLevelIdForCallback;      // 当前关卡ID（用于回调）
        private int _currentSongDurationForCallback;    // 当前歌曲时长（用于回调）

        // 事件系统
        public event Action<PlaySession, SessionRecord> SessionEndedWithRecord; // 新增：带 SessionRecord 的会话结束事件
        public event Action<PlaySession> SessionStarted;
        public event Action<PlaySession> SessionEnded;
        public event Action<SongInfo, int, int> SongChanged; // song, currentIndex, totalCount
        public event Action<SongInfo, string> SongFailed; // song, reason

        public PlaySessionManager(
            PlaylistManager playlistManager,
            SongSelector songSelector,
            TimeManager timeManager,
            HistoryManager historyManager,
            EnvironmentsListModel environmentsListModel)
        {
            _playlistManager = playlistManager;
            _songSelector = songSelector;
            _timeManager = timeManager;
            _historyManager = historyManager;
            _environmentsListModel = environmentsListModel;
        }

        public bool IsSessionActive => _currentSession != null;
        public SongInfo CurrentSong => GetCurrentSong();
        public int RemainingSongCount => _currentSongQueue?.Count - _currentSongIndex ?? 0;

        // NPS 筛选范围（由 UI 在 StartSession 前设置）
        public float MinNPS { get; set; } = 0f;
        public float MaxNPS { get; set; } = 99f;
        /// <summary>是否不按 NPS 筛选（Any）</summary>
        public bool NpsAny { get; set; } = true;
        /// <summary>选中的具体频段（min,max 列表）；NpsAny=true 时不使用</summary>
        public List<(float min, float max)> NpsBands { get; set; } = new List<(float, float)>();
        public bool NoFailEnabled { get; set; } = false;
        public bool HudEnabled { get; set; } = true;

        /// <summary>
        /// 让 RandomPlaylistMod 优先选用某个 BeatmapCharacteristic 起播，从而借已安装的 AutoBS 模组做增强。
        /// - "90Degree"：选 90° 谱播放（不转身、左右摆头范围，适合运动场地），AutoBS 在其上做墙/灯增强。
        ///   注意 AutoBS 当前版本只对"已有的 90° 谱"生效，不会把 Standard 谱派生成 90°；歌曲无 90° 特征时回退。
        /// - "Generated360Degree"：让 AutoBS 把该谱自动重做成 360° 旋转图（需 AutoBS 360fy 开启）。
        /// - null/空：不指定，直接用歌曲第一个特征（通常 Standard）。
        /// 找不到目标特征（AutoBS 未装 / 歌曲无该特征）时回退到 Standard。
        /// </summary>
        public string AutoBSCharacteristic { get; set; } = "90Degree";

        /// <summary>判定 NPS 是否通过当前筛选（Any 或未知时通过；否则需落在任一频段内）</summary>
        private bool IsNpsAllowed(float nps)
            => LevelBand.InBands(nps, NpsBands, NpsAny);

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

            // 如果有 NPS 数据，在符合筛选的难度中随机选一个
            if (difficultyNPS != null && difficultyNPS.Count > 0)
            {
                var matching = availableDifficulties
                    .Where(d => difficultyNPS.ContainsKey(d) && IsNpsAllowed(difficultyNPS[d]))
                    .ToList();

                if (matching.Count > 0)
                {
                    var chosen = matching[_rng.Next(matching.Count)];
                    Plugin.Log.Info($"PlaySessionManager: Selected difficulty {chosen} (NPS {difficultyNPS[chosen]:F1}) from {matching.Count} candidates");
                    return chosen;
                }

                Plugin.Log.Info($"PlaySessionManager: No difficulty in NPS filter, using hardest available");
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
            // 兼容旧版 UI 只传 Min/Max 的场景：自动推导 NpsAny/NpsBands
            NpsAny = settings.MinNps <= 0f && settings.MaxNps >= 99f;
            NpsBands = NpsAny
                ? new List<(float, float)>()
                : new List<(float, float)> { (settings.MinNps, settings.MaxNps) };
            NoFailEnabled = settings.NoFailEnabled;

            var allSongs = _playlistManager.GetSongsFromSelectedPlaylists();

            if (allSongs.Count == 0)
            {
                Plugin.Log.Warn("PlaySessionManager: No songs available in selected playlists");
                return;
            }

            _currentSongQueue = _songSelector.SelectSongsForDuration(allSongs, settings.DurationMinutes, NpsBands, NpsAny);
            _currentSongIndex = 0;

            if (_currentSongQueue.Count == 0)
            {
                Plugin.Log.Warn("PlaySessionManager: Song selector returned empty queue");
                return;
            }

            // Phase 2: 初始化结果列表和会话快照
            _currentResults = new List<SongResult>();
            _sessionStartedAt = DateTime.Now;
            var selectedPlaylists = _playlistManager.GetSelectedPlaylists();
            _playlistNamesSnapshot = selectedPlaylists.Select(p => p.Name).ToList();
            _playlistIdsSnapshot = selectedPlaylists.Select(p => p.Id).ToList();
            _totalSongsInQueueSnapshot = _currentSongQueue.Count;
            _availableSongCountSnapshot = allSongs.Count;

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
            var results = _currentResults ?? new List<SongResult>();
            _currentSession = null;
            _currentSongQueue = null;
            _currentSongIndex = 0;
            _currentResults = null;
            _timeManager.StopTimer();

            if (session != null)
            {
                session.CurrentSongIndex = songCount;
                SessionEnded?.Invoke(session);

                // Phase 2: 构建 SessionRecord 并保存
                try
                {
                    var sessionId = SessionRecord.GenerateId();
                    // 计算实际时长：优先用 TimeManager（更精确），fallback 到墙钟时间
                    float elapsedFromTimer = _timeManager.GetElapsedMinutes();
                    float elapsedFromClock = _sessionStartedAt != default
                        ? (float)(DateTime.Now - _sessionStartedAt).TotalMinutes
                        : 0f;
                    var actualDurationMin = (int)Math.Round(
                        elapsedFromTimer > 0.5f ? elapsedFromTimer : elapsedFromClock);
                    Plugin.Log.Info($"PlaySessionManager: ActualDuration = {actualDurationMin} min (timer={elapsedFromTimer:F1}, clock={elapsedFromClock:F1})");
                    var exerciseSummary = ExerciseSummary.FromSongResults(results);

                    var record = new SessionRecord
                    {
                        SessionId = sessionId,
                        StartedAt = _sessionStartedAt,
                        EndedAt = DateTime.Now,
                        TargetDurationMin = session.DurationMinutes,
                        ActualDurationMin = actualDurationMin,
                        PlaylistIds = _playlistIdsSnapshot ?? new List<string>(),
                        PlaylistNames = _playlistNamesSnapshot ?? new List<string>(),
                        TotalSongsInQueue = _totalSongsInQueueSnapshot,
                        TotalSongsPlayed = results.Count(r => !r.Failed),
                        SongResults = results,
                        ExerciseSummary = exerciseSummary,
                        Settings = new SessionSettingsSnapshot
                        {
                            MinNPS = MinNPS,
                            MaxNPS = MaxNPS,
                            NpsAny = NpsAny,
                            NpsBandLabels = NpsBands
                                .Select(b => LevelBand.All.FirstOrDefault(x => !x.IsAny && x.Min == b.min && x.Max == b.max)?.Label)
                                .Where(l => l != null)
                                .ToList(),
                            NoFailEnabled = NoFailEnabled,
                            HudEnabled = HudEnabled,
                            PlaylistCount = _playlistIdsSnapshot?.Count ?? 0,
                            AvailableSongCount = _availableSongCountSnapshot
                        },
                        ModVersion = "2.2.0"
                    };

                    // 异步保存会话记录
                    _historyManager.SaveSessionAsync(record);

                    // 增量更新 PlayerProfile
                    _historyManager.IncrementProfile(record);

                    // 触发 Phase 2 的事件
                    SessionEndedWithRecord?.Invoke(session, record);

                    Plugin.Log.Info($"PlaySessionManager: Session record '{sessionId}' saved with {results.Count} song results");
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error($"PlaySessionManager: Failed to save session record: {ex.Message}");
                }
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
                    RecordFailedSong(song, "Level not found");
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
                    RecordFailedSong(song, "No characteristics");
                    SongFailed?.Invoke(song, "No characteristics");
                    AdvanceToNextSong();
                    PlayNextSong();
                    return;
                }

                // 若指定了 AutoBS 生成类特征（Generated90Degree/Generated360Degree），RPM 随机放歌时
                // 跳过了"菜单选歌"流程，AutoBS 的 SetContent patch 不会为该歌注入生成特征，导致
                // GetCharacteristics() 里没有 Generated90Degree，AutoBS 也读不到 BasedOnKey 映射而无法生成。
                // 这里在起播前主动反射调用 AutoBS 的 SetContent.CreateGen360DifficultySet(level)，
                // 复用 AutoBS 自身逻辑把特征注入该 level 并填充生成映射表，从而真正触发 90°/360° 生成。
                if (!string.IsNullOrEmpty(AutoBSCharacteristic) &&
                    (AutoBSCharacteristic.Equals("Generated90Degree", StringComparison.OrdinalIgnoreCase) ||
                     AutoBSCharacteristic.Equals("Generated360Degree", StringComparison.OrdinalIgnoreCase)))
                {
                    EnsureAutoBSGeneratedCharacteristic(beatmapLevel);
                    characteristics = beatmapLevel.GetCharacteristics()?.ToList() ?? characteristics;
                }

                // 选择特征：优先使用指定的 AutoBS 特征（如 "90Degree"），让已安装的 AutoBS 模组做增强。
                // 若指定了特征但歌曲当前特征列表里还没有（AutoBS 在歌曲数据加载阶段才会生成 90° 特征），
                // 不再跳过该歌，而是回退 Standard 播放——AutoBS 的 SetContent patch 通常已为该歌生成了
                // 90° 特征（或在本次加载后下次即生效），这样既能让歌正常播放，也能让 AutoBS 有机会接管生成。
                // 未指定特征时同样回退 Standard。
                var characteristic = SelectCharacteristic(characteristics, out bool characteristicMatched);
                if (!characteristicMatched)
                {
                    Plugin.Log.Info($"PlaySessionManager: 歌曲 '{song.SongName}' 暂无特征 '{AutoBSCharacteristic}'，回退 Standard 播放（AutoBS 可在此播放时/加载后生成 90°）");
                }

                // 获取该特征下可用的难度
                var difficulties = beatmapLevel.GetDifficulties(characteristic)?.ToList();
                if (difficulties == null || difficulties.Count == 0)
                {
                    Plugin.Log.Error($"PlaySessionManager: No difficulties found for '{song.SongName}'");
                    RecordFailedSong(song, "No difficulties");
                    SongFailed?.Invoke(song, "No difficulties");
                    AdvanceToNextSong();
                    PlayNextSong();
                    return;
                }

                // 选择合适难度：优先匹配 NPS 范围的最难难度
                var difficulty = SelectBestDifficulty(beatmapLevel, characteristic, difficulties);

                // Phase 2: 记录当前歌曲信息，供回调使用
                _currentDifficulty = difficulty.ToString();
                _currentSongNameForCallback = song.SongName;
                _currentAuthorForCallback = song.Author;
                _currentLevelIdForCallback = song.LevelId;
                _currentSongDurationForCallback = song.Duration;

                // 计算所选难度的 NPS
                float selectedNPS = -1f;
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
                            foreach (var diff in sdcSong.difficulties)
                            {
                                if ((BeatmapDifficulty)diff.difficulty == difficulty)
                                {
                                    selectedNPS = diff.notes / dur;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { }
                _currentNPS = selectedNPS;

                Plugin.Log.Info($"PlaySessionManager: Launching level '{song.SongName}' difficulty {difficulty} characteristic {characteristic.serializedName} NPS={selectedNPS:F1}");

                // 获取 MenuTransitionsHelper
                var menuTransitionsHelper = Resources.FindObjectsOfTypeAll<MenuTransitionsHelper>().FirstOrDefault();
                if (menuTransitionsHelper == null)
                {
                    Plugin.Log.Error("PlaySessionManager: MenuTransitionsHelper not found!");
                    RecordFailedSong(song, "MenuTransitionsHelper not found");
                    SongFailed?.Invoke(song, "MenuTransitionsHelper not found");
                    AdvanceToNextSong();
                    PlayNextSong();
                    return;
                }

                // 获取 EnvironmentsListModel（通过 Zenject 注入，不能为 null）
                if (_environmentsListModel == null)
                {
                    Plugin.Log.Error("PlaySessionManager: EnvironmentsListModel is null!");
                    RecordFailedSong(song, "EnvironmentsListModel not found");
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

                // 启动标准关卡（参数顺序匹配 Beat Saber 1.44 的 StartStandardLevel 签名）
                // 1.44 相比 1.40 移除了 beatmapOverrideColorScheme / backButtonText /
                // useTestNoteCutSoundEffects / startPaused，新增 gameplayAdditionalInformation 与 beatmapLevelData。
                // 重要：游戏 GameplayCoreSceneSetupData 硬性规定 beatmapLevelData 与 _beatmapLevelsModel 互斥，
                // 而 _beatmapLevelsModel 由容器注入、永远非 null，故此处必须传 null，让游戏走正常的
                // BeatmapLevelsModel 路径加载数据（Loader.GetLevelById 返回的就是已注册进 model 的关卡，
                // 自定义歌与 OST 官方歌均支持）。传非 null 的 beatmapLevelData 会直接抛异常。
                menuTransitionsHelper.StartStandardLevel(
                    "Solo",                             // gameMode
                    in beatmapKey,                       // beatmapKey
                    beatmapLevel,                        // beatmapLevel
                    overrideEnvironmentSettings,         // overrideEnvironmentSettings
                    colorScheme,                         // playerOverrideColorScheme
                    true,                                // playerOverrideLightshowColors
                    CreateGameplayModifiers(),            // gameplayModifiers
                    new PlayerSpecificSettings(),         // playerSpecificSettings
                    null,                                // practiceSettings
                    _environmentsListModel,              // environmentsListModel
                    new GameplayAdditionalInformation(null, false, false, default(PlaymodeOptions), null), // gameplayAdditionalInformation
                    null,                                // beforeSceneSwitchToGameplayCallback
                    null,                                // afterSceneSwitchToGameplayCallback
                    OnLevelCompleted,                    // levelFinishedCallback
                    null,                                // levelRestartedCallback
                    null,                                // beatmapLevelData（传 null，走 BeatmapLevelsModel 正常路径）
                    null                                 // recordingToolData
                );

                Plugin.Log.Info($"PlaySessionManager: Level '{song.SongName}' started successfully");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.Error($"PlaySessionManager: Error starting level '{song.SongName}': {ex.Message}");
                Plugin.Log.Error($"Stack trace: {ex.StackTrace}");
                RecordFailedSong(song, ex.Message);
                SongFailed?.Invoke(song, ex.Message);
                AdvanceToNextSong();
                PlayNextSong();
            }
        }

        /// <summary>
        /// 在起播前主动让 AutoBS 为指定 level 注入生成类特征（Generated90/Generated360）。
        /// 复用 AutoBS 自身逻辑 SetContent.CreateGen360DifficultySet(level)，避免硬引用 AutoBS 程序集。
        /// 该方法会把 Generated90Degree/Generated360Degree 难度集加入该 level 的特征列表，并填充
        /// SetContent.GeneratedToStandardKey 映射，使 AutoBS 的 TransitionPatcher 能据此生成变换谱面。
        /// 若 AutoBS 未安装或方法签名变化，则静默失败（仅记日志），RPM 回退 Standard 播放。
        /// </summary>
        private void EnsureAutoBSGeneratedCharacteristic(BeatmapLevel beatmapLevel)
        {
            try
            {
                const string asmName = "AutoBS";
                const string typeName = "AutoBS.Patches.SetContent";
                const string methodName = "CreateGen360DifficultySet";
                var asm = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == asmName);
                if (asm == null)
                {
                    Plugin.Log.Warn("PlaySessionManager: 未找到 AutoBS 程序集，无法注入生成特征（回退 Standard）");
                    return;
                }
                var type = asm.GetType(typeName);
                if (type == null)
                {
                    Plugin.Log.Warn($"PlaySessionManager: 未找到类型 {typeName}（AutoBS 版本不兼容？）");
                    return;
                }
                var method = type.GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (method == null)
                {
                    Plugin.Log.Warn($"PlaySessionManager: 未找到方法 {typeName}.{methodName}");
                    return;
                }
                method.Invoke(null, new object[] { beatmapLevel });
                Plugin.Log.Info($"PlaySessionManager: 已请求 AutoBS 为 '{beatmapLevel.levelID}' 注入生成特征");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.Warn($"PlaySessionManager: 调用 AutoBS 注入特征失败：{ex.Message}（回退 Standard）");
            }
        }

        /// <summary>
        /// 从歌曲特征列表中选择要播放的特征。
        /// 若设置了 AutoBSCharacteristic（如 "90Degree"）且该特征确实存在于歌曲特征列表中，
        /// 则选用它（matched=true），从而借已安装的 AutoBS 模组做增强；否则 matched=false，回退第一个特征。
        /// </summary>
        private BeatmapCharacteristicSO SelectCharacteristic(List<BeatmapCharacteristicSO> characteristics, out bool matched)
        {
            var target = AutoBSCharacteristic?.Trim();
            if (!string.IsNullOrEmpty(target))
            {
                var found = characteristics.FirstOrDefault(c =>
                    c != null && string.Equals(c.serializedName, target, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                {
                    matched = true;
                    Plugin.Log.Info($"PlaySessionManager: 选用特征 '{target}' 播放（借 AutoBS 增强，不转身）");
                    return found;
                }
                matched = false;
                Plugin.Log.Warn($"PlaySessionManager: 目标特征 '{target}' 不在歌曲特征列表中（AutoBS 可能未安装 / 歌曲无该特征），回退默认特征");
                return characteristics[0];
            }
            matched = true;
            return characteristics[0];
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
        /// Phase 2: 记录一首加载失败的歌曲（部分数据）
        /// </summary>
        private void RecordFailedSong(SongInfo song, string reason)
        {
            if (_currentResults != null && song != null)
            {
                var failedResult = SongResult.CreateFailed(
                    song.SongName,
                    song.Author,
                    song.LevelId,
                    _currentDifficulty ?? "Unknown",
                    song.Duration,
                    song.NPS
                );
                _currentResults.Add(failedResult);
                Plugin.Log.Info($"PlaySessionManager: Failed song recorded - '{song.SongName}' reason: {reason}");
            }
        }

        /// <summary>
        /// 关卡完成回调，捕获结果并推进到下一首歌曲
        /// </summary>
        private void OnLevelCompleted(StandardLevelScenesTransitionSetupDataSO setupData, LevelCompletionResults results)
        {
            Plugin.Log.Info($"PlaySessionManager: Level completed with rank {results?.rank}");

            // Phase 2: 构建 SongResult 并添加到结果列表
            if (_currentResults != null)
            {
                try
                {
                    var songResult = SongResult.FromLevelCompletion(
                        _currentSongNameForCallback ?? "Unknown",
                        _currentAuthorForCallback ?? "",
                        _currentLevelIdForCallback ?? "",
                        _currentDifficulty ?? "Unknown",
                        _currentSongDurationForCallback,
                        _currentNPS,
                        results
                    );
                    _currentResults.Add(songResult);
                    Plugin.Log.Info($"PlaySessionManager: Song result captured - {songResult.SongName} [{songResult.Difficulty}] Score={songResult.Score} Rank={songResult.Rank} Acc={songResult.Accuracy:F1}%");
                }
                catch (Exception ex)
                {
                    Plugin.Log.Warn($"PlaySessionManager: Failed to capture song result: {ex.Message}");
                }
            }

            OnSongFinished();
        }
    }
}

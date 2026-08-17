using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using RandomPlaylistMod.Managers;
using RandomPlaylistMod.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

namespace RandomPlaylistMod.UI
{
    [ViewDefinition("RandomPlaylistMod.UI.Views.RandomPlaylistView.bsml")]
    public class RandomPlaylistUI : BSMLAutomaticViewController
    {
        private PlaylistManager _playlistManager;
        private PlaySessionManager _playSessionManager;
        private SongSelector _songSelector;

        private int _selectedDuration = 30;
        private float _minNPS = 0f;
        private float _maxNPS = 99f;
        private string _estimatedInfo = "~0 songs | 00:00";
        private string _selectedInfo = "No playlists selected";
        private string _sessionStatus = "";
        private bool _noFailEnabled = false;
        private bool _hudEnabled = true;
        // AutoBS 特征模式：Standard(空) / 90°(90Degree) / 360°(Generated360Degree)，控制起播时选用的 BeatmapCharacteristic
        private string _autoBSMode = "90°";
        // 90° 摆幅（度）：45 / 60 / 90。选 90° 系列按钮时写入 AutoBS.Config.Generated90SwingRange
        private int _autoBSSwingRange = 60;

        private Coroutine _sessionUpdateCoroutine;

        [UIComponent("playlist-list")]
        private CustomListTableData _playlistList = null;

        [UIValue("selected-duration")]
        public int SelectedDuration
        {
            get => _selectedDuration;
            set
            {
                _selectedDuration = value;
                UpdateEstimates();
                NotifyPropertyChanged();
            }
        }

        [UIValue("min-nps")]
        public float MinNPS
        {
            get => _minNPS;
            set
            {
                _minNPS = value;
                UpdateEstimates();
                NotifyPropertyChanged();
            }
        }

        [UIValue("max-nps")]
        public float MaxNPS
        {
            get => _maxNPS;
            set
            {
                _maxNPS = value;
                UpdateEstimates();
                NotifyPropertyChanged();
            }
        }

        [UIValue("estimated-info")]
        public string EstimatedInfo
        {
            get => _estimatedInfo;
            set
            {
                _estimatedInfo = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue("selected-info")]
        public string SelectedInfo
        {
            get => _selectedInfo;
            set
            {
                _selectedInfo = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue("selected-playlist-count-text")]
        public string SelectedPlaylistCountText
        {
            get
            {
                int total = _playlistManager?.Playlists?.Count ?? 0;
                int selected = _playlistManager?.GetSelectedPlaylists()?.Count ?? 0;
                return $"({selected}/{total})";
            }
        }

        [UIValue("session-status")]
        public string SessionStatus
        {
            get => _sessionStatus;
            set
            {
                _sessionStatus = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue("no-fail-enabled")]
        public bool NoFailEnabled
        {
            get => _noFailEnabled;
            set
            {
                _noFailEnabled = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(NoFailButtonText));
            }
        }

        [UIValue("no-fail-button-text")]
        public string NoFailButtonText => $"No Fail: {(NoFailEnabled ? "ON" : "OFF")}";

        [UIValue("hud-enabled")]
        public bool HudEnabled
        {
            get => _hudEnabled;
            set
            {
                _hudEnabled = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(HudButtonText));
            }
        }

        [UIValue("hud-button-text")]
        public string HudButtonText => $"HUD: {(HudEnabled ? "ON" : "OFF")}";

        [UIValue("auto-bs-mode")]
        public string AutoBSMode
        {
            get => _autoBSMode;
            set
            {
                _autoBSMode = value;
                NotifyPropertyChanged();
            }
        }

        // 将 UI 模式映射到 PlaySessionManager.AutoBSCharacteristic 的 serializedName
        private static string ModeToCharacteristic(string mode) => mode switch
        {
            "45°" or "60°" or "90°" => "90Degree",
            "360°" => "Generated360Degree",
            _ => "" // Standard：不指定，回退第一特征
        };

        // 从 UI 模式解析 90° 摆幅（度）。非 90° 系列返回 null。
        private static int? SwingRangeFromMode(string mode) => mode switch
        {
            "45°" => 45,
            "60°" => 60,
            "90°" => 90,
            _ => null
        };

        // 通过反射写入已安装的 AutoBS 模组的 Generated90SwingRange 配置（避免硬引用 AutoBS 程序集）。
        // 若 AutoBS 未安装或反射失败，仅记日志，不影响 RandomPlaylistMod 自身播放。
        private void ApplyAutoBSSwingRange(int rangeDegrees)
        {
            try
            {
                var autoBsAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "AutoBS");
                if (autoBsAssembly == null)
                {
                    Plugin.Log.Warn($"AutoBS 未安装，无法设置 90° 摆幅为 {rangeDegrees}°（将使用 AutoBS 默认）");
                    return;
                }
                var configType = autoBsAssembly.GetType("AutoBS.Config");
                var instanceProp = configType?.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var instance = instanceProp?.GetValue(null);
                var rangeProp = configType?.GetProperty("Generated90SwingRange");
                if (instance != null && rangeProp != null)
                {
                    rangeProp.SetValue(instance, rangeDegrees);
                    Plugin.Log.Info($"已通过反射将 AutoBS.Generated90SwingRange 设为 {rangeDegrees}°");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"设置 AutoBS 90° 摆幅失败（{ex.Message}），将使用 AutoBS 默认");
            }
        }

        [Inject]
        public void Construct(PlaylistManager playlistManager, PlaySessionManager playSessionManager, SongSelector songSelector)
        {
            _playlistManager = playlistManager;
            _playSessionManager = playSessionManager;
            _songSelector = songSelector;

            // 订阅会话生命周期事件
            _playSessionManager.SessionStarted += OnSessionStarted;
            _playSessionManager.SessionEnded += OnSessionEnded;
            _playSessionManager.SongChanged += OnSongChanged;
            _playSessionManager.SongFailed += OnSongFailed;

            Plugin.Log.Info("RandomPlaylistUI: Dependencies injected and events subscribed");
        }

        #region 会话事件处理器

        private void OnSessionStarted(PlaySession session)
        {
            Plugin.Log.Info($"RandomPlaylistUI: SessionStarted event - {session.TotalSongs} songs");
        }

        private void OnSessionEnded(PlaySession session)
        {
            Plugin.Log.Info($"RandomPlaylistUI: SessionEnded event - played {session.CurrentSongIndex} songs");
            SessionStatus = $"Session ended - played {session.CurrentSongIndex} songs";
            StopSessionUpdateCoroutine();
            UpdateEstimates();
        }

        private void OnSongChanged(SongInfo song, int currentIndex, int totalCount)
        {
            Plugin.Log.Info($"RandomPlaylistUI: SongChanged event - '{song.SongName}' ({currentIndex + 1}/{totalCount})");
            SessionStatus = $"Now playing: {song.SongName} ({currentIndex + 1}/{totalCount})";
        }

        private void OnSongFailed(SongInfo song, string reason)
        {
            Plugin.Log.Warn($"RandomPlaylistUI: SongFailed event - '{song.SongName}': {reason}");
            SessionStatus = $"Skipped: {song.SongName} ({reason})";
        }

        #endregion

        #region 会话进度协程

        private void StartSessionUpdateCoroutine()
        {
            StopSessionUpdateCoroutine();
            _sessionUpdateCoroutine = StartCoroutine(SessionUpdateRoutine());
        }

        private void StopSessionUpdateCoroutine()
        {
            if (_sessionUpdateCoroutine != null)
            {
                StopCoroutine(_sessionUpdateCoroutine);
                _sessionUpdateCoroutine = null;
            }
        }

        private IEnumerator SessionUpdateRoutine()
        {
            var wait = new WaitForSeconds(1f);
            while (true)
            {
                yield return wait;
                if (_playSessionManager.IsSessionActive)
                {
                    var session = _playSessionManager.GetCurrentSession();
                    var elapsed = TimeSpan.FromMinutes(session.ElapsedMinutes);
                    var currentSong = _playSessionManager.CurrentSong;
                    string songName = currentSong?.SongName ?? "—";
                    SessionStatus = $"▶ {songName} | {session.CurrentSongIndex + 1}/{session.TotalSongs} | {elapsed.Hours:D2}:{elapsed.Minutes:D2} elapsed";
                }
            }
        }

        #endregion

        [UIAction("on-playlist-click")]
        public void OnPlaylistClick(TableView tableView, int index)
        {
            if (index < 0 || index >= _playlistManager.Playlists.Count)
                return;

            var playlist = _playlistManager.Playlists[index];
            _playlistManager.TogglePlaylistSelection(playlist.Id);
            UpdateEstimates();
            RefreshPlaylistCell(index);
        }

        [UIAction("#post-parse")]
        public void PostParse()
        {
            Plugin.Log.Info("RandomPlaylistUI: PostParse called");

            if (_playlistList != null)
            {
                Plugin.Log.Info($"RandomPlaylistUI: _playlistList.TableView = {_playlistList.TableView}");
                Plugin.Log.Info($"RandomPlaylistUI: _playlistList.Data = {_playlistList.Data}");
            }
            else
            {
                Plugin.Log.Warn("RandomPlaylistUI: _playlistList is null in PostParse");
            }

            _playlistManager.LoadPlaylistsAsync();
            RefreshPlaylistList();
        }


        [UIAction("on-duration-change")]
        public void OnDurationChange(float value)
        {
            SelectedDuration = (int)value;
            Plugin.Log.Info($"[DEBUG] on-duration-change triggered! value={value}");
        }

        [UIAction("on-no-fail-change")]
        public void OnNoFailChange(bool value)
        {
            Plugin.Log.Info($"[DEBUG] No Fail changed: {value}");
            NoFailEnabled = value;
        }

        [UIAction("toggle-no-fail")]
        public void ToggleNoFail()
        {
            NoFailEnabled = !NoFailEnabled;
            Plugin.Log.Info($"[DEBUG] No Fail toggled: {NoFailEnabled}");
        }

        [UIAction("toggle-hud")]
        public void ToggleHud()
        {
            HudEnabled = !HudEnabled;
            // 立即同步到 PlaySessionManager，游戏场景中的 HUD 视图会读取这个值
            _playSessionManager.HudEnabled = HudEnabled;
            Plugin.Log.Info($"[DEBUG] HUD toggled: {HudEnabled}");
        }

        [UIAction("nps-any")]
        public void SetNpsAny()
        {
            MinNPS = 0f;
            MaxNPS = 99f;
            Plugin.Log.Info("[DEBUG] NPS preset: Any");
        }

        [UIAction("nps-38")]
        public void SetNps38()
        {
            MinNPS = 3f;
            MaxNPS = 8f;
            Plugin.Log.Info("[DEBUG] NPS preset: 3-8");
        }

        [UIAction("nps-8plus")]
        public void SetNpsFast()
        {
            MinNPS = 8f;
            MaxNPS = 99f;
            Plugin.Log.Info("[DEBUG] NPS preset: 8+");
        }

        [UIAction("abs-standard")]
        public void SetAutoBSStandard()
        {
            AutoBSMode = "Standard";
            _playSessionManager.AutoBSCharacteristic = ModeToCharacteristic(AutoBSMode);
            Plugin.Log.Info($"AutoBS mode -> Standard (characteristic='{_playSessionManager.AutoBSCharacteristic}')");
        }

        [UIAction("abs-45")]
        public void SetAutoBS45()
        {
            AutoBSMode = "45°";
            _autoBSSwingRange = 45;
            _playSessionManager.AutoBSCharacteristic = ModeToCharacteristic(AutoBSMode);
            ApplyAutoBSSwingRange(_autoBSSwingRange);
            Plugin.Log.Info($"AutoBS mode -> 45° (characteristic='{_playSessionManager.AutoBSCharacteristic}', swing={_autoBSSwingRange}°)");
        }

        [UIAction("abs-60")]
        public void SetAutoBS60()
        {
            AutoBSMode = "60°";
            _autoBSSwingRange = 60;
            _playSessionManager.AutoBSCharacteristic = ModeToCharacteristic(AutoBSMode);
            ApplyAutoBSSwingRange(_autoBSSwingRange);
            Plugin.Log.Info($"AutoBS mode -> 60° (characteristic='{_playSessionManager.AutoBSCharacteristic}', swing={_autoBSSwingRange}°)");
        }

        [UIAction("abs-90")]
        public void SetAutoBS90()
        {
            AutoBSMode = "90°";
            _autoBSSwingRange = 90;
            _playSessionManager.AutoBSCharacteristic = ModeToCharacteristic(AutoBSMode);
            ApplyAutoBSSwingRange(_autoBSSwingRange);
            Plugin.Log.Info($"AutoBS mode -> 90° (characteristic='{_playSessionManager.AutoBSCharacteristic}', swing={_autoBSSwingRange}°)");
        }

        [UIAction("abs-360")]
        public void SetAutoBS360()
        {
            AutoBSMode = "360°";
            _playSessionManager.AutoBSCharacteristic = ModeToCharacteristic(AutoBSMode);
            Plugin.Log.Info($"AutoBS mode -> 360° (characteristic='{_playSessionManager.AutoBSCharacteristic}')");
        }

        [UIAction("select-all")]
        public void SelectAllPlaylists()
        {
            _playlistManager.SelectAllPlaylists();
            RefreshPlaylistList();
            UpdateEstimates();
        }

        [UIAction("deselect-all")]
        public void DeselectAllPlaylists()
        {
            _playlistManager.DeselectAllPlaylists();
            RefreshPlaylistList();
            UpdateEstimates();
        }

        [UIAction("start-session")]
        public void StartSession()
        {
            if (_selectedDuration < 1)
                return;

            var selected = _playlistManager.GetSelectedPlaylists();
            if (selected.Count == 0)
            {
                SessionStatus = "Please select at least one playlist!";
                return;
            }

            // 传递 NPS 范围到 PlaySessionManager
            _playSessionManager.MinNPS = MinNPS;
            _playSessionManager.MaxNPS = MaxNPS;
            _playSessionManager.NoFailEnabled = NoFailEnabled;
            _playSessionManager.HudEnabled = HudEnabled;
            // 传递 AutoBS 特征模式（Standard/90°/360°）到 PlaySessionManager
            _playSessionManager.AutoBSCharacteristic = ModeToCharacteristic(AutoBSMode);

            SessionStatus = $"Starting session ({_selectedDuration} min) | No Fail: {(NoFailEnabled ? "ON" : "OFF")}";
            _playSessionManager.StartSession(new SessionSettings
            {
                DurationMinutes = _selectedDuration,
                MinNps = MinNPS,
                MaxNps = MaxNPS,
                NoFailEnabled = NoFailEnabled
            });

            if (_playSessionManager.IsSessionActive)
            {
                StartSessionUpdateCoroutine();
            }
            else
            {
                SessionStatus = "Failed to start session - no playable songs found";
            }
        }

        [UIAction("end-session")]
        public void EndSession()
        {
            _playSessionManager.EndSession();
            StopSessionUpdateCoroutine();
            UpdateEstimates();
        }

        /// <summary>
        /// 增量刷新：仅更新指定索引的单元格，保留滚动位置
        /// 策略：使用 TableView.ReloadDataKeepingPosition() 保持滚动位置
        /// </summary>
        private void RefreshPlaylistCell(int index)
        {
            if (_playlistList == null || _playlistList.TableView == null)
                return;

            if (index < 0 || index >= _playlistManager.Playlists.Count)
                return;

            var playlist = _playlistManager.Playlists[index];

            // 更新 Data 中的对应项
            if (index < _playlistList.Data.Count)
            {
                string prefix = playlist.Selected ? "✓ " : "○ ";
                string subtext = playlist.Selected
                    ? $"✓ {playlist.PlayableSongCount}/{playlist.SongCount} songs"
                    : $"{playlist.PlayableSongCount}/{playlist.SongCount} songs";

                _playlistList.Data[index] = new CustomListTableData.CustomCellInfo(
                    $"{prefix}{playlist.Name}",
                    subtext,
                    null
                );

                // 使用 ReloadDataKeepingPosition：HMUI 公开 API，刷新数据但保留滚动位置
                // 避免 ReloadData() 引起的 scrollPosition 重置到 0
                _playlistList.TableView.ReloadDataKeepingPosition();
            }
        }

        /// <summary>
        /// 全量刷新：重建所有列表数据，保留滚动位置
        /// </summary>
        private void RefreshPlaylistList()
        {
            Plugin.Log.Info("RandomPlaylistUI: RefreshPlaylistList called");

            if (_playlistList == null)
            {
                Plugin.Log.Warn("RandomPlaylistUI: _playlistList is null");
                return;
            }

            if (_playlistList.TableView == null)
            {
                Plugin.Log.Warn("RandomPlaylistUI: _playlistList.TableView is null");
                return;
            }

            Plugin.Log.Info($"RandomPlaylistUI: TableView exists, Data count before clear: {_playlistList.Data?.Count ?? 0}");

            // 保存当前滚动位置（仅在数据量未变化时有效，data 重建用 ReloadDataKeepingPosition）
            _playlistList.Data?.Clear();

            if (_playlistManager.Playlists == null)
            {
                Plugin.Log.Warn("RandomPlaylistUI: Playlists is null");
                _playlistList.TableView.ReloadData();
                return;
            }

            Plugin.Log.Info($"RandomPlaylistUI: Adding {_playlistManager.Playlists.Count} playlists");

            foreach (var playlist in _playlistManager.Playlists)
            {
                string prefix = playlist.Selected ? "✓ " : "○ ";
                string subtext = playlist.Selected
                    ? $"✓ {playlist.PlayableSongCount}/{playlist.SongCount} songs"
                    : $"{playlist.PlayableSongCount}/{playlist.SongCount} songs";

                _playlistList.Data.Add(new CustomListTableData.CustomCellInfo(
                    $"{prefix}{playlist.Name}",
                    subtext,
                    null
                ));
            }

            // 使用 ReloadDataKeepingPosition：HMUI 公开 API，刷新数据但保留滚动位置
            _playlistList.TableView.ReloadDataKeepingPosition();

            Plugin.Log.Info($"RandomPlaylistUI: Refreshed playlist list with {_playlistList.Data.Count} items (keeping scroll position)");
        }

        private void UpdateEstimates()
        {
            var selectedPlaylists = _playlistManager.GetSelectedPlaylists();
            int selectedCount = selectedPlaylists.Count;

            // 刷新顶部已选数量显示
            NotifyPropertyChanged(nameof(SelectedPlaylistCountText));

            if (selectedCount == 0)
            {
                EstimatedInfo = "~0 songs | 00:00";
                SelectedInfo = "No playlists selected";
                return;
            }

            int totalPlayable = selectedPlaylists.Sum(p => p.PlayableSongCount);

            SelectedInfo = $"{selectedCount} playlists ({totalPlayable} songs) | NPS {MinNPS:F1}-{MaxNPS:F1}";

            // NPS 过滤后估算（NPS<0=未知，始终通过）
            var allSongs = _playlistManager.GetSongsFromSelectedPlaylists()
                .Where(s => s.NPS < 0f || (s.NPS >= MinNPS && s.NPS <= MaxNPS)).ToList();
            int estimatedCount = _songSelector.CalculateEstimatedSongCount(allSongs, _selectedDuration);
            int songCount = Math.Min(estimatedCount, allSongs.Count);

            int totalDuration = allSongs.Sum(s => s.Duration);
            var timeSpan = TimeSpan.FromSeconds(Math.Min(totalDuration, _selectedDuration * 60));
            EstimatedInfo = $"~{songCount} songs | {timeSpan.Hours:D2}:{timeSpan.Minutes:D2}";
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

            Plugin.Log.Info($"RandomPlaylistUI activated: firstActivation={firstActivation}, addedToHierarchy={addedToHierarchy}");

            if (addedToHierarchy)
            {
                RefreshPlaylistList();
            }

            // 如果会话活跃，恢复进度显示并启动更新协程
            if (_playSessionManager.IsSessionActive)
            {
                var session = _playSessionManager.GetCurrentSession();
                var currentSong = _playSessionManager.CurrentSong;
                string songName = currentSong?.SongName ?? "—";
                var elapsed = TimeSpan.FromMinutes(session.ElapsedMinutes);
                SessionStatus = $"▶ {songName} | {session.CurrentSongIndex + 1}/{session.TotalSongs} | {elapsed.Hours:D2}:{elapsed.Minutes:D2} elapsed";
                StartSessionUpdateCoroutine();
            }
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
            // 离开 UI 时停止协程（会话仍在后台运行）
            StopSessionUpdateCoroutine();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            // 取消事件订阅，防止内存泄漏
            if (_playSessionManager != null)
            {
                _playSessionManager.SessionStarted -= OnSessionStarted;
                _playSessionManager.SessionEnded -= OnSessionEnded;
                _playSessionManager.SongChanged -= OnSongChanged;
                _playSessionManager.SongFailed -= OnSongFailed;
            }

            StopSessionUpdateCoroutine();
        }
    }
}

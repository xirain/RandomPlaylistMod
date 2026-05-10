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

        private Coroutine _sessionUpdateCoroutine;

        [UIComponent("playlist-list")]
        private CustomListTableData _playlistList;

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
            Plugin.Log.Info($"[DEBUG] on-duration-change triggered! value={value}");
        }

        [UIAction("nps-any")]
        public void SetNpsAny()
        {
            MinNPS = 0f;
            MaxNPS = 99f;
            Plugin.Log.Info("[DEBUG] NPS preset: Any");
        }

        [UIAction("nps-relax")]
        public void SetNpsRelax()
        {
            MinNPS = 0f;
            MaxNPS = 6f;
            Plugin.Log.Info("[DEBUG] NPS preset: <6 (Relax)");
        }

        [UIAction("nps-mid")]
        public void SetNpsMid()
        {
            MinNPS = 6f;
            MaxNPS = 9f;
            Plugin.Log.Info("[DEBUG] NPS preset: 6-9 (Hard/Expert)");
        }

        [UIAction("nps-fast")]
        public void SetNpsFast()
        {
            MinNPS = 9f;
            MaxNPS = 99f;
            Plugin.Log.Info("[DEBUG] NPS preset: 9+ (Expert+)");
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

            SessionStatus = $"Starting session ({_selectedDuration} min)...";
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
        /// 增量刷新：仅更新指定索引的单元格
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

                // 仅刷新可见行而非全量 ReloadData
                _playlistList.TableView.ReloadData();
            }
        }

        /// <summary>
        /// 全量刷新：重建所有列表数据
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

            _playlistList.TableView.ReloadData();
            Plugin.Log.Info($"RandomPlaylistUI: Refreshed playlist list with {_playlistList.Data.Count} items");
        }

        private void UpdateEstimates()
        {
            var selectedPlaylists = _playlistManager.GetSelectedPlaylists();
            int selectedCount = selectedPlaylists.Count;

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

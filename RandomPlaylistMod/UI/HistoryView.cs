using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using RandomPlaylistMod.Managers;
using RandomPlaylistMod.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Zenject;

namespace RandomPlaylistMod.UI
{
    /// <summary>
    /// 历史记录浏览面板 — Phase 2
    /// 展示最近的会话记录列表，支持查看详情、生成分享图、删除
    /// </summary>
    [ViewDefinition("RandomPlaylistMod.UI.Views.HistoryView.bsml")]
    public class HistoryView : BSMLAutomaticViewController
    {
        private HistoryManager _historyManager;
        private ShareImageGenerator _shareImageGenerator;
        private RandomPlaylistFlowCoordinator _flowCoordinator;

        // 缓存数据
        private List<SessionRecord> _allSessions = new List<SessionRecord>();
        private PlayerProfile _profile;
        private int _selectedIndex = -1;   // 当前选中条目的索引

        // ---- BSML 绑定属性 ----

        [UIValue("profile-summary-text")]
        public string ProfileSummaryText
        {
            get
            {
                if (_profile == null) return "";
                var lastStr = _profile.LastPlayedAt != default
                    ? $"Last: {_profile.LastPlayedAt:MM/dd HH:mm}"
                    : "";
                var bestStr = !string.IsNullOrEmpty(_profile.HighestRank)
                    ? $"Best: {_profile.HighestRank}"
                    : "";
                return $"{lastStr}  {bestStr}";
            }
        }

        [UIValue("visible-profile")]
        public bool VisibleProfile => _profile != null && _profile.TotalSessions > 0;

        [UIValue("profile-sessions")]
        public string ProfileSessions => _profile?.TotalSessions.ToString() ?? "0";

        [UIValue("profile-time")]
        public string ProfileTime => _profile != null
            ? $"{_profile.TotalPlayTimeMin / 60}h {_profile.TotalPlayTimeMin % 60}m"
            : "0h";

        [UIValue("profile-streak")]
        public string ProfileStreak => $"{_profile?.DailyStreak ?? 0} days";

        [UIValue("list-header")]
        public string ListHeader => _allSessions.Count > 0
            ? $"Recent ({Math.Min(_allSessions.Count, 10)} of {_allSessions.Count}):"
            : "No session history yet";

        [UIValue("history-list-text")]
        public string HistoryListText
        {
            get
            {
                var items = _allSessions.Take(10).ToList();
                if (items.Count == 0)
                    return "Play some random sessions to build your history! 🎮";

                var lines = new List<string>();
                for (int i = 0; i < items.Count; i++)
                {
                    var s = items[i];
                    var marker = i == _selectedIndex ? "▶ " : "  ";
                    var idShort = s.SessionId?.Length > 17 ? s.SessionId.Substring(9, 8) : (s.SessionId ?? "");
                    var dateStr = s.StartedAt.ToString("MM/dd HH:mm");
                    var durStr = $"{s.ActualDurationMin}min";
                    var songStr = $"{s.TotalSongsPlayed}songs";
                    var scoreStr = $"{s.ExerciseSummary.TotalScore / 1000}K";
                    var rankStr = string.IsNullOrEmpty(s.ExerciseSummary.BestRank)
                        ? "-" : s.ExerciseSummary.BestRank;

                    lines.Add($"{marker}[{idShort}] {dateStr}  {durStr}  {songStr}  {scoreStr}  ★{rankStr}");
                }

                // 提示
                if (_allSessions.Count > 10)
                    lines.Add($"  ... and {_allSessions.Count - 10} more sessions");

                return string.Join("\n", lines);
            }
        }

        [UIValue("visible-detail")]
        public bool VisibleDetail => _selectedIndex >= 0 && _selectedIndex < _allSessions.Count;

        [UIValue("detail-title-text")]
        public string DetailTitleText
        {
            get
            {
                if (!VisibleDetail) return "";
                var s = _allSessions[_selectedIndex];
                return $"Session: {s.StartedAt:yyyy/MM/dd HH:mm}";
            }
        }

        [UIValue("detail-content-text")]
        public string DetailContentText
        {
            get
            {
                if (!VisibleDetail) return "";
                var s = _allSessions[_selectedIndex];
                var lines = new List<string>
                {
                    $"Duration: {s.ActualDurationMin} min (target: {s.TargetDurationMin} min)",
                    $"Songs: {s.TotalSongsPlayed} played / {s.TotalSongsInQueue} queued",
                    $"Score: {s.ExerciseSummary.TotalScore:N0}",
                    $"Accuracy: {s.ExerciseSummary.AverageAccuracy:F1}%",
                    $"Best Rank: {s.ExerciseSummary.BestRank ?? "-"}",
                    $"Full Combos: {s.ExerciseSummary.FullComboCount}",
                    $"Playlists: {string.Join(", ", (s.PlaylistNames ?? new List<string>()).Take(5))}",
                    $"NPS: {(s.Settings.NpsAny ? "Any" : (s.Settings.NpsBandLabels != null && s.Settings.NpsBandLabels.Count > 0 ? string.Join(" / ", s.Settings.NpsBandLabels) : $"{s.Settings.MinNPS:F1} ~ {s.Settings.MaxNPS:F1}"))}",
                    $"NoFail: {(s.Settings.NoFailEnabled ? "ON" : "OFF")}"
                };
                return string.Join("\n", lines);
            }
        }

        [UIValue("selection-indicator-text")]
        public string SelectionIndicatorText
        {
            get
            {
                if (_allSessions.Count == 0) return "";
                if (_selectedIndex < 0 || _selectedIndex >= _allSessions.Count)
                    return "Click Prev/Next to select";
                return $"{_selectedIndex + 1} / {Math.Min(_allSessions.Count, 10)}";
            }
        }

        [UIValue("status-text")]
        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                NotifyPropertyChanged();
            }
        }
        private string _statusText = "";

        // ---- DI ----

        [Inject]
        public void Construct(
            HistoryManager historyManager,
            ShareImageGenerator shareImageGenerator,
            RandomPlaylistFlowCoordinator flowCoordinator)
        {
            _historyManager = historyManager;
            _shareImageGenerator = shareImageGenerator;
            _flowCoordinator = flowCoordinator;
        }

        // ---- 生命周期 ----

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            if (firstActivation || addedToHierarchy)
            {
                RefreshData();
            }
        }

        /// <summary>
        /// 从磁盘重新加载数据并刷新 UI
        /// </summary>
        public void RefreshData()
        {
            try
            {
                _allSessions = _historyManager.LoadAllSessions() ?? new List<SessionRecord>();
                _profile = _historyManager.LoadProfile();
                _selectedIndex = -1;

                RefreshAllBindings();
                Plugin.Log.Info($"HistoryView: Loaded {_allSessions.Count} sessions");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"HistoryView: Failed to load data: {ex.Message}");
                StatusText = "Failed to load history";
            }
        }

        private void RefreshAllBindings()
        {
            NotifyPropertyChanged(nameof(ProfileSummaryText));
            NotifyPropertyChanged(nameof(VisibleProfile));
            NotifyPropertyChanged(nameof(ProfileSessions));
            NotifyPropertyChanged(nameof(ProfileTime));
            NotifyPropertyChanged(nameof(ProfileStreak));
            NotifyPropertyChanged(nameof(ListHeader));
            NotifyPropertyChanged(nameof(HistoryListText));
            NotifyPropertyChanged(nameof(VisibleDetail));
            NotifyPropertyChanged(nameof(DetailTitleText));
            NotifyPropertyChanged(nameof(DetailContentText));
        }

        // ---- 用户操作 ----

        /// <summary>
        /// 通过条目索引选择一条会话（由外部或内部触发）
        /// </summary>
        public void SelectSession(int index)
        {
            if (index < 0 || index >= _allSessions.Count)
            {
                _selectedIndex = -1;
            }
            else
            {
                _selectedIndex = index;
            }

            NotifyPropertyChanged(nameof(HistoryListText));
            NotifyPropertyChanged(nameof(VisibleDetail));
            NotifyPropertyChanged(nameof(DetailTitleText));
            NotifyPropertyChanged(nameof(DetailContentText));
            NotifyPropertyChanged(nameof(SelectionIndicatorText));
            StatusText = "";
        }

        /// <summary>
        /// 选中上一条
        /// </summary>
        [UIAction("select-prev")]
        public void SelectPrev()
        {
            if (_allSessions.Count == 0) return;
            int newIndex = _selectedIndex <= 0 ? _allSessions.Count - 1 : _selectedIndex - 1;
            SelectSession(newIndex);
        }

        /// <summary>
        /// 选中下一条
        /// </summary>
        [UIAction("select-next")]
        public void SelectNext()
        {
            if (_allSessions.Count == 0) return;
            int newIndex = _selectedIndex >= _allSessions.Count - 1 ? 0 : _selectedIndex + 1;
            SelectSession(newIndex);
        }

        /// <summary>
        /// 为选中的会话生成分享图
        /// </summary>
        [UIAction("share-selected")]
        public void ShareSelected()
        {
            if (!VisibleDetail)
            {
                StatusText = "Select a session first";
                return;
            }

            try
            {
                var record = _allSessions[_selectedIndex];
                var htmlPath = _shareImageGenerator.GenerateShareHtml(record);
                if (!string.IsNullOrEmpty(htmlPath))
                {
                    StatusText = $"Share HTML saved! {System.IO.Path.GetFileName(htmlPath)}";
                    Plugin.Log.Info($"HistoryView: Share HTML generated at {htmlPath}");
                }
                else
                {
                    StatusText = "Failed to generate share";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
                Plugin.Log.Error($"HistoryView: Share failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 删除选中的会话
        /// </summary>
        [UIAction("delete-selected")]
        public void DeleteSelected()
        {
            if (!VisibleDetail)
            {
                StatusText = "Select a session first";
                return;
            }

            try
            {
                var record = _allSessions[_selectedIndex];
                bool ok = _historyManager.DeleteSession(record.SessionId);
                if (ok)
                {
                    StatusText = $"Deleted session {record.SessionId?.Substring(0, Math.Min(8, record.SessionId?.Length ?? 0))}";
                    Plugin.Log.Info($"HistoryView: Deleted session '{record.SessionId}'");
                    RefreshData();
                }
                else
                {
                    StatusText = "Delete failed";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
                Plugin.Log.Error($"HistoryView: Delete failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 返回总结面板（关闭自身）
        /// </summary>
        [UIAction("go-back")]
        public void GoBack()
        {
            try
            {
                _flowCoordinator.DismissHistoryView();
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"HistoryView: GoBack failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 关闭整个面板
        /// </summary>
        [UIAction("close-view")]
        public void CloseView()
        {
            try
            {
                _flowCoordinator.DismissHistoryView();
                _flowCoordinator.DismissSummaryView();
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"HistoryView: CloseView failed: {ex.Message}");
            }
        }
    }
}

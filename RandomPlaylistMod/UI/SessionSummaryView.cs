using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using RandomPlaylistMod.Managers;
using RandomPlaylistMod.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

namespace RandomPlaylistMod.UI
{
    /// <summary>
    /// 会话结束后的总结面板 — Phase 2
    /// 自动弹出，展示本次会话的核心数据
    /// </summary>
    [ViewDefinition("RandomPlaylistMod.UI.Views.SessionSummaryView.bsml")]
    public class SessionSummaryView : BSMLAutomaticViewController
    {
        private PlaySessionManager _playSessionManager;
        private HistoryManager _historyManager;
        private ShareImageGenerator _shareImageGenerator;
        private RandomPlaylistFlowCoordinator _flowCoordinator;
        private MainFlowCoordinator _mainFlowCoordinator;

        // 当前展示的 SessionRecord
        private SessionRecord _currentRecord;

        // BSML 绑定
        [UIValue("summary-title")]
        public string SummaryTitle => "🎵 Session Complete!";

        [UIValue("session-date")]
        public string SessionDate => _currentRecord != null
            ? $"{_currentRecord.StartedAt:yyyy/MM/dd HH:mm}"
            : "";

        [UIValue("duration-text")]
        public string DurationText => _currentRecord != null
            ? $"{_currentRecord.ActualDurationMin} min"
            : "";

        [UIValue("songs-played-text")]
        public string SongsPlayedText => _currentRecord != null
            ? $"{_currentRecord.TotalSongsPlayed}"
            : "";

        [UIValue("total-score-text")]
        public string TotalScoreText => _currentRecord != null
            ? $"{_currentRecord.ExerciseSummary.TotalScore:N0}"
            : "";

        [UIValue("fc-count-text")]
        public string FcCountText => _currentRecord != null
            ? $"{_currentRecord.ExerciseSummary.FullComboCount}"
            : "";

        [UIValue("accuracy-text")]
        public string AccuracyText => _currentRecord != null
            ? $"{_currentRecord.ExerciseSummary.AverageAccuracy:F1}%"
            : "";

        [UIValue("best-rank-text")]
        public string BestRankText => _currentRecord != null
            ? _currentRecord.ExerciseSummary.BestRank
            : "";

        [UIValue("playlists-text")]
        public string PlaylistsText => _currentRecord != null
            ? string.Join(", ", (_currentRecord.PlaylistNames ?? new List<string>()).Take(3))
            : "";

        [UIValue("share-status")]
        public string ShareStatus
        {
            get => _shareStatus;
            set
            {
                _shareStatus = value;
                NotifyPropertyChanged();
            }
        }
        private string _shareStatus = "";

        [UIValue("visible-share-status")]
        public bool VisibleShareStatus => !string.IsNullOrEmpty(ShareStatus);

        [Inject]
        public void Construct(
            PlaySessionManager playSessionManager,
            HistoryManager historyManager,
            ShareImageGenerator shareImageGenerator,
            RandomPlaylistFlowCoordinator flowCoordinator,
            MainFlowCoordinator mainFlowCoordinator)
        {
            _playSessionManager = playSessionManager;
            _historyManager = historyManager;
            _shareImageGenerator = shareImageGenerator;
            _flowCoordinator = flowCoordinator;
            _mainFlowCoordinator = mainFlowCoordinator;

            // 订阅 SessionEndedWithRecord 事件
            _playSessionManager.SessionEndedWithRecord += OnSessionEndedWithRecord;
        }

        /// <summary>
        /// 使用 SessionRecord 设置面板数据
        /// </summary>
        public void SetSessionRecord(SessionRecord record)
        {
            _currentRecord = record;
            // 使用无参 NotifyPropertyChanged 刷新所有绑定，
            // 避免 propertyName 与 BSML 的 UIValue key 不匹配导致刷新失效
            NotifyPropertyChanged();
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            // 视图解析完成后强制刷新一次：保证即便 SetSessionRecord 在 Parse 之前调用，
            // 数据也能在视图就绪后正确显示
            if (_currentRecord != null)
            {
                NotifyPropertyChanged();
            }
        }

        private void OnSessionEndedWithRecord(PlaySession session, SessionRecord record)
        {
            if (record == null) return;

            _currentRecord = record;
            SetSessionRecord(record);

            // 自动弹出总结面板。
            // 注意：session 结束时 RPM 的 FlowCoordinator 很可能已被游戏 deactivate（不在层级里），
            // 此时直接在它上面 PresentViewController 会失败/层级错乱（页面看不到且无法回退）。
            // 因此先确保 RPM FlowCoordinator 已重新呈现到 MainFlowCoordinator 层级，再 show summary。
            try
            {
                if (!_flowCoordinator.isActivated)
                {
                    Plugin.Log.Info("SessionSummaryView: RPM FlowCoordinator not active at session end, re-presenting it...");
                    _mainFlowCoordinator.PresentFlowCoordinator(_flowCoordinator);
                }
                _flowCoordinator.ShowSummaryView(this);
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"SessionSummaryView: Failed to auto-show: {ex.Message}");
            }
        }

        [UIAction("generate-share")]
        public void GenerateShareImage()
        {
            if (_currentRecord == null)
            {
                ShareStatus = "No session data available";
                return;
            }

            try
            {
                var htmlPath = _shareImageGenerator.GenerateShareHtml(_currentRecord);
                if (!string.IsNullOrEmpty(htmlPath))
                {
                    ShareStatus = $"Share image saved!\n{htmlPath}";
                    Plugin.Log.Info($"SessionSummaryView: Share HTML generated at {htmlPath}");
                }
                else
                {
                    ShareStatus = "Failed to generate share image";
                }
            }
            catch (Exception ex)
            {
                ShareStatus = $"Error: {ex.Message}";
                Plugin.Log.Error($"SessionSummaryView: Generate share failed: {ex.Message}");
            }

            NotifyPropertyChanged(nameof(ShareStatus));
            NotifyPropertyChanged(nameof(VisibleShareStatus));
        }

        [UIAction("view-history")]
        public void ViewHistory()
        {
            Plugin.Log.Info("SessionSummaryView: View History requested");
            try
            {
                _flowCoordinator.ShowHistoryView();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"SessionSummaryView: Failed to show history: {ex.Message}");
                ShareStatus = "Failed to open history";
                NotifyPropertyChanged(nameof(ShareStatus));
                NotifyPropertyChanged(nameof(VisibleShareStatus));
            }
        }

        [UIAction("close-view")]
        public void CloseView()
        {
            try
            {
                _flowCoordinator.DismissSummaryView();
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"SessionSummaryView: Failed to close: {ex.Message}");
            }
        }

        protected override void OnDestroy()
        {
            if (_playSessionManager != null)
            {
                _playSessionManager.SessionEndedWithRecord -= OnSessionEndedWithRecord;
            }
            base.OnDestroy();
        }
    }
}

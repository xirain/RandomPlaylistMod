using HMUI;
using Zenject;
using UnityEngine;
using BeatSaberMarkupLanguage;

namespace RandomPlaylistMod.UI
{
    public class RandomPlaylistFlowCoordinator : FlowCoordinator
    {
        private RandomPlaylistUI _randomPlaylistUI;
        private SessionSummaryView _sessionSummaryView;
        private HistoryView _historyView;
        private MainFlowCoordinator _mainFlowCoordinator;
        private bool _isPresented;
        private ViewController _previousTopViewController;

        [Inject]
        public void Construct(
            RandomPlaylistUI randomPlaylistUI,
            SessionSummaryView sessionSummaryView,
            HistoryView historyView,
            MainFlowCoordinator mainFlowCoordinator)
        {
            _randomPlaylistUI = randomPlaylistUI;
            _sessionSummaryView = sessionSummaryView;
            _historyView = historyView;
            _mainFlowCoordinator = mainFlowCoordinator;
            Plugin.Log.Info("RandomPlaylistFlowCoordinator: Dependencies injected");
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            Plugin.Log.Info($"RandomPlaylistFlowCoordinator: DidActivate firstActivation={firstActivation}, addedToHierarchy={addedToHierarchy}, screenSystemEnabling={screenSystemEnabling}");
            
            if (firstActivation)
            {
                SetTitle("Random Playlist");
                showBackButton = true;
            }

            if (addedToHierarchy || _isPresented)
            {
                if (_randomPlaylistUI != null)
                {
                    ProvideInitialViewControllers(_randomPlaylistUI);
                }
                else
                {
                    Plugin.Log.Error("RandomPlaylistFlowCoordinator: _randomPlaylistUI is NULL!");
                }
            }
            
            _isPresented = true;
        }

        /// <summary>
        /// 在会话结束后弹出总结面板
        /// </summary>
        public void ShowSummaryView(SessionSummaryView view)
        {
            try
            {
                _previousTopViewController = topViewController;
                PresentViewController(view, immediately: true);
                Plugin.Log.Info("RandomPlaylistFlowCoordinator: Summary view presented");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.Error($"RandomPlaylistFlowCoordinator: Failed to show summary view: {ex.Message}");
            }
        }

        /// <summary>
        /// 关闭总结面板
        /// </summary>
        public void DismissSummaryView()
        {
            try
            {
                if (_sessionSummaryView != null && _sessionSummaryView.isInViewControllerHierarchy)
                {
                    DismissViewController(_sessionSummaryView, immediately: true);
                    Plugin.Log.Info("RandomPlaylistFlowCoordinator: Summary view dismissed");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.Error($"RandomPlaylistFlowCoordinator: Failed to dismiss summary view: {ex.Message}");
            }
        }

        /// <summary>
        /// Phase 2: 显示历史记录面板
        /// </summary>
        public void ShowHistoryView()
        {
            try
            {
                _historyView.RefreshData();
                PresentViewController(_historyView, immediately: true);
                Plugin.Log.Info("RandomPlaylistFlowCoordinator: History view presented");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.Error($"RandomPlaylistFlowCoordinator: Failed to show history view: {ex.Message}");
            }
        }

        /// <summary>
        /// Phase 2: 关闭历史记录面板
        /// </summary>
        public void DismissHistoryView()
        {
            try
            {
                if (_historyView != null && _historyView.isInViewControllerHierarchy)
                {
                    DismissViewController(_historyView, immediately: true);
                    Plugin.Log.Info("RandomPlaylistFlowCoordinator: History view dismissed");
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.Error($"RandomPlaylistFlowCoordinator: Failed to dismiss history view: {ex.Message}");
            }
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            Plugin.Log.Info($"RandomPlaylistFlowCoordinator: Back button pressed, topVC={topViewController?.GetType().Name}");
            
            // 逐层关闭：先关闭弹出的子面板，最后才关闭 FlowCoordinator 本身
            // 避免直接 DismissFlowCoordinator 导致底层 RandomPlaylistUI 被 deactivate 后协程报错
            
            // 层 3: 历史记录面板 → 回到总结面板
            if (_historyView != null && _historyView.isInViewControllerHierarchy)
            {
                Plugin.Log.Info("RandomPlaylistFlowCoordinator: Dismissing HistoryView (back to SummaryView)");
                DismissViewController(_historyView, immediately: true);
                return;
            }
            
            // 层 2: 总结面板 → 回到主界面
            if (_sessionSummaryView != null && _sessionSummaryView.isInViewControllerHierarchy)
            {
                Plugin.Log.Info("RandomPlaylistFlowCoordinator: Dismissing SessionSummaryView (back to main UI)");
                DismissViewController(_sessionSummaryView, immediately: true);
                return;
            }
            
            // 层 1: 主界面 → 关闭整个 FlowCoordinator
            Plugin.Log.Info("RandomPlaylistFlowCoordinator: Dismissing entire FlowCoordinator");
            if (_mainFlowCoordinator != null)
            {
                _mainFlowCoordinator.DismissFlowCoordinator(this);
            }
            else
            {
                Plugin.Log.Error("RandomPlaylistFlowCoordinator: _mainFlowCoordinator is NULL, trying fallback...");
                BeatSaberUI.MainFlowCoordinator?.DismissFlowCoordinator(this);
            }
        }
    }
}

using System;
using System.Collections;
using UnityEngine;

namespace RandomPlaylistMod.UI
{
    /// <summary>
    /// 常驻协程宿主：用于「游戏关卡中途结束 session」的场景。
    /// 此时主菜单 UI（RandomPlaylistUI）仍处于 inactive 状态，
    /// 立即 PresentFlowCoordinator 会导致 ProvideInitialViewControllers
    /// 在 inactive 的 GameObject 上启动协程失败，层级错乱、无法返回主界面。
    /// 本类在 DontDestroyOnLoad 对象上等待主菜单重新激活后再重新呈现 FC 并弹出总结面板。
    /// </summary>
    public class SummaryPresenter : MonoBehaviour
    {
        private static SummaryPresenter _instance;

        public static SummaryPresenter Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("RandomPlaylistMod.SummaryPresenter");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<SummaryPresenter>();
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 等待 RPM FlowCoordinator 重新激活（游戏切回主菜单）后，再呈现并显示总结面板。
        /// </summary>
        public void ShowSummaryWhenReady(
            RandomPlaylistFlowCoordinator flowCoordinator,
            SessionSummaryView summaryView,
            MainFlowCoordinator mainFlowCoordinator,
            float timeoutSeconds = 30f)
        {
            StartCoroutine(WaitForActivationThenShow(
                flowCoordinator, summaryView, mainFlowCoordinator, timeoutSeconds));
        }

        private IEnumerator WaitForActivationThenShow(
            RandomPlaylistFlowCoordinator fc,
            SessionSummaryView view,
            MainFlowCoordinator main,
            float timeout)
        {
            float elapsed = 0f;

            // 等待 FC 重新激活：游戏切回主菜单时，菜单层级的 GameObject 会被重新激活，
            // isActivated 随之变回 true。
            while (elapsed < timeout &&
                   (fc == null || !fc.gameObject.activeInHierarchy || !fc.isActivated))
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            if (fc == null || view == null)
            {
                Plugin.Log.Error("SummaryPresenter: FC or summary view is null, cannot show summary");
                yield break;
            }

            Exception caught = null;
            if (!fc.isActivated && main != null)
            {
                try
                {
                    Plugin.Log.Info($"SummaryPresenter: Main menu active after {elapsed:F1}s, re-presenting RPM FlowCoordinator...");
                    main.PresentFlowCoordinator(fc);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            }
            if (caught != null)
            {
                Plugin.Log.Warn($"SummaryPresenter: Failed to present flow coordinator: {caught.Message}");
                yield break;
            }

            // 等一帧，让 PresentFlowCoordinator 完成层级挂载后再显示总结
            yield return null;

            try
            {
                fc.ShowSummaryView(view);
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"SummaryPresenter: Failed to show summary after menu activation: {ex.Message}");
            }
        }
    }
}

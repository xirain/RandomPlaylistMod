using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.XR;
using Zenject;
using RandomPlaylistMod.Managers;

namespace RandomPlaylistMod.UI
{
    /// <summary>
    /// 游玩中监听手柄 B 键（OpenXR secondaryButton）：
    /// - 短按：把当前歌曲收藏到 "RandomPlaylist Favorites" 歌单并弹提示
    /// - 长按（约 0.7s）：退出当前随机会话（停止随机播放，之后不再自动放下一首）
    /// 使用 Unity XR Input（OpenXR），对 Pico 串流等任意 OpenXR 运行时通用。
    /// 注：Pico 串流下 A 键被映射为系统菜单键（menuButton），会与系统菜单冲突，
    /// 故收藏/退出均绑定到 B 键，避免占用菜单键。
    /// </summary>
    public class GameplayFavoriteInput : MonoBehaviour
    {
        private FavoriteManager _favoriteManager;
        private PlaySessionManager _playSessionManager;

        [Inject]
        public void Construct(FavoriteManager favoriteManager, PlaySessionManager playSessionManager)
        {
            _favoriteManager = favoriteManager;
            _playSessionManager = playSessionManager;
        }

        private readonly List<InputDevice> _controllers = new List<InputDevice>();
        private bool _wasDown;                 // B 键（secondaryButton）边沿检测
        private float _pressDownTime = -1f;
        private bool _longTriggered;
        private int _releaseFrames;            // 松开防抖计数（过滤 OpenXR 手柄抖动误触发）
        private Coroutine _toastCoroutine;
        private int _lastControllerCount = -1;
        private float _lastDiagTime = -1f;

        private const float LongPressSeconds = 0.7f;
        private const int ReleaseDebounceFrames = 4;  // 约 0.066s，过滤按住期间抖动导致的伪松开/误短按

        private GameObject _toastCanvas;
        private TextMeshProUGUI _toastText;

        private void Start()
        {
            Plugin.Log?.Info("[GameplayFavoriteInput] Start called");
            RefreshControllers();
        }

        private void Update()
        {
            // 每 30 帧刷新一次设备列表（手柄可能在场景加载后才连接）
            if (Time.frameCount % 30 == 0)
            {
                RefreshControllers();
            }

            // 诊断：采样所有常见按键的裸值，定位 Pico 串流下的键位映射
            DiagnoseRawButtons();

            // 应用：检测 B 键（secondaryButton = Touch 手柄 B 右手 / Y 左手）
            bool down = false;
            foreach (var device in _controllers)
            {
                if (device.isValid &&
                    device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool value) &&
                    value)
                {
                    down = true;
                    break;
                }
            }

            // 按下边沿：仅在尚未处于一次按下周期内才启动计时。
            // 关键修复：OpenXR 手柄在按住 B 期间 secondaryButton 状态会抖动（某些帧轮询为 false），
            // 若每次伪边沿都重置 _pressDownTime，长按阈值永远累积不到，长按几乎不触发。
            // 这里只在 _pressDownTime < 0（全新一次按下）时计时，抖动伪边沿不会打断累计。
            if (down && !_wasDown)
            {
                if (_pressDownTime < 0f)
                {
                    _pressDownTime = Time.time;
                    _longTriggered = false;
                    _releaseFrames = 0;
                    Plugin.Log?.Info("[GameplayFavoriteInput] B down (edge)");
                }
            }

            // 长按检测：基于按下边沿累计时间，容忍按住期间的抖动（不要求每帧 down 为 true）。
            if (_pressDownTime >= 0f && !_longTriggered && Time.time - _pressDownTime >= LongPressSeconds)
            {
                _longTriggered = true;
                Plugin.Log?.Info("[GameplayFavoriteInput] B long-press -> exit session");
                ExitSession();
            }

            // 松开防抖：连续 ReleaseDebounceFrames 帧检测到未按下，才视为真正松开
            // （过滤按住期间的单帧抖动，避免误触发短按收藏）。
            if (!down)
            {
                _releaseFrames++;
            }
            else
            {
                _releaseFrames = 0;
            }

            // 短按检测：确认真正松开（防抖）+ 未达长按阈值 → 收藏当前歌曲。
            if (_releaseFrames >= ReleaseDebounceFrames && _pressDownTime >= 0f && !_longTriggered &&
                Time.time - _pressDownTime < LongPressSeconds)
            {
                Plugin.Log?.Info("[GameplayFavoriteInput] B short-press -> favorite");
                OnFavoritePressed();
                _pressDownTime = -1f;
                _releaseFrames = 0;
            }

            // 长按触发后用户松开：重置计时，准备下一次按下。
            if (_longTriggered && !down)
            {
                _pressDownTime = -1f;
                _releaseFrames = 0;
            }

            _wasDown = down;
        }

        /// <summary>
        /// 诊断采样：把当前所有手柄上 secondary/primary/menu/grip/trigger 的裸值打印出来，
        /// 用于确认 Pico 串流等环境下 B 键、A 键究竟映射到哪个 usage。
        /// </summary>
        private void DiagnoseRawButtons()
        {
            var pressed = new List<string>();
            foreach (var device in _controllers)
            {
                if (!device.isValid)
                {
                    continue;
                }

                ProbeUsage(device, CommonUsages.secondaryButton, "secondary(B/Y)", pressed);
                ProbeUsage(device, CommonUsages.primaryButton, "primary(A/X)", pressed);
                ProbeUsage(device, CommonUsages.menuButton, "menu", pressed);
                ProbeUsage(device, CommonUsages.gripButton, "grip", pressed);
                ProbeUsage(device, CommonUsages.triggerButton, "trigger", pressed);
            }

            if (pressed.Count > 0 && Time.time - _lastDiagTime > 0.4f)
            {
                Plugin.Log?.Info($"[GameplayFavoriteInput] Raw buttons down: {string.Join(", ", pressed)}");
                _lastDiagTime = Time.time;
            }
        }

        private void ProbeUsage(InputDevice device, InputFeatureUsage<bool> usage, string label, List<string> sink)
        {
            if (device.TryGetFeatureValue(usage, out bool value) && value)
            {
                sink.Add(label);
            }
        }

        private void RefreshControllers()
        {
            _controllers.Clear();
            var characteristics = InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller;
            InputDevices.GetDevicesWithCharacteristics(characteristics, _controllers);

            if (_controllers.Count == 0)
            {
                InputDevices.GetDevices(_controllers);
            }

            if (_controllers.Count != _lastControllerCount)
            {
                _lastControllerCount = _controllers.Count;
                var names = string.Join(", ", _controllers.Select(d => $"{d.name}[{(d.isValid ? "valid" : "invalid")}]"));
                Plugin.Log?.Info($"[GameplayFavoriteInput] Controllers refreshed: count={_controllers.Count} devices=[{names}]");
            }
        }

        private void OnFavoritePressed()
        {
            if (_favoriteManager == null)
            {
                Plugin.Log?.Warn("[GameplayFavoriteInput] FavoriteManager is null, cannot save");
                return;
            }

            Plugin.Log?.Info("[GameplayFavoriteInput] Invoking SaveCurrentSong");
            var result = _favoriteManager.SaveCurrentSong();
            ShowToast(result);
        }

        private void ExitSession()
        {
            if (_playSessionManager == null)
            {
                Plugin.Log?.Warn("[GameplayFavoriteInput] PlaySessionManager is null, cannot exit");
                ShowToast("退出失败：会话管理器为空", Color.red);
                return;
            }

            if (!_playSessionManager.IsSessionActive)
            {
                ShowToast("当前没有进行中的随机会话", new Color(1f, 0.6f, 0.6f));
                return;
            }

            Plugin.Log?.Info("[GameplayFavoriteInput] Exiting session (EndSession)");
            _playSessionManager.EndSession();
            ShowToast("已退出随机会话", new Color(0.7f, 0.9f, 1f));
        }

        private void ShowToast(FavoriteResult result)
        {
            string title;
            Color color;
            switch (result.Status)
            {
                case FavoriteStatus.Added:
                    title = $"★ 已收藏\n{result.Song?.SongName ?? ""}";
                    color = new Color(1f, 0.85f, 0.2f);
                    break;
                case FavoriteStatus.AlreadyInPlaylist:
                    title = $"已在收藏歌单\n{result.Song?.SongName ?? ""}";
                    color = new Color(0.7f, 0.9f, 1f);
                    break;
                case FavoriteStatus.NoCurrentSong:
                    title = "当前没有可收藏的歌曲";
                    color = new Color(1f, 0.6f, 0.6f);
                    break;
                default:
                    title = "收藏失败";
                    color = Color.red;
                    break;
            }

            ShowToast(title, color);
        }

        private void ShowToast(string text, Color color)
        {
            EnsureToast();
            if (_toastCanvas == null || _toastText == null)
            {
                return;
            }

            _toastText.text = text;
            _toastText.color = color;
            _toastCanvas.SetActive(true);

            if (_toastCoroutine != null)
            {
                StopCoroutine(_toastCoroutine);
            }

            _toastCoroutine = StartCoroutine(HideToastAfter(1.8f));
        }

        private void EnsureToast()
        {
            if (_toastCanvas != null)
            {
                return;
            }

            // 使用 ScreenSpaceOverlay（与 Beat Saber 的 HUD 同一渲染层），并设置较高的
            // sortingOrder，确保收藏/退出提示始终显示在 HUD 之上，不会被游戏内 UI 遮挡。
            // 不依赖 Camera.main，避免 gameplay 场景下相机获取不到导致提示消失。
            var canvasGO = new GameObject("RandomPlaylistFavoriteToast");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var textGO = new GameObject("ToastText");
            textGO.transform.SetParent(canvasGO.transform, false);
            var text = textGO.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 48f;
            text.rectTransform.anchorMin = new Vector2(0.5f, 0.72f);
            text.rectTransform.anchorMax = new Vector2(0.5f, 0.72f);
            text.rectTransform.sizeDelta = new Vector2(900f, 140f);
            text.rectTransform.anchoredPosition = Vector2.zero;

            _toastText = text;
            _toastCanvas = canvasGO;
            canvasGO.SetActive(false);
        }

        private IEnumerator HideToastAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            if (_toastCanvas != null)
            {
                _toastCanvas.SetActive(false);
            }

            _toastCoroutine = null;
        }

        private void OnDestroy()
        {
            if (_toastCoroutine != null)
            {
                StopCoroutine(_toastCoroutine);
                _toastCoroutine = null;
            }

            if (_toastCanvas != null)
            {
                Destroy(_toastCanvas);
                _toastCanvas = null;
            }
        }
    }
}

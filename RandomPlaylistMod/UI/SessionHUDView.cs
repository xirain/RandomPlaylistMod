using RandomPlaylistMod.Managers;
using RandomPlaylistMod.Models;
using System;
using System.Collections;
using UnityEngine;
using TMPro;
using Zenject;

namespace RandomPlaylistMod.UI
{
    public class SessionHUDView : MonoBehaviour
    {
        private PlaySessionManager _playSessionManager;
        private Coroutine _updateCoroutine;
        private TextMeshProUGUI _text;
        private GameObject _hudGo;
        private Camera _vrCam;

        [Inject]
        public void Construct(PlaySessionManager playSessionManager)
        {
            _playSessionManager = playSessionManager;
        }

        private void Start()
        {
            Plugin.Log.Info("SessionHUDView: Start - creating VR HUD");
            StartCoroutine(CreateHudAfterFrame());
        }

        private IEnumerator CreateHudAfterFrame()
        {
            yield return null;
            CreateHud();
        }

        private void CreateHud()
        {
            _vrCam = FindVRam();
            if (_vrCam == null)
            {
                Plugin.Log.Warn("SessionHUDView: No VR camera found, retrying...");
                StartCoroutine(RetryFindCamera());
                return;
            }

            Plugin.Log.Info($"SessionHUDView: Found VR camera: {_vrCam.name}");

            _hudGo = new GameObject("RandomPlaylistHUD");

            // 初始位置：相机前方 6 米 + 上方 2.5 米（仰头约 22° 可见）
            UpdateHudPosition();

            // Billboard：让文字正面朝向玩家
            // TextMeshPro WorldSpace 文字渲染在 +Z 面，LookAt 使 +Z 朝向相机
            _hudGo.transform.LookAt(_vrCam.transform);
            // +Z 朝向相机后，文字在 +Z 面，但需要从正面看，所以翻 180°
            // 这样文字正面朝向玩家
            _hudGo.transform.Rotate(0, 180, 0);

            var canvas = _hudGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            // 半透明黑底
            var bgGo = new GameObject("BG");
            bgGo.transform.SetParent(_hudGo.transform, false);
            var bg = bgGo.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0f, 0f, 0f, 0f); // 全透明背景
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Canvas 大小（单位：米）—— 配合大字体
            var canvasRect = _hudGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(6.0f, 0.6f);

            // 白字（0.36 = 36cm 高，远处清晰）
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(bgGo.transform, false);
            _text = textGo.AddComponent<TextMeshProUGUI>();
            _text.fontSize = 0.36f;
            _text.color = Color.white;
            _text.alignment = TextAlignmentOptions.Center;
            _text.text = "";
            _text.material = null;

            var textRect = _text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            _playSessionManager.SongChanged += OnSongChanged;
            _playSessionManager.SessionEnded += OnSessionEnded;
            _playSessionManager.SessionStarted += OnSessionStarted;

            if (_playSessionManager.IsSessionActive)
            {
                UpdateHudText();
                StartUpdateCoroutine();
            }

            Plugin.Log.Info("SessionHUDView: HUD created successfully");
        }

        private void UpdateHudPosition()
        {
            if (_hudGo == null || _vrCam == null) return;
            // 固定在相机前方 6 米 + 上方 2.5 米
            Vector3 targetPos = _vrCam.transform.position
                + _vrCam.transform.forward * 6f
                + Vector3.up * 2.5f;
            _hudGo.transform.position = targetPos;
        }

        private IEnumerator RetryFindCamera()
        {
            yield return new WaitForSeconds(0.5f);
            CreateHud();
        }

        private void LateUpdate()
        {
            if (_hudGo == null || _vrCam == null) return;

            // 位置固定不更新（世界空间固定位置，不跟随头部移动）
            // 只更新朝向，让文字始终可读
            _hudGo.transform.LookAt(_vrCam.transform);
            _hudGo.transform.Rotate(0, 180, 0);
        }

        private Camera FindVRam()
        {
            Camera cam = Camera.main;
            if (cam != null) return cam;

            Camera bestCam = null;
            foreach (var c in Camera.allCameras)
            {
                if (!c.gameObject.activeInHierarchy) continue;
                if (c.stereoTargetEye != StereoTargetEyeMask.None) return c;
                bestCam = c;
            }

            cam = FindObjectOfType<Camera>();
            if (cam != null) return cam;

            return bestCam;
        }

        private void OnDestroy()
        {
            StopUpdateCoroutine();
            if (_playSessionManager != null)
            {
                _playSessionManager.SongChanged -= OnSongChanged;
                _playSessionManager.SessionEnded -= OnSessionEnded;
                _playSessionManager.SessionStarted -= OnSessionStarted;
            }
        }

        private void OnSessionStarted(PlaySession session)
        {
            UpdateHudText();
            StartUpdateCoroutine();
        }

        private void OnSongChanged(SongInfo song, int currentIndex, int totalCount)
        {
            UpdateHudText();
        }

        private void OnSessionEnded(PlaySession session)
        {
            StopUpdateCoroutine();
            if (_text != null) _text.text = "";
        }

        private void UpdateHudText()
        {
            if (_text == null || !_playSessionManager.IsSessionActive) return;
            var session = _playSessionManager.GetCurrentSession();
            if (session == null) return;
            var currentSong = _playSessionManager.CurrentSong;
            string songName = currentSong?.SongName ?? "—";
            var elapsed = TimeSpan.FromMinutes(session.ElapsedMinutes);
            _text.text = $"#{session.CurrentSongIndex + 1}/{session.TotalSongs}  {songName}  |  {elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        }

        private void StartUpdateCoroutine()
        {
            StopUpdateCoroutine();
            _updateCoroutine = StartCoroutine(UpdateRoutine());
        }

        private void StopUpdateCoroutine()
        {
            if (_updateCoroutine != null)
            {
                StopCoroutine(_updateCoroutine);
                _updateCoroutine = null;
            }
        }

        private IEnumerator UpdateRoutine()
        {
            var wait = new WaitForSeconds(1f);
            while (true)
            {
                yield return wait;
                if (_playSessionManager.IsSessionActive) UpdateHudText();
            }
        }
    }
}

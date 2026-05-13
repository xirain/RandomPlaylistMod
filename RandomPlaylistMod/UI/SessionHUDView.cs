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

        [Inject]
        public void Construct(PlaySessionManager playSessionManager)
        {
            _playSessionManager = playSessionManager;
        }

        private void Start()
        {
            Plugin.Log.Info("SessionHUDView: Start");

            var canvasGo = new GameObject("RandomPlaylistHUD");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            
            
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // 半透明黑底
            var bgGo = new GameObject("BG");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bg = bgGo.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0.9f);
            bgRect.anchorMax = new Vector2(0.5f, 0.9f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.sizeDelta = new Vector2(800, 60);

            // 白字
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(bgGo.transform, false);
            _text = textGo.AddComponent<TextMeshProUGUI>();
            _text.fontSize = 28;
            _text.color = Color.white;
            _text.alignment = TextAlignmentOptions.Center;
            _text.text = "";
            
            // 关键修复：VR 中 Z-fighting 导致文本不可见，需要修复 shader
            FixTextShader(_text);

            var textRect = _text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 0);
            textRect.offsetMax = new Vector2(-10, 0);

            _playSessionManager.SongChanged += OnSongChanged;
            _playSessionManager.SessionEnded += OnSessionEnded;
            _playSessionManager.SessionStarted += OnSessionStarted;

            if (_playSessionManager.IsSessionActive)
            {
                UpdateHudText();
                StartUpdateCoroutine();
            }
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

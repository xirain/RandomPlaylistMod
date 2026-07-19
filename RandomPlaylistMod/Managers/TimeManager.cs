using UnityEngine;

namespace RandomPlaylistMod.Managers
{
    public class TimeManager
    {
        private float _startTime;
        private bool _isRunning;
        private float _lastElapsedSeconds; // 缓存最后一次读取的值，停止后仍可访问

        public float ElapsedSeconds => _isRunning ? Time.time - _startTime : _lastElapsedSeconds;

        public void StartTimer()
        {
            _startTime = Time.time;
            _isRunning = true;
            _lastElapsedSeconds = 0f;
        }

        public void StopTimer()
        {
            // 先缓存当前流逝时间，再停止 — 修复 Duration=0 Bug
            if (_isRunning)
            {
                _lastElapsedSeconds = Time.time - _startTime;
            }
            _isRunning = false;
        }

        public float GetElapsedSeconds()
        {
            return ElapsedSeconds;
        }

        public float GetElapsedMinutes()
        {
            return ElapsedSeconds / 60f;
        }

        public bool IsTimeUp(float targetMinutes)
        {
            return GetElapsedMinutes() >= targetMinutes;
        }

        public float GetRemainingSeconds(float targetMinutes)
        {
            float targetSeconds = targetMinutes * 60f;
            float remaining = targetSeconds - ElapsedSeconds;
            return Mathf.Max(0f, remaining);
        }

        public float GetRemainingMinutes(float targetMinutes)
        {
            return GetRemainingSeconds(targetMinutes) / 60f;
        }
    }
}

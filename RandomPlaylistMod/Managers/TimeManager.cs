
using UnityEngine;

namespace RandomPlaylistMod.Managers
{
    public class TimeManager
    {
        private float _startTime;
        private bool _isRunning;

        public float ElapsedSeconds => _isRunning ? Time.time - _startTime : 0f;

        public void StartTimer()
        {
            _startTime = Time.time;
            _isRunning = true;
        }

        public void StopTimer()
        {
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

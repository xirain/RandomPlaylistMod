
using System;

namespace RandomPlaylistMod.Tests.TestManagers
{
    public class TimeManager
    {
        private int _targetDurationMinutes;
        private int _elapsedSeconds;
        private bool _isRunning;
        private DateTime? _lastUpdateTime;

        public int TargetDurationMinutes
        {
            get => _targetDurationMinutes;
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(value), "Duration must be at least 1 minute");
                if (value > 720)
                    throw new ArgumentOutOfRangeException(nameof(value), "Duration cannot exceed 12 hours (720 minutes)");
                _targetDurationMinutes = value;
            }
        }

        public int ElapsedSeconds => _elapsedSeconds;
        public int RemainingSeconds => Math.Max(0, _targetDurationMinutes * 60 - _elapsedSeconds);
        public bool IsRunning => _isRunning;

        public void Start()
        {
            _elapsedSeconds = 0;
            _isRunning = true;
            _lastUpdateTime = DateTime.Now;
        }

        public void Stop()
        {
            _isRunning = false;
            _lastUpdateTime = null;
        }

        public void Reset()
        {
            _elapsedSeconds = 0;
            _isRunning = false;
            _lastUpdateTime = null;
        }

        public bool IsSessionComplete()
        {
            return _elapsedSeconds >= _targetDurationMinutes * 60;
        }

        public string FormatTime(int seconds)
        {
            var time = TimeSpan.FromSeconds(seconds);
            return $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
        }
    }
}

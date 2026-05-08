using NUnit.Framework;
using RandomPlaylistMod.Tests.TestModels;
using System;
using System.Collections.Generic;

namespace RandomPlaylistMod.Tests
{
    /// <summary>
    /// 测试 PlaySessionManager 事件系统和会话逻辑
    /// 使用简化版 TestPlaySessionManager 避免对 Unity 的依赖
    /// </summary>
    [TestFixture]
    public class PlaySessionManagerTests
    {
        private TestPlaySessionManager _manager;
        private List<string> _eventLog;

        [SetUp]
        public void Setup()
        {
            _manager = new TestPlaySessionManager();
            _eventLog = new List<string>();

            _manager.SessionStarted += (session) =>
                _eventLog.Add($"SessionStarted:{session.TotalSongs}");

            _manager.SessionEnded += (session) =>
                _eventLog.Add($"SessionEnded:{session.CurrentSongIndex}");

            _manager.SongChanged += (song, idx, total) =>
                _eventLog.Add($"SongChanged:{song.SongName}:{idx + 1}/{total}");

            _manager.SongFailed += (song, reason) =>
                _eventLog.Add($"SongFailed:{song.SongName}:{reason}");
        }

        [Test]
        public void StartSession_NoSongs_DoesNotStartSession()
        {
            _manager.StartSession(30, new List<SongInfo>());
            Assert.IsFalse(_manager.IsSessionActive);
            Assert.IsEmpty(_eventLog);
        }

        [Test]
        public void StartSession_WithSongs_StartsSessionAndFiresEvent()
        {
            var songs = CreateTestSongs(3);
            _manager.StartSession(30, songs);

            Assert.IsTrue(_manager.IsSessionActive);
            Assert.Contains("SessionStarted:3", _eventLog);
        }

        [Test]
        public void StartSession_WithSongs_FiresSongChangedForFirstSong()
        {
            var songs = CreateTestSongs(3);
            _manager.StartSession(30, songs);

            Assert.IsTrue(_eventLog.Exists(e => e.StartsWith("SongChanged:")));
        }

        [Test]
        public void EndSession_FiresEventWithCorrectSongCount()
        {
            var songs = CreateTestSongs(5);
            _manager.StartSession(30, songs);
            _eventLog.Clear();

            _manager.SimulateSongComplete();
            _manager.SimulateSongComplete();
            _manager.EndSession();

            Assert.Contains("SessionEnded:2", _eventLog);
        }

        [Test]
        public void OnSongFinished_AdvancesToNextSong()
        {
            var songs = CreateTestSongs(3);
            _manager.StartSession(30, songs);
            _eventLog.Clear();

            _manager.SimulateSongComplete();

            // 应该触发 SongChanged 事件
            Assert.IsTrue(_eventLog.Exists(e => e.StartsWith("SongChanged:") && e.Contains("2/3")));
        }

        [Test]
        public void OnSongFinished_LastSong_EndsSession()
        {
            var songs = CreateTestSongs(2);
            _manager.StartSession(30, songs);
            _eventLog.Clear();

            _manager.SimulateSongComplete(); // 第2首（最后一首）
            _manager.SimulateSongComplete(); // 没有下一首了

            Assert.IsFalse(_manager.IsSessionActive);
            Assert.IsTrue(_eventLog.Exists(e => e.StartsWith("SessionEnded:")));
        }

        [Test]
        public void SongFailed_FiresEventAndSkipsToNext()
        {
            var songs = CreateTestSongs(3);
            _manager.StartSession(30, songs);
            _eventLog.Clear();

            _manager.SimulateSongFailure("Level not found");

            Assert.IsTrue(_eventLog.Exists(e => e.StartsWith("SongFailed:") && e.Contains("Level not found")));
        }

        [Test]
        public void SessionTimeout_EndsSession()
        {
            var songs = CreateTestSongs(10);
            _manager.StartSession(30, songs);
            _eventLog.Clear();

            // 模拟超时
            _manager.SimulateTimeout();
            _manager.SimulateSongComplete();

            Assert.IsFalse(_manager.IsSessionActive);
            Assert.IsTrue(_eventLog.Exists(e => e.StartsWith("SessionEnded:")));
        }

        [Test]
        public void EndSession_WhenNotActive_DoesNotFireEvent()
        {
            _manager.EndSession();
            Assert.IsEmpty(_eventLog);
        }

        #region 测试辅助

        private List<SongInfo> CreateTestSongs(int count)
        {
            var songs = new List<SongInfo>();
            for (int i = 0; i < count; i++)
            {
                songs.Add(new SongInfo
                {
                    SongName = $"Song{i + 1}",
                    Author = $"Artist{(i % 3) + 1}",
                    Duration = 180 + i * 30,
                    Key = $"key{i}",
                    LevelId = $"custom_level_{i:D40}"
                });
            }
            return songs;
        }

        #endregion
    }

    /// <summary>
    /// 简化版 PlaySessionManager，避免 Unity 依赖
    /// 模拟核心事件驱动逻辑，与实际代码逻辑对齐
    /// </summary>
    internal class TestPlaySessionManager
    {
        private PlaySession _currentSession;
        private List<SongInfo> _currentSongQueue;
        private int _currentSongIndex;
        private float _elapsedMinutes;
        private bool _simulateTimeout;

        public event Action<PlaySession> SessionStarted;
        public event Action<PlaySession> SessionEnded;
        public event Action<SongInfo, int, int> SongChanged;
        public event Action<SongInfo, string> SongFailed;

        public bool IsSessionActive => _currentSession != null;
        public SongInfo CurrentSong => GetCurrentSong();
        public int RemainingSongCount => _currentSongQueue?.Count - _currentSongIndex ?? 0;

        public void StartSession(int durationMinutes, List<SongInfo> songs)
        {
            if (songs == null || songs.Count == 0)
                return;

            _currentSongQueue = new List<SongInfo>(songs);
            _currentSongIndex = 0;
            _elapsedMinutes = 0;
            _simulateTimeout = false;

            _currentSession = new PlaySession
            {
                DurationMinutes = durationMinutes,
                TotalSongs = _currentSongQueue.Count
            };

            SessionStarted?.Invoke(_currentSession);
            PlayNextSong();
        }

        public void EndSession()
        {
            if (_currentSession == null)
                return;

            var songCount = _currentSongIndex;
            var session = _currentSession;
            _currentSession = null;
            _currentSongQueue = null;
            _currentSongIndex = 0;

            session.CurrentSongIndex = songCount;
            SessionEnded?.Invoke(session);
        }

        public void SimulateSongComplete()
        {
            if (!IsSessionActive)
                return;

            // 超时检查
            if (_simulateTimeout || _elapsedMinutes >= _currentSession.DurationMinutes)
            {
                EndSession();
                return;
            }

            if (!HasNextSong())
            {
                EndSession();
                return;
            }

            _currentSongIndex++;
            _elapsedMinutes += 3; // 模拟每首歌 3 分钟
            PlayNextSong();
        }

        public void SimulateSongFailure(string reason)
        {
            if (!IsSessionActive)
                return;

            var song = GetCurrentSong();
            if (song != null)
                SongFailed?.Invoke(song, reason);

            _currentSongIndex++;
            PlayNextSong();
        }

        public void SimulateTimeout()
        {
            _simulateTimeout = true;
        }

        private void PlayNextSong()
        {
            var song = GetCurrentSong();
            if (song == null)
            {
                EndSession();
                return;
            }

            SongChanged?.Invoke(song, _currentSongIndex, _currentSongQueue.Count);
        }

        private SongInfo GetCurrentSong()
        {
            if (_currentSongQueue == null || _currentSongIndex >= _currentSongQueue.Count)
                return null;
            return _currentSongQueue[_currentSongIndex];
        }

        private bool HasNextSong()
        {
            return _currentSongQueue != null && _currentSongIndex + 1 < _currentSongQueue.Count;
        }
    }
}


using NUnit.Framework;
using RandomPlaylistMod.Tests.TestManagers;
using System;

namespace RandomPlaylistMod.Tests
{
    [TestFixture]
    public class TimeManagerTests
    {
        private TimeManager _timeManager;

        [SetUp]
        public void Setup()
        {
            _timeManager = new TimeManager();
        }

        [Test]
        public void TargetDurationMinutes_SetValidValue_SetsSuccessfully()
        {
            _timeManager.TargetDurationMinutes = 30;
            Assert.AreEqual(30, _timeManager.TargetDurationMinutes);
        }

        [Test]
        public void TargetDurationMinutes_SetZero_ThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _timeManager.TargetDurationMinutes = 0);
        }

        [Test]
        public void TargetDurationMinutes_SetNegative_ThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _timeManager.TargetDurationMinutes = -1);
        }

        [Test]
        public void TargetDurationMinutes_SetExceedsMax_ThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _timeManager.TargetDurationMinutes = 721);
        }

        [Test]
        public void Start_InitializesElapsedTimeToZero()
        {
            _timeManager.TargetDurationMinutes = 30;
            _timeManager.Start();

            Assert.AreEqual(0, _timeManager.ElapsedSeconds);
            Assert.IsTrue(_timeManager.IsRunning);
        }

        [Test]
        public void Stop_SetsIsRunningToFalse()
        {
            _timeManager.TargetDurationMinutes = 30;
            _timeManager.Start();
            _timeManager.Stop();

            Assert.IsFalse(_timeManager.IsRunning);
        }

        [Test]
        public void Reset_ClearsState()
        {
            _timeManager.TargetDurationMinutes = 30;
            _timeManager.Start();
            _timeManager.Reset();

            Assert.AreEqual(0, _timeManager.ElapsedSeconds);
            Assert.IsFalse(_timeManager.IsRunning);
        }

        [Test]
        public void FormatTime_ValidSeconds_ReturnsFormattedString()
        {
            string result = _timeManager.FormatTime(3661);
            Assert.AreEqual("01:01:01", result);
        }

        [Test]
        public void RemainingSeconds_CalculatedCorrectly()
        {
            _timeManager.TargetDurationMinutes = 30;
            _timeManager.Start();

            int remaining = _timeManager.RemainingSeconds;
            Assert.AreEqual(1800, remaining);
        }

        [Test]
        public void IsSessionComplete_NotStarted_ReturnsFalse()
        {
            _timeManager.TargetDurationMinutes = 30;
            Assert.IsFalse(_timeManager.IsSessionComplete());
        }
    }
}

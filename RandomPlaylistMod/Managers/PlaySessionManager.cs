
using System.Collections.Generic;
using RandomPlaylistMod.Models;
using UnityEngine;

namespace RandomPlaylistMod.Managers
{
    public class PlaySessionManager
    {
        private readonly PlaylistManager _playlistManager;
        private readonly SongSelector _songSelector;
        private readonly TimeManager _timeManager;
        private PlaySession _currentSession;
        private List<SongInfo> _currentSongQueue;
        private int _currentSongIndex;

        public PlaySessionManager(PlaylistManager playlistManager, SongSelector songSelector, TimeManager timeManager)
        {
            _playlistManager = playlistManager;
            _songSelector = songSelector;
            _timeManager = timeManager;
        }

        public bool IsSessionActive => _currentSession != null;

        public void StartSession(int durationMinutes)
        {
            var allSongs = _playlistManager.GetSongsFromSelectedPlaylists();

            if (allSongs.Count == 0)
            {
                Plugin.Log.Info("PlaySessionManager: No songs available in selected playlists");
                return;
            }

            _currentSongQueue = _songSelector.SelectSongsForDuration(allSongs, durationMinutes);
            _currentSongIndex = 0;
            _currentSession = new PlaySession
            {
                DurationMinutes = durationMinutes,
                TotalSongs = _currentSongQueue.Count,
                StartTime = Time.time
            };

            _timeManager.StartTimer();
            Plugin.Log.Info($"PlaySessionManager: Session started - {durationMinutes} min, {_currentSongQueue.Count} songs");

            // 启动第一首歌
            PlayNextSong();
        }

        public void EndSession()
        {
            _currentSession = null;
            _currentSongQueue = null;
            _currentSongIndex = 0;
            _timeManager.StopTimer();
            Plugin.Log.Info("PlaySessionManager: Session ended");
        }

        public SongInfo GetCurrentSong()
        {
            if (_currentSongQueue == null || _currentSongIndex >= _currentSongQueue.Count)
                return null;

            return _currentSongQueue[_currentSongIndex];
        }

        public SongInfo GetNextSong()
        {
            if (_currentSongQueue == null || _currentSongIndex >= _currentSongQueue.Count)
            {
                EndSession();
                return null;
            }

            return _currentSongQueue[_currentSongIndex++];
        }

        public bool HasNextSong()
        {
            return _currentSongQueue != null && _currentSongIndex < _currentSongQueue.Count;
        }

        public PlaySession GetCurrentSession()
        {
            if (_currentSession != null)
            {
                _currentSession.ElapsedMinutes = _timeManager.GetElapsedSeconds() / 60f;
            }
            return _currentSession;
        }

        private void PlayNextSong()
        {
            var song = GetCurrentSong();
            if (song == null)
            {
                Plugin.Log.Info("PlaySessionManager: No more songs, session complete");
                EndSession();
                return;
            }

            Plugin.Log.Info($"PlaySessionManager: Playing song '{song.SongName}' by {song.Author} (ID: {song.LevelId})");
            // TODO: 实际启动歌曲播放需要使用GameplayCoreSceneSetupData
            // 这里只是记录日志，实际的歌曲播放逻辑需要后续实现
        }
    }
}

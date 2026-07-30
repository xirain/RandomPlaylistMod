using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaberPlaylistsLib;
using BeatSaberPlaylistsLib.Types;
using SongCore;
using Zenject;

namespace RandomPlaylistMod.Managers
{
    /// <summary>
    /// 收藏管理：将游玩中按 Y/B 收藏的歌曲保存到一个固定歌单 "RandomPlaylist Favorites"。
    /// </summary>
    public class FavoriteManager : IInitializable, IDisposable
    {
        public const string FavoritesPlaylistName = "RandomPlaylist Favorites";
        private const string FavoritesPlaylistAuthor = "RandomPlaylistMod";

        private readonly PlaySessionManager _playSessionManager;

        [Inject]
        public FavoriteManager(PlaySessionManager playSessionManager)
        {
            _playSessionManager = playSessionManager;
        }

        public void Initialize() { }
        public void Dispose() { }

        /// <summary>
        /// 将当前歌曲保存到收藏歌单。返回保存结果，供 UI 反馈使用。
        /// </summary>
        public FavoriteResult SaveCurrentSong()
        {
            var song = _playSessionManager?.CurrentSong;
            if (song == null || string.IsNullOrEmpty(song.LevelId))
            {
                return new FavoriteResult(FavoriteStatus.NoCurrentSong, song);
            }

            try
            {
                var playlist = GetOrCreateFavoritesPlaylist();
                if (playlist == null)
                {
                    return new FavoriteResult(FavoriteStatus.Error, song);
                }

                if (IsInPlaylist(playlist, song.LevelId))
                {
                    return new FavoriteResult(FavoriteStatus.AlreadyInPlaylist, song);
                }

                var level = Loader.GetLevelById(song.LevelId);
                if (level == null)
                {
                    Plugin.Log?.Warn($"[Favorite] 无法加载关卡对象，跳过收藏: {song.LevelId}");
                    return new FavoriteResult(FavoriteStatus.Error, song);
                }

                playlist.Add(level);
                BeatSaberPlaylistsLib.PlaylistManager.DefaultManager.StorePlaylist(playlist);

                Plugin.Log?.Info($"[Favorite] 已收藏歌曲「{song.SongName}」到歌单「{FavoritesPlaylistName}」");
                return new FavoriteResult(FavoriteStatus.Added, song);
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error($"[Favorite] 保存收藏失败: {ex.Message}");
                return new FavoriteResult(FavoriteStatus.Error, song);
            }
        }

        private IPlaylist GetOrCreateFavoritesPlaylist()
        {
            var defaultManager = BeatSaberPlaylistsLib.PlaylistManager.DefaultManager;
            var existing = defaultManager.GetAllPlaylists(true)
                .FirstOrDefault(p => p.Title != null &&
                                     p.Title.Equals(FavoritesPlaylistName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing;
            }

            return defaultManager.CreatePlaylist("", FavoritesPlaylistName, FavoritesPlaylistAuthor, "");
        }

        private bool IsInPlaylist(IPlaylist playlist, string levelId)
        {
            foreach (var song in playlist)
            {
                if (!string.IsNullOrEmpty(levelId) &&
                    string.Equals(song.LevelId, levelId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }

    public enum FavoriteStatus
    {
        Added,
        AlreadyInPlaylist,
        NoCurrentSong,
        Error
    }

    public readonly struct FavoriteResult
    {
        public FavoriteStatus Status { get; }
        public Models.SongInfo Song { get; }

        public FavoriteResult(FavoriteStatus status, Models.SongInfo song)
        {
            Status = status;
            Song = song;
        }
    }
}

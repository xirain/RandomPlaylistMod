using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using RandomPlaylistMod.Managers;
using RandomPlaylistMod.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Zenject;

namespace RandomPlaylistMod.UI
{
    [ViewDefinition("RandomPlaylistMod.UI.Views.RandomPlaylistView.bsml")]
    public class RandomPlaylistUI : BSMLAutomaticViewController
    {
        private PlaylistManager _playlistManager;
        private PlaySessionManager _playSessionManager;
        private SongSelector _songSelector;

        private int _selectedDuration = 30;
        private string _estimatedSongs = "0";
        private string _estimatedTime = "00:00";
        private string _selectedInfo = "No playlists selected";

        [UIComponent("playlist-list")]
        private CustomListTableData _playlistList;

        [UIValue("selected-duration")]
        public int SelectedDuration
        {
            get => _selectedDuration;
            set
            {
                _selectedDuration = value;
                UpdateEstimates();
                NotifyPropertyChanged();
            }
        }

        [UIValue("estimated-songs")]
        public string EstimatedSongs
        {
            get => _estimatedSongs;
            set
            {
                _estimatedSongs = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue("estimated-time")]
        public string EstimatedTime
        {
            get => _estimatedTime;
            set
            {
                _estimatedTime = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue("selected-info")]
        public string SelectedInfo
        {
            get => _selectedInfo;
            set
            {
                _selectedInfo = value;
                NotifyPropertyChanged();
            }
        }

        [Inject]
        public void Construct(PlaylistManager playlistManager, PlaySessionManager playSessionManager, SongSelector songSelector)
        {
            _playlistManager = playlistManager;
            _playSessionManager = playSessionManager;
            _songSelector = songSelector;
            Plugin.Log.Info("RandomPlaylistUI: Dependencies injected");
        }

        [UIAction("on-playlist-click")]
        public void OnPlaylistClick(TableView tableView, int index)
        {
            if (index < 0 || index >= _playlistManager.Playlists.Count)
                return;

            var playlist = _playlistManager.Playlists[index];
            _playlistManager.TogglePlaylistSelection(playlist.Id);
            UpdateEstimates();
        }

        [UIAction("#post-parse")]
        public void PostParse()
        {
            Plugin.Log.Info("RandomPlaylistUI: PostParse called");
            _playlistManager.LoadPlaylistsAsync();
            RefreshPlaylistList();
        }

        [UIAction("select-all")]
        public void SelectAllPlaylists()
        {
            _playlistManager.SelectAllPlaylists();
            RefreshPlaylistList();
            UpdateEstimates();
        }

        [UIAction("deselect-all")]
        public void DeselectAllPlaylists()
        {
            _playlistManager.DeselectAllPlaylists();
            RefreshPlaylistList();
            UpdateEstimates();
        }

        [UIAction("start-session")]
        public void StartSession()
        {
            if (_selectedDuration < 1)
                return;

            _playSessionManager.StartSession(_selectedDuration);
            UpdateEstimates();
        }

        [UIAction("end-session")]
        public void EndSession()
        {
            _playSessionManager.EndSession();
            UpdateEstimates();
        }

        private void RefreshPlaylistList()
        {
            if (_playlistList == null) return;

            _playlistList.Data.Clear();
            foreach (var playlist in _playlistManager.Playlists)
            {
                string subtext = playlist.Selected
                    ? $"✓ {playlist.PlayableSongCount}/{playlist.SongCount} songs"
                    : $"{playlist.PlayableSongCount}/{playlist.SongCount} songs";

                _playlistList.Data.Add(new CustomListTableData.CustomCellInfo(
                    playlist.Name,
                    subtext,
                    null
                ));
            }

            _playlistList.TableView?.ReloadData();

            Plugin.Log.Info($"RandomPlaylistUI: Refreshed playlist list with {_playlistList.Data.Count} items");
        }

        private void UpdateEstimates()
        {
            var selectedPlaylists = _playlistManager.GetSelectedPlaylists();
            int selectedCount = selectedPlaylists.Count;

            if (selectedCount == 0)
            {
                EstimatedSongs = "0";
                EstimatedTime = "00:00";
                SelectedInfo = "No playlists selected";
                return;
            }

            // 使用PlaylistInfo中的统计信息
            int totalPlayable = selectedPlaylists.Sum(p => p.PlayableSongCount);
            int totalDuration = selectedPlaylists.Sum(p => p.TotalDuration);

            SelectedInfo = $"{selectedCount} playlists selected ({totalPlayable} songs)";

            // 估算歌曲数
            var allSongs = _playlistManager.GetSongsFromSelectedPlaylists();
            int estimatedCount = _songSelector.CalculateEstimatedSongCount(allSongs, _selectedDuration);
            EstimatedSongs = Math.Min(estimatedCount, totalPlayable).ToString();

            // 估算时间
            var timeSpan = TimeSpan.FromSeconds(Math.Min(totalDuration, _selectedDuration * 60));
            EstimatedTime = $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}";
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            
            Plugin.Log.Info($"RandomPlaylistUI activated: firstActivation={firstActivation}, addedToHierarchy={addedToHierarchy}");
        }
    }
}

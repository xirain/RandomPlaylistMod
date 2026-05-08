using System.Collections.Generic;
using System.ComponentModel;

namespace RandomPlaylistMod.Tests.TestModels
{
    public class PlaylistInfo : INotifyPropertyChanged
    {
        private string _id;
        private string _name;
        private bool _selected;
        private int _songCount;
        private int _playableSongCount;
        private int _totalDuration;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public bool Selected
        {
            get => _selected;
            set { _selected = value; OnPropertyChanged(nameof(Selected)); }
        }

        public int SongCount
        {
            get => _songCount;
            set { _songCount = value; OnPropertyChanged(nameof(SongCount)); }
        }

        public int PlayableSongCount
        {
            get => _playableSongCount;
            set { _playableSongCount = value; OnPropertyChanged(nameof(PlayableSongCount)); }
        }

        public int TotalDuration
        {
            get => _totalDuration;
            set { _totalDuration = value; OnPropertyChanged(nameof(TotalDuration)); }
        }

        public List<SongInfo> Songs { get; set; } = new List<SongInfo>();

        public string DisplayText => $"{Name} ({PlayableSongCount}/{SongCount} songs)";

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

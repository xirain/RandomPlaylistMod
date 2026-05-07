
using System.Collections.Generic;
using System.ComponentModel;

namespace RandomPlaylistMod.Models
{
    public class PlaylistInfo : INotifyPropertyChanged
    {
        private string _id;
        private string _name;
        private string _author;
        private bool _selected;
        private int _songCount;
        private int _playableSongCount;
        private int _totalDuration;
        private List<SongInfo> _songs = new List<SongInfo>();

        public event PropertyChangedEventHandler PropertyChanged;

        public string Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged(nameof(Id));
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public string Author
        {
            get => _author;
            set
            {
                _author = value;
                OnPropertyChanged(nameof(Author));
            }
        }

        public bool Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                OnPropertyChanged(nameof(Selected));
            }
        }

        public int SongCount
        {
            get => _songCount;
            set
            {
                _songCount = value;
                OnPropertyChanged(nameof(SongCount));
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        public int PlayableSongCount
        {
            get => _playableSongCount;
            set
            {
                _playableSongCount = value;
                OnPropertyChanged(nameof(PlayableSongCount));
                OnPropertyChanged(nameof(DisplayText));
            }
        }

        public int TotalDuration
        {
            get => _totalDuration;
            set
            {
                _totalDuration = value;
                OnPropertyChanged(nameof(TotalDuration));
            }
        }

        public List<SongInfo> Songs
        {
            get => _songs;
            set
            {
                _songs = value;
                OnPropertyChanged(nameof(Songs));
            }
        }

        /// <summary>
        /// BSML显示用的文本
        /// </summary>
        public string DisplayText => $"{Name} ({PlayableSongCount}/{SongCount} songs)";

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

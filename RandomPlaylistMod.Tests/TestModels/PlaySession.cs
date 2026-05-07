
using System.Collections.Generic;

namespace RandomPlaylistMod.Tests.TestModels
{
    public class PlaySession
    {
        public int TargetDuration { get; set; }
        public List<PlaylistInfo> SelectedPlaylists { get; set; } = new List<PlaylistInfo>();
        public Queue<SongInfo> SongQueue { get; set; } = new Queue<SongInfo>();
        public SongInfo CurrentSong { get; set; }
        public int ElapsedTime { get; set; }
        public bool IsActive { get; set; }
        public List<SongInfo> PlayedSongs { get; set; } = new List<SongInfo>();
    }
}


using System.Collections.Generic;

namespace RandomPlaylistMod.Tests.TestModels
{
    public class PlaylistInfo
    {
        public string Name { get; set; }
        public List<SongInfo> Songs { get; set; } = new List<SongInfo>();
        public bool IsSelected { get; set; }
        public string PlaylistId { get; set; }
    }
}

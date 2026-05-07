
namespace RandomPlaylistMod.Models
{
    public class PlaySession
    {
        public int DurationMinutes { get; set; }
        public int TotalSongs { get; set; }
        public float StartTime { get; set; }
        public float ElapsedMinutes { get; set; }
        public int CurrentSongIndex { get; set; }
    }
}

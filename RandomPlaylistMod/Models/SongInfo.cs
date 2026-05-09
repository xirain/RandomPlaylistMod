
namespace RandomPlaylistMod.Models
{
    public class SongInfo
    {
        public string SongName { get; set; }
        public string Author { get; set; }
        public int Duration { get; set; }
        public string Key { get; set; }
        public string PlaylistName { get; set; }
        public string LevelId { get; set; }
        public int BPM { get; set; }
        public float NPS { get; set; }  // Notes Per Second: 从 SongDetailsCache 计算 (-1=未知)
    }
}

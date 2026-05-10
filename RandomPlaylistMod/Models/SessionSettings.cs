namespace RandomPlaylistMod.Models
{
    public class SessionSettings
    {
        public int DurationMinutes { get; set; } = 30;
        public float MinNps { get; set; } = 0f;
        public float MaxNps { get; set; } = 99f;
        public bool NoFailEnabled { get; set; } = false;
    }
}

namespace Template.Services.Haptics
{
    public enum HapticType
    {
        Light,
        Medium,
        Heavy,
    }

    public interface IHapticService
    {
        /// <summary>Persisted in PlayerData. Play() is a no-op when disabled.</summary>
        bool Enabled { get; set; }

        void Play(HapticType type);
    }
}

using System;

namespace Template.Services.Save
{
    /// <summary>Data every template game persists. Game-specific data uses Load/Save with its own key.</summary>
    [Serializable]
    public sealed class PlayerData
    {
        public int Version = 1;
        public int BestScore;
        public int SessionCount;
        public bool TutorialSeen;
        public bool SoundEnabled = true;
        public bool HapticsEnabled = true;
        public long FirstLaunchUnixSeconds;
    }

    public interface ISaveService
    {
        /// <summary>Shared player data (loaded on construction). Call Save() after changing it.</summary>
        PlayerData Data { get; }

        void Save();

        /// <summary>Typed JSON load for game-specific data. Returns a new T when missing or corrupt.</summary>
        T Load<T>(string key) where T : class, new();
        void Save<T>(string key, T value) where T : class;
        bool HasKey(string key);
        void Delete(string key);

        /// <summary>Wipes everything (debug / "reset progress").</summary>
        void DeleteAll();
    }
}

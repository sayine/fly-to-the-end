using System;
using UnityEngine;

namespace Template.Services.Save
{
    /// <summary>Real save service: JsonUtility + PlayerPrefs. Flushes on pause/quit via Save().</summary>
    public sealed class PlayerPrefsSaveService : ISaveService
    {
        private const string Prefix = "hc.";
        private const string PlayerDataKey = "player";

        public PlayerData Data { get; private set; }

        public PlayerPrefsSaveService()
        {
            Data = Load<PlayerData>(PlayerDataKey);
            if (Data.FirstLaunchUnixSeconds == 0)
            {
                Data.FirstLaunchUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                Save();
            }
        }

        public void Save()
        {
            Save(PlayerDataKey, Data);
            PlayerPrefs.Save();
        }

        public T Load<T>(string key) where T : class, new()
        {
            var json = PlayerPrefs.GetString(Prefix + key, null);
            if (string.IsNullOrEmpty(json)) return new T();
            try
            {
                return JsonUtility.FromJson<T>(json) ?? new T();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Corrupt data for '{key}', resetting. {e.Message}");
                return new T();
            }
        }

        public void Save<T>(string key, T value) where T : class
        {
            PlayerPrefs.SetString(Prefix + key, JsonUtility.ToJson(value));
        }

        public bool HasKey(string key) => PlayerPrefs.HasKey(Prefix + key);

        public void Delete(string key) => PlayerPrefs.DeleteKey(Prefix + key);

        public void DeleteAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Data = new PlayerData { FirstLaunchUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
        }
    }
}

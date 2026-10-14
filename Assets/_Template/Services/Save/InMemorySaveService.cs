using System.Collections.Generic;
using UnityEngine;

namespace Template.Services.Save
{
    /// <summary>Mock save service: JSON round-trip in memory. Used by tests and "fresh install" Editor runs.</summary>
    public sealed class InMemorySaveService : ISaveService
    {
        private readonly Dictionary<string, string> _store = new Dictionary<string, string>();

        public PlayerData Data { get; private set; } = new PlayerData();

        public void Save() => Save("player", Data);

        public T Load<T>(string key) where T : class, new()
        {
            return _store.TryGetValue(key, out var json) ? JsonUtility.FromJson<T>(json) : new T();
        }

        public void Save<T>(string key, T value) where T : class => _store[key] = JsonUtility.ToJson(value);

        public bool HasKey(string key) => _store.ContainsKey(key);

        public void Delete(string key) => _store.Remove(key);

        public void DeleteAll()
        {
            _store.Clear();
            Data = new PlayerData();
        }
    }
}

using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Template.Services.Analytics
{
    /// <summary>Editor/default implementation: logs every event to the console.</summary>
    public sealed class MockAnalyticsService : IAnalyticsService
    {
        private readonly StringBuilder _sb = new StringBuilder(128);
        private readonly bool _verbose;

        public MockAnalyticsService(bool verbose = true) => _verbose = verbose;

        public int EventCount { get; private set; }
        public string LastEventName { get; private set; }

        public void Initialize(bool trackingAllowed)
        {
            if (_verbose) Debug.Log($"[Analytics:Mock] Initialized (trackingAllowed={trackingAllowed})");
        }

        public void LogEvent(string name, Dictionary<string, object> parameters = null)
        {
            EventCount++;
            LastEventName = name;
            if (!_verbose) return;

            _sb.Clear().Append("[Analytics:Mock] ").Append(name);
            if (parameters != null)
                foreach (var kv in parameters)
                    _sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
            Debug.Log(_sb.ToString());
        }

        public void SetUserProperty(string name, string value)
        {
            if (_verbose) Debug.Log($"[Analytics:Mock] user property {name}={value}");
        }
    }
}

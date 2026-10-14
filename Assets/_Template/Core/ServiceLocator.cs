using System;
using System.Collections.Generic;

namespace Template.Core
{
    /// <summary>
    /// Minimal service registry. Services are registered once by the Boot scene and
    /// resolved by interface. Not a singleton MonoBehaviour: plain static map, cleared on boot/tests.
    /// Resolve in Awake/Start and cache the reference; never call Get in Update.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            Services[typeof(T)] = service;
        }

        public static T Get<T>() where T : class
        {
            if (Services.TryGetValue(typeof(T), out var service)) return (T)service;
            throw new InvalidOperationException($"Service {typeof(T).Name} is not registered. Did the Boot scene run?");
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out var obj))
            {
                service = (T)obj;
                return true;
            }
            service = null;
            return false;
        }

        public static bool IsRegistered<T>() where T : class => Services.ContainsKey(typeof(T));

        public static void Unregister<T>() where T : class => Services.Remove(typeof(T));

        /// <summary>Disposes IDisposable services and clears the registry.</summary>
        public static void Clear()
        {
            foreach (var service in Services.Values)
                if (service is IDisposable disposable) disposable.Dispose();
            Services.Clear();
        }
    }
}

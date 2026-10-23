using UnityEngine;

namespace Game.Wind
{
    /// <summary>Spins fan blades around their local forward axis (visual only, speed from WindConfig).</summary>
    public sealed class Spinner : MonoBehaviour
    {
        [SerializeField] private WindConfig _config;

        private void Update()
        {
            if (_config != null) transform.Rotate(0f, 0f, _config.BladeSpeed * Time.deltaTime, Space.Self);
        }
    }
}

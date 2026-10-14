using Template.Core;
using UnityEngine;

namespace Game
{
    /// <summary>Smoothly follows a target with a fixed offset from GameConfig.</summary>
    public sealed class FollowCamera : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private Transform _target;

        private void Start()
        {
            if (_target == null || _config == null) return;
            transform.position = _target.position + _config.CameraOffset;
            transform.LookAt(_target);
        }

        private void LateUpdate()
        {
            if (_target == null || _config == null) return;
            var desired = _target.position.WithY(0f) + _config.CameraOffset;
            transform.position = Vector3.Lerp(transform.position, desired,
                MathExtensions.SmoothingFactor(_config.CameraFollowSharpness, Time.deltaTime));
        }
    }
}

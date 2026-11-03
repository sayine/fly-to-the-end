using Game.Flight;
using Template.Core;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Behind-and-above chase camera for the paper plane (GameConfig "Chase camera"). Follows the flat
    /// flight heading (not the nose), so pitch wobble and stalls don't swing the view. FOV rises with speed,
    /// gentle roll with bank, and the position is kept inside the rooms (a union of boxes: rooms, hallway) so it
    /// never clips through walls but can follow the plane through doorways.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ChaseCamera : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private PaperPlane _plane;
        [Tooltip("Interiors (world space). The camera stays inside one of them, minus ChaseWallMargin.")]
        [SerializeField] private Bounds[] _areas = { new Bounds(Vector3.zero, new Vector3(14f, 5f, 20f)) };
        [SerializeField, HideInInspector] private Camera _camera;

        private Vector3 _heading = Vector3.forward;
        private float _roll;

        private void Awake()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
        }

        /// <summary>Jumps to the resting pose behind the plane (run start / respawn).</summary>
        public void Snap(Vector3 heading)
        {
            if (_plane == null || _config == null) return;
            _heading = heading.Flat().sqrMagnitude > 1e-4f ? heading.Flat().normalized : Vector3.forward;
            _roll = 0f;
            var target = _plane.transform.position;
            transform.position = DesiredPosition(target);
            transform.rotation = DesiredRotation(target, transform.position);
            _camera.fieldOfView = _config.ChaseFovMin;
        }

        private void LateUpdate()
        {
            if (_plane == null || _config == null) return;
            var dt = Time.deltaTime;
            if (dt <= 0f) return;

            var flying = _plane.CurrentPhase == PaperPlane.Phase.Flying;
            var velocity = _plane.CurrentPhase == PaperPlane.Phase.Ready ? Vector3.zero : _plane.Velocity;
            var flat = velocity.Flat();
            if (flat.sqrMagnitude > 0.25f)
                _heading = Vector3.Slerp(_heading, flat.normalized, MathExtensions.SmoothingFactor(_config.ChaseHeadingSharpness, dt));

            var target = _plane.transform.position;
            transform.position = Vector3.Lerp(transform.position, DesiredPosition(target),
                MathExtensions.SmoothingFactor(_config.ChasePositionSharpness, dt));

            var targetRoll = flying ? -_plane.State.Bank * _config.ChaseRollFactor : 0f;
            _roll = Mathf.Lerp(_roll, targetRoll, MathExtensions.SmoothingFactor(_config.ChaseRotationSharpness, dt));
            transform.rotation = Quaternion.Slerp(transform.rotation, DesiredRotation(target, transform.position),
                MathExtensions.SmoothingFactor(_config.ChaseRotationSharpness, dt));

            var speed01 = Mathf.InverseLerp(_config.ChaseFovSpeedMin, _config.ChaseFovSpeedMax, flying ? velocity.magnitude : 0f);
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, Mathf.Lerp(_config.ChaseFovMin, _config.ChaseFovMax, speed01),
                MathExtensions.SmoothingFactor(_config.ChasePositionSharpness, dt));
        }

        private Vector3 DesiredPosition(Vector3 target)
        {
            var p = target - _heading * _config.ChaseDistance + Vector3.up * _config.ChaseHeight;
            if (_areas == null || _areas.Length == 0) return p;
            var m = _config.ChaseWallMargin;
            // Fine where it is if it's inside any area (e.g. still in the room while the plane enters the hallway).
            for (var i = 0; i < _areas.Length; i++)
                if (Inside(_areas[i], p, m)) return p;
            // Otherwise clamp into the area that holds the plane (or the first one).
            var area = _areas[0];
            for (var i = 0; i < _areas.Length; i++)
                if (Inside(_areas[i], target, 0f)) { area = _areas[i]; break; }
            var min = area.min + new Vector3(m, m, m);
            var max = area.max - new Vector3(m, m, m);
            return new Vector3(Mathf.Clamp(p.x, min.x, max.x), Mathf.Clamp(p.y, min.y, max.y), Mathf.Clamp(p.z, min.z, max.z));
        }

        private static bool Inside(Bounds b, Vector3 p, float margin) =>
            p.x > b.min.x + margin && p.x < b.max.x - margin &&
            p.y > b.min.y + margin && p.y < b.max.y - margin &&
            p.z > b.min.z + margin && p.z < b.max.z - margin;

        private Quaternion DesiredRotation(Vector3 target, Vector3 from)
        {
            var look = target + _heading * _config.ChaseLookAhead - from;
            if (look.sqrMagnitude < 1e-4f) look = _heading;
            return Quaternion.LookRotation(look, Vector3.up) * Quaternion.Euler(0f, 0f, _roll);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
        }
#endif
    }
}

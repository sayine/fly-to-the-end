using Game.Flight;
using Template.Core;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Behind-and-above chase camera for the paper plane (GameConfig "Chase camera"). Follows the flat
    /// flight heading (not the nose), so pitch wobble and stalls don't swing the view. FOV rises with speed,
    /// gentle roll with bank. Walls and furniture never come between the camera and the plane: a sphere cast from the
    /// plane back to the camera's ideal spot pulls the camera in front of anything in the way (at once, so it never
    /// clips), and it eases back out when the way is clear. No room boxes, so nothing snaps at doorways.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ChaseCamera : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private PaperPlane _plane;
        [Tooltip("Camera collision radius (m): how far it stays from walls and furniture.")]
        [SerializeField, Min(0.05f)] private float _radius = 0.35f;
        [SerializeField, HideInInspector] private Camera _camera;

        private readonly RaycastHit[] _hits = new RaycastHit[8];

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
            var eased = Vector3.Lerp(transform.position, DesiredPosition(target),
                MathExtensions.SmoothingFactor(_config.ChasePositionSharpness, dt));
            transform.position = Unblocked(target, eased); // never ease through a wall

            var targetRoll = flying ? -_plane.State.Bank * _config.ChaseRollFactor : 0f;
            _roll = Mathf.Lerp(_roll, targetRoll, MathExtensions.SmoothingFactor(_config.ChaseRotationSharpness, dt));
            transform.rotation = Quaternion.Slerp(transform.rotation, DesiredRotation(target, transform.position),
                MathExtensions.SmoothingFactor(_config.ChaseRotationSharpness, dt));

            var speed01 = Mathf.InverseLerp(_config.ChaseFovSpeedMin, _config.ChaseFovSpeedMax, flying ? velocity.magnitude : 0f);
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, Mathf.Lerp(_config.ChaseFovMin, _config.ChaseFovMax, speed01),
                MathExtensions.SmoothingFactor(_config.ChasePositionSharpness, dt));
        }

        private Vector3 DesiredPosition(Vector3 target) =>
            Unblocked(target, target - _heading * _config.ChaseDistance + Vector3.up * _config.ChaseHeight);

        /// <summary>The point on the way from the plane to `wanted` just in front of the first wall or prop.</summary>
        private Vector3 Unblocked(Vector3 target, Vector3 wanted)
        {
            var offset = wanted - target;
            var distance = offset.magnitude;
            if (distance < 1e-3f) return wanted;
            var direction = offset / distance;
            var count = Physics.SphereCastNonAlloc(target, _radius, direction, _hits, distance, ~0, QueryTriggerInteraction.Ignore);
            var nearest = distance;
            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.rigidbody == _plane.Body || hit.distance <= 0f) continue; // the plane itself / starting overlaps
                if (hit.distance < nearest) nearest = hit.distance;
            }
            return target + direction * Mathf.Max(0.2f, nearest - 0.05f);
        }

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

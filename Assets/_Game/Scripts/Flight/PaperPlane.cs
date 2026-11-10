using System;
using Game.Levels;
using Game.Wind;
using UnityEngine;

namespace Game.Flight
{
    /// <summary>
    /// Rigidbody driver for the paper plane. Ready: kinematic on the launch pad. Flying: FlightModel forces in
    /// FixedUpdate, rotation set from the model. Crashed: plain physics (full gravity, free rotation) so the
    /// crash tumble plays out. Any collision while flying is a crash, except touching a LandingTarget: any touch,
    /// soft or hard, counts as a landing (arcade-friendly; Landed: the plane settles kinematic where it touched).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PaperPlane : MonoBehaviour
    {
        public enum Phase
        {
            Ready,
            Flying,
            Crashed,
            Landed,
        }

        [SerializeField] private FlightConfig _config;
        [Tooltip("The visible plane (mesh and material swap with the selected plane).")]
        [SerializeField] private MeshFilter _modelFilter;
        [SerializeField] private MeshRenderer _modelRenderer;
        [SerializeField, HideInInspector] private Rigidbody _body;
        [SerializeField, HideInInspector] private Collider _collider;

        private FlightState _state;
        private Collider _ignored;
        private float _graceLeft;
        private float _invulnerableUntil = -1f;

        /// <summary>Raised when the plane is thrown (or put back in the air by a second chance).</summary>
        public event Action Launched;

        /// <summary>Raised once per flight, with the collider that was hit (null for out-of-bounds).</summary>
        public event Action<Collider> Crashed;

        /// <summary>Raised once per flight when the plane touches a landing target, with the contact point.</summary>
        public event Action<LandingTarget, Vector3> Landed;

        public Phase CurrentPhase { get; private set; } = Phase.Ready;
        public FlightControls Controls { get; set; }
        public FlightState State => _state;
        public FlightConfig Config => _config;
        public Rigidbody Body => _body;
        public Vector3 Velocity => _body.linearVelocity;
        /// <summary>Wind acceleration applied this physics step (for telemetry and feedback).</summary>
        public Vector3 Wind { get; private set; }
        /// <summary>After a second-chance respawn: bumping into things doesn't crash (the plane blinks).</summary>
        public bool IsInvulnerable => CurrentPhase == Phase.Flying && Time.time < _invulnerableUntil;

        private void Awake()
        {
            if (_body == null) _body = GetComponent<Rigidbody>();
            if (_collider == null) _collider = GetComponentInChildren<Collider>();
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // valid kinematic and dynamic
        }

        /// <summary>Flies with this config (a per-plane variant) and shows this look. Call while parked.</summary>
        public void SetPlane(FlightConfig config, Mesh mesh, Material material)
        {
            if (config != null) _config = config;
            if (_modelFilter != null && mesh != null) _modelFilter.sharedMesh = mesh;
            if (_modelRenderer != null && material != null) _modelRenderer.sharedMaterial = material;
        }

        /// <summary>Parks the plane (kinematic) at a pose, e.g. the launch point.</summary>
        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            EndGrace();
            EndInvulnerable();
            CurrentPhase = Phase.Ready;
            Controls = default;
            Wind = Vector3.zero;
            if (!_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }
            _body.isKinematic = true;
            _body.useGravity = false;
            _body.position = position;
            _body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Releases the plane with a launch velocity. ignoreBriefly (the pad) can't crash it for LaunchGraceTime.</summary>
        public void Launch(Vector3 velocity, Collider ignoreBriefly)
        {
            _state = FlightState.Launch(velocity, _config, UnityEngine.Random.Range(0f, 100f));
            _body.isKinematic = false;
            _body.useGravity = false;
            _body.linearVelocity = velocity;
            _body.angularVelocity = Vector3.zero;
            _body.rotation = FlightModel.Orientation(_state);

            EndGrace();
            if (ignoreBriefly != null && _collider != null && _config.LaunchGraceTime > 0f)
            {
                _ignored = ignoreBriefly;
                _graceLeft = _config.LaunchGraceTime;
                Physics.IgnoreCollision(_collider, _ignored, true);
            }
            CurrentPhase = Phase.Flying;
            Launched?.Invoke();
        }

        /// <summary>Second chance: back in the air at a pose with a velocity; collisions don't crash for a while.</summary>
        public void Respawn(Vector3 position, Vector3 velocity, float invulnerableSeconds)
        {
            PlaceAt(position, Quaternion.LookRotation(velocity.sqrMagnitude > 0.01f ? velocity : transform.forward, Vector3.up));
            Launch(velocity, null);
            _invulnerableUntil = Time.time + invulnerableSeconds;
        }

        private void EndInvulnerable()
        {
            _invulnerableUntil = -1f;
            if (_modelRenderer != null) _modelRenderer.enabled = true;
        }

        private void Update()
        {
            if (_invulnerableUntil < 0f) return;
            if (!IsInvulnerable)
            {
                EndInvulnerable();
                return;
            }
            if (_modelRenderer != null) _modelRenderer.enabled = Mathf.Repeat(Time.time * 8f, 1f) < 0.6f; // blink
        }

        /// <summary>Ends the flight (also used for out-of-bounds). Safe to call more than once.</summary>
        public void Crash(Collider hit)
        {
            if (CurrentPhase != Phase.Flying) return;
            CurrentPhase = Phase.Crashed;
            EndGrace();
            EndInvulnerable();
            _body.useGravity = true; // full gravity: crashes read as heavy and quick
            Crashed?.Invoke(hit);
        }

        private void FixedUpdate()
        {
            if (CurrentPhase != Phase.Flying) return;

            if (_graceLeft > 0f)
            {
                _graceLeft -= Time.fixedDeltaTime;
                if (_graceLeft <= 0f) EndGrace();
            }

            _state.Velocity = _body.linearVelocity;
            var acceleration = FlightModel.Step(ref _state, Controls, _config, Physics.gravity, Time.fixedDeltaTime);
            Wind = WindField.Sample(_body.position, Time.time) * _config.WindResponse; // zones have no colliders: wind only pushes
            _body.linearVelocity = _state.Velocity; // includes this step's turn
            _body.AddForce(acceleration + Wind, ForceMode.Acceleration);
            _body.angularVelocity = Vector3.zero;
            _body.MoveRotation(FlightModel.Orientation(_state));
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (CurrentPhase != Phase.Flying) return;
            if (collision.collider.TryGetComponent<LandingTarget>(out var target) && collision.contactCount > 0)
            {
                var point = collision.GetContact(0).point;
                CurrentPhase = Phase.Landed;
                EndGrace();
                EndInvulnerable();
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
                _body.isKinematic = true;
                // Settle level on the surface, keeping the heading.
                var flat = _state.Heading.sqrMagnitude > 0.01f ? _state.Heading : transform.forward;
                _body.position = new Vector3(point.x, point.y + 0.03f, point.z);
                _body.rotation = Quaternion.LookRotation(flat, Vector3.up);
                Landed?.Invoke(target, point);
                return;
            }
            if (IsInvulnerable) return; // bounce off, keep flying
            Crash(collision.collider);
        }

        private void EndGrace()
        {
            if (_ignored != null && _collider != null) Physics.IgnoreCollision(_collider, _ignored, false);
            _ignored = null;
            _graceLeft = 0f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_body == null) _body = GetComponent<Rigidbody>();
            if (_collider == null) _collider = GetComponentInChildren<Collider>();
        }
#endif
    }
}

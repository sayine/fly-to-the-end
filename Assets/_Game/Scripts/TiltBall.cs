using UnityEngine;

namespace Game
{
    /// <summary>Rolls a rigidbody in the XZ plane from an input axis. Used by the placeholder game and Demo_Tilt.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TiltBall : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [SerializeField, HideInInspector] private Rigidbody _body;

        private Vector3 _start;

        /// <summary>Set every frame by the owner (x → world X, y → world Z).</summary>
        public Vector2 Axis { get; set; }
        public Rigidbody Body => _body;

        private void Awake()
        {
            if (_body == null) _body = GetComponent<Rigidbody>();
            _start = transform.position;
            _body.linearDamping = _config != null ? _config.BallDrag : 0.5f;
        }

        private void FixedUpdate()
        {
            if (_body.isKinematic || _config == null) return;
            _body.AddForce(new Vector3(Axis.x, 0f, Axis.y) * _config.BallAcceleration, ForceMode.Acceleration);

            var v = _body.linearVelocity;
            var flat = new Vector3(v.x, 0f, v.z);
            if (flat.sqrMagnitude > _config.BallMaxSpeed * _config.BallMaxSpeed)
            {
                flat = flat.normalized * _config.BallMaxSpeed;
                _body.linearVelocity = new Vector3(flat.x, v.y, flat.z);
            }
        }

        public void ResetToStart()
        {
            var wasKinematic = _body.isKinematic;
            _body.isKinematic = false;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.isKinematic = wasKinematic;
            _body.position = _start;
            transform.position = _start;
            Axis = Vector2.zero;
        }

        public void SetFrozen(bool frozen)
        {
            if (_body == null) _body = GetComponent<Rigidbody>();
            _body.isKinematic = frozen;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_body == null) _body = GetComponent<Rigidbody>();
        }
#endif
    }
}

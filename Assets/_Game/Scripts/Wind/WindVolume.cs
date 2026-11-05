using System.Collections.Generic;
using UnityEngine;

namespace Game.Wind
{
    /// <summary>
    /// One wind zone: a cone-shaped beam from transform.position along transform.forward
    /// (radius grows from StartRadius to EndRadius over Length). No collider: wind can't crash the plane.
    /// Registers itself in <see cref="WindField"/>, which the plane samples once per physics step.
    /// </summary>
    [DefaultExecutionOrder(-100)] // cache the pose before the plane's FixedUpdate samples it
    public sealed class WindVolume : MonoBehaviour
    {
        [SerializeField] private WindConfig _config;
        [SerializeField] private WindKind _kind = WindKind.Fan;
        [SerializeField, Min(0.1f)] private float _length = 10f;
        [SerializeField, Min(0.05f)] private float _startRadius = 0.4f;
        [SerializeField, Min(0.05f)] private float _endRadius = 2f;
        [Tooltip("This zone's strength relative to its kind in WindConfig (e.g. 0.55 for a gentle helper fan).")]
        [SerializeField, Range(0f, 3f)] private float _strengthScale = 1f;

        [Header("Gust (period 0 = steady)")]
        [Tooltip("Seconds per on/off cycle, e.g. a window that gusts every few seconds.")]
        [SerializeField, Min(0f)] private float _gustPeriod;
        [Tooltip("Fraction of the cycle the gust blows.")]
        [SerializeField, Range(0.05f, 1f)] private float _gustOnFraction = 0.4f;
        [Tooltip("Offset into the cycle (0..1) so several gusts don't sync.")]
        [SerializeField, Range(0f, 1f)] private float _gustPhase;

        private Vector3 _origin;
        private Vector3 _axis = Vector3.forward;
        private float _noiseSeed;

        public WindKind Kind => _kind;
        public float Length => _length;
        public float StartRadius => _startRadius;
        public float EndRadius => _endRadius;
        public bool Gusts => _gustPeriod > 0f;

        private void OnEnable()
        {
            _noiseSeed = Random.Range(0f, 100f);
            CachePose();
            WindField.Register(this);
        }

        private void OnDisable() => WindField.Unregister(this);

        private void FixedUpdate() => CachePose();

        private void CachePose()
        {
            _origin = transform.position;
            _axis = transform.forward;
        }

        /// <summary>0..1 gust envelope at time (1 for steady zones): smooth ramp up, hold, smooth ramp down, calm.</summary>
        public float GustFactor(float time)
        {
            if (_gustPeriod <= 0f) return 1f;
            var t = Mathf.Repeat(time / _gustPeriod + _gustPhase, 1f);
            if (t >= _gustOnFraction) return 0f;
            var ramp = Mathf.Min(0.12f, _gustOnFraction * 0.4f);
            return Mathf.SmoothStep(0f, 1f, t / ramp) * Mathf.SmoothStep(0f, 1f, (_gustOnFraction - t) / ramp);
        }

        /// <summary>Acceleration this zone applies at position (zero outside).</summary>
        public Vector3 Sample(Vector3 position, float time)
        {
            if (_config == null) return Vector3.zero;
            var gust = GustFactor(time);
            if (gust <= 0f) return Vector3.zero;
            var strength = gust * WindMath.BeamStrength(position - _origin, _axis, _length, _startRadius, _endRadius,
                _config.LengthFalloff, _config.EdgeSoftness);
            if (strength <= 0f) return Vector3.zero;
            var noise = Mathf.PerlinNoise(_noiseSeed + time * _config.TurbulenceFrequency, position.y * 0.5f) * 2f - 1f;
            return _axis * (strength * _strengthScale * _config.Strength(_kind) * (1f + _config.Turbulence * noise));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _config != null ? _config.ColorFor(_kind) : Color.cyan;
            var end = transform.position + transform.forward * _length;
            Gizmos.DrawWireSphere(transform.position, _startRadius);
            Gizmos.DrawWireSphere(end, _endRadius);
            Gizmos.DrawLine(transform.position, end);
        }
#endif
    }

    /// <summary>All active wind zones. Allocation-free sampling (CLAUDE.md rule 4).</summary>
    public static class WindField
    {
        private static readonly List<WindVolume> Volumes = new List<WindVolume>(8);

        internal static void Register(WindVolume v)
        {
            if (!Volumes.Contains(v)) Volumes.Add(v);
        }

        internal static void Unregister(WindVolume v) => Volumes.Remove(v);

        /// <summary>Sum of every zone's acceleration at position.</summary>
        public static Vector3 Sample(Vector3 position, float time)
        {
            var total = Vector3.zero;
            for (var i = 0; i < Volumes.Count; i++) total += Volumes[i].Sample(position, time);
            return total;
        }
    }

    /// <summary>Pure zone math (unit-tested).</summary>
    public static class WindMath
    {
        /// <summary>
        /// 0..1 strength at offset (from the zone origin) inside a cone beam along axis (unit).
        /// Along: (1 - t)^lengthFalloff. Across: 1 inside, easing to 0 over the outer edgeSoftness of the radius.
        /// </summary>
        public static float BeamStrength(Vector3 offset, Vector3 axis, float length, float startRadius, float endRadius,
            float lengthFalloff, float edgeSoftness)
        {
            var along = Vector3.Dot(offset, axis);
            if (along < 0f || along > length) return 0f;
            var t = along / length;
            var radius = Mathf.Lerp(startRadius, endRadius, t);
            var radial = (offset - axis * along).magnitude;
            if (radial >= radius) return 0f;
            var lengthFactor = Mathf.Pow(1f - t, lengthFalloff);
            var softStart = radius * (1f - Mathf.Clamp01(edgeSoftness));
            var edgeFactor = radial <= softStart ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (radial - softStart) / (radius - softStart));
            return lengthFactor * edgeFactor;
        }
    }
}

using Game.Planes;
using Template.Core;
using UnityEngine;

namespace Game.Menu
{
    /// <summary>
    /// Menu decor: a paper plane looping a lazy figure-eight above the furniture, banking into its turns.
    /// Kinematic (no physics, no wind); speed from GameConfig.
    /// </summary>
    public sealed class MenuFlyer : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [Tooltip("Figure-eight half-size (m) along X and Z, centred on Centre.")]
        [SerializeField] private Vector2 _size = new Vector2(6f, 9f);
        [SerializeField] private Vector3 _centre = new Vector3(0f, 3f, 0f);
        [SerializeField, Range(0f, 2f)] private float _bob = 0.4f;
        [Tooltip("Shows the selected plane's look (updates when the hangar changes the selection).")]
        [SerializeField] private PlaneCatalog _planes;
        [SerializeField] private MeshFilter _filter;
        [SerializeField] private MeshRenderer _renderer;

        private PlaneCollection _collection;
        private int _shownPlane = -1;

        private float _phase;
        private float _bank;
        private Vector3 _lastHeading = Vector3.forward;

        private Vector3 Point(float phase) => _centre + new Vector3(
            _size.x * Mathf.Sin(phase),
            _bob * Mathf.Sin(phase * 2f + 1f),
            _size.y * Mathf.Sin(phase) * Mathf.Cos(phase));

        private void Update()
        {
            UpdateLook();
            var dt = Time.deltaTime;
            if (_config == null || dt <= 0f) return;
            // Advance the phase so the plane moves at a roughly constant speed along the curve.
            var tangent = Point(_phase + 0.01f) - Point(_phase);
            _phase += _config.MenuFlyerSpeed * dt * 0.01f / Mathf.Max(0.001f, tangent.magnitude);

            var position = Point(_phase);
            var heading = (Point(_phase + 0.01f) - position).normalized;
            var flat = heading.Flat();
            var turn = Vector3.SignedAngle(_lastHeading.Flat(), flat, Vector3.up) / dt; // deg/s
            _lastHeading = heading;
            _bank = Mathf.Lerp(_bank, Mathf.Clamp(turn * 0.6f, -40f, 40f), MathExtensions.SmoothingFactor(3f, dt));
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(heading) * Quaternion.Euler(0f, 0f, -_bank));
        }

        private void UpdateLook()
        {
            if (_planes == null || _planes.Count == 0) return;
            _collection ??= PlaneCollection.Get(_planes);
            var selected = _collection.SelectedIndex;
            if (selected == _shownPlane) return;
            _shownPlane = selected;
            var plane = _planes.Get(selected);
            if (_filter != null && plane.Mesh != null) _filter.sharedMesh = plane.Mesh;
            if (_renderer != null && plane.Material != null) _renderer.sharedMaterial = plane.Material;
        }
    }
}

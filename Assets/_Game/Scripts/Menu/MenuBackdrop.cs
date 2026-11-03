using Template.Core;
using UnityEngine;

namespace Game.Menu
{
    /// <summary>
    /// Menu camera: slowly circles the room on an ellipse (GameConfig "Menu backdrop") and keeps the decor
    /// plane in frame by looking between the room centre and the plane.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class MenuBackdrop : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private Transform _flyer;
        [SerializeField] private Vector3 _lookCentre = new Vector3(0f, 1.6f, 0f);

        private float _angle = 200f;
        private Vector3 _look;

        private void Start()
        {
            _look = _lookCentre;
            Place(1f);
        }

        private void LateUpdate() => Place(MathExtensions.SmoothingFactor(2f, Time.deltaTime));

        private void Place(float lookBlend)
        {
            if (_config == null) return;
            _angle += _config.MenuOrbitSpeed * Time.deltaTime;
            var rad = _angle * Mathf.Deg2Rad;
            transform.position = new Vector3(_config.MenuOrbitRadiusX * Mathf.Sin(rad), _config.MenuOrbitHeight,
                _config.MenuOrbitRadiusZ * Mathf.Cos(rad));
            var target = _flyer != null ? Vector3.Lerp(_lookCentre, _flyer.position, 0.85f) : _lookCentre;
            _look = Vector3.Lerp(_look, target, lookBlend);
            transform.rotation = Quaternion.LookRotation(_look - transform.position);
        }
    }
}

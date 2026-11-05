using System;
using UnityEngine;

namespace Game.Levels
{
    /// <summary>A gem to fly through: spins and bobs, pops when collected.</summary>
    public sealed class Gem : MissionTrigger
    {
        [SerializeField] private Transform _visual;
        [SerializeField] private float _spinSpeed = 120f;
        [SerializeField] private float _bob = 0.12f;

        private Action<Gem> _collected;
        private float _popTime = -1f;
        private float _phase;

        public void Place(Vector3 position, Rigidbody plane, Action<Gem> collected)
        {
            transform.position = position;
            _collected = collected;
            _popTime = -1f;
            _phase = position.x * 0.7f + position.z * 0.3f;
            if (_visual != null) _visual.localScale = Vector3.one;
            Arm(plane);
        }

        protected override void OnPlaneEntered()
        {
            if (_popTime >= 0f) return;
            _popTime = 0f;
            _collected?.Invoke(this);
        }

        private void Update()
        {
            if (_visual == null) return;
            var dt = Time.deltaTime;
            if (_popTime >= 0f)
            {
                // Quick swell and vanish.
                _popTime += dt;
                var t = _popTime / 0.25f;
                _visual.localScale = Vector3.one * (t < 0.4f ? 1f + t : Mathf.Max(0f, 1.4f - (t - 0.4f) * 2.4f));
                if (t >= 1f) Disarm();
                return;
            }
            _visual.Rotate(0f, _spinSpeed * dt, 0f, Space.World);
            _visual.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 2.2f + _phase) * _bob, 0f);
        }
    }
}

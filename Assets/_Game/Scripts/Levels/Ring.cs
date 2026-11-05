using System;
using UnityEngine;

namespace Game.Levels
{
    /// <summary>A ring to fly through in order. Only its hole is a trigger: brushing the frame never crashes.</summary>
    public sealed class Ring : MissionTrigger
    {
        public enum State { Waiting, Next, Passed }

        [SerializeField] private Renderer _renderer;
        [SerializeField] private Color _waiting = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color _next = new Color(1f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color _passed = new Color(0.4f, 0.9f, 0.5f, 1f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _block;
        private Func<int, bool> _passedCallback;
        private int _index;
        private float _pulse;

        public void Place(RingSpec spec, int index, Rigidbody plane, Func<int, bool> passed)
        {
            transform.SetPositionAndRotation(spec.Position, Quaternion.Euler(0f, spec.Yaw, 0f));
            transform.localScale = Vector3.one * spec.Radius;
            _index = index;
            _passedCallback = passed;
            Arm(plane);
            SetState(index == 0 ? State.Next : State.Waiting);
        }

        public void SetState(State state)
        {
            _block ??= new MaterialPropertyBlock();
            _pulse = state == State.Next ? 1f : 0f;
            _block.SetColor(BaseColorId, state == State.Next ? _next : state == State.Passed ? _passed : _waiting);
            if (_renderer != null) _renderer.SetPropertyBlock(_block);
        }

        protected override void OnPlaneEntered() => _passedCallback?.Invoke(_index);

        private void Update()
        {
            if (_pulse <= 0f || _renderer == null) return;
            // The next ring breathes a little so it reads as "this one".
            var s = 1f + Mathf.Sin(Time.time * 4f) * 0.04f;
            _renderer.transform.localScale = new Vector3(s, s, 1f);
        }
    }
}

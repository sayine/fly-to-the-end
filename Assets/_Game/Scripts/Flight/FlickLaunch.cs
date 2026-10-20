using Template.Core;
using UnityEngine;

namespace Game.Flight
{
    /// <summary>
    /// Single-finger flick tracking from IPlayerInput pointer state (allocation-free ring buffer).
    /// Drag = press start → current position in screen heights. Speed = movement over the last
    /// FlickWindow seconds, so a slow aim followed by a quick flick counts as quick.
    /// </summary>
    public sealed class FlickGesture
    {
        private const int Capacity = 32;
        private readonly Vector2[] _positions = new Vector2[Capacity];
        private readonly float[] _times = new float[Capacity];
        private int _count;
        private int _head;
        private Vector2 _start;

        public bool Active { get; private set; }
        public Vector2 Start => _start;
        /// <summary>Press start → latest position, in screen heights.</summary>
        public Vector2 Drag { get; private set; }

        public void Begin(Vector2 position, float time)
        {
            Active = true;
            _start = position;
            _count = 0;
            _head = 0;
            Drag = Vector2.zero;
            Add(position, time);
        }

        public void Add(Vector2 position, float time)
        {
            if (!Active) return;
            _positions[_head] = position;
            _times[_head] = time;
            _head = (_head + 1) % Capacity;
            if (_count < Capacity) _count++;
            Drag = (position - _start) / Mathf.Max(1, Screen.height);
        }

        public void Cancel() => Active = false;

        /// <summary>Recent speed in screen heights per second.</summary>
        public float Speed(float window)
        {
            if (_count < 2) return 0f;
            var newest = (_head - 1 + Capacity) % Capacity;
            var oldest = newest;
            for (var i = 1; i < _count; i++)
            {
                var index = (newest - i + Capacity) % Capacity;
                oldest = index;
                if (_times[newest] - _times[index] >= window) break;
            }
            var dt = _times[newest] - _times[oldest];
            if (dt <= 1e-4f) return 0f;
            return (_positions[newest] - _positions[oldest]).magnitude / Mathf.Max(1, Screen.height) / dt;
        }
    }

    /// <summary>
    /// Turns a flick into a launch velocity, clamped to a forward cone (SPEC "Phase 1 — Launch").
    /// The cone's upper pitch limit narrows with power, so hard throws go fast and flat, soft ones can be lobbed.
    /// </summary>
    public static class LaunchAim
    {
        public struct Result
        {
            public Vector3 Velocity;
            public float Speed;
            public float Pitch;
            public float Yaw;
            /// <summary>0 at MinLaunchSpeed, 1 at MaxLaunchSpeed.</summary>
            public float Power;
        }

        /// <summary>
        /// Direction: the swipe direction is projected AimScreenDistance from the plane on screen, then AimDepth
        /// along that camera ray: you throw toward where you swipe. Short swipes/taps go straight at DefaultLaunchPitch.
        /// Speed: flick speed remapped to [MinLaunchSpeed, MaxLaunchSpeed]; weak swipes and taps launch at min.
        /// </summary>
        public static Result Solve(Vector2 drag, float flickSpeed, Camera camera, Vector3 planePosition, Vector3 launchForward, FlightConfig cfg)
        {
            var basis = Quaternion.LookRotation(launchForward.Flat().sqrMagnitude > 1e-4f ? launchForward.Flat() : Vector3.forward);
            float pitch, yaw;
            if (drag.magnitude < cfg.MinAimSwipe || camera == null)
            {
                pitch = cfg.DefaultLaunchPitch;
                yaw = 0f;
            }
            else
            {
                var planeScreen = (Vector2)camera.WorldToScreenPoint(planePosition);
                var aimScreen = planeScreen + drag.normalized * (cfg.AimScreenDistance * Screen.height);
                var target = camera.ScreenPointToRay(aimScreen).GetPoint(cfg.AimDepth);
                var local = Quaternion.Inverse(basis) * (target - planePosition);
                yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                pitch = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            }
            var power = Mathf.Clamp01(Mathf.InverseLerp(cfg.FlickSpeedForMin, cfg.FlickSpeedForMax, flickSpeed));
            var maxPitch = Mathf.Lerp(cfg.MaxLaunchPitch, cfg.MaxLaunchPitchAtFullPower, power);
            pitch = Mathf.Clamp(pitch, cfg.MinLaunchPitch, maxPitch);
            yaw = Mathf.Clamp(yaw, -cfg.MaxLaunchYaw, cfg.MaxLaunchYaw);

            var speed = Mathf.Lerp(cfg.MinLaunchSpeed, cfg.MaxLaunchSpeed, power);
            var direction = basis * Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
            return new Result { Velocity = direction * speed, Speed = speed, Pitch = pitch, Yaw = yaw, Power = power };
        }
    }
}

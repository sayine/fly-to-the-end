using Template.Core;
using UnityEngine;

namespace Template.Services.Input
{
    /// <summary>Pure tilt math (unit-tested in TiltMathTests).</summary>
    public static class TiltMath
    {
        private static readonly Vector3 FlatDown = new Vector3(0f, 0f, -1f);

        /// <summary>Rotation that maps the neutral pose onto "lying flat": tilt is then measured relative to it.</summary>
        public static Quaternion Calibrate(Vector3 neutralGravity) =>
            Quaternion.FromToRotation(neutralGravity.normalized, FlatDown);

        /// <summary>
        /// Gravity (device space) → axis in [-1, 1]. x: tilt right = +; y: top edge away/down = +.
        /// maxTiltAngle degrees from neutral maps to 1. Radial dead zone, rescaled to stay continuous.
        /// </summary>
        public static Vector2 ToAxis(Quaternion calibration, Vector3 gravity, float maxTiltAngle, float sensitivity,
            float deadZone, bool invertX, bool invertY)
        {
            var g = calibration * gravity.normalized;
            // g.x / g.y are the sines of the roll / pitch angles away from neutral.
            var maxSin = Mathf.Sin(Mathf.Clamp(maxTiltAngle, 1f, 89f) * Mathf.Deg2Rad);
            var axis = new Vector2(g.x, g.y) / maxSin;
            if (invertX) axis.x = -axis.x;
            if (invertY) axis.y = -axis.y;
            axis *= sensitivity;

            var magnitude = Mathf.Min(1f, axis.magnitude);
            var scaled = magnitude.ApplyDeadZone(deadZone);
            return scaled <= 0f ? Vector2.zero : axis.normalized * scaled;
        }
    }
}

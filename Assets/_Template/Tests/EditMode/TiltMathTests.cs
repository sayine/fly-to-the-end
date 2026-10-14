using NUnit.Framework;
using Template.Services.Input;
using UnityEngine;

namespace Template.Tests
{
    public class TiltMathTests
    {
        private const float Max = 25f;
        private const float Dz = 0.08f;
        private static readonly Vector3 Upright = new Vector3(0f, -1f, 0f); // portrait, held vertical
        private static readonly Vector3 Flat = new Vector3(0f, 0f, -1f);    // lying on a table, screen up

        private static Vector2 Axis(Vector3 neutral, Vector3 gravity, bool invertX = false, float dz = Dz) =>
            TiltMath.ToAxis(TiltMath.Calibrate(neutral), gravity, Max, 1f, dz, invertX, false);

        [Test]
        public void NeutralPose_GivesZero_ForAnyHoldingAngle()
        {
            Assert.AreEqual(Vector2.zero, Axis(Upright, Upright));
            Assert.AreEqual(Vector2.zero, Axis(Flat, Flat));
            var tilted = new Vector3(0f, -0.7f, -0.7f);
            Assert.AreEqual(Vector2.zero, Axis(tilted, tilted));
        }

        [Test]
        public void TiltRight_ByMaxAngle_GivesFullPositiveX()
        {
            var a = Max * Mathf.Deg2Rad;
            var axis = Axis(Upright, new Vector3(Mathf.Sin(a), -Mathf.Cos(a), 0f));
            Assert.AreEqual(1f, axis.x, 0.01f);
            Assert.AreEqual(0f, axis.y, 0.01f);
        }

        [Test]
        public void TopEdgeDown_FromFlat_GivesPositiveY_RescaledByDeadZone()
        {
            var a = Max * 0.5f * Mathf.Deg2Rad; // half of max tilt
            var axis = Axis(Flat, new Vector3(0f, Mathf.Sin(a), -Mathf.Cos(a)));
            var raw = Mathf.Sin(a) / Mathf.Sin(Max * Mathf.Deg2Rad);
            Assert.AreEqual(0f, axis.x, 0.001f);
            Assert.AreEqual((raw - Dz) / (1f - Dz), axis.y, 0.01f);
        }

        [Test]
        public void SmallTilt_InsideDeadZone_IsZero()
        {
            var a = 1f * Mathf.Deg2Rad;
            Assert.AreEqual(Vector2.zero, Axis(Flat, new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a))));
        }

        [Test]
        public void LargeTilt_IsClampedToUnitLength()
        {
            var a = 70f * Mathf.Deg2Rad;
            var axis = Axis(Flat, new Vector3(Mathf.Sin(a), Mathf.Sin(a), -1f));
            Assert.LessOrEqual(axis.magnitude, 1.0001f);
        }

        [Test]
        public void InvertX_FlipsSign()
        {
            var a = 10f * Mathf.Deg2Rad;
            var g = new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a));
            Assert.Greater(Axis(Flat, g).x, 0f);
            Assert.Less(Axis(Flat, g, invertX: true).x, 0f);
        }

        [Test]
        public void Magnitude_IsIndependentOfGravityScale()
        {
            var a = 10f * Mathf.Deg2Rad;
            var g = new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a));
            Assert.AreEqual(Axis(Flat, g).x, Axis(Flat, g * 9.81f).x, 1e-4f);
        }
    }
}

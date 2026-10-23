using Game.Wind;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class WindMathTests
    {
        private static float Strength(Vector3 offset) =>
            WindMath.BeamStrength(offset, Vector3.forward, 10f, 0.5f, 2f, 0.5f, 0.4f);

        [Test]
        public void FullStrengthAtSourceCentre() => Assert.AreEqual(1f, Strength(Vector3.zero), 1e-4f);

        [Test]
        public void ZeroBehindSourceAndPastEnd()
        {
            Assert.AreEqual(0f, Strength(new Vector3(0f, 0f, -0.1f)));
            Assert.AreEqual(0f, Strength(new Vector3(0f, 0f, 10.1f)));
        }

        [Test]
        public void ZeroOutsideTheConeRadius()
        {
            Assert.AreEqual(0f, Strength(new Vector3(0.6f, 0f, 0f)), "start radius 0.5");
            Assert.Greater(Strength(new Vector3(1.0f, 0f, 9f)), 0f, "cone has widened to ~1.85 m near the end");
        }

        [Test]
        public void FadesAlongTheZoneAndTowardTheEdge()
        {
            Assert.Greater(Strength(new Vector3(0f, 0f, 2f)), Strength(new Vector3(0f, 0f, 8f)));
            Assert.Greater(Strength(new Vector3(0f, 0f, 5f)), Strength(new Vector3(1.1f, 0f, 5f)));
        }
    }
}

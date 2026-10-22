using Game.Flight;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Integrates FlightModel with default FlightConfig values (explicit Euler at 50 Hz, like FixedUpdate)
    /// and checks the feel targets: ~10:1 physical glide, ~30:1 with the arcade assist (long runs that end on a
    /// crash), fast throws don't balloon into the ceiling, stalls recover, banking turns the right way. Prints numbers so tuning changes are easy to compare.
    /// </summary>
    public class FlightModelTests
    {
        private const float Dt = 0.02f;
        private const float Ceiling = 7f;
        private const float PadHeight = 1.98f;
        private FlightConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = ScriptableObject.CreateInstance<FlightConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_cfg);

        private struct Result
        {
            public float Time, Distance, MaxHeight, EndSpeed;
            public bool HitCeiling, EverStalled, StalledAtEnd;
            public Vector3 Position;
        }

        private Result Fly(float speed, float pitchDeg, float height, FlightControls controls, float maxTime = 30f, float ceiling = Ceiling)
        {
            var velocity = Quaternion.Euler(-pitchDeg, 0f, 0f) * Vector3.forward * speed;
            var s = FlightState.Launch(velocity, _cfg, 0f);
            var p = new Vector3(0f, height, 0f);
            var r = new Result { MaxHeight = height };
            while (r.Time < maxTime)
            {
                var a = FlightModel.Step(ref s, controls, _cfg, Physics.gravity, Dt);
                s.Velocity += a * Dt;
                p += s.Velocity * Dt;
                r.Time += Dt;
                r.MaxHeight = Mathf.Max(r.MaxHeight, p.y);
                r.EverStalled |= s.Stalled;
                if (p.y <= 0f) break;
                if (p.y >= ceiling) { r.HitCeiling = true; break; }
            }
            r.Distance = new Vector2(p.x, p.z).magnitude;
            r.EndSpeed = s.Speed;
            r.StalledAtEnd = s.Stalled;
            r.Position = p;
            return r;
        }

        [Test]
        public void PhysicalGlide_WithoutAssist_IsAboutTenToOne()
        {
            JsonUtility.FromJsonOverwrite("{\"_glideAssist\":0,\"_verticalDamping\":0}", _cfg);
            var r = Fly(5f, -5.7f, 40f, default, 120f, float.MaxValue);
            var ratio = r.Distance / 40f;
            Debug.Log($"[Flight] physical glide ratio {ratio:0.0}:1, speed {r.EndSpeed:0.00} m/s");
            Assert.That(ratio, Is.InRange(8f, 12f));
            Assert.IsFalse(r.EverStalled, "steady glide must not stall");
        }

        [Test]
        public void ArcadeGlide_WithDefaults_IsLongAndSlowSinking()
        {
            var r = Fly(5f, -2f, 40f, default, 300f, float.MaxValue);
            var ratio = r.Distance / 40f;
            Debug.Log($"[Flight] arcade glide ratio {ratio:0.0}:1, sink {40f / r.Time:0.00} m/s, speed {r.EndSpeed:0.00} m/s");
            Assert.GreaterOrEqual(ratio, 20f);
            Assert.IsFalse(r.EverStalled);
        }

        [TestCase(5f, 0f)]
        [TestCase(5f, 12f)]
        [TestCase(6.5f, 10f)]
        [TestCase(8.5f, 5f)]
        [TestCase(8.5f, -5f)]
        public void LaunchFromPad_NoInput_FliesLongWithoutHittingCeiling(float speed, float pitch)
        {
            var r = Fly(speed, pitch, PadHeight, default);
            Debug.Log($"[Flight] launch {speed} m/s @ {pitch}°: {r.Time:0.0}s, {r.Distance:0.0} m, peak {r.MaxHeight:0.00} m");
            Assert.IsFalse(r.HitCeiling, $"balloons into the ceiling (peak {r.MaxHeight:0.00} m)");
            Assert.Greater(r.Time, 8f, "in open air an untouched throw should glide a long time; walls/props end runs");
        }

        [Test]
        public void HoldingNoseUp_Stalls_ThenRecoversWhenReleased()
        {
            var velocity = Vector3.forward * 6f;
            var s = FlightState.Launch(velocity, _cfg, 0f);
            var up = new FlightControls { Pitch = 1f };
            var stalled = false;
            for (var i = 0; i < 400 && !stalled; i++) { s.Velocity += FlightModel.Step(ref s, up, _cfg, Physics.gravity, Dt) * Dt; stalled = s.Stalled; }
            Assert.IsTrue(stalled, "full nose-up should eventually stall");

            var recovered = false;
            for (var i = 0; i < 300 && !recovered; i++) { s.Velocity += FlightModel.Step(ref s, default, _cfg, Physics.gravity, Dt) * Dt; recovered = !s.Stalled; }
            Assert.IsTrue(recovered, "stall must recover within 6 s with neutral input");
        }

        [Test]
        public void BankRight_TurnsRight()
        {
            var r = Fly(6f, 5f, 3f, new FlightControls { Bank = 1f }, 1f);
            Assert.Greater(r.Position.x, 0.2f);
        }

        [Test]
        public void Orientation_NoseFollowsVelocity_AndRollsWithBank()
        {
            var s = FlightState.Launch(new Vector3(0f, 0f, 5f), _cfg, 0f);
            s.EffectiveAoa = 0f;
            s.EffectiveBank = 30f;
            var rot = FlightModel.Orientation(s);
            Assert.Greater(Vector3.Dot(rot * Vector3.forward, Vector3.forward), 0.99f);
            Assert.Less((rot * Vector3.right).y, -0.4f, "right wing down when banking right");

            // Straight-down velocity must not produce NaN (no gimbal flip).
            s.Velocity = Vector3.down * 4f;
            rot = FlightModel.Orientation(s);
            Assert.IsFalse(float.IsNaN(rot.x));
        }

        [Test]
        public void TiltShape_DeadZoneExpoSensitivity()
        {
            Assert.AreEqual(0f, TiltSteering.Shape(0.03f, 0.05f, 1.5f, 1f));
            Assert.AreEqual(1f, TiltSteering.Shape(1f, 0.05f, 1.5f, 1f), 1e-4f);
            Assert.AreEqual(-1f, TiltSteering.Shape(-2f, 0.05f, 1f, 3f), 1e-4f);
            Assert.Less(TiltSteering.Shape(0.5f, 0f, 2f, 1f), 0.5f, "expo > 1 softens the centre");
        }

        [Test]
        public void LaunchAim_TapGoesStraightAtMinSpeed()
        {
            var r = LaunchAim.Solve(Vector2.zero, 0f, null, Vector3.zero, Vector3.forward, _cfg);
            Assert.AreEqual(_cfg.MinLaunchSpeed, r.Speed, 1e-4f);
            Assert.AreEqual(0f, r.Yaw, 1e-4f);
            Assert.AreEqual(_cfg.DefaultLaunchPitch, r.Pitch, 1e-4f);
            Assert.Greater(r.Velocity.z, 0f);
        }

        [Test]
        public void LaunchAim_FastFlickIsClampedToMax_AndFlat()
        {
            var r = LaunchAim.Solve(Vector2.zero, 100f, null, Vector3.zero, Vector3.forward, _cfg);
            Assert.AreEqual(_cfg.MaxLaunchSpeed, r.Speed, 1e-4f);
            Assert.LessOrEqual(r.Pitch, _cfg.MaxLaunchPitchAtFullPower + 1e-4f);
        }

        [Test]
        public void SoftCap_IdentityBelowKnee_NeverAboveCap()
        {
            Assert.AreEqual(4f, FlightModel.SoftCap(4f, 5f, 0.1f), 1e-5f);
            Assert.Less(FlightModel.SoftCap(100f, 5f, 0.1f), 5f + 1e-4f);
            Assert.Greater(FlightModel.SoftCap(4.8f, 5f, 0.1f), 4.6f);
        }
    }
}

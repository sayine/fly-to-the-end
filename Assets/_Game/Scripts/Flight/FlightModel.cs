using Template.Core;
using UnityEngine;

namespace Game.Flight
{
    /// <summary>Normalized control input. Bank: + = right wing down (turn right). Pitch: + = nose up.</summary>
    public struct FlightControls
    {
        public float Bank;
        public float Pitch;
    }

    /// <summary>Everything the flight model integrates besides position. Velocity is owned by the caller's body.</summary>
    public struct FlightState
    {
        public Vector3 Velocity;
        /// <summary>Flat unit forward. Kept when the velocity is near vertical so orientation never flips.</summary>
        public Vector3 Heading;
        /// <summary>Angle of attack (deg) from control/stall, before flutter. + = nose above the flight path.</summary>
        public float Aoa;
        /// <summary>Bank (deg) from control, before flutter. + = right wing down.</summary>
        public float Bank;
        /// <summary>Aoa/Bank including flutter and stall wobble: what lift uses and what the plane shows.</summary>
        public float EffectiveAoa;
        public float EffectiveBank;
        public bool Stalled;
        public float Time;
        public float NoiseSeed;

        public float Speed => Velocity.magnitude;

        public static FlightState Launch(Vector3 velocity, FlightConfig config, float noiseSeed)
        {
            var flat = velocity.Flat();
            return new FlightState
            {
                Velocity = velocity,
                Heading = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward,
                Aoa = config.TrimAoa,
                EffectiveAoa = config.TrimAoa,
                NoiseSeed = noiseSeed,
            };
        }
    }

    /// <summary>
    /// Arcade paper-plane model (SPEC "Flight model"), pure and allocation-free so it is unit-testable.
    /// The nose is never simulated as a free rigid body: orientation = flight path + AoA + bank, so the plane
    /// always weathervanes into its velocity and can't gimbal-lock or spin out.
    ///   Lift  = LiftCoef * v² * LiftCurve(AoA), perpendicular to the path in the vertical plane, capped at MaxLiftG (soft knee).
    ///   Drag  = (DragCoef + InducedDrag * AoA²) * v², opposite to the path.
    ///   Turn  = yaw rate Bank * TurnRate applied directly to the velocity (no sideslip).
    ///   Stall = speed &lt; StallSpeed or AoA &gt; StallAngle: nose drops, lift collapses, gentle wobble, recover by gaining speed.
    /// </summary>
    public static class FlightModel
    {
        /// <summary>
        /// Advances state by dt: updates bank/AoA/stall, rotates state.Velocity for the turn, and returns the
        /// acceleration (gravity + lift + drag) for the caller to integrate this step.
        /// </summary>
        public static Vector3 Step(ref FlightState s, FlightControls controls, FlightConfig cfg, Vector3 gravity, float dt)
        {
            s.Time += dt;
            var speed = s.Velocity.magnitude;
            var dir = speed > 1e-3f ? s.Velocity / speed : s.Heading;
            var flat = dir.Flat();
            if (flat.sqrMagnitude > 0.01f) s.Heading = flat.normalized;

            // Bank follows input.
            var bankInput = Mathf.Clamp(controls.Bank, -1f, 1f);
            s.Bank = Mathf.Lerp(s.Bank, bankInput * cfg.MaxBankAngle, MathExtensions.SmoothingFactor(cfg.BankResponse, dt));

            // Stall with hysteresis.
            if (!s.Stalled)
            {
                if (speed < cfg.StallSpeed || s.Aoa > cfg.StallAngle) s.Stalled = true;
            }
            else if (speed > cfg.StallSpeed * cfg.StallRecoverySpeedFactor && s.Aoa < cfg.StallAngle - 2f)
            {
                s.Stalled = false;
            }

            // AoA follows input (weathervane). While stalled the nose drops; pitching down still helps recovery.
            var pitch = Mathf.Clamp(controls.Pitch, -1f, 1f);
            var targetAoa = s.Stalled ? cfg.StallNoseAoa : cfg.TrimAoa;
            if (pitch > 0f) { if (!s.Stalled) targetAoa += pitch * cfg.PitchUpAuthority; }
            else targetAoa += pitch * cfg.PitchDownAuthority;
            s.Aoa = Mathf.Lerp(s.Aoa, targetAoa, MathExtensions.SmoothingFactor(cfg.PitchResponse, dt));

            // Flutter (Perlin, still air) + stall wobble.
            var t = s.Time * cfg.FlutterFrequency;
            var flutterAoa = (Mathf.PerlinNoise(s.NoiseSeed, t) * 2f - 1f) * cfg.FlutterAmplitude;
            var flutterBank = (Mathf.PerlinNoise(s.NoiseSeed + 31.7f, t) * 2f - 1f) * cfg.FlutterAmplitude;
            if (s.Stalled)
            {
                flutterAoa += Mathf.Sin(s.Time * 5.3f) * cfg.StallWobble * 0.5f;
                flutterBank += Mathf.Sin(s.Time * 3.7f) * cfg.StallWobble;
            }
            s.EffectiveAoa = s.Aoa + flutterAoa;
            s.EffectiveBank = s.Bank + flutterBank;

            // Forces (as accelerations).
            var right = Vector3.Cross(Vector3.up, s.Heading);
            var liftDir = Vector3.Cross(dir, right).normalized;
            var g = gravity.magnitude * cfg.GravityScale;

            var lift = cfg.LiftCoef * speed * speed * cfg.LiftCurve.Evaluate(s.EffectiveAoa);
            lift *= Mathf.Lerp(1f, Mathf.Cos(s.EffectiveBank * Mathf.Deg2Rad), cfg.BankLiftLoss);
            if (s.Stalled) lift *= cfg.StallLiftMultiplier;
            lift = SoftCap(lift, cfg.MaxLiftG * g, cfg.LiftCapSoftness);

            var aoaRad = s.EffectiveAoa * Mathf.Deg2Rad;
            var drag = (cfg.DragCoef + cfg.InducedDragCoef * aoaRad * aoaRad) * speed * speed;

            // Banked turn: rotate the path around world up.
            var yaw = s.EffectiveBank * cfg.TurnRate * dt;
            if (s.Stalled) yaw *= cfg.StallLiftMultiplier;
            if (yaw != 0f)
            {
                var turn = Quaternion.AngleAxis(yaw, Vector3.up);
                s.Velocity = turn * s.Velocity;
                s.Heading = turn * s.Heading;
            }

            return gravity * cfg.GravityScale + liftDir * lift - dir * drag;
        }

        /// <summary>Identity below cap * (1 - softness), then eases into cap (continuous slope, never exceeds cap).</summary>
        public static float SoftCap(float value, float cap, float softness)
        {
            cap = Mathf.Max(0.01f, cap);
            var band = cap * Mathf.Clamp(softness, 0.01f, 1f);
            var knee = cap - band;
            if (value <= knee) return value;
            return knee + band * (float)System.Math.Tanh((value - knee) / band);
        }

        /// <summary>World rotation of the plane for the current state (nose = path pitched up by AoA, rolled by bank).</summary>
        public static Quaternion Orientation(in FlightState s)
        {
            var speed = s.Velocity.magnitude;
            var dir = speed > 0.05f ? s.Velocity / speed : s.Heading;
            var right = Vector3.Cross(Vector3.up, s.Heading);
            var nose = Quaternion.AngleAxis(-s.EffectiveAoa, right) * dir;
            var noseUp = Vector3.Cross(nose, right);
            return Quaternion.LookRotation(nose, noseUp) * Quaternion.Euler(0f, 0f, -s.EffectiveBank);
        }
    }
}

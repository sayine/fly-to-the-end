using UnityEngine;

namespace Game.Levels
{
    public enum MissionType
    {
        FreeFlight, // the original endless mode: no goal, a crash ends the run (Game Over)
        Airtime,    // stay in the air N seconds
        Land,       // touch down softly on a target
        Gems,       // fly through gems
        Rings,      // fly through rings in order
        ReachRoom,  // fly into a target room
    }

    public enum MissionOutcome
    {
        Running,
        Success,
        Failed,
    }

    /// <summary>
    /// Mission rules (SPEC §2), pure and allocation-free. Stars thresholds per type:
    ///   Airtime   seconds for 1/2/3 stars; the run goes on after the goal and is judged when it ends.
    ///   Land      [unused, radius for 2 stars, radius for 3 stars]: any soft landing on the target = 1 star.
    ///   Gems      fraction collected for 1/2/3 stars; all gems ends the level at once with 3 stars.
    ///   Rings     [unused, seconds for 2 stars, seconds for 3 stars]: all rings in order = 1 star, ends at once.
    ///   ReachRoom [unused, seconds for 2 stars, seconds for 3 stars]: entering the room = 1 star, ends at once.
    /// </summary>
    public sealed class MissionTracker
    {
        private readonly float[] _stars;

        public MissionType Type { get; }
        public int GemTotal { get; }
        public int RingTotal { get; }
        public float Time { get; private set; }
        public int Gems { get; private set; }
        public int NextRing { get; private set; }
        public MissionOutcome Outcome { get; private set; }
        public int Stars { get; private set; }

        public MissionTracker(MissionType type, float[] stars, int gemTotal, int ringTotal)
        {
            Type = type;
            _stars = stars != null && stars.Length >= 3 ? stars : new[] { 0f, 0f, 0f };
            GemTotal = gemTotal;
            RingTotal = ringTotal;
        }

        /// <summary>Seconds needed (Airtime) — first star threshold.</summary>
        public float Goal => _stars[0];
        public bool IsFreeFlight => Type == MissionType.FreeFlight;
        /// <summary>The level's pass condition is already met (Airtime: goal time reached).</summary>
        public bool GoalReached => Type == MissionType.Airtime && Time >= _stars[0];

        public void Tick(float deltaTime)
        {
            if (Outcome == MissionOutcome.Running) Time += deltaTime;
        }

        public void CollectGem()
        {
            if (Outcome != MissionOutcome.Running || Type != MissionType.Gems) return;
            Gems = Mathf.Min(GemTotal, Gems + 1);
            if (Gems == GemTotal) Succeed(3);
        }

        /// <summary>Returns true if this was the next ring in order (rings out of order don't count).</summary>
        public bool PassRing(int index)
        {
            if (Outcome != MissionOutcome.Running || Type != MissionType.Rings || index != NextRing) return false;
            NextRing++;
            if (NextRing == RingTotal) Succeed(TimeStars());
            return true;
        }

        public void ReachedRoom()
        {
            if (Outcome != MissionOutcome.Running || Type != MissionType.ReachRoom) return;
            Succeed(TimeStars());
        }

        /// <summary>A soft landing on the target, distance (m) from its centre.</summary>
        public void LandedOnTarget(float distance)
        {
            if (Outcome != MissionOutcome.Running || Type != MissionType.Land) return;
            Succeed(distance <= _stars[2] ? 3 : distance <= _stars[1] ? 2 : 1);
        }

        /// <summary>The flight ended (crash, or a landing that was not on the target). Judges what is left.</summary>
        public void FlightEnded()
        {
            if (Outcome != MissionOutcome.Running) return;
            switch (Type)
            {
                case MissionType.Airtime:
                    var stars = Time >= _stars[2] ? 3 : Time >= _stars[1] ? 2 : Time >= _stars[0] ? 1 : 0;
                    if (stars > 0) Succeed(stars);
                    else Fail();
                    break;
                case MissionType.Gems:
                    var fraction = GemTotal > 0 ? Gems / (float)GemTotal : 0f;
                    var gemStars = fraction >= _stars[2] ? 3 : fraction >= _stars[1] ? 2 : fraction >= _stars[0] ? 1 : 0;
                    if (gemStars > 0) Succeed(gemStars);
                    else Fail();
                    break;
                default:
                    Fail();
                    break;
            }
        }

        /// <summary>0..1 progress toward the pass condition, for the HUD.</summary>
        public float Progress01 => Type switch
        {
            MissionType.Airtime => _stars[0] > 0f ? Mathf.Clamp01(Time / _stars[0]) : 0f,
            MissionType.Gems => GemTotal > 0 ? Gems / (float)GemTotal : 0f,
            MissionType.Rings => RingTotal > 0 ? NextRing / (float)RingTotal : 0f,
            _ => Outcome == MissionOutcome.Success ? 1f : 0f,
        };

        private int TimeStars() => Time <= _stars[2] ? 3 : Time <= _stars[1] ? 2 : 1;

        private void Succeed(int stars)
        {
            Outcome = MissionOutcome.Success;
            Stars = Mathf.Clamp(stars, 1, 3);
        }

        private void Fail()
        {
            Outcome = MissionOutcome.Failed;
            Stars = 0;
        }
    }
}

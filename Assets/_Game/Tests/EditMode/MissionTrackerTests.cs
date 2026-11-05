using Game.Levels;
using NUnit.Framework;

namespace Game.Tests
{
    public class MissionTrackerTests
    {
        [Test]
        public void Airtime_ContinuesPastGoal_StarsByFinalTime()
        {
            var m = new MissionTracker(MissionType.Airtime, new[] { 10f, 15f, 20f }, 0, 0);
            m.Tick(12f);
            Assert.IsTrue(m.GoalReached);
            Assert.AreEqual(MissionOutcome.Running, m.Outcome, "the run goes on after the goal");
            m.Tick(4f);
            m.FlightEnded();
            Assert.AreEqual(MissionOutcome.Success, m.Outcome);
            Assert.AreEqual(2, m.Stars);
        }

        [Test]
        public void Airtime_EndingBeforeGoal_Fails()
        {
            var m = new MissionTracker(MissionType.Airtime, new[] { 10f, 15f, 20f }, 0, 0);
            m.Tick(9f);
            m.FlightEnded();
            Assert.AreEqual(MissionOutcome.Failed, m.Outcome);
            Assert.AreEqual(0, m.Stars);
        }

        [TestCase(0.2f, 3)]
        [TestCase(0.6f, 2)]
        [TestCase(1.5f, 1)]
        public void Land_StarsByDistanceFromCentre(float distance, int stars)
        {
            var m = new MissionTracker(MissionType.Land, new[] { 0f, 0.8f, 0.3f }, 0, 0);
            m.LandedOnTarget(distance);
            Assert.AreEqual(MissionOutcome.Success, m.Outcome);
            Assert.AreEqual(stars, m.Stars);
        }

        [Test]
        public void Land_CrashingInstead_Fails()
        {
            var m = new MissionTracker(MissionType.Land, new[] { 0f, 0.8f, 0.3f }, 0, 0);
            m.FlightEnded();
            Assert.AreEqual(MissionOutcome.Failed, m.Outcome);
        }

        [Test]
        public void Gems_AllCollected_SucceedsAtOnceWithThreeStars()
        {
            var m = new MissionTracker(MissionType.Gems, new[] { 0.5f, 0.8f, 1f }, 4, 0);
            for (var i = 0; i < 4; i++) m.CollectGem();
            Assert.AreEqual(MissionOutcome.Success, m.Outcome);
            Assert.AreEqual(3, m.Stars);
        }

        [TestCase(1, false, 0)]
        [TestCase(5, true, 1)]
        [TestCase(8, true, 2)]
        public void Gems_JudgedByFractionWhenTheFlightEnds(int collected, bool success, int stars)
        {
            var m = new MissionTracker(MissionType.Gems, new[] { 0.5f, 0.8f, 1f }, 10, 0);
            for (var i = 0; i < collected; i++) m.CollectGem();
            m.FlightEnded();
            Assert.AreEqual(success ? MissionOutcome.Success : MissionOutcome.Failed, m.Outcome);
            Assert.AreEqual(stars, m.Stars);
        }

        [Test]
        public void Rings_OnlyCountInOrder_StarsByTime()
        {
            var m = new MissionTracker(MissionType.Rings, new[] { 0f, 20f, 12f }, 0, 3);
            Assert.IsFalse(m.PassRing(1), "ring 2 before ring 1 doesn't count");
            Assert.IsTrue(m.PassRing(0));
            m.Tick(14f);
            Assert.IsTrue(m.PassRing(1));
            Assert.IsTrue(m.PassRing(2));
            Assert.AreEqual(MissionOutcome.Success, m.Outcome);
            Assert.AreEqual(2, m.Stars);
        }

        [Test]
        public void Rings_CrashBeforeTheLast_Fails()
        {
            var m = new MissionTracker(MissionType.Rings, new[] { 0f, 20f, 12f }, 0, 3);
            m.PassRing(0);
            m.FlightEnded();
            Assert.AreEqual(MissionOutcome.Failed, m.Outcome);
        }

        [Test]
        public void ReachRoom_FastIsThreeStars_CrashingFails()
        {
            var fast = new MissionTracker(MissionType.ReachRoom, new[] { 0f, 20f, 10f }, 0, 0);
            fast.Tick(8f);
            fast.ReachedRoom();
            Assert.AreEqual(3, fast.Stars);

            var crash = new MissionTracker(MissionType.ReachRoom, new[] { 0f, 20f, 10f }, 0, 0);
            crash.FlightEnded();
            Assert.AreEqual(MissionOutcome.Failed, crash.Outcome);
        }

        [Test]
        public void NothingCountsAfterTheOutcome()
        {
            var m = new MissionTracker(MissionType.Gems, new[] { 0.5f, 0.8f, 1f }, 2, 0);
            m.FlightEnded();
            m.CollectGem();
            m.CollectGem();
            Assert.AreEqual(MissionOutcome.Failed, m.Outcome);
        }
    }
}

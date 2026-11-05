using System;
using Game.Flight;
using Game.Planes;
using NUnit.Framework;
using Template.Services.Save;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public class PlaneTests
    {
        private PlaneCatalog _catalog;
        private PlaneStatScaling _scaling;

        [SetUp]
        public void SetUp()
        {
            _scaling = ScriptableObject.CreateInstance<PlaneStatScaling>();
            _catalog = ScriptableObject.CreateInstance<PlaneCatalog>();
            _catalog.Define(new[]
            {
                Plane("dart", new UnlockRule(UnlockKind.Start, 0)),
                Plane("sky", new UnlockRule(UnlockKind.Start, 0)),
                Plane("stars5", new UnlockRule(UnlockKind.Stars, 5)),
                Plane("level3", new UnlockRule(UnlockKind.Level, 3)),
                Plane("day7", new UnlockRule(UnlockKind.CheckIn, 7)),
            }, _scaling);
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _catalog.Count; i++) Object.DestroyImmediate(_catalog.Get(i));
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_scaling);
        }

        private static PlaneConfig Plane(string id, UnlockRule rule)
        {
            var p = ScriptableObject.CreateInstance<PlaneConfig>();
            p.Define(id, id, PlaneShape.Dart, PlanePattern.Plain, Color.white, Color.white, PlaneStats.Classic, rule);
            return p;
        }

        [Test]
        public void StartPlanesOwned_OthersUnlockByStarsLevelAndDay()
        {
            var c = new PlaneCollection(new InMemorySaveService(), _catalog);
            CollectionAssert.AreEqual(new[] { 0, 1 }, c.Evaluate(0, _ => false, 0));
            Assert.IsFalse(c.IsOwned(2));

            CollectionAssert.AreEqual(new[] { 2 }, c.Evaluate(5, _ => false, 1));
            CollectionAssert.AreEqual(new[] { 3 }, c.Evaluate(5, level => level == 3, 1));
            CollectionAssert.AreEqual(new[] { 4 }, c.Evaluate(5, level => level == 3, 7));
            Assert.IsTrue(c.HasUnseen, "new planes flagged for the hangar");
            Assert.IsTrue(c.IsUnseen(2) && !c.IsUnseen(0), "starting planes are not 'new'");
        }

        [Test]
        public void SelectOnlyOwned_TrialIsForOneRunOnly_SavedById()
        {
            var save = new InMemorySaveService();
            var c = new PlaneCollection(save, _catalog);
            c.Evaluate(0, _ => false, 0);
            Assert.IsFalse(c.Select(4), "locked planes can't be selected");
            Assert.IsTrue(c.Select(1));
            c.SetTrial(4);
            Assert.AreEqual("day7", c.ForRun.Id);
            c.ClearTrial();
            Assert.AreEqual("sky", c.ForRun.Id);
            Assert.AreEqual(1, new PlaneCollection(save, _catalog).SelectedIndex, "selection saved");
        }

        [Test]
        public void CheckIn_CountsEachCalendarDayOnce_MissedDaysDontReset()
        {
            var save = new InMemorySaveService();
            var day = new DateTime(2026, 11, 1, 9, 0, 0);
            var c = new DailyCheckIn(save, () => day);
            Assert.IsTrue(c.CheckInToday());
            Assert.IsFalse(c.CheckInToday(), "same day counts once");
            day = day.AddHours(10);
            Assert.IsFalse(c.CheckInToday(), "still the same calendar day");
            day = day.AddDays(5); // five days missed
            Assert.IsTrue(c.CheckInToday());
            Assert.AreEqual(2, c.DaysPlayed, "total days played, not a streak");
            Assert.AreEqual(7, c.NextRewardDay);
            Assert.AreEqual(2, new DailyCheckIn(save, () => day).DaysPlayed, "saved");
        }

        [Test]
        public void Stats_ScaleTheFlightModel()
        {
            var baseConfig = ScriptableObject.CreateInstance<FlightConfig>();
            var fast = baseConfig.CreateVariant(new PlaneStats(5, 3, 3, 3, 3), _scaling);
            var nimble = baseConfig.CreateVariant(new PlaneStats(3, 5, 3, 3, 3), _scaling);
            var floaty = baseConfig.CreateVariant(new PlaneStats(3, 3, 5, 3, 3), _scaling);
            var steady = baseConfig.CreateVariant(new PlaneStats(3, 3, 3, 3, 5), _scaling);
            var classic = baseConfig.CreateVariant(PlaneStats.Classic, _scaling);

            Assert.AreEqual(baseConfig.CruiseSpeed, classic.CruiseSpeed, 1e-5f, "3s change nothing");
            Assert.Greater(fast.CruiseSpeed, baseConfig.CruiseSpeed);
            Assert.Greater(fast.MaxLaunchSpeed, baseConfig.MaxLaunchSpeed);
            Assert.Less(fast.LiftCoef, baseConfig.LiftCoef, "lift rescaled for the faster cruise");
            Assert.Greater(nimble.TurnRate, baseConfig.TurnRate);
            Assert.Less(floaty.NeutralSinkRate, baseConfig.NeutralSinkRate);
            Assert.Less(steady.WindResponse, baseConfig.WindResponse);
            Assert.AreEqual(1f, baseConfig.WindResponse, 1e-5f, "the base asset is untouched");

            foreach (var o in new Object[] { baseConfig, fast, nimble, floaty, steady, classic }) Object.DestroyImmediate(o);
        }
    }
}

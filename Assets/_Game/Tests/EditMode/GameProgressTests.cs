using Game.Levels;
using NUnit.Framework;
using Template.Services.Save;

namespace Game.Tests
{
    public class GameProgressTests
    {
        [Test]
        public void LevelsUnlockInOrder_AndBestStarsAreKept()
        {
            var save = new InMemorySaveService();
            var p = new GameProgress(save, 3);
            Assert.IsTrue(p.IsUnlocked(0));
            Assert.IsFalse(p.IsUnlocked(1));
            Assert.AreEqual(0, p.Selected, "first run plays level 1");

            p.Record(0, 2, 100);
            p.Record(0, 1, 50);
            Assert.AreEqual(2, p.StarsOf(0), "best stars kept");
            Assert.IsTrue(p.IsUnlocked(1));
            Assert.AreEqual(1, p.NextUnfinished);
            Assert.AreEqual(2, p.TotalStars);

            var reloaded = new GameProgress(save, 3);
            Assert.AreEqual(2, reloaded.StarsOf(0), "saved");
        }

        [Test]
        public void SelectAndAdvance_FreeFlightStaysPut()
        {
            var p = new GameProgress(new InMemorySaveService(), 3);
            p.Select(1);
            p.Advance();
            Assert.AreEqual(2, p.Selected);
            p.Advance();
            Assert.AreEqual(2, p.Selected, "clamped to the last level");
            p.Select(GameProgress.FreeFlight);
            p.Advance();
            Assert.AreEqual(GameProgress.FreeFlight, p.Selected);
        }

        [Test]
        public void MoreLevelsAddedLater_KeepsOldProgress()
        {
            var save = new InMemorySaveService();
            new GameProgress(save, 2).Record(1, 3, 10);
            var p = new GameProgress(save, 5);
            Assert.AreEqual(3, p.StarsOf(1));
            Assert.AreEqual(0, p.StarsOf(4));
        }
    }
}

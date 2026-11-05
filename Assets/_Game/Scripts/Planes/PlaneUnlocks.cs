using System.Collections.Generic;
using Game.Levels;

namespace Game.Planes
{
    /// <summary>Checks every unlock rule against the current stars, finished levels and days played.</summary>
    public static class PlaneUnlocks
    {
        /// <summary>Unlocks whatever is now earned; returns the new plane indexes (empty if none).</summary>
        public static List<int> Evaluate(PlaneCatalog planes, LevelCatalog levels)
        {
            var progress = GameProgress.Get(levels);
            return PlaneCollection.Get(planes).Evaluate(progress.TotalStars,
                level => progress.StarsOf(level - 1) > 0, // rule values are 1-based level numbers
                DailyCheckIn.Get().DaysPlayed);
        }
    }
}

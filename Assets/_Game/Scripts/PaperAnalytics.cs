using System.Collections.Generic;
using Game.Levels;
using Game.Planes;
using Template.Core;
using Template.Services.Analytics;

namespace Game
{
    /// <summary>
    /// Paper Flight's analytics events (SPEC §7) on top of the template's standard ones. Not for hot paths
    /// (each call builds a small dictionary): run start/end, unlocks, menu actions.
    /// </summary>
    public static class PaperAnalytics
    {
        public const string PlaneUnlockedEvent = "plane_unlocked";
        public const string PlaneSelectedEvent = "plane_selected";
        public const string CheckInEvent = "checkin";
        public const string FreeFlightEndEvent = "free_flight_end";

        private static void Log(string name, Dictionary<string, object> parameters)
        {
            if (ServiceLocator.TryGet(out IAnalyticsService analytics)) analytics.LogEvent(name, parameters);
        }

        /// <param name="level">1-based level number.</param>
        public static void LevelStart(int level, LevelConfig config, PlaneConfig plane) =>
            Log(AnalyticsEvents.LevelStart, new Dictionary<string, object>
            {
                { "level", level }, { "room", config != null ? config.Room : "" },
                { "mission", config != null ? config.Mission.ToString() : "" }, { "plane", plane != null ? plane.Id : "" },
            });

        /// <param name="result">"win", "lose" or "quit".</param>
        /// <param name="crashCause">What the plane hit ("" for a win or quit).</param>
        public static void LevelEnd(int level, string result, int stars, float duration, string crashCause, bool continued) =>
            Log(AnalyticsEvents.LevelEnd, new Dictionary<string, object>
            {
                { "level", level }, { "result", result }, { "stars", stars }, { "duration", (int)duration },
                { "crash_cause", crashCause ?? "" }, { "continued", continued ? 1 : 0 },
            });

        public static void FreeFlightEnd(float duration) =>
            Log(FreeFlightEndEvent, new Dictionary<string, object> { { "duration", (int)duration } });

        /// <param name="source">"stars", "level", "checkin" or "start".</param>
        public static void PlaneUnlocked(PlaneConfig plane, string source) =>
            Log(PlaneUnlockedEvent, new Dictionary<string, object> { { "plane", plane.Id }, { "source", source } });

        public static void PlaneSelected(PlaneConfig plane) =>
            Log(PlaneSelectedEvent, new Dictionary<string, object> { { "plane", plane.Id } });

        public static void CheckIn(int day) => Log(CheckInEvent, new Dictionary<string, object> { { "day", day } });

        public static void RewardedOffered(string placement)
        {
            if (ServiceLocator.TryGet(out IAnalyticsService analytics)) analytics.RewardedOffered(placement);
        }
    }
}

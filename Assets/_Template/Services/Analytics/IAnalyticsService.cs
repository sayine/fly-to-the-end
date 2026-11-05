using System.Collections.Generic;

namespace Template.Services.Analytics
{
    public interface IAnalyticsService
    {
        /// <param name="trackingAllowed">False if consent/ATT denied: provider should run in limited mode.</param>
        void Initialize(bool trackingAllowed);
        void LogEvent(string name, Dictionary<string, object> parameters = null);
        void SetUserProperty(string name, string value);
    }

    /// <summary>
    /// Registered (ServiceLocator) by a game that logs level_start / level_end itself with richer parameters;
    /// the flow then skips its generic ones so nothing is counted twice.
    /// </summary>
    public interface ILevelAnalyticsOwner { }

    /// <summary>Standard event names (CLAUDE.md). Keep them stable across games for comparable dashboards.</summary>
    public static class AnalyticsEvents
    {
        public const string SessionStart = "session_start";
        public const string LevelStart = "level_start";
        public const string LevelEnd = "level_end";
        public const string GameOver = "game_over";
        public const string RewardedOffered = "rewarded_offered";
        public const string RewardedCompleted = "rewarded_completed";
        public const string InterstitialShown = "interstitial_shown";
        public const string TutorialComplete = "tutorial_complete";
    }

    /// <summary>Typed helpers so call sites can't misspell parameter keys. Not for hot paths.</summary>
    public static class AnalyticsExtensions
    {
        public static void SessionStart(this IAnalyticsService a, int sessionCount) =>
            a.LogEvent(AnalyticsEvents.SessionStart, new Dictionary<string, object> { { "session", sessionCount } });

        public static void LevelStart(this IAnalyticsService a, int level) =>
            a.LogEvent(AnalyticsEvents.LevelStart, new Dictionary<string, object> { { "level", level } });

        /// <param name="result">"win", "lose" or "quit"</param>
        public static void LevelEnd(this IAnalyticsService a, int level, string result, float durationSeconds) =>
            a.LogEvent(AnalyticsEvents.LevelEnd, new Dictionary<string, object>
            {
                { "level", level }, { "result", result }, { "duration", (int)durationSeconds }
            });

        public static void GameOver(this IAnalyticsService a, int level, int score) =>
            a.LogEvent(AnalyticsEvents.GameOver, new Dictionary<string, object> { { "level", level }, { "score", score } });

        public static void RewardedOffered(this IAnalyticsService a, string placement) =>
            a.LogEvent(AnalyticsEvents.RewardedOffered, new Dictionary<string, object> { { "placement", placement } });

        public static void RewardedCompleted(this IAnalyticsService a, string placement) =>
            a.LogEvent(AnalyticsEvents.RewardedCompleted, new Dictionary<string, object> { { "placement", placement } });

        public static void InterstitialShown(this IAnalyticsService a, string placement) =>
            a.LogEvent(AnalyticsEvents.InterstitialShown, new Dictionary<string, object> { { "placement", placement } });

        public static void TutorialComplete(this IAnalyticsService a) => a.LogEvent(AnalyticsEvents.TutorialComplete);
    }
}

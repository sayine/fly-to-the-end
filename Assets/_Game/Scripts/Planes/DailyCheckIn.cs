using System;
using Template.Core;
using Template.Services.Save;

namespace Game.Planes
{
    /// <summary>
    /// Daily check-in (SPEC §4): counts the days the game was opened (missed days never reset anything).
    /// Local calendar date; the clock is injectable for tests. Rewards on day 7 and 14 are plane unlock rules.
    /// </summary>
    public sealed class DailyCheckIn
    {
        private const string Key = "checkin";
        public static readonly int[] RewardDays = { 7, 14 };

        [Serializable]
        private sealed class Data
        {
            public int DaysPlayed;
            public string LastDay = "";
        }

        private readonly ISaveService _save;
        private readonly Func<DateTime> _now;
        private readonly Data _data;

        public DailyCheckIn(ISaveService save, Func<DateTime> now = null)
        {
            _save = save;
            _now = now ?? (() => DateTime.Now);
            _data = save != null ? save.Load<Data>(Key) : new Data();
        }

        public static DailyCheckIn Get()
        {
            if (ServiceLocator.TryGet(out DailyCheckIn checkIn)) return checkIn;
            ServiceLocator.TryGet(out ISaveService save);
            checkIn = new DailyCheckIn(save);
            ServiceLocator.Register(checkIn);
            return checkIn;
        }

        public int DaysPlayed => _data.DaysPlayed;

        /// <summary>Counts today if it hasn't been counted yet. True when a new day was counted.</summary>
        public bool CheckInToday()
        {
            var today = _now().ToString("yyyy-MM-dd");
            if (_data.LastDay == today) return false;
            _data.LastDay = today;
            _data.DaysPlayed++;
            _save?.Save(Key, _data);
            return true;
        }

        /// <summary>The next reward day after today's count (0 when all are collected).</summary>
        public int NextRewardDay
        {
            get
            {
                foreach (var day in RewardDays)
                    if (day > _data.DaysPlayed) return day;
                return 0;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Template.Core;
using Template.Services.Save;

namespace Game.Planes
{
    /// <summary>
    /// The player's planes (SPEC §3-4): which are owned, which is selected, a one-run "Try it" trial, and planes
    /// unlocked since the hangar was last opened. Unlock rules are re-checked after levels and check-ins.
    /// Saved by plane id, so reordering or adding planes keeps progress.
    /// </summary>
    public sealed class PlaneCollection
    {
        private const string Key = "planes";

        [Serializable]
        private sealed class Data
        {
            public List<string> Owned = new List<string>();
            public List<string> Unseen = new List<string>();
            public string Selected = "";
        }

        private readonly ISaveService _save;
        private readonly PlaneCatalog _catalog;
        private readonly Data _data;
        private int _trial = -1;

        public PlaneCollection(ISaveService save, PlaneCatalog catalog)
        {
            _save = save;
            _catalog = catalog;
            _data = save != null ? save.Load<Data>(Key) : new Data();
        }

        public static PlaneCollection Get(PlaneCatalog catalog)
        {
            if (ServiceLocator.TryGet(out PlaneCollection collection)) return collection;
            ServiceLocator.TryGet(out ISaveService save);
            collection = new PlaneCollection(save, catalog);
            collection.Evaluate(0, _ => false, 0); // starting planes
            ServiceLocator.Register(collection);
            return collection;
        }

        public PlaneCatalog Catalog => _catalog;
        public bool IsOwned(int index) => _data.Owned.Contains(_catalog.Get(index).Id);
        public bool IsUnseen(int index) => _data.Unseen.Contains(_catalog.Get(index).Id);
        public bool HasUnseen => _data.Unseen.Count > 0;

        public int SelectedIndex
        {
            get
            {
                var index = _catalog.IndexOf(_data.Selected);
                return index >= 0 && IsOwned(index) ? index : 0;
            }
        }

        /// <summary>The plane the next run uses: the "Try it" trial if one is set, else the selected plane.</summary>
        public PlaneConfig ForRun => _catalog.Get(_trial >= 0 ? _trial : SelectedIndex);
        public bool IsTrial => _trial >= 0;

        public bool Select(int index)
        {
            if (!IsOwned(index)) return false;
            _data.Selected = _catalog.Get(index).Id;
            Save();
            return true;
        }

        /// <summary>Lets a locked plane fly the next run only (rewarded "Try it").</summary>
        public void SetTrial(int index) => _trial = index;
        public void ClearTrial() => _trial = -1;

        public void MarkAllSeen()
        {
            if (_data.Unseen.Count == 0) return;
            _data.Unseen.Clear();
            Save();
        }

        /// <summary>Unlocks every plane whose rule is now met. Returns the newly unlocked indexes (empty if none).</summary>
        public List<int> Evaluate(int totalStars, Func<int, bool> levelCompleted, int daysPlayed)
        {
            var unlocked = new List<int>();
            for (var i = 0; i < _catalog.Count; i++)
            {
                var plane = _catalog.Get(i);
                if (plane == null || _data.Owned.Contains(plane.Id)) continue;
                if (!IsMet(plane.Unlock, totalStars, levelCompleted, daysPlayed)) continue;
                _data.Owned.Add(plane.Id);
                if (plane.Unlock.Kind != UnlockKind.Start) _data.Unseen.Add(plane.Id);
                unlocked.Add(i);
            }
            if (unlocked.Count > 0) Save();
            return unlocked;
        }

        public static bool IsMet(UnlockRule rule, int totalStars, Func<int, bool> levelCompleted, int daysPlayed) => rule.Kind switch
        {
            UnlockKind.Start => true,
            UnlockKind.Stars => totalStars >= rule.Value,
            UnlockKind.Level => levelCompleted != null && levelCompleted(rule.Value),
            UnlockKind.CheckIn => daysPlayed >= rule.Value,
            _ => false,
        };

        /// <summary>Short unlock condition for the hangar ("12 stars", "Level 5", "Day 7").</summary>
        public static string Describe(UnlockRule rule) => rule.Kind switch
        {
            UnlockKind.Stars => $"{rule.Value} stars",
            UnlockKind.Level => $"Level {rule.Value}",
            UnlockKind.CheckIn => $"Day {rule.Value}",
            _ => "",
        };

        private void Save() => _save?.Save(Key, _data);
    }
}

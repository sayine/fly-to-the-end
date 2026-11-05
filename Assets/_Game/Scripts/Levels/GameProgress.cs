using System;
using Template.Core;
using Template.Services.Save;
using UnityEngine;

namespace Game.Levels
{
    /// <summary>
    /// Level progress (SPEC §6): best stars and score per level, total stars, which level the next run plays.
    /// Saved under its own key in ISaveService. Shared by the menu (selection) and the Game scene (results);
    /// created on first use and registered in the ServiceLocator.
    /// </summary>
    public sealed class GameProgress
    {
        public const int FreeFlight = -1;
        private const string Key = "progress";

        [Serializable]
        private sealed class Data
        {
            public int[] Stars = Array.Empty<int>();
            public int[] BestScore = Array.Empty<int>();
        }

        private readonly ISaveService _save;
        private readonly int _levelCount;
        private readonly Data _data;
        private int _selected = int.MinValue; // not chosen yet: the next unfinished level

        public GameProgress(ISaveService save, int levelCount)
        {
            _save = save;
            _levelCount = levelCount;
            _data = save != null ? save.Load<Data>(Key) : new Data();
            Resize();
        }

        /// <summary>The shared instance (creates and registers it on first use).</summary>
        public static GameProgress Get(LevelCatalog catalog)
        {
            if (ServiceLocator.TryGet(out GameProgress progress)) return progress;
            ServiceLocator.TryGet(out ISaveService save);
            progress = new GameProgress(save, catalog != null ? catalog.Count : 0);
            ServiceLocator.Register(progress);
            return progress;
        }

        public int LevelCount => _levelCount;
        public int StarsOf(int level) => level >= 0 && level < _levelCount ? _data.Stars[level] : 0;
        public int BestScoreOf(int level) => level >= 0 && level < _levelCount ? _data.BestScore[level] : 0;
        public bool IsUnlocked(int level) => level == 0 || (level > 0 && level < _levelCount && StarsOf(level - 1) > 0);

        public int TotalStars
        {
            get
            {
                var total = 0;
                for (var i = 0; i < _levelCount; i++) total += _data.Stars[i];
                return total;
            }
        }

        /// <summary>First level without stars (or the last level when all are done).</summary>
        public int NextUnfinished
        {
            get
            {
                for (var i = 0; i < _levelCount; i++)
                    if (_data.Stars[i] == 0) return i;
                return Mathf.Max(0, _levelCount - 1);
            }
        }

        /// <summary>Level the next run plays (FreeFlight = -1).</summary>
        public int Selected => _selected == int.MinValue ? NextUnfinished : _selected;

        public void Select(int level) =>
            _selected = level == FreeFlight ? FreeFlight : Mathf.Clamp(level, 0, Mathf.Max(0, _levelCount - 1));

        /// <summary>After Next on the level-complete screen.</summary>
        public void Advance()
        {
            if (_selected == FreeFlight) return;
            Select(Mathf.Min(Selected + 1, _levelCount - 1));
        }

        /// <summary>Stores a won level (keeps the best stars and score). Returns true if the score is a new best.</summary>
        public bool Record(int level, int stars, int score)
        {
            if (level < 0 || level >= _levelCount) return false;
            _data.Stars[level] = Mathf.Max(_data.Stars[level], Mathf.Clamp(stars, 0, 3));
            var newBest = score > _data.BestScore[level];
            if (newBest) _data.BestScore[level] = score;
            _save?.Save(Key, _data);
            return newBest;
        }

        private void Resize()
        {
            if (_data.Stars.Length != _levelCount) Array.Resize(ref _data.Stars, _levelCount);
            if (_data.BestScore.Length != _levelCount) Array.Resize(ref _data.BestScore, _levelCount);
        }
    }
}

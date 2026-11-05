using System;
using Game.Levels;
using Template.Core;
using Template.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Level map (SPEC §8): a grid of level buttons with their best stars; locked levels are dimmed. Picking an
    /// unlocked level selects it and starts the run. Overlay on the menu. Built by the game's scene generator.
    /// </summary>
    public sealed class LevelMapScreen : UIScreen
    {
        [Serializable]
        public sealed class Cell
        {
            public Button Button;
            public TMP_Text Number;
            public Image[] Stars = new Image[3];
            public CanvasGroup Group;
            public GameObject Lock;
            public Image Face;
        }

        [SerializeField] private LevelCatalog _catalog;
        [SerializeField] private Button _backButton;
        [SerializeField] private TMP_Text _totalStars;
        [SerializeField] private Cell[] _cells = Array.Empty<Cell>();
        [SerializeField] private Color _starOn = new Color(1f, 0.82f, 0.25f, 1f);
        [SerializeField] private Color _starOff = new Color(1f, 1f, 1f, 0.2f);
        [Tooltip("Tile face colours: the level Play continues from, the others.")]
        [SerializeField] private Color _currentFace = new Color(1f, 0.79f, 0.24f, 1f);
        [SerializeField] private Color _face = Color.white;

        public override bool IsOverlay => true;

        protected override void OnInit()
        {
            Bind(_backButton, Hide);
            for (var i = 0; i < _cells.Length; i++)
            {
                var index = i; // captured once at init
                Bind(_cells[i].Button, () => Pick(index));
            }
        }

        protected override void OnShow()
        {
            var progress = GameProgress.Get(_catalog);
            if (_totalStars != null) _totalStars.SetText("{0} / {1}", progress.TotalStars, progress.LevelCount * 3);
            var current = progress.NextUnfinished;
            for (var i = 0; i < _cells.Length; i++)
            {
                var cell = _cells[i];
                var unlocked = progress.IsUnlocked(i);
                if (cell.Lock != null) cell.Lock.SetActive(!unlocked);
                if (cell.Number != null) cell.Number.gameObject.SetActive(unlocked);
                if (cell.Face != null) cell.Face.color = i == current ? _currentFace : _face;
                var stars = progress.StarsOf(i);
                cell.Button.interactable = unlocked;
                if (cell.Group != null) cell.Group.alpha = unlocked ? 1f : 0.45f;
                for (var s = 0; s < cell.Stars.Length; s++)
                    if (cell.Stars[s] != null) cell.Stars[s].color = s < stars ? _starOn : _starOff;
            }
        }

        private void Pick(int level)
        {
            var progress = GameProgress.Get(_catalog);
            if (!progress.IsUnlocked(level)) return;
            progress.Select(level);
            Hide();
            Request(UIRequest.Play);
        }
    }
}

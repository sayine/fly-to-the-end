using System;
using Game.Levels;
using Game.Planes;
using Template.Core;
using Template.Services.Ads;
using Template.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Hangar (SPEC §4): every plane as a thumbnail; locked ones dimmed with their unlock condition, new ones badged.
    /// The detail panel shows the focused plane, its five stat bars and Select, or for a locked plane the condition
    /// and a rewarded "Try it" (flies it for one level). Overlay on the menu.
    /// </summary>
    public sealed class HangarScreen : UIScreen
    {
        [Serializable]
        public sealed class Cell
        {
            public Button Button;
            public Image Thumbnail;
            public TMP_Text Name;
            public TMP_Text Condition;
            public CanvasGroup Group;
            public GameObject NewBadge;
            public Image Frame;
            public GameObject Lock;
        }

        [Serializable]
        public sealed class StatBar
        {
            public Image[] Pips = new Image[5];
        }

        [SerializeField] private PlaneCatalog _planes;
        [SerializeField] private LevelCatalog _levels;
        [SerializeField] private Button _backButton;
        [SerializeField] private Cell[] _cells = Array.Empty<Cell>();
        [Header("Detail")]
        [SerializeField] private Image _detailThumbnail;
        [SerializeField] private TMP_Text _detailName;
        [SerializeField] private TMP_Text _detailInfo;
        [SerializeField] private StatBar[] _stats = new StatBar[5];
        [SerializeField] private Button _selectButton;
        [SerializeField] private TMP_Text _selectLabel;
        [SerializeField] private Button _tryButton;
        [SerializeField] private Color _pipOn = new Color(1f, 0.82f, 0.25f, 1f);
        [SerializeField] private Color _pipOff = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color _frameOn = new Color(1f, 0.82f, 0.25f, 1f);
        [SerializeField] private Color _frameOff = new Color(1f, 1f, 1f, 0f);
        [SerializeField] private Color _lockedTint = new Color(0.12f, 0.13f, 0.18f, 0.9f);

        public const string TryPlacement = "try_plane";

        private IAdService _ads;
        private int _focus;

        public override bool IsOverlay => true;

        protected override void OnInit()
        {
            ServiceLocator.TryGet(out _ads);
            Bind(_backButton, Hide);
            Bind(_selectButton, SelectFocused);
            Bind(_tryButton, TryFocused);
            for (var i = 0; i < _cells.Length; i++)
            {
                var index = i;
                Bind(_cells[i].Button, () => Focus(index));
            }
            if (_ads != null) _ads.RewardedReadyChanged += _ => { if (IsVisible) RefreshDetail(); };
        }

        protected override void OnShow()
        {
            var collection = PlaneCollection.Get(_planes);
            _focus = collection.SelectedIndex;
            RefreshGrid();
            RefreshDetail();
        }

        protected override void OnHide() => PlaneCollection.Get(_planes).MarkAllSeen();

        private void Focus(int index)
        {
            _focus = index;
            if (!PlaneCollection.Get(_planes).IsOwned(index) && _ads != null && _ads.IsRewardedReady)
                PaperAnalytics.RewardedOffered(TryPlacement);
            RefreshGrid();
            RefreshDetail();
        }

        private void RefreshGrid()
        {
            var collection = PlaneCollection.Get(_planes);
            for (var i = 0; i < _cells.Length && i < _planes.Count; i++)
            {
                var cell = _cells[i];
                var plane = _planes.Get(i);
                var owned = collection.IsOwned(i);
                if (cell.Thumbnail != null)
                {
                    cell.Thumbnail.sprite = plane.Thumbnail;
                    cell.Thumbnail.color = owned ? Color.white : _lockedTint; // silhouette, so the condition reads
                }
                if (cell.Name != null) cell.Name.text = plane.DisplayName;
                if (cell.Condition != null)
                {
                    cell.Condition.gameObject.SetActive(!owned);
                    if (!owned) cell.Condition.text = PlaneCollection.Describe(plane.Unlock);
                }
                if (cell.Group != null) cell.Group.alpha = owned ? 1f : 0.8f;
                if (cell.Lock != null) cell.Lock.SetActive(!owned);
                if (cell.NewBadge != null) cell.NewBadge.SetActive(collection.IsUnseen(i));
                if (cell.Frame != null) cell.Frame.color = i == _focus ? _frameOn : _frameOff;
            }
        }

        private void RefreshDetail()
        {
            var collection = PlaneCollection.Get(_planes);
            var plane = _planes.Get(_focus);
            var owned = collection.IsOwned(_focus);
            if (_detailThumbnail != null) _detailThumbnail.sprite = plane.Thumbnail;
            if (_detailName != null) _detailName.text = plane.DisplayName;
            if (_detailInfo != null) _detailInfo.text = owned ? "" : $"Unlock: {PlaneCollection.Describe(plane.Unlock)}";

            var s = plane.Stats;
            SetBar(0, s.Speed);
            SetBar(1, s.Agility);
            SetBar(2, s.Glide);
            SetBar(3, s.Lift);
            SetBar(4, s.Stability);

            var selected = owned && collection.SelectedIndex == _focus;
            _selectButton.gameObject.SetActive(owned);
            _selectButton.interactable = !selected;
            if (_selectLabel != null) _selectLabel.text = selected ? "SELECTED" : "SELECT";
            _tryButton.gameObject.SetActive(!owned && _ads != null && _ads.IsRewardedReady);
        }

        private void SetBar(int stat, int value)
        {
            if (stat >= _stats.Length || _stats[stat] == null) return;
            var pips = _stats[stat].Pips;
            for (var i = 0; i < pips.Length; i++)
                if (pips[i] != null) pips[i].color = i < value ? _pipOn : _pipOff;
        }

        private void SelectFocused()
        {
            if (PlaneCollection.Get(_planes).Select(_focus)) PaperAnalytics.PlaneSelected(_planes.Get(_focus));
            RefreshGrid();
            RefreshDetail();
        }

        private void TryFocused()
        {
            var index = _focus;
            _tryButton.interactable = false;
            _ads.ShowRewarded(TryPlacement, rewarded =>
            {
                _tryButton.interactable = true;
                if (!rewarded) return;
                // Fly the locked plane for the next level only.
                PlaneCollection.Get(_planes).SetTrial(index);
                var progress = GameProgress.Get(_levels);
                progress.Select(progress.NextUnfinished);
                Hide();
                Request(UIRequest.Play);
            });
        }
    }
}

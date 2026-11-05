using System;
using Game.Planes;
using Template.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Daily check-in popup (SPEC §4): "Day n", a 14-day track with gifts on day 7 and 14, and either the plane
    /// unlocked today or how many days until the next one. Shown by MenuExtras on the first menu of a new day.
    /// </summary>
    public sealed class CheckInScreen : UIScreen
    {
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _message;
        [SerializeField] private Image[] _days = Array.Empty<Image>();
        [SerializeField] private Image _rewardThumbnail;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Color _dayOn = new Color(0.25f, 0.78f, 0.55f, 1f);
        [SerializeField] private Color _dayOff = new Color(1f, 1f, 1f, 0.15f);

        public override bool IsOverlay => true;

        protected override void OnInit() => Bind(_continueButton, Hide);

        /// <summary>Fills the popup; unlockedToday is the plane the check-in just unlocked (or null).</summary>
        public void Present(int daysPlayed, int nextRewardDay, PlaneConfig unlockedToday)
        {
            if (_title != null) _title.SetText("DAY {0}", daysPlayed);
            for (var i = 0; i < _days.Length; i++)
                if (_days[i] != null) _days[i].color = i < daysPlayed ? _dayOn : _dayOff;
            if (_rewardThumbnail != null)
            {
                _rewardThumbnail.gameObject.SetActive(unlockedToday != null);
                if (unlockedToday != null) _rewardThumbnail.sprite = unlockedToday.Thumbnail;
            }
            if (_message == null) return;
            if (unlockedToday != null) _message.text = $"New plane: {unlockedToday.DisplayName}!";
            else if (nextRewardDay > 0) _message.text = $"{nextRewardDay - daysPlayed} more day(s) to a new plane";
            else _message.text = "Thanks for flying every day!";
        }
    }
}

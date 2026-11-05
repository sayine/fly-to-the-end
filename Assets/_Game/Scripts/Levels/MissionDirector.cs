using System;
using Game.Flight;
using UnityEngine;

namespace Game.Levels
{
    /// <summary>
    /// Sets up the selected level in the Game scene (SPEC §2): launch point (living-room shelf or a book stack),
    /// pooled mission items (gems, rings, landing target, room trigger), the MissionTracker and the mission HUD.
    /// PaperPlaneGame drives it: Prepare on run start, Tick while flying, Landed / FlightEnded at the end.
    /// Pools are built by the scene generator; nothing is instantiated at runtime.
    /// </summary>
    public sealed class MissionDirector : MonoBehaviour
    {
        [SerializeField] private LevelCatalog _catalog;
        [SerializeField] private PaperPlane _plane;
        [SerializeField] private Transform _launchPoint;
        [Tooltip("The living-room shelf (launch pad for shelf levels).")]
        [SerializeField] private Collider _launchShelf;
        [Tooltip("Box collider resized into a stack of books under book-stack starts.")]
        [SerializeField] private BoxCollider _bookStack;
        [SerializeField] private Gem[] _gems = Array.Empty<Gem>();
        [SerializeField] private Ring[] _rings = Array.Empty<Ring>();
        [SerializeField] private LandingTarget _landing;
        [SerializeField] private RoomTrigger _roomTrigger;
        [SerializeField] private MissionHudView _hud;
        [SerializeField, Min(0.1f)] private float _bookStackWidth = 0.8f;

        private Action<Gem> _onGem;
        private Func<int, bool> _onRing;
        private Action _onRoom;

        public LevelCatalog Catalog => _catalog;
        public int LevelIndex { get; private set; } = GameProgress.FreeFlight;
        public LevelConfig Level { get; private set; }
        public MissionTracker Tracker { get; private set; }
        public bool IsFreeFlight => Tracker == null || Tracker.IsFreeFlight;
        public Vector3 LaunchPosition => _launchPoint.position;

        private void Awake()
        {
            _onGem = OnGem;
            _onRing = OnRing;
            _onRoom = OnRoom;
        }

        /// <summary>Sets up the selected level; returns the launch pad collider (ignored briefly at launch).</summary>
        public Collider Prepare()
        {
            var progress = GameProgress.Get(_catalog);
            LevelIndex = progress.Selected;
            Level = _catalog != null ? _catalog.Get(LevelIndex) : null;
            HideAll();
            if (Level == null)
            {
                Tracker = new MissionTracker(MissionType.FreeFlight, null, 0, 0);
                return _launchShelf;
            }

            var gemCount = Mathf.Min(Level.Gems.Length, _gems.Length);
            var ringCount = Mathf.Min(Level.Rings.Length, _rings.Length);
            Tracker = new MissionTracker(Level.Mission, Level.Stars, gemCount, ringCount);

            _launchPoint.SetPositionAndRotation(Level.Start, Quaternion.Euler(0f, Level.StartYaw, 0f));
            var pad = _launchShelf;
            if (Level.BookStack && _bookStack != null)
            {
                var height = Mathf.Max(0.1f, Level.Start.y - 0.08f - Level.SurfaceHeight);
                _bookStack.transform.position = new Vector3(Level.Start.x, Level.SurfaceHeight + height * 0.5f, Level.Start.z);
                _bookStack.transform.rotation = Quaternion.Euler(0f, Level.StartYaw, 0f);
                _bookStack.transform.localScale = new Vector3(_bookStackWidth, height, _bookStackWidth);
                _bookStack.gameObject.SetActive(true);
                pad = _bookStack;
            }

            var body = _plane.Body;
            for (var i = 0; i < gemCount; i++) _gems[i].Place(Level.Gems[i], body, _onGem);
            for (var i = 0; i < ringCount; i++) _rings[i].Place(Level.Rings[i], i, body, _onRing);
            if (Level.Mission == MissionType.Land && _landing != null) _landing.Place(Level.LandCenter, Level.LandSize);
            if (Level.Mission == MissionType.ReachRoom && _roomTrigger != null) _roomTrigger.Place(Level.TargetRoom, body, _onRoom);

            if (_hud != null) _hud.ShowIntro(LevelIndex, Level, Tracker);
            return pad;
        }

        public void OnLaunched()
        {
            if (_hud != null) _hud.ShowProgress(Tracker);
        }

        /// <summary>Second chance: the failed mission continues (gems, rings and time kept).</summary>
        public void Resume()
        {
            Tracker?.Resume();
            if (_hud != null && Tracker != null) _hud.ShowProgress(Tracker);
        }

        public void Tick(float deltaTime)
        {
            if (Tracker == null) return;
            Tracker.Tick(deltaTime);
            if (_hud != null) _hud.Refresh(Tracker);
        }

        public void Landed(LandingTarget target, Vector3 point)
        {
            if (Tracker == null) return;
            if (target == _landing) Tracker.LandedOnTarget(_landing.DistanceFromCentre(point));
            else Tracker.FlightEnded();
            if (_hud != null) _hud.Refresh(Tracker);
        }

        public void FlightEnded()
        {
            Tracker?.FlightEnded();
            if (_hud != null && Tracker != null) _hud.Refresh(Tracker);
        }

        private void OnGem(Gem gem)
        {
            Tracker?.CollectGem();
        }

        private bool OnRing(int index)
        {
            if (Tracker == null || !Tracker.PassRing(index)) return false;
            _rings[index].SetState(Ring.State.Passed);
            if (index + 1 < Tracker.RingTotal) _rings[index + 1].SetState(Ring.State.Next);
            return true;
        }

        private void OnRoom() => Tracker?.ReachedRoom();

        private void HideAll()
        {
            for (var i = 0; i < _gems.Length; i++) _gems[i].Disarm();
            for (var i = 0; i < _rings.Length; i++) _rings[i].Disarm();
            if (_landing != null) _landing.Hide();
            if (_roomTrigger != null) _roomTrigger.Disarm();
            if (_bookStack != null) _bookStack.gameObject.SetActive(false);
            if (_hud != null) _hud.Hide();
        }
    }
}

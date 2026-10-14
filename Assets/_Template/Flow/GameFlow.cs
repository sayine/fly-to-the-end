using Template.Core;
using Template.Services.Ads;
using Template.Services.Analytics;
using Template.Services.Input;
using Template.Services.Save;
using Template.Services.SceneLoading;
using Template.UI;
using Template.UI.Screens;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Template.Flow
{
    /// <summary>
    /// Persistent game-flow state machine:
    /// Menu → Playing ⇄ Paused → GameOver → (Rewarded Continue → Playing | Retry → Playing | Menu).
    /// Listens to UIRequestEvent (from screens) and PlayerDiedEvent/ScoreChangedEvent (from gameplay).
    /// Gameplay only listens to RunStartedEvent / RunContinuedEvent / GameStateChangedEvent.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        private const int Level = 1; // endless template: one level. Games with levels pass their index here.

        private readonly StateMachine _machine = new StateMachine();
        private ScreenManager _ui;
        private ISceneLoader _scenes;
        private IAdService _ads;
        private IAnalyticsService _analytics;
        private ISaveService _save;
        private IPlayerInput _input;

        private MenuState _menu;
        private PlayingState _playing;
        private PausedState _paused;
        private GameOverState _gameOver;

        private int _score;
        private float _runDuration;
        private bool _continuedThisRun;

        public GameState State { get; private set; } = GameState.Boot;

        public static GameFlow Create()
        {
            var go = new GameObject("[GameFlow]");
            DontDestroyOnLoad(go);
            return go.AddComponent<GameFlow>();
        }

        private void Awake()
        {
            _ui = ServiceLocator.Get<ScreenManager>();
            _scenes = ServiceLocator.Get<ISceneLoader>();
            _ads = ServiceLocator.Get<IAdService>();
            _analytics = ServiceLocator.Get<IAnalyticsService>();
            _save = ServiceLocator.Get<ISaveService>();
            ServiceLocator.TryGet(out _input);

            _menu = new MenuState(this);
            _playing = new PlayingState(this);
            _paused = new PausedState(this);
            _gameOver = new GameOverState(this);

            EventBus<UIRequestEvent>.Subscribe(OnUIRequest);
            EventBus<PlayerDiedEvent>.Subscribe(OnPlayerDied);
            EventBus<ScoreChangedEvent>.Subscribe(OnScoreChanged);
            ServiceLocator.Get<IAppLifecycle>().PauseChanged += OnAppPause;
        }

        private void OnDestroy()
        {
            EventBus<UIRequestEvent>.Unsubscribe(OnUIRequest);
            EventBus<PlayerDiedEvent>.Unsubscribe(OnPlayerDied);
            EventBus<ScoreChangedEvent>.Unsubscribe(OnScoreChanged);
        }

        private void Update()
        {
            _machine.Tick(Time.unscaledDeltaTime);
        }

        // ---------- Public entry points ----------
        public void GoToMenu()
        {
            if (SceneManager.GetActiveScene().name == SceneNames.Menu) _machine.ChangeState(_menu);
            else _scenes.Load(SceneNames.Menu, () => _machine.ChangeState(_menu));
        }

        public void StartNewRun()
        {
            _scenes.Load(SceneNames.Game, () =>
            {
                _score = 0;
                _runDuration = 0f;
                _continuedThisRun = false;
                _input?.Calibrate();
                EventBus<RunStartedEvent>.Raise(default);
                _analytics.LevelStart(Level);
                _machine.ChangeState(_playing);
            });
        }

        // ---------- Event handlers ----------
        private void OnUIRequest(UIRequestEvent e)
        {
            if (_scenes.IsLoading) return;
            switch (e.Request)
            {
                case UIRequest.Play when State == GameState.Menu:
                    StartNewRun();
                    break;
                case UIRequest.Pause when State == GameState.Playing:
                    _machine.ChangeState(_paused);
                    break;
                case UIRequest.Resume when State == GameState.Paused:
                    _machine.ChangeState(_playing);
                    break;
                case UIRequest.Continue when State == GameState.GameOver:
                    TryContinue();
                    break;
                case UIRequest.Retry when State == GameState.GameOver:
                    _ads.ShowInterstitial(AdPlacements.GameOver, StartNewRun);
                    break;
                case UIRequest.Menu when State == GameState.GameOver:
                    _ads.ShowInterstitial(AdPlacements.GameOver, GoToMenu);
                    break;
                case UIRequest.Menu when State == GameState.Paused:
                    _analytics.LevelEnd(Level, "quit", _runDuration);
                    GoToMenu();
                    break;
            }
        }

        private void OnPlayerDied(PlayerDiedEvent e)
        {
            if (State == GameState.Playing) _machine.ChangeState(_gameOver);
        }

        private void OnScoreChanged(ScoreChangedEvent e) => _score = e.Score;

        private void OnAppPause(bool paused)
        {
            if (paused && State == GameState.Playing) _machine.ChangeState(_paused);
        }

        private void TryContinue()
        {
            if (_continuedThisRun) return;
            _ads.ShowRewarded(AdPlacements.Continue, rewarded =>
            {
                if (rewarded)
                {
                    _continuedThisRun = true;
                    EventBus<RunContinuedEvent>.Raise(default);
                    _machine.ChangeState(_playing);
                }
                else
                {
                    RaiseGameOver(false); // refresh the screen (re-enables buttons, re-checks ad availability)
                }
            });
        }

        private void RaiseGameOver(bool isNewBest)
        {
            EventBus<GameOverEvent>.Raise(new GameOverEvent
            {
                Score = _score,
                BestScore = _save.Data.BestScore,
                IsNewBest = isNewBest,
                CanContinue = !_continuedThisRun,
            });
        }

        private void SetState(GameState next)
        {
            var prev = State;
            State = next;
            _ads.SetGameplayActive(next == GameState.Playing || next == GameState.Paused);
            if (_input != null) _input.Enabled = next == GameState.Playing;
            EventBus<GameStateChangedEvent>.Raise(new GameStateChangedEvent { Previous = prev, Current = next });
        }

        // ---------- States ----------
        private sealed class MenuState : State
        {
            private readonly GameFlow _f;
            public MenuState(GameFlow f) => _f = f;

            public override void Enter()
            {
                Time.timeScale = 1f;
                _f.SetState(GameState.Menu);
                _f._ui.Show<MenuScreen>();
            }
        }

        private sealed class PlayingState : State
        {
            private readonly GameFlow _f;
            public PlayingState(GameFlow f) => _f = f;

            public override void Enter()
            {
                Time.timeScale = 1f;
                _f.SetState(GameState.Playing);
                _f._ui.Show<HUDScreen>();
            }

            public override void Tick(float deltaTime) => _f._runDuration += deltaTime;
        }

        private sealed class PausedState : State
        {
            private readonly GameFlow _f;
            public PausedState(GameFlow f) => _f = f;

            public override void Enter()
            {
                Time.timeScale = 0f;
                _f.SetState(GameState.Paused);
                _f._ui.ShowOverlay<PauseScreen>();
            }

            public override void Exit()
            {
                Time.timeScale = 1f;
                _f._ui.Hide<PauseScreen>();
                _f._ui.Hide<SettingsScreen>();
            }
        }

        private sealed class GameOverState : State
        {
            private readonly GameFlow _f;
            public GameOverState(GameFlow f) => _f = f;

            public override void Enter()
            {
                _f.SetState(GameState.GameOver);
                var data = _f._save.Data;
                var isNewBest = _f._score > data.BestScore;
                if (isNewBest)
                {
                    data.BestScore = _f._score;
                    _f._save.Save();
                }

                _f._analytics.LevelEnd(Level, "lose", _f._runDuration);
                _f._analytics.GameOver(Level, _f._score);
                _f._ui.Show<GameOverScreen>();
                _f.RaiseGameOver(isNewBest);
            }
        }
    }
}

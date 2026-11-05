namespace Template.Core
{
    /// <summary>
    /// Game flow: Boot → Menu → Playing ⇄ Paused → GameOver → (Continue | Retry | Menu),
    /// or Playing → LevelComplete → (Next | Retry | Menu) for level-based games.
    /// </summary>
    public enum GameState
    {
        Boot,
        Menu,
        Playing,
        Paused,
        GameOver,
        LevelComplete,
    }

    // ---------- Flow → everyone ----------
    public struct GameStateChangedEvent : IEvent
    {
        public GameState Previous;
        public GameState Current;
    }

    /// <summary>A fresh run begins (after the Game scene loaded). Gameplay resets itself.</summary>
    public struct RunStartedEvent : IEvent { }

    /// <summary>Rewarded continue granted: revive the player, keep score.</summary>
    public struct RunContinuedEvent : IEvent { }

    public struct GameOverEvent : IEvent
    {
        public int Score;
        public int BestScore;
        public bool IsNewBest;
        public bool CanContinue;
    }

    public struct BootProgressEvent : IEvent
    {
        public float Progress;
        public string Status;
    }

    // ---------- Gameplay → flow/UI ----------
    public struct ScoreChangedEvent : IEvent
    {
        public int Score;
    }

    /// <summary>Gameplay reports the lose condition. The flow decides what happens next.</summary>
    public struct PlayerDiedEvent : IEvent
    {
        /// <summary>True when this run's score is not the game's "best" (e.g. a failed level in a level-based game).</summary>
        public bool NotForBest;
    }

    /// <summary>Gameplay reports a won level (level-based games). The flow shows the level-complete screen.</summary>
    public struct LevelCompletedEvent : IEvent
    {
        /// <summary>0-3.</summary>
        public int Stars;
        public int Score;
        public bool IsNewBest;
    }

    /// <summary>Flow → gameplay: the player chose Next on the level-complete screen; advance before the next run starts.</summary>
    public struct LevelAdvanceEvent : IEvent { }

    // ---------- UI → flow ----------
    public enum UIRequest
    {
        Play,
        Pause,
        Resume,
        Retry,
        Continue,
        Menu,
        Next,
    }

    public struct UIRequestEvent : IEvent
    {
        public UIRequest Request;
    }
}

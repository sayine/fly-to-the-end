namespace Template.Core
{
    /// <summary>Game flow: Boot → Menu → Playing ⇄ Paused → GameOver → (Continue | Retry | Menu).</summary>
    public enum GameState
    {
        Boot,
        Menu,
        Playing,
        Paused,
        GameOver,
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
    public struct PlayerDiedEvent : IEvent { }

    // ---------- UI → flow ----------
    public enum UIRequest
    {
        Play,
        Pause,
        Resume,
        Retry,
        Continue,
        Menu,
    }

    public struct UIRequestEvent : IEvent
    {
        public UIRequest Request;
    }
}

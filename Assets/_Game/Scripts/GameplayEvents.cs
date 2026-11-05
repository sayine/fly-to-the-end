using Template.Core;

namespace Game
{
    /// <summary>The plane is (or stopped being) on the pad waiting for a throw. UI shows throw-time offers.</summary>
    public struct AimStateEvent : IEvent
    {
        public bool Aiming;
        public bool FreeFlight;
        public bool Boosted;
    }

    /// <summary>The player watched the "Boost throw" ad: the next throw this run is stronger.</summary>
    public struct BoostThrowGrantedEvent : IEvent { }
}

using System;

namespace Template.Core
{
    public interface IState
    {
        void Enter();
        void Exit();
        void Tick(float deltaTime);
    }

    /// <summary>Base class so states only override what they need.</summary>
    public abstract class State : IState
    {
        public virtual void Enter() { }
        public virtual void Exit() { }
        public virtual void Tick(float deltaTime) { }
    }

    /// <summary>
    /// Minimal state machine. The owner calls Tick from its Update.
    /// Re-entrant ChangeState calls (from inside Enter/Exit) are queued and applied in order.
    /// </summary>
    public sealed class StateMachine
    {
        private bool _isTransitioning;
        private IState _queued;

        public IState Current { get; private set; }
        public IState Previous { get; private set; }

        /// <summary>(previous, next)</summary>
        public event Action<IState, IState> StateChanged;

        public bool IsIn<TState>() where TState : IState => Current is TState;

        public void ChangeState(IState next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            if (_isTransitioning)
            {
                _queued = next;
                return;
            }

            _isTransitioning = true;
            var prev = Current;
            prev?.Exit();
            Previous = prev;
            Current = next;
            next.Enter();
            _isTransitioning = false;
            StateChanged?.Invoke(prev, next);

            if (_queued != null)
            {
                var queued = _queued;
                _queued = null;
                ChangeState(queued);
            }
        }

        public void Tick(float deltaTime) => Current?.Tick(deltaTime);
    }
}

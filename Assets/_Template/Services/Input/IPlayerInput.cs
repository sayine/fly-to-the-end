using UnityEngine;

namespace Template.Services.Input
{
    /// <summary>
    /// The only input API gameplay may use (never UnityEngine.Input / devices directly).
    /// Values are updated once per frame before gameplay Update (AppLifecycle runs at order -1000).
    /// </summary>
    public interface IPlayerInput
    {
        bool Enabled { get; set; }

        /// <summary>Main analog control in [-1, 1] per axis: tilt, or a virtual joystick for touch.</summary>
        Vector2 Axis { get; }

        /// <summary>Finger / mouse button is down.</summary>
        bool IsPressed { get; }

        /// <summary>True for one frame when a short, mostly stationary press ends.</summary>
        bool Tapped { get; }

        /// <summary>Pointer position in screen pixels (last known).</summary>
        Vector2 PointerPosition { get; }

        /// <summary>Pointer movement this frame, normalized by screen height (resolution independent).</summary>
        Vector2 DragDelta { get; }

        /// <summary>Tilt: sets the current device orientation as neutral. Touch: no-op.</summary>
        void Calibrate();

        void Tick(float deltaTime);
    }
}

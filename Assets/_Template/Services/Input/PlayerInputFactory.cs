using Template.Core;

namespace Template.Services.Input
{
    public static class PlayerInputFactory
    {
        /// <summary>Creates the input for config.Mode and ticks it from the lifecycle (before gameplay Update).</summary>
        public static IPlayerInput Create(InputConfig config, IAppLifecycle lifecycle)
        {
            IPlayerInput input = config.Mode == InputMode.Tilt ? new TiltInput(config) : new TouchInput(config);
            if (lifecycle != null) lifecycle.Updated += input.Tick;
            return input;
        }
    }
}

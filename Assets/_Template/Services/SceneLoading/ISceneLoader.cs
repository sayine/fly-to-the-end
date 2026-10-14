using System;

namespace Template.Services.SceneLoading
{
    public interface ISceneLoader
    {
        bool IsLoading { get; }

        /// <summary>Fade out, load async (single mode), fade in. onLoaded runs after activation, before fade-in.</summary>
        void Load(string sceneName, Action onLoaded = null);
    }

    /// <summary>Scene names in Build Settings (kept in sync by Tools/Template/Generate Base Scenes).</summary>
    public static class SceneNames
    {
        public const string Boot = "Boot";
        public const string Menu = "Menu";
        public const string Game = "Game";
        public const string DemoTilt = "Demo_Tilt";
    }
}

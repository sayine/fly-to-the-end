using System;

namespace Template.Editor
{
    /// <summary>
    /// Marks <c>public static void Build(bool force)</c> in a game's Editor assembly that builds the
    /// game-specific Game scene (and its prefabs). When one exists, Tools/Template/Generate calls it
    /// instead of building the placeholder Game scene. It runs after configs, UI prefabs and the template's
    /// Boot/Menu scenes exist, so it may also replace the Menu scene (SceneGenerator.MenuPath) with its own.
    /// The builder honours force like the template does: keep existing content unless force is true.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class GameContentBuilderAttribute : Attribute { }
}

using Template.Services.SceneLoading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Template.Flow
{
    /// <summary>
    /// Editor convenience: pressing Play in Menu or Game redirects to Boot so services exist.
    /// Other scenes (e.g. Demo_Tilt) run standalone. Compiled out of player builds.
    /// </summary>
    internal static class EditorAutoBoot
    {
#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RedirectToBoot()
        {
            var active = SceneManager.GetActiveScene().name;
            if (active != SceneNames.Menu && active != SceneNames.Game) return;
            Debug.Log($"[Boot] Play pressed in '{active}': loading {SceneNames.Boot} first.");
            SceneManager.LoadScene(SceneNames.Boot);
        }
#endif
    }

    internal static class StaticReset
    {
        /// <summary>Supports "Enter Play Mode Options" with domain reload disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Bootstrapper.ResetStatics();
    }
}

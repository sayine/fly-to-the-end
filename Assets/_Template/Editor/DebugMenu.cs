using UnityEditor;
using UnityEngine;

namespace Template.Editor
{
    internal static class DebugMenu
    {
        [MenuItem("Tools/Template/Debug/Clear Save Data (PlayerPrefs)", priority = 100)]
        private static void ClearSaveData()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Log.Info("PlayerPrefs cleared: next Play is a fresh install (session 1).");
        }

        [MenuItem("Tools/Template/Debug/Open Boot Scene", priority = 101)]
        private static void OpenBoot()
        {
            if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(SceneGenerator.ScenesFolder + "/Boot.unity");
        }
    }
}

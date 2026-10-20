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

        /// <summary>Applies JSON copied from the on-device DevTuningPanel ("Copy") to the selected config asset.</summary>
        [MenuItem("Tools/Template/Debug/Paste Tuning JSON Into Selected Config", priority = 102)]
        private static void PasteTuningJson()
        {
            if (!(Selection.activeObject is ScriptableObject config))
            {
                Log.Warn("Select a config asset (ScriptableObject) first.");
                return;
            }
            var json = EditorGUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))
            {
                Log.Warn("Clipboard does not contain tuning JSON.");
                return;
            }
            Undo.RecordObject(config, "Paste Tuning JSON");
            JsonUtility.FromJsonOverwrite(json, config);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
            Log.Info($"Pasted tuning JSON into {AssetDatabase.GetAssetPath(config)}.");
        }

        [MenuItem("Tools/Template/Debug/Open Boot Scene", priority = 101)]
        private static void OpenBoot()
        {
            if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(SceneGenerator.ScenesFolder + "/Boot.unity");
        }
    }
}

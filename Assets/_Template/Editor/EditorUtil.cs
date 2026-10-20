using System.IO;
using UnityEditor;
using UnityEngine;

namespace Template.Editor
{
    public static class EditorUtil
    {
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Loads the asset at path or creates it (never overwrites existing tuning).</summary>
        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            Log.Info($"Created {path}");
            return asset;
        }

        /// <summary>
        /// Assigns a private [SerializeField] by name. Fails loudly if the field was renamed,
        /// so generated wiring never silently breaks.
        /// </summary>
        public static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Log.Error($"{target.GetType().Name}.{field} not found (renamed?)");
                return;
            }
            if (value == null) Log.Error($"{target.GetType().Name}.{field}: assigning null (asset missing?)");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static Material LoadOrCreateMaterial(string path, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(shader) { enableInstancing = true };
            mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}

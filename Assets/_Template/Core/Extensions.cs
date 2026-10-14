using System.Collections.Generic;
using UnityEngine;

namespace Template.Core
{
    public static class VectorExtensions
    {
        public static Vector3 WithX(this Vector3 v, float x) => new Vector3(x, v.y, v.z);
        public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
        public static Vector3 WithZ(this Vector3 v, float z) => new Vector3(v.x, v.y, z);
        public static Vector3 Flat(this Vector3 v) => new Vector3(v.x, 0f, v.z);
        public static Vector2 WithX(this Vector2 v, float x) => new Vector2(x, v.y);
        public static Vector2 WithY(this Vector2 v, float y) => new Vector2(v.x, y);
        public static Vector3 ToXZ(this Vector2 v) => new Vector3(v.x, 0f, v.y);
    }

    public static class MathExtensions
    {
        /// <summary>Linearly maps value from [fromMin, fromMax] to [toMin, toMax] (unclamped).</summary>
        public static float Remap(this float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            var range = fromMax - fromMin;
            if (Mathf.Approximately(range, 0f)) return toMin;
            return toMin + (value - fromMin) / range * (toMax - toMin);
        }

        /// <summary>Returns 0 inside [-deadZone, deadZone], rescaled to keep the output continuous.</summary>
        public static float ApplyDeadZone(this float value, float deadZone)
        {
            var abs = Mathf.Abs(value);
            if (abs <= deadZone) return 0f;
            if (deadZone >= 1f) return 0f;
            return Mathf.Sign(value) * (abs - deadZone) / (1f - deadZone);
        }

        /// <summary>Frame-rate independent exponential smoothing factor.</summary>
        public static float SmoothingFactor(float sharpness, float deltaTime) => 1f - Mathf.Exp(-sharpness * deltaTime);
    }

    public static class ComponentExtensions
    {
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            return go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();
        }

        public static void DestroyChildren(this Transform t)
        {
            for (var i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject);
        }

        public static void SetVisible(this CanvasGroup group, bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);
    }

    public static class CollectionExtensions
    {
        public static T RandomElement<T>(this IReadOnlyList<T> list) => list[Random.Range(0, list.Count)];

        /// <summary>In-place Fisher-Yates shuffle.</summary>
        public static void Shuffle<T>(this IList<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}

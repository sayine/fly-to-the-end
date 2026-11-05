using System;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// UI icons drawn from simple shapes (white, tint with Image.color): play, pause, gear, home, retry, lock, gift,
    /// back, sound, vibrate, plane, video (ad), check, cloud, map. Coordinates are in [-1, 1], y up.
    /// </summary>
    internal static class PaperIcons
    {
        public static Sprite Play, Pause, Gear, Home, Retry, Lock, Gift, Back, Sound, Vibrate, Plane, Video, Check, Cloud, Map, Close;

        public static void Build(bool force)
        {
            Play = Icon("Play", force, p => Poly(p, new[] { V(-0.42f, -0.62f), V(0.68f, 0f), V(-0.42f, 0.62f) }) || RoundJoin(p, new[] { V(-0.42f, -0.62f), V(0.68f, 0f), V(-0.42f, 0.62f) }, 0.12f));
            Pause = Icon("Pause", force, p => RoundBox(p, V(-0.3f, 0f), V(0.17f, 0.62f), 0.1f) || RoundBox(p, V(0.3f, 0f), V(0.17f, 0.62f), 0.1f));
            Gear = Icon("Gear", force, GearShape);
            Home = Icon("Home", force, p =>
                (RoundJoin(p, new[] { V(-0.78f, 0.02f), V(0f, 0.74f), V(0.78f, 0.02f) }, 0.12f) || Poly(p, new[] { V(-0.78f, 0.02f), V(0f, 0.74f), V(0.78f, 0.02f) })
                 || RoundBox(p, V(0f, -0.35f), V(0.56f, 0.42f), 0.1f))
                && !RoundBox(p, V(0f, -0.48f), V(0.16f, 0.3f), 0.06f));
            Retry = Icon("Retry", force, RetryShape);
            Lock = Icon("Lock", force, p =>
                (RoundBox(p, V(0f, -0.32f), V(0.6f, 0.46f), 0.14f) || (Ring(p, V(0f, 0.16f), 0.3f, 0.48f) && p.y > 0.12f) || (Mathf.Abs(Mathf.Abs(p.x) - 0.39f) < 0.09f && p.y > -0.1f && p.y <= 0.16f))
                && !Circ(p, V(0f, -0.26f), 0.13f) && !RoundBox(p, V(0f, -0.45f), V(0.05f, 0.14f), 0.02f));
            Gift = Icon("Gift", force, p =>
                (RoundBox(p, V(0f, -0.35f), V(0.62f, 0.45f), 0.08f) || RoundBox(p, V(0f, 0.22f), V(0.76f, 0.16f), 0.06f)
                 || Ring(p, V(-0.22f, 0.56f), 0.08f, 0.22f) || Ring(p, V(0.22f, 0.56f), 0.08f, 0.22f))
                && !(Mathf.Abs(p.x) < 0.09f && p.y < 0.4f) && !(Mathf.Abs(p.y - 0.04f) < 0.03f));
            Back = Icon("Back", force, p => Segment(p, V(0.25f, 0.62f), V(-0.32f, 0f), 0.16f) || Segment(p, V(-0.32f, 0f), V(0.25f, -0.62f), 0.16f));
            Close = Icon("Close", force, p => Segment(p, V(-0.5f, -0.5f), V(0.5f, 0.5f), 0.15f) || Segment(p, V(-0.5f, 0.5f), V(0.5f, -0.5f), 0.15f));
            Check = Icon("Check", force, p => Segment(p, V(-0.6f, 0.02f), V(-0.16f, -0.44f), 0.16f) || Segment(p, V(-0.16f, -0.44f), V(0.64f, 0.5f), 0.16f));
            Sound = Icon("Sound", force, p =>
                RoundBox(p, V(-0.56f, 0f), V(0.2f, 0.26f), 0.06f) || Poly(p, new[] { V(-0.42f, -0.26f), V(0.08f, -0.66f), V(0.08f, 0.66f), V(-0.42f, 0.26f) })
                || (Arc(p, V(0.1f, 0f), 0.32f, 0.44f, 50f)) || (Arc(p, V(0.1f, 0f), 0.58f, 0.7f, 50f)));
            Vibrate = Icon("Vibrate", force, p =>
                (RoundBox(p, V(0f, 0f), V(0.32f, 0.66f), 0.12f) && !RoundBox(p, V(0f, 0.02f), V(0.2f, 0.46f), 0.04f))
                || Segment(p, V(-0.56f, -0.3f), V(-0.56f, 0.3f), 0.07f) || Segment(p, V(0.56f, -0.3f), V(0.56f, 0.3f), 0.07f)
                || Segment(p, V(-0.78f, -0.18f), V(-0.78f, 0.18f), 0.07f) || Segment(p, V(0.78f, -0.18f), V(0.78f, 0.18f), 0.07f));
            Plane = Icon("Plane", force, p =>
                Poly(p, new[] { V(-0.9f, 0.08f), V(0.9f, 0.58f), V(-0.18f, -0.2f) })
                || Poly(p, new[] { V(-0.18f, -0.2f), V(0.9f, 0.58f), V(-0.02f, -0.72f) }) && !Segment(p, V(-0.18f, -0.2f), V(0.9f, 0.58f), 0.035f));
            Video = Icon("Video", force, p =>
                (RoundBox(p, V(-0.14f, 0f), V(0.62f, 0.46f), 0.14f) && !Poly(p, new[] { V(-0.36f, -0.24f), V(0.16f, 0f), V(-0.36f, 0.24f) }))
                || Poly(p, new[] { V(0.42f, -0.08f), V(0.86f, -0.4f), V(0.86f, 0.4f), V(0.42f, 0.08f) }));
            Cloud = Icon("Cloud", force, p =>
                Circ(p, V(-0.42f, -0.12f), 0.32f) || Circ(p, V(0.04f, 0.14f), 0.44f) || Circ(p, V(0.48f, -0.14f), 0.3f)
                || RoundBox(p, V(0.03f, -0.28f), V(0.46f, 0.16f), 0.1f));
            Map = Icon("Map", force, p =>
                Poly(p, new[] { V(-0.8f, -0.56f), V(-0.8f, 0.62f), V(-0.3f, 0.78f), V(-0.3f, -0.4f) })
                || Poly(p, new[] { V(-0.22f, -0.4f), V(-0.22f, 0.78f), V(0.22f, 0.62f), V(0.22f, -0.56f) })
                || Poly(p, new[] { V(0.3f, -0.56f), V(0.3f, 0.62f), V(0.8f, 0.78f), V(0.8f, -0.4f) }));
        }

        // ---------- Shapes ----------
        private static Vector2 V(float x, float y) => new Vector2(x, y);
        private static bool Circ(Vector2 p, Vector2 c, float r) => (p - c).sqrMagnitude <= r * r;

        private static bool Ring(Vector2 p, Vector2 c, float r0, float r1)
        {
            var d = (p - c).magnitude;
            return d >= r0 && d <= r1;
        }

        /// <summary>Ring segment within ±halfAngle degrees of +x.</summary>
        private static bool Arc(Vector2 p, Vector2 c, float r0, float r1, float halfAngle)
        {
            if (!Ring(p, c, r0, r1)) return false;
            var a = Mathf.Atan2(p.y - c.y, p.x - c.x) * Mathf.Rad2Deg;
            return Mathf.Abs(a) <= halfAngle;
        }

        private static bool RoundBox(Vector2 p, Vector2 c, Vector2 half, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x - c.x) - (half.x - r), Mathf.Abs(p.y - c.y) - (half.y - r));
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) <= r;
        }

        private static bool Segment(Vector2 p, Vector2 a, Vector2 b, float w)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).sqrMagnitude <= w * w;
        }

        /// <summary>Thick outline of a closed polygon (rounds off corners when unioned with the fill).</summary>
        private static bool RoundJoin(Vector2 p, Vector2[] poly, float w)
        {
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if (Segment(p, poly[j], poly[i], w)) return true;
            return false;
        }

        private static bool Poly(Vector2 p, Vector2[] poly)
        {
            var inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        private static bool GearShape(Vector2 p)
        {
            var d = p.magnitude;
            if (d < 0.26f) return false;
            if (d <= 0.6f) return true;
            if (d > 0.9f) return false;
            // 8 rounded teeth.
            var a = Mathf.Atan2(p.y, p.x);
            var sector = Mathf.Round(a / (Mathf.PI / 4f)) * (Mathf.PI / 4f);
            var local = Rotate(p, -sector);
            return RoundBox(local, V(0.72f, 0f), V(0.18f, 0.16f), 0.06f);
        }

        private static bool RetryShape(Vector2 p)
        {
            // Ring with a gap at the top-right, arrowhead at the gap.
            var d = p.magnitude;
            var a = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
            if (d >= 0.42f && d <= 0.64f && !(a > 35f && a < 100f)) return true;
            var end = 35f * Mathf.Deg2Rad;
            var c = new Vector2(Mathf.Cos(end), Mathf.Sin(end)) * 0.53f;
            var radial = new Vector2(Mathf.Cos(end), Mathf.Sin(end));
            var tangent = new Vector2(-radial.y, radial.x); // counter-clockwise travel
            return Poly(p, new[] { c + radial * 0.3f, c + tangent * 0.36f, c - radial * 0.3f });
        }

        private static Vector2 Rotate(Vector2 v, float a)
        {
            var c = Mathf.Cos(a);
            var s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private static Sprite Icon(string name, bool force, Func<Vector2, bool> inside)
        {
            const int size = 128;
            return PaperKit.SpriteAsset(name, size, 0, force, px =>
            {
                var p = new Vector2(px.x / size * 2f - 1f, px.y / size * 2f - 1f);
                return inside(p) ? 1f : 0f;
            }, folder: PaperKit.Folder + "/Icons");
        }
    }
}

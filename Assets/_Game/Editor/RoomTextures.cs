using System.IO;
using Template.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Procedural, seamlessly tiling room textures (no downloads, no licences, tuned to the soft flat look):
    /// oak plank floor and kitchen tiles (+ normal maps), carpet, striped wallpaper, one near-white plaster that each
    /// room tints through its material colour (shared plaster normal), ceiling.
    /// Each material samples its texture with world-scaled UVs (see RoomLayout), so TileMetres below is the
    /// real-world size one texture repeat covers.
    /// </summary>
    internal static class RoomTextures
    {
        private const string Folder = "Assets/_Game/Art/Textures/Room";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";

        public const float FloorTileMetres = 2.4f * RoomLayout.FurnitureScale; // 12 planks of ~32 cm: scaled like the furniture
        public const float WallTileMetres = 1.6f;
        public const float CeilingTileMetres = 3f;

        public const float TileFloorTileMetres = 0.4f * 4f * RoomLayout.FurnitureScale; // 4 × 4 ceramic tiles of ~64 cm
        public const float CarpetTileMetres = 3f;

        public struct Set
        {
            public Material FloorOak, FloorTiles, CarpetLavender, CarpetPeach;
            public Material WallLiving, WallHall, WallKitchen, WallStudy, WallBed1, WallBed2;
            public Material Ceiling, Trim;
        }

        /// <summary>Creates (or, with force, regenerates) the textures and returns the room materials.</summary>
        public static Set Ensure(bool force)
        {
            EditorUtil.EnsureFolder(Folder);
            var floor = Texture("FloorOak", 1024, force, false, FloorOak);
            var floorNormal = Texture("FloorOak_Normal", 1024, force, true, FloorOakHeight);
            var tiles = Texture("FloorTiles", 512, force, false, FloorTiles);
            var tilesNormal = Texture("FloorTiles_Normal", 512, force, true, FloorTilesHeight);
            var carpet = Texture("Carpet", 512, force, false, (u, v) => Plaster(u, v, Color.white, 0.07f) * (0.94f + 0.06f * Fbm(u, v, 40, 40, 2, 21)));
            var wallpaper = Texture("WallpaperStripes", 512, force, false,
                (u, v) => Plaster(u, v, new Color(0.96f, 0.9f, 0.8f), 0.035f) * Stripes(u, 8, new Color(0.93f, 0.9f, 0.85f)));
            // One near-white plaster for every plaster wall; each room tints it with the material colour.
            var plaster = Texture("PlasterWhite", 512, force, false, (u, v) => Plaster(u, v, Color.white, 0.05f));
            var plasterNormal = Texture("Plaster_Normal", 512, force, true, (u, v) => Fbm(u, v, 6, 6, 4, 11) * 0.5f + 0.5f);
            var ceiling = Texture("CeilingPlaster", 256, force, false, (u, v) => Plaster(u, v, new Color(0.98f, 0.97f, 0.94f), 0.03f));

            return new Set
            {
                FloorOak = MakeMaterial("Room_Floor", floor, floorNormal, 0.9f, Color.white),
                FloorTiles = MakeMaterial("Room_Floor_Kitchen", tiles, tilesNormal, 0.8f, Color.white),
                CarpetLavender = MakeMaterial("Room_Floor_Bed1", carpet, null, 0f, new Color(0.78f, 0.74f, 0.88f)),
                CarpetPeach = MakeMaterial("Room_Floor_Bed2", carpet, null, 0f, new Color(0.97f, 0.8f, 0.7f)),
                WallLiving = MakeMaterial("Room_Wall", wallpaper, plasterNormal, 0.35f, Color.white),
                WallHall = MakeMaterial("Room_Wall_Hall", plaster, plasterNormal, 0.35f, new Color(0.93f, 0.84f, 0.72f)),
                WallKitchen = MakeMaterial("Room_Wall_Kitchen", plaster, plasterNormal, 0.35f, new Color(0.76f, 0.84f, 0.74f)),
                WallStudy = MakeMaterial("Room_Wall_Study", plaster, plasterNormal, 0.35f, new Color(0.66f, 0.74f, 0.8f)),
                WallBed1 = MakeMaterial("Room_Wall_Bed1", plaster, plasterNormal, 0.35f, new Color(0.74f, 0.83f, 0.94f)),
                WallBed2 = MakeMaterial("Room_Wall_Bed2", plaster, plasterNormal, 0.35f, new Color(0.98f, 0.82f, 0.84f)),
                Ceiling = MakeMaterial("Room_Ceiling", ceiling, null, 0f, Color.white),
                Trim = EditorUtil.LoadOrCreateMaterial($"{MaterialsFolder}/Room_Trim.mat", new Color(0.97f, 0.95f, 0.92f)),
            };
        }

        // ---------- Kitchen tiles ----------
        private const int TilesAcross = 4;

        private static Color FloorTiles(float u, float v)
        {
            var groutAmount = Grout(u, v, out var tile);
            // Soft cream / pale terracotta checker with a little per-tile variation.
            var checker = ((tile.x + tile.y) & 1) == 0;
            var baseColor = checker ? new Color(0.95f, 0.93f, 0.88f) : new Color(0.9f, 0.78f, 0.68f);
            var c = baseColor * Mathf.Lerp(0.95f, 1.04f, Hash(tile.x * 17 + tile.y, 9)) * (1f + 0.03f * Fbm(u, v, 8, 8, 3, 31));
            c = Color.Lerp(c, new Color(0.72f, 0.7f, 0.66f), groutAmount);
            c.a = 1f;
            return c;
        }

        private static float FloorTilesHeight(float u, float v) => 1f - Grout(u, v, out _) * 0.8f;

        private static float Grout(float u, float v, out Vector2Int tile)
        {
            var x = u * TilesAcross;
            var y = v * TilesAcross;
            tile = new Vector2Int(Mathf.FloorToInt(x), Mathf.FloorToInt(y));
            var fx = x - tile.x;
            var fy = y - tile.y;
            var edge = Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fy, 1f - fy)) / TilesAcross;
            return 1f - Mathf.SmoothStep(0f, 1f, edge / 0.004f);
        }

        // ---------- Patterns (u, v in [0, 1): one repeat) ----------
        private const int Rows = 12;

        private static Color FloorOak(float u, float v)
        {
            var plank = Plank(u, v, out var fu, out var fv, out var seam);
            var tone = Mathf.Lerp(0.86f, 1.06f, Hash(plank, 3));
            var oak = Color.Lerp(new Color(0.76f, 0.56f, 0.38f), new Color(0.84f, 0.64f, 0.45f), Hash(plank, 7)) * tone;
            // Grain: fine streaks along the plank, warped by noise, plus a few darker knots of tone.
            var warp = Fbm(u, v, 3, Rows * 2, 3, plank % 17 + 1) * 4f;
            var streak = 0.5f + 0.5f * Mathf.Sin((fv * 9f + Hash(plank, 11) * 6f + warp) * Mathf.PI * 2f);
            var grain = Mathf.Lerp(0.93f, 1.04f, streak) * Mathf.Lerp(0.95f, 1.03f, Fbm(u, v, 6, Rows * 6, 3, 5) * 0.5f + 0.5f);
            var c = oak * grain;
            c = Color.Lerp(c, c * 0.55f, seam); // dark seams between planks
            c.a = 1f;
            return c;
        }

        private static float FloorOakHeight(float u, float v)
        {
            Plank(u, v, out _, out _, out var seam);
            var grain = Fbm(u, v, 6, Rows * 6, 3, 5) * 0.06f;
            return 1f - seam * 0.9f + grain;
        }

        /// <summary>Plank id at (u, v); local plank coords; seam 0..1 near plank edges. Tiles in u and v.</summary>
        private static int Plank(float u, float v, out float fu, out float fv, out float seam)
        {
            var row = Mathf.Min(Rows - 1, Mathf.FloorToInt(v * Rows));
            fv = v * Rows - row;
            // Each row is cut into 2-3 planks of uneven length, starting at a random offset (wraps around).
            var pieces = 2 + (int)(Hash(row, 1) * 1.99f);
            var offset = Hash(row, 2);
            var x = Mathf.Repeat(u - offset, 1f);
            var start = 0f;
            var index = 0;
            float end = 1f;
            for (var i = 0; i < pieces; i++)
            {
                end = i == pieces - 1 ? 1f : start + (1f / pieces) * Mathf.Lerp(0.75f, 1.25f, Hash(row * 7 + i, 4));
                if (x < end || i == pieces - 1) { index = i; break; }
                start = end;
            }
            fu = (x - start) / Mathf.Max(1e-4f, end - start);
            var edgeV = Mathf.Min(fv, 1f - fv) * (1f / Rows);           // in repeat units
            var edgeU = Mathf.Min(x - start, end - x);
            var edge = Mathf.Min(edgeV, edgeU);
            seam = 1f - Mathf.SmoothStep(0f, 1f, edge / 0.0022f);
            return row * 8 + index;
        }

        private static Color Plaster(float u, float v, Color baseColor, float variation)
        {
            var n = Fbm(u, v, 5, 5, 4, 3);
            var c = baseColor * (1f + n * variation * 2f);
            c.a = 1f;
            return c;
        }

        private static Color Stripes(float u, int count, Color tint)
        {
            var s = 0.5f + 0.5f * Mathf.Cos(u * count * Mathf.PI * 2f);
            var t = Mathf.SmoothStep(0.35f, 0.65f, s);
            return Color.Lerp(Color.white, tint, t);
        }

        // ---------- Tileable noise ----------
        /// <summary>Perlin noise that repeats every 1 in u and v (periodU/periodV cells per repeat), roughly in [-1, 1].</summary>
        private static float Tile(float u, float v, float periodU, float periodV, int seed)
        {
            var x = u * periodU;
            var y = v * periodV;
            var ox = seed * 17.13f;
            var oy = seed * 31.7f;
            float N(float a, float b) => Mathf.PerlinNoise(a + ox, b + oy) * 2f - 1f;
            var wx = x / periodU;
            var wy = y / periodV;
            return N(x, y) * (1 - wx) * (1 - wy) + N(x - periodU, y) * wx * (1 - wy)
                 + N(x, y - periodV) * (1 - wx) * wy + N(x - periodU, y - periodV) * wx * wy;
        }

        private static float Fbm(float u, float v, float periodU, float periodV, int octaves, int seed)
        {
            var sum = 0f;
            var amp = 0.5f;
            var norm = 0f;
            for (var i = 0; i < octaves; i++)
            {
                sum += Tile(u, v, periodU, periodV, seed + i * 13) * amp;
                norm += amp;
                amp *= 0.5f;
                periodU *= 2f;
                periodV *= 2f;
            }
            return sum / norm;
        }

        private static float Hash(int n, int salt)
        {
            unchecked
            {
                var h = (uint)(n * 374761393 + salt * 668265263);
                h = (h ^ (h >> 13)) * 1274126177;
                return (h ^ (h >> 16)) / (float)uint.MaxValue;
            }
        }

        // ---------- Texture / material assets ----------
        private delegate Color ColorFn(float u, float v);
        private delegate float HeightFn(float u, float v);

        private static Texture2D Texture(string name, int size, bool force, bool normal, ColorFn color) =>
            Write(name, size, force, normal, (u, v) => color(u, v));

        private static Texture2D Texture(string name, int size, bool force, bool normal, HeightFn height)
        {
            if (!normal) return Write(name, size, force, false, (u, v) => { var h = height(u, v); return new Color(h, h, h, 1f); });
            // Normal map from the height field by central differences (wrapping, so it tiles too).
            var heights = new float[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                heights[y * size + x] = height((x + 0.5f) / size, (y + 0.5f) / size);
            const float strength = 3f;
            return Write(name, size, force, true, (u, v) =>
            {
                var x = Mathf.Clamp((int)(u * size), 0, size - 1);
                var y = Mathf.Clamp((int)(v * size), 0, size - 1);
                float H(int ix, int iy) => heights[((iy + size) % size) * size + (ix + size) % size];
                var dx = (H(x + 1, y) - H(x - 1, y)) * strength;
                var dy = (H(x, y + 1) - H(x, y - 1)) * strength;
                var n = new Vector3(-dx, -dy, 1f).normalized;
                return new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            });
        }

        private static Texture2D Write(string name, int size, bool force, bool normal, System.Func<float, float, Color> pixel)
        {
            var path = $"{Folder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null && !force) return existing;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                pixels[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
            tex.SetPixels(pixels);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.anisoLevel = normal ? 1 : 4; // the floor is seen at grazing angles
            importer.maxTextureSize = size;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material MakeMaterial(string name, Texture2D albedo, Texture2D normal, float bumpScale, Color tint)
        {
            var mat = EditorUtil.LoadOrCreateMaterial($"{MaterialsFolder}/{name}.mat", tint);
            mat.SetColor("_BaseColor", tint);
            mat.SetTexture("_BaseMap", albedo);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", bumpScale);
                mat.EnableKeyword("_NORMALMAP");
            }
            else
            {
                mat.SetTexture("_BumpMap", null);
                mat.DisableKeyword("_NORMALMAP");
            }
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}

using System.Collections.Generic;
using Game;
using Game.Demo;
using Template.Flow;
using Template.Services.Ads;
using Template.Services.Input;
using Template.Services.SceneLoading;
using Template.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Template.Editor
{
    /// <summary>
    /// Reproducible project content: configs, materials, UI prefabs and the Boot/Menu/Game/Demo_Tilt scenes.
    /// "Generate" keeps existing assets (your edits survive); "Regenerate (overwrite)" rebuilds everything.
    /// Batchmode: -executeMethod Template.Editor.SceneGenerator.GenerateBatch [-force]
    /// </summary>
    public static class SceneGenerator
    {
        public const string ScenesFolder = "Assets/Scenes";
        public const string GameScenesFolder = "Assets/_Game/Scenes";
        public const string ConfigsFolder = "Assets/_Game/Configs";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";

        private static string BootPath => $"{ScenesFolder}/{SceneNames.Boot}.unity";
        public static string MenuPath => $"{ScenesFolder}/{SceneNames.Menu}.unity";
        public static string GamePath => $"{ScenesFolder}/{SceneNames.Game}.unity";
        private static string DemoPath => $"{GameScenesFolder}/{SceneNames.DemoTilt}.unity";

        private static readonly Color SkyColor = new Color(0.55f, 0.75f, 0.95f);

        [MenuItem("Tools/Template/Generate Base Scenes", priority = 20)]
        public static void Generate() => Run(false);

        [MenuItem("Tools/Template/Regenerate Base Scenes (overwrite)", priority = 21)]
        public static void Regenerate()
        {
            if (Application.isBatchMode || EditorUtility.DisplayDialog("Regenerate base scenes",
                    "Overwrite Boot/Menu/Game/Demo_Tilt scenes and all UI prefabs? Configs and materials are kept.", "Overwrite", "Cancel"))
                Run(true);
        }

        public static void GenerateBatch()
        {
            var force = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-force") >= 0;
            Run(force);
        }

        private static void Run(bool force)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var configs = CreateConfigs();
            UIBuilder.BuildAll(force);
            var mats = CreateMaterials();

            EditorUtil.EnsureFolder(ScenesFolder);
            EditorUtil.EnsureFolder(GameScenesFolder);
            if (force || !Exists(BootPath)) BuildBoot(configs);
            if (force || !Exists(MenuPath)) BuildMenu(mats);
            var gameBuilder = FindGameContentBuilder();
            if (gameBuilder != null) gameBuilder.Invoke(null, new object[] { force });
            else if (force || !Exists(GamePath)) BuildGame(configs, mats);
            if (force || !Exists(DemoPath)) BuildDemo(configs, mats);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootPath, true),
                new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(GamePath, true),
                new EditorBuildSettingsScene(DemoPath, false), // dev-only: open it in the Editor and press Play
            };

            AssetDatabase.SaveAssets();
            if (!Application.isBatchMode) EditorSceneManager.OpenScene(BootPath);
            Log.Info($"Base scenes ready (force={force}). Build order: Boot, Menu, Game (+ Demo_Tilt disabled).");
        }

        public static bool Exists(string path) => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;

        /// <summary>The game's [GameContentBuilder] method (static void Build(bool force)), or null for the placeholder.</summary>
        private static System.Reflection.MethodInfo FindGameContentBuilder()
        {
            var methods = TypeCache.GetMethodsWithAttribute<GameContentBuilderAttribute>();
            if (methods.Count == 0) return null;
            if (methods.Count > 1) Log.Warn($"{methods.Count} [GameContentBuilder] methods found; using {methods[0].DeclaringType?.Name}.{methods[0].Name}.");
            var m = methods[0];
            var ps = m.GetParameters();
            if (m.IsStatic && ps.Length == 1 && ps[0].ParameterType == typeof(bool)) return m;
            Log.Error($"[GameContentBuilder] {m.DeclaringType?.Name}.{m.Name} must be 'static void Build(bool force)'. Using the placeholder Game scene.");
            return null;
        }

        // ---------- Configs ----------
        // NewScene() unloads unreferenced assets, so cached references go stale between scenes:
        // always resolve assets by path at the point of use.
        private const string AdConfigPath = ConfigsFolder + "/AdConfig.asset";
        private const string InputConfigPath = ConfigsFolder + "/InputConfig.asset";
        private const string GameConfigPath = ConfigsFolder + "/GameConfig.asset";

        private struct Configs
        {
            public AdConfig Ad => AssetDatabase.LoadAssetAtPath<AdConfig>(AdConfigPath);
            public InputConfig Input => AssetDatabase.LoadAssetAtPath<InputConfig>(InputConfigPath);
            public GameConfig Game => AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            public ScreenManager UIRoot => AssetDatabase.LoadAssetAtPath<ScreenManager>(UIBuilder.RootPath);
        }

        private struct Materials
        {
            public Material Ground => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Ground.mat");
            public Material Ball => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Ball.mat");
            public Material Accent => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Accent.mat");
        }

        private static Configs CreateConfigs()
        {
            EditorUtil.LoadOrCreate<AdConfig>(AdConfigPath);
            EditorUtil.LoadOrCreate<InputConfig>(InputConfigPath);
            EditorUtil.LoadOrCreate<GameConfig>(GameConfigPath);
            EditorUtil.LoadOrCreate<Secrets>($"{ConfigsFolder}/Secrets.example.asset");
            AssetDatabase.SaveAssets();
            return new Configs();
        }

        private static Materials CreateMaterials()
        {
            EditorUtil.LoadOrCreateMaterial($"{MaterialsFolder}/Ground.mat", new Color(0.93f, 0.93f, 0.9f));
            EditorUtil.LoadOrCreateMaterial($"{MaterialsFolder}/Ball.mat", new Color(1f, 0.42f, 0.35f));
            EditorUtil.LoadOrCreateMaterial($"{MaterialsFolder}/Accent.mat", new Color(0.3f, 0.55f, 1f));
            AssetDatabase.SaveAssets();
            return new Materials();
        }

        // ---------- Scenes ----------
        /// <summary>Empty scene with the template's flat lighting (no GI, no skybox, no fog).</summary>
        public static Scene NewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.66f, 0.72f);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            Lightmapping.bakedGI = false;
            Lightmapping.realtimeGI = false;
            return scene;
        }

        private static void Save(Scene scene, string path)
        {
            EditorSceneManager.SaveScene(scene, path);
            Log.Info($"Saved {path}");
        }

        private static void BuildBoot(Configs configs)
        {
            var scene = NewScene();
            CreateCamera(new Color(0.08f, 0.09f, 0.12f));
            var boot = new GameObject("Bootstrapper").AddComponent<Bootstrapper>();
            EditorUtil.Set(boot, "_adConfig", configs.Ad);
            EditorUtil.Set(boot, "_inputConfig", configs.Input);
            EditorUtil.Set(boot, "_uiRootPrefab", configs.UIRoot);
            Save(scene, BootPath);
        }

        private static void BuildMenu(Materials mats)
        {
            var scene = NewScene();
            var cam = CreateCamera(SkyColor);
            cam.transform.SetPositionAndRotation(new Vector3(0, 6, -9), Quaternion.Euler(30, 0, 0));
            CreateLight();
            CreatePlatform(mats.Ground, 8f);
            var ball = CreateBall(mats.Ball);
            Object.DestroyImmediate(ball.GetComponent<Rigidbody>()); // decoration only
            var deco = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deco.name = "Deco";
            deco.transform.SetPositionAndRotation(new Vector3(2.2f, 0.5f, 1.5f), Quaternion.Euler(0, 30, 0));
            deco.GetComponent<Renderer>().sharedMaterial = mats.Accent;
            Save(scene, MenuPath);
        }

        private static void BuildGame(Configs configs, Materials mats)
        {
            var scene = NewScene();
            var cam = CreateCamera(SkyColor);
            CreateLight();
            CreatePlatform(mats.Ground, configs.Game.PlatformSize);
            var ball = CreateBall(mats.Ball);
            var tiltBall = ball.AddComponent<TiltBall>();
            EditorUtil.Set(tiltBall, "_config", configs.Game);
            EditorUtil.Set(tiltBall, "_body", ball.GetComponent<Rigidbody>());
            AddFollow(cam, configs.Game, ball.transform);

            var game = new GameObject("PlaceholderGame").AddComponent<PlaceholderGame>();
            EditorUtil.Set(game, "_config", configs.Game);
            EditorUtil.Set(game, "_ball", tiltBall);
            Save(scene, GamePath);
        }

        private static void BuildDemo(Configs configs, Materials mats)
        {
            var scene = NewScene();
            var cam = CreateCamera(SkyColor);
            CreateLight();
            CreatePlatform(mats.Ground, configs.Game.PlatformSize * 1.5f);
            // A few bumps so movement is visible.
            var offsets = new List<Vector3> { new Vector3(3, 0.25f, 3), new Vector3(-3, 0.25f, -2), new Vector3(-2, 0.25f, 4) };
            foreach (var p in offsets)
            {
                var bump = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bump.name = "Bump";
                bump.transform.position = p;
                bump.transform.localScale = new Vector3(1.5f, 0.5f, 1.5f);
                bump.GetComponent<Renderer>().sharedMaterial = mats.Accent;
            }
            var ball = CreateBall(mats.Ball);
            var tiltBall = ball.AddComponent<TiltBall>();
            EditorUtil.Set(tiltBall, "_config", configs.Game);
            EditorUtil.Set(tiltBall, "_body", ball.GetComponent<Rigidbody>());
            AddFollow(cam, configs.Game, ball.transform);

            var demo = new GameObject("TiltDemo").AddComponent<TiltDemo>();
            EditorUtil.Set(demo, "_inputConfig", configs.Input);
            EditorUtil.Set(demo, "_gameConfig", configs.Game);
            EditorUtil.Set(demo, "_ball", tiltBall);
            Save(scene, DemoPath);
        }

        // ---------- Scene primitives ----------
        private static Camera CreateCamera(Color background)
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            var cam = go.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.fieldOfView = 50f;
            cam.farClipPlane = 100f;
            cam.transform.SetPositionAndRotation(new Vector3(0, 12, -7), Quaternion.Euler(60, 0, 0));
            return cam;
        }

        private static void CreateLight()
        {
            var go = new GameObject("Directional Light", typeof(Light));
            var light = go.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Hard;
            go.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        private static void CreatePlatform(Material mat, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Platform";
            go.transform.position = new Vector3(0, -0.25f, 0);
            go.transform.localScale = new Vector3(size, 0.5f, size);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        private static GameObject CreateBall(Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Ball";
            go.transform.position = new Vector3(0, 0.5f, 0);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            var body = go.AddComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.mass = 1f;
            return go;
        }

        private static void AddFollow(Camera cam, GameConfig config, Transform target)
        {
            var follow = cam.gameObject.AddComponent<FollowCamera>();
            EditorUtil.Set(follow, "_config", config);
            EditorUtil.Set(follow, "_target", target);
        }
    }
}

using Game.Flight;
using Game.Menu;
using Template.Editor;
using Template.Services.Input;
using Template.UI.DevTools;
using Template.UI.Screens;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Generates the Paper Flight content: configs, materials, the paper-plane mesh + prefab, the Game scene
    /// (rooms, Meshy props, wind, chase camera, touch hint, PaperPlaneGame, dev tuning panel) and the Menu scene.
    /// Runs from Tools/Template/Generate (via [GameContentBuilder]) or Tools/Game/Build Paper Plane Scenes.
    /// Batchmode: -executeMethod Game.Editor.PaperPlaneContentBuilder.BuildBatch
    /// </summary>
    public static class PaperPlaneContentBuilder
    {
        private const string ConfigsFolder = "Assets/_Game/Configs";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";
        private const string MeshPath = "Assets/_Game/Art/Meshes/PaperPlane.asset";
        private const string PlanePrefabPath = "Assets/_Game/Prefabs/PaperPlane.prefab";
        private const string FlightConfigPath = ConfigsFolder + "/FlightConfig.asset";
        private const string GameConfigPath = ConfigsFolder + "/GameConfig.asset";
        private const string InputConfigPath = ConfigsFolder + "/InputConfig.asset";
        private const string ScreensFolder = "Assets/_Game/Prefabs/UI/Screens";

        private static readonly Color Background = new Color(0.96f, 0.88f, 0.78f);

        [MenuItem("Tools/Game/Build Paper Plane Scenes (overwrite)", priority = 0)]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(true);
            EditorSceneManager.OpenScene(SceneGenerator.GamePath);
        }

        public static void BuildBatch() => Build(true);

        /// <summary>Called by Tools/Template/Generate. force=false keeps an existing Paper Plane Game scene.</summary>
        private static bool _regenerateTextures;

        [GameContentBuilder]
        public static void Build(bool force)
        {
            _regenerateTextures = force;
            EditorUtil.LoadOrCreate<FlightConfig>(FlightConfigPath);
            WindLayout.LoadOrCreateConfig();
            AssetDatabase.SaveAssets();
            PatchScoreDisplay();
            CreateMaterials();
            var mesh = BuildMesh();
            BuildPlanePrefab(mesh, force);

            if (!force && SceneGenerator.Exists(SceneGenerator.GamePath) && UsesScript<PaperPlaneGame>(SceneGenerator.GamePath))
                Log.Info("Paper Plane Game scene exists, kept (use Tools/Game/Build Paper Plane Scenes to rebuild).");
            else BuildScene();

            if (!force && SceneGenerator.Exists(SceneGenerator.MenuPath) && UsesScript<MenuBackdrop>(SceneGenerator.MenuPath))
                Log.Info("Paper Plane Menu scene exists, kept.");
            else BuildMenuScene();
        }

        /// <summary>True if the scene uses script T (i.e. it's ours, not the template's placeholder).</summary>
        private static bool UsesScript<T>(string path) where T : MonoBehaviour
        {
            string script = null;
            foreach (var guid in AssetDatabase.FindAssets($"{typeof(T).Name} t:MonoScript"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.LoadAssetAtPath<MonoScript>(p)?.GetClass() == typeof(T)) script = p;
            }
            if (script == null) return false;
            foreach (var dep in AssetDatabase.GetDependencies(path, false))
                if (dep == script) return true;
            return false;
        }

        // ---------- UI ----------
        /// <summary>Score is airborne time in tenths of a second: show it with one decimal.</summary>
        private static void PatchScoreDisplay()
        {
            PatchPrefab<HUDScreen>($"{ScreensFolder}/HUDScreen.prefab", so =>
            {
                so.FindProperty("_scoreFormat").stringValue = "{0:0.0}";
                so.FindProperty("_scoreScale").floatValue = 0.1f;
            });
            PatchPrefab<GameOverScreen>($"{ScreensFolder}/GameOverScreen.prefab", so =>
            {
                so.FindProperty("_scoreFormat").stringValue = "{0:0.0}s";
                so.FindProperty("_bestFormat").stringValue = "BEST {0:0.0}s";
                so.FindProperty("_scoreScale").floatValue = 0.1f;
            });
        }

        private static void PatchPrefab<T>(string path, System.Action<SerializedObject> patch) where T : Component
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Log.Error($"{path} missing: run Tools/Template/Generate Base Scenes first.");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var component = root.GetComponent<T>();
                var so = new SerializedObject(component);
                patch(so);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---------- Materials ----------
        private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/{name}.mat");

        private static void CreateMaterials()
        {
            EditorUtil.LoadOrCreateMaterial($"{MaterialsFolder}/Room_Floor.mat", new Color(0.78f, 0.62f, 0.48f));
            EditorUtil.LoadOrCreateMaterial($"{MaterialsFolder}/Room_Wall.mat", new Color(0.95f, 0.9f, 0.82f));
            EditorUtil.LoadOrCreateMaterial($"{MaterialsFolder}/Room_Ceiling.mat", new Color(0.98f, 0.97f, 0.94f));
            EditorUtil.LoadOrCreateMaterial($"{MaterialsFolder}/Prop_A.mat", new Color(0.45f, 0.62f, 0.7f));
            EditorUtil.LoadOrCreateMaterial($"{MaterialsFolder}/Prop_B.mat", new Color(0.88f, 0.6f, 0.45f));
            EditorUtil.LoadOrCreateMaterial($"{MaterialsFolder}/Prop_C.mat", new Color(0.55f, 0.72f, 0.5f));
            EditorUtil.LoadOrCreateMaterial($"{MaterialsFolder}/LaunchPad.mat", new Color(0.4f, 0.32f, 0.28f));
            EditorUtil.LoadOrCreateMaterial($"{MaterialsFolder}/Plane.mat", new Color(1f, 1f, 1f));

            const string arrowPath = MaterialsFolder + "/AimArrow.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(arrowPath) == null)
            {
                // Sprites/Default: unlit, alpha-blended, uses LineRenderer vertex colors; works in URP.
                AssetDatabase.CreateAsset(new Material(Shader.Find("Sprites/Default")), arrowPath);
            }
            AssetDatabase.SaveAssets();
        }

        // ---------- Plane mesh + prefab ----------
        /// <summary>Low-poly dart: two wings with a little dihedral and a keel, ~0.25 m long, double-sided.</summary>
        private static Mesh BuildMesh()
        {
            EditorUtil.EnsureFolder("Assets/_Game/Art/Meshes");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            var create = mesh == null;
            if (create) mesh = new Mesh { name = "PaperPlane" };

            var nose = new Vector3(0f, 0f, 0.13f);
            var tail = new Vector3(0f, 0f, -0.12f);
            var leftTip = new Vector3(-0.11f, 0.018f, -0.12f);
            var rightTip = new Vector3(0.11f, 0.018f, -0.12f);
            var keel = new Vector3(0f, -0.03f, -0.12f);
            var faces = new[]
            {
                new[] { nose, tail, leftTip },   // left wing (top: clockwise seen from above)
                new[] { nose, rightTip, tail },  // right wing
                new[] { nose, keel, tail },      // keel
            };

            var vertices = new Vector3[faces.Length * 6];
            var triangles = new int[faces.Length * 6];
            var v = 0;
            foreach (var f in faces)
            {
                // Front and back face with separate vertices so normals are flat on both sides.
                vertices[v] = f[0]; vertices[v + 1] = f[1]; vertices[v + 2] = f[2];
                vertices[v + 3] = f[0]; vertices[v + 4] = f[2]; vertices[v + 5] = f[1];
                for (var i = 0; i < 6; i++) triangles[v + i] = v + i;
                v += 6;
            }
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (create) AssetDatabase.CreateAsset(mesh, MeshPath);
            else EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            return mesh;
        }

        private static void BuildPlanePrefab(Mesh mesh, bool force)
        {
            if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(PlanePrefabPath) != null) return;

            var root = new GameObject("PaperPlane");
            var body = root.AddComponent<Rigidbody>();
            body.mass = 0.05f;
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.angularDamping = 0.5f;

            // Slightly smaller than the mesh: forgiving near misses (SPEC "Technical notes").
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.17f, 0.035f, 0.21f);
            collider.center = new Vector3(0f, -0.005f, 0f);

            var model = new GameObject("Model", typeof(MeshFilter), typeof(MeshRenderer));
            model.transform.SetParent(root.transform, false);
            model.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = model.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Mat("Plane");
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var plane = root.AddComponent<PaperPlane>();
            EditorUtil.Set(plane, "_config", AssetDatabase.LoadAssetAtPath<FlightConfig>(FlightConfigPath));
            EditorUtil.Set(plane, "_body", body);
            EditorUtil.Set(plane, "_collider", collider);

            EditorUtil.EnsureFolder("Assets/_Game/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(root, PlanePrefabPath);
            Object.DestroyImmediate(root);
            Log.Info($"Saved {PlanePrefabPath}");
        }

        // ---------- Scene ----------
        private static void BuildScene()
        {
            var scene = SceneGenerator.NewScene();
            var cam = CreateCamera();
            CreateLight();
            var pad = BuildEnvironment();

            var launchPoint = new GameObject("LaunchPoint").transform;
            launchPoint.SetPositionAndRotation(RoomLayout.LaunchPosition, Quaternion.identity);

            var planePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlanePrefabPath);
            var planeGo = (GameObject)PrefabUtility.InstantiatePrefab(planePrefab);
            planeGo.transform.SetPositionAndRotation(RoomLayout.LaunchPosition, Quaternion.identity);
            var plane = planeGo.GetComponent<PaperPlane>();

            var arrowGo = new GameObject("AimArrow", typeof(LineRenderer));
            var arrow = arrowGo.GetComponent<LineRenderer>();
            arrow.sharedMaterial = Mat("AimArrow");
            arrow.positionCount = 2;
            arrow.useWorldSpace = true;
            arrow.widthCurve = new AnimationCurve(new Keyframe(0f, 0.035f), new Keyframe(1f, 0.005f));
            arrow.startColor = new Color(1f, 1f, 1f, 0.55f);
            arrow.endColor = new Color(1f, 1f, 1f, 0.1f);
            arrow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            arrow.receiveShadows = false;
            arrow.enabled = false;

            var chase = cam.gameObject.AddComponent<ChaseCamera>();
            EditorUtil.Set(chase, "_config", AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath));
            EditorUtil.Set(chase, "_plane", plane);
            EditorUtil.Set(chase, "_camera", cam);
            var chaseSo = new SerializedObject(chase);
            var areas = chaseSo.FindProperty("_areas");
            areas.arraySize = RoomLayout.Areas.Length;
            for (var i = 0; i < RoomLayout.Areas.Length; i++) areas.GetArrayElementAtIndex(i).boundsValue = RoomLayout.Areas[i];
            chaseSo.ApplyModifiedPropertiesWithoutUndo();

            var game = new GameObject("PaperPlaneGame").AddComponent<PaperPlaneGame>();
            EditorUtil.Set(game, "_gameConfig", AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath));
            EditorUtil.Set(game, "_flightConfig", AssetDatabase.LoadAssetAtPath<FlightConfig>(FlightConfigPath));
            EditorUtil.Set(game, "_plane", plane);
            EditorUtil.Set(game, "_launchPoint", launchPoint);
            EditorUtil.Set(game, "_launchPad", pad.GetComponent<Collider>());
            EditorUtil.Set(game, "_chaseCamera", chase);
            EditorUtil.Set(game, "_camera", cam);
            EditorUtil.Set(game, "_aimArrow", arrow);
            EditorUtil.Set(game, "_hintView", BuildTouchHint());

            var panel = new GameObject("DevTuningPanel").AddComponent<DevTuningPanel>();
            var panelSo = new SerializedObject(panel);
            var targets = panelSo.FindProperty("_targets");
            targets.arraySize = 4;
            targets.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<FlightConfig>(FlightConfigPath);
            targets.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            targets.GetArrayElementAtIndex(2).objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputConfig>(InputConfigPath);
            targets.GetArrayElementAtIndex(3).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Game.Wind.WindConfig>(WindLayout.ConfigPath);
            panelSo.FindProperty("_telemetrySource").objectReferenceValue = game;
            panelSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SceneGenerator.GamePath);
            Log.Info($"Saved {SceneGenerator.GamePath} (Paper Plane greybox room)");
        }

        /// <summary>Menu: the same room and wind, a decor plane looping overhead and a slowly circling camera.</summary>
        private static void BuildMenuScene()
        {
            var scene = SceneGenerator.NewScene();
            var cam = CreateCamera();
            CreateLight();
            BuildEnvironment();

            var flyer = new GameObject("MenuPlane", typeof(MeshFilter), typeof(MeshRenderer));
            flyer.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            var flyerRenderer = flyer.GetComponent<MeshRenderer>();
            flyerRenderer.sharedMaterial = Mat("Plane");
            flyerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flyer.transform.localScale = Vector3.one * 3f; // reads at menu distance
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            EditorUtil.Set(flyer.AddComponent<MenuFlyer>(), "_config", config);

            var backdrop = cam.gameObject.AddComponent<MenuBackdrop>();
            EditorUtil.Set(backdrop, "_config", config);
            EditorUtil.Set(backdrop, "_flyer", flyer.transform);

            // Both circle the living room (the launch room).
            var centre = RoomLayout.LivingRoom.center;
            var backdropSo = new SerializedObject(backdrop);
            backdropSo.FindProperty("_lookCentre").vector3Value = new Vector3(centre.x, 1.6f, centre.z);
            backdropSo.ApplyModifiedPropertiesWithoutUndo();
            var flyerSo = new SerializedObject(flyer.GetComponent<MenuFlyer>());
            flyerSo.FindProperty("_centre").vector3Value = new Vector3(centre.x, 3.2f, centre.z);
            flyerSo.FindProperty("_size").vector2Value = new Vector2(6f, 7f);
            flyerSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SceneGenerator.MenuPath);
            Log.Info($"Saved {SceneGenerator.MenuPath} (Paper Plane menu backdrop)");
        }

        private static Camera CreateCamera()
        {
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            return cam;
        }

        private static void CreateLight()
        {
            RenderSettings.ambientLight = new Color(0.72f, 0.68f, 0.64f);
            var lightGo = new GameObject("Directional Light", typeof(Light));
            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.86f);
            light.intensity = 0.9f;
            light.shadows = LightShadows.None; // SPEC: no realtime shadows (the plane gets a blob shadow)
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        }

        /// <summary>Rooms + hallway, furniture (Meshy models over greybox colliders) and wind. Returns the launch pad.</summary>
        private static GameObject BuildEnvironment()
        {
            var room = new GameObject("Room").transform;
            RoomLayout.BuildArchitecture(room, RoomTextures.Ensure(_regenerateTextures));
            _regenerateTextures = false; // once per build (Game and Menu share them)
            var pad = RoomLayout.BuildLaunchPad(room, Mat("LaunchPad"));
            RoomLayout.BuildFurniture(room, Mat("Prop_A"), Mat("Prop_B"), Mat("Prop_C"));
            PropModels.Apply(room);
            foreach (var t in room.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);

            var wind = WindLayout.Build(Mat("Prop_C")); // not static: the fan rotor spins
            PropModels.Apply(wind);
            WindLayout.FinishRotors(wind);
            return pad;
        }

        /// <summary>First-time ghost-finger hint over the lower screen (no raycasts: never blocks HUD buttons).</summary>
        private static TouchHintView BuildTouchHint()
        {
            var root = new GameObject("TouchHint", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // under the UIRoot (10)
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // Invisible area in the lower screen that the ghost finger moves in.
            var area = new GameObject("HintArea", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(root.transform, false);
            area.anchorMin = new Vector2(0.1f, 0.12f);
            area.anchorMax = new Vector2(0.9f, 0.3f);
            area.offsetMin = area.offsetMax = Vector2.zero;

            var finger = UiImage(area, "HintFinger", circle, new Color(1f, 1f, 1f, 0.95f), new Vector2(110f, 110f));
            var fingerGroup = finger.gameObject.AddComponent<CanvasGroup>();
            fingerGroup.blocksRaycasts = false;
            var halo = UiImage(finger.transform, "Halo", circle, new Color(1f, 1f, 1f, 0.3f), new Vector2(170f, 170f));
            halo.transform.SetAsFirstSibling();

            // Label on a soft dark pill under the swipe (keeps the plane on the shelf uncovered).
            var pill = UiImage(area, "HintLabel", rounded, new Color(0f, 0f, 0f, 0.28f), Vector2.zero);
            pill.type = Image.Type.Sliced;
            var pillRt = pill.rectTransform;
            pillRt.anchorMin = new Vector2(0.15f, 0f);
            pillRt.anchorMax = new Vector2(0.85f, 0f);
            pillRt.pivot = new Vector2(0.5f, 1f);
            pillRt.anchoredPosition = new Vector2(0f, -40f);
            pillRt.sizeDelta = new Vector2(0f, 96f);
            var labelGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(pill.transform, false);
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = 54f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = "Swipe up to throw";
            var labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;

            var view = root.AddComponent<TouchHintView>();
            EditorUtil.Set(view, "_area", area);
            EditorUtil.Set(view, "_finger", finger.rectTransform);
            EditorUtil.Set(view, "_fingerGroup", fingerGroup);
            EditorUtil.Set(view, "_label", label);
            EditorUtil.Set(view, "_labelRoot", pill.gameObject);
            return view;
        }

        private static Image UiImage(Transform parent, string name, Sprite sprite, Color color, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            return image;
        }
    }
}

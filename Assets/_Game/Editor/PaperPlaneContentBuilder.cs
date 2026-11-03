using Game.Flight;
using Game.Menu;
using Template.Editor;
using Template.Services.Input;
using Template.UI.DevTools;
using Template.UI.Screens;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// Generates the Paper Flight content: configs, materials, the paper-plane mesh + prefab, the Game scene
    /// (room, Meshy props, wind, chase camera, steering pad, PaperPlaneGame, dev tuning panel) and the Menu scene.
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

        // Room interior. SPEC suggested ≈ 20 × 14 × 5 m; play-testing found that too small, so it's a big loft.
        // The plane launches from a tall shelf at the -Z end toward +Z.
        private static readonly Vector3 RoomSize = new Vector3(22f, 7f, 34f);
        private const float WallThickness = 0.5f;
        private const float PadTop = 1.9f;
        private static readonly Vector3 PadCenter = new Vector3(0f, PadTop * 0.5f, -15.6f);
        private static readonly Vector3 PadSize = new Vector3(1.2f, PadTop, 0.6f);
        private static readonly Vector3 LaunchPosition = new Vector3(0f, PadTop + 0.08f, -15.45f);

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
        [GameContentBuilder]
        public static void Build(bool force)
        {
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
            launchPoint.SetPositionAndRotation(LaunchPosition, Quaternion.identity);

            var planePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlanePrefabPath);
            var planeGo = (GameObject)PrefabUtility.InstantiatePrefab(planePrefab);
            planeGo.transform.SetPositionAndRotation(LaunchPosition, Quaternion.identity);
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
            chaseSo.FindProperty("_room").boundsValue = new Bounds(new Vector3(0f, RoomSize.y * 0.5f, 0f), RoomSize);
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
            EditorUtil.Set(game, "_padView", BuildControlPad());

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

        /// <summary>Room, furniture (Meshy models over greybox colliders) and wind. Returns the launch pad.</summary>
        private static GameObject BuildEnvironment()
        {
            var room = new GameObject("Room").transform;
            BuildRoom(room);
            var pad = Box(room, "LaunchPad", PadCenter, PadSize, Mat("LaunchPad"));
            BuildProps(room);
            PropModels.Apply(room);
            foreach (var t in room.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);

            var wind = WindLayout.Build(Mat("Prop_C")); // not static: the fan rotor spins
            PropModels.Apply(wind);
            WindLayout.FinishRotor(wind);
            return pad;
        }

        /// <summary>Screen-space steering pad: rounded strip + anchor ring + knob (no raycasts: never blocks HUD buttons).</summary>
        private static ControlPadView BuildControlPad()
        {
            var root = new GameObject("ControlPad", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // under the UIRoot (10)
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            var pad = UiImage(root.transform, "Pad", rounded, new Color(1f, 1f, 1f, 0.16f), Vector2.zero);
            pad.type = Image.Type.Sliced;
            var group = pad.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var hint = UiImage(pad.transform, "CenterLine", null, new Color(1f, 1f, 1f, 0.12f), new Vector2(4f, 0f));
            var hintRt = hint.rectTransform;
            hintRt.anchorMin = new Vector2(0.5f, 0.15f);
            hintRt.anchorMax = new Vector2(0.5f, 0.85f);
            hintRt.sizeDelta = new Vector2(4f, 0f);
            var ring = UiImage(pad.transform, "Ring", circle, new Color(1f, 1f, 1f, 0.35f), new Vector2(190f, 190f));
            var knob = UiImage(pad.transform, "Knob", circle, new Color(1f, 1f, 1f, 0.85f), new Vector2(96f, 96f));

            var view = root.AddComponent<ControlPadView>();
            EditorUtil.Set(view, "_canvas", canvas);
            EditorUtil.Set(view, "_pad", pad.rectTransform);
            EditorUtil.Set(view, "_ring", ring.rectTransform);
            EditorUtil.Set(view, "_knob", knob.rectTransform);
            EditorUtil.Set(view, "_group", group);
            canvas.enabled = false; // shown while flying
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

        private static void BuildRoom(Transform parent)
        {
            var half = RoomSize * 0.5f;
            var t = WallThickness;
            var floor = Mat("Room_Floor");
            var wall = Mat("Room_Wall");
            Box(parent, "Floor", new Vector3(0f, -t * 0.5f, 0f), new Vector3(RoomSize.x + 2 * t, t, RoomSize.z + 2 * t), floor);
            Box(parent, "Ceiling", new Vector3(0f, RoomSize.y + t * 0.5f, 0f), new Vector3(RoomSize.x + 2 * t, t, RoomSize.z + 2 * t), Mat("Room_Ceiling"));
            Box(parent, "Wall_Left", new Vector3(-half.x - t * 0.5f, half.y, 0f), new Vector3(t, RoomSize.y, RoomSize.z), wall);
            Box(parent, "Wall_Right", new Vector3(half.x + t * 0.5f, half.y, 0f), new Vector3(t, RoomSize.y, RoomSize.z), wall);
            Box(parent, "Wall_Back", new Vector3(0f, half.y, -half.z - t * 0.5f), new Vector3(RoomSize.x, RoomSize.y, t), wall);
            Box(parent, "Wall_Front", new Vector3(0f, half.y, half.z + t * 0.5f), new Vector3(RoomSize.x, RoomSize.y, t), wall);
        }

        /// <summary>Greybox furniture: simple primitive colliders only (SPEC). Real layouts come on Day 2-3.</summary>
        private static void BuildProps(Transform parent)
        {
            var a = Mat("Prop_A");
            var b = Mat("Prop_B");
            var c = Mat("Prop_C");
            var props = new GameObject("Props").transform;
            props.SetParent(parent, false);

            // Living area (left, middle): sofa, armchair, coffee table, rug-height TV bench.
            Box(props, "Sofa_Seat", new Vector3(-8.5f, 0.25f, -1f), new Vector3(1f, 0.5f, 2.6f), a);
            Box(props, "Sofa_Back", new Vector3(-9.15f, 0.6f, -1f), new Vector3(0.3f, 1.2f, 2.6f), a);
            Box(props, "Armchair", new Vector3(-6f, 0.45f, -4f), new Vector3(1f, 0.9f, 1f), a);
            Box(props, "CoffeeTable", new Vector3(-6.4f, 0.22f, -1f), new Vector3(0.8f, 0.44f, 1.3f), b);
            Box(props, "TVBench", new Vector3(-3.2f, 0.3f, -1f), new Vector3(0.5f, 0.6f, 2.2f), b);
            Box(props, "TV", new Vector3(-3.2f, 1.05f, -1f), new Vector3(0.3f, 0.9f, 1.6f), a);
            // Right wall: two bookshelves.
            Box(props, "Bookshelf_1", new Vector3(10.6f, 1.1f, -6f), new Vector3(0.7f, 2.2f, 1.8f), b);
            Box(props, "Bookshelf_2", new Vector3(10.6f, 1.1f, -3.8f), new Vector3(0.7f, 2.2f, 1.8f), b);
            // Dining area (right, far half): table + chairs.
            Box(props, "DiningTable", new Vector3(5f, 0.38f, 7f), new Vector3(1.8f, 0.76f, 1f), b);
            Box(props, "Chair_1", new Vector3(4.5f, 0.45f, 7.9f), new Vector3(0.45f, 0.9f, 0.45f), a);
            Box(props, "Chair_2", new Vector3(5.5f, 0.45f, 6.1f), new Vector3(0.45f, 0.9f, 0.45f), a);
            // Work corner near the launch end: desk.
            Box(props, "Desk", new Vector3(7.5f, 0.38f, -13.5f), new Vector3(1.6f, 0.76f, 0.8f), b);
            // Floor lamps, plants, a box stack, a tall cabinet in the far corner.
            Primitive(props, PrimitiveType.Cylinder, "Lamp_Pole", new Vector3(-9.5f, 0.8f, -9f), new Vector3(0.06f, 0.8f, 0.06f), c);
            Primitive(props, PrimitiveType.Cylinder, "Lamp_Shade", new Vector3(-9.5f, 1.7f, -9f), new Vector3(0.45f, 0.15f, 0.45f), b);
            Primitive(props, PrimitiveType.Cylinder, "Lamp2_Pole", new Vector3(1.5f, 0.8f, 2f), new Vector3(0.06f, 0.8f, 0.06f), c);
            Primitive(props, PrimitiveType.Cylinder, "Lamp2_Shade", new Vector3(1.5f, 1.7f, 2f), new Vector3(0.45f, 0.15f, 0.45f), b);
            Primitive(props, PrimitiveType.Cylinder, "Plant_Pot", new Vector3(9.8f, 0.25f, 14.5f), new Vector3(0.5f, 0.25f, 0.5f), b);
            Primitive(props, PrimitiveType.Sphere, "Plant_Leaves", new Vector3(9.8f, 0.95f, 14.5f), new Vector3(1f, 1.1f, 1f), c);
            Primitive(props, PrimitiveType.Cylinder, "Plant2_Pot", new Vector3(-2f, 0.3f, 9f), new Vector3(0.6f, 0.3f, 0.6f), b);
            Primitive(props, PrimitiveType.Sphere, "Plant2_Leaves", new Vector3(-2f, 1.2f, 9f), new Vector3(1.2f, 1.6f, 1.2f), c);
            Box(props, "Box_1", new Vector3(-9.5f, 0.3f, 15f), new Vector3(0.6f, 0.6f, 0.6f), b);
            Box(props, "Box_2", new Vector3(-8.85f, 0.25f, 15.1f), new Vector3(0.5f, 0.5f, 0.5f), a);
            Box(props, "Box_3", new Vector3(-9.3f, 0.8f, 15f), new Vector3(0.45f, 0.4f, 0.45f), c);
            Box(props, "Cabinet", new Vector3(10.6f, 1.25f, 10f), new Vector3(0.6f, 2.5f, 1.1f), a);
        }

        private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat) =>
            Primitive(parent, PrimitiveType.Cube, name, center, size, mat);

        private static GameObject Primitive(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}

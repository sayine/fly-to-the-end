using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Template.Editor
{
    /// <summary>
    /// Applies the mobile Player/URP/Quality settings described in CLAUDE.md.
    /// Idempotent: safe to run on every new game clone.
    /// Batchmode: -executeMethod Template.Editor.ProjectSetup.ApplyAll
    /// </summary>
    public static class ProjectSetup
    {
        public const string DefaultCompany = "YourCompany";
        public const string DefaultProduct = "HC Template";
        public const string MinIOSVersion = "15.0";
        public const string DefaultBundleId = "com.sayine.hctemplate"; // template test app in AdMob
        private const string MobileRpAssetPath = "Assets/Settings/Mobile_RPAsset.asset";
        private const string TmpEssentialsFolder = "Assets/TextMesh Pro";

        [MenuItem("Tools/Template/Setup/Apply All Project Settings", priority = 0)]
        public static void ApplyAll()
        {
            ApplyPlayerSettings();
            ApplyRenderSettings();
            ImportTmpEssentials();
            ConfigureNativePlugins();
            ConfigureAdMob();
            AssetDatabase.SaveAssets();
            Log.Info("Project settings applied.");
        }

        [MenuItem("Tools/Template/Setup/Apply Player Settings", priority = 1)]
        public static void ApplyPlayerSettings()
        {
            if (PlayerSettings.companyName == "DefaultCompany") PlayerSettings.companyName = DefaultCompany;
            if (PlayerSettings.productName == "hc_template") PlayerSettings.productName = DefaultProduct;

            var ios = NamedBuildTarget.iOS;
            var id = PlayerSettings.GetApplicationIdentifier(ios);
            if (string.IsNullOrEmpty(id) || id.StartsWith("com.DefaultCompany") || id.StartsWith("com.Unity-Technologies") || id == "com.yourcompany.hctemplate")
                PlayerSettings.SetApplicationIdentifier(ios, DefaultBundleId);
            PlayerSettings.SetScriptingBackend(ios, ScriptingImplementation.IL2CPP); // iOS is IL2CPP + ARM64 only
            PlayerSettings.SetManagedStrippingLevel(ios, ManagedStrippingLevel.Medium);
            PlayerSettings.stripEngineCode = true;

            // Portrait lock.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // iOS: current Xcode builds only for iOS 15+ (PodfilePatcher aligns the CocoaPods targets).
            if (new System.Version(PlayerSettings.iOS.targetOSVersionString) < new System.Version(MinIOSVersion))
                PlayerSettings.iOS.targetOSVersionString = MinIOSVersion;

            SetActiveInputHandlerNewOnly();
            Log.Info($"Player settings: {PlayerSettings.companyName} / {PlayerSettings.productName}, portrait, iOS IL2CPP, stripping Medium.");
        }

        /// <summary>0 = Input Manager (old), 1 = Input System (new), 2 = Both. No public API exists.</summary>
        private static void SetActiveInputHandlerNewOnly()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) { Log.Warn("Could not load ProjectSettings.asset"); return; }
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null) { Log.Warn("activeInputHandler property not found; set it manually in Player > Other Settings."); return; }
            if (prop.intValue != 1)
            {
                prop.intValue = 1;
                so.ApplyModifiedPropertiesWithoutUndo();
                Log.Info("Active Input Handling set to Input System Package (New). Editor restart required.");
            }
        }

        [MenuItem("Tools/Template/Setup/Apply Render Settings", priority = 2)]
        public static void ApplyRenderSettings()
        {
            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(MobileRpAssetPath);
            if (rp == null) { Log.Error($"Missing {MobileRpAssetPath}"); return; }

            var so = new SerializedObject(rp);
            SetInt(so, "m_SupportsHDR", 0);
            SetInt(so, "m_MSAA", 2);
            SetInt(so, "m_UseSRPBatcher", 1);
            SetInt(so, "m_RequireDepthTexture", 0);
            SetInt(so, "m_RequireOpaqueTexture", 0);
            SetInt(so, "m_MainLightShadowsSupported", 1);
            SetInt(so, "m_MainLightShadowmapResolution", 1024);
            SetInt(so, "m_ShadowCascadeCount", 1);
            SetInt(so, "m_SoftShadowsSupported", 0);
            SetInt(so, "m_AdditionalLightShadowsSupported", 0);
            SetInt(so, "m_AdditionalLightsPerObjectLimit", 2);
            var dist = so.FindProperty("m_ShadowDistance");
            if (dist != null) dist.floatValue = 25f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Mobile pipeline everywhere (also in Editor play mode, so perf/look matches device).
            GraphicsSettings.defaultRenderPipeline = rp;
            UseMobileQualityEverywhere(rp);
            Log.Info("URP mobile asset: SRP Batcher on, HDR off, MSAA 2x, 1 cascade hard shadows @25m.");
        }

        private static void UseMobileQualityEverywhere(RenderPipelineAsset rp)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var levels = so.FindProperty("m_QualitySettings");
            var mobileIndex = -1;
            for (var i = 0; i < levels.arraySize; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                var pipeline = level.FindPropertyRelative("customRenderPipeline");
                if (pipeline != null && pipeline.objectReferenceValue == rp) { mobileIndex = i; break; }
            }
            if (mobileIndex < 0) { Log.Warn("No quality level uses the mobile RP asset."); return; }

            so.FindProperty("m_CurrentQuality").intValue = mobileIndex;
            var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            for (var i = 0; i < perPlatform.arraySize; i++)
                perPlatform.GetArrayElementAtIndex(i).FindPropertyRelative("second").intValue = mobileIndex;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Tools/Template/Setup/Import TMP Essentials", priority = 3)]
        public static void ImportTmpEssentials()
        {
            if (AssetDatabase.IsValidFolder(TmpEssentialsFolder)) return;
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui");
            if (info == null) { Log.Error("com.unity.ugui not found."); return; }
            var pkg = Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            if (!File.Exists(pkg)) { Log.Error($"TMP essentials not found at {pkg}"); return; }
            AssetDatabase.ImportPackage(pkg, false);
            Log.Info("Imported TMP Essential Resources.");
        }

        [MenuItem("Tools/Template/Setup/Create Secrets From Example", priority = 4)]
        public static void CreateSecretsFromExample()
        {
            const string example = "Assets/_Game/Configs/Secrets.example.asset";
            const string target = "Assets/_Game/Configs/Resources/Secrets.asset";
            if (AssetDatabase.LoadMainAssetAtPath(target) != null) { Log.Info($"{target} already exists."); return; }
            if (AssetDatabase.LoadMainAssetAtPath(example) == null) { Log.Error($"{example} missing. Run Tools/Template/Generate Base Scenes."); return; }
            EditorUtil.EnsureFolder("Assets/_Game/Configs/Resources");
            AssetDatabase.CopyAsset(example, target);
            Log.Info($"Created {target} (git-ignored). Fill in the AdMob ad unit IDs.");
        }

        /// <summary>Native sources under _Template/Plugins/iOS compile for iOS only.</summary>
        public static void ConfigureNativePlugins()
        {
            foreach (var guid in AssetDatabase.FindAssets("", new[] { "Assets/_Template/Plugins/iOS" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is PluginImporter importer)) continue;
                importer.SetCompatibleWithAnyPlatform(false);
                importer.SetCompatibleWithEditor(false);
                importer.SetCompatibleWithPlatform(BuildTarget.iOS, true);
                if (path.EndsWith("HCTracking.mm"))
                    importer.SetPlatformData(BuildTarget.iOS, "FrameworkDependencies", "AppTrackingTransparency;");
                importer.SaveAndReimport();
            }
        }

        // ---------- AdMob ----------
        public const string AdMobSampleAppId = "ca-app-pub-3940256099942544~1458002511"; // Google's iOS sample app
        private const string AdMobSettingsPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
        private const string AdMobDefine = "HC_ADMOB";
        private const string DefaultTrackingText =
            "Your data will be used to provide you a better and personalized ad experience.";

        /// <summary>
        /// Ensures Google Mobile Ads settings exist (App ID defaults to Google's sample ID until the game's own
        /// is entered), sets the ATT prompt text, and enables HC_ADMOB for iOS. Never overwrites a real App ID.
        /// </summary>
        [MenuItem("Tools/Template/Setup/Configure AdMob", priority = 5)]
        public static void ConfigureAdMob()
        {
            var settings = AssetDatabase.LoadMainAssetAtPath(AdMobSettingsPath);
            if (settings == null)
            {
                var type = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                    .FirstOrDefault(t => t.FullName == "GoogleMobileAds.Editor.GoogleMobileAdsSettings");
                if (type == null) { Log.Error("Google Mobile Ads package not installed."); return; }
                EditorUtil.EnsureFolder(Path.GetDirectoryName(AdMobSettingsPath));
                settings = ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(settings, AdMobSettingsPath);
            }

            var so = new SerializedObject(settings);
            var appId = so.FindProperty("adMobIOSAppId");
            var tracking = so.FindProperty("userTrackingUsageDescription");
            if (appId == null || tracking == null) { Log.Error("GoogleMobileAdsSettings fields changed (plugin update?)."); return; }
            if (string.IsNullOrEmpty(appId.stringValue)) appId.stringValue = AdMobSampleAppId;
            if (string.IsNullOrEmpty(tracking.stringValue)) tracking.stringValue = DefaultTrackingText;
            so.ApplyModifiedPropertiesWithoutUndo();

            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS);
            if (!defines.Split(';').Contains(AdMobDefine))
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.iOS, string.IsNullOrEmpty(defines) ? AdMobDefine : defines + ";" + AdMobDefine);

            AssetDatabase.SaveAssets();
            Log.Info($"AdMob: iOS App ID {(appId.stringValue == AdMobSampleAppId ? "= Google SAMPLE (replace per game)" : appId.stringValue)}, {AdMobDefine} enabled for iOS.");
        }

        public static string AdMobIOSAppId()
        {
            var settings = AssetDatabase.LoadMainAssetAtPath(AdMobSettingsPath);
            return settings == null ? null : new SerializedObject(settings).FindProperty("adMobIOSAppId")?.stringValue;
        }

        private static void SetInt(SerializedObject so, string name, int value)
        {
            var p = so.FindProperty(name);
            if (p == null) { Log.Warn($"URP property {name} not found (URP version changed?)"); return; }
            if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value != 0;
            else p.intValue = value;
        }
    }

    public static class Log
    {
        public static void Info(string msg) => Debug.Log("[Template] " + msg);
        public static void Warn(string msg) => Debug.LogWarning("[Template] WARN " + msg);
        public static void Error(string msg) => Debug.LogError("[Template] ERROR " + msg);
    }
}

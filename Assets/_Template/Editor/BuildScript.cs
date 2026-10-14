using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Template.Editor
{
    /// <summary>
    /// Builds the iOS Xcode project from the Editor menu or CI:
    ///   Unity -batchmode -quit -projectPath . -buildTarget iOS -executeMethod Template.Editor.BuildScript.BuildIOS
    ///   ... BuildIOSDev (Development build, script debugging, profiler-friendly)
    /// Optional args: -buildVersion 1.2.0  -buildNumber 42  -outputPath Builds/iOS
    /// Signing/archiving happens in Xcode (or xcodebuild/fastlane in CI). In batchmode a failed build exits with code 1.
    /// </summary>
    public static class BuildScript
    {
        private const string BuildsRoot = "Builds";

        [MenuItem("Tools/Template/Build iOS", priority = 40)]
        public static void BuildIOS() => IOS(development: false);

        [MenuItem("Tools/Template/Build iOS (dev)", priority = 41)]
        public static void BuildIOSDev() => IOS(development: true);

        [MenuItem("Tools/Template/Bump Build Number", priority = 60)]
        public static void BumpBuildNumber()
        {
            var next = ParseInt(PlayerSettings.iOS.buildNumber) + 1;
            PlayerSettings.iOS.buildNumber = next.ToString();
            AssetDatabase.SaveAssets();
            Log.Info($"Build number -> {next} (version {PlayerSettings.bundleVersion})");
        }

        private static void IOS(bool development)
        {
            if (!Prepare(development)) return;
            var output = Arg("-outputPath") ?? Path.Combine(BuildsRoot, development ? "iOS-dev" : "iOS");
            Run(new BuildPlayerOptions
            {
                scenes = Scenes(),
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                locationPathName = output,
                options = development ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None,
            });
        }

        private static bool Prepare(bool development)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                Fail($"iOS Build Support module is not installed. Unity Hub > Installs > {Application.unityVersion} > Add modules.");
                return false;
            }

            var version = Arg("-buildVersion");
            if (!string.IsNullOrEmpty(version)) PlayerSettings.bundleVersion = version;
            if (int.TryParse(Arg("-buildNumber"), out var n)) PlayerSettings.iOS.buildNumber = n.ToString();

            var scenes = Scenes();
            if (scenes.Length == 0 || !scenes[0].EndsWith("/Boot.unity"))
            {
                Fail("Boot.unity must be the first enabled scene. Run Tools/Template/Generate Base Scenes.");
                return false;
            }

            WarnAboutReleaseRisks(development);

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            return true;
        }

        private static void WarnAboutReleaseRisks(bool development)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.iOS);
            if (!defines.Contains("HC_ADMOB"))
                Log.Warn("HC_ADMOB not defined: this build uses MOCK ads (no revenue). Run Tools/Template/Setup/Configure AdMob.");
            else if (!development) // dev builds always use Google test ad units
            {
                if (Resources.Load("Secrets") == null)
                    Log.Warn("Release build without Assets/_Game/Configs/Resources/Secrets.asset: no ad units, ads disabled.");
                if (ProjectSetup.AdMobIOSAppId() == ProjectSetup.AdMobSampleAppId)
                    Log.Warn("AdMob App ID is Google's SAMPLE ID: set this game's App ID (Assets > Google Mobile Ads > Settings).");
            }
            if (PlayerSettings.companyName == ProjectSetup.DefaultCompany)
                Log.Warn("Company name is still the template placeholder.");
            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS) == ProjectSetup.DefaultBundleId)
                Log.Warn("Bundle identifier is still the template placeholder.");
        }

        private static void Run(BuildPlayerOptions options)
        {
            var dir = Path.GetDirectoryName(options.locationPathName);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            Log.Info($"Building {options.target} -> {options.locationPathName} (v{PlayerSettings.bundleVersion} ({PlayerSettings.iOS.buildNumber}))");
            var summary = BuildPipeline.BuildPlayer(options).summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Log.Info($"Build succeeded: {summary.outputPath} ({summary.totalTime.TotalSeconds:0}s)");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                Fail($"Build {summary.result}: {summary.totalErrors} error(s). See the Editor log.");
            }
        }

        private static string[] Scenes() =>
            EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        private static void Fail(string message)
        {
            Log.Error(message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else EditorUtility.DisplayDialog("Build failed", message, "OK");
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static int ParseInt(string s) => int.TryParse(s, out var v) ? v : 0;
    }
}

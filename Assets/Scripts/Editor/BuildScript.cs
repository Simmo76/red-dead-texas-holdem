using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CinematicPoker.Editor
{
    /// <summary>
    /// Scene creation and player builds for the Phase 2 prototype.
    ///
    /// The prototype scene is EMPTY on disk: everything is generated at runtime
    /// by PrototypeBootstrap, so no serialized scene references can break.
    ///
    /// Android APK:
    ///   - In the editor: Cinematic Poker → Build → Android APK
    ///   - Headless/CI:   unity -batchmode -quit -executeMethod
    ///                    CinematicPoker.Editor.BuildScript.BuildAndroid
    ///     (output path override via BUILD_OUTPUT env var)
    /// </summary>
    public static class BuildScript
    {
        private const string ScenePath = "Assets/Scenes/PokerPrototype.unity";
        private const string DefaultApkPath = "Builds/Android/CinematicPoker.apk";

        [MenuItem("Cinematic Poker/Create Prototype Scene")]
        public static void CreatePrototypeScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"Prototype scene saved to {ScenePath} and added to build settings.");
        }

        [MenuItem("Cinematic Poker/Build/Android APK")]
        public static void BuildAndroidMenu() => BuildAndroid();

        public static void BuildAndroid()
        {
            EnsureScene();
            ApplySharedPlayerSettings();

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            EditorUserBuildSettings.buildAppBundle = false; // APK, not AAB

            string output = Environment.GetEnvironmentVariable("BUILD_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = DefaultApkPath;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));

            Build(BuildTarget.Android, output);
        }

        /// <summary>Desktop build for quick playtesting without a device.</summary>
        [MenuItem("Cinematic Poker/Build/Windows (playtest)")]
        public static void BuildWindows()
        {
            EnsureScene();
            ApplySharedPlayerSettings();
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/CinematicPoker.exe");
        }

        private static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
                CreatePrototypeScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void ApplySharedPlayerSettings()
        {
            PlayerSettings.productName = "Cinematic Poker";
            PlayerSettings.companyName = "CinematicPoker";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.cinematicpoker.prototype");

            // Landscape only, per the design.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        }

        private static void Build(BuildTarget target, string output)
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = target,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Build succeeded: {output} ({summary.totalSize / (1024 * 1024)} MB)");
            }
            else
            {
                Debug.LogError($"Build failed: {summary.result}, {summary.totalErrors} errors.");
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
            }
        }
    }
}

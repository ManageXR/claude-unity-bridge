using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ClaudeBridge.TestProject {
    /// <summary>
    /// Test build entry points for validating the Claude Unity Bridge build command.
    /// Provides both a setup method (to create a test scene) and a custom build method
    /// (to test method invocation via reflection).
    /// </summary>
    public static class TestBuildPipeline {
        private const string TAG = nameof(TestBuildPipeline);
        private const string TestScenePath = "Assets/Scenes/TestScene.unity";

        /// <summary>
        /// Creates a minimal test scene and adds it to EditorBuildSettings.
        /// Call this before testing direct builds.
        /// Usage: unity-bridge build --method ClaudeBridge.TestProject.TestBuildPipeline.SetupTestScene
        /// </summary>
        public static void SetupTestScene() {
            Debug.Log($"[{TAG}] Setting up test scene...");

            // Create Scenes directory
            var scenesDir = Path.Combine(Application.dataPath, "Scenes");
            if (!Directory.Exists(scenesDir)) {
                Directory.CreateDirectory(scenesDir);
            }

            // Create a new empty scene with default setup (camera + light)
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, TestScenePath);

            // Add to EditorBuildSettings
            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene(TestScenePath, true)
            };

            Debug.Log($"[{TAG}] Test scene created at {TestScenePath} and added to build settings.");
        }

        /// <summary>
        /// Custom build method that demonstrates method invocation via the bridge.
        /// Builds a StandaloneOSX player using the test scene.
        /// Usage: unity-bridge build --method ClaudeBridge.TestProject.TestBuildPipeline.BuildStandalone
        /// </summary>
        public static void BuildStandalone() {
            Debug.Log($"[{TAG}] Starting custom standalone build...");

            // Check for UNITY_BRIDGE_BUILD env var (set by bridge)
            var isBridgeBuild = Environment.GetEnvironmentVariable("UNITY_BRIDGE_BUILD") == "true";
            Debug.Log($"[{TAG}] Bridge build context: {isBridgeBuild}");

            // Read optional env vars passed by the bridge
            var buildType = Environment.GetEnvironmentVariable("BUILD_TYPE") ?? "development";
            var outputDir = Environment.GetEnvironmentVariable("BUILD_OUTPUT") ?? "Builds";

            var outputPath = Path.Combine(Application.dataPath, "..", outputDir, "TestBuild.app");
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            // Get scenes from build settings
            var scenes = new string[EditorBuildSettings.scenes.Length];
            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++) {
                scenes[i] = EditorBuildSettings.scenes[i].path;
            }

            if (scenes.Length == 0) {
                Debug.LogError($"[{TAG}] No scenes in build settings. Run SetupTestScene first.");
                if (!isBridgeBuild) EditorApplication.Exit(1);
                return;
            }

            var options = new BuildPlayerOptions {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneOSX,
                options = buildType == "development" ? BuildOptions.Development : BuildOptions.None
            };

            Debug.Log($"[{TAG}] Building to: {outputPath} (type: {buildType})");
            var report = BuildPipeline.BuildPlayer(options);

            Debug.Log($"[{TAG}] Build {report.summary.result}: " +
                      $"{report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings, " +
                      $"{report.summary.totalTime.TotalSeconds:F1}s");

            // Only call Exit if NOT a bridge build (bridge handles lifecycle)
            if (!isBridgeBuild) {
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
        }
    }
}

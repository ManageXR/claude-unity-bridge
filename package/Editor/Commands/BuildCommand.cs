using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using MXR.ClaudeBridge.Models;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MXR.ClaudeBridge.Commands {
    public class BuildCommand : ICommand {

        public void Execute(
            CommandRequest request,
            Action<CommandResponse> onProgress,
            Action<CommandResponse> onComplete
        ) {
            var stopwatch = Stopwatch.StartNew();
            var method = request.@params?.method;

            Debug.Log($"{ClaudeBridge.LogPrefix} Build command received - method: {method ?? "direct"}");

            // Report running status
            var progressResponse = CommandResponse.Running(request.id, request.action);
            onProgress?.Invoke(progressResponse);

            try {
                if (!string.IsNullOrEmpty(method)) {
                    ExecuteMethodBuild(request, stopwatch, onComplete);
                } else {
                    ExecuteDirectBuild(request, stopwatch, onComplete);
                }
            }
            catch (Exception e) {
                stopwatch.Stop();
                Debug.LogError($"{ClaudeBridge.LogPrefix} Build error: {e.Message}");
                onComplete?.Invoke(
                    CommandResponse.Error(request.id, request.action, e.Message)
                );
            }
        }

        private void ExecuteDirectBuild(
            CommandRequest request,
            Stopwatch stopwatch,
            Action<CommandResponse> onComplete
        ) {
            var targetStr = request.@params?.target;
            var isDev = string.Equals(request.@params?.development, "true", StringComparison.OrdinalIgnoreCase);
            var outputOverride = request.@params?.output;

            // Parse build target (default to active target)
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            if (!string.IsNullOrEmpty(targetStr)) {
                if (!Enum.TryParse(targetStr, true, out BuildTarget parsed)) {
                    onComplete?.Invoke(CommandResponse.Error(
                        request.id, request.action,
                        $"Invalid build target: '{targetStr}'. Use Unity BuildTarget enum names (e.g., Android, StandaloneWindows64, iOS)."
                    ));
                    return;
                }
                target = parsed;
            }

            // Determine output path
            var outputPath = outputOverride;
            if (string.IsNullOrEmpty(outputPath)) {
                var buildDir = Path.Combine(Application.dataPath, "..", "Builds");
                Directory.CreateDirectory(buildDir);
                var ext = GetBuildExtension(target);
                outputPath = Path.Combine(buildDir, $"Build_{target}{ext}");
            }

            // Gather enabled scenes
            var scenes = GetEnabledScenePaths();
            if (scenes.Length == 0) {
                onComplete?.Invoke(CommandResponse.Error(
                    request.id, request.action,
                    "No scenes enabled in Build Settings. Add scenes via File > Build Settings."
                ));
                return;
            }

            // Build options
            var options = new BuildPlayerOptions {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = isDev ? BuildOptions.Development : BuildOptions.None
            };

            Debug.Log($"{ClaudeBridge.LogPrefix} Starting direct build: target={target}, dev={isDev}, output={outputPath}");

            BuildReport report = BuildPipeline.BuildPlayer(options);
            stopwatch.Stop();

            var buildInfo = new BuildInfo {
                buildResult = report.summary.result.ToString(),
                totalErrors = report.summary.totalErrors,
                totalWarnings = report.summary.totalWarnings,
                totalSeconds = (float)report.summary.totalTime.TotalSeconds,
                outputPath = outputPath,
                sizeBytes = (long)report.summary.totalSize,
                method = "direct"
            };

            CommandResponse response;
            if (report.summary.result == BuildResult.Succeeded) {
                Debug.Log($"{ClaudeBridge.LogPrefix} Build succeeded: {report.summary.totalSize} bytes in {buildInfo.totalSeconds:F1}s");
                response = CommandResponse.Success(request.id, request.action, stopwatch.ElapsedMilliseconds);
            } else {
                Debug.LogError($"{ClaudeBridge.LogPrefix} Build failed: {report.summary.result}");
                response = CommandResponse.Failure(
                    request.id, request.action, stopwatch.ElapsedMilliseconds,
                    $"Build {report.summary.result}: {report.summary.totalErrors} error(s), {report.summary.totalWarnings} warning(s)"
                );
            }

            response.buildInfo = buildInfo;
            onComplete?.Invoke(response);
        }

        private void ExecuteMethodBuild(
            CommandRequest request,
            Stopwatch stopwatch,
            Action<CommandResponse> onComplete
        ) {
            var methodPath = request.@params?.method;

            // Parse method path: "Namespace.Class.Method"
            var lastDot = methodPath.LastIndexOf('.');
            if (lastDot <= 0) {
                onComplete?.Invoke(CommandResponse.Error(
                    request.id, request.action,
                    $"Invalid method format: '{methodPath}'. Expected 'Namespace.Class.Method' (e.g., 'MXR.Builder.BuildEntryPoints.BuildQuest')."
                ));
                return;
            }

            var typeName = methodPath.Substring(0, lastDot);
            var methodName = methodPath.Substring(lastDot + 1);

            // Find type across all loaded assemblies
            Type targetType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                targetType = assembly.GetType(typeName);
                if (targetType != null) break;
            }

            if (targetType == null) {
                onComplete?.Invoke(CommandResponse.Error(
                    request.id, request.action,
                    $"Type not found: '{typeName}'. Ensure the class exists and is in a loaded assembly."
                ));
                return;
            }

            var targetMethod = targetType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (targetMethod == null) {
                onComplete?.Invoke(CommandResponse.Error(
                    request.id, request.action,
                    $"Static method not found: '{methodName}' on type '{typeName}'."
                ));
                return;
            }

            // Set environment variables
            var envVars = new Dictionary<string, string>();
            var envString = request.@params?.env;

            if (!string.IsNullOrEmpty(envString)) {
                foreach (var pair in envString.Split(';')) {
                    var trimmed = pair.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    var eqIdx = trimmed.IndexOf('=');
                    if (eqIdx <= 0) continue;
                    var key = trimmed.Substring(0, eqIdx);
                    var val = trimmed.Substring(eqIdx + 1);
                    Environment.SetEnvironmentVariable(key, val);
                    envVars[key] = val;
                }
            }

            Debug.Log($"{ClaudeBridge.LogPrefix} Invoking build method: {methodPath}");

            try {
                targetMethod.Invoke(null, null);
                stopwatch.Stop();

                var buildInfo = new BuildInfo {
                    buildResult = "Succeeded",
                    totalSeconds = stopwatch.ElapsedMilliseconds / 1000f,
                    method = methodPath
                };

                Debug.Log($"{ClaudeBridge.LogPrefix} Method build completed in {buildInfo.totalSeconds:F1}s");
                var response = CommandResponse.Success(request.id, request.action, stopwatch.ElapsedMilliseconds);
                response.buildInfo = buildInfo;
                onComplete?.Invoke(response);
            }
            catch (TargetInvocationException tie) {
                stopwatch.Stop();
                var inner = tie.InnerException ?? tie;
                Debug.LogError($"{ClaudeBridge.LogPrefix} Method build threw exception: {inner.Message}");

                var response = CommandResponse.Failure(
                    request.id, request.action, stopwatch.ElapsedMilliseconds,
                    inner.Message
                );
                response.buildInfo = new BuildInfo {
                    buildResult = "Failed",
                    totalSeconds = stopwatch.ElapsedMilliseconds / 1000f,
                    method = methodPath
                };
                onComplete?.Invoke(response);
            }
            finally {
                // Cleanup: restore env vars
                foreach (var kv in envVars) {
                    Environment.SetEnvironmentVariable(kv.Key, null);
                }
            }
        }

        private static string[] GetEnabledScenePaths() {
            var scenes = EditorBuildSettings.scenes;
            var paths = new List<string>();
            foreach (var scene in scenes) {
                if (scene.enabled) {
                    paths.Add(scene.path);
                }
            }
            return paths.ToArray();
        }

        private static string GetBuildExtension(BuildTarget target) {
            switch (target) {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return ".exe";
                case BuildTarget.StandaloneOSX:
                    return ".app";
                case BuildTarget.Android:
                    return ".apk";
                case BuildTarget.iOS:
                    return "";
                case BuildTarget.WebGL:
                    return "";
                default:
                    return "";
            }
        }
    }
}

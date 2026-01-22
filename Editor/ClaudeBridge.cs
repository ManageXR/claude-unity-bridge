using System;
using System.Collections.Generic;
using System.IO;
using MXR.ClaudeBridge.Commands;
using MXR.ClaudeBridge.Models;
using UnityEditor;
using UnityEngine;

namespace MXR.ClaudeBridge {
    [InitializeOnLoad]
    public static class ClaudeBridge {
        private static readonly string CommandDir;
        private static readonly string CommandFilePath;
        private static readonly Dictionary<string, ICommand> Commands;
        private static string _currentCommandId;
        private static bool _isProcessingCommand;

        static ClaudeBridge() {
            CommandDir = Path.Combine(Application.dataPath, "..", ".claude", "unity");
            CommandFilePath = Path.Combine(CommandDir, "command.json");

            Commands = new Dictionary<string, ICommand> {
                { "run-tests", new RunTestsCommand() },
                { "compile", new CompileCommand() },
                { "refresh", new RefreshCommand() },
                { "get-status", new GetStatusCommand() },
                { "get-console-logs", new GetConsoleLogsCommand() }
            };

            EnsureDirectoryExists();
            EditorApplication.update += PollForCommands;

            Debug.Log($"[ClaudeBridge] Initialized - Watching: {CommandDir}");
        }

        private static void EnsureDirectoryExists() {
            if (!Directory.Exists(CommandDir)) {
                Directory.CreateDirectory(CommandDir);
            }
        }

        private static void PollForCommands() {
            if (_isProcessingCommand) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!File.Exists(CommandFilePath)) return;

            ProcessCommand();
        }

        private static void ProcessCommand() {
            CommandRequest request = null;

            try {
                var json = File.ReadAllText(CommandFilePath);
                request = JsonUtility.FromJson<CommandRequest>(json);

                if (request == null || string.IsNullOrEmpty(request.id)) {
                    Debug.LogError("[ClaudeBridge] Invalid command file - missing id");
                    DeleteCommandFile();
                    return;
                }

                // Delete the command file immediately to prevent re-processing
                DeleteCommandFile();

                _currentCommandId = request.id;
                _isProcessingCommand = true;

                Debug.Log($"[ClaudeBridge] Processing command: {request.action} (id: {request.id})");

                if (!Commands.TryGetValue(request.action, out var command)) {
                    Debug.LogError($"[ClaudeBridge] Unknown action: {request.action}");
                    WriteResponse(CommandResponse.Error(request.id, request.action, $"Unknown action: {request.action}"));
                    _isProcessingCommand = false;
                    return;
                }

                command.Execute(request, OnProgress, OnComplete);
            }
            catch (Exception e) {
                Debug.LogError($"[ClaudeBridge] Error processing command: {e.Message}");
                DeleteCommandFile();

                if (request != null) {
                    WriteResponse(CommandResponse.Error(request.id, request.action, e.Message));
                }

                _isProcessingCommand = false;
            }
        }

        private static void OnProgress(CommandResponse response) {
            WriteResponse(response);
        }

        private static void OnComplete(CommandResponse response) {
            WriteResponse(response);
            _isProcessingCommand = false;
            _currentCommandId = null;
        }

        private static void WriteResponse(CommandResponse response) {
            try {
                EnsureDirectoryExists();
                var responsePath = Path.Combine(CommandDir, $"response-{response.id}.json");
                var json = JsonUtility.ToJson(response, true);
                File.WriteAllText(responsePath, json);
            }
            catch (Exception e) {
                Debug.LogError($"[ClaudeBridge] Error writing response: {e.Message}");
            }
        }

        private static void DeleteCommandFile() {
            try {
                if (File.Exists(CommandFilePath)) {
                    File.Delete(CommandFilePath);
                }
            }
            catch (Exception e) {
                Debug.LogError($"[ClaudeBridge] Error deleting command file: {e.Message}");
            }
        }

        // Cleanup utility - can be called from menu
        [MenuItem("Tools/Claude Bridge/Cleanup Old Responses")]
        private static void CleanupOldResponses() {
            if (!Directory.Exists(CommandDir)) return;

            var cutoff = DateTime.Now.AddHours(-1);
            var files = Directory.GetFiles(CommandDir, "response-*.json");

            foreach (var file in files) {
                var info = new FileInfo(file);
                if (info.LastWriteTime < cutoff) {
                    try {
                        File.Delete(file);
                        Debug.Log($"[ClaudeBridge] Cleaned up: {Path.GetFileName(file)}");
                    }
                    catch {
                        // Ignore errors during cleanup
                    }
                }
            }
        }

        [MenuItem("Tools/Claude Bridge/Show Status")]
        private static void ShowStatus() {
            Debug.Log($"[ClaudeBridge] Status:");
            Debug.Log($"  Command directory: {CommandDir}");
            Debug.Log($"  Is processing: {_isProcessingCommand}");
            Debug.Log($"  Current command ID: {_currentCommandId ?? "none"}");
            Debug.Log($"  Command file exists: {File.Exists(CommandFilePath)}");

            if (Directory.Exists(CommandDir)) {
                var responseFiles = Directory.GetFiles(CommandDir, "response-*.json");
                Debug.Log($"  Response files: {responseFiles.Length}");
            }
        }
    }
}

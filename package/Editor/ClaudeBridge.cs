using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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
        private static DateTime _commandStartTime;

        // Auto-reset stuck commands after this timeout (5 minutes)
        private const int COMMAND_TIMEOUT_SECONDS = 300;

        // Security: Only allow alphanumeric characters and hyphens in response IDs
        private static readonly Regex ValidIdPattern = new Regex(@"^[a-fA-F0-9\-]+$", RegexOptions.Compiled);

        // Read-only commands that are safe to execute during compilation/updating.
        // These only read EditorApplication state or console logs — they never mutate anything.
        private static readonly HashSet<string> SafeDuringCompileCommands = new HashSet<string> {
            "get-status",
            "get-console-logs"
        };

        static ClaudeBridge() {
            CommandDir = Path.Combine(Application.dataPath, "..", ".unity-bridge");
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

#if DEBUG
            Debug.Log($"[ClaudeBridge] Initialized - Watching: {CommandDir}");
#endif
        }

        private static void EnsureDirectoryExists() {
            if (!Directory.Exists(CommandDir)) {
                Directory.CreateDirectory(CommandDir);
#if UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
                try {
                    var chmod = new System.Diagnostics.Process();
                    chmod.StartInfo.FileName = "chmod";
                    chmod.StartInfo.Arguments = $"700 \"{CommandDir}\"";
                    chmod.StartInfo.UseShellExecute = false;
                    chmod.StartInfo.CreateNoWindow = true;
                    chmod.Start();
                    chmod.WaitForExit(1000);
                } catch (System.Exception) { }
#endif
            }
        }

        private static void PollForCommands() {
            if (!File.Exists(CommandFilePath)) return;

            bool isBusyEditor = EditorApplication.isCompiling || EditorApplication.isUpdating;

            // Auto-recover from stuck processing state
            if (_isProcessingCommand) {
                if ((DateTime.Now - _commandStartTime).TotalSeconds > COMMAND_TIMEOUT_SECONDS) {
                    Debug.LogWarning($"[ClaudeBridge] Command {_currentCommandId} timed out after {COMMAND_TIMEOUT_SECONDS}s, resetting state");
                    ResetProcessingState();
                } else {
                    // Bridge is busy - respond with error so client doesn't hang
                    TryRespondBusy();
                    return;
                }
            }

            // When compiling/updating, only allow safe read-only commands through.
            // Other commands get a busy response so the CLI doesn't hang.
            if (isBusyEditor) {
                if (!TryProcessSafeCommand()) {
                    return;
                }
            } else {
                ProcessCommand();
            }
        }

        private static void TryRespondBusy() {
            try {
                var json = File.ReadAllText(CommandFilePath);
                var request = JsonUtility.FromJson<CommandRequest>(json);

                if (request != null && !string.IsNullOrEmpty(request.id)) {
                    Debug.LogWarning($"[ClaudeBridge] Rejecting command {request.id} - bridge is busy processing {_currentCommandId}");
                    WriteResponse(CommandResponse.Error(request.id, request.action ?? "unknown",
                        $"Bridge is busy processing another command ({_currentCommandId}). Try again later."));
                }

                DeleteCommandFile();
            }
            catch (Exception e) {
                Debug.LogError($"[ClaudeBridge] Error handling busy response: {e.Message}");
                DeleteCommandFile();
            }
        }

        /// <summary>
        /// Peeks at the command file to check if it's a safe read-only command.
        /// If safe, processes it normally. If not, responds with a busy/compiling error.
        /// Returns true if the command was handled (either processed or rejected).
        /// </summary>
        private static bool TryProcessSafeCommand() {
            try {
                var json = File.ReadAllText(CommandFilePath);
                var request = JsonUtility.FromJson<CommandRequest>(json);

                if (request == null || string.IsNullOrEmpty(request.id) || string.IsNullOrEmpty(request.action)) {
                    // Malformed command — let ProcessCommand handle the error path
                    ProcessCommand();
                    return true;
                }

                if (SafeDuringCompileCommands.Contains(request.action)) {
                    // Safe command — process it normally
                    ProcessCommand();
                    return true;
                }

                // Mutating command during compilation — reject with informative error
                DeleteCommandFile();
                var state = EditorApplication.isCompiling ? "compiling" : "updating";
                Debug.LogWarning($"[ClaudeBridge] Rejecting command {request.action} ({request.id}) - Unity is {state}");
                WriteResponse(CommandResponse.Error(request.id, request.action,
                    $"Unity Editor is currently {state}. Only read-only commands (get-status, get-console-logs) are available. Try again later."));
                return true;
            }
            catch (Exception e) {
                Debug.LogError($"[ClaudeBridge] Error peeking at command during compile: {e.Message}");
                DeleteCommandFile();
                return true;
            }
        }

        private static void ResetProcessingState() {
            _isProcessingCommand = false;
            _currentCommandId = null;
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
                _commandStartTime = DateTime.Now;

#if DEBUG
                Debug.Log($"[ClaudeBridge] Processing command: {request.action} (id: {request.id})");
#endif

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
                } else {
                    // Generate fallback response with valid hex-only GUID so it passes ValidIdPattern
                    var fallbackId = Guid.NewGuid().ToString();
                    WriteResponse(CommandResponse.Error(fallbackId, "unknown", $"Failed to parse command: {e.Message}"));
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
                // Security: Validate response ID to prevent path traversal attacks
                if (string.IsNullOrEmpty(response.id) || !ValidIdPattern.IsMatch(response.id)) {
                    Debug.LogError($"[ClaudeBridge] Invalid response ID format: {response.id}");
                    return;
                }

                EnsureDirectoryExists();
                var responsePath = Path.Combine(CommandDir, $"response-{response.id}.json");
                var tempPath = responsePath + ".tmp";
                var json = JsonUtility.ToJson(response, true);

                // Atomic write: write to temp file then rename
                File.WriteAllText(tempPath, json);
#if UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
                try {
                    var chmod = new System.Diagnostics.Process();
                    chmod.StartInfo.FileName = "chmod";
                    chmod.StartInfo.Arguments = $"600 \"{tempPath}\"";
                    chmod.StartInfo.UseShellExecute = false;
                    chmod.StartInfo.CreateNoWindow = true;
                    chmod.Start();
                    chmod.WaitForExit(1000);
                } catch (System.Exception) { }
#endif
                if (File.Exists(responsePath)) {
                    File.Delete(responsePath);
                }
                File.Move(tempPath, responsePath);
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

        [MenuItem("Tools/Claude Bridge/Reset Processing State")]
        private static void ResetProcessingStateMenu() {
            if (_isProcessingCommand) {
                Debug.Log($"[ClaudeBridge] Manually resetting processing state (was processing: {_currentCommandId})");
                ResetProcessingState();
            } else {
                Debug.Log("[ClaudeBridge] No command is currently being processed");
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

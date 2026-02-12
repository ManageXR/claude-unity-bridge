using System;
using System.Diagnostics;
using MXR.ClaudeBridge.Models;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MXR.ClaudeBridge.Commands {
    public class StepCommand : ICommand {
        public void Execute(CommandRequest request, Action<CommandResponse> onProgress, Action<CommandResponse> onComplete) {
            var stopwatch = Stopwatch.StartNew();

            if (!EditorApplication.isPlaying) {
                stopwatch.Stop();
                onComplete?.Invoke(CommandResponse.Error(request.id, request.action,
                    "Cannot step: Unity Editor is not in Play Mode. Use 'play' to enter Play Mode first."));
                return;
            }

            try {
                EditorApplication.Step();
                stopwatch.Stop();

#if DEBUG
                Debug.Log("[ClaudeBridge] Editor stepped one frame");
#endif

                var response = CommandResponse.Success(request.id, request.action, stopwatch.ElapsedMilliseconds);
                response.editorStatus = new EditorStatus {
                    isCompiling = EditorApplication.isCompiling,
                    isUpdating = EditorApplication.isUpdating,
                    isPlaying = EditorApplication.isPlaying,
                    isPaused = EditorApplication.isPaused
                };
                onComplete?.Invoke(response);
            }
            catch (Exception e) {
                stopwatch.Stop();
                Debug.LogError($"[ClaudeBridge] Step failed: {e.Message}");
                onComplete?.Invoke(CommandResponse.Error(request.id, request.action, e.Message));
            }
        }
    }
}

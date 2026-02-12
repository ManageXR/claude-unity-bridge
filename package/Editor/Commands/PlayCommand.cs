using System;
using System.Diagnostics;
using MXR.ClaudeBridge.Models;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MXR.ClaudeBridge.Commands {
    public class PlayCommand : ICommand {
        public void Execute(CommandRequest request, Action<CommandResponse> onProgress, Action<CommandResponse> onComplete) {
            var stopwatch = Stopwatch.StartNew();

            try {
                EditorApplication.isPlaying = !EditorApplication.isPlaying;
                stopwatch.Stop();

#if DEBUG
                Debug.Log($"[ClaudeBridge] Play mode toggled: isPlaying={EditorApplication.isPlaying}");
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
                Debug.LogError($"[ClaudeBridge] Play mode toggle failed: {e.Message}");
                onComplete?.Invoke(CommandResponse.Error(request.id, request.action, e.Message));
            }
        }
    }
}

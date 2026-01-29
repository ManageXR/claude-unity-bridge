using System;
using System.Diagnostics;
using MXR.ClaudeBridge.Models;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MXR.ClaudeBridge.Commands {
    public class GetStatusCommand : ICommand {
        public void Execute(CommandRequest request, Action<CommandResponse> onProgress, Action<CommandResponse> onComplete) {
            var stopwatch = Stopwatch.StartNew();
#if DEBUG
            Debug.Log("[ClaudeBridge] Getting editor status");
#endif

            // Use the proper EditorStatus model instead of overloading the error field
            var editorStatus = new EditorStatus {
                isCompiling = EditorApplication.isCompiling,
                isUpdating = EditorApplication.isUpdating,
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused
            };

            stopwatch.Stop();
            var response = CommandResponse.Success(request.id, request.action, stopwatch.ElapsedMilliseconds);
            response.editorStatus = editorStatus;

            onComplete?.Invoke(response);
        }
    }
}

using System;
using MXR.ClaudeBridge.Models;
using UnityEditor;
using UnityEngine;

namespace MXR.ClaudeBridge.Commands {
    public class GetStatusCommand : ICommand {
        public void Execute(CommandRequest request, Action<CommandResponse> onProgress, Action<CommandResponse> onComplete) {
            Debug.Log("[ClaudeBridge] Getting editor status");

            var response = CommandResponse.Success(request.id, request.action, 0);

            // Use the proper EditorStatus model instead of overloading the error field
            response.editorStatus = new EditorStatus {
                isCompiling = EditorApplication.isCompiling,
                isUpdating = EditorApplication.isUpdating,
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused
            };

            // Keep backwards compatibility: also set error field with JSON for older Python clients
            response.error = JsonUtility.ToJson(response.editorStatus);

            onComplete?.Invoke(response);
        }
    }
}

using System;
using MXR.ClaudeBridge.Models;
using UnityEditor;
using UnityEngine;

namespace MXR.ClaudeBridge.Commands {
    public class GetStatusCommand : ICommand {
        public void Execute(CommandRequest request, Action<CommandResponse> onProgress, Action<CommandResponse> onComplete) {
            Debug.Log("[ClaudeBridge] Getting editor status");

            var response = CommandResponse.Success(request.id, request.action, 0);
            response.result = new TestResult(); // Reusing for simplicity, could add EditorStatus model

            // Add status info as error field (quick way to return arbitrary data)
            var status = new EditorStatus {
                isCompiling = EditorApplication.isCompiling,
                isUpdating = EditorApplication.isUpdating,
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused
            };

            response.error = JsonUtility.ToJson(status);

            onComplete?.Invoke(response);
        }

        [Serializable]
        private class EditorStatus {
            public bool isCompiling;
            public bool isUpdating;
            public bool isPlaying;
            public bool isPaused;
        }
    }
}

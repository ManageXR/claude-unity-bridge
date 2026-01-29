using System;
using System.Diagnostics;
using MXR.ClaudeBridge.Models;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MXR.ClaudeBridge.Commands {
    public class RefreshCommand : ICommand {
        public void Execute(CommandRequest request, Action<CommandResponse> onProgress, Action<CommandResponse> onComplete) {
            var stopwatch = Stopwatch.StartNew();

#if DEBUG
            Debug.Log("[ClaudeBridge] Refreshing asset database");
#endif

            var response = CommandResponse.Running(request.id, request.action);
            onProgress?.Invoke(response);

            try {
                AssetDatabase.Refresh();
                stopwatch.Stop();

#if DEBUG
                Debug.Log("[ClaudeBridge] Asset database refresh completed");
#endif
                onComplete?.Invoke(CommandResponse.Success(request.id, request.action, stopwatch.ElapsedMilliseconds));
            }
            catch (Exception e) {
                stopwatch.Stop();
                Debug.LogError($"[ClaudeBridge] Asset database refresh failed: {e.Message}");
                onComplete?.Invoke(CommandResponse.Error(request.id, request.action, e.Message));
            }
        }
    }
}

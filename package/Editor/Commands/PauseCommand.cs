using System;
using System.Diagnostics;
using MXR.ClaudeBridge.Models;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MXR.ClaudeBridge.Commands {
    public class PauseCommand : ICommand {
        private readonly IEditorPlayMode _editor;

        public PauseCommand(IEditorPlayMode editor) {
            _editor = editor;
        }

        public void Execute(CommandRequest request, Action<CommandResponse> onProgress, Action<CommandResponse> onComplete) {
            var stopwatch = Stopwatch.StartNew();

            if (!_editor.IsPlaying) {
                stopwatch.Stop();
                onComplete?.Invoke(CommandResponse.Error(request.id, request.action,
                    "Cannot pause: Unity Editor is not in Play Mode. Use 'play' to enter Play Mode first."));
                return;
            }

            try {
                _editor.IsPaused = !_editor.IsPaused;
                stopwatch.Stop();

#if DEBUG
                Debug.Log($"[ClaudeBridge] Pause toggled: isPaused={_editor.IsPaused}");
#endif

                var response = CommandResponse.Success(request.id, request.action, stopwatch.ElapsedMilliseconds);
                response.editorStatus = new EditorStatus {
                    isCompiling = _editor.IsCompiling,
                    isUpdating = _editor.IsUpdating,
                    isPlaying = _editor.IsPlaying,
                    isPaused = _editor.IsPaused
                };
                onComplete?.Invoke(response);
            }
            catch (Exception e) {
                stopwatch.Stop();
                Debug.LogError($"[ClaudeBridge] Pause toggle failed: {e.Message}");
                onComplete?.Invoke(CommandResponse.Error(request.id, request.action, e.Message));
            }
        }
    }
}

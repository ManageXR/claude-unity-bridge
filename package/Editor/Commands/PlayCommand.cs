using System;
using System.Diagnostics;
using MXR.ClaudeBridge.Models;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MXR.ClaudeBridge.Commands {
    public class PlayCommand : ICommand {
        private readonly IEditorPlayMode _editor;

        public PlayCommand(IEditorPlayMode editor) {
            _editor = editor;
        }

        public void Execute(CommandRequest request, Action<CommandResponse> onProgress, Action<CommandResponse> onComplete) {
            var stopwatch = Stopwatch.StartNew();

            try {
                bool willPlay = !_editor.IsPlaying;
                _editor.IsPlaying = willPlay;
                stopwatch.Stop();

#if DEBUG
                Debug.Log($"[ClaudeBridge] Play mode toggled: isPlaying={willPlay}");
#endif

                var response = CommandResponse.Success(request.id, request.action, stopwatch.ElapsedMilliseconds);
                response.editorStatus = new EditorStatus {
                    isCompiling = _editor.IsCompiling,
                    isUpdating = _editor.IsUpdating,
                    isPlaying = willPlay,
                    isPaused = willPlay ? _editor.IsPaused : false
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

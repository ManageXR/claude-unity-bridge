using System;
using System.Diagnostics;
using MXR.ClaudeBridge.Models;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace MXR.ClaudeBridge.Commands {
    public class CompileCommand : ICommand {
        private string _commandId;
        private Stopwatch _stopwatch;
        private Action<CommandResponse> _onProgress;
        private Action<CommandResponse> _onComplete;
        private bool _hasErrors;

        public void Execute(CommandRequest request, Action<CommandResponse> onProgress, Action<CommandResponse> onComplete) {
            _commandId = request.id;
            _stopwatch = Stopwatch.StartNew();
            _onProgress = onProgress;
            _onComplete = onComplete;
            _hasErrors = false;

#if DEBUG
            UnityEngine.Debug.Log("[ClaudeBridge] Starting script compilation");
#endif

            var response = CommandResponse.Running(_commandId, request.action);
            onProgress?.Invoke(response);

            CompilationPipeline.compilationFinished += OnCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;

            CompilationPipeline.RequestScriptCompilation();
        }

        private void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages) {
            foreach (var message in messages) {
                if (message.type == CompilerMessageType.Error) {
                    _hasErrors = true;
                    UnityEngine.Debug.LogError($"[ClaudeBridge] Compilation error: {message.message}");
                }
            }
        }

        private void OnCompilationFinished(object context) {
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;

            _stopwatch.Stop();
#if DEBUG
            UnityEngine.Debug.Log($"[ClaudeBridge] Compilation finished - HasErrors: {_hasErrors}");
#endif

            var response = _hasErrors
                ? CommandResponse.Failure(_commandId, "compile", _stopwatch.ElapsedMilliseconds, "Compilation errors occurred")
                : CommandResponse.Success(_commandId, "compile", _stopwatch.ElapsedMilliseconds);

            _onComplete?.Invoke(response);
        }
    }
}

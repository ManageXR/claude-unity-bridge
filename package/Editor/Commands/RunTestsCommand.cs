using System;
using System.Collections.Generic;
using System.Diagnostics;
using MXR.ClaudeBridge.Models;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MXR.ClaudeBridge.Commands {
    public class RunTestsCommand : ICommand {
        public void Execute(CommandRequest request, Action<CommandResponse> onProgress, Action<CommandResponse> onComplete) {
            var stopwatch = Stopwatch.StartNew();
            var callbacks = new TestCallbacks(request.id, stopwatch, onProgress, onComplete);

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(callbacks);
            callbacks.SetApi(api);

            var testMode = GetTestMode(request.@params?.testMode);
            var filter = new Filter {
                testMode = testMode
            };

            if (!string.IsNullOrEmpty(request.@params?.filter)) {
                filter.testNames = request.@params.filter.Split(';');
            }

#if DEBUG
            Debug.Log($"[ClaudeBridge] Running tests - Mode: {testMode}, Filter: {request.@params?.filter ?? "none"}");
#endif

            var response = CommandResponse.Running(request.id, request.action);
            onProgress?.Invoke(response);

            api.Execute(new ExecutionSettings(filter));
        }

        private TestMode GetTestMode(string mode) {
            if (string.IsNullOrEmpty(mode)) {
                return TestMode.EditMode | TestMode.PlayMode;
            }

            return mode.ToLower() switch {
                "editmode" => TestMode.EditMode,
                "playmode" => TestMode.PlayMode,
                _ => TestMode.EditMode | TestMode.PlayMode
            };
        }

        private class TestCallbacks : ICallbacks {
            private readonly string _commandId;
            private readonly Stopwatch _stopwatch;
            private readonly Action<CommandResponse> _onProgress;
            private readonly Action<CommandResponse> _onComplete;
            private readonly List<TestFailure> _failures = new List<TestFailure>();
            private TestRunnerApi _api;
            private int _passed;
            private int _failed;
            private int _skipped;
            private int _total;
            private int _current;

            public TestCallbacks(string commandId, Stopwatch stopwatch,
                Action<CommandResponse> onProgress, Action<CommandResponse> onComplete) {
                _commandId = commandId;
                _stopwatch = stopwatch;
                _onProgress = onProgress;
                _onComplete = onComplete;
            }

            public void SetApi(TestRunnerApi api) {
                _api = api;
            }

            private void CleanupApi() {
                if (_api != null) {
                    _api.UnregisterCallbacks(this);
                    UnityEngine.Object.DestroyImmediate(_api);
                    _api = null;
                }
            }

            public void RunStarted(ITestAdaptor testsToRun) {
                _total = CountTests(testsToRun);
#if DEBUG
                Debug.Log($"[ClaudeBridge] Test run started - {_total} tests");
#endif

                var response = CommandResponse.Running(_commandId, "run-tests");
                response.progress = new ProgressInfo {
                    current = 0,
                    total = _total
                };
                _onProgress?.Invoke(response);
            }

            public void RunFinished(ITestResultAdaptor result) {
                _stopwatch.Stop();
#if DEBUG
                Debug.Log($"[ClaudeBridge] Test run finished - Passed: {_passed}, Failed: {_failed}, Skipped: {_skipped}");
#endif

                var response = _failed > 0
                    ? CommandResponse.Failure(_commandId, "run-tests", _stopwatch.ElapsedMilliseconds)
                    : CommandResponse.Success(_commandId, "run-tests", _stopwatch.ElapsedMilliseconds);

                response.result = new TestResult {
                    passed = _passed,
                    failed = _failed,
                    skipped = _skipped,
                    failures = _failures
                };

                _onComplete?.Invoke(response);

                // Clean up the TestRunnerApi instance to prevent memory leak
                CleanupApi();
            }

            public void TestStarted(ITestAdaptor test) {
                if (!test.IsSuite) {
                    var response = CommandResponse.Running(_commandId, "run-tests");
                    response.progress = new ProgressInfo {
                        current = _current,
                        total = _total,
                        currentTest = test.Name
                    };
                    _onProgress?.Invoke(response);
                }
            }

            public void TestFinished(ITestResultAdaptor result) {
                if (result.Test.IsSuite) return;

                _current++;

                switch (result.TestStatus) {
                    case TestStatus.Passed:
                        _passed++;
                        break;
                    case TestStatus.Failed:
                        _failed++;
                        _failures.Add(new TestFailure {
                            name = result.Test.FullName,
                            message = result.Message
                        });
                        break;
                    case TestStatus.Skipped:
                        _skipped++;
                        break;
                }

                var response = CommandResponse.Running(_commandId, "run-tests");
                response.progress = new ProgressInfo {
                    current = _current,
                    total = _total
                };
                response.failures = new List<TestFailure>(_failures);
                _onProgress?.Invoke(response);
            }

            private int CountTests(ITestAdaptor test) {
                if (!test.IsSuite) return 1;

                int count = 0;
                foreach (var child in test.Children) {
                    count += CountTests(child);
                }
                return count;
            }
        }
    }
}

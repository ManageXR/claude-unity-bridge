using MXR.ClaudeBridge.Commands;
using MXR.ClaudeBridge.Models;
using NUnit.Framework;
using UnityEditor.Compilation;
using UnityEngine.TestTools;
using System.Reflection;

namespace MXR.ClaudeBridge.Tests.Commands {
    /// <summary>
    /// Tests for CompileCommand.
    /// Focus: Tests REAL async behavior (event registration, state tracking, callback timing)
    /// NOT testing: CompilationPipeline internals, actual compilation
    ///
    /// Testing approach:
    /// - Execute() starts async operation, registers events
    /// - Manually trigger event handlers to simulate compilation finishing
    /// - Verify command logic responds correctly to events
    /// - Test both success and error paths
    /// </summary>
    [TestFixture]
    public class CompileCommandTests : CommandTestFixture {
        private CompileCommand _command;

        [SetUp]
        public override void SetUp() {
            base.SetUp();
            _command = new CompileCommand();
            Request.action = "compile";
        }

        [Test]
        public void Execute_CallsOnProgressWithRunningStatus() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.HasProgressResponses, Is.True, "Should call onProgress immediately");
            Assert.That(Responses.ProgressResponses.Count, Is.EqualTo(1), "Should call onProgress exactly once");
            Assert.That(Responses.ProgressResponses[0].status, Is.EqualTo("running"), "Progress status should be 'running'");
        }

        [Test]
        public void Execute_DoesNotCallOnCompleteImmediately() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert - Async command should NOT complete immediately
            Assert.That(Responses.HasCompleteResponse, Is.False, "Should NOT call onComplete immediately (async operation)");
        }

        [Test]
        public void OnCompilationFinished_WithoutErrors_CallsOnCompleteWithSuccess() {
            // Arrange - Start async operation
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Act - Simulate compilation finishing without errors (no OnAssemblyCompilationFinished calls)
            SimulateCompilationFinished(_command);

            // Assert
            Assert.That(Responses.HasCompleteResponse, Is.True, "Should call onComplete after compilation");
            Assert.That(Responses.CompleteResponse.status, Is.EqualTo("success"), "Should return success status");
        }

        [Test]
        public void OnCompilationFinished_WithErrors_CallsOnCompleteWithFailure() {
            // Arrange - Start async operation
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Expect error log from CompileCommand
            LogAssert.Expect(UnityEngine.LogType.Error, "[ClaudeBridge] Compilation error: Test compilation error");

            // Simulate compilation errors
            var errorMessages = new CompilerMessage[] {
                new CompilerMessage {
                    type = CompilerMessageType.Error,
                    message = "Test compilation error"
                }
            };
            SimulateAssemblyCompilationFinished(_command, "TestAssembly.dll", errorMessages);

            // Act - Simulate compilation finishing
            SimulateCompilationFinished(_command);

            // Assert
            Assert.That(Responses.HasCompleteResponse, Is.True, "Should call onComplete");
            Assert.That(Responses.CompleteResponse.status, Is.EqualTo("failure"), "Should return failure status");
            Assert.That(Responses.CompleteResponse.error, Is.Not.Null, "Should include error message");
            Assert.That(Responses.CompleteResponse.error, Does.Contain("Compilation errors"), "Error should mention compilation errors");
        }

        [Test]
        public void OnCompilationFinished_IncludesDuration() {
            // Arrange
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Act
            SimulateCompilationFinished(_command);

            // Assert - Duration should be measured (will be small but >= 0)
            Assert.That(Responses.CompleteResponse.duration_ms, Is.GreaterThanOrEqualTo(0),
                "Should measure and include duration");
        }

        [Test]
        public void OnAssemblyCompilationFinished_WithWarning_DoesNotFlagError() {
            // Arrange
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Simulate compilation with only warnings (not errors)
            var warningMessages = new CompilerMessage[] {
                new CompilerMessage {
                    type = CompilerMessageType.Warning,
                    message = "Test warning"
                }
            };
            SimulateAssemblyCompilationFinished(_command, "TestAssembly.dll", warningMessages);

            // Act
            SimulateCompilationFinished(_command);

            // Assert - Warnings should not cause failure
            Assert.That(Responses.CompleteResponse.status, Is.EqualTo("success"), "Warnings should not cause failure");
        }

        [Test]
        public void OnAssemblyCompilationFinished_WithMultipleErrors_StillFails() {
            // Arrange
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Expect error logs from CompileCommand (one per error)
            LogAssert.Expect(UnityEngine.LogType.Error, "[ClaudeBridge] Compilation error: Error 1");
            LogAssert.Expect(UnityEngine.LogType.Error, "[ClaudeBridge] Compilation error: Error 2");

            // Simulate multiple assemblies with errors
            var errorMessages1 = new CompilerMessage[] {
                new CompilerMessage { type = CompilerMessageType.Error, message = "Error 1" }
            };
            var errorMessages2 = new CompilerMessage[] {
                new CompilerMessage { type = CompilerMessageType.Error, message = "Error 2" }
            };

            SimulateAssemblyCompilationFinished(_command, "Assembly1.dll", errorMessages1);
            SimulateAssemblyCompilationFinished(_command, "Assembly2.dll", errorMessages2);

            // Act
            SimulateCompilationFinished(_command);

            // Assert
            Assert.That(Responses.CompleteResponse.status, Is.EqualTo("failure"), "Multiple errors should cause failure");
        }

        [Test]
        public void OnCompilationFinished_IncludesCorrectIdAndAction() {
            // Arrange
            var uniqueId = "compile-unique-" + System.Guid.NewGuid().ToString();
            Request.id = uniqueId;
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Act
            SimulateCompilationFinished(_command);

            // Assert
            Assert.That(Responses.CompleteResponse.id, Is.EqualTo(uniqueId), "Should echo request ID");
            Assert.That(Responses.CompleteResponse.action, Is.EqualTo("compile"), "Should include correct action");
        }

        [Test]
        public void Execute_MultipleCallbacks_OnlyLastOneInvoked() {
            // Arrange - First execution
            var firstResponses = new ResponseCapture();
            _command.Execute(Request, firstResponses.OnProgress, firstResponses.OnComplete);

            // Act - Second execution (overwrites callbacks)
            var secondResponses = new ResponseCapture();
            _command.Execute(Request, secondResponses.OnProgress, secondResponses.OnComplete);
            SimulateCompilationFinished(_command);

            // Assert - Only second callback should be invoked
            Assert.That(firstResponses.HasCompleteResponse, Is.False, "First callback should NOT be invoked");
            Assert.That(secondResponses.HasCompleteResponse, Is.True, "Second callback SHOULD be invoked");
        }

        /// <summary>
        /// Helper method to simulate CompilationPipeline.assemblyCompilationFinished event.
        /// Uses reflection to call the private OnAssemblyCompilationFinished method.
        /// </summary>
        private void SimulateAssemblyCompilationFinished(CompileCommand command, string assemblyPath, CompilerMessage[] messages) {
            var method = typeof(CompileCommand).GetMethod("OnAssemblyCompilationFinished",
                BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(command, new object[] { assemblyPath, messages });
        }

        /// <summary>
        /// Helper method to simulate CompilationPipeline.compilationFinished event.
        /// Uses reflection to call the private OnCompilationFinished method.
        /// </summary>
        private void SimulateCompilationFinished(CompileCommand command) {
            var method = typeof(CompileCommand).GetMethod("OnCompilationFinished",
                BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(command, new object[] { null });
        }
    }
}

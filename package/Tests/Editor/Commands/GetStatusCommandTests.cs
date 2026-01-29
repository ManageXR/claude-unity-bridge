using MXR.ClaudeBridge.Commands;
using MXR.ClaudeBridge.Models;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MXR.ClaudeBridge.Tests.Commands {
    /// <summary>
    /// Tests for GetStatusCommand.
    /// Focus: Tests REAL behavior (response construction, editor state capture, JSON serialization)
    /// NOT testing: Unity API internals, mock return values
    /// </summary>
    [TestFixture]
    public class GetStatusCommandTests : CommandTestFixture {
        private GetStatusCommand _command;

        [SetUp]
        public override void SetUp() {
            base.SetUp();
            _command = new GetStatusCommand();
            Request.action = "get-status";
        }

        [Test]
        public void Execute_ConstructsResponseWithCorrectIdAndAction() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.CompleteResponse, Is.Not.Null, "Should call onComplete");
            Assert.That(Responses.CompleteResponse.id, Is.EqualTo(Request.id), "Response ID should match request ID");
            Assert.That(Responses.CompleteResponse.action, Is.EqualTo(Request.action), "Response action should match request action");
        }

        [Test]
        public void Execute_IncludesEditorStatusInResponse() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.CompleteResponse, Is.Not.Null, "Should call onComplete");
            Assert.That(Responses.CompleteResponse.editorStatus, Is.Not.Null, "Should include editorStatus in response");
        }

        [Test]
        public void Execute_EditorStatusHasExpectedFields() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert - Test EditorStatus model has all fields
            var status = Responses.CompleteResponse.editorStatus;
            Assert.That(status, Is.Not.Null, "EditorStatus should not be null");

            // Serialize to JSON and verify field presence
            var statusJson = JsonUtility.ToJson(status);
            Assert.That(statusJson, Does.Contain("isCompiling"), "JSON should contain isCompiling field");
            Assert.That(statusJson, Does.Contain("isUpdating"), "JSON should contain isUpdating field");
            Assert.That(statusJson, Does.Contain("isPlaying"), "JSON should contain isPlaying field");
            Assert.That(statusJson, Does.Contain("isPaused"), "JSON should contain isPaused field");
        }

        [Test]
        public void Execute_CallsOnCompleteExactlyOnce() {
            // Arrange
            var callCount = 0;
            System.Action<CommandResponse> countingCallback = (response) => {
                callCount++;
            };

            // Act
            _command.Execute(Request, Responses.OnProgress, countingCallback);

            // Assert
            Assert.That(callCount, Is.EqualTo(1), "onComplete should be called exactly once");
        }

        [Test]
        public void Execute_DoesNotCallOnProgress() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.ProgressResponses.Count, Is.EqualTo(0), "Should not call onProgress for synchronous command");
        }

        [Test]
        public void Execute_ResponseHasDuration() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert - Duration is now measured (should be very small for this fast command)
            Assert.That(Responses.CompleteResponse.duration_ms, Is.GreaterThanOrEqualTo(0), "Duration should be a non-negative value");
        }

        [Test]
        public void Execute_ResponseStatusIsSuccess() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.CompleteResponse.status, Is.EqualTo("success"), "Should return success status");
        }

        [Test]
        public void Execute_EchoesRequestIdInResponse() {
            // Arrange
            var uniqueId = "test-unique-" + System.Guid.NewGuid().ToString();
            Request.id = uniqueId;

            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.CompleteResponse.id, Is.EqualTo(uniqueId), "Response should echo exact request ID");
        }

        [Test]
        public void Execute_IncludesEditorStatusModel() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert - Check the new editorStatus field (not just error field)
            Assert.That(Responses.CompleteResponse.editorStatus, Is.Not.Null,
                "Should include EditorStatus model in response");
        }

        [Test]
        public void Execute_EditorStatusHasAllFields() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert - Verify EditorStatus model has expected structure
            var status = Responses.CompleteResponse.editorStatus;
            Assert.That(status, Is.Not.Null, "EditorStatus should not be null");

            // Just verify it's a valid EditorStatus with boolean fields
            // (actual values depend on Unity Editor state at test time)
            Assert.DoesNotThrow(() => {
                var _ = status.isCompiling;
                var __ = status.isUpdating;
                var ___ = status.isPlaying;
                var ____ = status.isPaused;
            }, "EditorStatus should have all expected boolean fields");
        }

        [Test]
        public void Execute_DoesNotSetErrorField() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert - error field should be null/empty for success responses
            Assert.That(Responses.CompleteResponse.error, Is.Null.Or.Empty,
                "Success response should not have error field set");
        }
    }
}

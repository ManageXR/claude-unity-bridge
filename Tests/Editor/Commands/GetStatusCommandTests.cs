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
            Assert.That(Responses.CompleteResponse.error, Is.Not.Null, "Should include status data in error field");
            Assert.That(Responses.CompleteResponse.error, Is.Not.Empty, "Status data should not be empty");

            // Verify it's valid JSON that can be deserialized
            Assert.DoesNotThrow(() => {
                JsonUtility.FromJson<EditorStatusData>(Responses.CompleteResponse.error);
            }, "Error field should contain valid JSON");
        }

        [Test]
        public void Execute_SerializesStatusAsJsonInErrorField() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert - Test JSON serialization logic
            var statusJson = Responses.CompleteResponse.error;
            var status = JsonUtility.FromJson<EditorStatusData>(statusJson);

            // Verify all expected fields are present in serialized JSON
            Assert.That(statusJson, Does.Contain("isCompiling"), "JSON should contain isCompiling field");
            Assert.That(statusJson, Does.Contain("isUpdating"), "JSON should contain isUpdating field");
            Assert.That(statusJson, Does.Contain("isPlaying"), "JSON should contain isPlaying field");
            Assert.That(statusJson, Does.Contain("isPaused"), "JSON should contain isPaused field");

            // Verify deserialized object has expected properties
            Assert.That(status, Is.Not.Null, "Status should deserialize successfully");
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
        public void Execute_ResponseDurationIsZero() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.CompleteResponse.duration_ms, Is.EqualTo(0), "Synchronous command should report 0ms duration");
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

        /// <summary>
        /// Test data structure matching GetStatusCommand's internal EditorStatus class.
        /// Used for deserializing and validating JSON responses.
        /// </summary>
        [System.Serializable]
        private class EditorStatusData {
            public bool isCompiling;
            public bool isUpdating;
            public bool isPlaying;
            public bool isPaused;
        }
    }
}

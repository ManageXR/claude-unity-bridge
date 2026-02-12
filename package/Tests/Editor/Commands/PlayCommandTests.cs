using MXR.ClaudeBridge.Commands;
using MXR.ClaudeBridge.Models;
using NUnit.Framework;
using UnityEditor;

namespace MXR.ClaudeBridge.Tests.Commands {
    /// <summary>
    /// Tests for PlayCommand.
    /// Focus: Tests REAL behavior (response construction, editorStatus presence, duration tracking)
    /// NOT testing: Whether Unity actually enters/exits play mode
    /// </summary>
    [TestFixture]
    public class PlayCommandTests : CommandTestFixture {
        private PlayCommand _command;

        [SetUp]
        public override void SetUp() {
            base.SetUp();
            _command = new PlayCommand();
            Request.action = "play";
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
            Assert.That(Responses.CompleteResponse.editorStatus, Is.Not.Null, "Should include editorStatus in response");
        }

        [Test]
        public void Execute_EditorStatusHasAllFields() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            var status = Responses.CompleteResponse.editorStatus;
            Assert.That(status, Is.Not.Null, "EditorStatus should not be null");
            Assert.DoesNotThrow(() => {
                var _ = status.isCompiling;
                var __ = status.isUpdating;
                var ___ = status.isPlaying;
                var ____ = status.isPaused;
            }, "EditorStatus should have all expected boolean fields");
        }

        [Test]
        public void Execute_ResponseHasDuration() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.CompleteResponse.duration_ms, Is.GreaterThanOrEqualTo(0),
                "Duration should be a non-negative value");
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
    }
}

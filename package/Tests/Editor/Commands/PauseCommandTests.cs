using MXR.ClaudeBridge.Commands;
using MXR.ClaudeBridge.Models;
using NUnit.Framework;
using UnityEditor;

namespace MXR.ClaudeBridge.Tests.Commands {
    /// <summary>
    /// Tests for PauseCommand.
    /// Focus: Tests REAL behavior (error when not playing, response construction, editorStatus)
    /// NOT testing: Whether Unity actually pauses/unpauses
    /// </summary>
    [TestFixture]
    public class PauseCommandTests : CommandTestFixture {
        private PauseCommand _command;

        [SetUp]
        public override void SetUp() {
            base.SetUp();
            _command = new PauseCommand();
            Request.action = "pause";
        }

        [Test]
        public void Execute_WhenNotPlaying_ReturnsError() {
            // Arrange - Ensure we're NOT in play mode (tests run in edit mode)

            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.HasCompleteResponse, Is.True, "Should call onComplete");
            Assert.That(Responses.CompleteResponse.status, Is.EqualTo("error"), "Should return error when not in play mode");
            Assert.That(Responses.CompleteResponse.error, Does.Contain("not in Play Mode"),
                "Error message should explain the issue");
        }

        [Test]
        public void Execute_WhenNotPlaying_ErrorMentionsPlayCommand() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.CompleteResponse.error, Does.Contain("play"),
                "Error message should suggest using 'play' command");
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
            Assert.That(Responses.ProgressResponses.Count, Is.EqualTo(0), "Should not call onProgress");
        }

        [Test]
        public void Execute_ConstructsResponseWithCorrectIdAndAction() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.CompleteResponse.id, Is.EqualTo(Request.id), "Response ID should match request ID");
            Assert.That(Responses.CompleteResponse.action, Is.EqualTo(Request.action), "Response action should match request action");
        }
    }
}

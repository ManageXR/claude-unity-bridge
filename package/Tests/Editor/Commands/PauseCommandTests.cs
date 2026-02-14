using Moq;
using MXR.ClaudeBridge.Commands;
using MXR.ClaudeBridge.Models;
using NUnit.Framework;

namespace MXR.ClaudeBridge.Tests.Commands {
    /// <summary>
    /// Tests for PauseCommand using Moq to mock IEditorPlayMode.
    /// Focus: Precondition check (must be playing), toggle behavior, response construction.
    /// </summary>
    [TestFixture]
    public class PauseCommandTests : CommandTestFixture {
        private Mock<IEditorPlayMode> _mockEditor;
        private PauseCommand _command;

        [SetUp]
        public override void SetUp() {
            base.SetUp();
            _mockEditor = new Mock<IEditorPlayMode>();
            _mockEditor.SetupAllProperties();
            _mockEditor.SetupGet(m => m.IsCompiling).Returns(false);
            _mockEditor.SetupGet(m => m.IsUpdating).Returns(false);
            _command = new PauseCommand(_mockEditor.Object);
            Request.action = "pause";
        }

        [Test]
        public void Execute_WhenNotPlaying_ReturnsError() {
            _mockEditor.SetupGet(m => m.IsPlaying).Returns(false);

            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            Assert.That(Responses.HasCompleteResponse, Is.True);
            Assert.That(Responses.CompleteResponse.status, Is.EqualTo("error"));
            Assert.That(Responses.CompleteResponse.error, Does.Contain("not in Play Mode"));
        }

        [Test]
        public void Execute_WhenNotPlaying_ErrorMentionsPlayCommand() {
            _mockEditor.SetupGet(m => m.IsPlaying).Returns(false);

            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            Assert.That(Responses.CompleteResponse.error, Does.Contain("play"));
        }

        [Test]
        public void Execute_WhenNotPlaying_DoesNotTogglePause() {
            _mockEditor.SetupGet(m => m.IsPlaying).Returns(false);

            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            _mockEditor.VerifySet(m => m.IsPaused = It.IsAny<bool>(), Times.Never());
        }

        [Test]
        public void Execute_CallsOnCompleteExactlyOnce() {
            _mockEditor.SetupGet(m => m.IsPlaying).Returns(true);
            var callCount = 0;
            System.Action<CommandResponse> countingCallback = (response) => { callCount++; };

            _command.Execute(Request, Responses.OnProgress, countingCallback);

            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void Execute_DoesNotCallOnProgress() {
            _mockEditor.SetupGet(m => m.IsPlaying).Returns(true);

            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            Assert.That(Responses.ProgressResponses.Count, Is.EqualTo(0));
        }

        [Test]
        public void Execute_ConstructsResponseWithCorrectIdAndAction() {
            _mockEditor.SetupGet(m => m.IsPlaying).Returns(true);

            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            Assert.That(Responses.CompleteResponse.id, Is.EqualTo(Request.id));
            Assert.That(Responses.CompleteResponse.action, Is.EqualTo(Request.action));
        }

        [Test]
        public void Execute_WhenPlaying_ReturnsSuccess() {
            _mockEditor.SetupGet(m => m.IsPlaying).Returns(true);

            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            Assert.That(Responses.CompleteResponse.status, Is.EqualTo("success"));
        }

        [Test]
        public void Execute_WhenPlaying_TogglesPause() {
            _mockEditor.SetupGet(m => m.IsPlaying).Returns(true);
            _mockEditor.SetupProperty(m => m.IsPaused, false);

            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            _mockEditor.VerifySet(m => m.IsPaused = true, Times.Once());
        }

        [Test]
        public void Execute_WhenPlayingAndPaused_TogglesUnpause() {
            _mockEditor.SetupGet(m => m.IsPlaying).Returns(true);
            _mockEditor.SetupProperty(m => m.IsPaused, true);

            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            _mockEditor.VerifySet(m => m.IsPaused = false, Times.Once());
        }

        [Test]
        public void Execute_WhenPlaying_IncludesEditorStatus() {
            _mockEditor.SetupGet(m => m.IsPlaying).Returns(true);

            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            Assert.That(Responses.CompleteResponse.editorStatus, Is.Not.Null);
        }

        [Test]
        public void Execute_EditorStatusReflectsCompiling() {
            _mockEditor.SetupGet(m => m.IsPlaying).Returns(true);
            _mockEditor.SetupGet(m => m.IsCompiling).Returns(true);

            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            Assert.That(Responses.CompleteResponse.editorStatus.isCompiling, Is.True);
        }
    }
}

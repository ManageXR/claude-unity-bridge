using Moq;
using MXR.ClaudeBridge.Commands;
using MXR.ClaudeBridge.Models;
using NUnit.Framework;

namespace MXR.ClaudeBridge.Tests.Commands {
    /// <summary>
    /// Tests for PlayCommand using Moq to mock IEditorPlayMode.
    /// Focus: Toggle behavior, response construction, editorStatus reflects mock state.
    /// </summary>
    [TestFixture]
    public class PlayCommandTests : CommandTestFixture {
        private Mock<IEditorPlayMode> _mockEditor;
        private PlayCommand _command;

        [SetUp]
        public override void SetUp() {
            base.SetUp();
            _mockEditor = new Mock<IEditorPlayMode>();
            _mockEditor.SetupAllProperties();
            _mockEditor.SetupGet(m => m.IsCompiling).Returns(false);
            _mockEditor.SetupGet(m => m.IsUpdating).Returns(false);
            _command = new PlayCommand(_mockEditor.Object);
            Request.action = "play";
        }

        [Test]
        public void Execute_CallsOnCompleteExactlyOnce() {
            var callCount = 0;
            System.Action<CommandResponse> countingCallback = (response) => { callCount++; };

            _command.Execute(Request, Responses.OnProgress, countingCallback);

            Assert.That(callCount, Is.EqualTo(1), "onComplete should be called exactly once");
        }

        [Test]
        public void Execute_DoesNotCallOnProgress() {
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            Assert.That(Responses.ProgressResponses.Count, Is.EqualTo(0), "Should not call onProgress for synchronous command");
        }

        [Test]
        public void Execute_ConstructsResponseWithCorrectIdAndAction() {
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            Assert.That(Responses.CompleteResponse, Is.Not.Null, "Should call onComplete");
            Assert.That(Responses.CompleteResponse.id, Is.EqualTo(Request.id));
            Assert.That(Responses.CompleteResponse.action, Is.EqualTo(Request.action));
        }

        [Test]
        public void Execute_IncludesEditorStatusInResponse() {
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            Assert.That(Responses.CompleteResponse.editorStatus, Is.Not.Null, "Should include editorStatus in response");
        }

        [Test]
        public void Execute_ResponseHasDuration() {
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            Assert.That(Responses.CompleteResponse.duration_ms, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void Execute_ResponseStatusIsSuccess() {
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            Assert.That(Responses.CompleteResponse.status, Is.EqualTo("success"));
        }

        [Test]
        public void Execute_EchoesRequestIdInResponse() {
            var uniqueId = "test-unique-" + System.Guid.NewGuid().ToString();
            Request.id = uniqueId;

            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            Assert.That(Responses.CompleteResponse.id, Is.EqualTo(uniqueId));
        }

        [Test]
        public void Execute_WhenNotPlaying_TogglesIsPlayingToTrue() {
            _mockEditor.SetupProperty(m => m.IsPlaying, false);

            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            _mockEditor.VerifySet(m => m.IsPlaying = true, Times.Once());
        }

        [Test]
        public void Execute_WhenPlaying_TogglesIsPlayingToFalse() {
            _mockEditor.SetupProperty(m => m.IsPlaying, true);

            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            _mockEditor.VerifySet(m => m.IsPlaying = false, Times.Once());
        }

        [Test]
        public void Execute_EditorStatusReflectsCompiling() {
            _mockEditor.SetupGet(m => m.IsCompiling).Returns(true);

            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            Assert.That(Responses.CompleteResponse.editorStatus.isCompiling, Is.True);
        }

        [Test]
        public void Execute_EditorStatusReflectsUpdating() {
            _mockEditor.SetupGet(m => m.IsUpdating).Returns(true);

            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            Assert.That(Responses.CompleteResponse.editorStatus.isUpdating, Is.True);
        }

        [Test]
        public void Execute_EditorStatusReflectsPlayingState() {
            _mockEditor.SetupProperty(m => m.IsPlaying, false);
            _mockEditor.SetupGet(m => m.IsPaused).Returns(true);

            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            Assert.That(Responses.CompleteResponse.editorStatus.isPaused, Is.True);
        }
    }
}

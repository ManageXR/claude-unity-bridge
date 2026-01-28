using MXR.ClaudeBridge.Commands;
using MXR.ClaudeBridge.Models;
using NUnit.Framework;
using UnityEditor;

namespace MXR.ClaudeBridge.Tests.Commands {
    /// <summary>
    /// Tests for RefreshCommand.
    /// Focus: Tests REAL behavior (progress reporting, error handling, duration tracking)
    /// NOT testing: AssetDatabase internals, whether refresh actually works
    /// </summary>
    [TestFixture]
    public class RefreshCommandTests : CommandTestFixture {
        private RefreshCommand _command;

        [SetUp]
        public override void SetUp() {
            base.SetUp();
            _command = new RefreshCommand();
            Request.action = "refresh";
        }

        [Test]
        public void Execute_CallsOnProgressWithRunningStatus() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.HasProgressResponses, Is.True, "Should call onProgress");
            Assert.That(Responses.ProgressResponses.Count, Is.EqualTo(1), "Should call onProgress exactly once");
            Assert.That(Responses.ProgressResponses[0].status, Is.EqualTo("running"), "Progress status should be 'running'");
        }

        [Test]
        public void Execute_CallsOnCompleteWithSuccessStatus() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.HasCompleteResponse, Is.True, "Should call onComplete");
            Assert.That(Responses.CompleteResponse.status, Is.EqualTo("success"), "Complete status should be 'success'");
        }

        [Test]
        public void Execute_ProgressResponseIncludesCorrectIdAndAction() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            var progressResponse = Responses.ProgressResponses[0];
            Assert.That(progressResponse.id, Is.EqualTo(Request.id), "Progress response ID should match request ID");
            Assert.That(progressResponse.action, Is.EqualTo(Request.action), "Progress response action should match request action");
        }

        [Test]
        public void Execute_CompleteResponseIncludesCorrectIdAndAction() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.CompleteResponse.id, Is.EqualTo(Request.id), "Complete response ID should match request ID");
            Assert.That(Responses.CompleteResponse.action, Is.EqualTo(Request.action), "Complete response action should match request action");
        }

        [Test]
        public void Execute_MeasuresDuration() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert - Duration should be measured (will be small but >= 0)
            Assert.That(Responses.CompleteResponse.duration_ms, Is.GreaterThanOrEqualTo(0),
                "Duration should be measured and non-negative");
        }

        [Test]
        public void Execute_CallsCallbacksInCorrectOrder() {
            // Arrange
            var callOrder = new System.Collections.Generic.List<string>();

            System.Action<CommandResponse> trackingProgress = (response) => {
                callOrder.Add("progress");
                Responses.OnProgress(response);
            };

            System.Action<CommandResponse> trackingComplete = (response) => {
                callOrder.Add("complete");
                Responses.OnComplete(response);
            };

            // Act
            _command.Execute(Request, trackingProgress, trackingComplete);

            // Assert
            Assert.That(callOrder.Count, Is.EqualTo(2), "Should call both callbacks");
            Assert.That(callOrder[0], Is.EqualTo("progress"), "Should call onProgress first");
            Assert.That(callOrder[1], Is.EqualTo("complete"), "Should call onComplete second");
        }
    }
}

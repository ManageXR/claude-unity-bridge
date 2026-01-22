using MXR.ClaudeBridge.Models;
using NUnit.Framework;
using UnityEngine;

namespace MXR.ClaudeBridge.Tests.Models {
    /// <summary>
    /// Tests for CommandResponse factory methods and serialization.
    /// Focus: Verify factory methods create correct response structures
    /// </summary>
    [TestFixture]
    public class CommandResponseTests {
        [Test]
        public void Running_CreatesResponseWithRunningStatus() {
            // Act
            var response = CommandResponse.Running("test-id", "test-action");

            // Assert
            Assert.That(response.id, Is.EqualTo("test-id"), "Should set id");
            Assert.That(response.action, Is.EqualTo("test-action"), "Should set action");
            Assert.That(response.status, Is.EqualTo("running"), "Should set status to 'running'");
            Assert.That(response.progress, Is.Not.Null, "Should initialize progress");
            Assert.That(response.progress.current, Is.EqualTo(0), "Progress current should be 0");
            Assert.That(response.progress.total, Is.EqualTo(0), "Progress total should be 0");
        }

        [Test]
        public void Success_CreatesResponseWithSuccessStatus() {
            // Act
            var response = CommandResponse.Success("test-id", "test-action", 1234);

            // Assert
            Assert.That(response.id, Is.EqualTo("test-id"), "Should set id");
            Assert.That(response.action, Is.EqualTo("test-action"), "Should set action");
            Assert.That(response.status, Is.EqualTo("success"), "Should set status to 'success'");
            Assert.That(response.duration_ms, Is.EqualTo(1234), "Should set duration");
        }

        [Test]
        public void Failure_CreatesResponseWithFailureStatus() {
            // Act
            var response = CommandResponse.Failure("test-id", "test-action", 5678, "Test failure");

            // Assert
            Assert.That(response.id, Is.EqualTo("test-id"), "Should set id");
            Assert.That(response.action, Is.EqualTo("test-action"), "Should set action");
            Assert.That(response.status, Is.EqualTo("failure"), "Should set status to 'failure'");
            Assert.That(response.duration_ms, Is.EqualTo(5678), "Should set duration");
            Assert.That(response.error, Is.EqualTo("Test failure"), "Should set error message");
        }

        [Test]
        public void Error_CreatesResponseWithErrorStatus() {
            // Act
            var response = CommandResponse.Error("test-id", "test-action", "Test error");

            // Assert
            Assert.That(response.id, Is.EqualTo("test-id"), "Should set id");
            Assert.That(response.action, Is.EqualTo("test-action"), "Should set action");
            Assert.That(response.status, Is.EqualTo("error"), "Should set status to 'error'");
            Assert.That(response.error, Is.EqualTo("Test error"), "Should set error message");
        }

        [Test]
        public void Serialize_IncludesAllFields() {
            // Arrange
            var response = new CommandResponse {
                id = "serialize-test",
                status = "success",
                action = "run-tests",
                duration_ms = 9999,
                result = new TestResult { passed = 10, failed = 2, skipped = 1 }
            };

            // Act
            var json = JsonUtility.ToJson(response);

            // Assert - Verify key fields are present in JSON
            Assert.That(json, Does.Contain("\"id\":\"serialize-test\""), "JSON should contain id");
            Assert.That(json, Does.Contain("\"status\":\"success\""), "JSON should contain status");
            Assert.That(json, Does.Contain("\"action\":\"run-tests\""), "JSON should contain action");
            Assert.That(json, Does.Contain("\"duration_ms\":9999"), "JSON should contain duration_ms");
        }

        [Test]
        public void Deserialize_RestoresAllFields() {
            // Arrange
            var json = @"{
                ""id"": ""deserialize-test"",
                ""status"": ""failure"",
                ""action"": ""compile"",
                ""duration_ms"": 8888,
                ""error"": ""Compilation failed""
            }";

            // Act
            var response = JsonUtility.FromJson<CommandResponse>(json);

            // Assert
            Assert.That(response.id, Is.EqualTo("deserialize-test"), "Should restore id");
            Assert.That(response.status, Is.EqualTo("failure"), "Should restore status");
            Assert.That(response.action, Is.EqualTo("compile"), "Should restore action");
            Assert.That(response.duration_ms, Is.EqualTo(8888), "Should restore duration_ms");
            Assert.That(response.error, Is.EqualTo("Compilation failed"), "Should restore error");
        }

        [Test]
        public void Serialize_ThenDeserialize_RoundTrip() {
            // Arrange
            var original = CommandResponse.Success("round-trip", "get-status", 1111);
            original.error = "Some diagnostic info";

            // Act
            var json = JsonUtility.ToJson(original);
            var restored = JsonUtility.FromJson<CommandResponse>(json);

            // Assert
            Assert.That(restored.id, Is.EqualTo(original.id), "Round-trip should preserve id");
            Assert.That(restored.status, Is.EqualTo(original.status), "Round-trip should preserve status");
            Assert.That(restored.action, Is.EqualTo(original.action), "Round-trip should preserve action");
            Assert.That(restored.duration_ms, Is.EqualTo(original.duration_ms), "Round-trip should preserve duration");
            Assert.That(restored.error, Is.EqualTo(original.error), "Round-trip should preserve error");
        }
    }
}

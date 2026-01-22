using MXR.ClaudeBridge.Models;
using NUnit.Framework;
using UnityEngine;

namespace MXR.ClaudeBridge.Tests.Models {
    /// <summary>
    /// Tests for CommandRequest serialization/deserialization.
    /// Focus: Verify JSON serialization works correctly for file-based protocol
    /// </summary>
    [TestFixture]
    public class CommandRequestTests {
        [Test]
        public void Serialize_IncludesAllFields() {
            // Arrange
            var request = new CommandRequest {
                id = "test-123",
                timestamp = "2024-01-01T00:00:00Z",
                action = "run-tests",
                @params = new CommandParams {
                    testMode = "EditMode",
                    filter = "MyTests",
                    buildVariant = "Debug",
                    limit = "10"
                }
            };

            // Act
            var json = JsonUtility.ToJson(request);

            // Assert - Verify all fields are present in JSON
            Assert.That(json, Does.Contain("\"id\":\"test-123\""), "JSON should contain id field");
            Assert.That(json, Does.Contain("\"action\":\"run-tests\""), "JSON should contain action field");
            Assert.That(json, Does.Contain("\"timestamp\":\"2024-01-01T00:00:00Z\""), "JSON should contain timestamp field");
            Assert.That(json, Does.Contain("\"testMode\":\"EditMode\""), "JSON should contain params.testMode field");
        }

        [Test]
        public void Deserialize_RestoresAllFields() {
            // Arrange
            var json = @"{
                ""id"": ""test-456"",
                ""timestamp"": ""2024-01-02T00:00:00Z"",
                ""action"": ""compile"",
                ""params"": {
                    ""testMode"": ""PlayMode"",
                    ""filter"": ""Integration"",
                    ""buildVariant"": ""Release"",
                    ""limit"": ""20""
                }
            }";

            // Act
            var request = JsonUtility.FromJson<CommandRequest>(json);

            // Assert
            Assert.That(request.id, Is.EqualTo("test-456"), "id should be deserialized");
            Assert.That(request.timestamp, Is.EqualTo("2024-01-02T00:00:00Z"), "timestamp should be deserialized");
            Assert.That(request.action, Is.EqualTo("compile"), "action should be deserialized");
            Assert.That(request.@params, Is.Not.Null, "params should be deserialized");
            Assert.That(request.@params.testMode, Is.EqualTo("PlayMode"), "params.testMode should be deserialized");
            Assert.That(request.@params.filter, Is.EqualTo("Integration"), "params.filter should be deserialized");
        }

        [Test]
        public void Serialize_ThenDeserialize_RoundTrip() {
            // Arrange
            var original = new CommandRequest {
                id = "round-trip-test",
                timestamp = "2024-01-03T12:00:00Z",
                action = "get-status",
                @params = new CommandParams {
                    testMode = "EditMode"
                }
            };

            // Act - Serialize and deserialize
            var json = JsonUtility.ToJson(original);
            var restored = JsonUtility.FromJson<CommandRequest>(json);

            // Assert - Should match original
            Assert.That(restored.id, Is.EqualTo(original.id), "Round-trip should preserve id");
            Assert.That(restored.timestamp, Is.EqualTo(original.timestamp), "Round-trip should preserve timestamp");
            Assert.That(restored.action, Is.EqualTo(original.action), "Round-trip should preserve action");
            Assert.That(restored.@params.testMode, Is.EqualTo(original.@params.testMode), "Round-trip should preserve params");
        }

        [Test]
        public void Deserialize_WithNullParams_DoesNotThrow() {
            // Arrange - JSON without params field
            var json = @"{
                ""id"": ""test-null-params"",
                ""timestamp"": ""2024-01-04T00:00:00Z"",
                ""action"": ""refresh""
            }";

            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() => {
                var request = JsonUtility.FromJson<CommandRequest>(json);
                Assert.That(request.id, Is.EqualTo("test-null-params"), "Should deserialize even with missing params");
            });
        }
    }
}

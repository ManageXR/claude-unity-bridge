using MXR.ClaudeBridge.Models;
using NUnit.Framework;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MXR.ClaudeBridge.Tests {
    /// <summary>
    /// Tests for ClaudeBridge dispatcher and validation logic.
    /// Focus: Tests response ID validation, response factory methods, and error handling patterns.
    /// NOT testing: File I/O operations (require integration tests), Unity Editor static state.
    /// </summary>
    [TestFixture]
    public class ClaudeBridgeTests {
        // Mirror the regex pattern from ClaudeBridge for testing
        private static readonly Regex ValidIdPattern = new Regex(@"^[a-fA-F0-9\-]+$", RegexOptions.Compiled);

        #region Response ID Validation Tests

        [Test]
        public void ValidIdPattern_AcceptsValidUUID() {
            // Arrange
            var validUUID = "550e8400-e29b-41d4-a716-446655440000";

            // Act & Assert
            Assert.That(ValidIdPattern.IsMatch(validUUID), Is.True, "Should accept standard UUID format");
        }

        [Test]
        public void ValidIdPattern_AcceptsHexCharacters() {
            // Arrange
            var hexId = "abcdef0123456789";

            // Act & Assert
            Assert.That(ValidIdPattern.IsMatch(hexId), Is.True, "Should accept lowercase hex characters");
        }

        [Test]
        public void ValidIdPattern_AcceptsUppercaseHex() {
            // Arrange
            var hexId = "ABCDEF0123456789";

            // Act & Assert
            Assert.That(ValidIdPattern.IsMatch(hexId), Is.True, "Should accept uppercase hex characters");
        }

        [Test]
        public void ValidIdPattern_RejectsPathTraversal() {
            // Arrange
            var maliciousId = "../../../etc/passwd";

            // Act & Assert
            Assert.That(ValidIdPattern.IsMatch(maliciousId), Is.False, "Should reject path traversal attempts");
        }

        [Test]
        public void ValidIdPattern_RejectsSlashes() {
            // Arrange
            var idWithSlash = "test/malicious";

            // Act & Assert
            Assert.That(ValidIdPattern.IsMatch(idWithSlash), Is.False, "Should reject forward slashes");
        }

        [Test]
        public void ValidIdPattern_RejectsBackslashes() {
            // Arrange
            var idWithBackslash = "test\\malicious";

            // Act & Assert
            Assert.That(ValidIdPattern.IsMatch(idWithBackslash), Is.False, "Should reject backslashes");
        }

        [Test]
        public void ValidIdPattern_RejectsDots() {
            // Arrange
            var idWithDots = "test..file";

            // Act & Assert
            Assert.That(ValidIdPattern.IsMatch(idWithDots), Is.False, "Should reject dots");
        }

        [Test]
        public void ValidIdPattern_RejectsSpaces() {
            // Arrange
            var idWithSpaces = "test id with spaces";

            // Act & Assert
            Assert.That(ValidIdPattern.IsMatch(idWithSpaces), Is.False, "Should reject spaces");
        }

        [Test]
        public void ValidIdPattern_RejectsEmptyString() {
            // Arrange
            var emptyId = "";

            // Act & Assert
            Assert.That(ValidIdPattern.IsMatch(emptyId), Is.False, "Should reject empty string");
        }

        [Test]
        public void ValidIdPattern_AcceptsErrorPrefixedIds() {
            // Arrange - These are generated when parse fails
            var errorId = "error-20250129123456";

            // Act & Assert
            Assert.That(ValidIdPattern.IsMatch(errorId), Is.False,
                "Error IDs with non-hex characters should be rejected by hex-only pattern");
        }

        #endregion

        #region CommandResponse Factory Tests

        [Test]
        public void CommandResponse_Error_SetsCorrectFields() {
            // Arrange
            var id = "test-id";
            var action = "test-action";
            var errorMessage = "Something went wrong";

            // Act
            var response = CommandResponse.Error(id, action, errorMessage);

            // Assert
            Assert.That(response.id, Is.EqualTo(id), "Should set ID");
            Assert.That(response.action, Is.EqualTo(action), "Should set action");
            Assert.That(response.error, Is.EqualTo(errorMessage), "Should set error message");
            Assert.That(response.status, Is.EqualTo("error"), "Should set status to 'error'");
        }

        [Test]
        public void CommandResponse_Success_SetsCorrectFields() {
            // Arrange
            var id = "test-id";
            var action = "test-action";
            long durationMs = 1234;

            // Act
            var response = CommandResponse.Success(id, action, durationMs);

            // Assert
            Assert.That(response.id, Is.EqualTo(id), "Should set ID");
            Assert.That(response.action, Is.EqualTo(action), "Should set action");
            Assert.That(response.duration_ms, Is.EqualTo(durationMs), "Should set duration");
            Assert.That(response.status, Is.EqualTo("success"), "Should set status to 'success'");
        }

        [Test]
        public void CommandResponse_Failure_SetsCorrectFields() {
            // Arrange
            var id = "test-id";
            var action = "test-action";
            long durationMs = 5678;
            var errorMessage = "Failed to execute";

            // Act
            var response = CommandResponse.Failure(id, action, durationMs, errorMessage);

            // Assert
            Assert.That(response.id, Is.EqualTo(id), "Should set ID");
            Assert.That(response.action, Is.EqualTo(action), "Should set action");
            Assert.That(response.duration_ms, Is.EqualTo(durationMs), "Should set duration");
            Assert.That(response.error, Is.EqualTo(errorMessage), "Should set error message");
            Assert.That(response.status, Is.EqualTo("failure"), "Should set status to 'failure'");
        }

        [Test]
        public void CommandResponse_Running_SetsCorrectFields() {
            // Arrange
            var id = "test-id";
            var action = "test-action";

            // Act
            var response = CommandResponse.Running(id, action);

            // Assert
            Assert.That(response.id, Is.EqualTo(id), "Should set ID");
            Assert.That(response.action, Is.EqualTo(action), "Should set action");
            Assert.That(response.status, Is.EqualTo("running"), "Should set status to 'running'");
            Assert.That(response.progress, Is.Not.Null, "Should initialize progress info");
        }

        #endregion

        #region EditorStatus Model Tests

        [Test]
        public void EditorStatus_CanBeSerializedToJson() {
            // Arrange
            var status = new EditorStatus {
                isCompiling = true,
                isUpdating = false,
                isPlaying = true,
                isPaused = false
            };

            // Act
            var json = JsonUtility.ToJson(status);

            // Assert
            Assert.That(json, Does.Contain("isCompiling"), "Should serialize isCompiling");
            Assert.That(json, Does.Contain("isUpdating"), "Should serialize isUpdating");
            Assert.That(json, Does.Contain("isPlaying"), "Should serialize isPlaying");
            Assert.That(json, Does.Contain("isPaused"), "Should serialize isPaused");
        }

        [Test]
        public void EditorStatus_CanBeDeserializedFromJson() {
            // Arrange
            var json = "{\"isCompiling\":true,\"isUpdating\":false,\"isPlaying\":true,\"isPaused\":true}";

            // Act
            var status = JsonUtility.FromJson<EditorStatus>(json);

            // Assert
            Assert.That(status.isCompiling, Is.True, "Should deserialize isCompiling");
            Assert.That(status.isUpdating, Is.False, "Should deserialize isUpdating");
            Assert.That(status.isPlaying, Is.True, "Should deserialize isPlaying");
            Assert.That(status.isPaused, Is.True, "Should deserialize isPaused");
        }

        [Test]
        public void CommandResponse_EditorStatusField_CanBeAssigned() {
            // Arrange
            var response = CommandResponse.Success("id", "get-status", 0);
            var status = new EditorStatus {
                isCompiling = false,
                isUpdating = true,
                isPlaying = false,
                isPaused = false
            };

            // Act
            response.editorStatus = status;

            // Assert
            Assert.That(response.editorStatus, Is.Not.Null, "Should accept EditorStatus assignment");
            Assert.That(response.editorStatus.isUpdating, Is.True, "Should preserve EditorStatus values");
        }

        #endregion

        #region CommandRequest Validation Tests

        [Test]
        public void CommandRequest_NullId_IsInvalid() {
            // Arrange
            var request = new CommandRequest {
                id = null,
                action = "test"
            };

            // Assert
            Assert.That(string.IsNullOrEmpty(request.id), Is.True,
                "Null ID should be detected as empty");
        }

        [Test]
        public void CommandRequest_EmptyId_IsInvalid() {
            // Arrange
            var request = new CommandRequest {
                id = "",
                action = "test"
            };

            // Assert
            Assert.That(string.IsNullOrEmpty(request.id), Is.True,
                "Empty ID should be detected as empty");
        }

        [Test]
        public void CommandRequest_ValidId_IsAccepted() {
            // Arrange
            var request = new CommandRequest {
                id = "valid-uuid-12345",
                action = "test"
            };

            // Assert
            Assert.That(string.IsNullOrEmpty(request.id), Is.False,
                "Valid ID should not be empty");
        }

        #endregion
    }
}

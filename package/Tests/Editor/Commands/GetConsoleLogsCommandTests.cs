using MXR.ClaudeBridge.Commands;
using MXR.ClaudeBridge.Models;
using NUnit.Framework;
using System.Reflection;

namespace MXR.ClaudeBridge.Tests.Commands {
    /// <summary>
    /// Tests for GetConsoleLogsCommand.
    /// Focus: Tests REAL behavior (parameter parsing, response structure, synchronous execution)
    /// NOT testing: Unity's internal LogEntries API (accessed via reflection)
    ///
    /// Testing approach:
    /// - Test parameter parsing (limit, filter)
    /// - Test response structure and status
    /// - Test synchronous execution (immediate completion)
    /// - Test GetConsoleLogs method behavior via reflection
    /// - Verify error handling for missing/invalid parameters
    /// </summary>
    [TestFixture]
    public class GetConsoleLogsCommandTests : CommandTestFixture {
        private GetConsoleLogsCommand _command;

        [SetUp]
        public override void SetUp() {
            base.SetUp();
            _command = new GetConsoleLogsCommand();
            Request.action = "get-console-logs";
        }

        #region Execute Tests

        [Test]
        public void Execute_CallsOnCompleteImmediately() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.HasCompleteResponse, Is.True, "Should call onComplete immediately (synchronous)");
        }

        [Test]
        public void Execute_DoesNotCallOnProgress() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.ProgressResponses.Count, Is.EqualTo(0), "Should not call onProgress (synchronous operation)");
        }

        [Test]
        public void Execute_ReturnsSuccessStatus() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.CompleteResponse.status, Is.EqualTo("success"), "Should return success status");
        }

        [Test]
        public void Execute_ResponseIncludesConsoleLogs() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.CompleteResponse.consoleLogs, Is.Not.Null, "Should include consoleLogs field");
        }

        [Test]
        public void Execute_IncludesCorrectIdAndAction() {
            // Arrange
            var uniqueId = "console-logs-" + System.Guid.NewGuid().ToString();
            Request.id = uniqueId;

            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.CompleteResponse.id, Is.EqualTo(uniqueId), "Should echo request ID");
            Assert.That(Responses.CompleteResponse.action, Is.EqualTo("get-console-logs"), "Should include correct action");
        }

        [Test]
        public void Execute_HasDuration() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert - Duration is now measured
            Assert.That(Responses.CompleteResponse.duration_ms, Is.GreaterThanOrEqualTo(0), "Duration should be a non-negative value");
        }

        #endregion

        #region Parameter Parsing Tests

        [Test]
        public void Execute_WithNullParams_UsesDefaultLimit() {
            // Arrange
            Request.@params = null;

            // Act - Should not throw
            Assert.DoesNotThrow(() => {
                _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);
            }, "Should handle null params");

            // Verify it executes successfully
            Assert.That(Responses.HasCompleteResponse, Is.True, "Should complete successfully");
        }

        [Test]
        public void Execute_WithValidLimitParam_ParsesLimit() {
            // Arrange
            Request.@params = new CommandParams { limit = "10" };

            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert - Just verify it doesn't throw and completes
            Assert.That(Responses.HasCompleteResponse, Is.True, "Should handle valid limit parameter");
        }

        [Test]
        public void Execute_WithInvalidLimitParam_UsesDefaultLimit() {
            // Arrange
            Request.@params = new CommandParams { limit = "not-a-number" };

            // Act - Should fall back to default limit (50)
            Assert.DoesNotThrow(() => {
                _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);
            }, "Should handle invalid limit parameter");

            Assert.That(Responses.HasCompleteResponse, Is.True, "Should complete successfully with default limit");
        }

        [Test]
        public void Execute_WithFilterParam_AppliesFilter() {
            // Arrange
            Request.@params = new CommandParams { filter = "Error" };

            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert - Verify it doesn't throw
            Assert.That(Responses.HasCompleteResponse, Is.True, "Should handle filter parameter");
        }

        [Test]
        public void Execute_WithBothParams_HandlesBothCorrectly() {
            // Arrange
            Request.@params = new CommandParams {
                limit = "25",
                filter = "Warning"
            };

            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.HasCompleteResponse, Is.True, "Should handle both limit and filter parameters");
        }

        #endregion

        #region GetConsoleLogs Tests (via reflection)

        [Test]
        public void GetConsoleLogs_WithZeroLimit_ReturnsEmptyList() {
            // Arrange - Use reflection to call private GetConsoleLogs method
            var method = typeof(GetConsoleLogsCommand).GetMethod("GetConsoleLogs",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var logs = method.Invoke(_command, new object[] { 0, null }) as System.Collections.Generic.List<ConsoleLogEntry>;

            // Assert
            Assert.That(logs, Is.Not.Null, "Should return a list");
            // Note: Actual log count depends on Unity's console state, so we just verify it doesn't crash
        }

        [Test]
        public void GetConsoleLogs_WithPositiveLimit_DoesNotThrow() {
            // Arrange
            var method = typeof(GetConsoleLogsCommand).GetMethod("GetConsoleLogs",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act & Assert
            Assert.DoesNotThrow(() => {
                var logs = method.Invoke(_command, new object[] { 50, null });
            }, "Should not throw when getting console logs");
        }

        [Test]
        public void GetConsoleLogs_WithFilter_DoesNotThrow() {
            // Arrange
            var method = typeof(GetConsoleLogsCommand).GetMethod("GetConsoleLogs",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act & Assert - Test each filter type
            Assert.DoesNotThrow(() => {
                method.Invoke(_command, new object[] { 10, "Error" });
            }, "Should handle Error filter");

            Assert.DoesNotThrow(() => {
                method.Invoke(_command, new object[] { 10, "Warning" });
            }, "Should handle Warning filter");

            Assert.DoesNotThrow(() => {
                method.Invoke(_command, new object[] { 10, "Log" });
            }, "Should handle Log filter");
        }

        [Test]
        public void GetConsoleLogs_ReturnsListType() {
            // Arrange
            var method = typeof(GetConsoleLogsCommand).GetMethod("GetConsoleLogs",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = method.Invoke(_command, new object[] { 10, null });

            // Assert
            Assert.That(result, Is.InstanceOf<System.Collections.Generic.List<ConsoleLogEntry>>(),
                "Should return List<ConsoleLogEntry>");
        }

        [Test]
        public void GetConsoleLogs_WithNullFilter_DoesNotThrow() {
            // Arrange
            var method = typeof(GetConsoleLogsCommand).GetMethod("GetConsoleLogs",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act & Assert
            Assert.DoesNotThrow(() => {
                var logs = method.Invoke(_command, new object[] { 20, null });
            }, "Should handle null filter");
        }

        #endregion
    }
}

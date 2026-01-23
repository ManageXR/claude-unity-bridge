using MXR.ClaudeBridge.Commands;
using MXR.ClaudeBridge.Models;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using UnityEditor.TestTools.TestRunner.Api;
using System.Reflection;
using System.Collections.Generic;
using TestStatus = UnityEditor.TestTools.TestRunner.Api.TestStatus;
using RunState = UnityEditor.TestTools.TestRunner.Api.RunState;

namespace MXR.ClaudeBridge.Tests.Commands {
    /// <summary>
    /// Tests for RunTestsCommand.
    /// Focus: Tests REAL behavior (test mode parsing, callback tracking, progress reporting)
    /// NOT testing: TestRunnerApi internals, actual test execution
    ///
    /// Testing approach:
    /// - Test GetTestMode() parameter parsing logic
    /// - Test Execute() initial setup and progress callback
    /// - Use reflection to access and test TestCallbacks class
    /// - Simulate ICallbacks events to test result tracking
    /// - Verify progress reporting at each stage
    /// </summary>
    [TestFixture]
    public class RunTestsCommandTests : CommandTestFixture {
        private RunTestsCommand _command;

        [SetUp]
        public override void SetUp() {
            base.SetUp();
            _command = new RunTestsCommand();
            Request.action = "run-tests";
        }

        #region GetTestMode Tests

        [Test]
        public void GetTestMode_WithEditMode_ReturnsEditMode() {
            // Arrange
            Request.@params = new CommandParams { testMode = "EditMode" };

            // Act
            var testMode = InvokeGetTestMode(_command, "EditMode");

            // Assert
            Assert.That(testMode, Is.EqualTo(TestMode.EditMode), "Should return EditMode");
        }

        [Test]
        public void GetTestMode_WithPlayMode_ReturnsPlayMode() {
            // Arrange
            Request.@params = new CommandParams { testMode = "PlayMode" };

            // Act
            var testMode = InvokeGetTestMode(_command, "PlayMode");

            // Assert
            Assert.That(testMode, Is.EqualTo(TestMode.PlayMode), "Should return PlayMode");
        }

        [Test]
        public void GetTestMode_WithNull_ReturnsBothModes() {
            // Act
            var testMode = InvokeGetTestMode(_command, null);

            // Assert
            Assert.That(testMode, Is.EqualTo(TestMode.EditMode | TestMode.PlayMode), "Should return both modes");
        }

        [Test]
        public void GetTestMode_WithInvalidMode_ReturnsBothModes() {
            // Act
            var testMode = InvokeGetTestMode(_command, "InvalidMode");

            // Assert
            Assert.That(testMode, Is.EqualTo(TestMode.EditMode | TestMode.PlayMode), "Should return both modes for invalid input");
        }

        [Test]
        public void GetTestMode_CaseInsensitive() {
            // Act
            var testMode1 = InvokeGetTestMode(_command, "editmode");
            var testMode2 = InvokeGetTestMode(_command, "EDITMODE");
            var testMode3 = InvokeGetTestMode(_command, "eDiTmOdE");

            // Assert
            Assert.That(testMode1, Is.EqualTo(TestMode.EditMode), "Should handle lowercase");
            Assert.That(testMode2, Is.EqualTo(TestMode.EditMode), "Should handle uppercase");
            Assert.That(testMode3, Is.EqualTo(TestMode.EditMode), "Should handle mixed case");
        }

        #endregion

        #region Execute Tests

        [Test]
        public void Execute_CallsOnProgressWithRunningStatus() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert
            Assert.That(Responses.HasProgressResponses, Is.True, "Should call onProgress");
            Assert.That(Responses.ProgressResponses.Count, Is.GreaterThanOrEqualTo(1), "Should call onProgress at least once");
            Assert.That(Responses.ProgressResponses[0].status, Is.EqualTo("running"), "First progress should be 'running'");
        }

        [Test]
        public void Execute_DoesNotCallOnCompleteImmediately() {
            // Act
            _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);

            // Assert - Async command should NOT complete immediately
            Assert.That(Responses.HasCompleteResponse, Is.False, "Should NOT call onComplete immediately (async operation)");
        }

        [Test]
        public void Execute_WithFilterParam_ParsesFilter() {
            // Arrange
            Request.@params = new CommandParams {
                filter = "TestA;TestB;TestC"
            };

            // Act - Just verify it doesn't throw
            Assert.DoesNotThrow(() => {
                _command.Execute(Request, Responses.OnProgress, Responses.OnComplete);
            }, "Should handle filter parameter without errors");
        }

        #endregion

        #region TestCallbacks Tests

        [Test]
        public void TestCallbacks_RunStarted_SendsProgressWithTotal() {
            // Arrange
            var callbacks = CreateTestCallbacks(Request.id, Responses);
            var mockTest = CreateMockTest("TestSuite", isSuite: true, childCount: 5);

            // Act
            InvokeCallbackMethod(callbacks, "RunStarted", mockTest);

            // Assert
            Assert.That(Responses.HasProgressResponses, Is.True, "Should send progress on RunStarted");
            var progress = Responses.ProgressResponses[Responses.ProgressResponses.Count - 1];
            Assert.That(progress.progress, Is.Not.Null, "Should include progress info");
            Assert.That(progress.progress.total, Is.EqualTo(5), "Should report total test count");
        }

        [Test]
        public void TestCallbacks_TestStarted_SendsProgressWithCurrentTest() {
            // Arrange
            var callbacks = CreateTestCallbacks(Request.id, Responses);
            var mockTest = CreateMockTest("MyTest", isSuite: false);

            // Act
            InvokeCallbackMethod(callbacks, "TestStarted", mockTest);

            // Assert
            Assert.That(Responses.HasProgressResponses, Is.True, "Should send progress on TestStarted");
            var progress = Responses.ProgressResponses[Responses.ProgressResponses.Count - 1];
            Assert.That(progress.progress, Is.Not.Null, "Should include progress info");
            Assert.That(progress.progress.currentTest, Is.EqualTo("MyTest"), "Should report current test name");
        }

        [Test]
        public void TestCallbacks_TestFinished_Passed_IncrementsPassedCount() {
            // Arrange
            var callbacks = CreateTestCallbacks(Request.id, Responses);
            var mockResult = CreateMockTestResult("PassedTest", TestStatus.Passed);

            // Initialize with RunStarted first
            var mockTestSuite = CreateMockTest("Suite", isSuite: true, childCount: 1);
            InvokeCallbackMethod(callbacks, "RunStarted", mockTestSuite);

            // Act
            InvokeCallbackMethod(callbacks, "TestFinished", mockResult);

            // Then call RunFinished to get final result
            InvokeCallbackMethod(callbacks, "RunFinished", mockResult);

            // Assert
            Assert.That(Responses.HasCompleteResponse, Is.True, "Should have completion response");
            Assert.That(Responses.CompleteResponse.result, Is.Not.Null, "Should include result");
            Assert.That(Responses.CompleteResponse.result.passed, Is.EqualTo(1), "Should count passed test");
            Assert.That(Responses.CompleteResponse.result.failed, Is.EqualTo(0), "Should have no failed tests");
        }

        [Test]
        public void TestCallbacks_TestFinished_Failed_IncrementsFailedCount() {
            // Arrange
            var callbacks = CreateTestCallbacks(Request.id, Responses);
            var mockResult = CreateMockTestResult("FailedTest", TestStatus.Failed, "Test failed message");

            // Initialize with RunStarted
            var mockTestSuite = CreateMockTest("Suite", isSuite: true, childCount: 1);
            InvokeCallbackMethod(callbacks, "RunStarted", mockTestSuite);

            // Act
            InvokeCallbackMethod(callbacks, "TestFinished", mockResult);
            InvokeCallbackMethod(callbacks, "RunFinished", mockResult);

            // Assert
            Assert.That(Responses.CompleteResponse.result.failed, Is.EqualTo(1), "Should count failed test");
            Assert.That(Responses.CompleteResponse.result.passed, Is.EqualTo(0), "Should have no passed tests");
            Assert.That(Responses.CompleteResponse.result.failures, Is.Not.Null, "Should include failures list");
            Assert.That(Responses.CompleteResponse.result.failures.Count, Is.EqualTo(1), "Should have one failure");
            Assert.That(Responses.CompleteResponse.result.failures[0].message, Does.Contain("Test failed"), "Should include failure message");
        }

        [Test]
        public void TestCallbacks_TestFinished_Skipped_IncrementsSkippedCount() {
            // Arrange
            var callbacks = CreateTestCallbacks(Request.id, Responses);
            var mockResult = CreateMockTestResult("SkippedTest", TestStatus.Skipped);

            // Initialize with RunStarted
            var mockTestSuite = CreateMockTest("Suite", isSuite: true, childCount: 1);
            InvokeCallbackMethod(callbacks, "RunStarted", mockTestSuite);

            // Act
            InvokeCallbackMethod(callbacks, "TestFinished", mockResult);
            InvokeCallbackMethod(callbacks, "RunFinished", mockResult);

            // Assert
            Assert.That(Responses.CompleteResponse.result.skipped, Is.EqualTo(1), "Should count skipped test");
            Assert.That(Responses.CompleteResponse.result.passed, Is.EqualTo(0), "Should have no passed tests");
            Assert.That(Responses.CompleteResponse.result.failed, Is.EqualTo(0), "Should have no failed tests");
        }

        [Test]
        public void TestCallbacks_RunFinished_WithFailures_ReturnsFailureStatus() {
            // Arrange
            var callbacks = CreateTestCallbacks(Request.id, Responses);
            var mockTestSuite = CreateMockTest("Suite", isSuite: true, childCount: 2);
            var passedResult = CreateMockTestResult("Test1", TestStatus.Passed);
            var failedResult = CreateMockTestResult("Test2", TestStatus.Failed, "Error");

            // Act - Simulate test run
            InvokeCallbackMethod(callbacks, "RunStarted", mockTestSuite);
            InvokeCallbackMethod(callbacks, "TestFinished", passedResult);
            InvokeCallbackMethod(callbacks, "TestFinished", failedResult);
            InvokeCallbackMethod(callbacks, "RunFinished", mockTestSuite);

            // Assert
            Assert.That(Responses.HasCompleteResponse, Is.True, "Should call onComplete");
            Assert.That(Responses.CompleteResponse.status, Is.EqualTo("failure"), "Should return failure status when tests fail");
            Assert.That(Responses.CompleteResponse.result.passed, Is.EqualTo(1), "Should count passed test");
            Assert.That(Responses.CompleteResponse.result.failed, Is.EqualTo(1), "Should count failed test");
        }

        [Test]
        public void TestCallbacks_RunFinished_AllPassed_ReturnsSuccessStatus() {
            // Arrange
            var callbacks = CreateTestCallbacks(Request.id, Responses);
            var mockTestSuite = CreateMockTest("Suite", isSuite: true, childCount: 2);
            var result1 = CreateMockTestResult("Test1", TestStatus.Passed);
            var result2 = CreateMockTestResult("Test2", TestStatus.Passed);

            // Act
            InvokeCallbackMethod(callbacks, "RunStarted", mockTestSuite);
            InvokeCallbackMethod(callbacks, "TestFinished", result1);
            InvokeCallbackMethod(callbacks, "TestFinished", result2);
            InvokeCallbackMethod(callbacks, "RunFinished", mockTestSuite);

            // Assert
            Assert.That(Responses.CompleteResponse.status, Is.EqualTo("success"), "Should return success status when all tests pass");
            Assert.That(Responses.CompleteResponse.result.passed, Is.EqualTo(2), "Should count all passed tests");
            Assert.That(Responses.CompleteResponse.result.failed, Is.EqualTo(0), "Should have no failures");
        }

        [Test]
        public void TestCallbacks_RunFinished_IncludesDuration() {
            // Arrange
            var callbacks = CreateTestCallbacks(Request.id, Responses);
            var mockResult = CreateMockTestResult("Test", TestStatus.Passed);

            // Act
            InvokeCallbackMethod(callbacks, "RunFinished", mockResult);

            // Assert
            Assert.That(Responses.HasCompleteResponse, Is.True, "Should have completion response");
            Assert.That(Responses.CompleteResponse.duration_ms, Is.GreaterThanOrEqualTo(0), "Should include duration");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Uses reflection to invoke private GetTestMode method
        /// </summary>
        private TestMode InvokeGetTestMode(RunTestsCommand command, string mode) {
            var method = typeof(RunTestsCommand).GetMethod("GetTestMode",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return (TestMode)method.Invoke(command, new object[] { mode });
        }

        /// <summary>
        /// Creates an instance of the private TestCallbacks class
        /// </summary>
        private object CreateTestCallbacks(string commandId, ResponseCapture responses) {
            var callbacksType = typeof(RunTestsCommand).GetNestedType("TestCallbacks",
                BindingFlags.NonPublic);
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            return System.Activator.CreateInstance(callbacksType,
                commandId, stopwatch, responses.OnProgress, responses.OnComplete);
        }

        /// <summary>
        /// Invokes a method on TestCallbacks object using reflection
        /// </summary>
        private void InvokeCallbackMethod(object callbacks, string methodName, object parameter) {
            var method = callbacks.GetType().GetMethod(methodName);
            method.Invoke(callbacks, new object[] { parameter });
        }

        /// <summary>
        /// Creates a mock ITestAdaptor for testing
        /// </summary>
        private ITestAdaptor CreateMockTest(string name, bool isSuite, int childCount = 0) {
            var mock = new MockTestAdaptor {
                Name = name,
                FullName = "TestSuite." + name,
                IsSuite = isSuite
            };

            if (isSuite && childCount > 0) {
                var children = new List<ITestAdaptor>();
                for (int i = 0; i < childCount; i++) {
                    children.Add(CreateMockTest($"Test{i}", false));
                }
                mock.Children = children;
            }

            return mock;
        }

        /// <summary>
        /// Creates a mock ITestResultAdaptor for testing
        /// </summary>
        private ITestResultAdaptor CreateMockTestResult(string testName, TestStatus status, string message = null) {
            return new MockTestResultAdaptor {
                Test = CreateMockTest(testName, false),
                TestStatus = status,
                Message = message
            };
        }

        #endregion

        #region Mock Classes

        private class MockTestAdaptor : ITestAdaptor {
            public string Id { get; set; }
            public string Name { get; set; }
            public string FullName { get; set; }
            public int TestCaseCount { get; set; }
            public bool HasChildren => Children != null && ((IEnumerable<ITestAdaptor>)Children).GetEnumerator().MoveNext();
            public bool IsSuite { get; set; }
            public IEnumerable<ITestAdaptor> Children { get; set; } = new List<ITestAdaptor>();
            public ITestAdaptor Parent { get; set; }
            public int TestCaseTimeout { get; set; }
            public ITypeInfo TypeInfo { get; set; }
            public IMethodInfo Method { get; set; }
            public string[] Categories { get; set; }
            public bool IsTestAssembly { get; set; }
            public RunState RunState { get; set; }
            public string Description { get; set; }
            public string SkipReason { get; set; }
            public string ParentId { get; set; }
            public string UniqueName { get; set; }
            public string ParentUniqueName { get; set; }
            public string ParentFullName { get; set; }
            public int ChildIndex { get; set; }
            public TestMode TestMode { get; set; }
            public string Arguments { get; set; }
        }

        private class MockTestResultAdaptor : ITestResultAdaptor {
            public ITestAdaptor Test { get; set; }
            public string Name => Test?.Name;
            public string FullName => Test?.FullName;
            public double Duration { get; set; }
            public string Message { get; set; }
            public string StackTrace { get; set; }
            public int AssertCount { get; set; }
            public int FailCount { get; set; }
            public int PassCount { get; set; }
            public int SkipCount { get; set; }
            public int InconclusiveCount { get; set; }
            public bool HasChildren => Children != null && ((IEnumerable<ITestResultAdaptor>)Children).GetEnumerator().MoveNext();
            public IEnumerable<ITestResultAdaptor> Children { get; set; }
            public string Output { get; set; }
            public TestStatus TestStatus { get; set; }
            public string ResultState { get; set; }
            public System.DateTime StartTime { get; set; }
            public System.DateTime EndTime { get; set; }

            public TNode ToXml() {
                return null; // Not needed for testing
            }
        }

        #endregion
    }
}

using MXR.ClaudeBridge.Models;
using NUnit.Framework;
using System;

namespace MXR.ClaudeBridge.Tests {
    /// <summary>
    /// Base test fixture for all command tests.
    /// Provides common test infrastructure including response capture and request builders.
    /// </summary>
    [TestFixture]
    public abstract class CommandTestFixture {
        protected ResponseCapture Responses;
        protected CommandRequest Request;

        [SetUp]
        public virtual void SetUp() {
            Responses = new ResponseCapture();
            Request = new CommandRequest {
                id = "test-" + Guid.NewGuid().ToString(),
                action = "test-action",
                @params = new CommandParams()
            };
        }

        [TearDown]
        public virtual void TearDown() {
            Responses = null;
            Request = null;
        }
    }
}

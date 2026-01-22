using System;
using System.Collections.Generic;

namespace MXR.ClaudeBridge.Models {
    [Serializable]
    public class CommandResponse {
        public string id;
        public string status;
        public string action;
        public long duration_ms;
        public TestResult result;
        public ProgressInfo progress;
        public List<TestFailure> failures;
        public string error;
        public List<ConsoleLogEntry> consoleLogs;

        public static CommandResponse Running(string id, string action) {
            return new CommandResponse {
                id = id,
                status = "running",
                action = action,
                progress = new ProgressInfo { current = 0, total = 0 }
            };
        }

        public static CommandResponse Success(string id, string action, long durationMs) {
            return new CommandResponse {
                id = id,
                status = "success",
                action = action,
                duration_ms = durationMs
            };
        }

        public static CommandResponse Failure(string id, string action, long durationMs, string error = null) {
            return new CommandResponse {
                id = id,
                status = "failure",
                action = action,
                duration_ms = durationMs,
                error = error
            };
        }

        public static CommandResponse Error(string id, string action, string error) {
            return new CommandResponse {
                id = id,
                status = "error",
                action = action,
                error = error
            };
        }
    }

    [Serializable]
    public class TestResult {
        public int passed;
        public int failed;
        public int skipped;
        public List<TestFailure> failures;
    }

    [Serializable]
    public class TestFailure {
        public string name;
        public string message;
    }

    [Serializable]
    public class ProgressInfo {
        public int current;
        public int total;
        public string currentTest;
    }

    [Serializable]
    public class ConsoleLogEntry {
        public string message;
        public string stackTrace;
        public string type; // "Log", "Warning", "Error"
        public int count; // For collapsed duplicates
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using MXR.ClaudeBridge.Models;
using UnityEditor;
using UnityEngine;

namespace MXR.ClaudeBridge.Commands {
    public class GetConsoleLogsCommand : ICommand {
        public void Execute(CommandRequest request, Action<CommandResponse> onProgress, Action<CommandResponse> onComplete) {
            Debug.Log("[ClaudeBridge] Getting console logs");

            // Parse limit parameter (default: 50)
            int limit = 50;
            if (request.@params != null && !string.IsNullOrEmpty(request.@params.limit)) {
                if (!int.TryParse(request.@params.limit, out limit)) {
                    limit = 50;
                }
            }

            // Parse filter parameter (optional: "Log", "Warning", "Error")
            string filter = request.@params?.filter;

            var logs = GetConsoleLogs(limit, filter);

            var response = CommandResponse.Success(request.id, request.action, 0);
            response.consoleLogs = logs;

            onComplete?.Invoke(response);
        }

        private List<ConsoleLogEntry> GetConsoleLogs(int limit, string filter) {
            var result = new List<ConsoleLogEntry>();

            try {
                // Use reflection to access Unity's internal LogEntries class
                var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor");
                if (logEntriesType == null) {
                    Debug.LogWarning("[ClaudeBridge] Could not access LogEntries type");
                    return result;
                }

                var getCountMethod = logEntriesType.GetMethod("GetCount", BindingFlags.Static | BindingFlags.Public);
                var startGettingEntriesMethod = logEntriesType.GetMethod("StartGettingEntries", BindingFlags.Static | BindingFlags.Public);
                var getEntryInternalMethod = logEntriesType.GetMethod("GetEntryInternal", BindingFlags.Static | BindingFlags.Public);
                var endGettingEntriesMethod = logEntriesType.GetMethod("EndGettingEntries", BindingFlags.Static | BindingFlags.Public);

                if (getCountMethod == null || getEntryInternalMethod == null) {
                    Debug.LogWarning("[ClaudeBridge] Could not access LogEntries methods");
                    return result;
                }

                // Get the total count of log entries
                int count = (int)getCountMethod.Invoke(null, null);
                if (count == 0) return result;

                // Start getting entries
                startGettingEntriesMethod?.Invoke(null, null);

                // Get the log entry type
                var logEntryType = Type.GetType("UnityEditor.LogEntry, UnityEditor");
                if (logEntryType == null) {
                    endGettingEntriesMethod?.Invoke(null, null);
                    return result;
                }

                // Calculate start index (get most recent logs)
                int startIndex = Math.Max(0, count - limit);
                int endIndex = count;

                // Iterate through log entries
                for (int i = startIndex; i < endIndex; i++) {
                    var logEntry = Activator.CreateInstance(logEntryType);
                    var getEntryParams = new object[] { i, logEntry };
                    var success = (bool)getEntryInternalMethod.Invoke(null, getEntryParams);

                    if (!success) continue;

                    // Extract log entry fields using reflection
                    var messageField = logEntryType.GetField("message", BindingFlags.Public | BindingFlags.Instance);
                    var fileField = logEntryType.GetField("file", BindingFlags.Public | BindingFlags.Instance);
                    var modeField = logEntryType.GetField("mode", BindingFlags.Public | BindingFlags.Instance);
                    var instanceIDField = logEntryType.GetField("instanceID", BindingFlags.Public | BindingFlags.Instance);
                    var lineField = logEntryType.GetField("line", BindingFlags.Public | BindingFlags.Instance);

                    if (messageField == null || modeField == null) continue;

                    string message = messageField.GetValue(logEntry) as string;
                    int mode = (int)modeField.GetValue(logEntry);

                    // Determine log type from mode
                    // mode: 0 = Error, 1 = Assert, 2 = Log, 4 = Fatal, 16 = Exception, 32 = Warning
                    string logType = "Log";
                    if ((mode & 0x30) != 0) { // 16 or 32
                        logType = (mode & 0x20) != 0 ? "Warning" : "Exception";
                    } else if ((mode & 0x05) != 0) { // 0, 1, or 4
                        logType = "Error";
                    }

                    // Apply filter if specified
                    if (!string.IsNullOrEmpty(filter) && !logType.Equals(filter, StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }

                    // Get stack trace if available
                    string stackTrace = "";
                    if (fileField != null && lineField != null) {
                        string file = fileField.GetValue(logEntry) as string;
                        int line = (int)lineField.GetValue(logEntry);
                        if (!string.IsNullOrEmpty(file)) {
                            stackTrace = $"{file}:{line}";
                        }
                    }

                    result.Add(new ConsoleLogEntry {
                        message = message,
                        stackTrace = stackTrace,
                        type = logType,
                        count = 1
                    });
                }

                // End getting entries
                endGettingEntriesMethod?.Invoke(null, null);
            }
            catch (Exception e) {
                Debug.LogError($"[ClaudeBridge] Error getting console logs: {e.Message}");
            }

            return result;
        }
    }
}

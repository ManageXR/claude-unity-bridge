using MXR.ClaudeBridge.Models;
using System;
using System.Collections.Generic;

namespace MXR.ClaudeBridge.Tests {
    /// <summary>
    /// Utility class to capture command callbacks (onProgress and onComplete).
    /// Used in tests to verify command behavior without relying on actual file I/O.
    /// </summary>
    public class ResponseCapture {
        public List<CommandResponse> ProgressResponses = new List<CommandResponse>();
        public CommandResponse CompleteResponse;

        /// <summary>
        /// Callback to capture progress responses.
        /// Adds each response to the ProgressResponses list.
        /// </summary>
        public Action<CommandResponse> OnProgress => (response) => {
            ProgressResponses.Add(response);
        };

        /// <summary>
        /// Callback to capture the final completion response.
        /// Stores the response in CompleteResponse.
        /// </summary>
        public Action<CommandResponse> OnComplete => (response) => {
            CompleteResponse = response;
        };

        /// <summary>
        /// Returns true if any progress responses were captured.
        /// </summary>
        public bool HasProgressResponses => ProgressResponses.Count > 0;

        /// <summary>
        /// Returns true if a completion response was captured.
        /// </summary>
        public bool HasCompleteResponse => CompleteResponse != null;
    }
}

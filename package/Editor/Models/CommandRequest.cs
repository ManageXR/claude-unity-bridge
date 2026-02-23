using System;

namespace MXR.ClaudeBridge.Models {
    [Serializable]
    public class CommandRequest {
        public string id;
        public string action;
        public CommandParams @params;
    }

    [Serializable]
    public class CommandParams {
        public string testMode;
        public string filter;
        public string limit;

        // Build command params
        public string method;       // Fully qualified static method (e.g., "MXR.Builder.BuildEntryPoints.BuildQuest")
        public string target;       // BuildTarget enum name (e.g., "Android", "StandaloneWindows64")
        public string development;  // "true"/"false" - development build flag
        public string env;          // Semicolon-separated KEY=VALUE pairs
        public string output;       // Output path override
    }
}

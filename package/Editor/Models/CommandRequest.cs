using System;

namespace MXR.ClaudeBridge.Models {
    [Serializable]
    public class CommandRequest {
        public string id;
        public string timestamp;
        public string action;
        public CommandParams @params;
    }

    [Serializable]
    public class CommandParams {
        public string testMode;
        public string filter;
        public string buildVariant;
        public string limit;
    }
}

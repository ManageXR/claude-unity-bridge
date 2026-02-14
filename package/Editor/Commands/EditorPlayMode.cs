using UnityEditor;

namespace MXR.ClaudeBridge.Commands {
    public class EditorPlayMode : IEditorPlayMode {
        public bool IsPlaying {
            get => EditorApplication.isPlaying;
            set => EditorApplication.isPlaying = value;
        }

        public bool IsPaused {
            get => EditorApplication.isPaused;
            set => EditorApplication.isPaused = value;
        }

        public void Step() {
            EditorApplication.Step();
        }

        public bool IsCompiling => EditorApplication.isCompiling;

        public bool IsUpdating => EditorApplication.isUpdating;
    }
}

namespace MXR.ClaudeBridge.Commands {
    public interface IEditorPlayMode {
        bool IsPlaying { get; set; }
        bool IsPaused { get; set; }
        void Step();
        bool IsCompiling { get; }
        bool IsUpdating { get; }
    }
}

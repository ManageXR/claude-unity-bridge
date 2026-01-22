using System;
using MXR.ClaudeBridge.Models;

namespace MXR.ClaudeBridge.Commands {
    public interface ICommand {
        void Execute(CommandRequest request, Action<CommandResponse> onProgress, Action<CommandResponse> onComplete);
    }
}

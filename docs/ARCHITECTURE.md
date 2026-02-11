# Architecture

## File Structure

```
package/                     # Unity package (UPM)
├── Editor/
│   ├── ClaudeBridge.cs          # Main coordinator
│   ├── ClaudeBridge.asmdef      # Assembly definition
│   ├── Commands/
│   │   ├── ICommand.cs          # Command interface
│   │   ├── RunTestsCommand.cs   # Test execution
│   │   ├── CompileCommand.cs    # Script compilation
│   │   ├── RefreshCommand.cs    # Asset refresh
│   │   ├── GetStatusCommand.cs  # Editor status
│   │   └── GetConsoleLogsCommand.cs  # Console logs
│   └── Models/
│       ├── CommandRequest.cs    # Request DTO
│       └── CommandResponse.cs   # Response DTO
└── Tests/                   # Unity tests

skill/                       # Claude Code skill (Python)
├── scripts/
│   └── cli.py               # Deterministic command script
├── references/
│   ├── COMMANDS.md          # Complete command specification
│   └── EXTENDING.md         # Guide for adding custom commands
└── SKILL.md                 # Skill documentation
```

## Command Pattern

All commands implement `ICommand`:

```csharp
public interface ICommand {
    void Execute(
        CommandRequest request,
        Action<CommandResponse> onProgress,
        Action<CommandResponse> onComplete
    );
}
```

This enables:
- Progress reporting during execution
- Consistent error handling
- Easy extension with new commands

## Adding New Commands

See [skill/references/EXTENDING.md](../skill/references/EXTENDING.md) for a complete guide on adding custom commands.

# Claude Unity Bridge

![CI](https://github.com/ManageXR/claude-unity-bridge/actions/workflows/test-skill.yml/badge.svg)
[![codecov](https://codecov.io/gh/ManageXR/claude-unity-bridge/graph/badge.svg?token=3PHF2GXHON)](https://codecov.io/gh/ManageXR/claude-unity-bridge)
![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)
![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-black.svg)

File-based bridge enabling Claude Code to trigger Unity Editor operations in a running editor instance.

## Features

- **Run Tests** - Execute EditMode or PlayMode tests
- **Compile** - Trigger script compilation
- **Refresh** - Force asset database refresh
- **Get Status** - Check editor compilation/update state
- **Get Console Logs** - Retrieve Unity console output

## Installation

### Via Package Manager (Git URL)

1. Open Unity Editor
2. Go to `Window > Package Manager`
3. Click the `+` button in the top-left corner
4. Select `Add package from git URL...`
5. Enter: `https://github.com/ManageXR/claude-unity-bridge.git?path=package`
6. Click `Add`

### Via Package Manager (Local Path)

For development or testing:

1. Clone this repository to your local machine
2. Open Unity Editor
3. Go to `Window > Package Manager`
4. Click the `+` button in the top-left corner
5. Select `Add package from disk...`
6. Navigate to the `package/` folder and select `package.json`
7. Click `Open`

### Via manifest.json

Add this line to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.managexr.claude-bridge": "https://github.com/ManageXR/claude-unity-bridge.git?path=package"
  }
}
```

## How It Works

ClaudeBridge uses a file-based protocol for communication:

1. **Command File**: Claude Code writes commands to `.claude/unity/command.json`
2. **Processing**: Unity Editor polls for commands and executes them
3. **Response File**: Results are written to `.claude/unity/response-{id}.json`

This approach enables multi-project support - each Unity project has its own `.claude/unity/` directory, allowing multiple agents to work on different projects simultaneously.

## Usage

### Run Tests

```json
{
  "id": "test-001",
  "action": "run-tests",
  "params": {
    "testMode": "EditMode",
    "filter": "MXR.Tests"
  }
}
```

**Parameters:**
- `testMode` - `"EditMode"` or `"PlayMode"`
- `filter` (optional) - Test filter pattern

### Compile Scripts

```json
{
  "id": "compile-001",
  "action": "compile",
  "params": {}
}
```

### Refresh Assets

```json
{
  "id": "refresh-001",
  "action": "refresh",
  "params": {}
}
```

### Get Editor Status

```json
{
  "id": "status-001",
  "action": "get-status",
  "params": {}
}
```

### Get Console Logs

```json
{
  "id": "logs-001",
  "action": "get-console-logs",
  "params": {
    "limit": 20,
    "filter": "Error"
  }
}
```

**Parameters:**
- `limit` (optional) - Maximum number of logs to retrieve (default: 50)
- `filter` (optional) - Filter by log type: `"Log"`, `"Warning"`, or `"Error"`

## Response Format

All commands return a response in this format:

```json
{
  "id": "test-001",
  "status": "success",
  "action": "run-tests",
  "duration_ms": 1250,
  "result": {
    "passed": 410,
    "failed": 0,
    "skipped": 0
  }
}
```

**Status Values:**
- `running` - Command in progress
- `success` - Command completed successfully
- `failure` - Command completed with failures
- `error` - Command execution error

## Tools Menu

The package adds menu items under `Tools > Claude Bridge`:

- **Show Status** - Display current bridge status in console
- **Cleanup Old Responses** - Delete response files older than 1 hour

## Claude Code Skill

For a better user experience, install the companion Claude Code skill that provides:

- **Natural Language Interface** - Ask Claude to "run Unity tests" instead of writing JSON
- **Rock-Solid Execution** - Deterministic Python script handles all edge cases
- **Beautiful Formatting** - Human-readable output with file paths and error details
- **Automatic Cleanup** - Manages response files automatically

### Installation

**Option 1: Using GNU Stow (recommended)**

If you have [GNU Stow](https://www.gnu.org/software/stow/) installed:

```bash
cd /path/to/claude-unity-bridge
stow -t ~/.codex/skills/unity -d . skill
```

To uninstall:
```bash
stow -t ~/.codex/skills/unity -d . -D skill
```

**Option 2: Using symlink**

```bash
ln -s "$(pwd)/skill" ~/.codex/skills/unity
```

**Option 3: Copy (not recommended)**

```bash
cp -r skill ~/.codex/skills/unity
```

Both Stow and symlink keep the skill updated automatically when you pull changes from git. Then restart Claude Code. The skill will auto-activate when you're working in a Unity project directory.

### Usage Examples

Once installed, simply ask Claude Code naturally:

- "Run the Unity tests in EditMode"
- "Check if there are any compilation errors"
- "Show me the last 10 error logs from Unity"
- "Refresh the Unity asset database"

Claude will automatically use the skill to execute commands and format the results.

### Documentation

See `skill/SKILL.md` for complete documentation:
- Command usage and examples
- Error handling and troubleshooting
- Advanced options (timeouts, cleanup, verbose mode)
- Integration patterns for CI/CD

See `skill/references/` for detailed references:
- `COMMANDS.md` - Complete command specification
- `EXTENDING.md` - Guide for adding custom commands

## Multi-Project Support

Each Unity project maintains its own command/response directory at `.claude/unity/`. This allows Claude Code to manage multiple Unity projects simultaneously without port conflicts or configuration changes.

## Architecture

### File Structure

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
│   └── unity_command.py     # Deterministic command script
└── SKILL.md                 # Skill documentation
```

### Command Pattern

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

## Requirements

- Unity 2021.3 or later
- No additional dependencies

## License

Apache 2.0

## Support

For issues or questions, visit: https://github.com/ManageXR/claude-unity-bridge/issues

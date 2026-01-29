---
name: unity
description: Execute Unity Editor commands (run tests, compile, get logs, refresh assets) via file-based bridge. Auto-activates for Unity-related tasks. Requires com.managexr.claude-bridge package installed in Unity project.
---

# Unity Bridge Skill

Control Unity Editor operations from Claude Code using a reliable file-based communication protocol.

## Overview

The Unity Bridge enables Claude Code to trigger operations in a running Unity Editor instance without network configuration or port conflicts. It uses a simple file-based protocol where commands are written to `.claude/unity/command.json` and responses are read from `.claude/unity/response-{id}.json`.

**Key Features:**
- Execute EditMode and PlayMode tests
- Trigger script compilation
- Refresh asset database
- Check editor status (compilation, play mode, etc.)
- Retrieve Unity console logs

**Multi-Project Support:** Each Unity project has its own `.claude/unity/` directory, allowing multiple projects to be worked on simultaneously.

## Requirements

1. **Unity Package:** Install `com.managexr.claude-bridge` in your Unity project
   - Via Package Manager: `https://github.com/ManageXR/claude-unity-bridge.git`
   - See main package README for installation instructions

2. **Unity Editor:** Must be open with your project loaded

3. **Python 3:** The skill uses a Python script for reliable command execution

## How It Works

The skill uses a Python helper script (`scripts/unity_command.py`) that handles:
- UUID generation for command tracking
- Atomic file writes to prevent corruption
- Exponential backoff polling for responses
- File locking handling
- Automatic cleanup of old response files
- Formatted, human-readable output

This approach ensures **deterministic, rock-solid execution** - the script is tested once and behaves identically every time, handling all edge cases (timeouts, file locking, malformed responses, etc.) without requiring Claude to manage these details in-context.

## Usage

### Basic Pattern

When you need to interact with Unity, use the Python script directly:

```bash
python scripts/unity_command.py [command] [options]
```

All commands automatically:
- Generate a unique UUID for tracking
- Write the command atomically
- Poll for response with timeout
- Format output for readability
- Cleanup response files

### Command Examples

#### Run Tests

Execute Unity tests in EditMode or PlayMode:

```bash
# Run all EditMode tests
python scripts/unity_command.py run-tests --mode EditMode

# Run tests with filter
python scripts/unity_command.py run-tests --mode EditMode --filter "MXR.Tests"

# Run all tests (both modes)
python scripts/unity_command.py run-tests
```

**Output:**
```
✓ Tests Passed: 410
✗ Tests Failed: 2
○ Tests Skipped: 0
Duration: 1.25s

Failed Tests:
  - MXR.Tests.AuthTests.LoginWithInvalidCredentials
    Expected: success, Actual: failure
  - MXR.Tests.NetworkTests.TimeoutHandling
    NullReferenceException: Object reference not set
```

**Parameters:**
- `--mode` - `EditMode` or `PlayMode` (optional, defaults to both)
- `--filter` - Test name filter pattern (optional)
- `--timeout` - Override default 30s timeout

#### Compile Scripts

Trigger Unity script compilation:

```bash
python scripts/unity_command.py compile
```

**Output (Success):**
```
✓ Compilation Successful
Duration: 2.3s
```

**Output (Failure):**
```
✗ Compilation Failed

Assets/Scripts/Player.cs:25: error CS0103: The name 'invalidVar' does not exist
Assets/Scripts/Enemy.cs:67: error CS0246: Type 'MissingClass' could not be found
```

#### Get Console Logs

Retrieve Unity console output:

```bash
# Get last 20 logs
python scripts/unity_command.py get-console-logs --limit 20

# Get only errors
python scripts/unity_command.py get-console-logs --limit 10 --filter Error

# Get warnings
python scripts/unity_command.py get-console-logs --filter Warning
```

**Output:**
```
Console Logs (last 10, filtered by Error):

[Error] NullReferenceException: Object reference not set
  at Player.Update() in Assets/Scripts/Player.cs:34

[Error] Failed to load asset: missing_texture.png

[Error] (x3) Shader compilation failed
  See Console for details
```

**Parameters:**
- `--limit` - Maximum number of logs (default: 50)
- `--filter` - Filter by type: `Log`, `Warning`, or `Error`

#### Get Editor Status

Check Unity Editor state:

```bash
python scripts/unity_command.py get-status
```

**Output:**
```
Unity Editor Status:
  - Compilation: ✓ Ready
  - Play Mode: ✏ Editing
  - Updating: No
```

**Possible States:**
- Compilation: `✓ Ready` or `⏳ Compiling...`
- Play Mode: `✏ Editing`, `▶ Playing`, or `⏸ Paused`
- Updating: `Yes` or `No`

#### Refresh Asset Database

Force Unity to refresh assets:

```bash
python scripts/unity_command.py refresh
```

**Output:**
```
✓ Asset Database Refreshed
Duration: 0.5s
```

### Advanced Options

#### Timeout Configuration

Override the default 30-second timeout:

```bash
python scripts/unity_command.py run-tests --timeout 60
```

Use longer timeouts for:
- Large test suites
- PlayMode tests (which start/stop Play Mode)
- Full project compilation

#### Cleanup Old Responses

Automatically remove old response files before executing:

```bash
python scripts/unity_command.py compile --cleanup
```

This removes response files older than 1 hour. Useful for maintaining a clean workspace.

#### Verbose Output

See detailed execution progress:

```bash
python scripts/unity_command.py run-tests --verbose
```

Prints:
- Command ID
- Polling attempts
- Response file detection
- Cleanup operations

### Error Handling

The script provides clear error messages for common issues:

**Unity Not Running:**
```
Error: Unity Editor not detected. Ensure Unity is open with the project loaded.
```

**Command Timeout:**
```
Error: Command timed out after 30s. Check Unity Console for errors.
```

**Invalid Parameters:**
```
Error: Failed to write command file: Invalid mode 'InvalidMode'
```

**Exit Codes:**
- `0` - Success
- `1` - Error (Unity not running, invalid params, etc.)
- `2` - Timeout

## Integration with Claude Code

When you're working in a Unity project directory, you can ask Claude Code to perform Unity operations naturally:

- "Run the Unity tests in EditMode"
- "Check if there are any compilation errors"
- "Show me the last 10 error logs from Unity"
- "Refresh the Unity asset database"

Claude Code will automatically use this skill to execute the commands via the Python script.

## File Protocol Details

### Command Format

Written to `.claude/unity/command.json`:

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "action": "run-tests",
  "params": {
    "testMode": "EditMode",
    "filter": "MyTests"
  }
}
```

### Response Format

Read from `.claude/unity/response-{id}.json`:

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "success",
  "action": "run-tests",
  "duration_ms": 1250,
  "result": {
    "passed": 410,
    "failed": 0,
    "skipped": 0,
    "failures": []
  }
}
```

**Status Values:**
- `running` - Command in progress (may see intermediate responses)
- `success` - Command completed successfully
- `failure` - Command completed with failures (e.g., failed tests)
- `error` - Command execution error

## Project Structure

```
skill/
├── SKILL.md                    # This file
├── scripts/
│   └── unity_command.py        # Command execution script
└── references/
    ├── COMMANDS.md             # Detailed command specifications
    └── EXTENDING.md            # Guide for adding custom commands
```

## Detailed Documentation

For more information, see:

- **[COMMANDS.md](references/COMMANDS.md)** - Complete command reference with all parameters, response formats, and edge cases
- **[EXTENDING.md](references/EXTENDING.md)** - Tutorial for adding custom commands to the Unity Bridge for project-specific workflows

## Troubleshooting

### Unity Not Responding

**Symptoms:** Commands timeout or "Unity not detected" error

**Solutions:**
1. Ensure Unity Editor is open with the project loaded
2. Check that the package is installed (`Window > Package Manager`)
3. Verify `.claude/unity/` directory exists in project root
4. Check Unity Console for errors from ClaudeBridge package

### Response File Issues

**Symptoms:** "Failed to parse response JSON" error

**Solutions:**
1. Check Unity Console for ClaudeBridge errors
2. Manually inspect `.claude/unity/response-*.json` files
3. Try cleaning up old responses with `--cleanup` flag
4. Restart Unity Editor if file system is in bad state

### Performance Issues

**Symptoms:** Slow response times, frequent timeouts

**Solutions:**
1. Increase timeout with `--timeout 60` or higher
2. Close unnecessary Unity Editor windows
3. Reduce test scope with `--filter` parameter
4. Check system resources (CPU, memory)

### File Locking Errors

**Symptoms:** Intermittent errors reading/writing files

**Solutions:**
1. The script handles file locking automatically with retries
2. If persistent, check for antivirus interference
3. Verify file permissions on `.claude/unity/` directory

## Installation

### For This Project (During Development)

The skill is already in this repository under `skill/`. No installation needed for development.

### For Claude Code Users

To use this skill in Claude Code:

1. Copy the entire `skill/` directory to your Claude Code skills location:
   ```bash
   cp -r skill ~/.claude/skills/unity
   ```

2. Restart Claude Code to load the skill

3. Install the Unity package in your Unity project:
   - Open Unity Editor
   - Go to `Window > Package Manager`
   - Click `+` > `Add package from git URL...`
   - Enter: `https://github.com/ManageXR/claude-unity-bridge.git`

4. Navigate to your Unity project directory in Claude Code

5. Ask Claude to perform Unity operations naturally

## Why Python Script?

The skill uses a Python script instead of implementing the protocol directly in Claude Code prompts for several critical reasons:

**Consistency:** UUID generation, polling logic, and error handling work identically every time. Without the script, Claude might implement these differently across sessions, leading to subtle bugs.

**Reliability:** All edge cases are handled once in tested code:
- File locking when Unity writes responses
- Exponential backoff for polling
- Atomic command writes to prevent corruption
- Graceful handling of malformed JSON
- Proper cleanup of stale files

**Error Messages:** Clear, actionable error messages for all failure modes. Claude doesn't have to figure out what went wrong each time.

**Token Efficiency:** The script handles complexity, so Claude doesn't need to manage low-level details in-context. The SKILL.md stays concise while providing full functionality.

**Deterministic Exit Codes:** Shell integration works reliably with standard exit codes (0=success, 1=error, 2=timeout).

**Rock Solid:** Test the script once, it works forever. No variability between Claude sessions.

## License

MIT - See main package repository for details.

## Support

For issues or questions:
- Package Issues: https://github.com/ManageXR/claude-unity-bridge/issues
- Skill Issues: Report in the same repository with `[Skill]` prefix

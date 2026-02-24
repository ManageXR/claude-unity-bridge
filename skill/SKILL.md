---
name: unity
description: Execute Unity Editor commands (run tests, compile, get logs, refresh assets, play/pause/step) via file-based bridge. Auto-activates for Unity-related tasks. Requires com.mxr.claude-bridge package installed in Unity project.
---

# Unity Bridge Skill

Control Unity Editor operations from Claude Code using a reliable file-based communication protocol.

## Overview

The Unity Bridge enables Claude Code to trigger operations in a running Unity Editor instance without network configuration or port conflicts. It uses a simple file-based protocol where commands are written to `.unity-bridge/command.json` and responses are read from `.unity-bridge/response-{id}.json`.

**Key Features:**
- Execute EditMode and PlayMode tests
- Trigger script compilation
- Refresh asset database
- Check editor status (compilation, play mode, etc.)
- Retrieve Unity console logs
- Control Play Mode (play, pause, step)
- Build projects (direct or custom pipeline)

**Multi-Project Support:** Each Unity project has its own `.unity-bridge/` directory, allowing multiple projects to be worked on simultaneously.

## Requirements

1. **Unity Package:** Install `com.mxr.claude-bridge` in your Unity project
   - Via Package Manager: `https://github.com/ManageXR/claude-unity-bridge.git`
   - See main package README for installation instructions

2. **Unity Editor:** Must be open with your project loaded

3. **Python 3:** The skill uses a Python script for reliable command execution

## How It Works

The skill uses a CLI tool (`unity-bridge`) that handles:
- UUID generation for command tracking
- Atomic file writes to prevent corruption
- Exponential backoff polling for responses
- File locking handling
- Automatic cleanup of old response files
- Formatted, human-readable output

This approach ensures **deterministic, rock-solid execution** - the script is tested once and behaves identically every time, handling all edge cases (timeouts, file locking, malformed responses, etc.) without requiring Claude to manage these details in-context.

## Usage

### Basic Pattern

When you need to interact with Unity, use the CLI directly:

```bash
unity-bridge [command] [options]
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
unity-bridge run-tests --mode EditMode

# Run tests with filter
unity-bridge run-tests --mode EditMode --filter "MXR.Tests"

# Run all tests (both modes)
unity-bridge run-tests
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
unity-bridge compile
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
unity-bridge get-console-logs --limit 20

# Get only errors
unity-bridge get-console-logs --limit 10 --filter Error

# Get warnings
unity-bridge get-console-logs --filter Warning
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
unity-bridge get-status
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
unity-bridge refresh
```

**Output:**
```
✓ Asset Database Refreshed
Duration: 0.5s
```

#### Play Mode Control

Toggle Play Mode, pause, and step through frames:

```bash
# Enter/exit Play Mode (toggle)
unity-bridge play

# Pause/unpause (while in Play Mode)
unity-bridge pause

# Step one frame (while in Play Mode)
unity-bridge step
```

**Output (play):**
```
✓ play completed
Play Mode: ▶ Playing
Duration: 0.01s
```

**Output (pause):**
```
✓ pause completed
Play Mode: ⏸ Paused
Duration: 0.01s
```

**Output (step):**
```
✓ step completed
Play Mode: ⏸ Paused
Duration: 0.02s
```

**Notes:**
- `play` toggles Play Mode on/off (like the Play button in Unity)
- `pause` and `step` require Play Mode to be active; returns error if not playing
- All three return the resulting `editorStatus` so the caller knows the current state

#### Build Project

Build the Unity project using either direct `BuildPipeline.BuildPlayer()` or a custom build method:

```bash
# Direct build with current active target
unity-bridge build

# Direct build for specific target
unity-bridge build --target Android --development

# Custom build pipeline via static method invocation
unity-bridge build --method MXR.Builder.BuildEntryPoints.BuildQuest

# With environment variables
unity-bridge build --method MXR.Builder.BuildEntryPoints.BuildQuest --env BUILD_TYPE=production --env SCRIPTING_BACKEND=il2cpp

# Using a named build profile (from .unity-bridge/build.json)
unity-bridge build --profile quest
```

**Output (Success):**
```
✓ Build Succeeded
Errors: 0
Warnings: 3
Build Time: 45.2s
Output: /path/to/Build_Android.apk
Size: 50.0 MB
Duration: 45.50s
```

**Output (Failure):**
```
✗ Build Failed
Errors: 5
Warnings: 2
Build Time: 30.0s
Duration: 30.50s

Build Failed: 5 error(s), 2 warning(s)
```

**Parameters:**
- `--method` - Fully qualified static method (e.g., `MXR.Builder.BuildEntryPoints.BuildQuest`)
- `--target` - BuildTarget enum name (e.g., `Android`, `StandaloneWindows64`, `iOS`)
- `--development` - Enable development build flag
- `--env` - Environment variable `KEY=VALUE` (repeatable)
- `--profile` - Named profile from `.unity-bridge/build.json`
- `--output` - Override output path
- `--timeout` - Override default 300s timeout

**Build Profiles:**

For projects with custom build pipelines, create `.unity-bridge/build.json` to define named profiles:

```json
{
  "profiles": {
    "quest": {
      "method": "MXR.Builder.BuildEntryPoints.BuildQuest",
      "env": { "BUILD_TYPE": "development" },
      "timeout": 600
    },
    "pico": {
      "method": "MXR.Builder.BuildEntryPoints.BuildPico"
    }
  },
  "default": "quest"
}
```

Claude can help create this file by scanning your project for build entry points. Ask: "Set up build profiles for my project."

**Notes:**
- Default timeout is 5 minutes (300s) — builds are long-running operations
- The bridge sets `UNITY_BRIDGE_BUILD=true` env var before method invocation, so build code can detect bridge context and skip `EditorApplication.Exit()` calls
- Direct builds use the currently active build target if `--target` is not specified
- Profile settings are defaults; CLI arguments override them

### Advanced Options

#### Timeout Configuration

Override the default 30-second timeout:

```bash
unity-bridge run-tests --timeout 60
```

Use longer timeouts for:
- Large test suites
- PlayMode tests (which start/stop Play Mode)
- Full project compilation

#### Cleanup Old Responses

Automatically remove old response files before executing:

```bash
unity-bridge compile --cleanup
```

This removes response files older than 1 hour. Useful for maintaining a clean workspace.

#### Verbose Output

See detailed execution progress:

```bash
unity-bridge run-tests --verbose
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
- "Enter Play Mode"
- "Pause the editor"
- "Step one frame"
- "Build for Android"
- "Run the Quest build"
- "Set up build profiles for my project"

Claude Code will automatically use this skill to execute the commands via the Python script.

## File Protocol Details

### Command Format

Written to `.unity-bridge/command.json`:

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

Read from `.unity-bridge/response-{id}.json`:

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
├── pyproject.toml              # Package configuration
├── src/
│   └── claude_unity_bridge/
│       ├── __init__.py         # Package version
│       └── cli.py              # CLI implementation
├── tests/
│   └── test_cli.py             # Unit tests
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
3. Verify `.unity-bridge/` directory exists in project root
4. Check Unity Console for errors from ClaudeBridge package

### Response File Issues

**Symptoms:** "Failed to parse response JSON" error

**Solutions:**
1. Check Unity Console for ClaudeBridge errors
2. Manually inspect `.unity-bridge/response-*.json` files
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
1. The CLI handles file locking automatically with retries
2. If persistent, check for antivirus interference
3. Verify file permissions on `.unity-bridge/` directory

## Installation

### Quick Install

```bash
pip install claude-unity-bridge
unity-bridge install-skill
```

This installs the CLI and the Claude Code skill.

### Verify Setup

```bash
unity-bridge health-check
```

### Updating

```bash
unity-bridge update
```

This upgrades the pip package and reinstalls the skill.

### Uninstalling

```bash
unity-bridge uninstall-skill
pip uninstall claude-unity-bridge
```

### Development Installation

```bash
cd claude-unity-bridge/skill
pip install -e ".[dev]"
unity-bridge install-skill
```

## Why a CLI Tool?

The skill uses a CLI tool instead of implementing the protocol directly in Claude Code prompts for several critical reasons:

**Consistency:** UUID generation, polling logic, and error handling work identically every time. Without the CLI, Claude might implement these differently across sessions, leading to subtle bugs.

**Reliability:** All edge cases are handled once in tested code:
- File locking when Unity writes responses
- Exponential backoff for polling
- Atomic command writes to prevent corruption
- Graceful handling of malformed JSON
- Proper cleanup of stale files

**Error Messages:** Clear, actionable error messages for all failure modes. Claude doesn't have to figure out what went wrong each time.

**Token Efficiency:** The CLI handles complexity, so Claude doesn't need to manage low-level details in-context. The SKILL.md stays concise while providing full functionality.

**Deterministic Exit Codes:** Shell integration works reliably with standard exit codes (0=success, 1=error, 2=timeout).

**Rock Solid:** Test the CLI once, it works forever. No variability between Claude sessions.

## Support

For issues or questions:
- Package Issues: https://github.com/ManageXR/claude-unity-bridge/issues
- Skill Issues: Report in the same repository with `[Skill]` prefix

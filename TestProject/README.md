# Unity Bridge - Integration Test Project

This is a minimal Unity 2021.3+ project used for end-to-end integration testing of the Claude Unity Bridge package.

## Purpose

The `TestProject/` directory enables:

1. **Package Development** - Test Unity Bridge changes in a real Unity project
2. **End-to-End Validation** - Verify the bridge protocol works correctly
3. **Agent Testing** - Allow AI agents to validate their Unity code changes
4. **Integration Testing** - Test Python skill script against actual Unity Editor

## Structure

```
TestProject/
├── Assets/               # Unity assets (gitignored, created by Unity)
├── Packages/             # Package dependencies
│   └── manifest.json    # References parent directory as local package
├── ProjectSettings/      # Unity project settings (committed to git)
├── .claude/             # Bridge protocol directory (gitignored, created at runtime)
│   └── unity/           # Command/response JSON files
├── .gitignore           # Ignores everything except ProjectSettings & README
└── README.md            # This file
```

## Package Reference

The project references the Claude Unity Bridge package via a **local package reference**:

```json
{
  "dependencies": {
    "com.mxr.claude-bridge": "file:.."
  }
}
```

This means:
- Changes to the package in `../Editor/` are immediately reflected in this project
- No need to reinstall or update the package
- Perfect for development and testing

## Usage

### 1. Open in Unity Editor

```bash
# From repository root
open -a "Unity" TestProject/
```

Or open via Unity Hub by adding `TestProject/` as a project.

**Important**: You must have Unity 2021.3 or later installed.

### 2. Verify Package Loaded

Once Unity Editor opens:

1. Check Window > Package Manager
2. Find "Claude Unity Bridge" in the package list
3. Verify it shows as a "Local" package
4. Check for any console errors related to package loading

### 3. Test the Bridge Protocol

With Unity Editor open and the TestProject loaded, test the Python skill script:

```bash
# From repository root
cd skill
python3 scripts/unity_command.py get-status
```

**Expected output:**
```
✓ Unity Editor is running
  Version: 2021.3.x
  Project: TestProject
  Play Mode: Inactive
  Compiling: No
  Platform: StandaloneOSX
```

### 4. Run More Commands

Try other commands to validate full functionality:

```bash
# Compile scripts
python3 scripts/unity_command.py compile

# Refresh asset database
python3 scripts/unity_command.py refresh

# Get console logs
python3 scripts/unity_command.py get-console-logs --limit 10

# Run tests (if any test assemblies exist)
python3 scripts/unity_command.py run-tests --mode EditMode
```

## Troubleshooting

### "Unity Editor not detected"

**Cause**: The `.claude/unity/` directory doesn't exist yet.

**Fix**:
1. Ensure Unity Editor is open with TestProject loaded
2. The package should create `.claude/unity/` automatically on startup
3. Check Unity Console for any `[ClaudeBridge]` errors
4. Try reopening the project if the directory isn't created

### Package Not Showing in Package Manager

**Cause**: The local package reference may not be resolving correctly.

**Fix**:
1. Check `Packages/manifest.json` contains `"com.mxr.claude-bridge": "file:.."`
2. Verify the parent directory contains a valid `package.json`
3. Try Window > Package Manager > Refresh
4. Check Unity Console for package resolution errors

### "Command timed out after 30s"

**Cause**: Unity Bridge isn't polling for commands.

**Fix**:
1. Check Unity Console for `[ClaudeBridge]` initialization messages
2. Verify no errors during package initialization
3. Try restarting Unity Editor
4. Check if `.claude/unity/` directory exists and is writable

## For AI Agents

When working on the Claude Unity Bridge package:

1. **Before Testing Changes**: Open TestProject in Unity Editor
2. **Make Changes**: Edit files in `../Editor/` (changes reflect immediately)
3. **Validate**: Run Python commands from `skill/` directory
4. **Check Logs**: Monitor Unity Console for `[ClaudeBridge]` messages
5. **Clean Up**: Unity will clean up old response files automatically

### Typical Workflow

```bash
# 1. Open Unity with TestProject (in separate terminal or GUI)
open -a "Unity" TestProject/

# 2. Make changes to package C# code
# Edit files in Editor/Commands/, Editor/ClaudeBridge.cs, etc.

# 3. Test changes via Python skill script
cd skill
python3 scripts/unity_command.py get-status
python3 scripts/unity_command.py compile

# 4. Run pytest tests for Python script
pytest tests/test_unity_command.py -v

# 5. Commit when all tests pass
```

## What Gets Committed

Only these files are tracked in git (see `.gitignore`):

- `ProjectSettings/` - Unity project settings (except ProjectSettings.asset)
- `Packages/manifest.json` - Package dependencies including local reference
- `.gitignore` - Git ignore rules
- `README.md` - This file

Everything else (Assets/, Library/, Temp/, Logs/, .claude/) is gitignored.

## Adding Test Assets (Optional)

If you need to add test scenes, scripts, or assets for testing:

1. Create them in Unity Editor as normal
2. Update `.gitignore` to include the specific test assets
3. Commit only the assets needed for automated testing

For example, to add a test scene:

```gitignore
# In TestProject/.gitignore, add:
!Assets/
!Assets/Scenes/
!Assets/Scenes/TestScene.unity
!Assets/Scenes/TestScene.unity.meta
```

## Unity Version

This project targets **Unity 2021.3** (the minimum version supported by the package).

To use a different Unity version:
1. Open the project in your Unity version
2. Unity will automatically upgrade `ProjectSettings/ProjectVersion.txt`
3. Commit the updated version file if intentional

## Related Documentation

- **[../AGENTS.md](../AGENTS.md)** - Complete agent guidelines for development
- **[../README.md](../README.md)** - Main package documentation
- **[../skill/SKILL.md](../skill/SKILL.md)** - Python skill documentation
- **[../skill/references/COMMANDS.md](../skill/references/COMMANDS.md)** - Command reference

## Quick Reference

```bash
# Open project
open -a "Unity" TestProject/

# Test bridge
cd skill && python3 scripts/unity_command.py get-status

# Run all commands
python3 scripts/unity_command.py --help

# Close Unity
# Use Cmd+Q or Unity > Quit
```

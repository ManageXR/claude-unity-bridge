# Unity Bridge Command Reference

Complete specification for all Unity Bridge commands, including parameters, response formats, and error scenarios.

## Table of Contents

- [run-tests](#run-tests) - Execute Unity tests
- [compile](#compile) - Trigger script compilation
- [refresh](#refresh) - Refresh asset database
- [get-status](#get-status) - Get editor status
- [get-console-logs](#get-console-logs) - Retrieve console logs

---

## run-tests

Execute Unity tests in EditMode or PlayMode.

### Usage

```bash
python scripts/unity_command.py run-tests [options]
```

### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `--mode` | string | No | Both modes | Test mode: `EditMode` or `PlayMode` |
| `--filter` | string | No | None | Test name filter pattern (semicolon-separated) |
| `--timeout` | int | No | 30 | Command timeout in seconds |

### Parameter Details

**`--mode`**
- `EditMode`: Runs tests that don't require entering Play Mode (fast, for logic tests)
- `PlayMode`: Runs tests in Play Mode (slower, for integration tests)
- Omit to run both modes

**`--filter`**
- Filter by test name or namespace
- Examples:
  - `"MyTests"` - All tests containing "MyTests"
  - `"MXR.Tests.Auth"` - All tests in the Auth namespace
  - `"LoginTest;LogoutTest"` - Multiple filters (semicolon-separated)
- Case-sensitive
- Matches test names using Unity's test filter syntax

### Response Format

**Success (all tests passed):**
```json
{
  "id": "uuid",
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

**Failure (some tests failed):**
```json
{
  "id": "uuid",
  "status": "failure",
  "action": "run-tests",
  "duration_ms": 3500,
  "result": {
    "passed": 408,
    "failed": 2,
    "skipped": 0,
    "failures": [
      {
        "name": "MXR.Tests.AuthTests.LoginWithInvalidCredentials",
        "message": "Expected: success\nActual: failure\nat Assets/Tests/AuthTests.cs:45"
      },
      {
        "name": "MXR.Tests.NetworkTests.TimeoutHandling",
        "message": "NullReferenceException: Object reference not set\nat Assets/Tests/NetworkTests.cs:123"
      }
    ]
  }
}
```

**Progress Updates:**

During execution, you may see intermediate `status: "running"` responses with progress information:

```json
{
  "id": "uuid",
  "status": "running",
  "action": "run-tests",
  "progress": {
    "current": 150,
    "total": 410,
    "currentTest": "MXR.Tests.Player.MovementTest"
  },
  "failures": []
}
```

### Formatted Output

The Python script formats the output for readability:

```
✓ Tests Passed: 408
✗ Tests Failed: 2
○ Tests Skipped: 0
Duration: 3.50s

Failed Tests:
  - MXR.Tests.AuthTests.LoginWithInvalidCredentials
    Expected: success
    Actual: failure
    at Assets/Tests/AuthTests.cs:45
  - MXR.Tests.NetworkTests.TimeoutHandling
    NullReferenceException: Object reference not set
    at Assets/Tests/NetworkTests.cs:123
```

### Error Scenarios

**No Tests Found:**
```json
{
  "id": "uuid",
  "status": "success",
  "action": "run-tests",
  "duration_ms": 100,
  "result": {
    "passed": 0,
    "failed": 0,
    "skipped": 0,
    "failures": []
  }
}
```

**Compilation Error (tests can't run):**
```json
{
  "id": "uuid",
  "status": "error",
  "action": "run-tests",
  "error": "Cannot run tests while scripts are compiling"
}
```

**Invalid Test Mode:**
```json
{
  "id": "uuid",
  "status": "error",
  "action": "run-tests",
  "error": "Invalid test mode: 'InvalidMode'. Use 'EditMode' or 'PlayMode'."
}
```

### Notes

- **PlayMode Tests:** May take longer as they require starting/stopping Play Mode
- **Test Timeout:** Use `--timeout` to extend for large test suites
- **Filter Syntax:** Follows Unity's test runner filter syntax (namespace or test name matching)
- **Progress Updates:** Unity may write multiple response files with progress; script handles this automatically
- **Skipped Tests:** Tests marked with `[Ignore]` attribute or platform-specific tests

### Examples

```bash
# Run all EditMode tests (fast)
python scripts/unity_command.py run-tests --mode EditMode

# Run specific test suite
python scripts/unity_command.py run-tests --filter "MXR.Tests.Auth"

# Run multiple test suites
python scripts/unity_command.py run-tests --filter "AuthTests;NetworkTests"

# Run PlayMode tests with extended timeout
python scripts/unity_command.py run-tests --mode PlayMode --timeout 60

# Run all tests
python scripts/unity_command.py run-tests
```

---

## compile

Trigger Unity script compilation and wait for completion.

### Usage

```bash
python scripts/unity_command.py compile [options]
```

### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `--timeout` | int | No | 30 | Command timeout in seconds |
| `--cleanup` | flag | No | False | Cleanup old response files first |

### Response Format

**Success:**
```json
{
  "id": "uuid",
  "status": "success",
  "action": "compile",
  "duration_ms": 2300
}
```

**Failure:**
```json
{
  "id": "uuid",
  "status": "failure",
  "action": "compile",
  "duration_ms": 1800,
  "error": "Compilation failed with 3 errors:\nAssets/Scripts/Player.cs(25,10): error CS0103: The name 'invalidVar' does not exist in the current context\nAssets/Scripts/Enemy.cs(67,5): error CS0246: The type or namespace name 'MissingClass' could not be found"
}
```

### Formatted Output

**Success:**
```
✓ Compilation Successful
Duration: 2.30s
```

**Failure:**
```
✗ Compilation Failed

Compilation failed with 3 errors:
Assets/Scripts/Player.cs(25,10): error CS0103: The name 'invalidVar' does not exist in the current context
Assets/Scripts/Enemy.cs(67,5): error CS0246: The type or namespace name 'MissingClass' could not be found
```

### Error Scenarios

**Already Compiling:**
```json
{
  "id": "uuid",
  "status": "running",
  "action": "compile",
  "duration_ms": 0
}
```

The command will wait for compilation to complete.

**Assembly Definition Issues:**
```json
{
  "id": "uuid",
  "status": "failure",
  "action": "compile",
  "duration_ms": 500,
  "error": "Assembly definition file has errors: Assets/Scripts/MyAssembly.asmdef"
}
```

### Notes

- **Automatic Recompilation:** Unity automatically compiles on script changes; this command forces immediate compilation
- **Duration:** Depends on project size and number of changed scripts
- **Error Details:** Includes file paths with line/column numbers for easy navigation
- **Dependencies:** Compilation includes all assembly dependencies
- **Platform:** Compiles for the currently selected build target

### Examples

```bash
# Basic compilation
python scripts/unity_command.py compile

# With extended timeout for large projects
python scripts/unity_command.py compile --timeout 60

# Cleanup old responses first
python scripts/unity_command.py compile --cleanup
```

---

## refresh

Force Unity to refresh the asset database, reimporting changed assets.

### Usage

```bash
python scripts/unity_command.py refresh [options]
```

### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `--timeout` | int | No | 30 | Command timeout in seconds |

### Response Format

**Success:**
```json
{
  "id": "uuid",
  "status": "success",
  "action": "refresh",
  "duration_ms": 500
}
```

**Failure:**
```json
{
  "id": "uuid",
  "status": "failure",
  "action": "refresh",
  "duration_ms": 300,
  "error": "Failed to refresh asset database: I/O error"
}
```

### Formatted Output

**Success:**
```
✓ Asset Database Refreshed
Duration: 0.50s
```

**Failure:**
```
✗ Refresh Failed: Failed to refresh asset database: I/O error
Duration: 0.30s
```

### When to Use

Use `refresh` when:
- Files were added/modified outside Unity (external editor, git operations)
- Asset metadata needs to be regenerated
- Forcing reimport of specific assets
- Debugging asset import issues

### Error Scenarios

**Asset Import Errors:**

Unity will refresh but may report import errors in the console. The command will still succeed with `status: "success"`, but you should check console logs.

**File System Errors:**
```json
{
  "id": "uuid",
  "status": "failure",
  "action": "refresh",
  "error": "Asset database locked by another process"
}
```

### Notes

- **Automatic Refresh:** Unity normally refreshes automatically when it detects file changes
- **Force Refresh:** This command forces an immediate refresh
- **Import Time:** Duration depends on number of changed assets
- **Compilation:** May trigger script compilation if C# files changed
- **Asset Serialization:** Respects Unity's asset serialization mode (text/binary)

### Examples

```bash
# Basic refresh
python scripts/unity_command.py refresh

# After git operations (pulling changes)
git pull && python scripts/unity_command.py refresh

# With extended timeout for large projects
python scripts/unity_command.py refresh --timeout 60
```

---

## get-status

Get current Unity Editor state, including compilation status, play mode, and update status.

### Usage

```bash
python scripts/unity_command.py get-status
```

### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `--timeout` | int | No | 30 | Command timeout in seconds |

### Response Format

The status is returned in the `error` field as JSON (historical implementation detail):

```json
{
  "id": "uuid",
  "status": "success",
  "action": "get-status",
  "duration_ms": 10,
  "error": "{\"isCompiling\":false,\"isUpdating\":false,\"isPlaying\":false,\"isPaused\":false}"
}
```

**Parsed Status Fields:**
- `isCompiling` (bool): True if Unity is compiling scripts
- `isUpdating` (bool): True if Unity is updating/processing assets
- `isPlaying` (bool): True if editor is in Play Mode
- `isPaused` (bool): True if Play Mode is paused

### Formatted Output

**Example Outputs:**

```
Unity Editor Status:
  - Compilation: ✓ Ready
  - Play Mode: ✏ Editing
  - Updating: No
```

```
Unity Editor Status:
  - Compilation: ⏳ Compiling...
  - Play Mode: ✏ Editing
  - Updating: No
```

```
Unity Editor Status:
  - Compilation: ✓ Ready
  - Play Mode: ▶ Playing
  - Updating: No
```

```
Unity Editor Status:
  - Compilation: ✓ Ready
  - Play Mode: ⏸ Paused
  - Updating: No
```

### Status Combinations

| Compilation | Play Mode | Meaning |
|-------------|-----------|---------|
| ✓ Ready | ✏ Editing | Normal editing mode, ready for commands |
| ⏳ Compiling | ✏ Editing | Scripts are compiling, wait before testing |
| ✓ Ready | ▶ Playing | In Play Mode, some commands unavailable |
| ✓ Ready | ⏸ Paused | Play Mode paused, can inspect state |
| ⏳ Compiling | Any | Wait for compilation before running tests |

### When to Use

Check status before:
- Running tests (wait for compilation to finish)
- Executing commands that require Edit Mode
- Checking if editor is responsive
- Debugging why commands aren't working

### Notes

- **Fast Response:** Status check is instant (~10ms)
- **Polling:** Can be used for polling until compilation finishes
- **Play Mode:** Some commands (tests, compile) may not work in Play Mode
- **Compilation:** If `isCompiling: true`, wait before running tests

### Examples

```bash
# Check current status
python scripts/unity_command.py get-status

# Wait for compilation to finish (pseudo-code workflow)
while status.isCompiling:
    sleep 1
    status = get-status()
run-tests()
```

---

## get-console-logs

Retrieve Unity console logs with filtering options.

### Usage

```bash
python scripts/unity_command.py get-console-logs [options]
```

### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `--limit` | int | No | 50 | Maximum number of logs to retrieve |
| `--filter` | string | No | None | Filter by log type: `Log`, `Warning`, or `Error` |
| `--timeout` | int | No | 30 | Command timeout in seconds |

### Parameter Details

**`--limit`**
- Controls maximum number of logs returned
- Range: 1 to 1000 (practical limit)
- Logs are returned in reverse chronological order (newest first)

**`--filter`**
- `Log`: Regular log messages (`Debug.Log`)
- `Warning`: Warning messages (`Debug.LogWarning`)
- `Error`: Error messages (`Debug.LogError`, exceptions)
- Omit to get all log types

### Response Format

```json
{
  "id": "uuid",
  "status": "success",
  "action": "get-console-logs",
  "duration_ms": 50,
  "consoleLogs": [
    {
      "message": "NullReferenceException: Object reference not set to an instance of an object",
      "stackTrace": "Player.Update () (at Assets/Scripts/Player.cs:34)\nUnityEngine.Debug:LogError(Object)",
      "type": "Error",
      "count": 1
    },
    {
      "message": "Failed to load asset: missing_texture.png",
      "stackTrace": "",
      "type": "Error",
      "count": 1
    },
    {
      "message": "Shader compilation succeeded",
      "stackTrace": "",
      "type": "Log",
      "count": 3
    }
  ]
}
```

**Log Entry Fields:**
- `message` (string): Log message text
- `stackTrace` (string): Stack trace if available (errors/warnings)
- `type` (string): `Log`, `Warning`, or `Error`
- `count` (int): Number of collapsed duplicate messages (Unity feature)

### Formatted Output

```
Console Logs (last 10, filtered by Error):

[Error] NullReferenceException: Object reference not set to an instance of an object
  Player.Update () (at Assets/Scripts/Player.cs:34)
  UnityEngine.Debug:LogError(Object)

[Error] Failed to load asset: missing_texture.png

[Log] (x3) Shader compilation succeeded
```

### When to Use

Get logs:
- After test failures to see error messages
- After compilation errors for detailed diagnostics
- To check for runtime errors during development
- To monitor warnings that should be fixed

### Error Scenarios

**No Logs Available:**
```json
{
  "id": "uuid",
  "status": "success",
  "action": "get-console-logs",
  "duration_ms": 10,
  "consoleLogs": []
}
```

Formatted as:
```
No console logs found
```

**Invalid Filter:**
```json
{
  "id": "uuid",
  "status": "error",
  "action": "get-console-logs",
  "error": "Invalid filter type: 'InvalidType'. Use 'Log', 'Warning', or 'Error'."
}
```

### Notes

- **Collapsed Logs:** Unity collapses duplicate messages; `count` field shows how many
- **Stack Traces:** Errors and warnings include stack traces when available
- **File Paths:** Stack traces include file paths with line numbers for navigation
- **Performance:** Reading logs is fast, but very large limits may be slow
- **Cleared Console:** If console was cleared in Unity, no logs will be available
- **Memory:** Unity keeps a limited history of console logs

### Examples

```bash
# Get last 20 logs
python scripts/unity_command.py get-console-logs --limit 20

# Get only errors
python scripts/unity_command.py get-console-logs --filter Error

# Get only warnings
python scripts/unity_command.py get-console-logs --filter Warning

# Get last 5 logs of all types
python scripts/unity_command.py get-console-logs --limit 5

# Check for errors after compilation
python scripts/unity_command.py compile
python scripts/unity_command.py get-console-logs --filter Error --limit 10
```

---

## Common Patterns

### Check Compilation Status Before Running Tests

```bash
# Check if compiling
status=$(python scripts/unity_command.py get-status)

# If ready, run tests
if [[ $status == *"✓ Ready"* ]]; then
    python scripts/unity_command.py run-tests
else
    echo "Waiting for compilation..."
fi
```

### Run Tests and Check Logs on Failure

```bash
# Run tests
python scripts/unity_command.py run-tests

# If failed (exit code 1), get error logs
if [ $? -ne 0 ]; then
    python scripts/unity_command.py get-console-logs --filter Error --limit 10
fi
```

### Full Project Health Check

```bash
# 1. Check status
python scripts/unity_command.py get-status

# 2. Compile
python scripts/unity_command.py compile

# 3. Run tests
python scripts/unity_command.py run-tests

# 4. Check for errors
python scripts/unity_command.py get-console-logs --filter Error
```

### After Git Pull Workflow

```bash
# Pull changes
git pull

# Refresh assets
python scripts/unity_command.py refresh

# Wait for compilation
# (Unity will auto-compile after refresh)

# Run tests
python scripts/unity_command.py run-tests --mode EditMode
```

---

## Exit Codes

All commands return standard exit codes for shell integration:

| Code | Meaning | When |
|------|---------|------|
| 0 | Success | Command completed successfully |
| 1 | Error | Unity not running, invalid parameters, command failed |
| 2 | Timeout | No response within timeout period |

### Shell Integration Example

```bash
#!/bin/bash

# Run tests and check exit code
python scripts/unity_command.py run-tests --mode EditMode

case $? in
    0)
        echo "Tests passed!"
        ;;
    1)
        echo "Tests failed or error occurred"
        exit 1
        ;;
    2)
        echo "Command timed out - is Unity responding?"
        exit 1
        ;;
esac
```

---

## Response Status Values

All commands return one of these status values:

| Status | Meaning | Next Steps |
|--------|---------|------------|
| `success` | Command completed successfully | No action needed |
| `failure` | Command completed but with failures | Check `error` or `failures` fields |
| `error` | Command could not execute | Check `error` field for details |
| `running` | Command in progress | Wait for final response (handled by script) |

The Python script automatically polls for final responses, so you typically only see `success`, `failure`, or `error`.

---

## Timeouts

### Default Timeout: 30 seconds

Suitable for:
- Most EditMode test suites
- Compilation of small-medium projects
- Status checks
- Console log retrieval
- Asset refresh

### When to Increase Timeout

Use `--timeout 60` or higher for:
- Large PlayMode test suites (entering/exiting Play Mode is slow)
- Large projects with many scripts
- First compilation after Unity opens
- Comprehensive test runs
- Slow hardware

### Timeout Troubleshooting

If commands frequently timeout:
1. Check Unity Console for errors/warnings
2. Close unnecessary Unity Editor windows
3. Verify system resources (CPU, RAM)
4. Check if Unity is responsive (try manual operations)
5. Increase timeout value
6. Consider breaking tests into smaller suites

---

## Error Messages

### Common Errors and Solutions

**"Unity Editor not detected"**
- Unity is not running
- Unity project is not open
- `.claude/unity/` directory doesn't exist (package not installed)

**Solution:** Open Unity with your project and ensure package is installed.

**"Command timed out after Xs"**
- Command took longer than timeout
- Unity is frozen or unresponsive
- Command failed silently

**Solution:** Check Unity Console, increase timeout, or restart Unity.

**"Failed to parse response JSON"**
- Response file corrupted
- Unity wrote invalid JSON
- File was caught mid-write (rare)

**Solution:** Script retries automatically; if persistent, check Unity Console.

**"Failed to write command file"**
- File system permissions issue
- Disk full
- Antivirus interference

**Solution:** Check file permissions on project directory.

---

## Platform Notes

### Windows
- File paths use backslashes in Unity output but are converted to forward slashes in responses
- File locking is more aggressive; script handles this with retries
- Antivirus may interfere with file operations

### macOS
- File paths use forward slashes
- Case-sensitive file systems may affect asset paths
- Gatekeeper may require approving Unity security prompt on first run

### Linux
- File paths use forward slashes
- Case-sensitive file systems
- May need to adjust file permissions on `.claude/` directory

---

## Performance Considerations

### Command Speed (Typical)

| Command | Duration | Notes |
|---------|----------|-------|
| get-status | ~10ms | Instant |
| get-console-logs | ~50ms | Fast |
| compile | 1-10s | Depends on project size |
| refresh | 0.5-5s | Depends on changed assets |
| run-tests (EditMode) | 1-30s | Depends on test count |
| run-tests (PlayMode) | 5-60s+ | Slower due to Play Mode startup |

### Optimization Tips

1. **Use filters** for tests to run only relevant tests
2. **Check status** before running tests to avoid errors
3. **Use EditMode tests** when possible (faster than PlayMode)
4. **Increase timeout** rather than retrying commands
5. **Cleanup old responses** periodically with `--cleanup` flag

# Usage

## How It Works

ClaudeBridge uses a file-based protocol for communication:

1. **Command File**: Claude Code writes commands to `.unity-bridge/command.json`
2. **Processing**: Unity Editor polls for commands and executes them
3. **Response File**: Results are written to `.unity-bridge/response-{id}.json`

This approach enables multi-project support - each Unity project has its own `.unity-bridge/` directory, allowing multiple agents to work on different projects simultaneously.

## Commands

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
- **Reset Processing State** - Clear stuck processing state

## Using with Claude Code

Once the skill is installed, simply ask Claude Code naturally:

- "Run the Unity tests in EditMode"
- "Check if there are any compilation errors"
- "Show me the last 10 error logs from Unity"
- "Refresh the Unity asset database"

See [skill/SKILL.md](../skill/SKILL.md) for complete documentation.

## Complete Command Reference

For detailed command specifications including all parameters, response formats, and error scenarios, see [skill/references/COMMANDS.md](../skill/references/COMMANDS.md).

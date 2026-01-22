# Extending Claude Unity Bridge

Learn how to add custom commands to the Unity Bridge for project-specific workflows.

## Table of Contents

- [Why Extend?](#why-extend)
- [Architecture Overview](#architecture-overview)
- [Quick Start: Adding a Custom Command](#quick-start-adding-a-custom-command)
- [Command Interface](#command-interface)
- [Custom Parameters](#custom-parameters)
- [Custom Response Fields](#custom-response-fields)
- [Example: Build Command](#example-build-command)
- [Example: Scene Management](#example-scene-management)
- [Example: Asset Management](#example-asset-management)
- [Integration Patterns](#integration-patterns)
- [Best Practices](#best-practices)
- [Testing Custom Commands](#testing-custom-commands)

---

## Why Extend?

The Unity Bridge ships with 5 core commands that work for any Unity project. However, your project may have unique workflows:

- **Custom Build Systems:** Build for specific platforms or configurations
- **Asset Pipelines:** Generate, validate, or process assets
- **Scene Management:** Load, validate, or switch scenes
- **Project-Specific Tools:** Run custom editor tools or validators
- **CI/CD Integration:** Automate project-specific build/test workflows

By extending the bridge with custom commands, you can automate these workflows through Claude Code.

---

## Architecture Overview

The Unity Bridge uses a **Command Pattern** for extensibility:

```
Claude Code (Python Script)
    ↓ writes
command.json
    ↓ polls (Unity EditorApplication.update)
ClaudeBridge.cs (Command Dispatcher)
    ↓ routes to
ICommand Implementation (YourCustomCommand.cs)
    ↓ writes
response-{id}.json
    ↓ reads
Claude Code (Python Script)
```

**Key Components:**

1. **ICommand Interface** - All commands implement this interface
2. **ClaudeBridge.cs** - Dispatches commands to registered handlers
3. **CommandRequest** - Input data structure
4. **CommandResponse** - Output data structure
5. **Python Script** - Handles command execution (no changes needed!)

---

## Quick Start: Adding a Custom Command

### Step 1: Create Command Class

Create `Editor/Commands/YourCommand.cs`:

```csharp
using System;
using System.Diagnostics;
using MXR.ClaudeBridge.Models;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MXR.ClaudeBridge.Commands {
    public class YourCommand : ICommand {
        public void Execute(
            CommandRequest request,
            Action<CommandResponse> onProgress,
            Action<CommandResponse> onComplete
        ) {
            var stopwatch = Stopwatch.StartNew();

            Debug.Log("[ClaudeBridge] Executing your custom command");

            // Report progress (optional)
            var progressResponse = CommandResponse.Running(request.id, request.action);
            onProgress?.Invoke(progressResponse);

            try {
                // Your command logic here
                PerformYourOperation();

                stopwatch.Stop();

                // Return success
                var response = CommandResponse.Success(
                    request.id,
                    request.action,
                    stopwatch.ElapsedMilliseconds
                );

                // Add custom data to response (optional)
                response.error = "Your custom data here";

                onComplete?.Invoke(response);
            }
            catch (Exception e) {
                stopwatch.Stop();
                Debug.LogError($"[ClaudeBridge] Command failed: {e.Message}");

                onComplete?.Invoke(
                    CommandResponse.Error(request.id, request.action, e.Message)
                );
            }
        }

        private void PerformYourOperation() {
            // Your implementation
        }
    }
}
```

### Step 2: Register Command

In `Editor/ClaudeBridge.cs`, add your command to the dictionary:

```csharp
Commands = new Dictionary<string, ICommand> {
    { "run-tests", new RunTestsCommand() },
    { "compile", new CompileCommand() },
    { "refresh", new RefreshCommand() },
    { "get-status", new GetStatusCommand() },
    { "get-console-logs", new GetConsoleLogsCommand() },
    { "your-command", new YourCommand() }  // Add this line
};
```

### Step 3: Test from Python Script

The Python script automatically works with your custom command:

```bash
# The script handles all the file I/O, polling, and formatting
python scripts/unity_command.py your-command
```

That's it! Your command is now available through Claude Code.

---

## Command Interface

All commands must implement `ICommand`:

```csharp
public interface ICommand {
    void Execute(
        CommandRequest request,
        Action<CommandResponse> onProgress,
        Action<CommandResponse> onComplete
    );
}
```

### Parameters

**`request` (CommandRequest)**
- `id` (string): Unique UUID for tracking
- `action` (string): Command name
- `params` (CommandParams): Command parameters
- `timestamp` (string): Optional timestamp

**`onProgress` (Action<CommandResponse>)**
- Callback for progress updates during execution
- Optional, but recommended for long-running operations
- Can be called multiple times

**`onComplete` (Action<CommandResponse>)**
- Callback for final result
- **Must be called exactly once** when command finishes
- Either success, failure, or error

### Response Types

**Success:**
```csharp
var response = CommandResponse.Success(
    request.id,
    request.action,
    stopwatch.ElapsedMilliseconds
);
onComplete?.Invoke(response);
```

**Failure (operation completed but with failures):**
```csharp
var response = CommandResponse.Failure(
    request.id,
    request.action,
    stopwatch.ElapsedMilliseconds,
    "Reason for failure"
);
onComplete?.Invoke(response);
```

**Error (operation could not execute):**
```csharp
var response = CommandResponse.Error(
    request.id,
    request.action,
    "Error message"
);
onComplete?.Invoke(response);
```

**Progress Update:**
```csharp
var progressResponse = CommandResponse.Running(request.id, request.action);
progressResponse.progress = new ProgressInfo {
    current = currentStep,
    total = totalSteps,
    currentTest = "Current operation description"
};
onProgress?.Invoke(progressResponse);
```

---

## Custom Parameters

To add custom parameters to your commands, extend `CommandParams` in `Editor/Models/CommandRequest.cs`:

```csharp
[Serializable]
public class CommandParams {
    // Existing parameters
    public string testMode;
    public string filter;
    public string buildVariant;
    public string limit;

    // Add your custom parameters
    public string targetPlatform;
    public string scenePath;
    public string buildConfiguration;
    public bool developmentBuild;
}
```

Then access them in your command:

```csharp
public void Execute(CommandRequest request, ...) {
    var platform = request.@params?.targetPlatform ?? "StandaloneWindows64";
    var isDevelopment = request.@params?.developmentBuild ?? false;

    // Use parameters in your logic
}
```

**From Python:**

The script automatically passes parameters from command-line args. You may want to extend the Python script's argparse configuration to support your custom parameters, or pass them as generic params.

---

## Custom Response Fields

To return custom data from your commands, you have several options:

### Option 1: Use Existing Fields

The `CommandResponse` has several fields you can repurpose:

```csharp
var response = CommandResponse.Success(request.id, request.action, duration);

// Use error field for custom JSON (like get-status does)
response.error = JsonUtility.ToJson(new {
    customField1 = "value",
    customField2 = 123
});

// Or use result field
response.result = new TestResult {
    passed = customValue1,
    failed = customValue2
};
```

### Option 2: Extend CommandResponse

In `Editor/Models/CommandResponse.cs`, add new fields:

```csharp
[Serializable]
public class CommandResponse {
    public string id;
    public string status;
    public string action;
    public long duration_ms;
    public TestResult result;
    public ProgressInfo progress;
    public List<TestFailure> failures;
    public string error;
    public List<ConsoleLogEntry> consoleLogs;

    // Add your custom fields
    public BuildInfo buildInfo;
    public List<string> generatedAssets;
    public SceneValidationResult sceneValidation;
}
```

Then populate in your command:

```csharp
var response = CommandResponse.Success(request.id, request.action, duration);
response.buildInfo = new BuildInfo {
    platform = "Windows",
    size = 123456789,
    path = "/path/to/build"
};
onComplete?.Invoke(response);
```

---

## Example: Build Command

Create a command to build your Unity project for a specific platform:

```csharp
using System;
using System.Diagnostics;
using System.IO;
using MXR.ClaudeBridge.Models;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MXR.ClaudeBridge.Commands {
    public class BuildCommand : ICommand {
        public void Execute(
            CommandRequest request,
            Action<CommandResponse> onProgress,
            Action<CommandResponse> onComplete
        ) {
            var stopwatch = Stopwatch.StartNew();

            // Get parameters
            var platformString = request.@params?.targetPlatform ?? "StandaloneWindows64";
            var isDevelopment = request.@params?.developmentBuild ?? false;

            Debug.Log($"[ClaudeBridge] Building for {platformString} (development: {isDevelopment})");

            // Report progress
            var progressResponse = CommandResponse.Running(request.id, request.action);
            onProgress?.Invoke(progressResponse);

            try {
                // Parse target platform
                BuildTarget target;
                if (!Enum.TryParse(platformString, out target)) {
                    throw new ArgumentException($"Invalid build target: {platformString}");
                }

                // Set build options
                var buildOptions = new BuildPlayerOptions {
                    scenes = GetScenePaths(),
                    locationPathName = GetBuildPath(target),
                    target = target,
                    options = isDevelopment ? BuildOptions.Development : BuildOptions.None
                };

                // Perform build
                BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
                stopwatch.Stop();

                // Check result
                if (report.summary.result == BuildResult.Succeeded) {
                    Debug.Log($"[ClaudeBridge] Build succeeded: {report.summary.totalSize} bytes");

                    var response = CommandResponse.Success(
                        request.id,
                        request.action,
                        stopwatch.ElapsedMilliseconds
                    );

                    // Return build info
                    response.error = JsonUtility.ToJson(new {
                        result = "success",
                        size = report.summary.totalSize,
                        path = buildOptions.locationPathName,
                        platform = platformString
                    });

                    onComplete?.Invoke(response);
                }
                else {
                    Debug.LogError($"[ClaudeBridge] Build failed: {report.summary.result}");

                    var response = CommandResponse.Failure(
                        request.id,
                        request.action,
                        stopwatch.ElapsedMilliseconds,
                        $"Build failed: {report.summary.result}"
                    );

                    onComplete?.Invoke(response);
                }
            }
            catch (Exception e) {
                stopwatch.Stop();
                Debug.LogError($"[ClaudeBridge] Build error: {e.Message}");

                onComplete?.Invoke(
                    CommandResponse.Error(request.id, request.action, e.Message)
                );
            }
        }

        private string[] GetScenePaths() {
            var scenes = EditorBuildSettings.scenes;
            var paths = new string[scenes.Length];
            for (int i = 0; i < scenes.Length; i++) {
                paths[i] = scenes[i].path;
            }
            return paths;
        }

        private string GetBuildPath(BuildTarget target) {
            var buildDir = Path.Combine(Application.dataPath, "..", "Builds");
            Directory.CreateDirectory(buildDir);

            var extension = target == BuildTarget.StandaloneWindows64 ? ".exe" : "";
            return Path.Combine(buildDir, $"Game_{target}{extension}");
        }
    }
}
```

**Register in ClaudeBridge.cs:**

```csharp
Commands = new Dictionary<string, ICommand> {
    // ... existing commands
    { "build", new BuildCommand() }
};
```

**Usage:**

```bash
# You'll need to extend the Python script to support --target-platform and --development-build
# Or create a project-specific wrapper script
python scripts/unity_command.py build
```

---

## Example: Scene Management

Load or validate specific scenes:

```csharp
using System;
using System.Diagnostics;
using MXR.ClaudeBridge.Models;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MXR.ClaudeBridge.Commands {
    public class LoadSceneCommand : ICommand {
        public void Execute(
            CommandRequest request,
            Action<CommandResponse> onProgress,
            Action<CommandResponse> onComplete
        ) {
            var stopwatch = Stopwatch.StartNew();

            var scenePath = request.@params?.scenePath;
            if (string.IsNullOrEmpty(scenePath)) {
                onComplete?.Invoke(CommandResponse.Error(
                    request.id,
                    request.action,
                    "Missing required parameter: scenePath"
                ));
                return;
            }

            Debug.Log($"[ClaudeBridge] Loading scene: {scenePath}");

            try {
                // Load scene
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                stopwatch.Stop();

                Debug.Log($"[ClaudeBridge] Scene loaded: {scene.name}");

                var response = CommandResponse.Success(
                    request.id,
                    request.action,
                    stopwatch.ElapsedMilliseconds
                );

                // Return scene info
                response.error = JsonUtility.ToJson(new {
                    sceneName = scene.name,
                    scenePath = scene.path,
                    rootCount = scene.rootCount,
                    isDirty = scene.isDirty
                });

                onComplete?.Invoke(response);
            }
            catch (Exception e) {
                stopwatch.Stop();
                Debug.LogError($"[ClaudeBridge] Failed to load scene: {e.Message}");

                onComplete?.Invoke(
                    CommandResponse.Error(request.id, request.action, e.Message)
                );
            }
        }
    }
}
```

---

## Example: Asset Management

Generate or validate assets:

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MXR.ClaudeBridge.Models;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MXR.ClaudeBridge.Commands {
    public class ValidateAssetsCommand : ICommand {
        public void Execute(
            CommandRequest request,
            Action<CommandResponse> onProgress,
            Action<CommandResponse> onComplete
        ) {
            var stopwatch = Stopwatch.StartNew();

            Debug.Log("[ClaudeBridge] Validating assets");

            var progressResponse = CommandResponse.Running(request.id, request.action);
            onProgress?.Invoke(progressResponse);

            try {
                var issues = new List<string>();

                // Find all assets
                var assetPaths = AssetDatabase.GetAllAssetPaths();
                int current = 0;
                int total = assetPaths.Length;

                foreach (var path in assetPaths) {
                    current++;

                    // Report progress every 100 assets
                    if (current % 100 == 0) {
                        progressResponse = CommandResponse.Running(request.id, request.action);
                        progressResponse.progress = new ProgressInfo {
                            current = current,
                            total = total
                        };
                        onProgress?.Invoke(progressResponse);
                    }

                    // Skip Unity internal assets
                    if (!path.StartsWith("Assets/")) continue;

                    // Validate asset
                    if (!File.Exists(path)) {
                        issues.Add($"Missing file: {path}");
                    }
                    else {
                        var asset = AssetDatabase.LoadMainAssetAtPath(path);
                        if (asset == null) {
                            issues.Add($"Failed to load: {path}");
                        }
                    }
                }

                stopwatch.Stop();

                if (issues.Count == 0) {
                    Debug.Log("[ClaudeBridge] Asset validation passed");

                    var response = CommandResponse.Success(
                        request.id,
                        request.action,
                        stopwatch.ElapsedMilliseconds
                    );

                    onComplete?.Invoke(response);
                }
                else {
                    Debug.LogWarning($"[ClaudeBridge] Asset validation found {issues.Count} issues");

                    var response = CommandResponse.Failure(
                        request.id,
                        request.action,
                        stopwatch.ElapsedMilliseconds,
                        $"{issues.Count} asset issues found"
                    );

                    // Return issues as failures
                    response.failures = new List<TestFailure>();
                    foreach (var issue in issues) {
                        response.failures.Add(new TestFailure {
                            name = "Asset Validation",
                            message = issue
                        });
                    }

                    onComplete?.Invoke(response);
                }
            }
            catch (Exception e) {
                stopwatch.Stop();
                Debug.LogError($"[ClaudeBridge] Asset validation error: {e.Message}");

                onComplete?.Invoke(
                    CommandResponse.Error(request.id, request.action, e.Message)
                );
            }
        }
    }
}
```

---

## Integration Patterns

### CI/CD Workflow

Extend the bridge for continuous integration:

```csharp
public class CIBuildCommand : ICommand {
    public void Execute(/* ... */) {
        // 1. Validate assets
        ValidateAssets();

        // 2. Run tests
        RunTests();

        // 3. Build for all platforms
        BuildAllPlatforms();

        // 4. Run post-build validation
        ValidateBuilds();

        // Return comprehensive build report
    }
}
```

**Usage from CI system:**

```bash
#!/bin/bash
# CI script

# Open Unity in batch mode
unity -batchmode -projectPath . -executeMethod YourCICommand.Execute

# Or use the bridge directly
python scripts/unity_command.py ci-build
```

### Pre-Commit Hook

Validate project before committing:

```bash
#!/bin/bash
# .git/hooks/pre-commit

# Run Unity validation
python scripts/unity_command.py validate-assets
python scripts/unity_command.py run-tests --mode EditMode

if [ $? -ne 0 ]; then
    echo "Tests failed! Fix issues before committing."
    exit 1
fi
```

### Custom Build Pipeline

Automate complex build processes:

```csharp
public class CustomBuildPipelineCommand : ICommand {
    public void Execute(/* ... */) {
        // 1. Generate procedural content
        GenerateProceduralAssets();

        // 2. Run asset optimization
        OptimizeAssets();

        // 3. Build addressables
        BuildAddressables();

        // 4. Build player
        BuildPlayer();

        // 5. Upload to distribution
        UploadBuild();
    }
}
```

---

## Best Practices

### 1. Always Use Try-Catch

Wrap your command logic in try-catch to handle unexpected errors:

```csharp
try {
    // Your logic
    onComplete?.Invoke(CommandResponse.Success(...));
}
catch (Exception e) {
    Debug.LogError($"[ClaudeBridge] Error: {e.Message}");
    onComplete?.Invoke(CommandResponse.Error(request.id, request.action, e.Message));
}
```

### 2. Report Progress for Long Operations

Keep Claude informed during long-running operations:

```csharp
for (int i = 0; i < totalSteps; i++) {
    PerformStep(i);

    // Report progress every step
    var progressResponse = CommandResponse.Running(request.id, request.action);
    progressResponse.progress = new ProgressInfo {
        current = i + 1,
        total = totalSteps
    };
    onProgress?.Invoke(progressResponse);
}
```

### 3. Use Stopwatch for Timing

Always measure command duration:

```csharp
var stopwatch = Stopwatch.StartNew();
// ... perform operation
stopwatch.Stop();

var response = CommandResponse.Success(
    request.id,
    request.action,
    stopwatch.ElapsedMilliseconds
);
```

### 4. Log to Unity Console

Help users debug by logging command execution:

```csharp
Debug.Log($"[ClaudeBridge] Starting {request.action}");
// ... operation
Debug.Log($"[ClaudeBridge] Completed {request.action}");
```

### 5. Validate Parameters

Check required parameters and fail fast:

```csharp
var requiredParam = request.@params?.requiredParam;
if (string.IsNullOrEmpty(requiredParam)) {
    onComplete?.Invoke(CommandResponse.Error(
        request.id,
        request.action,
        "Missing required parameter: requiredParam"
    ));
    return;
}
```

### 6. Return Actionable Error Messages

Provide clear guidance when errors occur:

```csharp
// Bad
onComplete?.Invoke(CommandResponse.Error(request.id, request.action, "Failed"));

// Good
onComplete?.Invoke(CommandResponse.Error(
    request.id,
    request.action,
    "Build failed: Missing platform module for WebGL. Install via Unity Hub."
));
```

### 7. Handle Editor State

Check if Unity is in a valid state for your command:

```csharp
if (EditorApplication.isPlaying) {
    onComplete?.Invoke(CommandResponse.Error(
        request.id,
        request.action,
        "Cannot execute command while in Play Mode"
    ));
    return;
}
```

---

## Testing Custom Commands

### Manual Testing

1. **Write Command File:**

```bash
cat > .claude/unity/command.json << EOF
{
  "id": "test-123",
  "action": "your-command",
  "params": {
    "yourParam": "test-value"
  }
}
EOF
```

2. **Check Unity Console:**
   - Unity should pick up the command automatically
   - Check for log messages from your command

3. **Read Response:**

```bash
cat .claude/unity/response-test-123.json
```

### Via Python Script

Once registered, test via the Python script:

```bash
python scripts/unity_command.py your-command --verbose
```

The `--verbose` flag shows detailed execution progress.

### Automated Testing

Create a test command that validates your custom commands:

```csharp
public class TestCustomCommandsCommand : ICommand {
    public void Execute(/* ... */) {
        var testsPassed = 0;
        var testsFailed = 0;

        // Test each custom command
        TestBuildCommand(ref testsPassed, ref testsFailed);
        TestValidateAssetsCommand(ref testsPassed, ref testsFailed);
        // ...

        // Return test results
        if (testsFailed == 0) {
            onComplete?.Invoke(CommandResponse.Success(...));
        } else {
            onComplete?.Invoke(CommandResponse.Failure(...));
        }
    }
}
```

---

## Extending the Python Script

If you need custom argument parsing for your commands, you can fork or extend the Python script:

```python
# Add to argparse configuration
parser.add_argument(
    "--target-platform",
    choices=["StandaloneWindows64", "iOS", "Android", "WebGL"],
    help="Build target platform (for build command)"
)

parser.add_argument(
    "--scene-path",
    help="Scene path (for load-scene command)"
)

# Build parameters based on command
if args.command == "build":
    if args.target_platform:
        params["targetPlatform"] = args.target_platform
    if args.development_build:
        params["developmentBuild"] = "true"

elif args.command == "load-scene":
    if args.scene_path:
        params["scenePath"] = args.scene_path
```

---

## Troubleshooting Custom Commands

### Command Not Recognized

**Symptom:** "Unknown action: your-command" error

**Solution:**
1. Check that command is registered in `ClaudeBridge.cs`
2. Verify command name matches exactly (case-sensitive)
3. Ensure Unity reloaded scripts after adding command

### Command Times Out

**Symptom:** Command never completes, timeout after 30s

**Solution:**
1. Check Unity Console for errors
2. Ensure you call `onComplete` exactly once
3. Add progress logging to debug where command hangs
4. Increase timeout with `--timeout` flag

### Parameters Not Received

**Symptom:** Parameters are null or empty

**Solution:**
1. Check parameter names match `CommandParams` fields
2. Ensure Python script passes parameters correctly
3. Use `request.@params?.yourParam` to handle null params
4. Log parameter values in Unity Console for debugging

### Response Not Formatted

**Symptom:** Python script shows raw JSON instead of formatted output

**Solution:**
1. The Python script only formats known commands
2. Either extend `format_response()` in Python script for your command
3. Or use existing response fields that are already formatted

---

## Summary

Extending the Unity Bridge is straightforward:

1. **Create command class** implementing `ICommand`
2. **Register command** in `ClaudeBridge.cs`
3. **Test with Python script** - it automatically handles file I/O

The architecture is designed for extensibility while keeping the core simple and reliable. Your custom commands benefit from the same rock-solid file protocol, error handling, and progress reporting as the built-in commands.

For questions or issues with custom commands, please open an issue on the GitHub repository with the `[Extension]` prefix.

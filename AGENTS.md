# Agent Guidelines for Claude Unity Bridge

## Project Overview

This is a Unity package (`com.managexr.claude-bridge`) that enables Claude Code to control Unity Editor operations via a file-based protocol. The package has two components:

1. **Unity Package** - C# code that runs in Unity Editor, polls for commands, executes them
2. **Claude Code Skill** - Python script + documentation that Claude uses to send commands

**Key Architecture Decision**: We use a deterministic Python script instead of in-context implementation because it guarantees consistent UUID generation, file handling, polling, and error handling across all Claude sessions.

## Project Structure

### Unity Package (Root)
- `Editor/` - C# command implementations for Unity
  - `ClaudeBridge.cs` - Main coordinator, command dispatcher
  - `Commands/` - Individual command implementations (ICommand interface)
  - `Models/` - Request/Response data structures
- `Documentation/` - Package documentation
- `README.md` - Package documentation and protocol specification

### Claude Code Skill (skill/)
- `scripts/unity_command.py` - **THE CORE** - Handles all command execution
- `SKILL.md` - Main documentation with YAML frontmatter
- `references/` - Extended documentation
  - `COMMANDS.md` - Complete command reference
  - `EXTENDING.md` - Guide for adding custom commands
- `tests/` - pytest test suite
- `TESTING.md` - Testing documentation

### Dependencies
- **Unity**: 2021.3 or later (no additional Unity dependencies)
- **Python**: 3.8+ for the skill script (3.12 supported)
- **pytest**: For testing the Python script
- **black/flake8**: For code formatting and linting
- **pre-commit**: For git hooks
- **GitHub Actions**: CI/CD for skill tests

### Setting Up Development Environment
```bash
# Install development dependencies
cd skill
pip install -r requirements-dev.txt

# Install pre-commit hooks (recommended)
pre-commit install

# Run pre-commit on all files (first time)
pre-commit run --all-files
```

## Commands You Can Use

### Testing the Python Script
```bash
# Run pytest suite (22 tests)
cd skill
pytest tests/test_unity_command.py -v

# Run with coverage
pytest tests/test_unity_command.py --cov=scripts --cov-report=term-missing

# Test script help
python3 scripts/unity_command.py --help
```

### Testing with Unity
```bash
# These require Unity Editor to be running
python3 skill/scripts/unity_command.py get-status
python3 skill/scripts/unity_command.py compile
python3 skill/scripts/unity_command.py run-tests --mode EditMode
python3 skill/scripts/unity_command.py get-console-logs --limit 10 --filter Error
python3 skill/scripts/unity_command.py refresh
```

### Git Workflow
```bash
# Check status
git status

# Typical commit structure for features
git add skill/scripts/*.py          # Core implementation
git commit -m "feat: Add feature"

git add skill/*.md skill/references/ README.md  # Documentation
git commit -m "docs: Add documentation"

git add skill/tests/ .github/       # Testing
git commit -m "test: Add tests and CI"
```

## Coding Style

### Python (skill/scripts/unity_command.py)
- PEP 8 compliant
- 100-character line limit
- Type hints where helpful
- Docstrings for all functions
- Use f-strings for formatting
- Exit codes: 0 (success), 1 (error), 2 (timeout)

### C# (Editor/)
- Follow Unity naming conventions
- Public members: `PascalCase`
- Private fields: `_camelCase` with underscore prefix
- Interfaces: `ICommand`, `ICallbacks`
- Namespaces: `MXR.ClaudeBridge.*`
- Use `[Serializable]` for data models
- Always log with `[ClaudeBridge]` prefix

### Markdown
- Use GitHub-flavored markdown
- Code blocks with language identifiers
- Table of contents for long documents
- Clear examples with actual code

## Testing Guidelines

### Python Script Tests (Critical)
- **Framework**: pytest
- **Coverage Goal**: ~95% of unity_command.py
- **Location**: `skill/tests/test_unity_command.py`
- **Run Before Commit**: Always run pytest before committing script changes
- **CI**: GitHub Actions runs on Python 3.8-3.11, Ubuntu/macOS/Windows

### Unity Package Tests
- **Framework**: Unity Test Framework (NUnit)
- **Location**: `Tests/Editor/`
- **Current Coverage**: All commands tested (~67 tests, Phases 1-4 complete)
- **Run Tests**: Open Unity Editor → Window > General > Test Runner → EditMode → Run All
- **Testing Philosophy**: Test real behavior, not mocks (see implementation plan)
  - Mock dependencies (file system, Unity APIs), test YOUR logic's response
  - Focus on state transitions, error handling, response construction
  - Avoid testing that mocks return what you set up
- **Test Infrastructure**:
  - `Tests/Editor/MXR.ClaudeBridge.Tests.Editor.asmdef` - Test assembly definition
  - `Tests/Editor/TestHelpers/CommandTestFixture.cs` - Base class for command tests
  - `Tests/Editor/TestHelpers/ResponseCapture.cs` - Utility to capture callbacks
- **Phased Rollout** (matching Python suite quality - 22 tests, 95% coverage):
  - ✅ Phase 1: Foundation + GetStatusCommand (8 tests) - COMPLETE
  - ✅ Phase 2: RefreshCommand (7 tests) + model tests (11 tests) - COMPLETE
  - ✅ Phase 3: CompileCommand async tests (9 tests) - COMPLETE
  - ✅ Phase 4: RunTestsCommand (17 tests) + GetConsoleLogsCommand (16 tests) - COMPLETE
  - ⏳ Phase 5: ClaudeBridge dispatcher tests (~15 tests)
  - ⏳ Phase 6: CI/CD integration (deferred - licensing discussion needed)

### Test-Driven Development
When modifying `unity_command.py`:
1. Write/update pytest tests first
2. Implement changes
3. Run `pytest tests/test_unity_command.py -v`
4. Check coverage: `pytest --cov=scripts`
5. All tests must pass before committing

## Critical Guidelines

### NEVER Do These Things

1. **NEVER modify unity_command.py without updating tests**
   - The script is the foundation of reliability
   - Untested changes break the deterministic guarantee
   - Always update `skill/tests/test_unity_command.py`

2. **NEVER modify Unity commands without updating Unity tests**
   - Command implementations must maintain test coverage
   - Run Unity Test Runner after changes (Window > General > Test Runner)
   - Update corresponding test file in `Tests/Editor/Commands/`
   - Maintain the testing philosophy: test real behavior, not mocks

3. **NEVER use in-context file I/O for Unity commands**
   - Always use the Python script
   - Don't implement command writing/polling in prompts
   - The script handles all edge cases (file locking, timeouts, cleanup)

4. **NEVER write to Unity project files while Unity Editor is running**
   - Unity locks files when open
   - File writes can corrupt project state
   - Use the bridge protocol instead

5. **NEVER change response format without updating formatters**
   - Unity-side response format is in `Models/CommandResponse.cs`
   - Python formatters are in `unity_command.py` (format_* functions)
   - These must stay in sync

6. **NEVER remove error handling from commands**
   - All Unity commands must use try-catch
   - All must call `onComplete` exactly once
   - All should call `onProgress` for long operations

### Always Do These Things

1. **ALWAYS run pytest before committing Python changes**
   ```bash
   cd skill && pytest tests/test_unity_command.py -v
   ```

2. **ALWAYS run Unity tests before committing C# changes**
   - Open Unity Editor with project loaded
   - Window > General > Test Runner > EditMode > Run All
   - Verify all tests pass before committing

3. **ALWAYS use descriptive commit messages**
   - Format: `feat:`, `fix:`, `docs:`, `test:`, `chore:`
   - Include context about what and why
   - Reference issues if applicable

4. **ALWAYS update documentation when changing behavior**
   - Update `SKILL.md` for user-facing changes
   - Update `COMMANDS.md` for command spec changes
   - Update `EXTENDING.md` for architecture changes

5. **ALWAYS use the Python script's exit codes correctly**
   - 0: Success
   - 1: Error (Unity not running, invalid params, command failed)
   - 2: Timeout

## File-Based Protocol (Critical Understanding)

### How It Works
1. **Python script** writes `UUID + action + params` to `.claude/unity/command.json`
2. **Unity Editor** polls for `command.json` via `EditorApplication.update`
3. **Unity** deletes command file, executes command
4. **Unity** writes result to `.claude/unity/response-{UUID}.json`
5. **Python script** polls for response file with exponential backoff
6. **Python script** reads response, formats output, deletes response file

### Why File-Based?
- No network configuration needed
- No port conflicts
- Multi-project support (each project has own `.claude/unity/` dir)
- Works across firewalls
- Simple debugging (just inspect JSON files)

### File Location Rules
- **Per-Project**: `.claude/unity/` at Unity project root
- **Not Global**: Each Unity project has its own directory
- **Gitignored**: `.claude/` should be in `.gitignore`
- **Cleanup**: Old responses cleaned up automatically (1 hour max age)

## Extending with Custom Commands

### Adding a New Unity Command

1. **Create Command Class** in `Editor/Commands/YourCommand.cs`:
   ```csharp
   public class YourCommand : ICommand {
       public void Execute(CommandRequest request,
                          Action<CommandResponse> onProgress,
                          Action<CommandResponse> onComplete) {
           var stopwatch = Stopwatch.StartNew();
           try {
               // Your logic here
               onComplete?.Invoke(CommandResponse.Success(...));
           } catch (Exception e) {
               onComplete?.Invoke(CommandResponse.Error(...));
           }
       }
   }
   ```

2. **Register in ClaudeBridge.cs**:
   ```csharp
   Commands = new Dictionary<string, ICommand> {
       // ... existing commands
       { "your-command", new YourCommand() }
   };
   ```

3. **Test with Python script**:
   ```bash
   python3 skill/scripts/unity_command.py your-command
   ```

4. **Optional: Add Python Formatter** in `unity_command.py`:
   ```python
   def format_your_command(response: Dict, status: str, duration: float) -> str:
       # Custom formatting logic
       return formatted_string
   ```

See `skill/references/EXTENDING.md` for complete tutorial with examples.

## Commit Strategy

### For New Features
Prefer 3-commit structure:

1. **Core Implementation** (`feat:`)
   - Python script changes
   - Unity command implementations
   - Requirements/dependencies

2. **Documentation** (`docs:`)
   - SKILL.md updates
   - COMMANDS.md updates
   - EXTENDING.md updates
   - README.md updates

3. **Testing** (`test:`)
   - pytest test suite updates
   - CI/CD workflow changes
   - Testing documentation

### Example Commit Messages
```bash
feat: Add Unity scene validation command

Add validate-scene command that checks scene integrity:
- Validates all GameObjects and components
- Checks for missing references
- Reports broken prefab connections

docs: Add scene validation to skill documentation

- Update COMMANDS.md with validate-scene spec
- Add example to EXTENDING.md
- Update SKILL.md usage section

test: Add pytest tests for scene validation

- Test response formatting
- Test error scenarios
- Update CI workflow
```

## Common Workflows

### Adding a New Command (Full Workflow)

1. **Create Unity-side implementation**
   ```bash
   # Create Editor/Commands/YourCommand.cs
   # Implement ICommand interface
   # Register in ClaudeBridge.cs
   ```

2. **Add Python formatter (optional)**
   ```bash
   # Edit skill/scripts/unity_command.py
   # Add format_your_command() function
   # Update format_response() dispatch
   ```

3. **Write tests**
   ```bash
   # Edit skill/tests/test_unity_command.py
   # Add TestFormatYourCommand class
   cd skill && pytest tests/test_unity_command.py -v
   ```

4. **Update documentation**
   ```bash
   # Update skill/SKILL.md with usage example
   # Update skill/references/COMMANDS.md with full spec
   # Update skill/references/EXTENDING.md if architectural
   ```

5. **Commit in logical chunks**
   ```bash
   git add Editor/ skill/scripts/
   git commit -m "feat: Add your-command implementation"

   git add skill/*.md skill/references/ README.md
   git commit -m "docs: Add your-command documentation"

   git add skill/tests/
   git commit -m "test: Add tests for your-command"
   ```

### Debugging the Bridge

1. **Check Unity is running**
   ```bash
   python3 skill/scripts/unity_command.py get-status --verbose
   ```

2. **Inspect command/response files**
   ```bash
   ls -la .claude/unity/
   cat .claude/unity/command.json
   cat .claude/unity/response-*.json
   ```

3. **Check Unity Console**
   - Look for `[ClaudeBridge]` log messages
   - Command processing logged on pickup
   - Errors logged with details

4. **Test with timeout and verbose**
   ```bash
   python3 skill/scripts/unity_command.py compile --timeout 60 --verbose
   ```

## Architecture Patterns

### Command Pattern (Unity Side)
- All commands implement `ICommand` interface
- `Execute()` receives request, returns via callbacks
- Must call `onComplete` exactly once
- Should call `onProgress` for long operations
- Always use try-catch for error handling

### Response Polling (Python Side)
- Exponential backoff: 0.1s → 1s
- Configurable timeout (default 30s)
- Handles file locking with retry
- Atomic file writes (temp file + replace)
- Automatic cleanup after read

### Error Handling Layers
1. **Python script**: File I/O, timeout, parsing errors
2. **Unity dispatcher**: Unknown commands, registration errors
3. **Unity commands**: Execution errors, validation errors
4. **Response formatting**: Display-friendly error messages

## Testing Strategy

### Unit Tests (pytest)
- Test each formatter function independently
- Test command writing (UUID, JSON structure)
- Test response polling (success, timeout, errors)
- Test cleanup functionality
- Use `tmp_path` fixture for file operations

### Integration Tests (pytest)
- Test full command cycle
- Write command → wait for response → format output
- Simulate Unity responses

### Manual Tests (Unity)
- Run actual commands against running Unity Editor
- Test each command type
- Test error scenarios (Unity not running, timeout)
- Verify formatted output matches examples

## Security & Safety

### File System Safety
- Only write to `.claude/unity/` directory
- Use atomic writes (temp file + replace)
- Handle file locking gracefully
- Clean up old files automatically

### Unity Editor Safety
- Never interrupt Unity compilation
- Check editor state before commands
- Respect Play Mode restrictions
- Don't modify Unity files directly

### Parameter Validation
- Validate all command parameters
- Fail fast with clear error messages
- Don't allow path traversal
- Sanitize user input

## Troubleshooting Guide

### "Unity Editor not detected"
**Cause**: `.claude/unity/` directory doesn't exist
**Fix**: Ensure Unity Editor is open with project loaded and package installed

### "Command timed out after 30s"
**Cause**: Unity not responding or command stuck
**Fix**:
1. Check Unity Console for errors
2. Increase timeout: `--timeout 60`
3. Restart Unity if frozen

### "Failed to parse response JSON"
**Cause**: Response file caught mid-write
**Fix**: Script retries automatically; if persistent, check Unity Console

### Tests Failing After Script Changes
**Cause**: Tests out of sync with implementation
**Fix**: Update `skill/tests/test_unity_command.py` to match new behavior

## Contributing Guidelines

### For Unity Package Changes
1. **Test manually** in Unity Editor (no automated tests yet - see Future Improvements)
2. **Consider adding Unity tests** if implementing significant new logic
3. Document in `README.md`
4. Update protocol spec if changed
5. Version bump in `package.json`

### For Skill Script Changes
1. **MUST** update pytest tests
2. **MUST** run full test suite
3. **MUST** update documentation
4. **SHOULD** update CI if new dependencies

### For Documentation Changes
1. Keep SKILL.md concise (main reference)
2. Put details in `references/` subdirectory
3. Use concrete examples
4. Show actual command output

## CI/CD

### GitHub Actions Workflow
- **Location**: `.github/workflows/test-skill.yml`
- **Triggers**: Push to main, PRs affecting `skill/`
- **Matrix**: Python 3.8-3.11, Ubuntu/macOS/Windows
- **Tests**: pytest with coverage reporting

### CI Must Pass Before Merge
- All 22 pytest tests passing
- Script syntax validation
- Help output validation

## Future Improvements / Known Gaps

### Unity C# Testing (Phases 1-4 Complete)
The Unity package now has comprehensive test coverage:

**Completed Phases:**
- ✅ Phase 1: Foundation + GetStatusCommand (8 tests)
- ✅ Phase 2: RefreshCommand (7 tests) + CommandRequest/CommandResponse serialization (11 tests)
- ✅ Phase 3: CompileCommand async/event-driven tests (9 tests)
- ✅ Phase 4: RunTestsCommand (17 tests) + GetConsoleLogsCommand (16 tests)

**Test Infrastructure:**
- Test assembly definition with NUnit framework
- Base test fixtures (`CommandTestFixture`, `ResponseCapture`)
- Reflection-based testing for internal callbacks
- Mock implementations for Unity Test Runner APIs

**Remaining Phases:**
- Phase 5: ClaudeBridge dispatcher with file system mocking
- Phase 6: CI/CD integration (requires Unity license strategy discussion)

**Testing Philosophy:**
- Test REAL behavior: state transitions, error handling, response construction
- Mock dependencies (file system, Unity APIs), test YOUR code's logic
- Avoid testing that mocks return setup values
- Focus on tests that catch real regressions

**Current Status:**
- Tests run locally in Unity Editor (Window > General > Test Runner)
- CI/CD deferred until licensing strategy determined
- Goal: Match Python suite quality (22 tests, 95% coverage)

### Additional Improvements
- **Performance benchmarks**: Measure command roundtrip time
- **Integration tests**: Python + Unity end-to-end tests
- **Response schema validation**: JSON schema for requests/responses
- **More commands**: Custom build commands, asset validation, etc.

## Quick Reference

### Most Common Commands
```bash
# Get Unity status
python3 skill/scripts/unity_command.py get-status

# Run EditMode tests
python3 skill/scripts/unity_command.py run-tests --mode EditMode

# Check for errors
python3 skill/scripts/unity_command.py get-console-logs --filter Error

# Test Python script
cd skill && pytest tests/test_unity_command.py -v
```

### Key Files to Know
- `skill/scripts/unity_command.py` - THE deterministic command executor
- `Editor/ClaudeBridge.cs` - Unity command dispatcher
- `Editor/Models/CommandResponse.cs` - Response structure
- `skill/tests/test_unity_command.py` - Python test suite
- `skill/SKILL.md` - User-facing documentation

### Exit Codes
- `0` - Success (command completed successfully)
- `1` - Error (Unity not running, invalid params, command failed)
- `2` - Timeout (no response within timeout period)

## Landing the Plane (Session Completion)

**When ending a work session**, you MUST complete ALL steps below. Work is NOT complete until `git push` succeeds.

**MANDATORY WORKFLOW:**

1. **File issues for remaining work** - Create issues for anything that needs follow-up
2. **Run quality gates** (if code changed) - Tests, linters, builds
3. **Update issue status** - Close finished work, update in-progress items
4. **PUSH TO REMOTE** - This is MANDATORY:
   ```bash
   git pull --rebase
   bd sync
   git push
   git status  # MUST show "up to date with origin"
   ```
5. **Clean up** - Clear stashes, prune remote branches
6. **Verify** - All changes committed AND pushed
7. **Hand off** - Provide context for next session

**CRITICAL RULES:**
- Work is NOT complete until `git push` succeeds
- NEVER stop before pushing - that leaves work stranded locally
- NEVER say "ready to push when you are" - YOU must push
- If push fails, resolve and retry until it succeeds

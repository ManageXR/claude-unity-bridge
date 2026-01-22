# Testing the Unity Bridge Skill

This document describes how to test the Unity Bridge Python script.

## Quick Start

```bash
# Install test dependencies
pip install -r requirements-dev.txt

# Run all tests
pytest tests/test_unity_command.py -v

# Run with coverage
pytest tests/test_unity_command.py -v --cov=scripts --cov-report=html
```

## Test Structure

The test suite uses pytest and covers:

1. **Response Formatting** - All command output formatters
2. **Command Writing** - UUID generation and file creation
3. **Response Polling** - Timeout handling and file reading
4. **Cleanup** - Old response file removal
5. **Integration** - Full command cycle

## Running Tests

### All Tests

```bash
cd skill
pytest tests/test_unity_command.py -v
```

### Specific Test Class

```bash
pytest tests/test_unity_command.py::TestFormatTestResults -v
```

### Specific Test

```bash
pytest tests/test_unity_command.py::TestFormatTestResults::test_all_tests_passed -v
```

### With Coverage

```bash
pytest tests/test_unity_command.py --cov=scripts --cov-report=term-missing
```

This shows which lines are not covered by tests.

### Generate HTML Coverage Report

```bash
pytest tests/test_unity_command.py --cov=scripts --cov-report=html
open htmlcov/index.html
```

## Test Categories

### Unit Tests

Test individual functions in isolation:

- `TestFormatTestResults` - Test result formatting
- `TestFormatCompileResults` - Compile result formatting
- `TestFormatConsoleLogs` - Console log formatting
- `TestFormatEditorStatus` - Editor status formatting
- `TestFormatRefreshResults` - Refresh result formatting
- `TestWriteCommand` - Command file writing
- `TestWaitForResponse` - Response polling
- `TestCleanupOldResponses` - File cleanup

### Integration Tests

Test full workflows:

- `TestIntegration::test_full_command_cycle` - Write command, wait for response, format output

## Manual Testing

For manual testing with a real Unity instance:

### 1. Start Unity

Open Unity Editor with a project that has the Claude Unity Bridge package installed.

### 2. Run Commands

```bash
cd skill

# Check editor status
python3 scripts/unity_command.py get-status

# Trigger compilation
python3 scripts/unity_command.py compile

# Run tests
python3 scripts/unity_command.py run-tests --mode EditMode

# Get console logs
python3 scripts/unity_command.py get-console-logs --limit 10 --filter Error

# Refresh assets
python3 scripts/unity_command.py refresh
```

### 3. Test Error Scenarios

```bash
# Close Unity to test "Unity not running" error
python3 scripts/unity_command.py get-status
# Should error: "Unity Editor not detected"

# Test timeout
python3 scripts/unity_command.py get-status --timeout 2
# If Unity is slow to respond, will timeout

# Test verbose mode
python3 scripts/unity_command.py compile --verbose
# Shows detailed execution progress
```

## Continuous Integration

Tests run automatically on GitHub Actions for:

- Python 3.8, 3.9, 3.10, 3.11
- Ubuntu, macOS, Windows
- On every push to `main` and pull requests

See `.github/workflows/test-skill.yml` for the CI configuration.

## Adding New Tests

When adding new functionality to the Python script:

1. Add test cases to `tests/test_unity_command.py`
2. Follow the existing test structure
3. Use descriptive test names
4. Test both success and failure cases
5. Use `tmp_path` fixture for file operations

Example:

```python
def test_new_feature(tmp_path):
    """Test description"""
    # Setup
    with patch('unity_command.UNITY_DIR', tmp_path):
        # Execute
        result = your_new_function()

        # Assert
        assert result == expected_value
```

## Common Issues

### Import Errors

If you see `ModuleNotFoundError: No module named 'unity_command'`:

```bash
# Ensure you're in the skill directory
cd skill

# Run tests from there
pytest tests/test_unity_command.py
```

### Path Issues

The test adds the scripts directory to Python path:

```python
sys.path.insert(0, str(Path(__file__).parent.parent / "scripts"))
```

If you're running tests from a different location, adjust the path accordingly.

### Temporary Directory Cleanup

Tests use `tmp_path` fixture which automatically cleans up after each test. If you need to inspect test files:

```python
def test_something(tmp_path):
    # ... test code
    print(f"Test files at: {tmp_path}")
    import time
    time.sleep(60)  # Keep files for inspection
```

## Test Coverage Goals

Current coverage: ~95% of `unity_command.py`

Not covered (intentionally):
- `main()` function (CLI entry point, tested manually)
- Some error handling edge cases (rare scenarios)
- Platform-specific code paths

## Performance Testing

To test performance of response polling:

```python
import time
from unity_command import wait_for_response

def test_polling_performance(tmp_path):
    with patch('unity_command.UNITY_DIR', tmp_path):
        # Measure polling overhead
        start = time.time()
        try:
            wait_for_response("nonexistent", timeout=1)
        except CommandTimeoutError:
            elapsed = time.time() - start

        # Should timeout close to 1 second (within 100ms)
        assert 0.9 < elapsed < 1.1
```

## Debugging Tests

### Verbose Output

```bash
pytest tests/test_unity_command.py -v -s
```

The `-s` flag shows print statements.

### Debug Specific Test

```bash
pytest tests/test_unity_command.py::TestWriteCommand::test_write_command_creates_file -v -s
```

### Use pdb Debugger

Add to test:

```python
def test_something():
    result = some_function()
    import pdb; pdb.set_trace()  # Breakpoint
    assert result == expected
```

Run:

```bash
pytest tests/test_unity_command.py::test_something -s
```

## Resources

- [pytest documentation](https://docs.pytest.org/)
- [pytest-cov documentation](https://pytest-cov.readthedocs.io/)
- [Python unittest.mock](https://docs.python.org/3/library/unittest.mock.html)

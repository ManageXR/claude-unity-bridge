"""
Tests for Unity Bridge command script.

Run with: pytest skill/tests/test_unity_command.py
"""

import json
import sys
import tempfile
from pathlib import Path
from unittest.mock import patch, MagicMock

import pytest

# Add parent directory to path
sys.path.insert(0, str(Path(__file__).parent.parent / "scripts"))

from unity_command import (
    format_response,
    format_test_results,
    format_compile_results,
    format_console_logs,
    format_editor_status,
    format_refresh_results,
    write_command,
    wait_for_response,
    cleanup_old_responses,
    UnityCommandError,
    CommandTimeoutError,
    UnityNotRunningError,
    UNITY_DIR
)


class TestFormatTestResults:
    """Test formatting of test results"""

    def test_all_tests_passed(self):
        response = {
            "status": "success",
            "result": {
                "passed": 410,
                "failed": 0,
                "skipped": 0,
                "failures": []
            }
        }
        result = format_test_results(response, "success", 1.25)

        assert "✓ Tests Passed: 410" in result
        assert "✗ Tests Failed: 0" in result
        assert "○ Tests Skipped: 0" in result
        assert "Duration: 1.25s" in result
        assert "Failed Tests:" not in result

    def test_tests_with_failures(self):
        response = {
            "status": "failure",
            "result": {
                "passed": 408,
                "failed": 2,
                "skipped": 1,
                "failures": [
                    {
                        "name": "MXR.Tests.AuthTests.LoginTest",
                        "message": "Expected: success\nActual: failure"
                    },
                    {
                        "name": "MXR.Tests.NetworkTests.TimeoutTest",
                        "message": "NullReferenceException"
                    }
                ]
            }
        }
        result = format_test_results(response, "failure", 3.5)

        assert "✓ Tests Passed: 408" in result
        assert "✗ Tests Failed: 2" in result
        assert "○ Tests Skipped: 1" in result
        assert "Failed Tests:" in result
        assert "MXR.Tests.AuthTests.LoginTest" in result
        assert "Expected: success" in result
        assert "MXR.Tests.NetworkTests.TimeoutTest" in result


class TestFormatCompileResults:
    """Test formatting of compilation results"""

    def test_compile_success(self):
        response = {"status": "success"}
        result = format_compile_results(response, "success", 2.3)

        assert "✓ Compilation Successful" in result
        assert "Duration: 2.30s" in result

    def test_compile_failure(self):
        response = {
            "status": "failure",
            "error": "Assets/Scripts/Player.cs(25,10): error CS0103: The name 'invalidVar' does not exist"
        }
        result = format_compile_results(response, "failure", 1.8)

        assert "✗ Compilation Failed" in result
        assert "invalidVar" in result


class TestFormatConsoleLogs:
    """Test formatting of console logs"""

    def test_console_logs_with_errors(self):
        response = {
            "consoleLogs": [
                {
                    "message": "NullReferenceException: Object reference not set",
                    "stackTrace": "Player.Update () (at Assets/Scripts/Player.cs:34)",
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
        result = format_console_logs(response)

        assert "Console Logs" in result
        assert "[Error]" in result
        assert "NullReferenceException" in result
        assert "Player.Update" in result
        assert "[Log] (x3)" in result
        assert "Shader compilation succeeded" in result

    def test_console_logs_empty(self):
        response = {"consoleLogs": []}
        result = format_console_logs(response)

        assert "No console logs found" in result


class TestFormatEditorStatus:
    """Test formatting of editor status"""

    def test_editor_ready(self):
        response = {
            "error": json.dumps({
                "isCompiling": False,
                "isUpdating": False,
                "isPlaying": False,
                "isPaused": False
            })
        }
        result = format_editor_status(response)

        assert "Unity Editor Status:" in result
        assert "✓ Ready" in result
        assert "✏ Editing" in result

    def test_editor_compiling(self):
        response = {
            "error": json.dumps({
                "isCompiling": True,
                "isUpdating": False,
                "isPlaying": False,
                "isPaused": False
            })
        }
        result = format_editor_status(response)

        assert "⏳ Compiling..." in result

    def test_editor_playing(self):
        response = {
            "error": json.dumps({
                "isCompiling": False,
                "isUpdating": False,
                "isPlaying": True,
                "isPaused": False
            })
        }
        result = format_editor_status(response)

        assert "▶ Playing" in result

    def test_editor_paused(self):
        response = {
            "error": json.dumps({
                "isCompiling": False,
                "isUpdating": False,
                "isPlaying": True,
                "isPaused": True
            })
        }
        result = format_editor_status(response)

        assert "⏸ Paused" in result


class TestFormatRefreshResults:
    """Test formatting of refresh results"""

    def test_refresh_success(self):
        response = {"status": "success"}
        result = format_refresh_results(response, "success", 0.5)

        assert "✓ Asset Database Refreshed" in result
        assert "Duration: 0.50s" in result

    def test_refresh_failure(self):
        response = {
            "status": "failure",
            "error": "Failed to refresh: I/O error"
        }
        result = format_refresh_results(response, "failure", 0.3)

        assert "✗ Refresh Failed" in result
        assert "I/O error" in result


class TestFormatResponse:
    """Test main format_response function"""

    def test_format_error_status(self):
        response = {
            "status": "error",
            "error": "Cannot compile while Unity is updating"
        }
        result = format_response(response, "compile")

        assert "✗ Error:" in result
        assert "Cannot compile while Unity is updating" in result

    def test_format_run_tests(self):
        response = {
            "status": "success",
            "duration_ms": 1250,
            "result": {
                "passed": 10,
                "failed": 0,
                "skipped": 0,
                "failures": []
            }
        }
        result = format_response(response, "run-tests")

        assert "✓ Tests Passed: 10" in result


class TestWriteCommand:
    """Test command writing"""

    def test_write_command_creates_file(self, tmp_path):
        # Patch UNITY_DIR to use temp directory
        with patch('unity_command.UNITY_DIR', tmp_path):
            command_id = write_command("test-action", {"param": "value"})

            # Check UUID format
            assert len(command_id) == 36
            assert command_id.count('-') == 4

            # Check file exists
            command_file = tmp_path / "command.json"
            assert command_file.exists()

            # Check content
            content = json.loads(command_file.read_text())
            assert content["id"] == command_id
            assert content["action"] == "test-action"
            assert content["params"]["param"] == "value"

    def test_write_command_creates_directory(self, tmp_path):
        unity_dir = tmp_path / "nested" / "unity"
        with patch('unity_command.UNITY_DIR', unity_dir):
            write_command("test", {})
            assert unity_dir.exists()


class TestWaitForResponse:
    """Test response waiting and polling"""

    def test_wait_for_response_success(self, tmp_path):
        with patch('unity_command.UNITY_DIR', tmp_path):
            command_id = "test-123"
            response_data = {
                "id": command_id,
                "status": "success",
                "action": "test"
            }

            # Create response file
            response_file = tmp_path / f"response-{command_id}.json"
            response_file.write_text(json.dumps(response_data))

            # Should return immediately
            result = wait_for_response(command_id, timeout=1)
            assert result == response_data

    def test_wait_for_response_timeout(self, tmp_path):
        with patch('unity_command.UNITY_DIR', tmp_path):
            # Create directory to simulate Unity running
            tmp_path.mkdir(exist_ok=True)

            with pytest.raises(CommandTimeoutError) as exc_info:
                wait_for_response("nonexistent-id", timeout=1)

            assert "timed out after 1s" in str(exc_info.value)

    def test_wait_for_response_unity_not_running(self, tmp_path):
        # Don't create directory to simulate Unity not running
        nonexistent_dir = tmp_path / "does-not-exist"
        with patch('unity_command.UNITY_DIR', nonexistent_dir):
            with pytest.raises(UnityNotRunningError) as exc_info:
                wait_for_response("test-id", timeout=1)

            assert "Unity Editor not detected" in str(exc_info.value)


class TestCleanupOldResponses:
    """Test cleanup functionality"""

    def test_cleanup_old_responses(self, tmp_path):
        with patch('unity_command.UNITY_DIR', tmp_path):
            # Create some response files
            old_file = tmp_path / "response-old-123.json"
            recent_file = tmp_path / "response-recent-456.json"

            old_file.write_text('{"id": "old-123"}')
            recent_file.write_text('{"id": "recent-456"}')

            # Make old file appear old
            import os
            import time
            old_time = time.time() - 7200  # 2 hours ago
            os.utime(old_file, (old_time, old_time))

            # Run cleanup (max age 1 hour)
            cleanup_old_responses(max_age_hours=1)

            # Old file should be deleted, recent file should remain
            assert not old_file.exists()
            assert recent_file.exists()

    def test_cleanup_no_directory(self, tmp_path):
        # Should not raise error if directory doesn't exist
        nonexistent_dir = tmp_path / "does-not-exist"
        with patch('unity_command.UNITY_DIR', nonexistent_dir):
            cleanup_old_responses()  # Should not raise


class TestIntegration:
    """Integration tests"""

    def test_full_command_cycle(self, tmp_path):
        """Test writing command, waiting for response, and formatting"""
        with patch('unity_command.UNITY_DIR', tmp_path):
            # Write command
            command_id = write_command("get-status", {})

            # Simulate Unity response
            response_data = {
                "id": command_id,
                "status": "success",
                "action": "get-status",
                "duration_ms": 10,
                "error": json.dumps({
                    "isCompiling": False,
                    "isUpdating": False,
                    "isPlaying": False,
                    "isPaused": False
                })
            }
            response_file = tmp_path / f"response-{command_id}.json"
            response_file.write_text(json.dumps(response_data))

            # Wait for response
            response = wait_for_response(command_id, timeout=1)

            # Format response
            formatted = format_response(response, "get-status")

            # Verify
            assert "Unity Editor Status:" in formatted
            assert "✓ Ready" in formatted


if __name__ == "__main__":
    pytest.main([__file__, "-v"])

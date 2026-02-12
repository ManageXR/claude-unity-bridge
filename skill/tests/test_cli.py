"""
Tests for Unity Bridge CLI.

Run with: pytest skill/tests/test_cli.py
"""

import json
import sys
import time
from pathlib import Path
from unittest.mock import patch

import pytest

from claude_unity_bridge.cli import (
    format_response,
    format_test_results,
    format_compile_results,
    format_console_logs,
    format_editor_status,
    format_refresh_results,
    format_play_mode_result,
    format_generic_response,
    write_command,
    wait_for_response,
    cleanup_old_responses,
    cleanup_response_file,
    cleanup_stale_command_file,
    execute_command,
    execute_health_check,
    check_gitignore_and_notify,
    install_skill,
    uninstall_skill,
    update_package,
    get_skill_source_dir,
    get_skill_target_dir,
    get_claude_skills_dir,
    _validate_command_id,
    main,
    UnityCommandError,
    CommandTimeoutError,
    UnityNotRunningError,
    EXIT_SUCCESS,
    EXIT_ERROR,
    EXIT_TIMEOUT,
    MIN_LIMIT,
    MAX_LIMIT,
)


class TestFormatTestResults:
    """Test formatting of test results"""

    def test_all_tests_passed(self):
        response = {
            "status": "success",
            "result": {"passed": 410, "failed": 0, "skipped": 0, "failures": []},
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
                        "message": "Expected: success\nActual: failure",
                    },
                    {
                        "name": "MXR.Tests.NetworkTests.TimeoutTest",
                        "message": "NullReferenceException",
                    },
                ],
            },
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
        error_msg = (
            "Assets/Scripts/Player.cs(25,10): " "error CS0103: The name 'invalidVar' does not exist"
        )
        response = {"status": "failure", "error": error_msg}
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
                    "count": 1,
                },
                {
                    "message": "Shader compilation succeeded",
                    "stackTrace": "",
                    "type": "Log",
                    "count": 3,
                },
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
            "editorStatus": {
                "isCompiling": False,
                "isUpdating": False,
                "isPlaying": False,
                "isPaused": False,
            }
        }
        result = format_editor_status(response)

        assert "Unity Editor Status:" in result
        assert "✓ Ready" in result
        assert "✏ Editing" in result

    def test_editor_compiling(self):
        response = {
            "editorStatus": {
                "isCompiling": True,
                "isUpdating": False,
                "isPlaying": False,
                "isPaused": False,
            }
        }
        result = format_editor_status(response)

        assert "⏳ Compiling..." in result

    def test_editor_playing(self):
        response = {
            "editorStatus": {
                "isCompiling": False,
                "isUpdating": False,
                "isPlaying": True,
                "isPaused": False,
            }
        }
        result = format_editor_status(response)

        assert "▶ Playing" in result

    def test_editor_paused(self):
        response = {
            "editorStatus": {
                "isCompiling": False,
                "isUpdating": False,
                "isPlaying": True,
                "isPaused": True,
            }
        }
        result = format_editor_status(response)

        assert "⏸ Paused" in result

    def test_editor_status_missing(self):
        """Test response when editorStatus field is missing"""
        response = {}
        result = format_editor_status(response)

        assert "Unknown" in result


class TestFormatRefreshResults:
    """Test formatting of refresh results"""

    def test_refresh_success(self):
        response = {"status": "success"}
        result = format_refresh_results(response, "success", 0.5)

        assert "✓ Asset Database Refreshed" in result
        assert "Duration: 0.50s" in result

    def test_refresh_failure(self):
        response = {"status": "failure", "error": "Failed to refresh: I/O error"}
        result = format_refresh_results(response, "failure", 0.3)

        assert "✗ Refresh Failed" in result
        assert "I/O error" in result


class TestFormatPlayModeResult:
    """Test formatting of play/pause/step results"""

    def test_play_enter_play_mode(self):
        response = {
            "action": "play",
            "status": "success",
            "editorStatus": {
                "isCompiling": False,
                "isUpdating": False,
                "isPlaying": True,
                "isPaused": False,
            },
        }
        result = format_play_mode_result(response, "success", 0.01)

        assert "✓ play completed" in result
        assert "▶ Playing" in result
        assert "Duration: 0.01s" in result

    def test_play_exit_play_mode(self):
        response = {
            "action": "play",
            "status": "success",
            "editorStatus": {
                "isCompiling": False,
                "isUpdating": False,
                "isPlaying": False,
                "isPaused": False,
            },
        }
        result = format_play_mode_result(response, "success", 0.01)

        assert "✓ play completed" in result
        assert "⏹ Stopped" in result

    def test_pause_while_playing(self):
        response = {
            "action": "pause",
            "status": "success",
            "editorStatus": {
                "isCompiling": False,
                "isUpdating": False,
                "isPlaying": True,
                "isPaused": True,
            },
        }
        result = format_play_mode_result(response, "success", 0.01)

        assert "✓ pause completed" in result
        assert "⏸ Paused" in result

    def test_unpause_while_playing(self):
        response = {
            "action": "pause",
            "status": "success",
            "editorStatus": {
                "isCompiling": False,
                "isUpdating": False,
                "isPlaying": True,
                "isPaused": False,
            },
        }
        result = format_play_mode_result(response, "success", 0.01)

        assert "✓ pause completed" in result
        assert "▶ Playing" in result

    def test_step_while_playing(self):
        response = {
            "action": "step",
            "status": "success",
            "editorStatus": {
                "isCompiling": False,
                "isUpdating": False,
                "isPlaying": True,
                "isPaused": True,
            },
        }
        result = format_play_mode_result(response, "success", 0.02)

        assert "✓ step completed" in result
        assert "⏸ Paused" in result
        assert "Duration: 0.02s" in result

    def test_failure_response(self):
        response = {
            "action": "pause",
            "status": "failure",
            "error": "Cannot pause: not in play mode",
        }
        result = format_play_mode_result(response, "failure", 0.01)

        assert "✗ pause failed" in result
        assert "Cannot pause: not in play mode" in result

    def test_missing_editor_status_falls_through(self):
        response = {
            "action": "play",
            "status": "success",
        }
        result = format_play_mode_result(response, "success", 0.01)

        # Should fall through to generic formatting
        assert "play completed successfully" in result


class TestFormatResponse:
    """Test main format_response function"""

    def test_format_error_status(self):
        response = {
            "status": "error",
            "error": "Cannot compile while Unity is updating",
        }
        result = format_response(response, "compile")

        assert "✗ Error:" in result
        assert "Cannot compile while Unity is updating" in result

    def test_format_run_tests(self):
        response = {
            "status": "success",
            "duration_ms": 1250,
            "result": {"passed": 10, "failed": 0, "skipped": 0, "failures": []},
        }
        result = format_response(response, "run-tests")

        assert "✓ Tests Passed: 10" in result


class TestWriteCommand:
    """Test command writing"""

    def test_write_command_creates_file(self, tmp_path):
        # Patch UNITY_DIR to use temp directory
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            command_id = write_command("test-action", {"param": "value"})

            # Check UUID format
            assert len(command_id) == 36
            assert command_id.count("-") == 4

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
        with patch("claude_unity_bridge.cli.UNITY_DIR", unity_dir):
            write_command("test", {})
            assert unity_dir.exists()


class TestWaitForResponse:
    """Test response waiting and polling"""

    def test_wait_for_response_success(self, tmp_path):
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            command_id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
            response_data = {"id": command_id, "status": "success", "action": "test"}

            # Create response file
            response_file = tmp_path / f"response-{command_id}.json"
            response_file.write_text(json.dumps(response_data))

            # Should return immediately
            result = wait_for_response(command_id, timeout=1)
            assert result == response_data

    def test_wait_for_response_timeout(self, tmp_path):
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            # Create directory to simulate Unity running
            tmp_path.mkdir(exist_ok=True)

            with pytest.raises(CommandTimeoutError) as exc_info:
                wait_for_response("b2c3d4e5-f6a7-8901-bcde-f12345678901", timeout=1)

            assert "timed out after 1s" in str(exc_info.value)

    def test_wait_for_response_unity_not_running(self, tmp_path):
        # Don't create directory to simulate Unity not running
        nonexistent_dir = tmp_path / "does-not-exist"
        with patch("claude_unity_bridge.cli.UNITY_DIR", nonexistent_dir):
            with pytest.raises(UnityNotRunningError) as exc_info:
                wait_for_response("c3d4e5f6-a7b8-9012-cdef-123456789012", timeout=1)

            assert "Unity Editor not detected" in str(exc_info.value)


class TestCleanupOldResponses:
    """Test cleanup functionality"""

    def test_cleanup_old_responses(self, tmp_path):
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
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
        with patch("claude_unity_bridge.cli.UNITY_DIR", nonexistent_dir):
            cleanup_old_responses()  # Should not raise


class TestIntegration:
    """Integration tests"""

    def test_full_command_cycle(self, tmp_path):
        """Test writing command, waiting for response, and formatting"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            # Write command
            command_id = write_command("get-status", {})

            # Simulate Unity response (new editorStatus format)
            response_data = {
                "id": command_id,
                "status": "success",
                "action": "get-status",
                "duration_ms": 10,
                "editorStatus": {
                    "isCompiling": False,
                    "isUpdating": False,
                    "isPlaying": False,
                    "isPaused": False,
                },
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


class TestFormatGenericResponse:
    """Test generic response formatting"""

    def test_generic_success(self):
        response = {"action": "custom-action", "status": "success"}
        result = format_generic_response(response, "success", 1.5)

        assert "✓ custom-action completed successfully" in result
        assert "Duration: 1.50s" in result

    def test_generic_failure(self):
        response = {
            "action": "custom-action",
            "status": "failure",
            "error": "Something broke",
        }
        result = format_generic_response(response, "failure", 0.5)

        assert "✗ custom-action failed: Something broke" in result

    def test_generic_unknown_status(self):
        response = {"action": "custom-action", "status": "pending"}
        result = format_generic_response(response, "pending", 0.1)

        assert "custom-action status: pending" in result


class TestFormatResponseBranches:
    """Test format_response routing to different formatters"""

    def test_format_compile(self):
        response = {"status": "success", "duration_ms": 1000}
        result = format_response(response, "compile")
        assert "Compilation Successful" in result

    def test_format_get_console_logs(self):
        response = {"consoleLogs": []}
        result = format_response(response, "get-console-logs")
        assert "No console logs found" in result

    def test_format_refresh(self):
        response = {"status": "success", "duration_ms": 500}
        result = format_response(response, "refresh")
        assert "Asset Database Refreshed" in result

    def test_format_play(self):
        response = {
            "status": "success",
            "action": "play",
            "duration_ms": 10,
            "editorStatus": {
                "isCompiling": False,
                "isUpdating": False,
                "isPlaying": True,
                "isPaused": False,
            },
        }
        result = format_response(response, "play")
        assert "play completed" in result
        assert "▶ Playing" in result

    def test_format_pause(self):
        response = {
            "status": "success",
            "action": "pause",
            "duration_ms": 10,
            "editorStatus": {
                "isCompiling": False,
                "isUpdating": False,
                "isPlaying": True,
                "isPaused": True,
            },
        }
        result = format_response(response, "pause")
        assert "pause completed" in result
        assert "⏸ Paused" in result

    def test_format_step(self):
        response = {
            "status": "success",
            "action": "step",
            "duration_ms": 10,
            "editorStatus": {
                "isCompiling": False,
                "isUpdating": False,
                "isPlaying": True,
                "isPaused": True,
            },
        }
        result = format_response(response, "step")
        assert "step completed" in result

    def test_format_play_error(self):
        response = {
            "status": "error",
            "action": "pause",
            "error": "Cannot pause: Unity Editor is not in Play Mode.",
        }
        result = format_response(response, "pause")
        assert "✗ Error:" in result
        assert "not in Play Mode" in result

    def test_format_unknown_action(self):
        response = {"action": "unknown-action", "status": "success", "duration_ms": 100}
        result = format_response(response, "unknown-action")
        assert "completed successfully" in result


class TestFormatCompileEdgeCases:
    """Test compile formatting edge cases"""

    def test_compile_failure_no_error(self):
        response = {"status": "failure"}
        result = format_compile_results(response, "failure", 1.0)
        assert "✗ Compilation Failed" in result
        assert "Duration: 1.00s" in result

    def test_compile_unknown_status(self):
        response = {"status": "running"}
        result = format_compile_results(response, "running", 0.5)
        assert "Compilation Status: running" in result


class TestFormatRefreshEdgeCases:
    """Test refresh formatting edge cases"""

    def test_refresh_unknown_status(self):
        response = {"status": "running"}
        result = format_refresh_results(response, "running", 0.3)
        assert "Refresh Status: running" in result


class TestFormatEditorStatusEdgeCases:
    """Test editor status formatting edge cases"""

    def test_editor_updating(self):
        response = {
            "editorStatus": {
                "isCompiling": False,
                "isUpdating": True,
                "isPlaying": False,
                "isPaused": False,
            }
        }
        result = format_editor_status(response)
        assert "⏳ Yes" in result

    def test_editor_missing_status(self):
        """Test response when editorStatus field is missing"""
        response = {"status": "success"}
        result = format_editor_status(response)
        assert "Unknown" in result


class TestFormatConsoleLogsEdgeCases:
    """Test console logs formatting edge cases"""

    def test_console_logs_with_warning(self):
        response = {
            "consoleLogs": [
                {
                    "message": "Deprecated API usage",
                    "stackTrace": "",
                    "type": "Warning",
                    "count": 1,
                }
            ]
        }
        result = format_console_logs(response)
        assert "[Warning]" in result

    def test_console_logs_with_filter(self):
        response = {
            "consoleLogs": [{"message": "test", "type": "Error", "stackTrace": "", "count": 1}],
            "params": {"filter": "Error"},
        }
        result = format_console_logs(response)
        assert "filtered by Error" in result


class TestCleanupResponseFile:
    """Test cleanup_response_file function"""

    def test_cleanup_existing_file(self, tmp_path):
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            command_id = "d4e5f6a7-b8c9-0123-defa-234567890123"
            response_file = tmp_path / f"response-{command_id}.json"
            response_file.write_text('{"id": "test"}')

            cleanup_response_file(command_id)
            assert not response_file.exists()

    def test_cleanup_nonexistent_file(self, tmp_path):
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            # Should not raise error
            cleanup_response_file("e5f6a7b8-c9d0-1234-efab-345678901234")

    def test_cleanup_with_verbose(self, tmp_path, capsys):
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            command_id = "f6a7b8c9-d0e1-2345-fabc-456789012345"
            response_file = tmp_path / f"response-{command_id}.json"
            response_file.write_text('{"id": "test"}')

            cleanup_response_file(command_id, verbose=True)

            captured = capsys.readouterr()
            assert "Cleaned up response file" in captured.err


class TestCleanupOldResponsesVerbose:
    """Test cleanup_old_responses verbose mode"""

    def test_cleanup_verbose_output(self, tmp_path, capsys):
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            old_file = tmp_path / "response-old-verbose.json"
            old_file.write_text('{"id": "old"}')

            import os

            old_time = time.time() - 7200
            os.utime(old_file, (old_time, old_time))

            cleanup_old_responses(max_age_hours=1, verbose=True)

            captured = capsys.readouterr()
            assert "Cleaned up" in captured.err


class TestWriteCommandErrors:
    """Test error handling in write_command"""

    def test_write_command_mkdir_failure(self, tmp_path):
        # Create a file where the directory should be to cause mkdir to fail
        blocking_file = tmp_path / "blocking"
        blocking_file.write_text("blocking")
        unity_dir = blocking_file / "unity"

        with patch("claude_unity_bridge.cli.UNITY_DIR", unity_dir):
            with pytest.raises(UnityCommandError) as exc_info:
                write_command("test", {})
            assert "Failed to create Unity directory" in str(exc_info.value)

    def test_write_command_file_write_failure(self, tmp_path):
        # Make the directory read-only to cause write failure
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            tmp_path.mkdir(parents=True, exist_ok=True)
            # Mock Path.write_text to raise an exception
            with patch.object(Path, "write_text", side_effect=PermissionError("Permission denied")):
                with pytest.raises(UnityCommandError) as exc_info:
                    write_command("test", {})
                assert "Failed to write command file" in str(exc_info.value)


class TestWaitForResponseEdgeCases:
    """Test edge cases in wait_for_response"""

    def test_wait_verbose_polling(self, tmp_path, capsys):
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            command_id = "a7b8c9d0-e1f2-3456-abcd-567890123456"
            response_data = {"id": command_id, "status": "success"}

            # Create response file after a small delay
            def create_response():
                time.sleep(0.15)
                response_file = tmp_path / f"response-{command_id}.json"
                response_file.write_text(json.dumps(response_data))

            import threading

            thread = threading.Thread(target=create_response)
            thread.start()

            result = wait_for_response(command_id, timeout=2, verbose=True)
            thread.join()

            assert result == response_data

    def test_wait_json_decode_error_recovery(self, tmp_path, capsys):
        """Test that mid-write JSON errors are retried once"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            command_id = "b8c9d0e1-f2a3-4567-bcde-678901234567"
            response_file = tmp_path / f"response-{command_id}.json"

            # Write invalid JSON initially (will be overwritten)
            response_file.write_text("{ invalid json")

            # Track calls to simulate file being written mid-read
            call_count = [0]

            def mock_read(self):
                call_count[0] += 1
                if call_count[0] <= 1:
                    return "{ invalid"
                return json.dumps({"id": command_id, "status": "success"})

            with patch.object(Path, "read_text", mock_read):
                result = wait_for_response(command_id, timeout=2, verbose=True)
                assert result["status"] == "success"

    def test_wait_json_decode_error_persistent(self, tmp_path, capsys):
        """Test that persistent JSON errors raise an exception"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            command_id = "c9d0e1f2-a3b4-5678-cdef-789012345678"
            response_file = tmp_path / f"response-{command_id}.json"

            # Write invalid JSON that stays invalid
            response_file.write_text("{ not valid json at all")

            with pytest.raises(UnityCommandError) as exc_info:
                wait_for_response(command_id, timeout=2, verbose=True)

            assert "Failed to parse response JSON" in str(exc_info.value)
            captured = capsys.readouterr()
            assert "Warning: Failed to parse response" in captured.err


class TestWaitForRunningStatus:
    """Test that wait_for_response polls through 'running' status"""

    def test_polls_until_complete(self, tmp_path):
        """wait_for_response should keep polling when status is 'running'"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            command_id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
            response_file = tmp_path / f"response-{command_id}.json"

            # Write initial "running" response
            running_response = {
                "id": command_id,
                "status": "running",
                "action": "run-tests",
                "progress": {"current": 0, "total": 10},
            }
            response_file.write_text(json.dumps(running_response))

            # After a delay, update to "success"
            def update_response():
                time.sleep(0.5)
                success_response = {
                    "id": command_id,
                    "status": "success",
                    "action": "run-tests",
                    "duration_ms": 1000,
                    "result": {"passed": 10, "failed": 0, "skipped": 0, "failures": []},
                }
                response_file.write_text(json.dumps(success_response))

            import threading

            thread = threading.Thread(target=update_response)
            thread.start()

            result = wait_for_response(command_id, timeout=5)
            thread.join()

            assert result["status"] == "success"
            assert result["result"]["passed"] == 10

    def test_timeout_while_running(self, tmp_path):
        """wait_for_response should timeout even if status stays 'running'"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            command_id = "b2c3d4e5-f6a7-8901-bcde-f12345678901"
            response_file = tmp_path / f"response-{command_id}.json"

            # Write "running" response that never completes
            running_response = {
                "id": command_id,
                "status": "running",
                "action": "run-tests",
                "progress": {"current": 0, "total": 10},
            }
            response_file.write_text(json.dumps(running_response))

            with pytest.raises(CommandTimeoutError):
                wait_for_response(command_id, timeout=1)

    def test_verbose_progress_output(self, tmp_path, capsys):
        """wait_for_response should print progress when verbose and status is 'running'"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            command_id = "c3d4e5f6-a7b8-9012-cdef-123456789012"
            response_file = tmp_path / f"response-{command_id}.json"

            # Write initial "running" response with progress
            running_response = {
                "id": command_id,
                "status": "running",
                "action": "run-tests",
                "progress": {"current": 5, "total": 10, "currentTest": "TestFoo"},
            }
            response_file.write_text(json.dumps(running_response))

            # After a delay, update to "success"
            def update_response():
                time.sleep(0.5)
                success_response = {
                    "id": command_id,
                    "status": "success",
                    "action": "run-tests",
                    "duration_ms": 1000,
                    "result": {"passed": 10, "failed": 0, "skipped": 0, "failures": []},
                }
                response_file.write_text(json.dumps(success_response))

            import threading

            thread = threading.Thread(target=update_response)
            thread.start()

            result = wait_for_response(command_id, timeout=5, verbose=True)
            thread.join()

            assert result["status"] == "success"
            captured = capsys.readouterr()
            assert "Tests in progress: 5/10 TestFoo" in captured.err

    def test_verbose_no_progress_info(self, tmp_path, capsys):
        """Verbose output should say 'Command running...' when no progress info"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            command_id = "d4e5f6a7-b8c9-0123-defa-234567890123"
            response_file = tmp_path / f"response-{command_id}.json"

            # Write "running" response without progress total
            running_response = {
                "id": command_id,
                "status": "running",
                "action": "compile",
            }
            response_file.write_text(json.dumps(running_response))

            # After a delay, update to "success"
            def update_response():
                time.sleep(0.5)
                success_response = {
                    "id": command_id,
                    "status": "success",
                    "action": "compile",
                    "duration_ms": 500,
                }
                response_file.write_text(json.dumps(success_response))

            import threading

            thread = threading.Thread(target=update_response)
            thread.start()

            result = wait_for_response(command_id, timeout=5, verbose=True)
            thread.join()

            assert result["status"] == "success"
            captured = capsys.readouterr()
            assert "Command running..." in captured.err

    def test_returns_failure_not_running(self, tmp_path):
        """wait_for_response should return immediately for non-running statuses"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            command_id = "e5f6a7b8-c9d0-1234-efab-345678901234"
            response_file = tmp_path / f"response-{command_id}.json"

            # Write a "failure" response (should return immediately)
            failure_response = {
                "id": command_id,
                "status": "failure",
                "action": "run-tests",
                "duration_ms": 1000,
                "result": {"passed": 8, "failed": 2, "skipped": 0, "failures": []},
            }
            response_file.write_text(json.dumps(failure_response))

            result = wait_for_response(command_id, timeout=5)
            assert result["status"] == "failure"
            assert result["result"]["failed"] == 2


class TestExecuteCommand:
    """Test execute_command function"""

    def test_execute_command_success(self, tmp_path):
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            # Write command file manually
            import uuid

            command_id = str(uuid.uuid4())

            # Mock write_command to return our known ID and create the response
            def mock_write(action, params):
                # Create the command file
                tmp_path.mkdir(parents=True, exist_ok=True)
                command_file = tmp_path / "command.json"
                command_file.write_text(
                    json.dumps({"id": command_id, "action": action, "params": params})
                )
                # Immediately create the response (new editorStatus format)
                response_file = tmp_path / f"response-{command_id}.json"
                response_file.write_text(
                    json.dumps(
                        {
                            "id": command_id,
                            "status": "success",
                            "action": "get-status",
                            "duration_ms": 10,
                            "editorStatus": {
                                "isCompiling": False,
                                "isUpdating": False,
                                "isPlaying": False,
                                "isPaused": False,
                            },
                        }
                    )
                )
                return command_id

            with patch("claude_unity_bridge.cli.write_command", side_effect=mock_write):
                result = execute_command("get-status", {}, timeout=5)
                assert "Unity Editor Status" in result

    def test_execute_command_always_cleans_up(self, tmp_path):
        """execute_command always runs cleanup, even without cleanup flag"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            # Create an old response file
            tmp_path.mkdir(parents=True, exist_ok=True)
            old_file = tmp_path / "response-old-exec.json"
            old_file.write_text('{"id": "old"}')
            import os

            old_time = time.time() - 7200
            os.utime(old_file, (old_time, old_time))

            import uuid

            command_id = str(uuid.uuid4())

            def mock_write(action, params):
                response_file = tmp_path / f"response-{command_id}.json"
                response_file.write_text(
                    json.dumps(
                        {
                            "id": command_id,
                            "status": "success",
                            "action": "compile",
                            "duration_ms": 100,
                        }
                    )
                )
                return command_id

            with patch("claude_unity_bridge.cli.write_command", side_effect=mock_write):
                # Note: cleanup flag NOT passed — cleanup should still run
                result = execute_command("compile", {}, timeout=5)
                assert "Compilation Successful" in result
                # Old file should be cleaned up even without cleanup=True
                assert not old_file.exists()

    def test_execute_command_verbose(self, tmp_path, capsys):
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            import uuid

            command_id = str(uuid.uuid4())

            def mock_write(action, params):
                tmp_path.mkdir(parents=True, exist_ok=True)
                response_file = tmp_path / f"response-{command_id}.json"
                response_file.write_text(
                    json.dumps(
                        {
                            "id": command_id,
                            "status": "success",
                            "action": "refresh",
                            "duration_ms": 50,
                        }
                    )
                )
                return command_id

            with patch("claude_unity_bridge.cli.write_command", side_effect=mock_write):
                result = execute_command("refresh", {}, timeout=5, verbose=True)
                assert "Asset Database Refreshed" in result

                captured = capsys.readouterr()
                assert "Writing command: refresh" in captured.err
                assert f"Command ID: {command_id}" in captured.err
                assert "Waiting for response" in captured.err


class TestHealthCheck:
    """Test health-check command"""

    def test_health_check_no_directory(self, tmp_path, capsys):
        """Health check fails when Unity directory doesn't exist"""
        nonexistent_dir = tmp_path / "does-not-exist"
        with patch("claude_unity_bridge.cli.UNITY_DIR", nonexistent_dir):
            result = execute_health_check(timeout=5, verbose=False)
            assert result == EXIT_ERROR

            captured = capsys.readouterr()
            assert "Unity Bridge not detected" in captured.out
            assert "Directory not found" in captured.out

    def test_health_check_success(self, tmp_path, capsys):
        """Health check succeeds when Unity responds"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            tmp_path.mkdir(parents=True, exist_ok=True)

            # Mock execute_command to return success
            def mock_execute(action, params, timeout, verbose):
                return "Unity Editor Status:\n  - Compilation: ✓ Ready"

            with patch("claude_unity_bridge.cli.execute_command", side_effect=mock_execute):
                result = execute_health_check(timeout=5, verbose=False)
                assert result == EXIT_SUCCESS

                captured = capsys.readouterr()
                assert "Bridge directory exists" in captured.out
                assert "Unity Editor is responding" in captured.out

    def test_health_check_unity_not_responding(self, tmp_path, capsys):
        """Health check fails when Unity doesn't respond"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            tmp_path.mkdir(parents=True, exist_ok=True)

            # Mock execute_command to raise UnityNotRunningError
            with patch(
                "claude_unity_bridge.cli.execute_command",
                side_effect=UnityNotRunningError("Unity not running"),
            ):
                result = execute_health_check(timeout=5, verbose=False)
                assert result == EXIT_ERROR

                captured = capsys.readouterr()
                assert "Unity Editor not responding" in captured.out

    def test_health_check_timeout(self, tmp_path, capsys):
        """Health check returns timeout when Unity times out"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            tmp_path.mkdir(parents=True, exist_ok=True)

            # Mock execute_command to raise CommandTimeoutError
            with patch(
                "claude_unity_bridge.cli.execute_command",
                side_effect=CommandTimeoutError("Timeout"),
            ):
                result = execute_health_check(timeout=5, verbose=False)
                assert result == EXIT_TIMEOUT

                captured = capsys.readouterr()
                assert "Unity Editor timed out" in captured.out


class TestMainFunction:
    """Test main() CLI function"""

    def test_main_help(self, capsys):
        with patch("sys.argv", ["unity-bridge", "--help"]):
            with pytest.raises(SystemExit) as exc_info:
                main()
            assert exc_info.value.code == 0

    def test_main_run_tests(self, tmp_path):
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            argv = ["unity-bridge", "run-tests", "--mode", "EditMode", "--timeout", "1"]
            with patch("sys.argv", argv):
                # Create response immediately
                def mock_write(action, params):
                    command_id = "d0e1f2a3-b4c5-6789-defa-890123456789"
                    response_file = tmp_path / f"response-{command_id}.json"
                    response_file.write_text(
                        json.dumps(
                            {
                                "id": command_id,
                                "status": "success",
                                "action": "run-tests",
                                "duration_ms": 100,
                                "result": {
                                    "passed": 5,
                                    "failed": 0,
                                    "skipped": 0,
                                    "failures": [],
                                },
                            }
                        )
                    )
                    return command_id

                with patch("claude_unity_bridge.cli.write_command", side_effect=mock_write):
                    exit_code = main()
                    assert exit_code == EXIT_SUCCESS

    def test_main_get_console_logs(self, tmp_path):
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            argv = [
                "unity-bridge",
                "get-console-logs",
                "--limit",
                "10",
                "--filter",
                "Error",
                "--timeout",
                "1",
            ]
            with patch("sys.argv", argv):

                def mock_write(action, params):
                    assert params.get("limit") == "10"
                    assert params.get("filter") == "Error"
                    command_id = "e1f2a3b4-c5d6-7890-efab-901234567890"
                    response_file = tmp_path / f"response-{command_id}.json"
                    response_file.write_text(
                        json.dumps(
                            {
                                "id": command_id,
                                "status": "success",
                                "action": "get-console-logs",
                                "consoleLogs": [],
                            }
                        )
                    )
                    return command_id

                with patch("claude_unity_bridge.cli.write_command", side_effect=mock_write):
                    exit_code = main()
                    assert exit_code == EXIT_SUCCESS

    def test_main_health_check(self, tmp_path, capsys):
        """Test health-check via main()"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            tmp_path.mkdir(parents=True, exist_ok=True)

            argv = ["unity-bridge", "health-check", "--timeout", "5"]
            with patch("sys.argv", argv):

                def mock_execute(action, params, timeout, verbose):
                    return "Unity Editor Status:\n  - Compilation: ✓ Ready"

                with patch("claude_unity_bridge.cli.execute_command", side_effect=mock_execute):
                    exit_code = main()
                    assert exit_code == EXIT_SUCCESS

    def test_main_play(self, tmp_path):
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            argv = ["unity-bridge", "play", "--timeout", "1"]
            with patch("sys.argv", argv):

                def mock_write(action, params):
                    command_id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
                    response_file = tmp_path / f"response-{command_id}.json"
                    response_file.write_text(
                        json.dumps(
                            {
                                "id": command_id,
                                "status": "success",
                                "action": "play",
                                "duration_ms": 10,
                                "editorStatus": {
                                    "isCompiling": False,
                                    "isUpdating": False,
                                    "isPlaying": True,
                                    "isPaused": False,
                                },
                            }
                        )
                    )
                    return command_id

                with patch("claude_unity_bridge.cli.write_command", side_effect=mock_write):
                    exit_code = main()
                    assert exit_code == EXIT_SUCCESS

    def test_main_pause(self, tmp_path):
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            argv = ["unity-bridge", "pause", "--timeout", "1"]
            with patch("sys.argv", argv):

                def mock_write(action, params):
                    command_id = "b2c3d4e5-f6a7-8901-bcde-f12345678901"
                    response_file = tmp_path / f"response-{command_id}.json"
                    response_file.write_text(
                        json.dumps(
                            {
                                "id": command_id,
                                "status": "success",
                                "action": "pause",
                                "duration_ms": 10,
                                "editorStatus": {
                                    "isCompiling": False,
                                    "isUpdating": False,
                                    "isPlaying": True,
                                    "isPaused": True,
                                },
                            }
                        )
                    )
                    return command_id

                with patch("claude_unity_bridge.cli.write_command", side_effect=mock_write):
                    exit_code = main()
                    assert exit_code == EXIT_SUCCESS

    def test_main_step(self, tmp_path):
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            argv = ["unity-bridge", "step", "--timeout", "1"]
            with patch("sys.argv", argv):

                def mock_write(action, params):
                    command_id = "c3d4e5f6-a7b8-9012-cdef-123456789012"
                    response_file = tmp_path / f"response-{command_id}.json"
                    response_file.write_text(
                        json.dumps(
                            {
                                "id": command_id,
                                "status": "success",
                                "action": "step",
                                "duration_ms": 10,
                                "editorStatus": {
                                    "isCompiling": False,
                                    "isUpdating": False,
                                    "isPlaying": True,
                                    "isPaused": True,
                                },
                            }
                        )
                    )
                    return command_id

                with patch("claude_unity_bridge.cli.write_command", side_effect=mock_write):
                    exit_code = main()
                    assert exit_code == EXIT_SUCCESS

    def test_main_timeout_error(self, tmp_path):
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            with patch("sys.argv", ["unity-bridge", "compile", "--timeout", "1"]):
                # Don't create response - will timeout
                exit_code = main()
                assert exit_code == EXIT_TIMEOUT

    def test_main_unity_not_running(self, tmp_path):
        nonexistent_dir = tmp_path / "nonexistent"
        with patch("claude_unity_bridge.cli.UNITY_DIR", nonexistent_dir):
            with patch("sys.argv", ["unity-bridge", "get-status", "--timeout", "1"]):
                # Mock write_command to return an ID without creating the directory
                # This simulates the case where the command file can't be written
                # because Unity never created the directory structure
                with patch(
                    "claude_unity_bridge.cli.write_command",
                    return_value="f2a3b4c5-d6e7-8901-fabc-012345678901",
                ):
                    exit_code = main()
                    assert exit_code == EXIT_ERROR

    def test_main_command_error(self, tmp_path):
        # Create a file where the directory should be
        blocking_file = tmp_path / "blocking"
        blocking_file.write_text("blocking")
        unity_dir = blocking_file / "unity"

        with patch("claude_unity_bridge.cli.UNITY_DIR", unity_dir):
            with patch("sys.argv", ["unity-bridge", "compile", "--timeout", "1"]):
                exit_code = main()
                assert exit_code == EXIT_ERROR

    def test_main_keyboard_interrupt(self, tmp_path):
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            with patch("sys.argv", ["unity-bridge", "compile", "--timeout", "1"]):
                with patch(
                    "claude_unity_bridge.cli.execute_command",
                    side_effect=KeyboardInterrupt,
                ):
                    exit_code = main()
                    assert exit_code == EXIT_ERROR

    def test_main_unexpected_error(self, tmp_path):
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            with patch("sys.argv", ["unity-bridge", "compile", "--timeout", "1"]):
                with patch(
                    "claude_unity_bridge.cli.execute_command",
                    side_effect=RuntimeError("Unexpected"),
                ):
                    exit_code = main()
                    assert exit_code == EXIT_ERROR

    def test_main_verbose_unexpected_error(self, tmp_path, capsys):
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            with patch("sys.argv", ["unity-bridge", "compile", "--timeout", "1", "--verbose"]):
                with patch(
                    "claude_unity_bridge.cli.execute_command",
                    side_effect=RuntimeError("Unexpected"),
                ):
                    exit_code = main()
                    assert exit_code == EXIT_ERROR
                    captured = capsys.readouterr()
                    assert "Unexpected error" in captured.err


class TestArgumentValidation:
    """Test command-line argument validation"""

    def test_timeout_zero_rejected(self, tmp_path, capsys):
        """--timeout 0 should fail validation"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            with patch("sys.argv", ["unity-bridge", "get-status", "--timeout", "0"]):
                with pytest.raises(SystemExit) as exc_info:
                    main()
                assert exc_info.value.code == 2  # argparse error exit code

                captured = capsys.readouterr()
                assert "must be a positive integer" in captured.err

    def test_timeout_negative_rejected(self, tmp_path, capsys):
        """--timeout -5 should fail validation"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            with patch("sys.argv", ["unity-bridge", "compile", "--timeout", "-5"]):
                with pytest.raises(SystemExit) as exc_info:
                    main()
                assert exc_info.value.code == 2

                captured = capsys.readouterr()
                assert "must be a positive integer" in captured.err

    def test_limit_zero_rejected(self, tmp_path, capsys):
        """--limit 0 should fail validation"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            argv = [
                "unity-bridge",
                "get-console-logs",
                "--limit",
                "0",
                "--timeout",
                "1",
            ]
            with patch("sys.argv", argv):
                with pytest.raises(SystemExit) as exc_info:
                    main()
                assert exc_info.value.code == 2

                captured = capsys.readouterr()
                expected_msg = f"--limit must be between {MIN_LIMIT} and {MAX_LIMIT}"
                assert expected_msg in captured.err

    def test_limit_negative_rejected(self, tmp_path, capsys):
        """--limit -1 should fail validation"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            argv = [
                "unity-bridge",
                "get-console-logs",
                "--limit",
                "-1",
                "--timeout",
                "1",
            ]
            with patch("sys.argv", argv):
                with pytest.raises(SystemExit) as exc_info:
                    main()
                assert exc_info.value.code == 2

                captured = capsys.readouterr()
                assert "--limit must be between" in captured.err

    def test_limit_too_large_rejected(self, tmp_path, capsys):
        """--limit 1001 should fail validation (exceeds MAX_LIMIT)"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            argv = [
                "unity-bridge",
                "get-console-logs",
                "--limit",
                "1001",
                "--timeout",
                "1",
            ]
            with patch("sys.argv", argv):
                with pytest.raises(SystemExit) as exc_info:
                    main()
                assert exc_info.value.code == 2

                captured = capsys.readouterr()
                expected_msg = f"--limit must be between {MIN_LIMIT} and {MAX_LIMIT}"
                assert expected_msg in captured.err

    def test_limit_valid_boundary(self, tmp_path):
        """--limit 1 and --limit 1000 should be accepted"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            # Test lower boundary
            argv = [
                "unity-bridge",
                "get-console-logs",
                "--limit",
                "1",
                "--timeout",
                "1",
            ]
            with patch("sys.argv", argv):

                def mock_write(action, params):
                    assert params.get("limit") == "1"  # String for C# compatibility
                    command_id = "a3b4c5d6-e7f8-9012-abcd-123456789abc"
                    response_file = tmp_path / f"response-{command_id}.json"
                    response_file.write_text(
                        json.dumps(
                            {
                                "id": command_id,
                                "status": "success",
                                "action": "get-console-logs",
                                "consoleLogs": [],
                            }
                        )
                    )
                    return command_id

                with patch("claude_unity_bridge.cli.write_command", side_effect=mock_write):
                    exit_code = main()
                    assert exit_code == EXIT_SUCCESS

            # Test upper boundary
            argv = [
                "unity-bridge",
                "get-console-logs",
                "--limit",
                "1000",
                "--timeout",
                "1",
            ]
            with patch("sys.argv", argv):

                def mock_write_1000(action, params):
                    assert params.get("limit") == "1000"  # String for C# compatibility
                    command_id = "b4c5d6e7-f8a9-0123-bcde-234567890bcd"
                    response_file = tmp_path / f"response-{command_id}.json"
                    response_file.write_text(
                        json.dumps(
                            {
                                "id": command_id,
                                "status": "success",
                                "action": "get-console-logs",
                                "consoleLogs": [],
                            }
                        )
                    )
                    return command_id

                with patch("claude_unity_bridge.cli.write_command", side_effect=mock_write_1000):
                    exit_code = main()
                    assert exit_code == EXIT_SUCCESS


class TestSecurityValidation:
    """Test security-related validations"""

    def test_symlink_detection(self, tmp_path):
        """Symlinked .unity-bridge directory should raise security error"""
        # Create a target directory for the symlink
        target_dir = tmp_path / "real_dir"
        target_dir.mkdir()

        # Try to create a symlink for the .unity-bridge directory
        symlink_path = tmp_path / ".unity-bridge"
        try:
            symlink_path.symlink_to(target_dir)
        except OSError:
            # Can't create symlinks (Windows without Developer Mode)
            # Skip this test as it requires symlink support
            pytest.skip("Symlink creation not supported (requires Developer Mode on Windows)")

        with patch("claude_unity_bridge.cli.UNITY_DIR", symlink_path):
            with pytest.raises(UnityCommandError) as exc_info:
                write_command("test", {})

            assert "symlink" in str(exc_info.value).lower()
            assert "security" in str(exc_info.value).lower()

    def test_normal_directory_allowed(self, tmp_path):
        """Normal (non-symlink) directory should work fine"""
        unity_dir = tmp_path / ".unity-bridge"
        # Don't create it - write_command should create it
        with patch("claude_unity_bridge.cli.UNITY_DIR", unity_dir):
            # Also patch cwd for gitignore check
            with patch("pathlib.Path.cwd", return_value=tmp_path):
                command_id = write_command("test-action", {"param": "value"})

            # Should succeed
            assert len(command_id) == 36
            assert unity_dir.exists()
            assert not unity_dir.is_symlink()


class TestGitignoreNotification:
    """Test gitignore notification feature"""

    def test_no_notification_when_gitignore_contains_unity_bridge(self, tmp_path, capsys):
        """No notification when .unity-bridge is already in .gitignore"""
        gitignore = tmp_path / ".gitignore"
        gitignore.write_text(".unity-bridge/\n")

        with patch("pathlib.Path.cwd", return_value=tmp_path):
            check_gitignore_and_notify()

        captured = capsys.readouterr()
        assert ".unity-bridge" not in captured.err

    def test_no_notification_when_gitignore_contains_pattern(self, tmp_path, capsys):
        """No notification when gitignore contains .unity-bridge pattern (without slash)"""
        gitignore = tmp_path / ".gitignore"
        gitignore.write_text("*.log\n.unity-bridge\ntemp/\n")

        with patch("pathlib.Path.cwd", return_value=tmp_path):
            check_gitignore_and_notify()

        captured = capsys.readouterr()
        assert ".unity-bridge" not in captured.err

    def test_notification_when_gitignore_missing(self, tmp_path, capsys):
        """Notification when .gitignore doesn't exist"""
        # Ensure no .gitignore exists
        gitignore = tmp_path / ".gitignore"
        if gitignore.exists():
            gitignore.unlink()

        with patch("pathlib.Path.cwd", return_value=tmp_path):
            check_gitignore_and_notify()

        captured = capsys.readouterr()
        assert ".unity-bridge/" in captured.err
        assert "gitignore" in captured.err.lower()

    def test_notification_when_gitignore_exists_without_unity_bridge(self, tmp_path, capsys):
        """Notification when .gitignore exists but doesn't contain .unity-bridge"""
        gitignore = tmp_path / ".gitignore"
        gitignore.write_text("*.log\nnode_modules/\n")

        with patch("pathlib.Path.cwd", return_value=tmp_path):
            check_gitignore_and_notify()

        captured = capsys.readouterr()
        assert ".unity-bridge/" in captured.err
        assert "gitignore" in captured.err.lower()

    def test_notification_on_first_directory_creation(self, tmp_path, capsys):
        """Notification is shown when directory is first created"""
        unity_dir = tmp_path / ".unity-bridge"
        # Ensure no gitignore to trigger notification
        gitignore = tmp_path / ".gitignore"
        if gitignore.exists():
            gitignore.unlink()

        with patch("claude_unity_bridge.cli.UNITY_DIR", unity_dir):
            with patch("pathlib.Path.cwd", return_value=tmp_path):
                write_command("test", {})

        captured = capsys.readouterr()
        assert ".unity-bridge/" in captured.err

    def test_no_notification_on_subsequent_command(self, tmp_path, capsys):
        """No notification when directory already exists"""
        unity_dir = tmp_path / ".unity-bridge"
        unity_dir.mkdir()  # Pre-create directory

        with patch("claude_unity_bridge.cli.UNITY_DIR", unity_dir):
            with patch("pathlib.Path.cwd", return_value=tmp_path):
                write_command("test", {})

        captured = capsys.readouterr()
        assert ".unity-bridge/" not in captured.err


class TestSkillManagement:
    """Test skill installation/uninstallation commands"""

    def test_get_skill_source_dir_exists(self):
        """get_skill_source_dir should find the bundled skill directory"""
        source_dir = get_skill_source_dir()
        assert source_dir is not None
        assert source_dir.exists()
        assert (source_dir / "SKILL.md").exists()

    def test_get_claude_skills_dir(self):
        """get_claude_skills_dir should return ~/.claude/skills"""
        skills_dir = get_claude_skills_dir()
        assert skills_dir == Path.home() / ".claude" / "skills"

    def test_get_skill_target_dir(self):
        """get_skill_target_dir should return ~/.claude/skills/unity-bridge"""
        target_dir = get_skill_target_dir()
        assert target_dir == Path.home() / ".claude" / "skills" / "unity-bridge"

    def test_install_skill_creates_symlink(self, tmp_path, capsys):
        """install_skill should create a symlink or copy to the skill directory"""
        skills_dir = tmp_path / "skills"

        with patch.object(Path, "home", return_value=tmp_path / "home"):
            # Create mock home directory structure
            (tmp_path / "home" / ".claude").mkdir(parents=True)

            # Patch get_claude_skills_dir to use our temp dir
            with patch(
                "claude_unity_bridge.cli.get_claude_skills_dir",
                return_value=skills_dir,
            ):
                with patch(
                    "claude_unity_bridge.cli.get_skill_target_dir",
                    return_value=skills_dir / "unity-bridge",
                ):
                    result = install_skill(verbose=False)

        assert result == EXIT_SUCCESS
        assert (skills_dir / "unity-bridge").exists()
        # On Windows without Developer Mode, may be a copy instead of symlink
        # Either is acceptable
        assert (skills_dir / "unity-bridge").is_dir()

        captured = capsys.readouterr()
        assert "Skill installed" in captured.out

    def test_install_skill_replaces_existing_symlink(self, tmp_path, capsys):
        """install_skill should replace an existing symlink or directory"""
        skills_dir = tmp_path / "skills"
        skills_dir.mkdir(parents=True)

        # Create an old installation (try symlink, fall back to directory)
        old_target = tmp_path / "old_skill"
        old_target.mkdir()
        (old_target / "old_file.txt").write_text("old")
        target_path = skills_dir / "unity-bridge"

        try:
            target_path.symlink_to(old_target)
            created_symlink = True
        except OSError:
            # Can't create symlink (Windows without Developer Mode)
            # Create a directory instead to simulate old copied installation
            import shutil

            shutil.copytree(old_target, target_path)
            created_symlink = False

        with patch(
            "claude_unity_bridge.cli.get_claude_skills_dir",
            return_value=skills_dir,
        ):
            with patch(
                "claude_unity_bridge.cli.get_skill_target_dir",
                return_value=target_path,
            ):
                result = install_skill(verbose=True)

        assert result == EXIT_SUCCESS
        assert target_path.exists()
        # New installation should not contain old file
        assert not (target_path / "old_file.txt").exists()
        # Should contain SKILL.md from new installation
        assert (target_path / "SKILL.md").exists()

        captured = capsys.readouterr()
        if created_symlink:
            assert "Removing existing symlink" in captured.err
        else:
            assert "Removing existing directory" in captured.err

    def test_install_skill_removes_existing_directory(self, tmp_path, capsys):
        """install_skill should remove and replace an existing directory"""
        skills_dir = tmp_path / "skills"
        skill_dir = skills_dir / "unity-bridge"
        skill_dir.mkdir(parents=True)

        # Put a file in it so it's not empty
        (skill_dir / "some_file.txt").write_text("test")

        with patch(
            "claude_unity_bridge.cli.get_claude_skills_dir",
            return_value=skills_dir,
        ):
            with patch(
                "claude_unity_bridge.cli.get_skill_target_dir",
                return_value=skill_dir,
            ):
                result = install_skill(verbose=True)

        assert result == EXIT_SUCCESS
        # Old file should be gone
        assert not (skill_dir / "some_file.txt").exists()
        # New skill should be installed
        assert (skill_dir / "SKILL.md").exists()

        captured = capsys.readouterr()
        assert "Removing existing directory" in captured.err

    def test_install_skill_fails_when_source_missing(self, tmp_path, capsys):
        """install_skill should fail when skill source directory is missing"""
        with patch(
            "claude_unity_bridge.cli.get_skill_source_dir",
            return_value=None,
        ):
            result = install_skill(verbose=False)

        assert result == EXIT_ERROR

        captured = capsys.readouterr()
        assert "Could not find skill files" in captured.err

    def test_uninstall_skill_removes_symlink(self, tmp_path, capsys):
        """uninstall_skill should remove the symlink or copied directory"""
        skills_dir = tmp_path / "skills"
        skills_dir.mkdir(parents=True)

        # Try to create a symlink, fall back to copying if not possible
        target = tmp_path / "skill_target"
        target.mkdir()
        (target / "SKILL.md").write_text("# Skill")
        install_path = skills_dir / "unity-bridge"

        try:
            install_path.symlink_to(target)
            is_symlink = True
        except OSError:
            # Can't create symlink, use copy instead
            import shutil

            shutil.copytree(target, install_path)
            is_symlink = False

        with patch(
            "claude_unity_bridge.cli.get_skill_target_dir",
            return_value=install_path,
        ):
            result = uninstall_skill(verbose=False)

        assert result == EXIT_SUCCESS
        assert not install_path.exists()

        captured = capsys.readouterr()
        assert "Skill uninstalled" in captured.out
        if is_symlink:
            assert "symlink" in captured.out
        else:
            assert "directory" in captured.out

    def test_uninstall_skill_idempotent(self, tmp_path, capsys):
        """uninstall_skill should succeed even when skill is not installed"""
        skills_dir = tmp_path / "skills"
        skills_dir.mkdir(parents=True)
        symlink = skills_dir / "unity-bridge"

        with patch(
            "claude_unity_bridge.cli.get_skill_target_dir",
            return_value=symlink,
        ):
            result = uninstall_skill(verbose=False)

        assert result == EXIT_SUCCESS

        captured = capsys.readouterr()
        assert "not installed" in captured.out

    def test_uninstall_skill_removes_skill_directory(self, tmp_path, capsys):
        """uninstall_skill should remove a directory that contains SKILL.md"""
        skills_dir = tmp_path / "skills"
        skill_dir = skills_dir / "unity-bridge"
        skill_dir.mkdir(parents=True)
        # Add SKILL.md to make it look like a valid skill installation
        (skill_dir / "SKILL.md").write_text("# Skill")

        with patch(
            "claude_unity_bridge.cli.get_skill_target_dir",
            return_value=skill_dir,
        ):
            result = uninstall_skill(verbose=False)

        assert result == EXIT_SUCCESS
        assert not skill_dir.exists()

        captured = capsys.readouterr()
        assert "Skill uninstalled: removed directory" in captured.out

    def test_main_install_skill(self, tmp_path, capsys):
        """Test install-skill command via main()"""
        skills_dir = tmp_path / "skills"

        with patch("sys.argv", ["unity-bridge", "install-skill"]):
            with patch(
                "claude_unity_bridge.cli.get_claude_skills_dir",
                return_value=skills_dir,
            ):
                with patch(
                    "claude_unity_bridge.cli.get_skill_target_dir",
                    return_value=skills_dir / "unity-bridge",
                ):
                    exit_code = main()

        assert exit_code == EXIT_SUCCESS

    def test_main_uninstall_skill(self, tmp_path, capsys):
        """Test uninstall-skill command via main()"""
        skills_dir = tmp_path / "skills"
        skills_dir.mkdir(parents=True)

        # Create an installation to uninstall (try symlink, fall back to copy)
        target = tmp_path / "skill_target"
        target.mkdir()
        (target / "SKILL.md").write_text("# Skill")
        install_path = skills_dir / "unity-bridge"

        try:
            install_path.symlink_to(target)
        except OSError:
            # Can't create symlink, use copy instead
            import shutil

            shutil.copytree(target, install_path)

        with patch("sys.argv", ["unity-bridge", "uninstall-skill"]):
            with patch(
                "claude_unity_bridge.cli.get_skill_target_dir",
                return_value=install_path,
            ):
                exit_code = main()

        assert exit_code == EXIT_SUCCESS

    def test_install_skill_removes_regular_file(self, tmp_path, capsys):
        """install_skill should remove and replace a regular file"""
        skills_dir = tmp_path / "skills"
        skills_dir.mkdir(parents=True)
        target_file = skills_dir / "unity-bridge"
        target_file.write_text("not a symlink or directory")

        with patch(
            "claude_unity_bridge.cli.get_claude_skills_dir",
            return_value=skills_dir,
        ):
            with patch(
                "claude_unity_bridge.cli.get_skill_target_dir",
                return_value=target_file,
            ):
                result = install_skill(verbose=True)

        assert result == EXIT_SUCCESS
        # Should now be a directory (or symlink), not a file
        assert target_file.is_dir() or target_file.is_symlink()
        # Should contain SKILL.md
        assert (target_file / "SKILL.md").exists()

        captured = capsys.readouterr()
        assert "Removing existing file" in captured.err

    def test_update_package_success(self, tmp_path, capsys):
        """update_package should upgrade pip package and reinstall skill"""
        skills_dir = tmp_path / "skills"

        mock_result = type("Result", (), {"returncode": 0, "stderr": ""})()

        with patch("subprocess.run", return_value=mock_result):
            with patch(
                "claude_unity_bridge.cli.get_claude_skills_dir",
                return_value=skills_dir,
            ):
                with patch(
                    "claude_unity_bridge.cli.get_skill_target_dir",
                    return_value=skills_dir / "unity-bridge",
                ):
                    result = update_package(verbose=False)

        assert result == EXIT_SUCCESS

        captured = capsys.readouterr()
        assert "Updating" in captured.out

    def test_update_package_pip_failure(self, capsys):
        """update_package should fail when pip upgrade fails"""
        mock_result = type("Result", (), {"returncode": 1, "stderr": "pip error"})()

        with patch("subprocess.run", return_value=mock_result):
            result = update_package(verbose=False)

        assert result == EXIT_ERROR

        captured = capsys.readouterr()
        assert "pip upgrade failed" in captured.err

    def test_update_package_subprocess_exception(self, capsys):
        """update_package should handle subprocess exceptions"""
        with patch("subprocess.run", side_effect=OSError("command not found")):
            result = update_package(verbose=False)

        assert result == EXIT_ERROR

        captured = capsys.readouterr()
        assert "Could not run pip" in captured.err

    def test_main_update(self, tmp_path, capsys):
        """Test update command via main()"""
        skills_dir = tmp_path / "skills"
        mock_result = type("Result", (), {"returncode": 0, "stderr": ""})()

        with patch("sys.argv", ["unity-bridge", "update"]):
            with patch("subprocess.run", return_value=mock_result):
                with patch(
                    "claude_unity_bridge.cli.get_claude_skills_dir",
                    return_value=skills_dir,
                ):
                    with patch(
                        "claude_unity_bridge.cli.get_skill_target_dir",
                        return_value=skills_dir / "unity-bridge",
                    ):
                        exit_code = main()

        assert exit_code == EXIT_SUCCESS

    def test_install_skill_copy_fallback_on_symlink_failure(self, tmp_path, capsys):
        """install_skill should fall back to copying when symlink creation fails"""
        skills_dir = tmp_path / "skills"
        skills_dir.mkdir(parents=True)
        target_dir = skills_dir / "unity-bridge"

        def mock_symlink_to(self, target):
            raise OSError("[WinError 1314] A required privilege is not held by the client")

        with patch.object(Path, "symlink_to", mock_symlink_to):
            with patch(
                "claude_unity_bridge.cli.get_claude_skills_dir",
                return_value=skills_dir,
            ):
                with patch(
                    "claude_unity_bridge.cli.get_skill_target_dir",
                    return_value=target_dir,
                ):
                    result = install_skill(verbose=True)

        assert result == EXIT_SUCCESS
        assert target_dir.exists()
        assert target_dir.is_dir()
        assert not target_dir.is_symlink()
        assert (target_dir / "SKILL.md").exists()

        captured = capsys.readouterr()
        assert "Symlink creation failed" in captured.err
        assert "Falling back to directory copy" in captured.err
        assert "Skill installed (copy)" in captured.out
        assert "Using directory copy instead of symlink" in captured.out

    def test_install_skill_copy_fallback_failure(self, tmp_path, capsys):
        """install_skill should fail gracefully when both symlink and copy fail"""
        skills_dir = tmp_path / "skills"
        skills_dir.mkdir(parents=True)
        target_dir = skills_dir / "unity-bridge"

        def mock_symlink_to(self, target):
            raise OSError("[WinError 1314] A required privilege is not held by the client")

        with patch.object(Path, "symlink_to", mock_symlink_to):
            with patch("shutil.copytree", side_effect=PermissionError("Permission denied")):
                with patch(
                    "claude_unity_bridge.cli.get_claude_skills_dir",
                    return_value=skills_dir,
                ):
                    with patch(
                        "claude_unity_bridge.cli.get_skill_target_dir",
                        return_value=target_dir,
                    ):
                        result = install_skill(verbose=False)

        assert result == EXIT_ERROR

        captured = capsys.readouterr()
        assert "Could not create symlink or copy directory" in captured.err

    def test_install_skill_replaces_existing_directory(self, tmp_path, capsys):
        """install_skill should replace an existing copied directory"""
        skills_dir = tmp_path / "skills"
        skills_dir.mkdir(parents=True)
        target_dir = skills_dir / "unity-bridge"

        # Create an existing directory (from previous copy install)
        target_dir.mkdir()
        (target_dir / "old_file.txt").write_text("old")

        with patch(
            "claude_unity_bridge.cli.get_claude_skills_dir",
            return_value=skills_dir,
        ):
            with patch(
                "claude_unity_bridge.cli.get_skill_target_dir",
                return_value=target_dir,
            ):
                result = install_skill(verbose=True)

        assert result == EXIT_SUCCESS
        assert target_dir.exists()
        # Old file should be gone
        assert not (target_dir / "old_file.txt").exists()
        # New skill should be present
        assert (target_dir / "SKILL.md").exists()

        captured = capsys.readouterr()
        assert "Removing existing directory" in captured.err

    def test_uninstall_skill_removes_copied_directory(self, tmp_path, capsys):
        """uninstall_skill should remove a copied skill directory"""
        skills_dir = tmp_path / "skills"
        skills_dir.mkdir(parents=True)
        target_dir = skills_dir / "unity-bridge"

        # Simulate a copied installation
        target_dir.mkdir()
        (target_dir / "SKILL.md").write_text("# Skill")
        (target_dir / "scripts").mkdir()

        with patch(
            "claude_unity_bridge.cli.get_skill_target_dir",
            return_value=target_dir,
        ):
            result = uninstall_skill(verbose=False)

        assert result == EXIT_SUCCESS
        assert not target_dir.exists()

        captured = capsys.readouterr()
        assert "Skill uninstalled: removed directory" in captured.out

    def test_uninstall_skill_warns_on_non_skill_directory(self, tmp_path, capsys):
        """uninstall_skill should warn when directory doesn't look like a skill"""
        skills_dir = tmp_path / "skills"
        skills_dir.mkdir(parents=True)
        target_dir = skills_dir / "unity-bridge"

        # Create a directory without SKILL.md
        target_dir.mkdir()
        (target_dir / "random_file.txt").write_text("not a skill")

        with patch(
            "claude_unity_bridge.cli.get_skill_target_dir",
            return_value=target_dir,
        ):
            result = uninstall_skill(verbose=False)

        assert result == EXIT_ERROR

        captured = capsys.readouterr()
        assert "doesn't appear to be a skill installation" in captured.err


class TestUUIDValidation:
    """Test UUID validation for command IDs"""

    def test_valid_uuid_accepted(self):
        """Valid UUID format should not raise"""
        _validate_command_id("a1b2c3d4-e5f6-7890-abcd-ef1234567890")

    def test_invalid_uuid_rejected(self):
        """Non-UUID strings should raise UnityCommandError"""
        with pytest.raises(UnityCommandError, match="Invalid command ID format"):
            _validate_command_id("not-a-uuid")

    def test_path_traversal_rejected(self):
        """Path traversal attempts should be rejected"""
        with pytest.raises(UnityCommandError, match="Invalid command ID format"):
            _validate_command_id("../../etc/passwd")

    def test_empty_string_rejected(self):
        """Empty string should be rejected"""
        with pytest.raises(UnityCommandError, match="Invalid command ID format"):
            _validate_command_id("")

    def test_uuid_with_extra_chars_rejected(self):
        """UUID with trailing path components should be rejected"""
        with pytest.raises(UnityCommandError, match="Invalid command ID format"):
            _validate_command_id("a1b2c3d4-e5f6-7890-abcd-ef1234567890/../secret")

    def test_wait_for_response_validates_id(self, tmp_path):
        """wait_for_response should reject invalid command IDs"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            with pytest.raises(UnityCommandError, match="Invalid command ID format"):
                wait_for_response("../../etc/passwd", timeout=1)

    def test_cleanup_response_file_validates_id(self, tmp_path):
        """cleanup_response_file should reject invalid command IDs"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            with pytest.raises(UnityCommandError, match="Invalid command ID format"):
                cleanup_response_file("../../etc/passwd")

    def test_response_id_mismatch_rejected(self, tmp_path):
        """Response with mismatched ID should raise UnityCommandError"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            command_id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
            response_file = tmp_path / f"response-{command_id}.json"
            response_file.write_text(json.dumps({"id": "different-id", "status": "success"}))

            with pytest.raises(UnityCommandError, match="Response ID mismatch"):
                wait_for_response(command_id, timeout=1)


@pytest.mark.skipif(sys.platform == "win32", reason="POSIX permissions not supported on Windows")
class TestDirectoryPermissions:
    """Test that .unity-bridge/ directory and files get restrictive permissions"""

    def test_directory_created_with_0700_permissions(self, tmp_path):
        import os
        import stat

        unity_dir = tmp_path / ".unity-bridge"
        with patch("claude_unity_bridge.cli.UNITY_DIR", unity_dir):
            write_command("test-action", {"param": "value"})
        mode = os.stat(unity_dir).st_mode
        dir_perms = stat.S_IMODE(mode)
        assert dir_perms == 0o700, f"Expected 0o700, got {oct(dir_perms)}"

    def test_command_file_has_0600_permissions(self, tmp_path):
        import os
        import stat

        unity_dir = tmp_path / ".unity-bridge"
        with patch("claude_unity_bridge.cli.UNITY_DIR", unity_dir):
            write_command("test-action", {"param": "value"})
        command_file = unity_dir / "command.json"
        mode = os.stat(command_file).st_mode
        file_perms = stat.S_IMODE(mode)
        assert file_perms == 0o600, f"Expected 0o600, got {oct(file_perms)}"


class TestCleanupStaleCommandFile:
    """Test cleanup_stale_command_file function"""

    def test_removes_stale_command_file(self, tmp_path):
        """Stale command.json older than timeout is removed"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            import os

            command_file = tmp_path / "command.json"
            command_file.write_text('{"id": "stale", "action": "compile"}')

            # Make it old (older than 30s timeout)
            old_time = time.time() - 60
            os.utime(command_file, (old_time, old_time))

            cleanup_stale_command_file(timeout=30)
            assert not command_file.exists()

    def test_keeps_fresh_command_file(self, tmp_path):
        """Recent command.json within timeout is kept"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            command_file = tmp_path / "command.json"
            command_file.write_text('{"id": "fresh", "action": "compile"}')

            cleanup_stale_command_file(timeout=30)
            assert command_file.exists()

    def test_no_command_file(self, tmp_path):
        """No error when command.json doesn't exist"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            cleanup_stale_command_file(timeout=30)  # Should not raise

    def test_verbose_output(self, tmp_path, capsys):
        """Verbose mode logs stale command file cleanup"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            import os

            command_file = tmp_path / "command.json"
            command_file.write_text('{"id": "stale"}')
            old_time = time.time() - 60
            os.utime(command_file, (old_time, old_time))

            cleanup_stale_command_file(timeout=30, verbose=True)

            captured = capsys.readouterr()
            assert "stale command file" in captured.err


class TestCleanupOldResponsesWithTmpFiles:
    """Test that cleanup_old_responses also cleans .tmp files"""

    def test_cleanup_old_tmp_files(self, tmp_path):
        """Old .tmp files are cleaned up alongside response files"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            import os

            # Create old tmp file
            old_tmp = tmp_path / "command.json.tmp"
            old_tmp.write_text("temp data")
            old_time = time.time() - 7200  # 2 hours ago
            os.utime(old_tmp, (old_time, old_time))

            # Create recent tmp file
            recent_tmp = tmp_path / "response-abc.json.tmp"
            recent_tmp.write_text("recent temp")

            cleanup_old_responses(max_age_hours=1)

            assert not old_tmp.exists()
            assert recent_tmp.exists()

    def test_cleanup_both_response_and_tmp(self, tmp_path):
        """Both old response files and old tmp files are cleaned"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            import os

            old_time = time.time() - 7200

            old_response = tmp_path / "response-old.json"
            old_response.write_text('{"id": "old"}')
            os.utime(old_response, (old_time, old_time))

            old_tmp = tmp_path / "something.tmp"
            old_tmp.write_text("old temp")
            os.utime(old_tmp, (old_time, old_time))

            cleanup_old_responses(max_age_hours=1)

            assert not old_response.exists()
            assert not old_tmp.exists()


class TestResponseCleanupOnError:
    """Test that response files are cleaned up even on timeout/error"""

    def test_response_file_cleaned_on_timeout(self, tmp_path):
        """Response file is cleaned up when CommandTimeoutError is raised"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            import uuid

            command_id = str(uuid.uuid4())

            def mock_write(action, params):
                # Create a response file that might exist from a partial operation
                response_file = tmp_path / f"response-{command_id}.json"
                response_file.write_text('{"partial": true}')
                return command_id

            with patch("claude_unity_bridge.cli.write_command", side_effect=mock_write):
                with patch(
                    "claude_unity_bridge.cli.wait_for_response",
                    side_effect=CommandTimeoutError("Timed out"),
                ):
                    with pytest.raises(CommandTimeoutError):
                        execute_command("compile", {}, timeout=5)

                    # Response file should be cleaned up despite the error
                    response_file = tmp_path / f"response-{command_id}.json"
                    assert not response_file.exists()

    def test_response_file_cleaned_on_format_error(self, tmp_path):
        """Response file is cleaned up when format_response raises"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            import uuid

            command_id = str(uuid.uuid4())
            response_data = {
                "id": command_id,
                "status": "success",
                "action": "compile",
                "duration_ms": 100,
            }

            def mock_write(action, params):
                response_file = tmp_path / f"response-{command_id}.json"
                response_file.write_text(json.dumps(response_data))
                return command_id

            with patch("claude_unity_bridge.cli.write_command", side_effect=mock_write):
                with patch(
                    "claude_unity_bridge.cli.format_response",
                    side_effect=RuntimeError("Format error"),
                ):
                    with pytest.raises(RuntimeError, match="Format error"):
                        execute_command("compile", {}, timeout=5)

                    # Response file should be cleaned up despite the error
                    response_file = tmp_path / f"response-{command_id}.json"
                    assert not response_file.exists()

    def test_cleanup_handles_missing_response_file_on_timeout(self, tmp_path):
        """No error when response file doesn't exist during timeout cleanup"""
        with patch("claude_unity_bridge.cli.UNITY_DIR", tmp_path):
            import uuid

            command_id = str(uuid.uuid4())

            def mock_write(action, params):
                # Don't create response file — simulates Unity never responding
                return command_id

            with patch("claude_unity_bridge.cli.write_command", side_effect=mock_write):
                with patch(
                    "claude_unity_bridge.cli.wait_for_response",
                    side_effect=CommandTimeoutError("Timed out"),
                ):
                    with pytest.raises(CommandTimeoutError):
                        execute_command("compile", {}, timeout=5)

                    # Should not raise — cleanup_response_file handles missing files


if __name__ == "__main__":
    pytest.main([__file__, "-v"])

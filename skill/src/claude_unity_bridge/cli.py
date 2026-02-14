#!/usr/bin/env python3
"""
Claude Unity Bridge - Command Execution Script

Rock-solid, deterministic command execution for Unity Editor operations.
Handles UUID generation, file-based polling, response parsing, and cleanup.

Also provides skill installation commands for Claude Code integration.
"""

import argparse
import json
import os
import re
import platform
import shutil
import subprocess
import sys
import time
import uuid
from pathlib import Path
from typing import Dict, Any, Optional

# Constants
UNITY_DIR = Path.cwd() / ".unity-bridge"
DEFAULT_TIMEOUT = 30
MIN_SLEEP = 0.1
MAX_SLEEP = 1.0
SLEEP_MULTIPLIER = 1.5
MIN_LIMIT = 1
MAX_LIMIT = 1000

# Exit codes
EXIT_SUCCESS = 0
EXIT_ERROR = 1
EXIT_TIMEOUT = 2


class UnityCommandError(Exception):
    """Base exception for Unity command errors"""

    pass


class UnityNotRunningError(UnityCommandError):
    """Unity Editor is not running or not responding"""

    pass


class CommandTimeoutError(UnityCommandError):
    """Command execution timed out"""

    pass


UUID_PATTERN = re.compile(
    r"^[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}$"
)


def _validate_command_id(command_id: str) -> None:
    """Validate command_id is a proper UUID to prevent path traversal."""
    if not UUID_PATTERN.match(command_id):
        raise UnityCommandError("Invalid command ID format")


def check_gitignore_and_notify():
    """Print a notice if .unity-bridge/ is not in .gitignore."""
    gitignore_path = Path.cwd() / ".gitignore"

    if gitignore_path.exists():
        try:
            content = gitignore_path.read_text()
            # Check for various patterns that would ignore the directory
            if ".unity-bridge" in content:
                return  # Already ignored
        except Exception:
            pass  # If we can't read gitignore, show the notice

    print(
        "\nNote: Add '.unity-bridge/' to your .gitignore to avoid committing runtime files.\n",
        file=sys.stderr,
    )


def write_command(action: str, params: Dict[str, Any]) -> str:
    """
    Write command file atomically, return UUID.

    Args:
        action: Command action (run-tests, compile, etc.)
        params: Command parameters dictionary

    Returns:
        Command UUID string

    Raises:
        UnityCommandError: If writing fails
    """
    command_id = str(uuid.uuid4())
    command = {"id": command_id, "action": action, "params": params}

    # Security: Ensure UNITY_DIR is not a symlink (prevent symlink attacks)
    if UNITY_DIR.exists() and UNITY_DIR.is_symlink():
        raise UnityCommandError("Security error: .unity-bridge cannot be a symlink")

    # Ensure directory exists
    dir_existed = UNITY_DIR.exists()
    try:
        UNITY_DIR.mkdir(parents=True, exist_ok=True)
        if sys.platform != "win32":
            os.chmod(UNITY_DIR, 0o700)
    except Exception as e:
        raise UnityCommandError(f"Failed to create Unity directory: {e}")

    # Notify about gitignore on first directory creation
    if not dir_existed:
        check_gitignore_and_notify()

    # Atomic write using temp file
    command_file = UNITY_DIR / "command.json"
    temp_file = command_file.with_suffix(".tmp")

    try:
        temp_file.write_text(json.dumps(command, indent=2))
        if sys.platform != "win32":
            os.chmod(temp_file, 0o600)
        temp_file.replace(command_file)
    except Exception as e:
        # Cleanup temp file if it exists
        if temp_file.exists():
            try:
                temp_file.unlink()
            except Exception:
                pass
        raise UnityCommandError(f"Failed to write command file: {e}")

    return command_id


def wait_for_response(command_id: str, timeout: int, verbose: bool = False) -> Dict[str, Any]:
    """
    Poll for response file with exponential backoff.

    Args:
        command_id: UUID of the command
        timeout: Maximum seconds to wait
        verbose: Print polling progress

    Returns:
        Parsed response dictionary

    Raises:
        CommandTimeoutError: If timeout is reached
        UnityCommandError: If response parsing fails
    """
    _validate_command_id(command_id)
    response_file = UNITY_DIR / f"response-{command_id}.json"

    start = time.time()
    sleep_time = MIN_SLEEP
    attempts = 0

    while time.time() - start < timeout:
        attempts += 1

        if response_file.exists():
            # Wait a bit for Unity to finish writing
            time.sleep(0.1)

            try:
                response_text = response_file.read_text()
                result = json.loads(response_text)
                response_id = result.get("id", "")
                if response_id != command_id:
                    raise UnityCommandError(f"Response ID mismatch: expected {command_id}")
                # Continue polling if command is still running (Unity writes progress updates)
                if result.get("status") == "running":
                    if verbose:
                        progress = result.get("progress", {})
                        current = progress.get("current", 0)
                        total = progress.get("total", 0)
                        current_test = progress.get("currentTest", "")
                        if total > 0:
                            print(
                                f"Tests in progress: {current}/{total} {current_test}",
                                file=sys.stderr,
                            )
                        else:
                            print("Command running...", file=sys.stderr)
                    time.sleep(sleep_time)
                    sleep_time = min(sleep_time * SLEEP_MULTIPLIER, MAX_SLEEP)
                    continue
                return result
            except json.JSONDecodeError:
                # Might have caught it mid-write, retry once
                if verbose:
                    print(
                        "Warning: Failed to parse response, retrying...",
                        file=sys.stderr,
                    )
                time.sleep(0.2)
                try:
                    response_text = response_file.read_text()
                    result = json.loads(response_text)
                    response_id = result.get("id", "")
                    if response_id != command_id:
                        raise UnityCommandError(f"Response ID mismatch: expected {command_id}")
                    # Continue polling if command is still running
                    if result.get("status") == "running":
                        time.sleep(sleep_time)
                        sleep_time = min(sleep_time * SLEEP_MULTIPLIER, MAX_SLEEP)
                        continue
                    return result
                except json.JSONDecodeError as e:
                    # Log raw response for debugging
                    print("Error: Invalid JSON in response file", file=sys.stderr)
                    print(f"Raw response: {response_text}", file=sys.stderr)
                    raise UnityCommandError(f"Failed to parse response JSON: {e}")
            except UnityCommandError:
                raise
            except Exception as e:
                raise UnityCommandError(f"Failed to read response file: {e}")

        if verbose and attempts % 10 == 0:
            elapsed = time.time() - start
            print(f"Waiting for response... ({elapsed:.1f}s)", file=sys.stderr)

        time.sleep(sleep_time)
        sleep_time = min(sleep_time * SLEEP_MULTIPLIER, MAX_SLEEP)

    # Check if Unity directory exists - if not, Unity likely isn't running
    if not UNITY_DIR.exists():
        raise UnityNotRunningError(
            "Unity Editor not detected. Ensure Unity is open with the project loaded."
        )

    raise CommandTimeoutError(
        f"Command timed out after {timeout}s. Check Unity Console for errors."
    )


def format_response(response: Dict[str, Any], action: str) -> str:
    """
    Format response based on command type.

    Args:
        response: Parsed response dictionary
        action: Command action name

    Returns:
        Formatted human-readable string
    """
    status = response.get("status", "unknown")
    duration = response.get("duration_ms", 0)
    duration_sec = duration / 1000.0

    # Handle error status
    if status == "error":
        error_msg = response.get("error", "Unknown error")
        return f"✗ Error: {error_msg}"

    # Format based on action type
    if action == "run-tests":
        return format_test_results(response, status, duration_sec)
    elif action == "compile":
        return format_compile_results(response, status, duration_sec)
    elif action == "get-console-logs":
        return format_console_logs(response)
    elif action == "get-status":
        return format_editor_status(response)
    elif action == "refresh":
        return format_refresh_results(response, status, duration_sec)
    elif action in ("play", "pause", "step"):
        return format_play_mode_result(response, status, duration_sec)
    else:
        # Generic formatting
        return format_generic_response(response, status, duration_sec)


def format_test_results(response: Dict[str, Any], status: str, duration: float) -> str:
    """Format run-tests response"""
    result = response.get("result", {})
    passed = result.get("passed", 0)
    failed = result.get("failed", 0)
    skipped = result.get("skipped", 0)
    failures = result.get("failures", [])

    # Summary
    lines = [
        f"✓ Tests Passed: {passed}",
        f"✗ Tests Failed: {failed}",
        f"○ Tests Skipped: {skipped}",
        f"Duration: {duration:.2f}s",
    ]

    # Failed tests details
    if failed > 0 and failures:
        lines.append("")
        lines.append("Failed Tests:")
        for failure in failures:
            name = failure.get("name", "Unknown test")
            message = failure.get("message", "")
            lines.append(f"  - {name}")
            if message:
                # Try to extract file path from message
                lines.append(f"    {message}")

    return "\n".join(lines)


def format_compile_results(response: Dict[str, Any], status: str, duration: float) -> str:
    """Format compile response"""
    if status == "success":
        return f"✓ Compilation Successful\nDuration: {duration:.2f}s"
    elif status == "failure":
        error = response.get("error", "")
        if error:
            # Try to parse error count from message
            return f"✗ Compilation Failed\n\n{error}"
        return f"✗ Compilation Failed\nDuration: {duration:.2f}s"
    else:
        return f"Compilation Status: {status}\nDuration: {duration:.2f}s"


def format_console_logs(response: Dict[str, Any]) -> str:
    """Format get-console-logs response"""
    logs = response.get("consoleLogs", [])

    if not logs:
        return "No console logs found"

    log_filter = response.get("params", {}).get("filter", "")
    limit = len(logs)

    filter_suffix = f", filtered by {log_filter}" if log_filter else ""
    lines = [f"Console Logs (last {limit}{filter_suffix}):"]
    lines.append("")

    for log in logs:
        log_type = log.get("type", "Log")
        message = log.get("message", "")
        stack_trace = log.get("stackTrace", "")
        count = log.get("count", 1)

        # Format type indicator
        if log_type == "Error":
            indicator = "[Error]"
        elif log_type == "Warning":
            indicator = "[Warning]"
        else:
            indicator = "[Log]"

        # Add count if duplicated
        if count > 1:
            indicator += f" (x{count})"

        lines.append(f"{indicator} {message}")

        if stack_trace:
            # Indent stack trace
            for line in stack_trace.split("\n"):
                if line.strip():
                    lines.append(f"  {line}")

        lines.append("")

    return "\n".join(lines)


def format_editor_status(response: Dict[str, Any]) -> str:
    """Format get-status response"""
    status = response.get("editorStatus")

    if status is None:
        return "Unity Editor Status: Unknown (missing editorStatus field)"

    is_compiling = status.get("isCompiling", False)
    is_updating = status.get("isUpdating", False)
    is_playing = status.get("isPlaying", False)
    is_paused = status.get("isPaused", False)

    lines = ["Unity Editor Status:"]

    # Compilation status
    if is_compiling:
        lines.append("  - Compilation: ⏳ Compiling...")
    else:
        lines.append("  - Compilation: ✓ Ready")

    # Play mode status
    if is_playing:
        if is_paused:
            lines.append("  - Play Mode: ⏸ Paused")
        else:
            lines.append("  - Play Mode: ▶ Playing")
    else:
        lines.append("  - Play Mode: ✏ Editing")

    # Update status
    if is_updating:
        lines.append("  - Updating: ⏳ Yes")
    else:
        lines.append("  - Updating: No")

    return "\n".join(lines)


def format_refresh_results(response: Dict[str, Any], status: str, duration: float) -> str:
    """Format refresh response"""
    if status == "success":
        return f"✓ Asset Database Refreshed\nDuration: {duration:.2f}s"
    elif status == "failure":
        error = response.get("error", "Unknown error")
        return f"✗ Refresh Failed: {error}\nDuration: {duration:.2f}s"
    else:
        return f"Refresh Status: {status}\nDuration: {duration:.2f}s"


def format_play_mode_result(response: Dict[str, Any], status: str, duration: float) -> str:
    """Format play/pause/step response"""
    action = response.get("action", "unknown")
    editor_status = response.get("editorStatus")

    if status == "success" and editor_status:
        is_playing = editor_status.get("isPlaying", False)
        is_paused = editor_status.get("isPaused", False)
        if is_playing:
            state = "⏸ Paused" if is_paused else "▶ Playing"
        else:
            state = "⏹ Stopped"
        return f"✓ {action} completed\nPlay Mode: {state}\nDuration: {duration:.2f}s"
    elif status == "failure":
        error = response.get("error", "Unknown error")
        return f"✗ {action} failed: {error}\nDuration: {duration:.2f}s"
    else:
        return format_generic_response(response, status, duration)


def format_generic_response(response: Dict[str, Any], status: str, duration: float) -> str:
    """Generic response formatting"""
    action = response.get("action", "unknown")

    if status == "success":
        return f"✓ {action} completed successfully\nDuration: {duration:.2f}s"
    elif status == "failure":
        error = response.get("error", "Unknown error")
        return f"✗ {action} failed: {error}\nDuration: {duration:.2f}s"
    else:
        return f"{action} status: {status}\nDuration: {duration:.2f}s"


def cleanup_old_responses(max_age_hours: int = 1, verbose: bool = False):
    """
    Remove stale response and temp files.

    Args:
        max_age_hours: Maximum age in hours before cleanup
        verbose: Print cleanup progress
    """
    if not UNITY_DIR.exists():
        return

    current_time = time.time()
    max_age_seconds = max_age_hours * 3600
    cleaned = 0

    for pattern in ("response-*.json", "*.tmp"):
        for stale_file in UNITY_DIR.glob(pattern):
            try:
                file_age = current_time - stale_file.stat().st_mtime
                if file_age > max_age_seconds:
                    stale_file.unlink()
                    cleaned += 1
                    if verbose:
                        print(f"Cleaned up: {stale_file.name}", file=sys.stderr)
            except Exception as e:
                if verbose:
                    print(
                        f"Warning: Failed to cleanup {stale_file.name}: {e}",
                        file=sys.stderr,
                    )

    if verbose and cleaned > 0:
        print(f"Cleaned up {cleaned} old file(s)", file=sys.stderr)


def cleanup_stale_command_file(timeout: int, verbose: bool = False):
    """
    Remove stale command.json if older than timeout period.

    This handles the case where a previous command was written but Unity
    wasn't running to process it.

    Args:
        timeout: Command timeout in seconds (used as staleness threshold)
        verbose: Print cleanup progress
    """
    command_file = UNITY_DIR / "command.json"
    try:
        if command_file.exists():
            file_age = time.time() - command_file.stat().st_mtime
            if file_age > timeout:
                command_file.unlink()
                if verbose:
                    print(
                        f"Cleaned up stale command file ({file_age:.0f}s old)",
                        file=sys.stderr,
                    )
    except Exception as e:
        if verbose:
            print(
                f"Warning: Failed to cleanup stale command file: {e}",
                file=sys.stderr,
            )


def cleanup_response_file(command_id: str, verbose: bool = False):
    """
    Remove response file after successful read.

    Args:
        command_id: UUID of the command
        verbose: Print cleanup progress
    """
    _validate_command_id(command_id)
    response_file = UNITY_DIR / f"response-{command_id}.json"

    try:
        if response_file.exists():
            response_file.unlink()
            if verbose:
                print(f"Cleaned up response file: {response_file.name}", file=sys.stderr)
    except Exception as e:
        if verbose:
            print(f"Warning: Failed to cleanup response file: {e}", file=sys.stderr)


def execute_command(
    action: str,
    params: Dict[str, Any],
    timeout: int,
    cleanup: bool = False,
    verbose: bool = False,
) -> str:
    """
    Execute Unity command and return formatted response.

    Args:
        action: Command action
        params: Command parameters
        timeout: Timeout in seconds
        cleanup: Unused, kept for backwards compatibility
        verbose: Print progress messages

    Returns:
        Formatted response string

    Raises:
        UnityCommandError: On execution errors
    """
    # Always cleanup old responses and stale command files
    cleanup_old_responses(verbose=verbose)
    cleanup_stale_command_file(timeout, verbose=verbose)

    # Write command
    if verbose:
        print(f"Writing command: {action}", file=sys.stderr)
    command_id = write_command(action, params)

    # Wait for response
    if verbose:
        print(f"Command ID: {command_id}", file=sys.stderr)
        print(f"Waiting for response (timeout: {timeout}s)...", file=sys.stderr)

    try:
        response = wait_for_response(command_id, timeout, verbose)
        formatted = format_response(response, action)
        return formatted
    finally:
        cleanup_response_file(command_id, verbose)


def get_skill_source_dir() -> Optional[Path]:
    """
    Find the skill directory bundled with this package.

    Returns:
        Path to the skill directory, or None if not found.
    """
    # Try using importlib.resources (Python 3.9+)
    try:
        import importlib.resources as resources

        # For Python 3.9+
        if hasattr(resources, "files"):
            package_dir = resources.files("claude_unity_bridge")
            skill_dir = Path(str(package_dir)) / "skill"
            if skill_dir.exists():
                return skill_dir
    except (ImportError, TypeError):
        pass

    # Fallback: use __file__ to locate the package
    package_dir = Path(__file__).parent
    skill_dir = package_dir / "skill"
    if skill_dir.exists():
        return skill_dir

    return None


def get_claude_skills_dir() -> Path:
    """Get the Claude Code skills directory path."""
    return Path.home() / ".claude" / "skills"


def get_skill_target_dir() -> Path:
    """Get the target directory for the unity-bridge skill."""
    return get_claude_skills_dir() / "unity-bridge"


def install_skill(verbose: bool = False) -> int:
    """
    Install the Claude Code skill by creating a symlink (with copy fallback on Windows).

    Returns:
        Exit code (0 for success, 1 for error).
    """
    source_dir = get_skill_source_dir()
    if source_dir is None:
        print("Error: Could not find skill files in package.", file=sys.stderr)
        print("This may indicate a corrupted installation.", file=sys.stderr)
        print(
            "Try reinstalling: pip install --force-reinstall claude-unity-bridge",
            file=sys.stderr,
        )
        return EXIT_ERROR

    if verbose:
        print(f"Skill source: {source_dir}", file=sys.stderr)

    # Verify SKILL.md exists
    skill_md = source_dir / "SKILL.md"
    if not skill_md.exists():
        print(f"Error: SKILL.md not found at {skill_md}", file=sys.stderr)
        return EXIT_ERROR

    # Create skills directory if it doesn't exist
    skills_dir = get_claude_skills_dir()
    try:
        skills_dir.mkdir(parents=True, exist_ok=True)
    except Exception as e:
        print(f"Error: Could not create skills directory: {e}", file=sys.stderr)
        return EXIT_ERROR

    target_dir = get_skill_target_dir()

    # Remove existing installation
    if target_dir.exists() or target_dir.is_symlink():
        if target_dir.is_symlink():
            if verbose:
                print(f"Removing existing symlink: {target_dir}", file=sys.stderr)
            try:
                target_dir.unlink()
            except Exception as e:
                print(f"Error: Could not remove existing symlink: {e}", file=sys.stderr)
                return EXIT_ERROR
        elif target_dir.is_dir():
            if verbose:
                print(f"Removing existing directory: {target_dir}", file=sys.stderr)
            try:
                shutil.rmtree(target_dir)
            except Exception as e:
                print(f"Error: Could not remove existing directory: {e}", file=sys.stderr)
                return EXIT_ERROR
        else:
            # Regular file
            if verbose:
                print(f"Removing existing file: {target_dir}", file=sys.stderr)
            try:
                target_dir.unlink()
            except Exception as e:
                print(f"Error: Could not remove existing file: {e}", file=sys.stderr)
                return EXIT_ERROR

    # Try to create symlink first
    used_copy_fallback = False
    try:
        target_dir.symlink_to(source_dir)
        if verbose:
            print(f"Created symlink: {target_dir} -> {source_dir}", file=sys.stderr)
    except (OSError, NotImplementedError) as e:
        # Symlink creation failed (likely Windows permissions or unsupported filesystem)
        # Fall back to directory copy
        if verbose:
            print(f"Symlink creation failed: {e}", file=sys.stderr)
            print("Falling back to directory copy...", file=sys.stderr)

        try:
            shutil.copytree(source_dir, target_dir)
            used_copy_fallback = True
            if verbose:
                print(f"Copied skill files to: {target_dir}", file=sys.stderr)
        except Exception as copy_error:
            print(
                f"Error: Could not create symlink or copy directory: {copy_error}",
                file=sys.stderr,
            )
            if platform.system() == "Windows":
                print()
                print("On Windows, symlinks require either:", file=sys.stderr)
                print("  - Administrator privileges, or", file=sys.stderr)
                print(
                    "  - Developer Mode enabled (Settings > Update & Security > For developers)",
                    file=sys.stderr,
                )
            return EXIT_ERROR

    # Success message
    if used_copy_fallback:
        print(f"✓ Skill installed (copy): {target_dir}")
        print()
        print("Note: Using directory copy instead of symlink.")
        print("To update the skill, re-run: python -m claude_unity_bridge.cli install-skill")
        if platform.system() == "Windows":
            print()
            print("To enable symlinks (optional), enable Developer Mode:")
            print("  Settings > Update & Security > For developers > Developer Mode")
    else:
        print(f"✓ Skill installed (symlink): {target_dir} -> {source_dir}")

    print()
    print("The Claude Code skill is now available.")
    print("Restart Claude Code to load the skill, then ask Claude naturally:")
    print('  "Run the Unity tests"')
    print('  "Check for compilation errors"')
    print()

    return EXIT_SUCCESS


def uninstall_skill(verbose: bool = False) -> int:
    """
    Uninstall the Claude Code skill by removing the symlink or copied directory.

    Returns:
        Exit code (0 for success, 1 for error).
    """
    target_dir = get_skill_target_dir()

    if not target_dir.exists() and not target_dir.is_symlink():
        print("Skill is not installed.")
        return EXIT_SUCCESS

    if target_dir.is_symlink():
        try:
            target_dir.unlink()
            print(f"✓ Skill uninstalled: removed symlink {target_dir}")
            return EXIT_SUCCESS
        except Exception as e:
            print(f"Error: Could not remove symlink: {e}", file=sys.stderr)
            return EXIT_ERROR
    elif target_dir.is_dir():
        # Check if this is a copied skill directory (contains SKILL.md)
        if (target_dir / "SKILL.md").exists():
            try:
                shutil.rmtree(target_dir)
                print(f"✓ Skill uninstalled: removed directory {target_dir}")
                return EXIT_SUCCESS
            except Exception as e:
                print(f"Error: Could not remove directory: {e}", file=sys.stderr)
                return EXIT_ERROR
        else:
            # Directory exists but doesn't look like our skill
            print(
                f"Warning: {target_dir} exists but doesn't appear to be a skill installation.",
                file=sys.stderr,
            )
            print("Remove it manually if desired:", file=sys.stderr)
            if platform.system() == "Windows":
                print(f"  rmdir /s {target_dir}", file=sys.stderr)
            else:
                print(f"  rm -rf {target_dir}", file=sys.stderr)
            return EXIT_ERROR
    else:
        # Regular file
        print(
            f"Warning: {target_dir} exists but is not a symlink or directory.",
            file=sys.stderr,
        )
        print("Remove it manually if desired:", file=sys.stderr)
        if platform.system() == "Windows":
            print(f"  del {target_dir}", file=sys.stderr)
        else:
            print(f"  rm {target_dir}", file=sys.stderr)
        return EXIT_ERROR


def update_package(verbose: bool = False) -> int:
    """
    Update the package via pip and reinstall the skill.

    Returns:
        Exit code (0 for success, 1 for error).
    """
    print("Updating claude-unity-bridge...")

    try:
        # Run pip install --upgrade
        result = subprocess.run(
            [
                sys.executable,
                "-m",
                "pip",
                "install",
                "--upgrade",
                "claude-unity-bridge",
            ],
            capture_output=not verbose,
            text=True,
        )

        if result.returncode != 0:
            print("Error: pip upgrade failed.", file=sys.stderr)
            if not verbose and result.stderr:
                print(result.stderr, file=sys.stderr)
            return EXIT_ERROR

        if verbose:
            print("Package upgraded successfully.", file=sys.stderr)

    except Exception as e:
        print(f"Error: Could not run pip: {e}", file=sys.stderr)
        return EXIT_ERROR

    # Reinstall skill to ensure symlink points to updated package
    print("Reinstalling skill...")
    return install_skill(verbose)


def execute_health_check(timeout: int, verbose: bool) -> int:
    """Verify Unity Bridge is set up correctly."""
    print("Checking Unity Bridge setup...")

    # Check 1: Does .unity-bridge directory exist?
    if not UNITY_DIR.exists():
        print("✗ Unity Bridge not detected")
        print(f"  Directory not found: {UNITY_DIR}")
        print("  Is Unity Editor open with the bridge package installed?")
        return EXIT_ERROR
    print(f"✓ Bridge directory exists: {UNITY_DIR}")

    # Check 2: Can we communicate with Unity?
    try:
        result = execute_command("get-status", {}, timeout=min(timeout, 5), verbose=verbose)
        print("✓ Unity Editor is responding")
        if verbose:
            print(result)
        return EXIT_SUCCESS
    except UnityNotRunningError:
        print("✗ Unity Editor not responding")
        print("  Ensure Unity is open and the project is loaded")
        return EXIT_ERROR
    except CommandTimeoutError:
        print("✗ Unity Editor timed out")
        return EXIT_TIMEOUT


def main():
    parser = argparse.ArgumentParser(
        description="Execute Unity Editor commands via Claude Unity Bridge",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Unity Commands:
  run-tests          Run Unity tests
  compile            Trigger script compilation
  refresh            Refresh asset database
  get-status         Get editor status
  get-console-logs   Get Unity console logs
  play               Toggle Play Mode (enter/exit)
  pause              Toggle pause (while in Play Mode)
  step               Step one frame (while in Play Mode)
  health-check       Verify Unity Bridge setup

Skill Commands:
  install-skill      Install Claude Code skill
  uninstall-skill    Uninstall Claude Code skill
  update             Update package and reinstall skill

Examples:
  %(prog)s run-tests --mode EditMode --filter "MyTests"
  %(prog)s compile
  %(prog)s get-console-logs --limit 20 --filter Error
  %(prog)s get-status
  %(prog)s refresh
  %(prog)s play
  %(prog)s pause
  %(prog)s step
  %(prog)s health-check
  %(prog)s install-skill
        """,
    )

    parser.add_argument(
        "command",
        choices=[
            "run-tests",
            "compile",
            "refresh",
            "get-status",
            "get-console-logs",
            "play",
            "pause",
            "step",
            "health-check",
            "install-skill",
            "uninstall-skill",
            "update",
        ],
        help="Command to execute",
    )

    # Test command options
    parser.add_argument(
        "--mode", choices=["EditMode", "PlayMode"], help="Test mode (for run-tests)"
    )
    parser.add_argument(
        "--filter",
        help="Test filter pattern (for run-tests) or log type filter (for get-console-logs)",
    )

    # Console logs options
    parser.add_argument(
        "--limit",
        type=int,
        help="Maximum number of logs to retrieve (for get-console-logs)",
    )

    # General options
    parser.add_argument(
        "--timeout",
        type=int,
        default=DEFAULT_TIMEOUT,
        help=f"Command timeout in seconds (default: {DEFAULT_TIMEOUT})",
    )
    parser.add_argument(
        "--cleanup",
        action="store_true",
        help="Cleanup old response files before executing",
    )
    parser.add_argument("--verbose", action="store_true", help="Print verbose progress messages")

    args = parser.parse_args()

    # Handle skill management commands first (they don't need timeout validation)
    if args.command == "install-skill":
        return install_skill(args.verbose)

    if args.command == "uninstall-skill":
        return uninstall_skill(args.verbose)

    if args.command == "update":
        return update_package(args.verbose)

    # Validate timeout (only for Unity commands)
    if args.timeout <= 0:
        parser.error("--timeout must be a positive integer")

    # Validate limit if provided
    if args.limit is not None:
        if args.limit < MIN_LIMIT or args.limit > MAX_LIMIT:
            parser.error(f"--limit must be between {MIN_LIMIT} and {MAX_LIMIT}")

    # Handle health-check command separately
    if args.command == "health-check":
        return execute_health_check(args.timeout, args.verbose)

    # Build parameters based on command
    params = {}

    if args.command == "run-tests":
        if args.mode:
            params["testMode"] = args.mode
        if args.filter:
            params["filter"] = args.filter

    elif args.command == "get-console-logs":
        if args.limit is not None:
            # Send as string for compatibility with C# JsonUtility which expects string
            params["limit"] = str(args.limit)
        if args.filter:
            params["filter"] = args.filter

    # Execute command
    try:
        result = execute_command(
            action=args.command,
            params=params,
            timeout=args.timeout,
            cleanup=args.cleanup,
            verbose=args.verbose,
        )
        print(result)
        return EXIT_SUCCESS

    except CommandTimeoutError as e:
        print(f"Error: {e}", file=sys.stderr)
        return EXIT_TIMEOUT

    except UnityNotRunningError as e:
        print(f"Error: {e}", file=sys.stderr)
        return EXIT_ERROR

    except UnityCommandError as e:
        print(f"Error: {e}", file=sys.stderr)
        return EXIT_ERROR

    except KeyboardInterrupt:
        print("\nInterrupted by user", file=sys.stderr)
        return EXIT_ERROR

    except Exception as e:
        print(f"Unexpected error: {e}", file=sys.stderr)
        if args.verbose:
            import traceback

            traceback.print_exc()
        return EXIT_ERROR


if __name__ == "__main__":
    sys.exit(main())

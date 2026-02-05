#!/usr/bin/env python3
"""
Claude Unity Bridge - Command Execution Script

Rock-solid, deterministic command execution for Unity Editor operations.
Handles UUID generation, file-based polling, response parsing, and cleanup.
"""

import argparse
import json
import sys
import time
import uuid
from pathlib import Path
from typing import Dict, Any

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
                return json.loads(response_text)
            except json.JSONDecodeError:
                # Might have caught it mid-write, retry once
                if verbose:
                    print("Warning: Failed to parse response, retrying...", file=sys.stderr)
                time.sleep(0.2)
                try:
                    response_text = response_file.read_text()
                    return json.loads(response_text)
                except json.JSONDecodeError as e:
                    # Log raw response for debugging
                    print("Error: Invalid JSON in response file", file=sys.stderr)
                    print(f"Raw response: {response_text}", file=sys.stderr)
                    raise UnityCommandError(f"Failed to parse response JSON: {e}")
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
    Remove stale response files.

    Args:
        max_age_hours: Maximum age in hours before cleanup
        verbose: Print cleanup progress
    """
    if not UNITY_DIR.exists():
        return

    current_time = time.time()
    max_age_seconds = max_age_hours * 3600
    cleaned = 0

    for response_file in UNITY_DIR.glob("response-*.json"):
        try:
            file_age = current_time - response_file.stat().st_mtime
            if file_age > max_age_seconds:
                response_file.unlink()
                cleaned += 1
                if verbose:
                    print(f"Cleaned up: {response_file.name}", file=sys.stderr)
        except Exception as e:
            if verbose:
                print(f"Warning: Failed to cleanup {response_file.name}: {e}", file=sys.stderr)

    if verbose and cleaned > 0:
        print(f"Cleaned up {cleaned} old response file(s)", file=sys.stderr)


def cleanup_response_file(command_id: str, verbose: bool = False):
    """
    Remove response file after successful read.

    Args:
        command_id: UUID of the command
        verbose: Print cleanup progress
    """
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
    action: str, params: Dict[str, Any], timeout: int, cleanup: bool = False, verbose: bool = False
) -> str:
    """
    Execute Unity command and return formatted response.

    Args:
        action: Command action
        params: Command parameters
        timeout: Timeout in seconds
        cleanup: Whether to cleanup old responses first
        verbose: Print progress messages

    Returns:
        Formatted response string

    Raises:
        UnityCommandError: On execution errors
    """
    # Cleanup old responses if requested
    if cleanup:
        cleanup_old_responses(verbose=verbose)

    # Write command
    if verbose:
        print(f"Writing command: {action}", file=sys.stderr)
    command_id = write_command(action, params)

    # Wait for response
    if verbose:
        print(f"Command ID: {command_id}", file=sys.stderr)
        print(f"Waiting for response (timeout: {timeout}s)...", file=sys.stderr)

    response = wait_for_response(command_id, timeout, verbose)

    # Format response
    formatted = format_response(response, action)

    # Cleanup response file
    cleanup_response_file(command_id, verbose)

    return formatted


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
Commands:
  run-tests          Run Unity tests
  compile            Trigger script compilation
  refresh            Refresh asset database
  get-status         Get editor status
  get-console-logs   Get Unity console logs
  health-check       Verify Unity Bridge setup

Examples:
  %(prog)s run-tests --mode EditMode --filter "MyTests"
  %(prog)s compile
  %(prog)s get-console-logs --limit 20 --filter Error
  %(prog)s get-status
  %(prog)s refresh
  %(prog)s health-check
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
            "health-check",
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
        "--limit", type=int, help="Maximum number of logs to retrieve (for get-console-logs)"
    )

    # General options
    parser.add_argument(
        "--timeout",
        type=int,
        default=DEFAULT_TIMEOUT,
        help=f"Command timeout in seconds (default: {DEFAULT_TIMEOUT})",
    )
    parser.add_argument(
        "--cleanup", action="store_true", help="Cleanup old response files before executing"
    )
    parser.add_argument("--verbose", action="store_true", help="Print verbose progress messages")

    args = parser.parse_args()

    # Validate timeout
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

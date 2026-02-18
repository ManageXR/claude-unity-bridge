# Changelog

All notable changes to the Claude Unity Bridge package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.1] - 2026-02-17

### Added

- `play` command - Toggle Play Mode on/off
- `pause` command - Toggle pause while in Play Mode
- `step` command - Advance one frame while paused
- `IEditorPlayMode` interface abstracting `EditorApplication` for testability
- Full Moq-based test coverage for play/pause/step commands

### Fixed

- Play command response now reports intended play mode state instead of stale pre-transition value

## [0.2.0] - 2026-02-11

### Fixed

- Windows symlink permission error (`[WinError 1314]`) during skill installation (#13)
  - Implement symlink-with-copy-fallback strategy
  - Gracefully fall back to `shutil.copytree()` when symlink creation fails
  - Installation now succeeds on Windows without Administrator privileges or Developer Mode
- Handle both symlinks and copied directories in `uninstall-skill` command
- Unicode encoding error on Windows console (replaced checkmarks with `[OK]`)
- CLI returning prematurely when Unity writes progress updates (#32)
  - Now polls through "running" status with exponential backoff instead of returning early
  - Fixes issue where test results showed 0/0/0 while tests were still executing
  - Added progress info display in verbose mode

### Added

- `install.ps1` PowerShell installer for native Windows support
- Platform-aware error messaging (uses `rmdir /s` on Windows, `rm -rf` on Unix)
- User notification about copy fallback with Developer Mode instructions
- Comprehensive test coverage for copy fallback scenarios:
  - `test_install_skill_copy_fallback_on_symlink_failure`
  - `test_install_skill_copy_fallback_failure`
  - `test_uninstall_skill_removes_copied_directory`
  - `test_uninstall_skill_warns_on_non_skill_directory`
- Updated existing tests to handle both symlinks and copied directories on Windows
- Windows-specific troubleshooting section in installation documentation

### Changed

- `install_skill()` now removes existing installations (files, directories, or symlinks) before installing
- `uninstall_skill()` validates directory contains `SKILL.md` before removal

## [0.1.5] - 2026-02-10

### Fixed

- Allow read-only commands (`get-status`, `get-console-logs`) while Unity is compiling or updating — mutating commands return an explicit error instead of timing out
- Always clean up stale response, temp, and command files at the start of every command (no longer gated behind `--cleanup` flag)
- Clean up response files on timeout and error via try/finally, not just on success
- Include `.tmp` files in TTL sweep alongside `response-*.json`
- Remove orphaned `command.json` files older than timeout before writing new commands
- Clean up stale files on Unity Editor startup

### Added

- Contributor Covenant Code of Conduct

## [0.1.4] - 2026-02-09

### Security

- Restrict `.unity-bridge/` directory and file permissions to owner-only on POSIX systems
- Add UUID validation for command IDs to prevent path traversal attacks
- Verify response ID matches expected command ID to prevent response spoofing
- Pin GitHub Actions to commit SHAs to prevent supply chain attacks
- Pin pre-commit hooks to commit SHAs

### Added

- Dependabot configuration for automated dependency updates (GitHub Actions + pip)
- `.gitignore` entries for `.unity-bridge/`, `.env`, and secret file patterns
- CODEOWNERS for `@ManageXR/unity-external` team

### Changed

- Bumped dev dependencies: black 25.11.0, pytest 8.4.2, pytest-cov 7.0.0, flake8 7.3.0, pre-commit 4.3.0
- Bumped CI dependencies: actions/checkout 6.0.2, actions/setup-python 6.2.0, codecov/codecov-action 5.5.2

## [0.1.3] - 2026-02-06

### Fixed

- Handle regular file (not just directory) at install target path
- Sync all version files (pyproject.toml, __init__.py, package.json)

### Added

- Tests for `update_package` function (success, pip failure, subprocess exception, CLI integration)
- Test for regular file detection in `install_skill`

## [0.1.2] - 2026-02-05

### Added

- PyPI publish workflow (automated on tag push)
- Bootstrap installer (`install.sh`) for one-command setup
- `install-skill` and `update` CLI subcommands
- PATH detection and auto-configuration in installer

### Changed

- Promoted one-liner install as primary installation approach
- Excluded CHANGELOG.md.meta from package distribution

## [0.1.1] - 2026-02-04

### Fixed

- Corrected license reference in skill documentation
- Fixed Unity project folder reference in contributing guide

## [0.1.0] - 2026-01-22

### Added

- Initial pre-release of Claude Unity Bridge as a standalone Unity package
- File-based bridge enabling Claude Code to trigger Unity Editor operations
- `run-tests` command - Execute EditMode or PlayMode tests with filtering
- `compile` command - Trigger script compilation
- `refresh` command - Force asset database refresh
- `get-status` command - Check editor compilation/update state
- `get-console-logs` command - Retrieve Unity console output with filtering
- Multi-project support via per-project `.unity-bridge/` directories
- Tools menu with status display and cleanup utilities
- Zero external dependencies - pure C# implementation
- Support for Unity 2021.3 and later

### Features

- Command/response protocol using JSON file exchange
- Progress reporting for long-running operations
- Automatic cleanup of old response files
- Console log retrieval with type filtering (Log, Warning, Error)
- Configurable log retrieval limits
- Reflection-based access to Unity's internal console API

### Architecture

- Command pattern for extensibility
- Assembly definition for clean integration
- Editor-only package (no runtime impact)
- Automatic initialization via `[InitializeOnLoad]`

[0.2.1]: https://github.com/ManageXR/claude-unity-bridge/releases/tag/v0.2.1
[0.2.0]: https://github.com/ManageXR/claude-unity-bridge/releases/tag/v0.2.0
[0.1.5]: https://github.com/ManageXR/claude-unity-bridge/releases/tag/v0.1.5
[0.1.4]: https://github.com/ManageXR/claude-unity-bridge/releases/tag/v0.1.4
[0.1.3]: https://github.com/ManageXR/claude-unity-bridge/releases/tag/v0.1.3
[0.1.2]: https://github.com/ManageXR/claude-unity-bridge/releases/tag/v0.1.2
[0.1.1]: https://github.com/ManageXR/claude-unity-bridge/releases/tag/v0.1.1
[0.1.0]: https://github.com/ManageXR/claude-unity-bridge/releases/tag/v0.1.0

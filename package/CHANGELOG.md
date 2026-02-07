# Changelog

All notable changes to the Claude Unity Bridge package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[0.1.3]: https://github.com/ManageXR/claude-unity-bridge/releases/tag/v0.1.3
[0.1.2]: https://github.com/ManageXR/claude-unity-bridge/releases/tag/v0.1.2
[0.1.1]: https://github.com/ManageXR/claude-unity-bridge/releases/tag/v0.1.1
[0.1.0]: https://github.com/ManageXR/claude-unity-bridge/releases/tag/v0.1.0

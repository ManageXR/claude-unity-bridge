# Changelog

All notable changes to the Claude Unity Bridge package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-01-22

### Added

- Initial pre-release of Claude Unity Bridge as a standalone Unity package
- File-based bridge enabling Claude Code to trigger Unity Editor operations
- `run-tests` command - Execute EditMode or PlayMode tests with filtering
- `compile` command - Trigger script compilation
- `refresh` command - Force asset database refresh
- `get-status` command - Check editor compilation/update state
- `get-console-logs` command - Retrieve Unity console output with filtering
- Multi-project support via per-project `.claude/unity/` directories
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

[0.1.0]: https://github.com/ManageXR/claude-unity-bridge/releases/tag/v0.1.0

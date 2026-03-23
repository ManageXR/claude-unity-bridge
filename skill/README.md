# Claude Unity Bridge

[![PyPI](https://img.shields.io/pypi/v/claude-unity-bridge)](https://pypi.org/project/claude-unity-bridge/)
[![Python 3.8+](https://img.shields.io/pypi/pyversions/claude-unity-bridge)](https://pypi.org/project/claude-unity-bridge/)
[![CI](https://github.com/ManageXR/claude-unity-bridge/actions/workflows/test-skill.yml/badge.svg)](https://github.com/ManageXR/claude-unity-bridge/actions/workflows/test-skill.yml)
[![codecov](https://codecov.io/gh/ManageXR/claude-unity-bridge/graph/badge.svg?token=3PHF2GXHON)](https://codecov.io/gh/ManageXR/claude-unity-bridge)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://github.com/ManageXR/claude-unity-bridge/blob/main/LICENSE)
[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-black.svg)](https://unity.com/)
[![PyPI Downloads](https://img.shields.io/pypi/dm/claude-unity-bridge)](https://pypi.org/project/claude-unity-bridge/)

> File-based bridge enabling [Claude Code](https://docs.anthropic.com/en/docs/claude-code) to control Unity Editor operations in a running editor instance.

## Why Claude Unity Bridge?

- **Zero config** — No network setup, no port conflicts. Just install and go.
- **Deterministic CLI** — Tested Python script handles UUIDs, polling, retries, and cleanup so Claude doesn't have to.
- **Multi-project** — Each Unity project gets its own `.unity-bridge/` directory. Work on multiple projects simultaneously.
- **Full editor control** — Run tests, compile, check logs, refresh assets, build, and control Play Mode.
- **Cross-platform** — macOS, Linux, and Windows support.

## Installation

**Install the CLI:**

```bash
pip install claude-unity-bridge
unity-bridge install-skill
```

Or use the one-line installer:

```bash
# macOS / Linux / Git Bash
curl -sSL https://raw.githubusercontent.com/ManageXR/claude-unity-bridge/main/install.sh | bash

# Windows (PowerShell)
irm https://raw.githubusercontent.com/ManageXR/claude-unity-bridge/main/install.ps1 | iex
```

**Add the Unity package** (in Unity Editor):

`Window > Package Manager > + > Add package from git URL...`

```
https://github.com/ManageXR/claude-unity-bridge.git?path=package
```

## Quick Start

Open Claude Code in your Unity project directory and ask naturally:

```
"Run the Unity tests"
"Check for compilation errors"
"Show me the error logs"
"Build for Android"
```

Or use the CLI directly:

```bash
unity-bridge run-tests --mode EditMode
unity-bridge compile
unity-bridge get-console-logs --limit 10
unity-bridge get-status
unity-bridge build --target Android
```

## Commands

| Command | Description |
|---------|-------------|
| `run-tests` | Execute EditMode or PlayMode tests with optional filters |
| `compile` | Trigger script compilation and report errors |
| `get-console-logs` | Retrieve Unity console output (filter by Log/Warning/Error) |
| `get-status` | Check editor state — compilation, play mode, updating |
| `refresh` | Force asset database refresh |
| `play` / `pause` / `step` | Control Play Mode — enter, pause, step frames |
| `build` | Build project with direct pipeline or custom method |

## How It Works

```
Claude Code  -->  unity-bridge CLI  -->  .unity-bridge/command.json
                                                  |
                                            Unity Editor
                                                  |
Claude Code  <--  unity-bridge CLI  <--  .unity-bridge/response-{id}.json
```

1. Claude Code (or you) runs a `unity-bridge` command
2. The CLI writes a JSON command with a unique UUID
3. Unity Editor polls for and executes the command
4. The CLI polls for the response with exponential backoff
5. Results are formatted and displayed; response files are cleaned up

All file I/O is atomic (temp file + rename) to prevent corruption. The CLI handles file locking, retries, and stale file cleanup automatically.

## Updating

```bash
unity-bridge update
```

## Documentation

- [Installation Options](https://github.com/ManageXR/claude-unity-bridge/blob/main/docs/INSTALLATION.md) — Alternative installation methods
- [Usage Guide](https://github.com/ManageXR/claude-unity-bridge/blob/main/docs/USAGE.md) — Command formats and response details
- [Architecture](https://github.com/ManageXR/claude-unity-bridge/blob/main/docs/ARCHITECTURE.md) — Project structure and design
- [Command Reference](https://github.com/ManageXR/claude-unity-bridge/blob/main/skill/references/COMMANDS.md) — Complete command specification

## License

[Apache 2.0](https://github.com/ManageXR/claude-unity-bridge/blob/main/LICENSE)

# 🌉 Claude Unity Bridge

[![PyPI](https://img.shields.io/pypi/v/claude-unity-bridge)](https://pypi.org/project/claude-unity-bridge/)
[![PyPI Downloads](https://img.shields.io/pypi/dm/claude-unity-bridge)](https://pypi.org/project/claude-unity-bridge/)
[![GitHub release](https://img.shields.io/github/v/release/ManageXR/claude-unity-bridge)](https://github.com/ManageXR/claude-unity-bridge/releases)
[![CI](https://github.com/ManageXR/claude-unity-bridge/actions/workflows/test-skill.yml/badge.svg)](https://github.com/ManageXR/claude-unity-bridge/actions/workflows/test-skill.yml)
[![codecov](https://codecov.io/gh/ManageXR/claude-unity-bridge/graph/badge.svg?token=3PHF2GXHON)](https://codecov.io/gh/ManageXR/claude-unity-bridge)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-black.svg)
![Python 3.8+](https://img.shields.io/badge/Python-3.8%2B-blue.svg)

File-based bridge enabling [Claude Code](https://docs.anthropic.com/en/docs/claude-code) to trigger Unity Editor operations in a running editor instance.

## ✨ Features

- **Run Tests** — Execute EditMode or PlayMode tests
- **Compile** — Trigger script compilation
- **Refresh** — Force asset database refresh
- **Get Status** — Check editor compilation/update state
- **Get Console Logs** — Retrieve Unity console output
- **Play Mode Control** — Play, pause, and step through frames

## 🚀 Quick Start

### 1. Install

**macOS / Linux / Git Bash:**
```bash
curl -sSL https://raw.githubusercontent.com/ManageXR/claude-unity-bridge/main/install.sh | bash
```

**Windows (PowerShell):**
```powershell
irm https://raw.githubusercontent.com/ManageXR/claude-unity-bridge/main/install.ps1 | iex
```

### 2. Add to Your Unity Project(s)

In Unity: `Window > Package Manager > + > Add package from git URL...`

```
https://github.com/ManageXR/claude-unity-bridge.git?path=package
```

### 3. Use It

Open Claude Code in your Unity project directory:

```
"Run the Unity tests"
"Check for compilation errors"
"Show me the error logs"
```

Or use the CLI directly:

```bash
unity-bridge run-tests --mode EditMode
unity-bridge compile
unity-bridge get-console-logs --limit 10
```

### Updating

```bash
unity-bridge update
```

## ⚙️ How It Works

```
Claude Code → unity-bridge CLI → .unity-bridge/command.json → Unity Editor → response.json
```

1. Claude Code (or you) runs `unity-bridge` commands
2. The CLI writes commands to `.unity-bridge/command.json`
3. Unity Editor polls and executes commands
4. Results appear in `.unity-bridge/response-{id}.json`

Each Unity project has its own `.unity-bridge/` directory, enabling multi-project support.

## 📚 Documentation

- [Installation Options](docs/INSTALLATION.md) — Alternative installation methods
- [Usage Guide](docs/USAGE.md) — Command formats and response details
- [Architecture](docs/ARCHITECTURE.md) — Project structure and design
- [Skill Reference](skill/SKILL.md) — Claude Code skill documentation
- [Command Reference](skill/references/COMMANDS.md) — Complete command specification

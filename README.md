# 🌉 Claude Unity Bridge

[![GitHub release](https://img.shields.io/github/v/release/ManageXR/claude-unity-bridge)](https://github.com/ManageXR/claude-unity-bridge/releases)
![CI](https://github.com/ManageXR/claude-unity-bridge/actions/workflows/test-skill.yml/badge.svg)
[![codecov](https://codecov.io/gh/ManageXR/claude-unity-bridge/graph/badge.svg?token=3PHF2GXHON)](https://codecov.io/gh/ManageXR/claude-unity-bridge)
![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-black.svg)

File-based bridge enabling Claude Code to trigger Unity Editor operations in a running editor instance.

## ✨ Features

- **Run Tests** — Execute EditMode or PlayMode tests
- **Compile** — Trigger script compilation
- **Refresh** — Force asset database refresh
- **Get Status** — Check editor compilation/update state
- **Get Console Logs** — Retrieve Unity console output

## 🚀 Quick Start

### 1. Install the Unity Package

In Unity: `Window > Package Manager > + > Add package from git URL...`

```
https://github.com/ManageXR/claude-unity-bridge.git?path=package
```

### 2. Install the Claude Code Skill (optional)

```bash
ln -s "$(pwd)/skill" ~/.claude/skills/unity
```

Then restart Claude Code. The skill auto-activates in Unity project directories.

### 3. Use It

Ask Claude Code naturally:

- "Run the Unity tests in EditMode"
- "Check if there are any compilation errors"
- "Show me the error logs from Unity"

## ⚙️ How It Works

1. Claude Code writes commands to `.claude/unity/command.json`
2. Unity Editor polls and executes commands
3. Results appear in `.claude/unity/response-{id}.json`

Each Unity project has its own `.claude/unity/` directory, enabling multi-project support.

## 📚 Documentation

- [Installation Options](docs/INSTALLATION.md) — Alternative installation methods
- [Usage Guide](docs/USAGE.md) — Command formats and response details
- [Architecture](docs/ARCHITECTURE.md) — Project structure and design
- [Skill Reference](skill/SKILL.md) — Claude Code skill documentation
- [Command Reference](skill/references/COMMANDS.md) — Complete command specification

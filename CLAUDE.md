# Claude Agent Instructions

**READ THIS FIRST**: All agent guidelines, coding standards, and workflows are documented in `AGENTS.md`.

Before working on this repository, you must read:

1. **[AGENTS.md](AGENTS.md)** - Complete agent guidelines (READ THIS)
2. **[README.md](README.md)** - Package documentation and protocol specification
3. **[skill/SKILL.md](skill/SKILL.md)** - Claude Code skill documentation

## Critical Rules

From AGENTS.md, these are the most important rules:

### NEVER:
- Modify `cli.py` without updating pytest tests
- Use in-context file I/O for Unity commands (always use the Python script)
- Write to Unity project files while Unity Editor is running
- Remove error handling from command implementations

### ALWAYS:
- Run `pytest tests/test_cli.py -v` before committing Python changes
- Use the deterministic Python script for all Unity commands
- Update documentation when changing behavior
- Follow the 3-commit structure for features (core, docs, testing)

## Quick Start

```bash
# Test the Python skill script
cd skill
pytest tests/test_cli.py -v

# Test with Unity (requires Unity Editor running)
python3 scripts/cli.py get-status
```

## Why This Matters

The Unity Bridge uses a **deterministic Python script** (`skill/scripts/cli.py`) as the foundation of reliability. This script guarantees consistent UUID generation, file handling, polling behavior, and error handling across all Claude sessions.

Without reading AGENTS.md, you risk:
- Breaking the deterministic guarantee
- Introducing file handling bugs
- Skipping critical tests
- Violating the protocol specification

---

**📖 READ [AGENTS.md](AGENTS.md) NOW** - It contains all the context you need to work effectively on this project.

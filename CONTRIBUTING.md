# Contributing to Claude Unity Bridge

Welcome! We appreciate your interest in contributing to the Claude Unity Bridge. This guide will help you get started.

## How to Contribute

- **Bug Reports**: Open an issue with a clear description, steps to reproduce, and expected vs actual behavior
- **Feature Requests**: Open an issue describing the use case and proposed solution
- **Pull Requests**: Fork the repo, create a branch, make changes, and submit a PR
- **Questions**: Open a discussion or issue for general questions

## Development Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/your-fork/claude-unity-bridge.git
   cd claude-unity-bridge
   ```

2. **Install Python dependencies**
   ```bash
   pip install pytest
   ```

3. **Open Unity project** (for testing Unity components)
   - Open the `UnityProject` folder in Unity 2021.3 or later

## Testing Requirements

All contributions must pass existing tests. Before submitting a PR:

### Python Skill Tests
```bash
cd skill
pytest tests/ -v
```

### Unity Tests
1. Open the project in Unity 2021.3+
2. Open Window > General > Test Runner
3. Run all tests in Edit Mode and Play Mode

## Code Style

- Follow existing patterns and conventions in the codebase
- Keep changes focused and minimal
- No major refactors without prior discussion in an issue
- Maintain the deterministic behavior of `unity_command.py`

## PR Process

1. **Fork** the repository
2. **Create a branch** from `main` with a descriptive name (e.g., `fix/timeout-handling`, `feat/new-command`)
3. **Make your changes** with clear, atomic commits
4. **Run all tests** to ensure nothing is broken
5. **Submit a PR** against `main` with a clear description of the changes

## Code of Conduct

- Be respectful and constructive in all interactions
- Welcome newcomers and help them get started
- Focus on the technical merits of contributions
- Assume good intent from other contributors

Thank you for contributing!

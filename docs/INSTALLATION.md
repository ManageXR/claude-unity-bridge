# Installation

## Requirements

- Unity 2021.3 or later
- Python 3.8 or later (for the CLI)

## Quick Install

### macOS / Linux / Git Bash

```bash
curl -sSL https://raw.githubusercontent.com/ManageXR/claude-unity-bridge/main/install.sh | bash
```

### Windows (PowerShell)

```powershell
irm https://raw.githubusercontent.com/ManageXR/claude-unity-bridge/main/install.ps1 | iex
```

Both installers:
- Install the `claude-unity-bridge` pip package
- Add Python scripts directory to your PATH
- Install the Claude Code skill

**Windows Note:** On Windows without Developer Mode, the skill is installed as a directory copy instead of a symlink. This works seamlessly, but updates require re-running `unity-bridge install-skill`.

## Manual Install

If you prefer not to use curl, or need more control:

```bash
pip install claude-unity-bridge
unity-bridge install-skill
```

## Verify Installation

```bash
unity-bridge --help
unity-bridge health-check
```

## Unity Package

### Via Package Manager (Git URL)

1. Open Unity Editor
2. Go to `Window > Package Manager`
3. Click the `+` button in the top-left corner
4. Select `Add package from git URL...`
5. Enter: `https://github.com/ManageXR/claude-unity-bridge.git?path=package`
6. Click `Add`

### Via Package Manager (Local Path)

For development or testing:

1. Clone this repository to your local machine
2. Open Unity Editor
3. Go to `Window > Package Manager`
4. Click the `+` button in the top-left corner
5. Select `Add package from disk...`
6. Navigate to the `package/` folder and select `package.json`
7. Click `Open`

### Via manifest.json

Add this line to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.mxr.claude-bridge": "https://github.com/ManageXR/claude-unity-bridge.git?path=package"
  }
}
```

## CLI Commands

### Skill Management

```bash
# Install the Claude Code skill
unity-bridge install-skill

# Uninstall the Claude Code skill
unity-bridge uninstall-skill

# Update package and reinstall skill
unity-bridge update
```

The skill is installed to `~/.claude/skills/unity-bridge/`:
- **macOS/Linux/Windows with Developer Mode:** Installed as a symlink pointing to the bundled skill files in the pip package (updates automatically with package)
- **Windows without Developer Mode:** Installed as a directory copy (requires re-running `unity-bridge install-skill` after updates)

To enable symlinks on Windows 10/11, enable Developer Mode in Settings > Update & Security > For developers.

### Unity Commands

```bash
unity-bridge run-tests --mode EditMode
unity-bridge compile
unity-bridge get-console-logs --limit 10
unity-bridge get-status
unity-bridge refresh
unity-bridge health-check
```

## Updating

```bash
unity-bridge update
```

This runs `pip install --upgrade claude-unity-bridge` and reinstalls the skill to ensure the symlink points to the updated package.

Or manually:

```bash
pip install --upgrade claude-unity-bridge
unity-bridge install-skill
```

## Uninstalling

```bash
unity-bridge uninstall-skill
pip uninstall claude-unity-bridge
```

## Development Installation

For contributing to the project:

```bash
git clone https://github.com/ManageXR/claude-unity-bridge.git
cd claude-unity-bridge/skill
pip install -e ".[dev]"
unity-bridge install-skill
```

## Troubleshooting

### PATH Issues

If `unity-bridge` is not found after pip install, the pip scripts directory may not be in your PATH. You can:

1. Add the pip scripts directory to your PATH
2. Use the module directly: `python -m claude_unity_bridge.cli install-skill`

### Windows Symlink Permissions

On Windows, creating symlinks requires either:
- Administrator privileges, or
- Developer Mode enabled

If you see `[WinError 1314] A required privilege is not held by the client`, the installer will automatically fall back to copying the skill directory. This works perfectly fine, but updates require re-running `unity-bridge install-skill`.

**To enable symlinks (optional):**
1. Open Settings > Update & Security > For developers
2. Enable "Developer Mode"
3. Restart your terminal
4. Run `unity-bridge install-skill` again

### Python Version

Make sure you're using Python 3.8 or later:

```bash
python --version
# or
python3 --version
```

# Installation

## Requirements

- Unity 2021.3 or later
- Python 3.8 or later (for the CLI)

## Unity Bridge CLI

### Via pip (Recommended)

```bash
pip install claude-unity-bridge
```

### For Development

```bash
cd claude-unity-bridge/skill
pip install -e ".[dev]"
```

### Verify Installation

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
    "com.managexr.claude-bridge": "https://github.com/ManageXR/claude-unity-bridge.git?path=package"
  }
}
```

## Claude Code Skill

For a better user experience, install the companion Claude Code skill.

### Option 1: Using GNU Stow (recommended)

If you have [GNU Stow](https://www.gnu.org/software/stow/) installed:

```bash
cd /path/to/claude-unity-bridge
stow -t ~/.claude/skills/unity -d . skill
```

To uninstall:
```bash
stow -t ~/.claude/skills/unity -d . -D skill
```

### Option 2: Using symlink

```bash
ln -s "$(pwd)/skill" ~/.claude/skills/unity
```

### Option 3: Copy (not recommended)

```bash
cp -r skill ~/.claude/skills/unity
```

Both Stow and symlink keep the skill updated automatically when you pull changes from git.

Restart Claude Code after installation. The skill will auto-activate when you're working in a Unity project directory.

#!/bin/bash
# Claude Unity Bridge - Quick Installer
#
# Usage:
#   curl -sSL https://raw.githubusercontent.com/ManageXR/claude-unity-bridge/main/install.sh | bash
#
# Or download and run:
#   ./install.sh

set -e

echo "Installing Claude Unity Bridge..."
echo

# Check Python
if ! command -v python3 &> /dev/null; then
    echo "Error: Python 3 is required but not installed."
    exit 1
fi

# Install via pip
echo "Installing pip package..."
python3 -m pip install --upgrade claude-unity-bridge

# Install skill
echo
echo "Installing Claude Code skill..."
python3 -m claude_unity_bridge.cli install-skill

echo
echo "Installation complete!"
echo

# Check if unity-bridge is on PATH
if command -v unity-bridge &> /dev/null; then
    echo "Unity commands:"
    echo "  unity-bridge run-tests"
    echo "  unity-bridge compile"
    echo "  unity-bridge get-console-logs"
else
    echo "Note: 'unity-bridge' is not on your PATH."
    echo "You can either:"
    echo "  1. Add Python scripts to PATH (recommended):"
    echo "     export PATH=\"\$PATH:\$(python3 -m site --user-base)/bin\""
    echo "  2. Or use the module directly:"
    echo "     python3 -m claude_unity_bridge.cli <command>"
    echo
    echo "The Claude Code skill works regardless - just ask Claude naturally."
fi

echo
echo "Next steps:"
echo "  1. Add the Unity package to your project:"
echo "     Window > Package Manager > + > Add package from git URL..."
echo "     https://github.com/ManageXR/claude-unity-bridge.git?path=package"
echo
echo "  2. Open Claude Code in your Unity project directory"
echo
echo "  3. Ask Claude naturally: \"Run the Unity tests\""
echo

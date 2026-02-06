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
python3 -m pip install claude-unity-bridge

# Install skill
echo
echo "Installing Claude Code skill..."
python3 -m claude_unity_bridge.cli install-skill

echo
echo "Installation complete!"
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

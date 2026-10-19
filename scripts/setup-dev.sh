#!/usr/bin/env bash
# Installs the .NET 10 SDK on Ubuntu if `dotnet` is missing. Idempotent.
set -euo pipefail

if ! command -v dotnet >/dev/null 2>&1 && [ -x "$HOME/.dotnet/dotnet" ]; then
    export PATH="$HOME/.dotnet:$PATH"
fi

if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks | grep -q '^10\.'; then
    echo ".NET 10 SDK already installed."
else
    SUDO=""
    if [ "$(id -u)" -ne 0 ] && command -v sudo >/dev/null 2>&1; then
        SUDO="sudo"
    fi

    if $SUDO apt-get update -y && $SUDO apt-get install -y dotnet-sdk-10.0; then
        echo "Installed dotnet-sdk-10.0 via apt."
    else
        echo "apt install failed, falling back to dotnet-install.sh."
        curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
        bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
        export PATH="$HOME/.dotnet:$PATH"
        echo "Add to your shell profile: export PATH=\"\$HOME/.dotnet:\$PATH\""
    fi
fi

dotnet --info

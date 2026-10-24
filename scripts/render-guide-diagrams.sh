#!/usr/bin/env bash
# Renders the Mermaid diagrams of the user guide: docs/user-guide/img/<name>.<lang>.mmd -> <name>.<lang>.svg.
# White background so the diagram stays readable in the app's dark theme.
# The in-app help shows the SVG files (it cannot run Mermaid offline), so commit both files after editing a diagram.
# Uses `mmdc` if installed, otherwise `npx @mermaid-js/mermaid-cli`. Needs a Chrome/Chromium; set
# PUPPETEER_EXECUTABLE_PATH if mermaid-cli cannot find one.
set -euo pipefail

cd "$(dirname "$0")/.."
img="docs/user-guide/img"

if command -v mmdc >/dev/null 2>&1; then
    mmdc_cmd=(mmdc)
else
    mmdc_cmd=(npx --yes @mermaid-js/mermaid-cli)
fi

config="$(mktemp)"
trap 'rm -f "$config"' EXIT
# Containers and CI run as root, where Chromium refuses to start without --no-sandbox.
echo '{"args":["--no-sandbox"]}' > "$config"

shopt -s nullglob
sources=("$img"/*.mmd)
if [ ${#sources[@]} -eq 0 ]; then
    echo "No .mmd files in $img" >&2
    exit 1
fi

for source in "${sources[@]}"; do
    target="${source%.mmd}.svg"
    echo "Rendering $source -> $target"
    "${mmdc_cmd[@]}" -p "$config" -b white -i "$source" -o "$target"
done

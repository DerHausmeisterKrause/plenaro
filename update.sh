#!/usr/bin/env bash
# One intentional maintenance command; no background updater or Git hook.
set -Eeuo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")"
if [[ "$(git symbolic-ref --quiet --short HEAD)" != main ]]; then
  echo "Bitte zuerst auf den main-Branch wechseln." >&2
  exit 1
fi
lock=$(git rev-parse --git-path plenaro-update.lock)
exec 9>"$lock"
flock -n 9 || { echo "Ein Plenaro-Update läuft bereits." >&2; exit 1; }
if [[ -n "$(git status --porcelain --untracked-files=no)" ]]; then
  echo "Lokale Änderungen an versionierten Dateien: Update abgebrochen." >&2
  exit 1
fi
echo "=== GitHub main aktualisieren ==="
git pull --ff-only origin main
if [[ "$(git rev-parse HEAD)" != "$(git rev-parse FETCH_HEAD)" ]]; then
  echo "Lokaler main enthält eigene Commits. Kein Deployment." >&2
  exit 1
fi
# Execute the newly fetched implementation, not the old shell's cached code.
exec bash tools/update-stack.sh "$@"

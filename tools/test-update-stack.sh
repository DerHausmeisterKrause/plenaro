#!/usr/bin/env bash
# Test update ordering, active profiles and fail-closed behavior without
# touching Docker, registries, production data or the real checkout.
set -euo pipefail
root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
tmp=$(mktemp -d)
trap 'rc=$?; if ((rc!=0)); then cat "$tmp/output" "$tmp/docker.log" 2>/dev/null || true; fi; rm -rf -- "$tmp"; exit "$rc"' EXIT
mkdir -p "$tmp/bin" "$tmp/local/tools"
cp "$root/tools/update-stack.sh" "$tmp/local/tools/"
cp "$root/update.sh" "$tmp/local/"
cat >"$tmp/bin/docker" <<'MOCK'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "$MOCK_LOG"
if [[ "$1" == run ]]; then
  echo "postgres (PostgreSQL) ${MOCK_TARGET_MAJOR:-17}.12"
  exit 0
fi
[[ "$1" == compose ]] || exit 99
shift
while [[ "${1:-}" == --profile ]]; do shift 2; done
case "$*" in
  'ps --services --status running') printf 'database\nweb\nnginx\n'; if [[ "${MOCK_CLAM:-1}" == 1 ]]; then echo clamav; fi;;
  'config --services') printf 'database\nweb\nnginx\n'; if [[ "${MOCK_CONFIG_BROWSER:-0}" == 1 ]]; then echo favorites-login; fi;;
  'config --quiet') ;;
  'config --images database') echo postgres:17-alpine;;
  'exec -T database postgres --version') echo 'postgres (PostgreSQL) 17.11';;
  'pull --ignore-buildable') [[ "${MOCK_FAIL:-}" != pull ]];;
  'build --pull --no-cache') [[ "${MOCK_FAIL:-}" != build ]];;
  'exec -T database sh -c '*)
    printf '%s\n' '-- PostgreSQL test dump'
    [[ "${MOCK_FAIL:-}" != backup ]];;
  'up -d --no-build --pull never --wait --wait-timeout 300') [[ "${MOCK_FAIL:-}" != up ]];;
  'restart nginx'|'up -d --no-deps --no-build --pull never --wait --wait-timeout 120 nginx'|'ps'|'exec -T nginx nginx -v'|'exec -T web dotnet --list-runtimes'|'exec -T clamav clamd --version') ;;
  *) echo "Unexpected mock command: $*" >&2; exit 99;;
esac
MOCK
chmod +x "$tmp/bin/docker"
export PATH="$tmp/bin:$PATH"
export MOCK_LOG="$tmp/docker.log"
export PLENARO_BACKUP_DIR="$tmp/backups"
run_update(){ bash "$tmp/local/tools/update-stack.sh" >"$tmp/output" 2>&1; }
assert_no_deploy(){ ! grep -q ' up -d ' "$MOCK_LOG" && ! grep -q ' restart nginx' "$MOCK_LOG"; }
: >"$MOCK_LOG"
MOCK_CONFIG_BROWSER=1 run_update
grep -q -- '--profile attachment-scan --profile browser-login pull --ignore-buildable' "$MOCK_LOG"
grep -q -- '--profile attachment-scan --profile browser-login build --pull --no-cache' "$MOCK_LOG"
grep -q 'restart nginx' "$MOCK_LOG"
grep -q 'Update erfolgreich' "$tmp/output"
[[ "$(find "$PLENARO_BACKUP_DIR" -name '*.sql.gz' | wc -l)" -eq 1 ]]
gzip -dc "$PLENARO_BACKUP_DIR"/*.sql.gz | grep -q 'PostgreSQL test dump'
[[ "$(stat -c %a "$PLENARO_BACKUP_DIR")" == 700 ]]
[[ "$(stat -c %a "$PLENARO_BACKUP_DIR"/*.sql.gz)" == 600 ]]
python3 - "$MOCK_LOG" <<'PY'
import sys
lines=open(sys.argv[1]).read().splitlines()
def locate(part):
    return next(i for i,line in enumerate(lines) if part in line)
assert locate("pull --ignore-buildable") < locate("build --pull") < locate("pg_dumpall") < locate("up -d") < locate("restart nginx")
PY
for failure in pull build backup; do
  : >"$MOCK_LOG"
  if MOCK_FAIL="$failure" run_update; then echo "Expected $failure failure"; exit 1; fi
  assert_no_deploy
  [[ "$(find "$PLENARO_BACKUP_DIR" -name '*.sql.gz' | wc -l)" -eq 1 ]]
  [[ -z "$(find "$PLENARO_BACKUP_DIR" -name '.plenaro-*' -print)" ]]
done
: >"$MOCK_LOG"
if MOCK_TARGET_MAJOR=18 run_update; then echo 'Expected major-version rejection'; exit 1; fi
assert_no_deploy
: >"$MOCK_LOG"
if MOCK_FAIL=up run_update; then echo 'Expected deployment failure'; exit 1; fi
! grep -q 'restart nginx' "$MOCK_LOG"
grep -q 'DB-Backup:' "$tmp/output"
: >"$MOCK_LOG"
MOCK_CLAM=0 run_update
! grep -q -- '--profile attachment-scan' "$MOCK_LOG"

# Exercise the Git wrapper against a real local remote. The freshly fetched
# deployment script must be executed; a dirty checkout must be refused.
git init -q -b main "$tmp/local"
git -C "$tmp/local" add update.sh tools/update-stack.sh
git -C "$tmp/local" -c user.name=Test -c user.email=test@example.org commit -qm initial
git clone -q --bare "$tmp/local" "$tmp/origin.git"
git -C "$tmp/local" remote add origin "$tmp/origin.git"
git clone -q "$tmp/origin.git" "$tmp/publisher"
printf 'echo fresh-script-marker\n' >> "$tmp/publisher/tools/update-stack.sh"
git -C "$tmp/publisher" add tools/update-stack.sh
git -C "$tmp/publisher" -c user.name=Test -c user.email=test@example.org commit -qm update
git -C "$tmp/publisher" push -q origin main
: >"$MOCK_LOG"
bash "$tmp/local/update.sh" >"$tmp/output" 2>&1
grep -q fresh-script-marker "$tmp/output"
printf '# local edit\n' >>"$tmp/local/update.sh"
: >"$MOCK_LOG"
if bash "$tmp/local/update.sh" >"$tmp/output" 2>&1; then echo 'Expected dirty checkout rejection'; exit 1; fi
[[ ! -s "$MOCK_LOG" ]]
echo 'Stack update safety tests passed.'

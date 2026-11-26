#!/usr/bin/env bash
# Called by update.sh after the fast-forward pull. Existing installations only.
set -Eeuo pipefail
umask 077
cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
if (($#)); then
  echo "Aufruf: bash update.sh (keine Argumente)." >&2
  exit 2
fi
stage="Vorbereitung"
backup=""
partial=""
cleanup(){
  local rc=$?
  if [[ -n "$partial" ]]; then rm -f -- "$partial"; fi
  if ((rc!=0)); then
    echo "Update abgebrochen: $stage (Exit $rc)." >&2
    if [[ -n "$backup" ]]; then echo "DB-Backup: $backup" >&2; fi
    echo "Keine automatische Rückmigration. Status: docker compose --profile '*' ps" >&2
  fi
}
trap cleanup EXIT

# Preserve enabled profiles from the environment/.env as well as optional
# services that were explicitly started on a previous deployment.
running=$(docker compose --profile '*' ps --services --status running)
profiles=()
if grep -Fxq clamav <<<"$running"; then profiles+=(--profile attachment-scan); fi
if grep -Fxq favorites-login <<<"$running"; then profiles+=(--profile browser-login); fi
compose(){ docker compose "${profiles[@]}" "$@"; }
compose config --quiet
if ! grep -Fxq database <<<"$running"; then
  echo "Datenbank läuft nicht. Bitte zuerst die bestehende Installation starten." >&2
  exit 1
fi
current_pg=$(compose exec -T database postgres --version)
if [[ ! "$current_pg" =~ PostgreSQL\)\ 17\. ]]; then
  echo "Dieses Update unterstützt nur PostgreSQL 17. Kein Major-Upgrade." >&2
  exit 1
fi

# Download and compile first. A failed pull/build does not replace any
# running container. --no-cache also refreshes apt and package-install layers.
stage="Docker-Images beziehen"
echo "=== Images aktualisieren ==="
compose pull --ignore-buildable
pg_image=$(compose config --images database)
if [[ -z "$pg_image" || "$pg_image" == *$'\n'* ]]; then
  echo "Datenbank-Image nicht eindeutig." >&2
  exit 1
fi
new_pg=$(docker run --rm --network none --entrypoint postgres "$pg_image" --version)
if [[ ! "$new_pg" =~ PostgreSQL\)\ 17\. ]]; then
  echo "Ziel-Image ist nicht PostgreSQL 17. Kein Deployment." >&2
  exit 1
fi
stage="Web-/Worker-Build"
echo "=== Web und aktivierte Build-Container neu bauen ==="
compose build --pull --no-cache

stage="Datenbankbackup"
backup_dir=${PLENARO_BACKUP_DIR:-/var/backups/plenaro}
if [[ "$backup_dir" != /* ]]; then
  echo "PLENARO_BACKUP_DIR muss ein absoluter Pfad sein." >&2
  exit 1
fi
install -d -m 700 -- "$backup_dir"
backup_dir=$(realpath -- "$backup_dir")
case "$backup_dir/" in "$PWD/"*) echo "Backup außerhalb des Git-Repositories speichern." >&2; exit 1;; esac
chmod 700 -- "$backup_dir"
partial=$(mktemp "$backup_dir/.plenaro-XXXXXX.sql.gz")
echo "=== Datenbank sichern ==="
compose exec -T database sh -c 'PGPASSWORD="$POSTGRES_PASSWORD" pg_dumpall --no-password --username="$POSTGRES_USER"' | gzip -c >"$partial"
gzip -t "$partial"
# Require a nonempty decompressed stream without a pipefail/SIGPIPE race.
size=$(gzip -dc "$partial" | wc -c)
if ((size==0)); then echo "Leeres Datenbankbackup." >&2; exit 1; fi
backup="$backup_dir/plenaro-$(date -u +%Y%m%dT%H%M%SZ)-${partial##*.plenaro-}"
mv -- "$partial" "$backup"
partial=""
echo "DB-Backup: $backup"

stage="Stack aktualisieren"
echo "=== Stack aktualisieren (Volumes bleiben erhalten) ==="
compose up -d --no-build --pull never --wait --wait-timeout 300
# NGINX must re-resolve the web container and reload bind-mounted templates.
stage="NGINX neu laden und Healthchecks"
compose restart nginx
compose up -d --no-deps --no-build --pull never --wait --wait-timeout 120 nginx
compose ps
echo "=== Installierte Versionen ==="
compose exec -T nginx nginx -v
compose exec -T database postgres --version
compose exec -T web dotnet --list-runtimes
if grep -Fxq clamav <<<"$(compose config --services)"; then
  compose exec -T clamav clamd --version
fi
echo "Plenaro-Update erfolgreich. Backup: $backup"

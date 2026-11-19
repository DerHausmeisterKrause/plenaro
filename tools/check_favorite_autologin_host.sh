#!/usr/bin/env bash
# Diagnose one isolated Plenaro personal-favorite hostname WITHOUT credentials.
# Run this on the Docker/NGINX host (and verify DNS once more on the client).
set -euo pipefail

host="${1:-}"
if [[ ! "$host" =~ ^site-[a-fA-F0-9]{32}(-plenaro\.intern\.test\.org|\.apps-plenaro\.intern\.test\.org)$ ]]; then
  cat >&2 <<'USAGE'
Verwendung:
  bash tools/check_favorite_autologin_host.sh site-<32-HEX>-plenaro.intern.test.org
  oder:
  bash tools/check_favorite_autologin_host.sh site-<32-HEX>.apps-plenaro.intern.test.org

Dieses Skript prueft nur DNS, HTTPS und Proxy-Health; niemals Passwoerter.
USAGE
  exit 2
fi

echo "1/3 DNS des dedizierten Favoriten-Hosts: $host"
if ! records="$(getent ahostsv4 "$host")"; then
  echo >&2 "DNS nicht vorhanden. Lege im internen DNS einen A-/CNAME-Eintrag"
  echo >&2 "fuer DIESEN Host oder ein geeignetes Wildcard-DNS an."
  exit 1
fi
printf '%s\n' "$records" | sed -n '1p'

echo "2/3 HTTPS-Zertifikat und NGINX-Proxy"
if ! body="$(curl --fail --silent --show-error --max-time 12 "https://$host/_plenaro/favorite-health")"; then
  echo >&2 "HTTPS-/TLS-/Proxy-Fehler. Pruefe DNS vom Client, Zertifikat-SAN,"
  echo >&2 "NGINX server_name, Weiterleitung und die gewaehlte Hostvorlage."
  exit 1
fi
if [[ "$body" != "plenaro-favorite-proxy-ready" ]]; then
  echo >&2 "Falsche HTTPS-Antwort: Der Alias landet nicht am Plenaro-Favoriten-Proxy."
  exit 1
fi

echo "3/3 Proxy-Health OK: $host"
cat <<'INFO'
Jetzt erst die dedizierte Origin fuer persoenliche Favoriten aktivieren:
  PLENARO_FAVORITES_PERSONAL_DEDICATED=true
  docker compose up -d --force-recreate web

WICHTIG: Der Test auf dem Docker-Host beweist NICHT, dass der Browser-
Client denselben Host aufloest und dem Zertifikat vertraut. Auf dem
Windows-Client nslookup <hostname> und den HTTPS-Health-Link pruefen.

Danach ATOSS-Favorit 'Neu verbinden'. In F12/Netzwerk muss auf der
HTML-Dokumentantwort X-Plenaro-Autofill: dedicated-dom stehen.
Dieser Header bestaetigt die Injektion, NICHT den Login-Erfolg.
INFO

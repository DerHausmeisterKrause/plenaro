#!/usr/bin/env bash
# Verify that both fullchain options configure NGINX without weakening TLS.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
temp="$(mktemp -d)"
trap 'rm -rf "$temp"' EXIT

openssl req -x509 -nodes -newkey rsa:2048 -days 1 \
  -keyout "$temp/issuer.key" -out "$temp/issuer.crt" \
  -subj '/CN=CI Intermediate' \
  -addext 'basicConstraints=critical,CA:TRUE' >/dev/null 2>&1
openssl req -nodes -newkey rsa:2048 \
  -keyout "$temp/leaf.key" -out "$temp/leaf.csr" \
  -subj '/CN=portal.example.org' >/dev/null 2>&1
openssl x509 -req -in "$temp/leaf.csr" -CA "$temp/issuer.crt" \
  -CAkey "$temp/issuer.key" -CAcreateserial -days 1 \
  -out "$temp/leaf.crt" >/dev/null 2>&1
openssl verify -CAfile "$temp/issuer.crt" "$temp/leaf.crt" | grep -q OK

docker run --rm --entrypoint sh \
  -v "$root/deploy/nginx:/etc/plenaro-nginx:ro" \
  -v "$temp/leaf.crt:/etc/nginx/tls/fullchain.pem:ro" \
  -v "$temp/leaf.key:/etc/nginx/tls/privkey.pem:ro" \
  -v "$temp/issuer.crt:/etc/nginx/tls/intermediates.pem:ro" \
  -e PLENARO_HTTPS_ENABLED=true -e PLENARO_SERVER_NAME=portal.example.org \
  nginx:stable-alpine -ec '
  /etc/plenaro-nginx/entrypoint.sh >/tmp/boot.log 2>&1 &
  pid=$!
  trap "kill $pid 2>/dev/null || true" EXIT
  tries=0
  while [ ! -s /tmp/plenaro-fullchain.pem ] && [ "$tries" -lt 30 ]; do
    sleep 0.1
    tries=$((tries + 1))
  done
  test -s /tmp/plenaro-fullchain.pem || { cat /tmp/boot.log; exit 1; }
  test "$(grep -c "BEGIN CERTIFICATE" /tmp/plenaro-fullchain.pem)" -eq 2
  grep -q "ssl_certificate /tmp/plenaro-fullchain.pem;" /etc/nginx/conf.d/default.conf
  nginx -t >/dev/null 2>&1 || { cat /tmp/boot.log; exit 1; }
  '

# Existing fullchain setup remains unchanged when no separate chain is mounted.
cat "$temp/leaf.crt" "$temp/issuer.crt" > "$temp/existing-fullchain.pem"
docker run --rm --entrypoint sh \
  -v "$root/deploy/nginx:/etc/plenaro-nginx:ro" \
  -v "$temp/existing-fullchain.pem:/etc/nginx/tls/fullchain.pem:ro" \
  -v "$temp/leaf.key:/etc/nginx/tls/privkey.pem:ro" \
  -e PLENARO_HTTPS_ENABLED=true -e PLENARO_SERVER_NAME=portal.example.org \
  nginx:stable-alpine -ec '
  /etc/plenaro-nginx/entrypoint.sh >/tmp/boot.log 2>&1 &
  pid=$!
  trap "kill $pid 2>/dev/null || true" EXIT
  tries=0
  while ! grep -q "ssl_certificate /etc/nginx/tls/fullchain.pem;" /etc/nginx/conf.d/default.conf 2>/dev/null && [ "$tries" -lt 30 ]; do
    sleep 0.1
    tries=$((tries + 1))
  done
  grep -q "ssl_certificate /etc/nginx/tls/fullchain.pem;" /etc/nginx/conf.d/default.conf
  nginx -t >/dev/null 2>&1 || { cat /tmp/boot.log; exit 1; }
  '
echo "CI checkpoint: existing fullchain and optional intermediate chain both configure NGINX successfully"

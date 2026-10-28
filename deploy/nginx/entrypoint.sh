#!/bin/sh
set -eu

case "${PLENARO_HTTPS_ENABLED:-false}" in
  true)
    if [ ! -s /etc/nginx/tls/fullchain.pem ] || [ ! -s /etc/nginx/tls/privkey.pem ]; then
      echo >&2 "Plenaro: HTTPS aktiviert, aber Zertifikat oder Schlüssel fehlt/ist leer. PLENARO_TLS_CERT_PATH und PLENARO_TLS_KEY_PATH in .env setzen."
      exit 1
    fi
    template=/etc/plenaro-nginx/https.conf.template
    ;;
  false)
    template=/etc/plenaro-nginx/http.conf.template
    ;;
  *)
    echo >&2 "PLENARO_HTTPS_ENABLED muss true oder false sein."
    exit 1
    ;;
esac

# Replace the configured NGINX server_name only; retain $host and $remote_addr.
envsubst '$PLENARO_SERVER_NAME' < "$template" > /etc/nginx/conf.d/default.conf
nginx -t
exec nginx -g 'daemon off;'

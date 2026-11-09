#!/bin/sh
set -eu

case "${PLENARO_HTTPS_ENABLED:-false}" in
  true)
    if [ ! -s /etc/nginx/tls/fullchain.pem ] || [ ! -s /etc/nginx/tls/privkey.pem ]; then
      echo >&2 "Plenaro: HTTPS aktiviert, aber Zertifikat oder Schlüssel fehlt/ist leer. PLENARO_TLS_CERT_PATH und PLENARO_TLS_KEY_PATH in .env setzen."
      exit 1
    fi
    # NGINX must present the complete intermediate chain for a second
    # verifying reverse proxy. A leaf-only cert works with curl -k but
    # produces 502 upstream certificate verify errors.
    PLENARO_TLS_EFFECTIVE_CERT=/etc/nginx/tls/fullchain.pem
    if [ -s /etc/nginx/tls/intermediates.pem ]; then
      if ! grep -q -- '-----BEGIN CERTIFICATE-----' /etc/nginx/tls/intermediates.pem; then
        echo >&2 "Plenaro TLS: PLENARO_TLS_CHAIN_PATH must contain PEM intermediate certificates."
        exit 1
      fi
      { cat /etc/nginx/tls/fullchain.pem; printf '\\n'; cat /etc/nginx/tls/intermediates.pem; } > /tmp/plenaro-fullchain.pem
      PLENARO_TLS_EFFECTIVE_CERT=/tmp/plenaro-fullchain.pem
    fi
    cert_count=$(grep -c -- '-----BEGIN CERTIFICATE-----' "$PLENARO_TLS_EFFECTIVE_CERT" || true)
    if [ "$cert_count" -lt 2 ]; then
      echo >&2 "Plenaro TLS WARNUNG: Zertifikatsdatei hat nur $cert_count PEM-Zertifikat(e). Fuer HTTPS mit pruefendem vorgelagertem NGINX vollstaendige Zwischenzertifikatskette in PLENARO_TLS_CERT_PATH oder PLENARO_TLS_CHAIN_PATH bereitstellen (sonst 502)."
    fi
    export PLENARO_TLS_EFFECTIVE_CERT
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
envsubst '$PLENARO_SERVER_NAME $PLENARO_TLS_EFFECTIVE_CERT' < "$template" > /etc/nginx/conf.d/default.conf
nginx -t
exec nginx -g 'daemon off;'

#!/bin/bash
# Selenium's Chromium uses seluser's NSS shared certificate database.
# Trust the administrator-supplied internal root CA, not arbitrary websites.
# No --ignore-certificate-errors or acceptInsecureCerts is permitted.
set -euo pipefail
ca=/run/plenaro-trust/ca.crt
if [[ -s "$ca" ]]; then
    if ! openssl x509 -in "$ca" -noout -text |
        grep -qE 'CA:TRUE'; then
        echo 'Plenaro CA: expected a trusted root/intermediate CA PEM (CA:TRUE).' >&2
        exit 1
    fi
    db=/home/seluser/.pki/nssdb
    mkdir -p "$db"
    if ! certutil -d "sql:$db" -L >/dev/null 2>&1; then
        certutil -d "sql:$db" -N --empty-password
    fi
    # Replace a previous CA by the same nickname (e.g. after rotation).
    certutil -d "sql:$db" -D -n "Plenaro Internal CA" >/dev/null 2>&1 || true
    certutil -d "sql:$db" -A -n "Plenaro Internal CA" -t 'C,,' -i "$ca"
    echo 'Plenaro CA: internal CA trusted by Chromium NSS.'
fi
exec /opt/bin/entry_point.sh "$@"

#!/bin/sh
# Merge optional trusted corporate CA with public system roots for the .NET
# OpenSSL trust store. The web image runs as its unprivileged APP_UID.
# The host's CA file is mounted read-only; no private keys are copied.
set -eu
ca=/run/plenaro-trust/ca.crt
if [ -s "$ca" ]; then
    if ! grep -q -- '-----BEGIN CERTIFICATE-----' "$ca"; then
        echo "Plenaro CA: expected a PEM certificate file." >&2
        exit 1
    fi
    bundle=/tmp/plenaro-ca-bundle.pem
    { cat /etc/ssl/certs/ca-certificates.crt; printf '\n'; cat "$ca"; } > "$bundle"
    chmod 0600 "$bundle"
    export SSL_CERT_FILE="$bundle"
    echo "Plenaro CA: additional trusted CA bundle activated for the backend."
fi
exec dotnet Plenaro.Api.dll "$@"

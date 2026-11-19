#!/bin/sh
set -eu

# Chromium must validate HTTPS normally, including a user-supplied private
# enterprise CA. This certificate is NOT a private key and is read-only.
if [ -s /run/plenaro-trust/ca.crt ]; then
  cp /run/plenaro-trust/ca.crt /usr/local/share/ca-certificates/plenaro-internal.crt
  update-ca-certificates >/dev/null
fi
# Do not run the browser or the HTTP worker as root.
exec runuser -u pwuser -- node /app/server.mjs

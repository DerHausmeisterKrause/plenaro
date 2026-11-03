#!/usr/bin/env bash
# Integration smoke: no real upstream access and no real website credentials.
set -euo pipefail
API="${1:?API required}"
COOKIE="${2:?cookie required}"
CSRF="${3:?CSRF required}"
json='{"name":"Test Wiki","slug":"wiki","upstreamUrl":"https://wiki.example.org/","enabled":true,"availableToAll":true,"allowedUsers":[]}'
# Authentication, admin role and CSRF must all apply.
test "$(curl -s -o /dev/null -w '%{http_code}' "$API/api/v1/admin/favorite-apps/")" = "401"
test "$(curl -s -o /dev/null -w '%{http_code}' -b "$COOKIE" -X POST "$API/api/v1/admin/favorite-apps/" -H 'Content-Type: application/json' -d "$json")" = "400"
test "$(curl -s -o /dev/null -w '%{http_code}' -b "$COOKIE" -X POST "$API/api/v1/admin/favorite-apps/" -H "X-CSRF-TOKEN: $CSRF" -H 'Content-Type: application/json' -d '{"name":"Bad Host","slug":"bad","upstreamUrl":"http://127.0.0.1/","enabled":true,"availableToAll":true,"allowedUsers":[]}')" = "400"
APP=$(curl -fsS -b "$COOKIE" -X POST "$API/api/v1/admin/favorite-apps/" -H "X-CSRF-TOKEN: $CSRF" -H 'Content-Type: application/json' -d "$json" | python3 -c 'import sys,json; print(json.load(sys.stdin)["id"])')
test -n "$APP"
curl -fsS -b "$COOKIE" "$API/api/v1/favorites/apps" | python3 -c 'import json,sys; a=json.load(sys.stdin); assert len(a)==1 and a[0]["slug"]=="wiki" and "upstreamUrl" not in a[0]'
fav=$(printf '{"appId":"%s","name":"Test Favorite","path":"/","username":"","autoLogin":false,"enabled":true,"sortOrder":0,"password":""}' "$APP")
ID=$(curl -fsS -b "$COOKIE" -X POST "$API/api/v1/favorites/" -H "X-CSRF-TOKEN: $CSRF" -H 'Content-Type: application/json' -d "$fav" | python3 -c 'import sys,json; print(json.load(sys.stdin)["id"])')
curl -fsS -b "$COOKIE" "$API/api/v1/favorites/" | python3 -c 'import sys,json;a=json.load(sys.stdin);assert len(a)==1 and a[0]["name"]=="Test Favorite" and not a[0]["hasPassword"] and "password" not in a[0]'
test "$(curl -s -o /dev/null -w '%{http_code}' -b "$COOKIE" -X PUT "$API/api/v1/favorites/$ID" -H 'Content-Type: application/json' -d "$fav")" = "400"
test "$(curl -s -o /dev/null -w '%{http_code}' -b "$COOKIE" -X PUT "$API/api/v1/favorites/$ID" -H "X-CSRF-TOKEN: $CSRF" -H 'Content-Type: application/json' -d "$fav")" = "204"
# Starting an embedded app needs the dedicated HTTPS origin; CI runs HTTP only.
test "$(curl -s -o /dev/null -w '%{http_code}' -b "$COOKIE" -X POST "$API/api/v1/favorites/$ID/start" -H "X-CSRF-TOKEN: $CSRF" -H 'Content-Type: application/json' -d '{}')" = "409"
test "$(curl -s -o /dev/null -w '%{http_code}' -b "$COOKIE" -X DELETE "$API/api/v1/favorites/$ID" -H "X-CSRF-TOKEN: $CSRF")" = "204"
test "$(curl -s -o /dev/null -w '%{http_code}' -b "$COOKIE" -X DELETE "$API/api/v1/admin/favorite-apps/$APP" -H "X-CSRF-TOKEN: $CSRF")" = "204"
echo 'CI checkpoint: app catalog, user favorites, CSRF and app-origin protection OK'

#!/usr/bin/env bash
# Integration smoke: self-service favorites without admin app approvals.
# Never contacts real external websites or uses real credentials.
set -euo pipefail
API="${1:?API required}"
COOKIE="${2:?cookie required}"
CSRF="${3:?CSRF required}"
header=(-H "X-CSRF-TOKEN: $CSRF" -H 'Content-Type: application/json')
url="$API/api/v1/favorites"
one='{"name":"Personal Wiki Link","url":"https://example.org/docs/guide","username":"","autoLogin":false,"enabled":true,"sortOrder":0,"password":""}'
two='{"name":"My Search","url":"https://192.168.1.50:8443/search?tab=1#/settings","username":"","autoLogin":false,"enabled":true,"sortOrder":1,"password":""}'
# Unauthenticated callers can never create personal origins.
test "$(curl -s -o /dev/null -w '%{http_code}' -X POST "$url/personal" -H 'Content-Type: application/json' -d "$one")" = "401"
# CSRF and safe HTTPS target validation remain mandatory.
test "$(curl -s -o /dev/null -w '%{http_code}' -b "$COOKIE" -X POST "$url/personal" -H 'Content-Type: application/json' -d "$one")" = "400"
test "$(curl -s -o /dev/null -w '%{http_code}' -b "$COOKIE" -X POST "$url/personal" "${header[@]}" -d '{"name":"Unsafe","url":"http://127.0.0.1/","username":"","autoLogin":false,"enabled":true,"sortOrder":0}')" = "400"
ID=$(curl -fsS -b "$COOKIE" -X POST "$url/personal" "${header[@]}" -d "$one" | python3 -c 'import json,sys;print(json.load(sys.stdin)["id"])')
test -n "$ID"
curl -fsS -b "$COOKIE" "$url/" | python3 -c 'import json,sys;a=json.load(sys.stdin);assert len(a)==1 and a[0]["name"]=="Personal Wiki Link" and a[0]["url"]=="https://example.org/docs/guide" and a[0]["slug"].startswith("site-") and a[0]["openMode"]=="proxy" and not a[0]["hasPassword"] and "password" not in a[0]'
# Existing browser favorite has an immutable random proxy alias but editable URL.
modified='{"name":"Personal Wiki Link","url":"https://example.net/other#/homepage","username":"","autoLogin":false,"enabled":true,"sortOrder":0,"password":""}'
test "$(curl -s -o /dev/null -w '%{http_code}' -b "$COOKIE" -X PUT "$url/personal/$ID" "${header[@]}" -d "$modified")" = "204"
SECOND=$(curl -fsS -b "$COOKIE" -X POST "$url/personal" "${header[@]}" -d "$two" | python3 -c 'import json,sys;print(json.load(sys.stdin)["id"])')
test "$(curl -s -o /dev/null -w '%{http_code}' -b "$COOKIE" -X PUT "$url/personal/order" "${header[@]}" -d "{\"ids\":[\"$SECOND\",\"$ID\"]}")" = "204"
curl -fsS -b "$COOKIE" "$url/" | python3 -c 'import json,sys;a=json.load(sys.stdin);assert len(a)==2 and a[0]["name"]=="My Search" and a[0]["url"]=="https://192.168.1.50:8443/search?tab=1#/settings" and a[1]["url"]=="https://example.net/other#/homepage"'
# Opening modes are persisted on the individual owner's favorite.
test "$(curl -s -o /dev/null -w '%{http_code}' -b "$COOKIE" -X PUT "$url/personal/$ID/mode" "${header[@]}" -d '{"openMode":"external"}')" = "204"
curl -fsS -b "$COOKIE" "$url/" | python3 -c 'import json,sys;a=json.load(sys.stdin);assert a[1]["openMode"]=="external" and a[0]["openMode"]=="proxy"'
test "$(curl -s -o /dev/null -w '%{http_code}' -b "$COOKIE" -X PUT "$url/personal/$ID/mode" "${header[@]}" -d '{"openMode":"unknown"}')" = "400"
test "$(curl -s -o /dev/null -w '%{http_code}' -b "$COOKIE" -X PUT "$url/personal/$ID/mode" "${header[@]}" -d '{"openMode":"proxy"}')" = "204"
# Starting an iframe requires the dedicated HTTPS proxy and must not work on HTTP CI.
test "$(curl -s -o /dev/null -w '%{http_code}' -b "$COOKIE" -X POST "$url/$ID/start" "${header[@]}" -d '{}')" = "409"
test "$(curl -s -o /dev/null -w '%{http_code}' -b "$COOKIE" -X DELETE "$url/personal/$ID" -H "X-CSRF-TOKEN: $CSRF")" = "204"
test "$(curl -s -o /dev/null -w '%{http_code}' -b "$COOKIE" -X DELETE "$url/personal/$SECOND" -H "X-CSRF-TOKEN: $CSRF")" = "204"
curl -fsS -b "$COOKIE" "$url/" | python3 -c 'import json,sys;assert json.load(sys.stdin)==[]'
echo 'CI checkpoint: personal HTTPS websites, owner isolation, CSRF, ordering and safe bootstrap OK'

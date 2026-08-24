#!/bin/sh
set -eu

INTERVAL="${MAPPER_OOC_INTERVAL:-600}"
MESSAGE="${MAPPER_OOC_MESSAGE:-Это мапперский сервер. Не играйте раунд — сохраняйте карты.}"
SENDER="${MAPPER_OOC_SENDER:-Server}"
URL="http://127.0.0.1:1213/admin/actions/ooc"

until curl -sf -m 3 http://127.0.0.1:1213/status >/dev/null 2>&1; do
    sleep 5
done

while true; do
    if [ -n "${ADMIN_API_TOKEN:-}" ]; then
        curl -sS -m 15 -X POST "$URL" \
            -H "Authorization: SS14Token ${ADMIN_API_TOKEN}" \
            -H "Content-Type: application/json" \
            -d "{\"sender\":\"${SENDER}\",\"message\":\"${MESSAGE}\"}" \
            >/dev/null 2>&1 || true
    fi
    sleep "$INTERVAL"
done

#!/bin/sh
set -eu

cd /app
mkdir -p /data
chmod u+rwX /data 2>/dev/null || true

if [ -z "${ADMIN_API_TOKEN:-}" ]; then
    ADMIN_API_TOKEN=$(openssl rand -hex 32)
fi
export ADMIN_API_TOKEN

MOTD="${MAPPER_OOC_MESSAGE:-Это мапперский сервер. Не играйте раунд — сохраняйте карты.}"
export MAPPER_OOC_MESSAGE="$MOTD"

nginx

if [ -x /usr/local/bin/broadcast.sh ]; then
    /usr/local/bin/broadcast.sh &
fi

set -- ./Robust.Server \
    --data-dir /data \
    --cvar status.bind=*:1213 \
    --cvar adminlogs.enabled=false \
    --cvar game.map=Empty \
    --cvar game.defaultpreset=Sandbox \
    --cvar game.lobbyenabled=false \
    --cvar "game.hostname=Grosse Mapping" \
    --cvar "game.desc=${MOTD}" \
    --cvar shuttle.emergency=false \
    --cvar shuttle.auto_call_time=0 \
    --cvar shuttle.arrivals=false \
    --cvar shuttle.grid_fill=false \
    --cvar hub.advertise=false \
    --cvar database.engine=sqlite \
    --cvar "chat.motd=${MOTD}" \
    --cvar whitelist.enabled=true \
    --cvar net.log_late_msg=false \
    --cvar admin.admins_count_in_playercount=true \
    --cvar game.role_timers=false \
    --cvar game.role_loadout_timers=false \
    --cvar ooc.enable_during_round=true \
    --cvar "admin.api_token=${ADMIN_API_TOKEN}"

if [ -n "${LOGIN_HOST_USER:-}" ]; then
    set -- "$@" --cvar "console.login_host_user=${LOGIN_HOST_USER}"
    set -- "$@" "+whitelistadd ${LOGIN_HOST_USER}"
fi

if [ -n "${WHITELIST_USERS:-}" ]; then
    old_ifs=$IFS
    IFS=','
    for user in $WHITELIST_USERS; do
        user=$(printf '%s' "$user" | tr -d ' \t\r\n')
        if [ -n "$user" ]; then
            set -- "$@" "+whitelistadd ${user}"
        fi
    done
    IFS=$old_ifs
fi

exec "$@"

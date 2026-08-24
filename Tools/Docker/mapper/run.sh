#!/bin/sh
set -eu
cd "$(dirname "$0")"

if [ -z "${LOGIN_HOST_USER:-}" ]; then
  echo "Set LOGIN_HOST_USER to your SS14 account name." >&2
  exit 1
fi

mkdir -p data

docker run --name grosse-map-server --rm \
  -p 1212:1212/tcp -p 1212:1212/udp \
  -v "$(pwd)/data:/data" \
  -e LOGIN_HOST_USER="$LOGIN_HOST_USER" \
  -e WHITELIST_USERS="${WHITELIST_USERS:-}" \
  repo.a.backmen.ru/grosse/map-server:latest

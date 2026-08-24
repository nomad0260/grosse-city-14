# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet build Content.Packaging --configuration Release \
 && dotnet run --project Content.Packaging --no-build --configuration Release -- \
      server --hybrid-acz --platform linux-x64

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app

RUN apt-get update \
 && apt-get install -y --no-install-recommends nginx curl openssl unzip ca-certificates \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /src/release/SS14.Server_linux-x64.zip /tmp/server.zip
ARG CONTENT_VERSION=unknown
ARG ENGINE_VERSION=unknown
RUN unzip -q /tmp/server.zip -d /app \
 && chmod +x /app/Robust.Server \
 && rm /tmp/server.zip \
 && engine="${ENGINE_VERSION#v}" \
 && printf '{\n  "fork_id": "grosse",\n  "engine_version": "%s",\n  "version": "%s",\n  "hash": "",\n  "download": ""\n}\n' \
      "$engine" "$CONTENT_VERSION" > /app/build.json

COPY Tools/Docker/mapper/nginx.conf /etc/nginx/nginx.conf
COPY Tools/Docker/mapper/www/index.html /var/www/mapper/index.html
COPY Tools/Docker/mapper/start.sh /usr/local/bin/start.sh
COPY Tools/Docker/mapper/broadcast.sh /usr/local/bin/broadcast.sh
RUN sed -i 's/\r$//' /usr/local/bin/start.sh /usr/local/bin/broadcast.sh \
 && chmod +x /usr/local/bin/start.sh /usr/local/bin/broadcast.sh \
 && mkdir -p /data /var/www/mapper /run \
 && nginx -t

ENV LOGIN_HOST_USER="" \
    WHITELIST_USERS="" \
    MAPPER_OOC_INTERVAL=600

EXPOSE 1212/tcp
EXPOSE 1212/udp
VOLUME ["/data"]

ENTRYPOINT ["/usr/local/bin/start.sh"]

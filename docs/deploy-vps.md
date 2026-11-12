# SkyRoute VPS deployment

How SkyRoute (API + web) is deployed on the shared Castel Tech test VPS. Written 2026-11-10 from the Castel Tech infrastructure records, which are private and hold the host details this public repository leaves out. The demo is presented on **Wednesday 2026-11-18**.

## Decision

One public host, path-based routing, no CORS in production.

```
https://skyroute.casteltech.ar/       -> skyroute_web:3000  (Next standalone)
https://skyroute.casteltech.ar/api/   -> skyroute_api:8080  (ASP.NET Core, SignalR hub included)
```

| Option | Verdict |
|---|---|
| One host, `/api` passed through unchanged | **Chosen.** One DNS record, one vhost, existing `*.casteltech.ar` certificate, browser stays same-origin so no CORS. |
| Two hosts (`skyroute` + `skyroute-api`) with CORS | Works, but adds CORS in production and a second record and vhost for no gain. Note: `api.skyroute.casteltech.ar` would not work, wildcard certs cover one level only. |
| One host, nginx strips `/api` | Rejected. The OpenAPI document would publish paths without `/api`, giving the Padi group (our API consumer) wrong URLs. |
| Next `rewrites()` as proxy in production | Rejected. SignalR websockets through Next are fragile and add a hop. |

## Code requirements before deploying

1. **Every API route lives under `/api`**, including the SignalR hub (`/api/hubs/telemetry`). This is part of the public contract consumed by the Padi group. Done in #22.
2. **OpenAPI and Scalar also live under `/api`:** `/api/openapi/v1.json` and `/api/scalar`, published in every environment. Done in #22.
3. **CORS only in Development**, allowing `http://localhost:3000`. Production needs none because web and API share an origin. Server-to-server callers (the Padi group, both as payment provider and as API consumer) are never subject to CORS.
4. **The API trusts the proxy's forwarded headers** (`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` in `docker-compose.vps.yml`), so it sees `https` and the client IP instead of the internal hop.
5. **The SQLite file lives on the `api-data` volume** (`Database__Path=/app/data/skyroute.db` in `docker-compose.yml`).
6. **`PublicWebUrl` is the base of every mission's `trackingUrl`** (`https://skyroute.casteltech.ar` in `docker-compose.vps.yml`; defaults to `http://localhost:3000`). The page `/embed/missions/{id}` must allow being framed by other origins.
7. **`NEXT_PUBLIC_API_URL` is inlined at build time.** On the VPS build it as `https://skyroute.casteltech.ar`; frontend code appends `/api/...` paths, the same code path used locally with `http://localhost:5113`.

## VPS facts

| Item | Value |
|---|---|
| Host | Ubuntu with Docker + Compose v5. Address and access: Castel Tech infrastructure records. |
| Operator | In the `docker` group: Docker needs no `sudo`; files under `/opt` do. |
| Reverse proxy | Container `castel_proxy` (`nginx:1.30.4-alpine`), stack in `/opt/castel-stack` |
| Vhosts | `/opt/castel-stack/nginx/conf.d/*.conf` (only `*.conf` is loaded) |
| Certificate | Cloudflare Origin Cert for `casteltech.ar` + `*.casteltech.ar`, inside the container at `/etc/nginx/certs/casteltech-origin.pem` / `.key` |
| Shared network | External Docker network `castel_network` |
| Cloudflare SSL | Full (strict) |
| Memory | Enough for SkyRoute without stopping anything. Building .NET + Next on the host peaks around 1-1.5 GB. |

Other projects share the host: never touch a container, vhost, or compose file that is not SkyRoute's.

## Host rules (mandatory)

1. **No container publishes a port without a `127.0.0.1:` prefix.** Docker-published ports bypass the host firewall. SkyRoute publishes nothing on the VPS.
2. **Traffic must come through Cloudflare.** The DNS record must be **proxied** (orange cloud); a DNS-only record is unreachable.
3. **Be surgical.** Add files, never edit another tenant's vhost or compose. Every step has a rollback ready before it runs.
4. Braian runs every VPS command himself, one line at a time. The host has `grep`/`cat`, not `rg`/`fd`.

## Files in this repo

### `docker-compose.vps.yml`

Overrides the base compose: removes published ports, fixes container names so nginx can resolve them, joins `castel_network`, and enables forwarded headers in the API.

```yaml
services:
  api:
    container_name: skyroute_api
    ports: !reset []
    environment:
      ASPNETCORE_FORWARDEDHEADERS_ENABLED: "true"
      PublicWebUrl: https://skyroute.casteltech.ar
    restart: unless-stopped
    networks: [default, castel_network]

  web:
    container_name: skyroute_web
    build:
      args:
        NEXT_PUBLIC_API_URL: https://skyroute.casteltech.ar
    ports: !reset []
    restart: unless-stopped
    networks: [default, castel_network]

networks:
  castel_network:
    external: true
```

### `deploy/nginx/skyroute.casteltech.ar.conf`

Upstreams are variables resolved through Docker DNS at request time, so `nginx -t` and the shared proxy keep working even when SkyRoute is down. With a variable and no URI part, `proxy_pass` forwards the original path, so `/api` reaches the API unchanged.

```nginx
server {
    listen 80;
    server_name skyroute.casteltech.ar;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl;
    http2 on;
    server_name skyroute.casteltech.ar;

    ssl_certificate     /etc/nginx/certs/casteltech-origin.pem;
    ssl_certificate_key /etc/nginx/certs/casteltech-origin.key;

    resolver 127.0.0.11 valid=10s;
    set $skyroute_api http://skyroute_api:8080;
    set $skyroute_web http://skyroute_web:3000;

    location /api/ {
        proxy_pass $skyroute_api;
        proxy_http_version 1.1;
        # SignalR websockets
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $http_connection;
        proxy_read_timeout 120s;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location / {
        proxy_pass $skyroute_web;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Check before copying: if the existing vhosts in `conf.d` use `listen 443 ssl http2;` instead of `http2 on;`, match their style.

## Rollout

Each line is one command. Run from `~` unless stated.

**1. DNS (Cloudflare dashboard, zone `casteltech.ar`)**
Add `A skyroute <VPS address>` (from the Castel Tech infrastructure records), **Proxied**. Rollback: delete the record.

**2. Get the code**

```bash
git clone https://github.com/BraianMedrano/pad-skyroute.git ~/projects/pad-skyroute
cd ~/projects/pad-skyroute && git checkout main && git pull --ff-only && git log -1 --oneline
```

**3. Build and start (no published ports)**

```bash
docker network inspect castel_network >/dev/null && echo network-ok
docker compose -p skyroute -f docker-compose.yml -f docker-compose.vps.yml up -d --build
docker ps --filter name=skyroute --format '{{.Names}}\t{{.Status}}\t{{.Ports}}'
```

Expected: `skyroute_api` and `skyroute_web` running, the Ports column shows only container ports (no `0.0.0.0:`).
Rollback: `docker compose -p skyroute -f docker-compose.yml -f docker-compose.vps.yml down` (keeps the SQLite volume; never add `-v`).

**4. Reachability from the proxy**

```bash
docker exec castel_proxy wget -qO- http://skyroute_api:8080/api/openapi/v1.json | head -c 200
docker exec castel_proxy wget -qO- http://skyroute_web:3000 | head -c 200
```

**5. Vhost (new file only)**

```bash
sudo cp ~/projects/pad-skyroute/deploy/nginx/skyroute.casteltech.ar.conf /opt/castel-stack/nginx/conf.d/skyroute.casteltech.ar.conf
docker exec castel_proxy nginx -t
docker exec castel_proxy nginx -s reload
```

If `nginx -t` fails, do not reload: `sudo rm /opt/castel-stack/nginx/conf.d/skyroute.casteltech.ar.conf` and run `nginx -t` again.
Rollback after reload: remove the file, `nginx -t`, reload.

**6. Public checks (from any machine)**

```bash
curl -sI https://skyroute.casteltech.ar | head -1
curl -s https://skyroute.casteltech.ar/api/openapi/v1.json | head -c 200
curl -sI http://skyroute.casteltech.ar | grep -i '^location'
```

Then open the web in a browser and confirm in DevTools that the SignalR connection upgrades to a websocket (status 101).

## Redeploy

```bash
cd ~/projects/pad-skyroute && git pull --ff-only && docker compose -p skyroute -f docker-compose.yml -f docker-compose.vps.yml up -d --build
```

Rollback: `git checkout <previous-sha>` and run the same command.

## After the demo

`docker compose -p skyroute -f docker-compose.yml -f docker-compose.vps.yml stop`, then remove the vhost and DNS record when the course ends. Deleting the volume (`down -v`) is irreversible and needs an explicit decision.

## Sources

- Castel Tech infrastructure records (private): host details, firewall, host rules, and the shared proxy.
- The vhost follows the pattern of the other vhosts on the shared proxy.

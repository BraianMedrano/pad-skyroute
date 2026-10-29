# SkyRoute

Fleet management system for simulated drones. The first mission type is **package delivery**: request a delivery, assign an available drone, and track it in real time on an interactive map.

## Repository layout

```
backend/    .NET API, drone simulator, SignalR hub
frontend/   Next.js app with the interactive map
docs/       Architecture and decisions
```

See [docs/architecture.md](docs/architecture.md) for the system overview.

## Run locally

Requirements: .NET 10 SDK, Node.js and pnpm.

```sh
# Backend: API on http://localhost:5113, OpenAPI at /openapi/v1.json
dotnet run --project backend/src/SkyRoute.Api --launch-profile http

# Backend tests
dotnet test backend/SkyRoute.slnx

# Frontend: http://localhost:3000
cd frontend && pnpm install && pnpm dev
```

## Run everything with Docker

For demos and integration with other groups. For day-to-day development use the native commands above (faster hot reload).

```sh
docker compose up --build
```

- Frontend on http://localhost:3000, API on http://localhost:5113.
- The SQLite database lives in the `api-data` volume and survives restarts.
- The browser calls the API directly. When other machines use the frontend, rebuild with the host's network address: `API_PUBLIC_URL=http://<host-ip>:5113 docker compose up --build`.

## Working with coding agents

Every agent must follow [AGENTS.md](AGENTS.md).

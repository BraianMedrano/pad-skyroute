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

## Working with coding agents

Every agent must follow [AGENTS.md](AGENTS.md).

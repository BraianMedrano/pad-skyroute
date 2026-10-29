# Architecture

## Scope

- Drone fleet management with simulated drones.
- MVP mission type: package delivery (origin, destination, package weight).
- Missions are modeled generically (type + route) so inspection or surveying can be added later.

## Mission lifecycle

`Requested → Validated → Paid → Assigned → InProgress → Completed / Failed`

## What we offer (public API)

- Request and schedule package deliveries.
- Query drone availability and status.
- Track missions in real time.
- Webhook notification when a mission completes or fails.

## What we consume

| Service  | Provider                      | Purpose                                  |
|----------|-------------------------------|------------------------------------------|
| Identity | RENAPER group API             | Validate the package recipient           |
| Weather  | Open-Meteo                    | Block flights in high wind or rain       |
| Payments | Mercado Pago sandbox or fake  | Charge for deliveries                    |

Expected consumer: the food delivery group, requesting drone deliveries for their orders.

## Stack

- **Backend (.NET):** REST API, simulator as a `BackgroundService`, SignalR for telemetry.
- **Persistence:** SQLite through EF Core for mission and drone state; live telemetry stays in memory.
- **Frontend (Next.js):** interactive map (Leaflet or MapLibre with OpenStreetMap).

## Design principles

- **Ports and adapters:** every external service sits behind an interface with a fake implementation, so the system never blocks on another group.
- **Contract first:** the OpenAPI spec is published early so other groups can integrate in parallel.
- **Resilience:** external calls use timeouts and retries; failures are visible and handled.

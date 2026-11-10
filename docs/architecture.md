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
| Payments | Padi (wallet group API) or fake | Charge for deliveries                  |

Expected consumer: the food delivery group, requesting drone deliveries for their orders.

## Payment and notification

- **Price:** ARS 2000 base plus ARS 250 per km of the full trip (base → origin → destination → base), rounded to whole pesos. The longest allowed trip (25 km) costs ARS 8250. Returned as `price` on every mission.
- **Payment is asynchronous (Padi):** `POST /missions/{id}/payment` creates a Padi checkout link for the price and returns `202` with `checkoutUrl` and `expiresAt`. The mission stays `Validated` until the backend confirms the payment with Padi (Padi has no webhooks, so we poll it); then it becomes `Paid`, and `Assigned` when a drone is available. If the link expires unpaid, calling the endpoint again creates a new one. Clients follow progress with `GET /missions/{id}`.
- **Webhook:** when a mission ends as `Completed` or `Failed`, the same JSON as `GET /missions/{id}` is POSTed to its `callbackUrl`, if one was given.

## Stack

- **Backend (.NET):** REST API, simulator as a `BackgroundService`, SignalR for telemetry.
- **Persistence:** SQLite through EF Core for mission and drone state; live telemetry stays in memory.
- **Frontend (Next.js):** interactive map (Leaflet or MapLibre with OpenStreetMap).

## Design principles

- **Ports and adapters:** every external service sits behind an interface with a fake implementation, so the system never blocks on another group.
- **Contract first:** the OpenAPI spec is published early so other groups can integrate in parallel.
- **Resilience:** external calls use timeouts and retries; failures are visible and handled.

## Open decisions

- **Team split and way of working:** how the four members divide the work and coordinate. To be decided in a team meeting.
- **Map library:** decided: `maplibre-gl` with OpenStreetMap raster tiles (single dependency, TypeScript types included).

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

## Flight rules

Simulated multirotor drones, sized after real urban delivery drones (Wing, Matternet M2, Zipline P2).

- **Bases:** 4 bases cover Greater San Miguel de Tucumán, with 2 drones each (8 drones). A base is the `HomePosition` of its drones, seeded in #9; it is not a separate entity. Approximate coordinates:

  | Base               | Lat      | Lon      |
  |--------------------|----------|----------|
  | Centro             | -26.8305 | -65.2039 |
  | Yerba Buena        | -26.8160 | -65.3160 |
  | Banda del Río Salí | -26.8400 | -65.1700 |
  | Tafí Viejo         | -26.7320 | -65.2590 |

- **Maximum flight:** 25 km for the full trip, base → origin → destination → base, using great-circle (Haversine) distance.
- **Maximum package weight:** 2.5 kg.
- **Cruise speed:** 60 km/h. The simulator may speed up time for the demo.
- **Request (#10):** a request is rejected with 422 when the package exceeds 2.5 kg, or when no base can fly the full trip within 25 km.
- **Assignment (#11):** the available drone nearest to the origin whose full trip fits within 25 km. If none is available, the mission stays `Paid`.

## Open decisions

- **Team split and way of working:** how the four members divide the work and coordinate. To be decided in a team meeting.
- **Map library:** decided: `maplibre-gl` with OpenStreetMap raster tiles (single dependency, TypeScript types included).

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

## Real-time telemetry

- **Hub URL:** `/hubs/telemetry` (SignalR, server to client only).
- **Event:** `TelemetryUpdated`, broadcast to every connected client about once per second per drone.
- **Message:** JSON, camelCase, `status` as a string (same values as the REST `DroneStatus`).

| Field        | Type                     | Notes                                  |
|--------------|--------------------------|----------------------------------------|
| `droneId`    | string (GUID)            |                                        |
| `missionId`  | string (GUID) or `null`  | `null` when the drone has no mission   |
| `lat`        | number                   | Degrees                                |
| `lon`        | number                   | Degrees                                |
| `headingDeg` | number                   | 0 = north, 90 = east                   |
| `batteryPct` | number                   | 0 to 100                               |
| `status`     | string                   | `Available` or `Busy`                  |
| `timestamp`  | string (ISO 8601, UTC)   |                                        |

```json
{
  "droneId": "8f14e45f-ceea-4672-9b3a-1d2c3e4f5a6b",
  "missionId": null,
  "lat": -26.828,
  "lon": -65.20421,
  "headingDeg": 90,
  "batteryPct": 99.7,
  "status": "Busy",
  "timestamp": "2026-11-08T15:00:00.000+00:00"
}
```

Telemetry lives in memory and is never written to the database. Until the real simulator lands, one fake drone flies a fixed circuit around Plaza Independencia, San Miguel de Tucumán. Browsers on another origin (the Next.js frontend) also need the API's CORS policy to allow that origin with credentials.

## Design principles

- **Ports and adapters:** every external service sits behind an interface with a fake implementation, so the system never blocks on another group.
- **Contract first:** the OpenAPI spec is published early so other groups can integrate in parallel.
- **Resilience:** external calls use timeouts and retries; failures are visible and handled.

## Open decisions

- **Team split and way of working:** how the four members divide the work and coordinate. To be decided in a team meeting.
- **Map library:** `react-leaflet` + `leaflet` or `maplibre-gl`, chosen once by the team.

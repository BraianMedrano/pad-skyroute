# Demo script

The demo follows one delivery from request to completion, plus requests that are rejected. It runs entirely with the Fakes, so it does not depend on Padi, RENAPER, Open-Meteo, or another group's API being up.

## Before the demo

Start the system with the Fakes on. `UseFakes=true` is required: the real adapters are not on `main` yet, and without the Fakes the API stops at startup with `Unable to resolve service for type 'SkyRoute.Application.External.IWebhookNotifier'`.

```sh
# Option A: Docker (after #40 is merged, docker-compose.yml sets UseFakes=true by default)
docker compose up --build

# Option B: native
UseFakes=true dotnet run --project backend/src/SkyRoute.Api --launch-profile http
cd frontend && pnpm install && pnpm dev
```

Check that it is ready:

- `http://localhost:5113/api/drones` lists the 8 seeded drones (Falcon-1 to Falcon-8), all `Available`.
- `http://localhost:3000` shows the map with the drones at their bases.
- `http://localhost:5113/api/scalar` opens the API reference (plan B below).

On the VPS, replace `http://localhost:5113/api` with `https://skyroute.casteltech.ar/api` and the frontend with `https://skyroute.casteltech.ar`.

Use a fresh database for each rehearsal so the mission list starts empty: delete the `api-data` volume (`docker compose down -v`), or the `skyroute.db` file for the native run.

## Script

| Step | What to do | What the audience sees |
| --- | --- | --- |
| 1. Fleet | Open the map. | 8 drones at 4 bases in Tucumán, broadcasting live telemetry. |
| 2. Request | Request a delivery (payload below). | The mission is created as `Validated` with its `price` and `trackingUrl`. |
| 3. Pay | Pay the mission. | A checkout link comes back. The Fake confirms the payment on its own. |
| 4. Assign | Wait about 5 s. | The mission becomes `Assigned` to the nearest available drone. |
| 5. Fly | Wait about 5 s more. | The mission becomes `InProgress` and the drone flies base → origin → destination on the map, with battery going down. |
| 6. Complete | Wait about 20 s. | The mission ends `Completed`, the drone is back at its base and `Available`. The API log shows `Fake webhook notification: mission … is Completed`. |
| 7. Rejections | Send the rejected requests below. | Each one returns `422` with its reason. |

Timings are from a local run with the Fakes (2026-11-16). They depend on the route: longer trips fly longer.

### Valid request

`POST /api/missions`

```json
{
  "origin": { "lat": -26.8241, "lon": -65.2226 },
  "destination": { "lat": -26.8083, "lon": -65.2176 },
  "packageWeightKg": 1.2,
  "recipientDni": "30123456"
}
```

Returns `Validated` with `price` 3658 (ARS 2000 base plus ARS 250 per km of the full trip).

Then `POST /api/missions/{id}/payment` returns the `checkoutUrl`, and `GET /api/missions/{id}` shows the status moving to `Assigned`, `InProgress`, and `Completed`.

### Rejected requests

Same payload as the valid one, changing one field:

| Change | `detail` of the 422 |
| --- | --- |
| `"recipientDni": "00000000"` | `Recipient DNI rejected` |
| `"packageWeightKg": 4` | `Package weight must be greater than 0 and at most 2.5 kg.` |
| `"destination": { "lat": -26.65, "lon": -65.40 }` | `Out of range: no base can fly base → origin → destination → base within 25 km.` |

## Plan B: the web app is not ready

If the request form (#17) or the tracking view (#18) are not merged, do steps 2, 3, and 7 from Scalar at `/api/scalar` with the payloads above, and keep the map open next to it. The drone still flies on the map, because the map only needs the live telemetry from the simulator.

To show the status changes, run `GET /api/missions/{id}` from Scalar after each step.

## Plan C: an external service is down

The demo already runs with `UseFakes=true`, so Padi, RENAPER, Open-Meteo, or a webhook receiver being down does not affect it. If a real adapter is merged and enabled with `UseFakes=false` and fails during the demo, restart the API with `UseFakes=true` and continue from step 2.

## Checklist before presenting

- [ ] Fresh database, 8 drones `Available`.
- [ ] Map open at `http://localhost:3000` (or the VPS URL).
- [ ] Scalar open in another tab with the valid payload ready.
- [ ] API log visible, to show the webhook line in step 6.

# External integrations

## Integration modes

`docker-compose.yml` forwards `UseFakes` and defaults it to `true`.
`UseFakes=true` keeps all four Fakes. Explicit `UseFakes=false` registers real
Open-Meteo and RENAPER typed clients with `AddStandardResilienceHandler()`.
Missing or invalid `UseFakes` registers no integrations. There is no automatic
Fake fallback.

**Real-mode acceptance is pending.** The default real-mode composition still
lacks `IPaymentGateway` (#20) and `IWebhookNotifier` (#15 has a local opt-in).
Flows that require these ports cannot complete, and the API can fail to start
when dependency validation is enabled. Agree on a local composition before
trying to verify #14 and #19 through `POST /api/missions`.

## Identity (#19)

Set `Renaper__BaseUrl=https://api-openrenaper.matiasagustinibanez.com` in the
local `.env` and export it to the API process. The setting requires an absolute
HTTPS URL without embedded credentials. Compose does not forward this setting
by default; if running the API in a container, provide it explicitly to the
container environment. RENAPER requires no API key.

The client sends only the DNI lookup, does not follow redirects, and does not
persist the provider response. It accepts a successful response only when the
returned DNI matches the submitted one.

| Result | SkyRoute outcome |
| --- | --- |
| Invalid format, documented `404` (not found) or `422` (invalid input) | `422` business rejection |
| Documented `409` (ambiguous match) | `422`: DNI could not be verified unambiguously |
| Transport error, timeout, throttling, unexpected status or malformed response | `503` via `ExternalServiceUnavailableException` |

Verify live behavior only with synthetic DNIs supplied by the RENAPER team.
Reference: https://api-openrenaper.matiasagustinibanez.com/docs

## Weather (#14)

### Configuration

The project's `.env.example` provides these safe defaults; export overrides when needed:

```dotenv
Weather__MaxWindSpeedKmh=30
Weather__MaxPrecipitationMm=0
```

The double underscore maps to `Weather:MaxWindSpeedKmh` and
`Weather:MaxPrecipitationMm`. Both default to the values above when absent;
configured thresholds must be finite, non-negative numbers. Open-Meteo needs no
API key.

### Behavior

Only current `wind_speed_10m` (km/h, at 10 m above ground) and
`precipitation` (mm over the current interval) are requested, at the mission's
origin and destination by the existing request use case. Coordinates are
formatted using invariant culture. Precipitation includes rain, showers and
snow; it is not an instantaneous rain sensor.

Weather is unsafe when either reading exceeds its threshold. Equality is safe:

| Wind (km/h) | Precipitation (mm) | Default outcome |
|---|---|---|
| 30 | 0 | Safe |
| 30.1 | 0 | Unsafe |
| 18 | 0.1 | Unsafe |

Unsafe weather returns the business rejection reason (422 through the request
use case). Failed HTTP calls, timeouts and invalid/missing/null numeric readings
throw `ExternalServiceUnavailableException("Open-Meteo", ...)`, which the existing
API handler maps to 503. Missing values never silently become zero. Caller
cancellation propagates unchanged. No custom retries, caching or forecasts.

These are demo rules for simulated drones, not real-flight safety guarantees.

### Verification and integration

Run `dotnet build backend/SkyRoute.slnx` and
`dotnet test backend/SkyRoute.slnx`. Adapter tests are excluded by `AGENTS.md`.
A public provider GET confirms schema/units only, not adapter correctness or
mission-level acceptance.

The allowlisted resilience package requires an issue-specific exception for
`SkyRoute.Infrastructure.csproj`, authorized in #14. When merging #20, retain one
compatible package reference and combine identity, weather and payment
registrations. No Padi implementation is included here.

Reference: https://open-meteo.com/en/docs

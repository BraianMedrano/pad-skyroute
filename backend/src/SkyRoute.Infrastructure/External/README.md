# External integrations

## Weather (#14)

`docker-compose.yml` forwards `UseFakes` to the API container and defaults it
to `true`. `UseFakes=true` keeps all four existing Fakes. Explicit
`UseFakes=false` registers only `OpenMeteoWeatherService`, with a typed HttpClient
and `AddStandardResilienceHandler()`. Missing or invalid `UseFakes` still
registers no integrations. No new switch or automatic Fake fallback is added.

**Real-mode mission flows are not ready:** mission requests also need the
identity adapter (#19); payment flows need `IPaymentGateway` (#20); and terminal
mission notifications need `IWebhookNotifier`. Those services are not registered
in this weather-only mode. The API's dependency-injected paths that require them
cannot complete until their real integrations are available. End-to-end weather
rejection and service-outage acceptance criteria remain pending until #19 is
available.

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
compatible package reference and combine weather/payment registrations instead
of replacing either adapter. No Padi implementation is included here.

Reference: https://open-meteo.com/en/docs

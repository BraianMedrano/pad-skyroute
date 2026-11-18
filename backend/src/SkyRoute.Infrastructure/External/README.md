# External integrations

`AddIntegrations` has two explicit modes. It never silently fills missing real integrations with Fakes.

| `UseFakes` configuration | Registrations |
|---|---|
| `true` | All four existing Fakes: identity, weather, payment, and webhook. |
| `false` | Real RENAPER identity, Open-Meteo weather and Padi payment clients. All use `AddStandardResilienceHandler()`; Padi POST retries are disabled. The HTTP webhook notifier is registered too. |
| Missing or invalid | No integrations are registered. |

In real mode `AddIntegrations` also registers `HttpWebhookNotifier`, because the mission completion and failure use cases require `IWebhookNotifier`. The API is public and unauthenticated, so the notifier posts only when a mission's `callbackUrl` is exactly `Webhook__ReceiverUrl` (an absolute HTTP(S) URL without credentials, set by the operator). Any other callback is skipped with a warning. When `Webhook__ReceiverUrl` is unset, no webhook is delivered. Compose does not forward it, so the VPS delivers none; for a local test, set it to your controlled receiver, for example `http://127.0.0.1:9000/hook`.

## Identity (#19)

Set `Renaper__BaseUrl=https://api-openrenaper.matiasagustinibanez.com` in the
local `.env` and export it to the API process. The setting requires an absolute
HTTPS URL without embedded credentials. Compose forwards it to the API
container. RENAPER requires no API key.

The client sends only the DNI lookup, does not follow redirects, and does not
persist the provider response. It accepts a successful response only when the
returned DNI matches the submitted one.

| Result | SkyRoute outcome |
| --- | --- |
| Invalid format, documented `404` (not found) or `422` (invalid input) | `422` business rejection |
| Documented `409` (ambiguous match) | `422`: DNI could not be verified unambiguously |
| Transport error, timeout, throttling, unexpected status or malformed response | `503` via `ExternalServiceUnavailableException` |

RENAPER returns real personal data. Verify live behavior only with DNIs whose
owners authorized the lookup.
Reference: https://api-openrenaper.matiasagustinibanez.com/docs

## Configuration and Compose

`docker-compose.yml` forwards `UseFakes` (default `true`), `Database__Path`, `Renaper__BaseUrl`, `Padi__ApiKey` (default empty), `Padi__BaseUrl`, `Weather__MaxWindSpeedKmh`, and `Weather__MaxPrecipitationMm` to the API container, with the defaults documented here. Docker Compose reads them from the `.env` file next to it or from the shell. `Padi__TtlMinutes` is not forwarded, so containers use its default. Real mode needs a non-empty `Padi__ApiKey`. Outside Compose the application does not load `.env`; set the values in the API process environment.

Padi configuration keys are `Padi__ApiKey`, `Padi__BaseUrl` (default `https://padi.runasp.net`), and optional `Padi__TtlMinutes` (default 30, positive integer). Generate an API key from **Integraciones** in the [Padi app](https://padi-billetera.netlify.app/) using your seller account. Keep the real key only in a private local or server environment, never in chat, logs, Git, or frontend configuration. The key is sent in the `X-Api-Key` request header and validated when the Padi client is resolved. Fake mode does not require Padi configuration.

For a local Bash run, set `UseFakes=false` and the Padi settings in your private `.env` using Bash assignment syntax with LF line endings. The application does not load `.env` automatically; source only your own trusted file and start the API if loading succeeds:

```sh
(
    set +x
    set -a
    . ./.env && dotnet run --project backend/src/SkyRoute.Api
)
```

Weather thresholds are `Weather__MaxWindSpeedKmh` (default `30`) and `Weather__MaxPrecipitationMm` (default `0`). They map to `Weather:MaxWindSpeedKmh` and `Weather:MaxPrecipitationMm`; configured values must be finite, non-negative numbers. Open-Meteo needs no API key.

## Weather behavior

Open-Meteo is queried for current `wind_speed_10m` (km/h at 10 m above ground) and `precipitation` (mm over the current interval), using the mission's origin and destination in the existing request use case. Coordinates use invariant culture. Precipitation includes rain, showers, and snow; it is not an instantaneous rain sensor.

Weather is unsafe when either reading exceeds its threshold. Equality is safe:

| Wind (km/h) | Precipitation (mm) | Default outcome |
|---|---|---|
| 30 | 0 | Safe |
| 30.1 | 0 | Unsafe |
| 18 | 0.1 | Unsafe |

Unsafe weather returns the business rejection reason (422 through the request use case). Failed HTTP calls, timeouts, and invalid, missing, or null numeric readings throw `ExternalServiceUnavailableException("Open-Meteo", ...)`, which the API maps to 503. Missing values never silently become zero. Caller cancellation propagates unchanged. There are no custom retries, caching, or forecasts. These are demo rules for simulated drones, not real-flight safety guarantees.

## Padi payment behavior and limitations

- Link creation sends the supplied amount as-is, with title `Mission {mission.Id}`, currency `ARS`, type `SingleUse`, and the configured TTL. The JSON is serialized once using the existing `JsonContent` options into an in-memory UTF-8 byte array before sending; the byte-array content carries its exact byte length and original JSON content type. The adapter requires HTTP `201 Created` and validates the returned id, amount, currency, HTTPS checkout URL, and expiry.
- The Padi client uses `AddStandardResilienceHandler()`. Retries are disabled for POST because Padi does not provide idempotency/deduplication for payment-link creation; ambiguous POST failures must not be retried automatically. Redirect following is disabled so the API key is not forwarded to a redirect target. Creation connection failures, provider HTTP 5xx responses, resilience timeouts, and an open circuit map to `ExternalServiceUnavailableException`, which the API maps to HTTP 503. Caller cancellation, provider 4xx responses, and invalid response payloads are not classified as availability failures.
- Payment status is confirmed only when a matching received payment has status `Succeeded` and its individually fetched record matches the link id, amount, and `ARS`. Entries for other links (including entries without a link id) are ignored before their other fields are validated; a matching `Succeeded` entry must have an id. A payment-link status of `Completed` alone is not proof of payment. A verified success takes precedence over link expiry. Without a verified success, a provider-expired or cancelled link, or a link whose expiry time has elapsed, maps to `Expired`; otherwise it maps to `Pending`.
- Padi's received-payments query is limited to the newest 100 records and has no pagination. Absence from that result is unconfirmed, not proof that no payment exists. Therefore the adapter may report `Pending` while a payment is outside that window; it does not poll or infer payment from the link status. Polling belongs to issue #11.

## Real-mode composition limits

Real mode registers every port with a real implementation, including the webhook notifier restricted to `Webhook__ReceiverUrl`, so the API starts with `UseFakes=false`. This composition does not add a Fake fallback.

## Live verification (2026-11-18)

The manual academic check exercised the real .NET Padi adapter. After the local `.env` was converted to LF and reloaded, local configuration passed. At `01:23:04.6365729 UTC` the adapter created an ARS 1500 link (HTTP 201, validated checkout URL and expiry). A read-only query of that same link returned `Pending`; after the academic payment, another query returned `Paid`.

A later combined run with real RENAPER, Open-Meteo and Padi (with a local webhook registration, before it moved into real mode) created a mission for an authorized DNI, returned a Padi `checkoutUrl` from `POST /api/missions/{id}/payment`, and after payment the mission moved from `Validated` to `InProgress` with an assigned drone. A malformed DNI and a RENAPER-rejected DNI returned 422; an unreachable RENAPER returned 503 while the API kept running.

Earlier attempts returned HTTP 400 with HTML even with a known content length. The successful run supersedes the adapter's live blocker but does not establish the cause of those earlier responses.

## Dependency exception

`Microsoft.Extensions.Http.Resilience` is referenced directly by this infrastructure project because `AddStandardResilienceHandler()` is required and is not supplied by its existing framework reference. The package is allowlisted by `AGENTS.md`; the reason for the project-file exception was authorized and recorded in [issue #20](https://github.com/BraianMedrano/pad-skyroute/issues/20#issuecomment-6781231639).

Reference: https://open-meteo.com/en/docs

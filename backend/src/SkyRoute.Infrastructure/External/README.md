# External integrations

## Padi payment gateway

`AddIntegrations` keeps the existing fake integrations when `UseFakes=true`. Explicit `UseFakes=false` registers only `IPaymentGateway` backed by Padi; identity, weather, and webhook ports remain unregistered. Missing or invalid `UseFakes` preserves the previous behavior of registering no integrations. No `.env` loader is used. Supply configuration through the process environment before starting the API.

The tracked `.env.example` should use fake mode by default and document the Padi variables with a placeholder only:

```dotenv
UseFakes=true
Padi__ApiKey=replace-with-your-Padi-server-api-key
Padi__BaseUrl=https://padi.runasp.net
```

To use Padi locally, set `UseFakes=false` and replace the placeholder in your private `.env`. Keep the file in Bash assignment syntax with LF line endings. From the repository root, in Bash, source only your own trusted file and start the API if loading succeeds:

```sh
(
    set +x
    set -a
    . ./.env && dotnet run --project backend/src/SkyRoute.Api
)
```

Docker Compose forwards `UseFakes` (defaulting to `true`) but does not forward `Padi__ApiKey`, `Padi__BaseUrl`, or `Padi__TtlMinutes` to the API container.

Generate an API key from **Integraciones** in the [Padi app](https://padi-billetera.netlify.app/) using your seller account. Store the real key only in your local, uncommitted `.env` or private server process environment. Export those values before starting the API; SkyRoute does not load `.env` automatically. Never put the key in chat, logs, Git, or frontend configuration. The key is sent in the `X-Api-Key` request header. Real client configuration is validated when `IPaymentGateway` is resolved; fake mode does not require Padi configuration. The base URL defaults to `https://padi.runasp.net` and must use HTTPS. Link TTL defaults to 30 minutes and can be overridden with `Padi__TtlMinutes` using a positive integer.

### Payment behavior and limitations

- Link creation sends the supplied amount as-is, with title `Mission {mission.Id}`, currency `ARS`, type `SingleUse`, and the configured TTL. The JSON is serialized once with the existing `JsonContent` options into an in-memory UTF-8 byte array before sending; the byte-array content carries its exact byte length and the original JSON content type. It requires HTTP `201 Created` and validates the returned id, amount, currency, HTTPS checkout URL, and expiry.
- The adapter uses `AddStandardResilienceHandler()`. Retries are disabled for POST because Padi does not provide idempotency/deduplication for payment-link creation; ambiguous POST failures must not be retried automatically. Redirect following is disabled so the API key is not forwarded to a redirect target. Creation connection failures, provider HTTP 5xx responses, resilience timeouts, and an open circuit map to `ExternalServiceUnavailableException`, which the API maps to HTTP 503. Caller cancellation, provider 4xx responses, and invalid response payloads are not classified as availability failures.
- Payment status is confirmed only when a matching received payment has status `Succeeded` and its individually fetched record matches the link id, amount, and `ARS`. Entries for other links (including entries without a link id) are ignored before their other fields are validated; a matching `Succeeded` entry must have an id. A payment-link status of `Completed` alone is not proof of payment. A verified success takes precedence over link expiry. Without a verified success, a provider-expired or cancelled link, or a link whose expiry time has elapsed, maps to `Expired`; otherwise it maps to `Pending`.
- Padi's received-payments query is limited to the newest 100 records and has no pagination. Absence from that result is unconfirmed, not proof that no payment exists. Therefore the adapter may report `Pending` while a payment is outside that window; it does not poll or infer payment from the link status. Polling belongs to issue #11.

### Live verification (2026-11-18)

The manual academic check exercised the real .NET adapter. After the local `.env` was converted to LF and reloaded, local configuration passed. At `01:23:04.6365729 UTC` the adapter created an ARS 1500 link (HTTP 201, validated checkout URL and expiry). A read-only query of that same link returned `Pending`; after the academic payment, another query returned `Paid`. This verifies the provider-facing adapter flow, not the mission endpoint, mission state transition, or polling in #11.

Earlier attempts returned HTTP 400 with HTML even with a known content length. The successful run supersedes the adapter's live blocker but does not establish the cause of those earlier responses.

### Dependency exception

`Microsoft.Extensions.Http.Resilience` is referenced directly by this infrastructure project because `AddStandardResilienceHandler()` is required and is not supplied by its existing framework reference. The package is allowlisted by `AGENTS.md`; the reason for the project-file exception was authorized and recorded in [issue #20](https://github.com/BraianMedrano/pad-skyroute/issues/20#issuecomment-6781231639).

# External integrations

## Padi payment gateway

`AddIntegrations` keeps the existing fake integrations when `UseFakes=true`. Explicit `UseFakes=false` registers only `IPaymentGateway` backed by Padi; identity, weather, and webhook ports remain unregistered. Missing or invalid `UseFakes` preserves the previous behavior of registering no integrations. No `.env` loader is used. Supply configuration through the process environment before starting the API.

The manual `.env.example` block (provided here because the harness blocks access to that file) is:

```dotenv
UseFakes=false
Padi__ApiKey=replace-with-your-Padi-server-api-key
Padi__BaseUrl=https://padi.runasp.net
```

Configure the values locally in the server process environment before `dotnet run`, for example:

```sh
export UseFakes=false
export Padi__ApiKey='your-server-side-Padi-api-key'
export Padi__BaseUrl='https://padi.runasp.net'
dotnet run --project backend/src/SkyRoute.Api
```

Generate an API key from **Integraciones** in the [Padi app](https://padi-billetera.netlify.app/) using your seller account. Store the real key only in your local, uncommitted `.env` or private server process environment. Export those values before starting the API; SkyRoute does not load `.env` automatically. Never put the key in chat, logs, Git, or frontend configuration. The key is sent in the `X-Api-Key` request header. Real client configuration is validated when `IPaymentGateway` is resolved; fake mode does not require Padi configuration. The base URL defaults to `https://padi.runasp.net` and must use HTTPS. Link TTL defaults to 30 minutes and can be overridden with `Padi__TtlMinutes` using a positive integer.

### Payment behavior and limitations

- Link creation sends the supplied amount as-is, with title `Mission {mission.Id}`, currency `ARS`, type `SingleUse`, and the configured TTL. It requires HTTP `201 Created` and validates the returned id, amount, currency, HTTPS checkout URL, and expiry.
- The adapter uses `AddStandardResilienceHandler()`. Retries are disabled for POST because Padi does not provide idempotency/deduplication for payment-link creation; ambiguous POST failures must not be retried automatically. Redirect following is disabled so the API key is not forwarded to a redirect target. HTTP failures are surfaced, never converted to fake success.
- Payment status is confirmed only when a matching received payment has status `Succeeded` and its individually fetched record matches the link id, amount, and `ARS`. A payment-link status of `Completed` alone is not proof of payment. A verified success takes precedence over link expiry. Without a verified success, a provider-expired or cancelled link, or a link whose expiry time has elapsed, maps to `Expired`; otherwise it maps to `Pending`.
- Padi's received-payments query is limited to the newest 100 records and has no pagination. Absence from that result is unconfirmed, not proof that no payment exists. Therefore the adapter may report `Pending` while a payment is outside that window; it does not poll or infer payment from the link status. Polling belongs to issue #11.

### Dependency exception

`Microsoft.Extensions.Http.Resilience` is referenced directly by this infrastructure project because `AddStandardResilienceHandler()` is required and is not supplied by its existing framework reference. The package is allowlisted by `AGENTS.md`; the reason for the project-file exception was authorized and recorded in [issue #20](https://github.com/BraianMedrano/pad-skyroute/issues/20#issuecomment-6781231639).

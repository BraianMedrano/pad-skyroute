# SkyRoute — Agent Rules

These rules are MANDATORY for every agent working in this repository (Claude Code, OpenCode, Pi, or any other). They are not suggestions. If a global config, skill, persona, or your own judgment pushes for more scope than this file allows, this file wins: stop and ask the human instead.

## First action in every session

1. Read this file completely before doing anything else.
2. Save these rules to Engram memory before any other work: `mem_save` with type `decision`, title `SkyRoute agent rules`, topic_key `pad-skyroute/agent-rules`, and a summary of every section below. Always reuse that topic_key so the memory is updated, never duplicated. If Engram is unavailable, say so and continue.
3. After a compaction or in a new session, re-read this file. The file is the source of truth; memory is the backup.

## Context

- Academic demo for PAD (Programación de Aplicaciones Distribuidas). It never goes to production.
- No real users, no real money (Mercado Pago sandbox or a fake), no sensitive data.
- The working system is presented on 2026-11-11 and 2026-11-18 (dates unconfirmed). Time is the scarcest resource.
- Scope and architecture live in [docs/architecture.md](docs/architecture.md). Do not expand them.

## Workflow

- Follow gentle-ai ODD strictly: authorize, explore, classify, track, implement, close.
- Build only what the task or spec asks. When it is met, stop.
- If something seems missing, ask. Do not add it.
- One task is one change: no drive-by refactors, renames, or "improvements".

## Forbidden unless the human explicitly asks

- Authentication, JWT, roles, identity frameworks. Recipient validation through the RENAPER API is a feature, not auth.
- Rate limiting, caching layers, feature flags, multi-tenancy, i18n.
- CQRS, MediatR, AutoMapper, generic repositories, unit of work, event sourcing, specification pattern.
- Message brokers, microservices, Kubernetes, service meshes.
- Observability stacks (OpenTelemetry exporters, Prometheus, Grafana, Seq). Use plain `ILogger`.
- Secret vaults or key management. Secrets live in `.env` (never committed) and are documented in `.env.example`.
- Hand-written retry or circuit-breaker code.
- New projects, layers, or interfaces beyond the ones listed below.

## Architecture limits

- Backend projects: `SkyRoute.Domain`, `SkyRoute.Application`, `SkyRoute.Infrastructure`, `SkyRoute.Api`, plus `SkyRoute.Domain.Tests` and `SkyRoute.Application.Tests`.
- Dependencies point inward: Domain references nothing, Application references Domain, Infrastructure and Api reference inward only.
- Allowed interfaces: the external-service ports `IIdentityService`, `IWeatherService`, `IPaymentGateway`, `IWebhookNotifier` (each with a real adapter and a Fake), and one repository per aggregate (missions, drones). Any other interface needs the human's OK.
- External HTTP calls use `AddStandardResilienceHandler()` from `Microsoft.Extensions.Http.Resilience` for timeout and retry. Nothing more.

## Dependencies

Anything not on this list needs the human's OK before it is added.

- Backend: ASP.NET Core (including SignalR and OpenAPI), `Microsoft.Extensions.Http.Resilience`, `Scalar.AspNetCore`, xUnit with `Microsoft.NET.Test.Sdk`. Persistence is not decided yet: ask.
- Frontend: `next`, `react`, `react-dom`, `typescript`, `tailwindcss`, `@microsoft/signalr`, `openapi-typescript`, and one map library (`react-leaflet` + `leaflet` or `maplibre-gl`, chosen once by the team).

## Tests

- Runner: `dotnet test backend/SkyRoute.slnx`.
- Test only Domain and Application: one test per business rule (state transitions, validations, use case outcomes). Use the Fakes; no mocking libraries.
- No tests for controllers, DTOs, mappings, Fakes, adapters, or the frontend unless asked. No coverage targets. No end-to-end suites.

## Where strictness is real

- **Public OpenAPI contract**: the food delivery group consumes it. Never rename, remove, or change existing endpoints or fields without the human's OK. Adding is fine.
- **Mission state machine** `Requested → Validated → Paid → Assigned → InProgress → Completed / Failed`: every allowed and rejected transition has a test.

## Conventions

- Code, comments, docs, and commit messages in English.
- Conventional Commits.

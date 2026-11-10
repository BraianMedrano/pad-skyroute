# Padi payment adapter (#20)
Objective: implement the existing async payment port with Padi. Branch: feat/issue-20-padi-payment-adapter (base 6437126). Delivery: ask-on-risk, local only, forecast ~300 authored lines plus this tracking document.
Checks: dotnet build backend/SkyRoute.slnx; dotnet test backend/SkyRoute.slnx. No adapter/Fake tests per AGENTS.md.

## Specs
S1. "El objetivo es implementar el adaptador real de Padi para crear enlaces de pago y consultar su estado, usando el puerto asincrónico existente y AddStandardResilienceHandler(), manteniendo disponibles los Fakes."
S2. "No incluyas el caso de uso ni el polling, que corresponden a la #11."
S3. "Revisá la documentación actual de Padi y las firmas del código antes de proponer el plan." "Trabajá en una rama nueva desde main actualizada, no directamente en main."
S4. "No leas ni muestres credenciales. Si necesitás una API key, indicame cómo configurarla localmente, sin pedirme que la pegue en el chat. El harness bloqueaba .env.example; si sigue ocurriendo, no eludas el bloqueo: pasame el contenido exacto para editarlo manualmente."
S5. "Podés implementar y ejecutar las verificaciones permitidas por AGENTS.md. No hagas push, abras una PR ni hagas merge sin mi autorización."
S6. "si y documentá el motivo por el cual se hizo la excepcion"

## Tasks
T1 | S3,S6 | inline | done | branch from origin/main6437126; authorized exception comment6781231639; evidence commit52d2b0f.
T2 | S1,S2,S4,S5,S6 | delegated worker | done | adapter/configuration/README/resilience10.0.0; final build/test passed; commit52d2b0f. Risk: item 5 (dependency addition), item 2 (server-side API key handling).
T3 | S1-S6 | independent verify + parent | done | independent S1-S6 accepted after two corrections; final parent build/test/readback passed; commit52d2b0f, no publishing. Live sandbox proof and mission integration (#11) not claimed.

## Log
L1. Original user request (verbatim):
Quiero continuar con la planificación e implementación de la issue #20 de
 BraianMedrano/pad-skyroute: Padi payment adapter (async).

   Recuperá el contexto de Engram del proyecto exacto pad-skyroute, incluido el último resumen
 de continuidad, y leé AGENTS.md antes de trabajar. Verificá el estado actual del repositorio,
 la issue #20, sus dependencias #5 y #35 y la PR #22; no asumas que el estado guardado sigue
 vigente.

   Al terminar la sesión anterior, main local estaba actualizada hasta 6437126, con el puerto
 de pagos asincrónico y su Fake integrados mediante la PR #36. La #5 está completada y
 mergeada. La #20 ahora es obligatoria, no opcional.

   El objetivo es implementar el adaptador real de Padi para crear enlaces de pago y consultar
 su estado, usando el puerto asincrónico existente y AddStandardResilienceHandler(),
 manteniendo disponibles los Fakes. No incluyas el caso de uso ni el polling, que corresponden
 a la #11.

   Revisá la documentación actual de Padi y las firmas del código antes de proponer el plan.
 Resolvé conmigo solo las decisiones pendientes que realmente bloqueen la implementación.
 Trabajá en una rama nueva desde main actualizada, no directamente en main.

   No leas ni muestres credenciales. Si necesitás una API key, indicame cómo configurarla
 localmente, sin pedirme que la pegue en el chat. El harness bloqueaba .env.example; si sigue
 ocurriendo, no eludas el bloqueo: pasame el contenido exacto para editarlo manualmente.

   Podés implementar y ejecutar las verificaciones permitidas por AGENTS.md. No hagas push,
 abras una PR ni hagas merge sin mi autorización., crea la rama correspondiente para planificar e implementar allí la issue #20

L2. 2026-11-10 user authorization (verbatim): "si y documentá el motivo por el cual se hizo la excepcion"
L3. Reconciliation: AGENTS.md read; exact Engram project pad-skyroute, full observations7695/7696 read. git fetch succeeded; main/origin/main6437126; new feature branch created. #5/#35 closed, PR36 merged; #20 open mandatory; PR22 still open756f29c. Only preexisting .engram untracked. No credentials read.
L4. Exception authorized and posted: https://github.com/BraianMedrano/pad-skyroute/issues/20#issuecomment-6781231639 . Infrastructure.csproj must reference Microsoft.Extensions.Http.Resilience to supply required AddStandardResilienceHandler; existing framework reference does not supply it. Package is allowlisted; no new projects/interfaces/architecture. No other allowed-area expansion.
L5. Current code: CreatePaymentLinkAsync(Mission, decimal, CancellationToken) -> PaymentLink(Id,Amount,CheckoutUrl,ExpiresAt); GetPaymentStatusAsync(string,CancellationToken) -> Pending/Paid/Expired. No port changes. UseFakes=true keeps all four Fakes; false registers only real payment, other ports remain unregistered as today.
L6. Fresh Padi docs: https://padi-api.mintlify.site/api/primer-cobro.md ; api/endpoints/crear-cobro.md, consultar-enlace.md, listar-pagos.md, consultar-pago.md; api/errores-y-reintentos.md. Base https://padi.runasp.net, X-Api-Key server-side. POST /api/v1/integrations/payment-links amount,title,currency ARS,type SingleUse,ttlMinutes30 yields201 id,amount,checkoutUrl,expiresAt. Disable POST retries in standard handler: no idempotency/deduplication. GET link by id, received-payments?limit=100, then received-payment by id. Only Succeeded and matching link/amount/ARS confirms Paid, never link Completed alone. Link Active/Completed/Expired/Cancelled; check expiresAt as provider need not refresh Expired. List newest100, no pagination: absence means unconfirmed, not proof of unpaid. No custom retries, caching, polling, persistence, or automatic recovery. HTTP/malformed errors must not become Paid; propagate safe exceptions without credential/body logs. Recommend Cancelled as unavailable/expired link (document mapping); unknown status fails explicitly.
L7. Worker mwb9x2bd-1-c3yb completed T2. First build failed compilation in status mapping, corrected; subsequent build0 warnings/errors and tests45/45 passed, Application.Tests empty. Independent verifier mwba5t9a-2-vusg found missing strict201 and missing key-generation instructions; both corrected, limited recheck mwba9dt5-3-pspw accepted S1/S4. Final parent build/test/diff-check job-2 exit0, build0 warnings/errors,45 passed/0 failed/0 skipped; Application.Tests has no tests. No adapter tests or live Padi proof. Native assess unavailable due undeclared untracked files; escalated item5, independent verification completed; RDD off, no lifecycle continuation. Final issue-comment readback confirmed exception text. Temporary verifier scratch retained, no deletion outside project attempted.
L8. Verification approach: adapter tests prohibited by AGENTS.md, so no meaningful authorized RED; use build, existing Domain/Application tests, structural review. No live authenticated check without privately configured credentials and sandbox execution; do not inspect .env. .env.example access previously denied, do not bypass. Document exact manual example block in External/README.md if still denied. Parent context backstop triggered, so bounded implementation delegated; independent verification required for dependency/API-key risk.

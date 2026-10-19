# Security checklist — Talent Assistant

Scope: .NET 10 · ASP.NET Core Web API · Blazor Web App (InteractiveServer) · Clean Architecture modular monolith · EF Core/MySQL · synthetic HCM source · read-only PostgreSQL HCM connector · assistant with Groq (PoC egress accepted 2026-10-19) · SDD specs. For each item record one state: `Confirmed vulnerability`, `Potential risk (needs validation)`, `Verified control (evidence)`, `Not applicable (why)`, `Not verified`. Evidence = `file:symbol` or executed command + observed result.

## 1. Identity and session

1.1 JWT signing key: ≥32 chars, from env/User Secrets only, fail-fast at startup (`Authentication:SigningKey` validation); never in repo, images, or logs. OWASP A02/Cheat Sheet "JWT".
1.2 Cookie auth: `HttpOnly`, `Secure`, `SameSite` posture; remember-me cookie lifetime (30 d) justified; no token in URLs/localStorage.
1.3 Login: seed credentials only local; password hashing = Identity defaults (PBKDF2) — confirm no custom weak hasher; throttling/lockout enabled (Identity defaults) — confirm configured, not disabled.
1.4 Token lifetime vs revocation: 60-min JWT, no server-side revocation — acceptable for PoC? State residual risk (stolen token until expiry).

## 2. Authorization

2.1 Every data endpoint requires authorization (`.RequireAuthorization()` or FallbackPolicy); verify on NEW endpoints added by the diff (the `/chat` and workforce endpoints were the recent additions).
2.2 Server-side enforcement: controls exist in API, not only in Blazor UI (UI hiding is not a control).
2.3 IDOR: employee-scoped data (`/timeoff`, `/tasks`) keyed by employee id from the request — confirm any authenticated user may read any employee by design (single-org PoC) and record it as an accepted design decision, or flag it.
2.4 Multi-company isolation: `Not applicable` for single-tenant PoC with justification; becomes High the moment a second org exists.
2.5 Assistant tool authorization: tools execute with the caller's session (no privilege escalation through the model); catalog is fixed (no user-selectable tools).

## 3. Data exposure and privacy

3.1 Errors: API problem details in English, no stack traces/secrets/person values to users; degradation messages localized without internals.
3.2 Logs: no person values or query arguments with PII; connection strings and keys redacted (verify logging config + any `Log.*` of payloads).
3.3 LLM egress: questions + tool results travel to Groq — accepted for PoC (SPEC-011 §10, 2026-10-19); verify the decision is documented and no REAL personal data is used yet; productive phase must re-open ADR-0011.
3.4 Responses: names/absences returned to authenticated users by design; confirm no salary/health/credential-class attributes exist in fixtures or contract views.
3.5 Assistant trace: `toolsUsed` exposure is tool names only, no raw records beyond the answer.

## 4. Injection and input handling

4.1 SQL: EF Core parameterized everywhere; HCM connector = fixed `const` SQL + Npgsql parameters only; zero dynamic SQL (project rule) — grep for string-built SQL in diffs.
4.2 HCM connector hardening: read-only DB role over views, `SET TRANSACTION READ ONLY`, statement timeout, grants deny physical tables (evidence: live probes from SPEC-011).
4.3 Prompt injection: user text reaches the model; residual risk = model steered within its fixed tool catalog; guardrails (SPEC-010) scope it; verify out-of-scope refusals still hold after prompt changes (bench scripts).
4.4 Model/tool arguments: provider validates against JSON Schema; executor normalizes `null`→absent; hostile argument shapes covered by tests (`PostgresSourceSafetyTests`, injection shape).
4.5 Blazor/forms: antiforgery defaults intact; validation duplicated server-side; input length caps (chat message/history caps) enforced server-side, not only in UI.
4.6 Deserialization: System.Text.Json only; no `TypeNameHandling`-style polymorphic deserialization; jsonb arrays from HCM views parsed defensively.

## 5. Secrets and configuration

5.1 No secrets in repo: scan diffs for key-shaped patterns (`gsk_[A-Za-z0-9]{8,}`, `Password=`, PEM blocks); `.env` git-ignored; `.env.example` placeholders only.
5.2 Env-only secrets: JWT key, Groq key, MySQL/HCM passwords arrive via environment; containers receive them via compose interpolation; never baked into images.
5.3 Fail-fast config: missing signing key or Postgres source without connection string fails startup (no silent fallback; no synthetic fallback in real mode).
5.4 Dev/prod parity: Development-only pages/config not reachable in production builds; DataProtection keys warning in container (persistence decision documented).

## 6. Dependencies and supply chain

6.1 New packages pinned to concrete versions (recent: `Microsoft.AspNetCore.Mvc.Testing` 10.0.13, `Npgsql` 10.0.3); no floating versions; every addition user-authorized.
6.2 No unexpected transitive exposure (test host packages confined to test project; Npgsql confined to Infrastructure).
6.3 SDK/runtime: net10.0 consistent across projects; container images from official repos.

## 7. Transport, network, and hosts

7.1 All published ports bound to `127.0.0.1` (Compose); no LAN exposure of MySQL/Postgres/API/Web.
7.2 Real HCM Postgres: `Ssl Mode=VerifyFull` required by guide/contract (pending real-DB validation evidence); local test PG loopback-only, no TLS (documented exception).
7.3 No outbound calls beyond Groq from the API; SSRF surface `Not applicable` (no user-controlled URLs fetched) with justification.

## 8. AI-specific controls (assistant)

8.1 Boundary: model never touches data or network; tools execute server-side with caller identity (ADR-0003/0011/0012).
8.2 Guardrails: scope rule as first prompt instruction; adversarial bench green (5 cases + 7 reference questions); verify after any prompt edit.
8.3 No autonomous writes (FR-015): tool catalog has read-only tools only.
8.4 Limits: timeouts, max tool rounds, history caps, provider 429/503 degradation without blind retries.
8.5 Provider hygiene: key only in env; model id and base URL config; degradation reason codes machine-readable, user messages localized.

## 9. Blazor / ASP.NET Core specifics

9.1 InteractiveServer circuits: no sensitive state in circuit beyond auth cookie; prerendering does not leak data to unauthenticated SSR (protected routes render after auth).
9.2 FallbackPolicy covers Blazor routes, not only API; `AuthorizeRouteView` intact; no `[AllowAnonymous]` beyond login.
9.3 Static assets: no config/secrets under `wwwroot`; resx resources contain no real data.
9.4 Health endpoints: `/health` reveals no internals.

## 10. Operational and database posture

10.1 MySQL app user: least privilege (no root in app connection; app migrations user separated if applicable).
10.2 Postgres test DB: app role = SELECT on contract views only (live-probed); admin credentials local-only.
10.3 Backups/volumes: local dev volumes only; no real data at risk today.
10.4 Audit trail: login and chat activity not persisted server-side (PoC) — state as accepted gap or propose logging AC if needed.

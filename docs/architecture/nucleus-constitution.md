# Nucleus Constitution

> The single source of truth for how Nucleus is built. Every contributor — human or
> agent — reads this before changing code, and every pull request is checked against it.
> When the code and this document disagree, that is a bug in one of them: fix it, don't
> ignore it.
>
> **Status:** v0 — 2026-07-02. Grounded in a live audit of `master` (which surfaced
> NUC-ISO-4). Maintained going forward by the weekly architecture-review agent.

## 1. What Nucleus is
Multi-tenant SaaS rebuild of SEO Hub, sold to external businesses. .NET 9 + Blazor WASM +
EF Core + PostgreSQL (Supabase) + Stripe + Hangfire, deployed on Railway. Clean
architecture. Five service hubs: Content (blue), Search (green), Authority (purple),
Distribution (amber), Studio (pink).

## 2. Layering (clean architecture — dependencies point inward)
| Project | Holds | May depend on |
|---|---|---|
| `Nucleus.Domain` | Entities, `TenantEntity` base | nothing |
| `Nucleus.Application` | CQRS commands/queries (MediatR), interfaces | Domain |
| `Nucleus.Infrastructure` | `NucleusDbContext`, EF configs, auth, external services | Application, Domain |
| `Nucleus.Api` | Thin controllers → MediatR, middleware | Application (+ Infra at composition root) |
| `Nucleus.Web` | Blazor WASM per hub | Api contracts only |

Controllers are thin — no business logic; MediatR handlers drive it. Never reference
Infrastructure from Domain or Application.

## 3. Multi-tenancy — the non-negotiable invariants
The one area where a mistake leaks one customer's data to another. Enforced by the
tenant-isolation gate (`scripts/tenant-gate/gate.sh` + `tests/Nucleus.Architecture.Tests`).

| # | Invariant | Status on `master` (2026-07-02) |
|---|---|---|
| 1 | **No secrets in committed config.** Connection-string passwords, JWT signing keys, Stripe secret keys come from env / user-secrets — never committed `appsettings*.json`. | ⚠️ dev DB password + dev JWT key committed in `Nucleus.Api/appsettings.Development.json` → **NUC-ISO-3** |
| 2 | **RLS on every tenant table.** Each tenant table has Row-Level Security enabled with a `TenantId` policy — a database backstop independent of the app. | ❌ not implemented anywhere → **NUC-ISO-1** |
| 3 | **`TenantId` auto-stamped on insert.** `SaveChangesAsync` sets `TenantId` from `ICurrentTenantService` on new `TenantEntity` rows. | ❌ `SaveChangesAsync` only stamps `UpdatedAt` → **NUC-ISO-2** |
| 4 | **Global query filter on every `TenantEntity`, evaluated per request.** Applied in `OnModelCreating`; must reference the *current* tenant, not a baked constant. No `IgnoreQueryFilters()` without `// tenant-gate:allow <reason>`. | ⚠️ filter is **present but frozen** — `OnModelCreating` bakes `Expression.Constant(TenantId)` at first model build (EF caches the model once), so it sticks on the first tenant seen (in practice `Guid.Empty`). App-layer isolation does not currently work → **NUC-ISO-4** |

The gate turns each ❌/⚠️ into a merge-checkable target: implement the fix, un-skip its test /
flip its check, done.

## 4. Plan-gating
Feature access gates through `TenantPlanService`. Never hardcode a plan check
(`if plan == "pro"`) — always ask the service. Every AI-backed feature has a known
cost-per-use and a plan limit.

## 5. Config & secrets
`Nucleus.Api/appsettings.json` holds non-secret defaults only. Secrets → Railway env vars
(prod) / `dotnet user-secrets` (dev). Blazor `wwwroot/appsettings*.json` is shipped to the
browser — treat it as fully public, client config only.

## 6. Data & migrations
Single Supabase database, tenant-scoped — never per-tenant databases. Schema changes are EF
migrations only; never `EnsureCreated`. Background jobs are `[DisableConcurrentExecution]`.

## 7. The ship gate (from CLAUDE.md — a feature enters Nucleus only when all six pass)
Works end-to-end in SEO Hub · stable data model (2+ weeks) · clear tenant boundary · AI cost
understood · 2 weeks real use with no structural change requests · describable in one sentence.

## 8. How this document stays true
- **On every PR:** the tenant-isolation gate runs in CI. A red gate blocks merge.
- **Weekly:** an architecture-review agent reads the live code against this document and
  reports drift. *(This v0 audit already did exactly that — it found NUC-ISO-4, a frozen
  query filter that "a filter exists" checks would miss.)* Drift becomes backlog items.
- **When code and this doc disagree:** that's the bug. Fix whichever is wrong in the same PR.

## Current backlog (born from the 2026-07-02 audit, in priority order)
- **NUC-ISO-4** — make the tenant query filter dynamic per-request *(live isolation break; small; do first)*
- **NUC-ISO-1** — add RLS + `TenantId` policy migrations for every tenant table *(database backstop)*
- **NUC-ISO-2** — auto-stamp `TenantId` in `SaveChangesAsync`; un-skip its architecture test
- **NUC-ISO-3** — move dev secrets out of committed config into user-secrets; confirm prod is env-only
- **NUC-ISO-5** — annotate the 4 reviewed-legitimate `IgnoreQueryFilters()` bypasses (`AdminController` SuperAdmin audit view; `BrandProvisioningJob` background job) with `// tenant-gate:allow`, then flip the bypass guard to blocking

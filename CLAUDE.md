# Nucleus — Claude Code Guide

## MEMORY SYSTEM — Read First, Every Session

Persistent memory lives at `memory/`. Read all files before doing any work.

```
memory/
  strategy.md      ← what Nucleus IS, relationship to SEO Hub, SaaS vision, north star
  architecture.md  ← tenant hierarchy, service hub structure, shell pattern, DB principles
  sprints.md       ← sprint history (1-22 complete), current sprint, what's next
  entities.md      ← all domain entities, relationships, key fields
  decisions.md     ← architectural decisions with rationale
```

### Session Protocol
1. **START:** Read all files in `memory/` — especially `sprints.md` for current state
2. **DURING:** Update files immediately when: a decision is made, a feature ships, a pattern is confirmed
3. **END:** Update `sprints.md` with what shipped this session

---

## What This Is

**Nucleus** is a multi-tenant SaaS Marketing OS built in C#/.NET 9 + Blazor WASM. It is the clean-architecture rebuild of SEO Hub — designed to be sold as a product to other businesses. Multi-tenancy, billing, and correct data isolation are built in from the start.

**SEO Hub** (separate repo: TK-DL-Master-Databaset) is Tim's internal operational tool. It continues to run as-is. Nucleus is the SaaS product version.

**Deploy:** Commit + push to `main` → Railway auto-deploys both the API and Blazor WASM.

---

## Tech Stack

| Layer | Tech |
|-------|------|
| Backend API | C# .NET 9, ASP.NET Core, CQRS via MediatR |
| Frontend | Blazor WASM (client-side) |
| ORM | EF Core 9 + EF Migrations (never EnsureCreated) |
| Database | Supabase PostgreSQL — one DB, TenantId-scoped RLS |
| Auth | JWT access (60min) + refresh token (30-day rotation) |
| Background jobs | Hangfire (locked with DisableConcurrentExecution) |
| Billing | Stripe (checkout, portal, webhooks) |
| Email | SMTP (password reset, verification, campaigns) |
| Monitoring | Sentry |
| CI/CD | GitHub Actions (build + test on every PR) |
| Hosting | Railway (API + WASM + Hangfire in single service) |

---

## Architecture at a Glance

```
Tenant (owner/company)
  └── Brand (one or many per tenant)
        └── All tenant data is scoped via TenantId + BrandId
              Keywords, Contacts, Content, Campaigns, etc.

MainLayout.razor → global nav + service switcher
  └── ContentLayout.razor   → blue theme + Content focus menu
  └── SearchLayout.razor    → green theme + Search focus menu
  └── AuthorityLayout.razor → purple theme + Authority focus menu
  └── DistribLayout.razor   → amber theme + Distribution focus menu
  └── StudioLayout.razor    → pink theme + Studio focus menu
```

**5 Service Hubs** (see `memory/architecture.md` for full detail):
1. **Content Hub** — keywords, AI generator, editorial calendar, content library, link health
2. **Search Hub** — rankings, alerts, topic clusters, content gaps, SERP features, GEO/AI visibility
3. **Authority Hub** — backlinks, brand mentions, press releases, schema manager, outreach
4. **Distribution Hub** — social media, email blasts, campaigns, send log, reviews
5. **Studio Hub** — page manager, design studio, image gen, video, asset library, HeyGen

---

## Critical Rules

1. **SEO Hub = test server, Nucleus = live production** — Features are built and proven in SEO Hub first. A feature does NOT ship to Nucleus until it is fully working, multi-tenanted, and plan-gated. Never port a half-built feature.
2. **Only fully working services ship** — A service hub is not added to Nucleus until ALL of its core features work end-to-end. No placeholder pages, no "coming soon" sections, no partial implementations in production.
3. **Tenant isolation is non-negotiable** — every query MUST filter by `TenantId`. No exceptions. Use `TenantEntity` base class.
4. **EF Migrations always** — never `EnsureCreated`. `dotnet ef migrations add [Name]` for every schema change.
5. **One database per deployment** — do NOT create separate Supabase projects per tenant. TenantId + RLS handles isolation.
6. **Build it right from the start** — if the pattern isn't correct, fix the pattern before adding features on top of it. Technical debt in the foundation is never acceptable.
7. **Service hubs are modular** — each hub has its own layout, its own pages folder, its own theme. Never mix hub concerns.
8. **Stripe plan gates everything** — feature access is always checked via `TenantPlanService`. Never hardcode "is pro" checks.
9. **Background jobs** — always `[DisableConcurrentExecution(timeoutInSeconds: 300)]`. Never fire-and-forget without Hangfire.
10. **CQRS** — new features go in `Nucleus.Application/{Feature}/Commands` or `/Queries`. Controllers are thin — they call MediatR only.

---

## Key Files

| File | Purpose |
|------|---------|
| `src/Nucleus.Api/Program.cs` | DI, middleware, route registration |
| `src/Nucleus.Api/Data/NucleusDbContext.cs` | EF Core context + all entity configs |
| `src/Nucleus.Web/Layout/MainLayout.razor` | Global shell — sidebar nav + service switcher |
| `src/Nucleus.Web/Program.cs` | Blazor WASM entry point, HttpClient auth handler |
| `src/Nucleus.Domain/Entities/` | All domain entities |
| `src/Nucleus.Application/` | CQRS commands + queries (MediatR) |
| `src/Nucleus.Infrastructure/Auth/` | JWT, refresh tokens |
| `src/Nucleus.Infrastructure/Multitenancy/` | Tenant resolution middleware |

---

## SEO Hub → Nucleus Ship Gate (check ALL before porting any feature)

A feature is ready to move from SEO Hub to Nucleus when every box is checked:

- [ ] **Works end-to-end in SEO Hub** — every screen loads, every action completes, errors handled. Tim uses it daily without complaints.
- [ ] **Stable data model** — tables and fields haven't changed in 2+ weeks. No open "we need to add a column" items.
- [ ] **Clear tenant boundary** — you can draw a box around exactly what data belongs to one brand/tenant. No ambiguous cross-brand data pulls.
- [ ] **AI cost is understood** — if it uses Claude/Flux/etc., cost-per-use is known and a plan limit is defined.
- [ ] **2 weeks of real use without major change requests** — Tim has used it for 2+ weeks and stopped asking for structural changes.
- [ ] **Describable in one sentence** — if you can't explain it simply to a customer, it's not ready.

Only after all 6 pass: write it up in `memory/sprints.md` as "proven, ready to port" and sprint it into Nucleus.

---

## Adding a Feature — Checklist

1. Domain entity in `Nucleus.Domain/Entities/` — inherits `TenantEntity` if tenant-scoped
2. EF config in `NucleusDbContext.cs` + `dotnet ef migrations add [Name]`
3. Command/Query in `Nucleus.Application/{Feature}/`
4. Controller endpoint in `Nucleus.Api/Controllers/` — thin, calls MediatR
5. Blazor page in `Nucleus.Web/Pages/{Hub}/` — uses correct hub layout
6. Plan gate: check `TenantPlanService` if feature is plan-gated

---

## Production

- **Railway:** https://nucleus-production.up.railway.app/ (verify current URL)
- **Database:** Supabase PostgreSQL (same account as TK-DL-Master)
- **Never run locally for production ops** — commit + push to deploy

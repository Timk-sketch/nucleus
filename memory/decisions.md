# Nucleus — Architectural Decisions

## 2026-07-30 — Tenant Isolation

### Tenant Query Filter References the DbContext Instance, Not a Captured Value (NUC-ISO-4)
**Decision:** `OnModelCreating` builds every `TenantEntity` query filter against the
`NucleusDbContext.CurrentTenantId` instance property. Reading `tenantService.TenantId` into a local
and baking it in with `Expression.Constant` is banned.

**Rationale:** EF builds the model once and caches it per context type. A baked `Guid` froze the
filter to whichever tenant triggered model building — in practice `Guid.Empty`, from the startup
health check resolving a context with no HTTP user. App-layer tenant isolation did not work at all.
EF recognises a constant that *is* the context and rewrites the member access into a query
parameter, re-read on every query; that is the documented multi-tenant pattern and it costs nothing.

**Rejected:** a per-tenant `IModelCacheKeyFactory`. It would compile and cache a separate model per
tenant, converting a bounded cache into one that grows with the customer count — paying a large
price to solve a problem that an instance member solves for free.

**Implication:** `CurrentTenantId` is `public` rather than the `private` the work order sketched, so
that EF's dynamically built expression can read it without depending on private-member access from
generated code. It is also the natural hook for NUC-ISO-2's insert stamping.

**Guarded by:** `Tenant_query_filter_is_evaluated_dynamically_not_frozen` (no `Guid` constant in the
expression) and `Each_context_sees_only_its_own_tenants_rows_through_the_shared_model` (two tenants
plus an empty tenant, all reading through one cached model). Both run in `tenant-isolation-gate`.

---

## 2026-05-26 — Core Strategic Decisions

### SEO Hub = Test Server, Nucleus = Live Production
**Decision:** SEO Hub is Tim's internal operational tool and serves as the test/staging environment where features are prototyped. When a feature is proven working in SEO Hub, it is ported into Nucleus with proper multi-tenancy.

**Rationale:** Don't rebuild what's already working. Test with real data (Tim's brands), then productize. This avoids building features nobody needs.

**Implication:** Nothing ships to Nucleus until it has been proven in SEO Hub.

---

### Only Fully Working Services Ship to Nucleus
**Decision:** A service hub is not added to Nucleus until ALL of its core features work correctly end-to-end. No placeholder pages, no partial implementations.

**Rationale:** Nucleus is a paid product. Paying customers see a complete, working tool — not a roadmap.

**Implication:** Sprint 23 builds the shell. Sprint 24 builds Content Hub FULLY before Sprint 25 starts.

---

### One Database, TenantId Isolation (Not Separate DBs)
**Decision:** Single Supabase PostgreSQL database. All tenant data is scoped via TenantId + RLS. No separate Supabase projects per tenant.

**Rationale:** Cross-tenant queries (e.g., admin reporting, benchmarking) would require joining across separate DBs — impractical. Single DB with proper indexing handles 1000+ tenants fine.

**Implication:** Every query must filter by TenantId. TenantEntity base class is mandatory for all tenant-scoped entities.

---

### Shell Pattern UX (Not Separate Pages Per Hub)
**Decision:** Nucleus uses a shell application pattern. MainLayout is always visible. Each of the 5 service hubs has its own Layout extending MainLayout with a hub-specific focus menu and theme color. Switching hubs = zero page reload.

**Rationale:** A multi-hub product where every hub switch causes a full page load degrades UX and breaks shared state (brand selector, unsaved edits). The shell pattern keeps the global context alive.

**Implication:** MainLayout.razor is the global shell. Five sub-layouts (ContentLayout, SearchLayout, etc.) extend it. Pages declare `@layout ContentLayout` etc.

---

### Build the 5 Hubs in Order: Content First
**Decision:** Content Hub ships first because (a) content features are the most proven in SEO Hub, (b) keyword + AI generation is the highest-value feature for initial customers, (c) it exercises all the multi-tenant patterns (TenantId scoping, AI cost tracking, plan gates).

**Order:** Content → Search → Distribution → Authority → Studio

---

### Brands Are Tenant-Scoped, Not Global
**Decision:** A Brand belongs to one Tenant. Users within that tenant can see and manage that tenant's brands. Cross-tenant brand visibility is only possible for SuperAdmin.

**Rationale:** This is the core isolation that SEO Hub lacks. All brand credentials (GHL API keys, WP passwords, Stripe keys) must never leak across tenants.

---

### AI Costs Are Per-Tenant Metered
**Decision:** All AI generation (Claude API calls) are tracked per-tenant and enforced by plan tier. Starter = limited generations/month. Pro/Agency = higher limits or unlimited.

**Rationale:** AI is the biggest variable cost. Without metering, a single heavy tenant could exhaust the budget for all others.

**Implementation:** `AiUsage` entity (TenantId, BrandId, Feature, TokensUsed, Cost, Timestamp). Checked before every generation. Blocked + notified when limit reached.

---

## Pre-2026-05-26 Architecture Decisions

### Single Railway Service (API + Blazor WASM + Hangfire)
**Decision:** All three components run in the same Railway service.
**Rationale:** Cheaper. Hangfire uses in-memory at this scale. Split when Redis is needed.

### JWT + Refresh Token (Not Cookie Sessions)
**Decision:** JWT access tokens (60min) with refresh tokens (30-day rotation) stored in localStorage.
**Rationale:** Blazor WASM needs to attach auth headers to API calls. Cookie-based auth is more complex in WASM. JWT is simpler and works well at this scale.

### MediatR CQRS (Not Direct Service Injection)
**Decision:** All business logic goes in MediatR Commands/Queries. Controllers are thin dispatchers.
**Rationale:** Enables pipeline behaviors (validation, auth, audit logging) without touching controller code. Easier to test in isolation.

### EF Core Migrations (Not EnsureCreated)
**Decision:** All schema changes via `dotnet ef migrations add`. EnsureCreated removed in Sprint 17.
**Rationale:** EnsureCreated doesn't handle incremental changes. Migrations give a reversible, auditable schema history.

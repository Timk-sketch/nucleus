# Nucleus — Architecture Reference

## Tenant Hierarchy

```
Owner/User (ApplicationUser)
  └── Tenant (the company buying Nucleus)
        └── Brand (one or many per tenant — e.g. Dirt Legal, Ride Legal, MRS)
              └── All data: Keywords, Content, Contacts, Campaigns, Rankings, etc.
```

**TenantEntity base class** — all tenant-scoped entities inherit this:
```csharp
public class TenantEntity {
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
}
```

**Every query must filter by TenantId.** The tenant context is resolved by `ICurrentTenantService` (JWT claim → TenantId). This is the hard wall that stops cross-contamination.

---

## Service Hub UX Architecture (Shell Pattern)

The UX is a **shell application** — global nav is always visible, but the service context (focus menu + theme color) changes based on which hub you're in.

```
MainLayout.razor
├── Sidebar (always visible)
│   ├── Nucleus logo
│   ├── Service switcher (5 hub pills — Content / Search / Authority / Distribution / Studio)
│   ├── Global items: Dashboard, Brands, Team, Settings, Billing
│   └── [Active hub focus menu renders here]
├── Topbar
│   ├── Current hub name + theme color strip
│   ├── Brand selector (global — persists via localStorage)
│   └── User menu
└── @Body (page content)
```

**Each service hub has its own layout file** that extends MainLayout and injects the focus menu + theme:
```
Nucleus.Web/Layout/
  MainLayout.razor          ← global shell
  ContentLayout.razor       ← blue theme + Content focus menu
  SearchLayout.razor        ← green theme + Search focus menu
  AuthorityLayout.razor     ← purple theme + Authority focus menu
  DistributionLayout.razor  ← amber theme + Distribution focus menu
  StudioLayout.razor        ← pink theme + Studio focus menu
```

Each hub's pages declare their layout:
```razor
@page "/content/keywords"
@layout ContentLayout
```

**Switching between hubs = zero page reload.** The shell stays mounted; only @Body replaces.

---

## Database Design Principles

1. **One database** — never separate Supabase projects per tenant
2. **TenantId on every tenant-scoped table** — EF HasIndex(t => t.TenantId) always
3. **Composite indexes** — `(TenantId, BrandId)` on all junction tables
4. **EF Core Migrations always** — `dotnet ef migrations add [Name]` for every schema change
5. **RLS** — Supabase row-level security as defense-in-depth, but EF TenantId filter is the primary guard
6. **No cross-tenant queries** — service layer never queries without tenant context

---

## Current Domain Entities (Sprint 22)

| Entity | Inherits | Key Fields |
|--------|----------|------------|
| Tenant | — | Id, Name, Slug, Plan, StripeCustomerId, IsActive |
| Brand | TenantEntity | Code, Name, Domain, Slug, PrimaryColor, GhlLocationId, GhlApiKey, WpSiteUrl, DataForSeoTag, EmailProvider |
| ApplicationUser | IdentityUser | TenantId, RefreshToken, LockoutEnabled |
| BrandKeyword | TenantEntity | BrandId, Keyword, CurrentRank, PeakRank |
| KeywordRank | TenantEntity | BrandKeywordId, Rank, CheckedAt |
| GhlContact | TenantEntity | BrandId, GhlId, Email, Phone, Tags |
| EmailCampaign | TenantEntity | BrandId, Name, Subject, Status, SentAt |
| AuditLog | — | Actor, Action, EntityType, EntityId, DiffJson, Timestamp |
| RefreshToken | — | UserId, Token, ExpiresAt, IsRevoked |

---

## CQRS Pattern (Application Layer)

```
Nucleus.Application/
  {Feature}/
    Commands/
      Create{Entity}Command.cs      ← IRequest<Guid>
      Create{Entity}Handler.cs      ← IRequestHandler<Create{Entity}Command, Guid>
    Queries/
      Get{Entity}Query.cs
      Get{Entity}Handler.cs
    DTOs/
      {Entity}Dto.cs
```

Controllers call MediatR only:
```csharp
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateBrandCommand cmd)
    => Ok(await _mediator.Send(cmd));
```

---

## Background Jobs (Hangfire)

All jobs must have `[DisableConcurrentExecution(timeoutInSeconds: 300)]`.

| Job | Schedule | Purpose |
|-----|----------|---------|
| KeywordRankJob | Nightly + on-demand | DataForSEO rank checks |
| GhlContactSyncJob | Per-brand schedule | GHL contact sync |
| BrandProvisioningJob | On-demand | New brand setup (WP/GHL verification) |

---

## Plan Tiers

| Feature | Starter | Pro | Agency |
|---------|---------|-----|--------|
| Brands | 1 | 5 | Unlimited |
| Keywords | 20 | 100 | Unlimited |
| GHL Contacts sync | — | Yes | Yes |
| Email Campaigns | — | Yes | Yes |
| Team members | 1 | 5 | Unlimited |
| All 5 Service Hubs | Partial | Full | Full |

Feature gates checked via `TenantPlanService.CanUse(feature)` — never inline conditionals.

---

## File Structure

```
Nucleus.sln
src/
  Nucleus.Api/
    Controllers/        ← thin HTTP handlers, MediatR only
    Data/               ← NucleusDbContext, migrations
    Jobs/               ← Hangfire job classes
    Middleware/         ← tenant resolution, auth middleware
    Program.cs
  Nucleus.Application/
    Auth/               ← login, register, JWT commands
    Brands/             ← brand CRUD + provisioning
    Common/             ← shared DTOs, interfaces
  Nucleus.Domain/
    Entities/           ← all domain entities
    Events/             ← domain events
  Nucleus.Infrastructure/
    Auth/               ← JWT generation, refresh tokens
    Multitenancy/       ← ICurrentTenantService, middleware
  Nucleus.Web/
    Layout/             ← MainLayout + per-hub layouts
    Pages/              ← Blazor pages organized by hub
    Services/           ← AuthService, JwtAuthStateProvider
    wwwroot/
tests/
  Nucleus.Tests/        ← xUnit, auth + brand + analytics tests
```

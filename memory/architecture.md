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

**Focus menu CSS pattern:** use `.hub-focus-menu` nav + `.hub-focus-item` NavLink (not `.hub-focus-nav`/`.nav-item`). See SearchLayout.razor.

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

## Current Domain Entities (Sprint 27)

| Entity | Inherits | Key Fields |
|--------|----------|------------|
| Tenant | — | Id, Name, Slug, Plan, StripeCustomerId, IsActive |
| Brand | TenantEntity | Code, Name, Domain, Slug, PrimaryColor, GhlLocationId, GhlApiKey, WpSiteUrl, DataForSeoTag, EmailProvider |
| ApplicationUser | IdentityUser | TenantId, RefreshToken, LockoutEnabled |
| BrandKeyword | TenantEntity | BrandId, Keyword, CurrentRank, PeakRank |
| KeywordRank | TenantEntity | BrandKeywordId, Rank, CheckedAt |
| KeywordRankSnapshot | TenantEntity | BrandId, KeywordId, Position, Url, SearchVolume, Competition, CheckedAt |
| SearchAlert | TenantEntity | BrandId, KeywordId, AlertType, Threshold, IsActive, TriggeredAt, Message |
| TopicCluster | TenantEntity | BrandId, Name, PillarKeyword, ClusterKeywordsJson, Status, Notes |
| GhlContact | TenantEntity | BrandId, GhlId, Email, Phone, Tags |
| EmailCampaign | TenantEntity | BrandId, Subject, HtmlBody, Status, RecipientCount, SentAt |
| EmailCampaignMessage | TenantEntity | CampaignId, BrandId, Subject, HtmlBody, SentAt, OpenCount, ClickCount, RecipientCount, Status |
| SocialPost | TenantEntity | BrandId, Platform, Caption, ImageUrl, ScheduledAt, PublishedAt, Status, ExternalPostId, Provider |
| SendLog | TenantEntity | BrandId, CampaignId?, SocialPostId?, Channel, RecipientCount, SentAt, Provider, Status |
| BacklinkRecord | TenantEntity | BrandId, SourceUrl, TargetUrl, AnchorText, DomainRating, FirstSeenAt, LastSeenAt, IsActive |
| BrandMention | TenantEntity | BrandId, SourceUrl, MentionText, Sentiment, DiscoveredAt, IsReviewed |
| SchemaTemplate | TenantEntity | BrandId, PageType, SchemaType, TemplateJson (jsonb), IsActive |
| OutreachQueueItem | TenantEntity | BrandId, TargetUrl, ContactEmail, Status, Notes, OutreachAt |
| AuditLog | — | Actor, Action, EntityType, EntityId, DiffJson, Timestamp |
| RefreshToken | — | UserId, Token, ExpiresAt, IsRevoked |

---

## CQRS Pattern (Application Layer)

```
Nucleus.Application/
  {Feature}/
    Commands/
      Create{Entity}Command.cs      ← IRequest<Guid> — command + validator + handler in one file
    Queries/
      Get{Entity}Query.cs           ← IRequest<T> — query + handler in one file
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
| Social Scheduling | 3/month | Unlimited | Unlimited |
| Team members | 1 | 5 | Unlimited |
| All 5 Service Hubs | Partial | Full | Full |
| Backlink Tracking | — | Yes | Yes |
| Brand Mentions | — | Yes | Yes |
| Schema Editor | View | Edit | Bulk |
| Outreach Queue | — | — | Yes |

Feature gates checked via `TenantPlanService.CanUse(feature)` — never inline conditionals.

---

## Feature Isolation Rule (Authority Hub pattern)

Each hub is a **fully self-contained vertical slice**:
- Application layer: ALL files under `Nucleus.Application/{HubName}/` (Commands/, Queries/, DTOs/)
- Controller: ONE file at `Nucleus.Api/Controllers/{Hub}Controller.cs`
- Blazor pages: under `Nucleus.Web/Pages/{Hub}/`
- Hub-specific interfaces (if any): `Nucleus.Application/{HubName}/Interfaces/`
- NEVER import from another hub's namespace
- Shared kernel only: `Nucleus.Application.Common.Interfaces` (INucleusDbContext, ICurrentTenantService, IAuditService, ITenantPlanService, IEmailService)

---

## File Structure

```
Nucleus.sln
src/
  Nucleus.Api/
    Controllers/        ← thin HTTP handlers, MediatR only
    Data/               ← DesignTimeDbContextFactory
    Jobs/               ← Hangfire job classes
    Middleware/         ← tenant resolution, auth middleware
    Program.cs
  Nucleus.Application/
    Auth/               ← login, register, JWT commands
    Brands/             ← brand CRUD + provisioning
    Common/             ← shared DTOs, interfaces (INucleusDbContext, ICurrentTenantService)
    AuthorityHub/       ← Authority Hub commands/queries/DTOs (Sprint 27)
    Distribution/       ← Distribution Hub commands/queries/DTOs (Sprint 26)
    Search/             ← Search Hub commands/queries/DTOs (Sprint 25)
  Nucleus.Domain/
    Entities/           ← all domain entities
    Events/             ← domain events
  Nucleus.Infrastructure/
    Auth/               ← JWT generation, refresh tokens
    Data/               ← NucleusDbContext + migrations
    Multitenancy/       ← ICurrentTenantService, middleware
  Nucleus.Web/
    Layout/             ← MainLayout + per-hub layouts
    Pages/              ← Blazor pages organized by hub
    Services/           ← AuthService, JwtAuthStateProvider
    wwwroot/
tests/
  Nucleus.Application.Tests/  ← xUnit, auth + brand + analytics tests
```

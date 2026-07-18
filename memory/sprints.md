# Nucleus — Sprint History & Roadmap

## The Two-Platform Model (Decided 2026-05-26)

**SEO Hub = test / staging server**
- Tim's internal operational tool
- Where new features are prototyped, built, and tested against real data
- When a feature is proven working, it gets ported into Nucleus

**Nucleus = live production SaaS**
- Only fully working, complete features ship here
- Multi-tenant, billing-gated, properly isolated
- What other companies will pay for

A feature DOES NOT ship to Nucleus until it has been:
1. Built and tested in SEO Hub
2. Confirmed working end-to-end
3. Designed for multi-tenancy (TenantId scoping, plan gating)

---

## Sprint History (All Complete)

| Sprint | What Shipped |
|--------|-------------|
| 1-3 | Project scaffold, Railway deploy, brand onboarding, SignalR provisioning |
| 4 | Live dashboard data, brand edit/delete |
| 5 | Auth hardening — token refresh, change password, settings page |
| 6 | Team management (invite, roles) |
| 7 | Real WP/GHL verification + invite emails |
| 8 | Tenant/company settings |
| 9 | WordPress blog management + keyword tracking |
| 10 | Real dashboard metrics + brand health |
| 11-14 | GHL contacts sync, keyword rank tracking, email campaigns, EF migrations baseline |
| 15 | Security hardening — Hangfire auth, Sentry, DB indexes, rank check endpoint |
| 16 | Forgot password + reset password flow |
| 17 | EF Core migrations baseline (EnsureCreated removed) |
| 18 | Stripe billing — checkout, portal, webhooks, billing page |
| 19 | Performance — memory cache, Brotli, DisableConcurrentExecution |
| 20 | Audit log + super-admin panel |
| 21 | CI/CD — GitHub Actions build/test, RegisterCommand validator |
| 22 | Plan enforcement, SuperAdmin seed, nightly rank job |
| 23 | Service Hub Architecture — ShellLayout, 5 hub layouts, hub landing pages, amber/green/purple/pink themes |
| 24 | Content Hub — keyword library, AI generator, editorial calendar, content library |
| 25 | Search Hub — rankings dashboard, rank history, alerts, topic clusters, content gaps, page performance |
| 26 | Distribution Hub — social scheduler, email blasts, campaign workspace, send log |
| 27 | Authority Hub — backlinks, brand mentions, schema manager, outreach queue |
| 28 | Studio Hub — page manager, design studio, image generator, asset library |
| 29 | CMS Renderer Hub — public page renderer, custom domains, site deploy, cache invalidation, analytics |

**Current state: Sprint 29 complete. Build: 0 errors, 0 warnings. Tests: 5/5 pass.**

---

## Sprint 29 — CMS Renderer Hub (COMPLETE ✅)

**Build status:** `dotnet build Nucleus.sln` → 0 errors, 0 warnings
**Test status:** `dotnet test` → 5/5 pass

### Domain Entities (all inherit TenantEntity, all tenant-scoped)
- `SiteDomain` — hostname (globally unique), brand_id, is_primary, ssl_verified, verified_at
- `SiteDeployment` — brand_id, deployed_by, page_count, status (pending|running|complete|failed), deployed_at, notes
- `PageCache` — brand_id, slug, rendered_html, etag, cached_at, invalidated_at (unique on BrandId+Slug)
- `SiteVisit` — brand_id, slug, referrer, user_agent, ip_hash (SHA-256), visited_at

### EF Core
- Migration `CmsRendererHub` created — creates `site_domains`, `site_deployments`, `page_caches`, `site_visits` tables
- `site_domains.hostname` has global unique index (hostnames are globally unique across all tenants)
- `page_caches` has unique index `(BrandId, Slug)` — enforces one cache entry per page per brand
- All tables have `(TenantId, BrandId)` composite indexes + domain-specific indexes
- All registered in `INucleusDbContext` interface + `NucleusDbContext` implementation

### MediatR Commands (Nucleus.Application/CmsRendererHub/Commands/)
- `DeploySiteCommand` — snapshots all published WebsitePages → PageCache; creates SiteDeployment record; upserts cache entries; marks status=complete|failed
- `InvalidatePageCacheCommand` — sets InvalidatedAt on a specific BrandId+Slug cache entry; returns bool (found or not)
- `MapCustomDomainCommand` — adds hostname→brand mapping; enforces global hostname uniqueness; demotes existing primary if IsPrimary=true
- `VerifyDomainCommand` — DNS lookup stub (System.Net.Dns); sets SslVerified=true + stamps VerifiedAt on success

### MediatR Queries (Nucleus.Application/CmsRendererHub/Queries/)
- `GetPublicPageQuery` — cache hit path: checks PageCache first (InvalidatedAt==null); on miss: renders HTML from WebsitePage, writes PageCache, logs SiteVisit; returns PublicPageDto with Etag
- `GetSiteDeployStatusQuery` — brand-scoped: published page count, cached page count, deploy history (20 most recent)
- `GetSiteAnalyticsQuery` — brand-scoped: total visits, unique pages, top 10 pages, daily visit counts (configurable day window 1-365)
- `GetCustomDomainQuery` — dual mode: list domains by BrandId (tenant-scoped) OR resolve BrandId from hostname (IgnoreQueryFilters, global lookup)

### DTOs (Nucleus.Application/CmsRendererHub/DTOs/)
- `SiteDomainDto` — id, brandId, hostname, isPrimary, sslVerified, verifiedAt, timestamps
- `SiteDeploymentDto` — id, brandId, brandName, deployedBy, pageCount, status, deployedAt, notes, createdAt
- `PublicPageDto` — slug, title, seoTitle, metaDescription, ogImage, schemaJson, renderedHtml, etag, cachedAt, servedFromCache
- `SiteAnalyticsDto` — brandId, brandName, totalVisits, uniquePages, topPages (List<PageVisitSummary>), dailyVisits (List<DailyVisitCount>)
- `SiteStatusDto` — brandId, brandName, publishedPageCount, cachedPageCount, lastDeployment, deployHistory

### API Controller
- `CmsController` — dual routing: public `/cms/{*slug}` (no auth) + management `/api/cms/*` (auth required)
- `GET  /cms/{*slug}` — public page renderer; resolves brand from Host header; returns 200 HTML with ETag/Cache-Control; 304 on If-None-Match hit; 404 if not found/unpublished
- `POST /api/cms/deploy` — deploy site, warm PageCache
- `POST /api/cms/cache/invalidate` — invalidate slug cache entry
- `GET  /api/cms/status?brandId=` — site status + deploy history
- `GET  /api/cms/analytics?brandId=&days=30` — visit analytics
- `GET  /api/cms/domains?brandId=` — list custom domains
- `POST /api/cms/domains` — map custom domain
- `POST /api/cms/domains/{id}/verify` — trigger DNS verification

### Blazor Pages (all use CmsLayout — sky blue #0ea5e9 theme)
- `/cms/sites` — deploy dashboard with stat cards (published/cached/last deploy), Deploy Site button, deployment history table
- `/cms/domains` — domain list with hostname, primary badge, SSL verified status, Verify DNS button, Add Domain modal
- `/cms/analytics` — visit analytics with stat cards, daily bar chart, top pages table with visit share bar

### Layout
- `CmsLayout.razor` — focus menu: Sites, Domains, Analytics — using `hub-focus-menu`/`hub-focus-item` CSS classes
- Hub color: `#0ea5e9` (sky blue — distinct from all 5 existing hubs)
- `ShellLayout.razor` — CMS hub pill added to hub-switcher

### Key Technical Notes
- Public renderer uses `IgnoreQueryFilters()` — Host-header brand resolution bypasses tenant filter (brand is the security boundary, not authenticated tenant)
- IP addresses hashed with SHA-256 before storage (GDPR/privacy)
- ETag format: first 16 hex chars of MD5 of rendered HTML content
- Cache-Control: `public, max-age=300` on rendered pages (5-min browser cache)
- Hostname validation: `Uri.CheckHostName()` must not return `Unknown`
- Hostname uniqueness: global unique DB index + application-level check across ALL tenants (IgnoreQueryFilters)
- RenderPage shared between DeploySiteCommand and GetPublicPageQuery — produces full HTML5 document

### Acceptance Criteria — ALL PASS ✅
- [x] `dotnet build Nucleus.sln` — 0 errors, 0 warnings
- [x] `dotnet test` — 5/5 pass
- [x] EF migration `CmsRendererHub` applies cleanly (4 tables: site_domains, site_deployments, page_caches, site_visits)
- [x] `GET /cms/{slug}` returns 200 HTML for a published WebsitePage (no auth required)
- [x] `GET /cms/{slug}` returns 404 for unpublished or unknown slug
- [x] PageCache row written after first render; second request served from cache
- [x] `POST /api/cms/deploy` creates SiteDeployment + warms PageCache for all published pages
- [x] `POST /api/cms/cache/invalidate` clears PageCache for specified slug
- [x] `SiteDomain.hostname` lookup resolves correct BrandId from Host header
- [x] `/cms/sites` Blazor page shows deploy history and status per brand
- [x] `/cms/domains` Blazor page lists and verifies custom domains
- [x] `src/cms/` deletion from SEO Hub confirmed in retirement-checklist.md

---

## Sprint 28 — Studio Hub (COMPLETE ✅)

**Build status:** `dotnet build Nucleus.sln` → 0 errors, 0 warnings
**Test status:** `dotnet test` → 5/5 pass

### Domain Entities (all inherit TenantEntity, all tenant-scoped)
- `WebsitePage` — slug, title, page_type, html_content, seo_title, meta_description, og_image, status (draft|published|archived), published_at, schema_json (jsonb)
- `DesignAsset` — name, asset_type (image|document|font|svg|generated|other), url, width, height, file_size, uploaded_at, prompt_used, mime_type
- `VideoAsset` — name, url, thumbnail_url, duration_seconds, platform (youtube|vimeo|heygen|cloudflare|local|other), uploaded_at, description

### Acceptance Criteria — ALL PASS ✅
- [x] `dotnet build Nucleus.sln` — 0 errors, 0 warnings
- [x] `dotnet test` — 5/5 pass
- [x] EF migration `StudioHub` created (3 tables: website_pages, design_assets, video_assets)
- [x] `POST /api/studio/pages` creates WebsitePage for tenant (returns 201 + id)
- [x] `PUT /api/studio/pages/{id}/publish` sets status=published + published_at
- [x] `GET /api/studio/assets` returns asset library scoped to tenant

---

## Sprint 27 — Authority Hub (COMPLETE ✅)

### Acceptance Criteria — ALL PASS ✅
- [x] `dotnet build Nucleus.sln` — 0 errors, 0 warnings
- [x] `dotnet test` — 5/5 pass
- [x] EF migration `AuthorityHub` applies cleanly (4 tables created with indexes)

---

## Sprint 26 — Distribution Hub (COMPLETE ✅)
## Sprint 25 — Search Hub (COMPLETE ✅)
## Sprint 24 — Content Hub (COMPLETE ✅)
## Sprint 23 — Service Hub Architecture (COMPLETE ✅)

---

## What's Built (Feature Inventory)

### Auth & Identity
- Email/password with lockout (5 fails)
- JWT access tokens (60min) + refresh tokens (30-day rotation)
- Forgot password + reset via email
- Email verification on register
- Change password (settings page)
- Super-admin role (seeded from SUPER_ADMIN_EMAIL env var)

### Multi-tenancy
- Tenant entity with Slug, Plan, Stripe IDs
- TenantEntity base class — all data scoped by TenantId
- ICurrentTenantService — resolves tenant from JWT
- Plan enforcement middleware

### Brands
- Brand entity with all integration credentials (GHL, WP, DataForSEO, Email)
- Brand onboarding wizard with provisioning steps
- WP and GHL connection verification
- Brand edit/delete

### Content Hub (Sprint 24)
- WP blog post management, keyword tracking, DataForSEO rank checking
- AI content generator, editorial calendar, content library

### Search Hub (Sprint 25)
- Rankings dashboard, rank history, alerts, topic clusters, content gaps, page performance

### Distribution Hub (Sprint 26)
- Social post scheduling, email campaigns, campaign stats, send log
- `DistributionController` at `/api/distribution`

### Authority Hub (Sprint 27)
- Backlink tracking, brand mentions, schema manager, outreach queue
- `AuthorityController` at `/api/authority`

### Studio Hub (Sprint 28)
- Page Manager (website_pages CMS), Design Studio (AI HTML builder)
- Image Generator (Flux stub), Asset Library (design_assets)
- Video Library entity ready (UI pending)
- `StudioController` at `/api/studio`

### CMS Renderer Hub (Sprint 29)
- Public page renderer (GET /cms/{slug}) — no auth, resolves brand from Host header
- Custom domain mapping + DNS verification
- Site deployment (cache warm) — snapshots all published pages
- PageCache with ETag support + cache invalidation API
- Site visit analytics (30-day window, top pages, daily chart)
- `CmsController` at `/cms/{slug}` (public) + `/api/cms/*` (auth)

### Infrastructure
- GitHub Actions CI (build + test on every PR)
- Sentry error monitoring
- Hangfire background jobs (DisableConcurrentExecution)
- EF Core 9 migrations (no EnsureCreated)
- Memory cache (5-min TTL analytics)
- Brotli compression on WASM assets
- Railway deploy (single service: API + Blazor WASM + Hangfire)

---

## Sprint 30+ Roadmap

### Sprint 30 — Studio Hub v2 + Plan Gates
- Video Library Blazor page (/studio/videos)
- GET /api/studio/pages/{id} — full page detail endpoint for editor pre-fill
- PUT /api/studio/pages/{id} — update page content endpoint
- Plan gates: TenantPlanService enforcement for all hubs
- Flux API real integration (replace picsum stub)

### Ongoing — Infrastructure
- Redis (when scaling to 2+ Railway instances)
- CDN for WASM assets (improve cold load time)
- Public API + API keys (Zapier/Make integrations) — P3
- GHL webhook receiver (real-time vs polling) — P3

---

## Worker System (Added 2026-07-09)

Sprint worker + maintenance pipeline live on master (commit 23db922).
- Staging URL confirmed: `https://nucleus-staging-0a33.up.railway.app` (Railway Staging env, already live)
- Production URL: `https://nucleus-production.up.railway.app`

---

## Environment Variables (Railway)

| Var | Status | Purpose |
|-----|--------|---------|
| NUCLEUS_DB_CONNECTION | Live | Supabase PostgreSQL |
| JWT_SECRET | Live | Token signing |
| STRIPE_SECRET_KEY | Live | Billing |
| STRIPE_WEBHOOK_SECRET | Live | Webhook validation |
| STRIPE_PRICE_ID | Live | Subscription price |
| SENTRY_DSN | Live | Error monitoring |
| SMTP_HOST/PORT/USER/PASS/FROM | Live | Transactional email |
| DATAFORSEO_LOGIN / PASSWORD | Live | Keyword ranks |
| SUPER_ADMIN_EMAIL | Live | Admin panel seed |
| REDIS_URL | Pending | Distributed cache (Sprint 30+) |
| GOOGLE_CLIENT_ID | Pending | Google Sign-In |
| RAILWAY_STAGING_DEPLOY_WEBHOOK | Pending | GitHub Actions → staging deploy trigger |

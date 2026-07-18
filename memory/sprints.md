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

**Current state: Sprint 28 complete. Build: 0 errors, 0 warnings. Tests: 5/5 pass.**

---

## Sprint 28 — Studio Hub (COMPLETE ✅)

**Build status:** `dotnet build Nucleus.sln` → 0 errors, 0 warnings
**Test status:** `dotnet test` → 5/5 pass

### Domain Entities (all inherit TenantEntity, all tenant-scoped)
- `WebsitePage` — slug, title, page_type, html_content, seo_title, meta_description, og_image, status (draft|published|archived), published_at, schema_json (jsonb)
- `DesignAsset` — name, asset_type (image|document|font|svg|generated|other), url, width, height, file_size, uploaded_at, prompt_used, mime_type
- `VideoAsset` — name, url, thumbnail_url, duration_seconds, platform (youtube|vimeo|heygen|cloudflare|local|other), uploaded_at, description

### EF Core
- Migration `StudioHub` created — creates `website_pages`, `design_assets`, `video_assets` tables
- All three tables have `(TenantId, BrandId)` composite indexes + domain-specific indexes (Status, Slug, AssetType, Platform)
- `website_pages` has unique index on `(BrandId, Slug)` — enforces slug uniqueness per brand
- `website_pages.SchemaJson` stored as jsonb
- All registered in `INucleusDbContext` interface + `NucleusDbContext` implementation

### MediatR Commands (Nucleus.Application/StudioHub/Commands/)
- `CreateWebsitePageCommand` — validates slug regex (lowercase alphanumeric + hyphens/slashes); enforces slug uniqueness per brand; returns new page Id
- `PublishWebsitePageCommand` — sets status="published" + stamps PublishedAt (or reverts to "draft"); returns updated WebsitePageDto
- `GenerateDesignCommand` — generates HTML page scaffold from brand context + prompt; auto-slug; saves as draft; returns WebsitePageDto
- `UploadAssetCommand` — registers asset metadata + URL in design_assets table; returns DesignAssetDto
- `GenerateImageCommand` — generates AI image (Flux stub → picsum placeholder); saves as generated asset; returns DesignAssetDto

### MediatR Queries (Nucleus.Application/StudioHub/Queries/)
- `GetPageLibraryQuery` — brand stats (total/published/draft) + paginated pages ordered by UpdatedAt desc; filterable by status + page_type
- `GetAssetLibraryQuery` — brand stats (total/images/generated) + paginated assets ordered by UploadedAt desc; filterable by asset_type
- `GetDesignStudioContextQuery` — brand identity (color, domain) + recent pages (10) + recent assets (10) + studio stats aggregate

### DTOs (Nucleus.Application/StudioHub/DTOs/)
- `WebsitePageDto` + `PageLibraryDto` (summary stats + paginated rows)
- `DesignAssetDto` + `AssetLibraryDto` (summary stats + paginated rows)
- `VideoAssetDto`
- `DesignStudioContextDto` (PageSummary, AssetSummary, StudioStats)

### API Controller
- `StudioController` at `/api/studio` — thin MediatR dispatcher
- `GET  /api/studio/pages` — page library with stats + paginated rows (status, pageType, page, pageSize filters)
- `POST /api/studio/pages` — create CMS page draft (returns 201 + id)
- `PUT  /api/studio/pages/{id}/publish` — set status=published + stamp PublishedAt
- `PUT  /api/studio/pages/{id}/unpublish` — revert to draft
- `GET  /api/studio/design/context` — design studio context (brand + recent pages + assets + stats)
- `POST /api/studio/design/generate` — AI-generate HTML page scaffold
- `POST /api/studio/images/generate` — Flux image generation (stub)
- `GET  /api/studio/assets` — asset library with stats + paginated grid (assetType, page, pageSize filters)
- `POST /api/studio/assets` — register uploaded asset URL in library

### Blazor Pages (all use StudioLayout — pink #ec4899 theme)
- `/studio` — hub overview with 6 feature cards (already existed from Sprint 23)
- `/studio/pages` — page inventory with stat cards, status filter tabs, table with publish/unpublish actions, create modal, pagination
- `/studio/pages/editor` — HTML editor with two-column layout (code editor + SEO metadata panel); publish/unpublish buttons
- `/studio/design` — AI generator with brand context stats, prompt form, page-type selector, generated page result card, recent pages/assets grid
- `/studio/images` — image generator with prompt + size selector + style hint, results gallery, previously generated grid
- `/studio/assets` — asset library with type filter tabs, visual grid view, upload modal (URL-based), copy URL + open actions

### Layout
- `StudioLayout.razor` — full focus menu: Overview, Page Manager, Design Studio, Image Generator, Asset Library — using `hub-focus-menu`/`hub-focus-item` CSS classes

### Plan Gates (spec, enforcement via TenantPlanService in Sprint 29)
- Starter: page_library_view (view only), 5 published pages max
- Pro: unlimited_pages, design_studio, asset_library
- Agency: image_generator, video_library, bulk_publish

### Key Technical Notes
- Slug validation: regex `^[a-z0-9]+(?:[-/][a-z0-9]+)*$` (allows paths like `services/registration`)
- Slug uniqueness: unique DB index `(BrandId, Slug)` + application-level check with friendly error
- Loop variable naming: use `pg` (not `page`) in Blazor foreach — avoids RZ2005 conflict with `@page` directive
- HTML placeholder with `@` symbols: use HTML entity `&#64;` to prevent Blazor C# interpolation
- GenerateDesignCommand: uses StringBuilder (not raw string literal) to avoid CS9006 brace conflicts

### Acceptance Criteria — ALL PASS ✅
- [x] `dotnet build Nucleus.sln` — 0 errors, 0 warnings
- [x] `dotnet test` — 5/5 pass
- [x] EF migration `StudioHub` created (3 tables: website_pages, design_assets, video_assets)
- [x] `POST /api/studio/pages` creates WebsitePage for tenant (returns 201 + id)
- [x] `PUT /api/studio/pages/{id}/publish` sets status=published + published_at
- [x] `GET /api/studio/assets` returns asset library scoped to tenant
- [x] `/studio/pages` Blazor page loads page inventory (pink theme, stat cards, status filter, table)
- [x] `/studio/pages/editor` Blazor page opens page editor (HTML textarea + SEO metadata panel)
- [x] `retirement-checklist.md` created — 38/40 rows complete; remaining: Video Library UI + Redis

---

## Sprint 27 — Authority Hub (COMPLETE ✅)

**Build status:** `dotnet build Nucleus.sln` → 0 errors, 0 warnings
**Test status:** `dotnet test` → 5/5 pass

### Domain Entities (all inherit TenantEntity, all tenant-scoped)
- `BacklinkRecord` — sourceUrl, targetUrl, anchorText, domainRating, firstSeenAt, lastSeenAt, isActive
- `BrandMention` — sourceUrl, mentionText, sentiment (positive/neutral/negative), discoveredAt, isReviewed
- `SchemaTemplate` — pageType, schemaType, templateJson (jsonb), isActive
- `OutreachQueueItem` — targetUrl, contactEmail, status (pending/emailed/replied/accepted/rejected/skipped), notes, outreachAt

### Acceptance Criteria — ALL PASS ✅
- [x] `dotnet build Nucleus.sln` — 0 errors, 0 warnings
- [x] `dotnet test` — 5/5 pass
- [x] EF migration `AuthorityHub` applies cleanly (4 tables created with indexes)
- [x] `GET /api/authority/backlinks` returns backlink profile for tenant brand domain
- [x] Schema template auto-generates correct JSON-LD for FAQPage type with `@context`/`@type`
- [x] `/authority/backlinks` Blazor page loads (purple theme, stat cards, paginated table)
- [x] `/authority/schema` Blazor page shows template library with page_type filter tabs

---

## Sprint 26 — Distribution Hub (COMPLETE)

### Domain Entities
- `SocialPost`, `EmailCampaignMessage`, `SendLog`

### Acceptance Criteria — ALL PASS ✅

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
- WP blog post management (create, edit, publish)
- Keyword tracking per brand
- DataForSEO rank checking (on-demand + nightly)
- AI content generator, editorial calendar, content library

### Search Hub (Sprint 25)
- Rankings dashboard with Top3/Top10/Top30 stats
- Rank history, search alerts, topic clusters, content gaps, page performance

### Distribution Hub (Sprint 26)
- Social post scheduling, email campaigns, campaign stats, send log
- `DistributionController` at `/api/distribution`

### Authority Hub (Sprint 27)
- Backlink tracking, brand mentions, schema manager, outreach queue
- `AuthorityController` at `/api/authority`

### Studio Hub (Sprint 28)
- Page Manager (website_pages CMS), Design Studio (AI HTML builder)
- Image Generator (Flux stub), Asset Library (design_assets)
- Video Library entity ready (UI in Sprint 29)
- `StudioController` at `/api/studio`

### Infrastructure
- GitHub Actions CI (build + test on every PR)
- Sentry error monitoring
- Hangfire background jobs (DisableConcurrentExecution)
- EF Core 9 migrations (no EnsureCreated)
- Memory cache (5-min TTL analytics)
- Brotli compression on WASM assets
- Railway deploy (single service: API + Blazor WASM + Hangfire)

---

## Sprint 29+ Roadmap

### Sprint 29 — Studio Hub v2 + Plan Gates
- Video Library Blazor page (/studio/videos)
- GET /api/studio/pages/{id} — full page detail endpoint for editor pre-fill
- PUT /api/studio/pages/{id} — update page content endpoint
- Plan gates: TenantPlanService enforcement for all 5 hubs
- Flux API real integration (replace picsum stub)

### Ongoing — Infrastructure
- Redis (when scaling to 2+ Railway instances)
- CDN for WASM assets (improve cold load time)
- Public API + API keys (Zapier/Make integrations) — P3
- GHL webhook receiver (real-time vs polling) — P3
- Distribution Hub: GHL Social Planner live integration
- Distribution Hub: Reviews Manager (GHL reviews sync)
- Authority Hub: Hangfire job for DataForSEO backlink API sync (nightly)

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
| REDIS_URL | Pending | Distributed cache (Sprint 29+) |
| GOOGLE_CLIENT_ID | Pending | Google Sign-In |
| RAILWAY_STAGING_DEPLOY_WEBHOOK | Pending | GitHub Actions → staging deploy trigger |

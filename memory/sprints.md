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
| 30 | Finder Hub — quiz builder, embed widget, session tracking, conversion analytics |

**Current state: Sprint 30 complete. Build: 0 errors, 0 warnings. Tests: 5/5 pass.**

---

## Sprint 30 — Finder Hub (Quiz Builder) (COMPLETE ✅)

**Build status:** `dotnet build Nucleus.sln` → 0 errors, 0 warnings
**Test status:** `dotnet test` → 5/5 pass

### Domain Entities (all inherit TenantEntity, all tenant-scoped)
- `Finder` — name, brand_id, slug (unique per brand), intro_text, status (draft|published|archived), published_at, embed_token (globally unique)
- `FinderStep` — finder_id, step_order, step_type (single_choice|multi_choice|text|date|number), question_text, helper_text, is_required
- `FinderOption` — step_id, label, value, icon_url, description, sort_order
- `FinderResult` — finder_id, condition_json (jsonb), product_key, headline, body, cta_label, cta_url
- `FinderSession` — finder_id, session_token (globally unique), answers_json (jsonb), result_key, converted, completed_at
- `FinderAnalytics` — finder_id, date (DateOnly), starts, completions, conversions, drop_off_step_id (unique on FinderId+Date)

### EF Core
- Migration `FinderHub` created — creates 6 tables: finders, finder_steps, finder_options, finder_results, finder_sessions, finder_analytics
- `finders.embed_token` has global unique index — used for unauthenticated public API lookups
- `finders` has unique index `(BrandId, Slug)` — slug unique per brand
- `finder_sessions.session_token` has global unique index
- `finder_analytics` has unique index `(FinderId, Date)` — one row per finder per day
- All tables have `(TenantId, BrandId/FinderId)` composite indexes
- All registered in `INucleusDbContext` interface + `NucleusDbContext` implementation

### MediatR Commands (Nucleus.Application/FinderHub/Commands/)
- `CreateFinderCommand` — creates Finder for brand; enforces slug uniqueness per brand; generates embed_token; returns Finder Id
- `PublishFinderCommand` — validates 1+ steps + 1+ results exist; sets status=published + PublishedAt; returns bool
- `AddFinderStepCommand` — adds step to finder; auto-assigns StepOrder = max+1; returns FinderStepDto
- `AddFinderOptionCommand` — adds option to step; validates step belongs to tenant; auto-assigns SortOrder; returns FinderOptionDto
- `AddFinderResultCommand` — adds result to finder; stores ConditionJson as jsonb; returns FinderResultDto
- `RecordFinderSessionCommand` — creates/resumes anonymous session by EmbedToken; runs result matching on completion; increments daily analytics (starts/completions); uses IgnoreQueryFilters (no auth required)
- `RecordFinderConversionCommand` — marks session converted=true; increments daily analytics conversions; idempotent; uses IgnoreQueryFilters

### MediatR Queries (Nucleus.Application/FinderHub/Queries/)
- `GetFindersQuery` — lists finders for a brand, tenant-scoped; includes step/result count
- `GetFinderBuilderQuery` — returns full builder view (steps+options+results) for admin UI; tenant-scoped
- `GetPublicFinderQuery` — returns full public config by EmbedToken; IgnoreQueryFilters (no auth); only published finders
- `GetFinderAnalyticsQuery` — returns aggregate + daily stats for a finder; configurable day window (1-365); tenant-scoped
- `GetFinderSessionQuery` — resumes session by EmbedToken+SessionToken; IgnoreQueryFilters (no auth)

### FinderResultMatcher (Nucleus.Application/FinderHub/)
- Static helper: matches user answers (JSON keyed by StepOrder) against FinderResult conditions
- Supports exact match and array OR matching: `{"1": ["car", "truck"]}`
- Empty condition `{}` = catch-all (sorted last so specific rules win)
- Both server-side (RecordFinderSessionCommand) and client-side preview (Blazor) use same logic

### DTOs (Nucleus.Application/FinderHub/DTOs/)
- `FinderDto` — summary (id, name, slug, status, embed_token, step_count, result_count)
- `FinderBuilderDto` — full admin view with steps+options+results
- `FinderStepDto` — step with nested options list
- `FinderOptionDto` — label, value, icon_url, description, sort_order
- `FinderResultDto` — condition_json, product_key, headline, cta
- `FinderSessionDto` — session_token, answers_json, result_key, converted, completed_at
- `FinderAnalyticsDto` — totals + completion/conversion rates + daily breakdown
- `PublicFinderDto` — public-safe config (no internal IDs except finder Id) for embed widget

### API Controller (`Nucleus.Api/Controllers/FinderController.cs`)
#### Authenticated (admin/builder):
- `GET  /api/finder?brandId=` — list finders
- `POST /api/finder` — create finder (returns 201 + id)
- `GET  /api/finder/{id}/builder` — full builder view
- `POST /api/finder/{id}/publish` — publish finder
- `POST /api/finder/{id}/steps` — add a step
- `POST /api/finder/steps/{stepId}/options` — add option to step
- `POST /api/finder/{id}/results` — add result
- `GET  /api/finder/{id}/analytics?days=30` — analytics
#### Unauthenticated (embed widget):
- `GET  /api/finder/{embedToken}` — public finder config (steps+results)
- `GET  /api/finder/{embedToken}/session/{sessionToken}` — resume session
- `POST /api/finder/{embedToken}/session` — start/update session + completion
- `POST /api/finder/{embedToken}/convert` — record CTA conversion

### Blazor Pages (all use FinderLayout — violet #7c3aed theme)
- `/finder` — finder list with brand picker, status badges, create modal, publish button
- `/finder/builder?id={finderId}` — split-panel builder: steps (left) + results (right), add-step/option/result modals, embed token display, publish button
- `/finder/builder/steps` — redirects to /finder/builder
- `/finder/builder/results` — redirects to /finder/builder
- `/finder/analytics` — finder picker + stat cards (starts/completions/conversions/rates) + daily breakdown table with selectable day window
- `/finder/preview?token={embedToken}` — live widget preview (intro → steps with progress bar → result), client-side result matching, Copy Embed Code button

### Layout
- `FinderLayout.razor` — focus menu: Finders, Builder, Preview, Analytics — using `hub-focus-menu`/`hub-focus-item` CSS classes
- Hub color: `#7c3aed` (violet — distinct from all 6 existing hubs)
- `ShellLayout.razor` — Finder hub pill added to hub-switcher (7th pill)

### Key Technical Notes
- EmbedToken is the security boundary for unauthenticated operations (like BrandId for CMS renderer)
- All public endpoints use `IgnoreQueryFilters()` — global lookup, not tenant-scoped
- Session tracking is anonymous — no login required from end user
- Result matching: conditions keyed by StepOrder (string), supports exact + array OR
- FinderResultMatcher is a static pure function — used both in server-side commands and Blazor preview
- Analytics incremented inline in RecordFinderSession/Conversion (no background job needed at this scale)
- Preview page parses query string via `NavigationManager + HttpUtility.ParseQueryString` (not `[SupplyParameterFromQuery]` to avoid Blazor Razor parser issues)

### Acceptance Criteria — ALL PASS ✅
- [x] `dotnet build Nucleus.sln` — 0 errors, 0 warnings
- [x] `dotnet test` — 5/5 pass
- [x] EF migration `FinderHub` applies cleanly (6 tables created with indexes)
- [x] `POST /api/finder` creates Finder scoped to TenantId+BrandId
- [x] `GET /api/finder/{embedToken}` returns full config (unauthenticated)
- [x] `POST /api/finder/{embedToken}/session` creates FinderSession with answers_json
- [x] `POST /api/finder/{embedToken}/convert` marks session converted=true
- [x] FinderResult condition matching logic returns correct product_key for given answers
- [x] `/finder` Blazor page lists all finders for brand
- [x] `/finder/builder` Blazor page creates steps and options via modals
- [x] `/finder/preview` Blazor page shows live finder preview with step-by-step UX
- [x] Embed snippet (JS) generated and copyable from Preview page
- [x] Analytics: starts/completions/conversions tracked per finder per day

---

## Sprint 29 — CMS Renderer Hub (COMPLETE ✅)

**Build status:** `dotnet build Nucleus.sln` → 0 errors, 0 warnings
**Test status:** `dotnet test` → 5/5 pass

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

---

## Sprint 28 — Studio Hub (COMPLETE ✅)
## Sprint 27 — Authority Hub (COMPLETE ✅)
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

### Finder Hub (Sprint 30)
- Quiz/product-finder builder (multi-step with options)
- Result condition matching (JSON conditions, exact + OR)
- Anonymous session tracking + conversion recording
- Daily analytics (starts/completions/conversions)
- Embeddable widget via EmbedToken (no auth required)
- `FinderController` at `/api/finder`

### Infrastructure
- GitHub Actions CI (build + test on every PR)
- Sentry error monitoring
- Hangfire background jobs (DisableConcurrentExecution)
- EF Core 9 migrations (no EnsureCreated)
- Memory cache (5-min TTL analytics)
- Brotli compression on WASM assets
- Railway deploy (single service: API + Blazor WASM + Hangfire)

---

## Sprint 31+ Roadmap

### Sprint 31 — Studio Hub v2 + Plan Gates
- Video Library Blazor page (/studio/videos)
- GET /api/studio/pages/{id} — full page detail endpoint for editor pre-fill
- PUT /api/studio/pages/{id} — update page content endpoint
- Plan gates: TenantPlanService enforcement for all hubs
- Flux API real integration (replace picsum stub)

### Sprint 32+ — Finder Hub v2
- GHL lead capture via Hangfire job on conversion
- A/B testing (agency plan)
- Analytics export (CSV)
- White-label embed (agency plan)
- Custom result conditions UI

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
| REDIS_URL | Pending | Distributed cache (Sprint 31+) |
| GOOGLE_CLIENT_ID | Pending | Google Sign-In |
| RAILWAY_STAGING_DEPLOY_WEBHOOK | Pending | GitHub Actions → staging deploy trigger |

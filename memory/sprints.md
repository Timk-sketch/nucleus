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
| 24 | Content Hub — keyword library, AI generator, editorial calendar, content library, review queue, brand voice, templates |
| 25 | Search Hub — rankings dashboard, rank history, alerts, topic clusters, content gaps, page performance |
| 26 | Distribution Hub — social scheduler, email blasts, campaign workspace, send log |
| 27 | Authority Hub — backlinks, brand mentions, schema manager, outreach queue |
| 28 | Studio Hub — page manager, design studio, image generator, asset library |
| 29 | CMS Renderer Hub — public page renderer, custom domains, site deploy, cache invalidation, analytics |
| 30 | Finder Hub — quiz builder, embed widget, session tracking, conversion analytics |

**Current state: Sprint 24 complete (re-implemented). Build: 0 errors, 0 warnings. Tests: 5/5 pass.**

---

## Sprint 24 — Content Hub (COMPLETE ✅)

**Build status:** `dotnet build Nucleus.sln` → 0 errors, 0 warnings
**Test status:** `dotnet test` → 5/5 pass

### Domain Entities (all inherit TenantEntity, all tenant-scoped)
- `ContentPage` — brand_id, keyword_id (nullable FK), title, page_type, status (draft|review|approved|published|rejected), html_content, seo_title, meta_description, ai_model, ai_prompt, word_count, scheduled_at, published_at, review_notes
- `ContentTemplate` — brand_id, name, page_type, body (with {{keyword}}/{{brand}} placeholders), is_global, is_active
- `AiUsage` — brand_id, feature, tokens_used, cost_usd, model, content_page_id (nullable), for cost metering + plan enforcement
- `BannedWord` — brand_id, word, reason — Brand Voice config; injected into AI prompts

### EF Core
- Migration `ContentHub` created — creates 4 tables: `content_pages`, `content_templates`, `ai_usages`, `banned_words`
- `content_pages` indexes: TenantId, (TenantId,BrandId), (BrandId,Status), (BrandId,ScheduledAt), (BrandId,KeywordId)
- `content_templates` indexes: TenantId, (TenantId,BrandId), (BrandId,PageType), (BrandId,IsActive)
- `ai_usages` indexes: TenantId, (TenantId,BrandId), (TenantId,Feature,CreatedAt)
- `banned_words` indexes: TenantId, (TenantId,BrandId), (BrandId,Word)
- CostUsd uses decimal(10,6) for precision
- All registered in `INucleusDbContext` interface + `NucleusDbContext` implementation

### MediatR Commands (Nucleus.Application/ContentHub/Commands/)
- `GenerateContentCommand` — AI content generation (simulated Claude call); plan gate: starter=5/month (returns 402); records AiUsage after every generation; checks banned words; returns ContentPageDto
- `CreateContentPageCommand` — manual content page creation; validates brand+keyword ownership; returns Guid
- `ApproveContentPageCommand` — approve or reject a page in review queue; moves draft→review→approved or back to draft with notes; returns bool
- `AddBannedWordCommand` — adds banned word to Brand Voice; normalises to lowercase; prevents duplicates; returns BannedWordDto
- `CreateContentTemplateCommand` — creates template with placeholder support; IsGlobal shares across all brands in tenant; returns ContentTemplateDto

### MediatR Queries (Nucleus.Application/ContentHub/Queries/)
- `GetKeywordLibraryQuery` — lists keywords for brand with latest rank position + content page count per keyword; supports search + pagination
- `GetContentLibraryQuery` — lists ContentPages with filters (status, pageType, keywordId, search); returns paginated ContentLibraryResult
- `GetEditorialCalendarQuery` — returns ContentPages with ScheduledAt or PublishedAt within 8-week window; ordered chronologically
- `GetContentApprovalQueueQuery` — returns pages in "review" status + recently reviewed pages (last 30 days)
- `GetBrandVoiceQuery` — returns full banned words list for a brand
- `GetContentTemplatesQuery` — returns brand-specific + global templates; filterable by page type

### DTOs (Nucleus.Application/ContentHub/DTOs/)
- `ContentPageDto` — full page fields including keyword text, word count, AI model
- `ContentTemplateDto` — template with is_global flag
- `AiUsageDto` — cost tracking data
- `BannedWordDto` — word + reason + created date
- `KeywordLibraryDto` / `KeywordItemDto` — keyword with rank + content count enrichment
- `EditorialCalendarDto` / `CalendarEntryDto` — calendar window + entries
- `BrandVoiceDto` — brand name + banned words list + total count

### API Controller (`Nucleus.Api/Controllers/ContentHubController.cs`)
- `GET  /api/content/keywords?brandId=&search=&page=&pageSize=` — keyword library
- `POST /api/content/generate` — AI generate (returns 201 or 402 on plan limit)
- `GET  /api/content/library?brandId=&status=&pageType=&search=&page=` — content library
- `POST /api/content/pages` — manual content page create
- `GET  /api/content/calendar?brandId=&windowStart=&windowEnd=` — editorial calendar
- `GET  /api/content/queue?brandId=` — approval queue
- `PUT  /api/content/pages/{id}/approve` — approve/reject content page
- `GET  /api/content/brand-voice?brandId=` — brand voice (banned words)
- `POST /api/content/brand-voice/banned-words` — add banned word
- `GET  /api/content/templates?brandId=&pageType=&activeOnly=` — templates list
- `POST /api/content/templates` — create template
- **Note:** Existing `ContentController.cs` (at `/api/v1/brands/{brandId}/posts|keywords`) kept for backward compatibility with legacy `Content.razor` → now deleted, so this is now solely the new CQRS hub controller

### Blazor Pages (all use ContentLayout — blue #3b82f6 theme)
- `/content` — hub overview with feature cards (Content/Index.razor)
- `/content/keywords` — keyword library table with rank positions + content count badges; Generate button per keyword
- `/content/generator` — two-panel: settings form (left) + generated content preview (right); plan limit warning with upgrade link; Submit for Review action
- `/content/calendar` — 8-week editorial calendar; week-by-week view; status color bands; empty week indicators
- `/content/library` — paginated content table; filter by status/type/search; Submit for Review inline action
- `/content/queue` — review queue with approve/reject buttons + reviewer notes textarea; recently reviewed section
- `/content/brand-voice` — two-panel: add banned word form (left) + current list (right); explains how Brand Voice works
- `/content/templates` — two-panel: create template form + templates list; "Use template" link to generator

### ContentLayout.razor Updated
- Added full focus menu with 8 nav items: Overview, Keywords, AI Generator, Calendar, Library, Review Queue, Brand Voice, Templates
- Uses `hub-focus-menu`/`hub-focus-item` CSS classes (established pattern)

### Key Technical Notes
- Loop variable named `cp` (not `page`) to avoid Razor parser confusion with `@page` directive
- Navigation with interpolated URLs done via helper methods (not inline `$"..."` in attributes) to avoid Razor parser issues
- AI generation is simulated (stub) — real Claude API call would replace `SimulateContentGeneration()`; cost estimate uses Claude 3.5 Sonnet pricing
- Plan gate: starter = 5 generations/month counted from `AiUsage` table (Feature = "content_generation", monthly window)
- `Content.razor` (old `/content` page with WP posts) deleted — replaced by `Content/Index.razor` hub overview
- Old `ContentController.cs` (WP posts + keywords at `/api/v1/brands/{id}/posts|keywords`) retained for any existing integrations

### Acceptance Criteria — ALL PASS ✅
- [x] `dotnet build Nucleus.sln` — 0 errors, 0 warnings
- [x] `dotnet test` — 5/5 pass
- [x] EF migration `ContentHub` applies cleanly (4 tables: content_pages, content_templates, ai_usages, banned_words)
- [x] `GET /api/content/keywords` returns 200 for authenticated tenant
- [x] `POST /api/content/generate` creates ContentPage with correct TenantId
- [x] AiUsage row written after every generation call
- [x] Starter plan blocked at 5 generations per month (returns 402)
- [x] `/content/keywords` Blazor page loads and displays keyword list
- [x] `/content/generator` Blazor page submits and shows generated content

---

## Sprint 30 — Finder Hub (Quiz Builder) (COMPLETE ✅)

**Build status:** `dotnet build Nucleus.sln` → 0 errors, 0 warnings
**Test status:** `dotnet test` → 5/5 pass

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
## Sprint 28 — Studio Hub (COMPLETE ✅)
## Sprint 27 — Authority Hub (COMPLETE ✅)
## Sprint 26 — Distribution Hub (COMPLETE ✅)
## Sprint 25 — Search Hub (COMPLETE ✅)
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

### Content Hub (Sprint 24) ✅ FULLY REBUILT
- **New CQRS architecture**: ContentHubController.cs + 5 Commands + 6 Queries + 7 DTOs
- **4 Domain Entities**: ContentPage, ContentTemplate, AiUsage, BannedWord
- **EF Migration**: ContentHub (4 new tables)
- **7 Blazor Pages**: Keywords, Generator, Calendar, Library, Queue, BrandVoice, Templates
- **Updated ContentLayout**: full focus menu with 8 nav items
- AI generator with plan-gated usage tracking (starter = 5/month)
- Editorial calendar (8-week window, weekly layout)
- Review/approval workflow (draft → review → approved/rejected)
- Brand Voice (banned words list injected into AI prompts)
- Content templates with placeholder support

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
- Content Hub: wire real Claude API into GenerateContentCommand

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
| ANTHROPIC_API_KEY | Needed | Claude API for real AI generation (Sprint 31+) |

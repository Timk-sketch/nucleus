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
| 31 | Studio Hub v2 — real Claude API, fal.ai images, plan gates, video library |
| 32 | Finder Hub v2 — GHL lead capture, A/B testing, analytics CSV, visual conditions, white-label |
| 33 | Reports Hub — cross-hub analytics: content, search, finders, distribution |
| 34 | Leads Hub — paginated lead browser, per-finder filter, answers expand, CSV export |
| 35 | Contacts Hub — GHL contacts as proper hub, contact detail with related finder leads |
| 36 | Notification Center — bell + badge in topbar, /notifications unified feed (search alerts + brand mentions) |
| 37 | Keyword Manager — add keyword, delete keyword, bulk import (paste list), trigger rank check; all wired to existing ContentController endpoints |

**Current state: Sprint 37 complete. Build: 0 errors, 0 warnings.**

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

### Finder Hub (Sprint 30 + Sprint 32)
- Quiz/product-finder builder (multi-step with options)
- Result condition matching (JSON conditions, exact + OR)
- Anonymous session tracking + conversion recording
- Daily analytics (starts/completions/conversions) + CSV export
- Embeddable widget via EmbedToken (no auth required)
- `lead_capture` StepType — contact form collects LeadName/LeadEmail/LeadPhone
- GHL contact creation on conversion (Hangfire job)
- A/B variant testing (agency plan) — weighted random session assignment + breakdown in analytics
- Visual condition editor for result matching (Builder/Results.razor)
- White-label embed — CustomCss/LogoUrl/PrimaryColorOverride (agency plan)
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

### Sprint 31 — Studio Hub v2 + Plan Gates ✅ COMPLETE (2026-07-28)
- ✅ IClaudeService + ClaudeService (real Anthropic API, claude-sonnet-4-6)
- ✅ IImageGenerationService + FalAiService (real fal.ai Flux schnell)
- ✅ ITenantPlanService + TenantPlanService (starter: 5/mo content, 3/mo design, 0 image; pro/agency: unlimited)
- ✅ GenerateContentCommand, GenerateDesignCommand, GenerateImageCommand wired to real AI
- ✅ GetWebsitePageQuery + GET /api/studio/pages/{id}
- ✅ UpdateWebsitePageCommand + PUT /api/studio/pages/{id}
- ✅ GetVideoLibraryQuery + AddVideoAssetCommand + GET/POST /api/studio/videos
- ✅ Videos/Index.razor — Blazor video library page at /studio/videos
- ✅ Editor.razor wired to real GET/PUT endpoints
- ✅ StudioLayout.razor — Video Library nav item added
- NOTE: FAL_KEY still needs adding to Railway for image generation
- Commit: 1cb932e

### Sprint 37 — Keyword Manager ✅ COMPLETE (2026-07-28)
- ✅ Pages/Content/Keywords/Index.razor — add keyword form (inline, Enter-to-submit), delete per row (✕ button), bulk import textarea (paste comma/newline list), Check Rankings button
- ✅ Add: POST /api/v1/brands/{brandId}/keywords — keyboard shortcut (Enter) + button
- ✅ Delete: DELETE /api/v1/brands/{brandId}/keywords/{keywordId} — per-row ✕ with hover red style
- ✅ Bulk import: splits on newline + comma, deduplicates, calls add endpoint for each; shows count on completion
- ✅ Rank check: POST /api/v1/brands/{brandId}/keywords/ranks/check — enqueues Hangfire job
- ✅ Toast notifications (4s auto-clear) for all operations
- ✅ All endpoints already existed in ContentController — pure UI change
- ✅ Build: 0 errors, 0 warnings

### Sprint 36 — Notification Center ✅ COMPLETE (2026-07-28)
- ✅ NotificationsController: GET /api/notifications (merged feed), GET /count, DELETE /alerts/{id}, PUT /mentions/{id}/reviewed
- ✅ Pages/Notifications/Index.razor — brand picker, unified list (alert vs mention), dismiss/mark-read per item, clear-all
- ✅ ShellLayout.razor — bell icon in topbar-right, loads unread count after brand selected, red badge if > 0
- ✅ EF range expression bug fixed: MentionText[..140] moved out of LINQ expression tree to in-memory projection
- ✅ No migrations — queries existing search_alerts + brand_mentions tables
- ✅ Build: 0 errors, 0 warnings

### Sprint 35 — Contacts Hub ✅ COMPLETE (2026-07-28)
- ✅ ContactsLayout.razor — Hub="contacts" HubColor="#f97316" (orange), single nav item
- ✅ ShellLayout.razor — Contacts hub-pill added after Leads
- ✅ Pages/Contacts/Index.razor — brand pills, search, sync, tags column, click-to-detail
- ✅ Pages/Contacts/Detail.razor — /contacts/{brandId}/{id}, contact card + tags + finder leads table
- ✅ ContactsController: GET /{contactId} (single contact with parsed tags) + GET /{contactId}/leads (finder sessions by email, EF join)
- ✅ Pages/Contacts.razor renamed to ContactsLegacy.razor (eliminates Blazor namespace/type conflict)
- ✅ No migrations — queries existing ghl_contacts + finder_sessions tables
- ✅ Build: 0 errors, 0 warnings

### Sprint 34 — Leads Hub ✅ COMPLETE (2026-07-28)
- ✅ GetBrandLeadsQuery — paginated, filterable by finder + days (FinderSessions with LeadEmail)
- ✅ ExportLeadsCsvQuery — CSV with Finder, Name, Email, Phone, Converted, CapturedAt
- ✅ LeadsController: GET /api/leads + GET /api/leads/export
- ✅ LeadsLayout.razor — Hub="leads" HubColor="#ec4899" (pink)
- ✅ ShellLayout.razor — Leads hub-pill added after Reports
- ✅ Pages/Leads/Index.razor — paginated table, finder filter pills, click-to-expand answers, Export CSV
- ✅ No migrations — queries existing finder_sessions table
- ✅ Build: 0 errors, 0 warnings

### Sprint 33 — Reports Hub ✅ COMPLETE (2026-07-28)
- ✅ 5 DTOs: BrandOverviewDto, ContentReportDto, SearchReportDto, FinderReportDto, DistributionReportDto
- ✅ 5 MediatR queries: GetBrandOverviewQuery, GetContentReportQuery, GetSearchReportQuery, GetFinderReportQuery, GetDistributionReportQuery
- ✅ ReportsController (5 endpoints: /api/reports/{overview|content|search|finders|distribution})
- ✅ ReportsLayout.razor — Hub="reports" HubColor="#06b6d4" (cyan), 6 nav items
- ✅ ShellLayout.razor — Reports hub-pill button added after Finder
- ✅ 6 Blazor pages: Index, Overview, Content, Search, Finders, Distribution
- ✅ No new migrations — pure aggregation over existing tables
- ✅ Build: 0 errors, 0 warnings

### Sprint 32 — Finder Hub v2 ✅ COMPLETE (2026-07-28)
- ✅ `lead_capture` StepType — contact form step in the widget (no migration needed for type column)
- ✅ FinderSession gains LeadName/LeadEmail/LeadPhone + VariantId (AddFinderV2 migration)
- ✅ Finder entity gains WhiteLabelEnabled/CustomCss/LogoUrl/PrimaryColorOverride (same migration)
- ✅ FinderVariant entity + finder_variants table (AddFinderV2 migration — 3 tables modified)
- ✅ A/B testing (agency plan gate): CreateFinderVariantCommand, GetFinderVariantsQuery, weighted random assignment in RecordFinderSessionCommand, variant breakdown in GetFinderAnalyticsQuery
- ✅ GHL lead capture: GhlLeadCaptureJob (Hangfire) enqueued by FinderController after RecordFinderConversionCommand returns session.Id (Guid?)
- ✅ Analytics CSV export: ExportFinderAnalyticsCsvQuery + GET /api/finder/{id}/analytics/export + Export CSV button in Analytics/Index.razor
- ✅ UpdateFinderResultCommand + PUT /api/finder/results/{id}
- ✅ Builder/Results.razor rebuilt as visual condition editor (step/option dropdowns → ConditionJson)
- ✅ Builder/Index.razor: A/B Variants panel + Add Variant modal + lead_capture in step type dropdown
- ✅ Analytics/Index.razor: variant breakdown table + Export CSV button + JS downloadFileFromBase64 helper
- ✅ GetPublicFinderQuery + GetFinderBuilderQuery return white-label fields
- ✅ Build: 0 errors, 0 warnings
- Commit: d03eea1

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

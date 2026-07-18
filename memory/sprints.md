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
| 24 | Content Hub — keyword library, AI generator, editorial calendar, content library (assumed from roadmap) |
| 25 | Search Hub — rankings dashboard, rank history, alerts, topic clusters, content gaps, page performance |
| 26 | Distribution Hub — social scheduler, email blasts, campaign workspace, send log |

**Current state: Sprint 26 complete.**

---

## Sprint 26 — Distribution Hub (COMPLETE)

**Shipped:**

### Domain Entities (all inherit TenantEntity, all tenant-scoped)
- `SocialPost` — platform, caption, imageUrl, scheduledAt, publishedAt, status, externalPostId, provider
- `EmailCampaignMessage` — campaignId, subject, htmlBody, sentAt, openCount, clickCount, recipientCount, status
- `SendLog` — channel, recipientCount, sentAt, provider, status, errorMessage (immutable audit trail)

### EF Core
- Migration `DistributionHub` applied — creates `social_posts`, `email_campaign_messages`, `send_logs` tables
- All three tables have `(TenantId, BrandId)` composite indexes
- All registered in `INucleusDbContext` interface + `NucleusDbContext` implementation
- Global tenant query filter applied automatically via `TenantEntity` base class loop

### MediatR Commands (Application layer, all tenant-scoped)
- `ScheduleSocialPostCommand` — validates platform, caption, future scheduledAt; appends SendLog on success
- `CreateEmailCampaignCommand` — creates EmailCampaign + initial EmailCampaignMessage in "draft"
- `SendEmailCampaignCommand` — pluggable transport (SMTP via IEmailService); creates EmailCampaignMessage record + SendLog; handles partial failures

### MediatR Queries
- `GetSocialScheduleQuery` — date-window + optional status filter, ordered by scheduledAt
- `GetEmailCampaignsQuery` — with rolled-up open/click stats from EmailCampaignMessages
- `GetSendLogQuery` — paginated, channel-filterable, newest first
- `GetCampaignStatsQuery` — aggregate stats (openRate, clickRate) for a specific campaign

### API Controller
- `DistributionController` at `/api/distribution` — thin MediatR dispatcher
- `GET  /api/distribution/social` — social schedule with optional date/status filters
- `POST /api/distribution/social` — schedule a social post (returns 201 + post id)
- `GET  /api/distribution/email` — list email campaigns for a brand
- `GET  /api/distribution/email/{id}/stats` — campaign stats (opens, clicks, rates)
- `POST /api/distribution/email` — create a campaign draft
- `POST /api/distribution/email/send` — send a campaign to a recipient list
- `GET  /api/distribution/sendlog` — paginated send log with channel filter

### Blazor Pages (all use DistributionLayout — amber theme)
- `/distribution/social` — calendar-style grouped schedule view, new post modal, platform filter tabs
- `/distribution/email` — campaign list with open/click rate stats, create modal, send modal
- `/distribution/campaigns` — campaign workspace with summary stat cards, per-campaign stats drill-in
- `/distribution/sendlog` — paginated audit log, channel filter (email/social/sms), pagination

### Layout
- `DistributionLayout.razor` updated with full focus menu: Overview, Social Scheduler, Email Blasts, Campaign Workspace, Send Log — using `hub-focus-menu`/`hub-focus-item` CSS classes consistent with SearchLayout pattern

---

## Sprint 23 — Service Hub Architecture (COMPLETE 2026-05-27)

**Shipped:**
- `ShellLayout.razor` — shared shell component: sidebar with 5 hub-switcher icon pills, per-hub CSS custom property theming (`--hub-color`, `--hub-color-dim`), brand selector in topbar persisted via localStorage, hub badge in topbar
- `MainLayout.razor` rebuilt as 3-line thin wrapper delegating to ShellLayout
- 5 hub layout files: `ContentLayout.razor` (blue), `SearchLayout.razor` (green), `AuthorityLayout.razor` (purple), `DistributionLayout.razor` (amber), `StudioLayout.razor` (pink)
- Hub landing pages: `Pages/Search/Index.razor`, `Pages/Authority/Index.razor`, `Pages/Distribution/Index.razor`, `Pages/Studio/Index.razor` — each with 6 feature overview cards using hub-themed icons
- `Pages/Content.razor` wired to ContentLayout
- `nucleus.css` extended: `.hub-switcher`, `.hub-pill`, `.hub-pill--active`, `.main-column`, `.topbar`, `.topbar--hub`, `.topbar-hub-badge`, `.brand-selector`, `.hub-feature-grid`, `.hub-feature-card`, `.sidebar-divider`

**Also fixed (pre-existing, blocked deployment):**
- NU1605: bumped `Microsoft.Extensions.DependencyInjection.Abstractions` 9.0.15 → 9.0.16 in Api + Application
- CS1705: bumped `Microsoft.EntityFrameworkCore` + `Design` 9.0.15 → 9.0.16 in Infrastructure
- Hangfire crash: `ConvertPostgresUri` now uses `NpgsqlConnectionStringBuilder` (handles special chars in password); added `.Trim()` + `IsNullOrEmpty` guard on connection string env var
- `NUCLEUS_DB_CONNECTION` Railway env var was invalid — corrected with proper Supabase transaction pooler URI

---

## What's Built (Feature Inventory)

### Auth & Identity
- Google Sign-In (primary)
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

### Content Hub
- WP blog post management (create, edit, publish)
- Keyword tracking per brand
- DataForSEO rank checking (on-demand + nightly)
- Keyword rank history

### Search Hub
- Rankings dashboard with Top3/Top10/Top30 stats
- Rank history per keyword
- Search alerts (rank_drop, rank_rise, out_of_top_10, entered_top_3)
- Topic clusters (pillar keyword + cluster keywords)
- Content gaps (keywords without content)
- Page performance metrics

### Distribution Hub
- Social post scheduling (platform, caption, imageUrl, scheduledAt)
- Email campaigns (create, draft, send with SMTP)
- Campaign stats (open rate, click rate)
- Full send log (email + social audit trail, paginated)
- `DistributionController` at `/api/distribution`

### Contacts
- GHL contacts sync (Hangfire background job)
- Contact list view per brand

### Email Campaigns (legacy)
- Campaign entity (name, subject, status, sent date)
- Campaign list view
- Basic send infrastructure (EmailCampaignController)

### Team
- User invite flow
- Role assignment (TenantAdmin, Member)
- Team member list

### Billing
- Stripe checkout integration
- Stripe customer portal
- Webhook handler (checkout.session.completed, subscription events)
- Plan field on Tenant (starter/pro/agency)
- Billing page in UI

### Admin
- Super-admin panel: all tenants, user counts, plan
- Audit log: actor, action, entity, diff JSON, timestamp
- AuditLog entity with EF SaveChanges interceptor

### Infrastructure
- GitHub Actions CI (build + test on every PR)
- Sentry error monitoring
- Hangfire background jobs (DisableConcurrentExecution)
- EF Core 9 migrations (no EnsureCreated)
- Memory cache (5-min TTL analytics)
- Brotli compression on WASM assets
- Railway deploy (single service: API + Blazor WASM + Hangfire)

---

## Sprint 27+ Roadmap

### Sprint 27 — Authority Hub
- Backlink tracking
- Brand mentions
- Press releases
- Schema manager (auto-schema by page type)
- Outreach queue

### Sprint 28 — Studio Hub
- Page Manager (CMS — website_pages equivalent)
- Design Studio (AI-assisted HTML page builder)
- Image Generator (Flux integration)
- Asset Library
- Video library

### Ongoing — Infrastructure
- Redis (when scaling to 2+ Railway instances)
- CDN for WASM assets (improve cold load time)
- Public API + API keys (Zapier/Make integrations) — P3
- GHL webhook receiver (real-time vs polling) — P3
- Distribution Hub: GHL Social Planner live integration (currently queues to DB, GHL push via Hangfire job)
- Distribution Hub: Reviews Manager (GHL reviews sync)

---

## Worker System (Added 2026-07-09)

Sprint worker + maintenance pipeline live on master (commit 23db922).
- Staging URL confirmed: `https://nucleus-staging-0a33.up.railway.app` (Railway Staging env, already live)
- Production URL: `https://nucleus-production.up.railway.app`
- Sprint specs: `.sprints/sprint-24.yaml` → `sprint-28.yaml`
- Runner: `cd scripts/worker && npm install && node run-sprint.js 24`
- Auto-chains 24 → 25 → 26 → 27 → 28. SEO Hub retires when all 23 checklist rows = complete.

**Pending wiring before first sprint run:**
1. Railway → Staging env → Settings → Deploy Webhook → copy URL → add as GitHub Actions secret `RAILWAY_STAGING_DEPLOY_WEBHOOK`
2. Create `scripts/worker/.env` with: `ANTHROPIC_API_KEY`, `SLACK_NUCLEUS_WEBHOOK`, `NUCLEUS_STAGING_URL=https://nucleus-staging-0a33.up.railway.app`, `NUCLEUS_PROD_URL=https://nucleus-production.up.railway.app`
3. Add Railway cron service for maintenance workers per `.maintenance/schedule.yaml`

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
| GOOGLE_CLIENT_ID | Pending | Google Sign-In (Sprint 23) |
| RAILWAY_STAGING_DEPLOY_WEBHOOK | Pending | GitHub Actions → staging deploy trigger |

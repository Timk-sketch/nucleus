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

## Sprint History (All Complete as of 2026-05-26)

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

**Current state: Sprint 22 complete.**

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

### Content
- WP blog post management (create, edit, publish)
- Keyword tracking per brand
- DataForSEO rank checking (on-demand + nightly)
- Keyword rank history

### Contacts
- GHL contacts sync (Hangfire background job)
- Contact list view per brand

### Email Campaigns
- Campaign entity (name, subject, status, sent date)
- Campaign list view
- Basic send infrastructure

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

## Next: Sprint 23+ Roadmap

### Sprint 23 — Service Hub Architecture (FOUNDATION — do first)
Build the shell pattern before adding any hub features. This is the structural work.

1. Add service switcher to `MainLayout.razor` (5 hub pills in sidebar)
2. Create per-hub layout files with theme injection:
   - `ContentLayout.razor` (blue #3b82f6)
   - `SearchLayout.razor` (green #16a34a)
   - `AuthorityLayout.razor` (purple #8b5cf6)
   - `DistributionLayout.razor` (amber #f59e0b)
   - `StudioLayout.razor` (pink #ec4899)
3. Layout: global nav always visible + hub focus menu in sidebar + colored topbar strip
4. Brand selector in topbar — persists via localStorage across hubs
5. Hub switcher animation (smooth theme transition on hub switch)
6. Add `Pages/{Hub}/` folder structure to `Nucleus.Web`

**Deliverable:** Navigate between 5 hubs, theme changes, focus menu shows, brand selector persists. No actual hub features yet — just the working shell.

### Sprint 24 — Content Hub (first full hub)
SEO Hub has proven Content features. Port them into Nucleus with multi-tenancy.

Core features (proven in SEO Hub, ready to port):
- Keyword Library (CRUD, import, bulk operations)
- AI Content Generator (brand context, page type, keyword)
- Editorial Calendar (scheduled content)
- Content Approval Queue
- Content Library (all published + draft content)
- Brand Voice Rules (banned words)
- Content Templates

Multi-tenancy additions over SEO Hub:
- All keyword/content queries scoped by TenantId + BrandId
- AI generation costs tracked per tenant
- Content templates shared within tenant (not global)

Plan gates: Starter = AI generator limited to 5/month; Pro/Agency = unlimited

### Sprint 25 — Search Performance Hub
- Keyword rankings dashboard (DataForSEO — already integrated at entity level)
- Ranking history charts per keyword
- Alerts (rank drops, significant changes)
- Topic cluster map
- Content gaps (keywords without content)
- Page performance metrics (GSC integration)

### Sprint 26 — Distribution Hub
- Social media scheduler (GHL Social Planner integration)
- Email blasts (GHL or Drip depending on brand config)
- Campaign workspace
- Send log
- Reviews (GHL reviews sync)

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

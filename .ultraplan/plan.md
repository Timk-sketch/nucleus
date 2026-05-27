# Nucleus Marketing OS — Current State & Roadmap (Updated 2026-05-26)

## Context
Sprints 1-22 are complete. Nucleus has a working multi-tenant foundation (auth, brands, billing, GHL sync, keyword ranks, Stripe, CI/CD, super-admin, audit log). Next: build the 5-service-hub architecture, then port proven SEO Hub features into each hub one at a time.

## The Model
- **SEO Hub** = test/staging — features are built and proven here first
- **Nucleus** = live SaaS production — only fully working, multi-tenanted features ship here
- A feature moves SEO Hub → Nucleus when: proven working + multi-tenancy added + plan gates added

## What's Built (Sprint 22 Baseline)
- Multi-tenant: Tenant → Brand → TenantEntity hierarchy, ICurrentTenantService
- Auth: JWT + refresh tokens, lockout, password reset, verification, Google Sign-In ready
- Stripe billing: checkout, portal, webhooks, plan tiers (starter/pro/agency), plan enforcement
- Super-admin: all-tenants view, impersonate, plan change
- Audit log: EF interceptor captures CRUD on all key entities
- GHL contacts sync (Hangfire background job)
- DataForSEO keyword rank tracking (nightly + on-demand)
- Email campaigns (basic)
- WP blog management
- CI/CD: GitHub Actions build + test on every PR
- Performance: memory cache, Brotli compression, DisableConcurrentExecution on all jobs
- Sentry error monitoring

## Current Stack
C# .NET 9 / Blazor WASM / EF Core 9 / Supabase PostgreSQL / Hangfire / Stripe / Railway

## Sprint 23 — Service Hub Architecture (DO FIRST)
Build the shell before any hub features. Nothing else starts until this is done.

### Changes

#### `src/Nucleus.Web/Layout/MainLayout.razor`
- Add service switcher (5 hub pills) to sidebar, below brand selector
- Add hub-context CSS class to `<div class="app-shell">` based on active hub
- Hub pills: Content (blue), Search (green), Authority (purple), Distribution (amber), Studio (pink)
- Global nav items (Dashboard, Brands, Team, Settings, Billing) always visible below switcher

#### New hub layout files (`src/Nucleus.Web/Layout/`)
- `ContentLayout.razor` — `--hub-color: #3b82f6` + Content focus menu
- `SearchLayout.razor` — `--hub-color: #16a34a` + Search focus menu
- `AuthorityLayout.razor` — `--hub-color: #8b5cf6` + Authority focus menu
- `DistributionLayout.razor` — `--hub-color: #f59e0b` + Distribution focus menu
- `StudioLayout.razor` — `--hub-color: #ec4899` + Studio focus menu

Each layout injects `--hub-color` as a CSS variable on `:root` → topbar strip + sidebar accent + active nav states pick it up automatically.

#### `src/Nucleus.Web/wwwroot/` CSS
- Add `--hub-color` CSS variable consumed by `.topbar`, `.nav-item.active`, `.sidebar-logo`
- Add `.hub-switcher` component styles

#### Page folder structure (`src/Nucleus.Web/Pages/`)
- `Content/` — future Content Hub pages
- `Search/` — future Search Hub pages
- `Authority/` — future Authority Hub pages
- `Distribution/` — future Distribution Hub pages
- `Studio/` — future Studio Hub pages

#### Brand selector persistence
- Store active brand in `localStorage["nucleus_active_brand"]`
- Read on `MainLayout.razor` init — all hub layouts inherit it

### Verification
Navigate between 5 hub areas → sidebar accent + topbar color changes → focus menu updates → brand selector persists → no full page reload

---

## Sprint 24 — Content Hub (first complete hub)

### Scope (all must work before shipping)
1. Keyword Library — list, add, bulk import, generate from keyword, tag/filter
2. AI Generator — brand context, page type selector, keyword input, generate blog/page/FAQ
3. Editorial Calendar — scheduled content view, assign keyword, status tracking
4. Content Approval Queue — review, approve, request changes
5. Content Library — all content (published, draft, scheduled) with search + filter
6. Brand Voice Rules — banned words list per brand
7. Content Templates — global + brand-specific approved templates

### Multi-tenancy additions over SEO Hub version
- All queries: `WHERE tenant_id = @TenantId AND brand_id = @BrandId`
- AI usage tracked in `AiUsage` table — blocked + notified at plan limit
- Templates scoped per tenant (not global)

### New entities needed
- `ContentPage` (TenantEntity) — title, keyword, page_type, status, html_content, seo metadata
- `ContentTemplate` (TenantEntity) — name, page_type, body, is_global
- `AiUsage` (TenantEntity) — feature, tokens_used, cost, created_at
- `BannedWord` (TenantEntity) — word, reason

### EF Migration name
`dotnet ef migrations add ContentHub`

---

## Sprint 25+ (after Content Hub ships complete)

| Sprint | Hub | Key features |
|--------|-----|-------------|
| 25 | Search | Rankings, rank history, alerts, topic clusters, content gaps, page performance |
| 26 | Distribution | Social scheduler, email blasts, campaigns, send log, reviews |
| 27 | Authority | Backlinks, brand mentions, press releases, schema manager, outreach |
| 28 | Studio | Page manager (CMS), design studio, image gen, asset library |

---

## Implementation Sequence (Sprint 23)
1. Update `MainLayout.razor` — hub switcher + CSS variable injection
2. Create 5 layout files (ContentLayout, SearchLayout, AuthorityLayout, DistributionLayout, StudioLayout)
3. Add `--hub-color` CSS var to wwwroot styles
4. Create `Pages/{Hub}/` folder scaffolding
5. Add brand selector localStorage persistence
6. Test: navigate all 5 hubs, verify theme switches, brand selector persists

## Risks
- Blazor WASM CSS variable injection: set via JS interop or CSS class on body — test both
- Focus menu in layout: use `[CascadingParameter]` or NavigationManager.Uri to determine active section
- Brand selector state: `localStorage` bridge needs JS interop wrapper

## Verification
```
dotnet build Nucleus.sln   # clean build
dotnet test                # all tests pass
git push origin main       # Railway deploys
# Navigate: /content/keywords → blue theme
# Navigate: /search/rankings → green theme
# Switch brand in selector → persists when switching hubs
```

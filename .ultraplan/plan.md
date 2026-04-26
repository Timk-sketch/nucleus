# Implementation Plan: Nucleus Marketing OS — Remaining Sprints

## Context
Sprints 1–3 complete: infra, JWT auth, dashboard shell, brand onboarding + provisioning (simulated). All remaining sprints to ship a functional multi-tenant Marketing OS.

---

## Sprint 4: Live Dashboard + Brand CRUD (Priority 1)

### Dashboard — wire real data
- **File**: `src/Nucleus.Web/Pages/Dashboard.razor:87-98`
- **Change**: Replace hardcoded `_brandCount=0` with `GET api/v1/brands` count; compute ServicesActive from provisioning steps
- **Reuses**: existing `BrandsResponse` record from `Brands.razor:95`

### Brand edit page
- **File**: `src/Nucleus.Web/Pages/BrandEdit.razor` (new)
- **Change**: Mirror `NewBrand.razor` form with prefilled values; call `PUT api/v1/brands/{id}`
- **File**: `src/Nucleus.Api/Controllers/BrandsController.cs:91`
- **Change**: Add `[HttpPut("{id}")]` and `[HttpDelete("{id}")]` endpoints; reuse `INucleusDbContext`

### BrandDetail — link to edit
- **File**: `src/Nucleus.Web/Pages/BrandDetail.razor`
- **Change**: Add "Edit" button → `/brands/{id}/edit`

---

## Sprint 5: Auth Hardening (Priority 2)

### Auto token refresh on 401
- **File**: `src/Nucleus.Web/Services/AuthHeaderHandler.cs`
- **Change**: Override `SendAsync` — on 401 response call `AuthService.RefreshAsync()`, retry once with new token
- **Reuses**: `AuthService.RefreshAsync()` at `AuthService.cs:52`

### Token expiry check before request
- **File**: `src/Nucleus.Web/Services/AuthHeaderHandler.cs`
- **Change**: Parse JWT exp claim before sending; if within 60s of expiry, proactively refresh

### Password change
- **File**: `src/Nucleus.Api/Controllers/AuthController.cs`
- **Change**: Add `POST /api/v1/auth/change-password` using `UserManager.ChangePasswordAsync`
- **File**: `src/Nucleus.Web/Pages/Settings.razor` (new) — form: current password + new password fields

---

## Sprint 6: User & Team Management (Priority 3)

### API — users list + invite
- **File**: `src/Nucleus.Api/Controllers/UsersController.cs` (new)
- **Change**: `GET /api/v1/users` (tenant-scoped), `POST /api/v1/users/invite` (create user + send email)
- **Reuses**: `ICurrentTenantService` from `CurrentTenantService.cs`, `UserManager<ApplicationUser>`

### Blazor — team page
- **File**: `src/Nucleus.Web/Pages/Team.razor` (new)
- **Change**: List tenant users (name, email, role), invite button → modal with email + role selector
- **File**: `src/Nucleus.Web/Layout/MainLayout.razor`
- **Change**: Add "Team" nav link → `/team`

---

## Sprint 7: Real Service Provisioning (Priority 4)

### WordPress verification service
- **File**: `src/Nucleus.Infrastructure/Services/WordPressProvisioningService.cs` (new)
- **Change**: `VerifyCredentialsAsync(url, username, appPassword)` — call WP REST API `/wp-json/wp/v2/users/me` with Basic Auth
- **Pattern**: Portable service class, no HTTP context

### GHL verification service
- **File**: `src/Nucleus.Infrastructure/Services/GhlProvisioningService.cs` (new)
- **Change**: `VerifyCredentialsAsync(locationId, apiKey)` — call GHL `/locations/{locationId}` endpoint

### Wire into BrandProvisioningJob
- **File**: `src/Nucleus.Api/Jobs/BrandProvisioningJob.cs:49`
- **Change**: Replace `Task.Delay` simulation with real service calls per step name:
  - `"wordpress"` → `WordPressProvisioningService.VerifyCredentialsAsync`
  - `"ghl"` → `GhlProvisioningService.VerifyCredentialsAsync`
  - `"dataforseo"` → stub OK if no creds, else verify
  - `"email"` / `"backlinks"` → stub OK (future)

---

## Sprint 8: Settings Page (Priority 5)

### Tenant settings
- **File**: `src/Nucleus.Api/Controllers/TenantsController.cs` (new)
- **Change**: `GET /api/v1/tenants/me` (name, slug, plan), `PUT /api/v1/tenants/me` (name only)
- **File**: `src/Nucleus.Web/Pages/Settings.razor` (extend Sprint 5 file)
- **Change**: Add "Company" section (name, plan display), profile section (first/last name)

---

## Sprint 9: SEO & Content Tools (Priority 6)

### WordPress blog management
- **File**: `src/Nucleus.Api/Controllers/ContentController.cs` (new)
- **Change**: `GET /api/v1/brands/{id}/posts` (proxy to WP REST), `POST` create post — uses `WordPressProvisioningService`

### Keyword tracking dashboard
- **File**: `src/Nucleus.Web/Pages/BrandSeo.razor` (new)
- **Change**: `/brands/{id}/seo` — keyword rank table, DataForSEO integration (stub first, real API second)

---

## Implementation Sequence
1. `Dashboard.razor` — wire brand count (30 min)
2. `BrandsController` — add PUT/DELETE (45 min)
3. `BrandEdit.razor` — edit form (45 min)
4. `AuthHeaderHandler` — auto-refresh on 401 (1h)
5. `AuthController` — change-password endpoint (30 min)
6. `Settings.razor` — password change UI (30 min)
7. `UsersController` + `Team.razor` — team management (2h)
8. `WordPressProvisioningService` + `GhlProvisioningService` — real verification (2h)
9. Wire real provisioning into `BrandProvisioningJob` (1h)
10. `TenantsController` + settings company section (1h)
11. Content + SEO pages (ongoing)

## Edge Cases & Risks
- **WP REST blocked by firewall/plugin**: Catch HTTP errors in provisioning service, set step status=failed with descriptive error
- **GHL rate limits**: Add `Polly` retry with exponential backoff on 429
- **Token refresh race condition**: Use `SemaphoreSlim(1,1)` in `AuthHeaderHandler` to prevent concurrent refresh storms
- **Team invite email**: Use `IEmailSender` interface (stub first, wire SMTP/SendGrid in settings)

## Verification
```
dotnet build src/Nucleus.Api/Nucleus.Api.csproj && dotnet build src/Nucleus.Web/Nucleus.Web.csproj
git push origin main  # Railway auto-deploy
```

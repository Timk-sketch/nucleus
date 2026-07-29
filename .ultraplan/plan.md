# Implementation Plan: Sprint 32 — Finder Hub v2

## Context
GHL lead capture on conversion, A/B variant system (agency), CSV analytics export, white-label embed (agency), visual condition editor in Results builder.

## Migration: AddFinderV2
- **File**: `src/Nucleus.Infrastructure/Migrations/20260728_FinderV2.cs` (new)
- finder_sessions: + lead_name text, lead_email text, lead_phone text, variant_id uuid nullable
- finders: + white_label_enabled bool default false, custom_css text, logo_url text, primary_color_override text
- New table: finder_variants (id, tenant_id, finder_id, name, intro_text_override, weight int default 50, created_at, updated_at)

## Domain
- `src/Nucleus.Domain/Entities/FinderSession.cs` — + LeadName?, LeadEmail?, LeadPhone?, VariantId?, Variant nav
- `src/Nucleus.Domain/Entities/Finder.cs` — + WhiteLabelEnabled, CustomCss?, LogoUrl?, PrimaryColorOverride?, Variants nav
- `src/Nucleus.Domain/Entities/FinderVariant.cs` (new) — TenantEntity; FinderId, Name, IntroTextOverride?, Weight

## Application
- `src/Nucleus.Application/FinderHub/Commands/RecordFinderSessionCommand.cs`
  - + LeadName?, LeadEmail?, LeadPhone? params; weighted variant assignment on new session; store lead fields
- `src/Nucleus.Application/FinderHub/Commands/RecordFinderConversionCommand.cs`
  - After SaveChanges: enqueue GhlLeadCaptureJob via IBackgroundJobService (only if brand has GhlApiKey)
- `src/Nucleus.Application/FinderHub/Commands/CreateFinderVariantCommand.cs` (new) — plan gate: agency only
- `src/Nucleus.Application/FinderHub/Queries/GetFinderVariantsQuery.cs` (new)
- `src/Nucleus.Application/FinderHub/Queries/GetFinderAnalyticsQuery.cs` — + variant breakdown from sessions grouped by VariantId
- `src/Nucleus.Application/FinderHub/DTOs/FinderAnalyticsDto.cs` — + List<VariantBreakdownDto> Variants
- `src/Nucleus.Application/FinderHub/Queries/ExportFinderAnalyticsCsvQuery.cs` (new) — returns string CSV
- `src/Nucleus.Application/FinderHub/DTOs/PublicFinderDto.cs` — + WhiteLabelEnabled, CustomCss?, LogoUrl?, PrimaryColorOverride?, AssignedVariantId?
- `src/Nucleus.Application/FinderHub/Queries/GetPublicFinderQuery.cs` — extend to return white-label fields
- `src/Nucleus.Application/FinderHub/DTOs/FinderBuilderDto.cs` — + white-label fields + List<FinderVariantDto> Variants

## Infrastructure
- `src/Nucleus.Infrastructure/Data/NucleusDbContext.cs` — + DbSet<FinderVariant>; configure FinderVariant entity
- `src/Nucleus.Api/Jobs/GhlLeadCaptureJob.cs` (new) — Hangfire job: load session+brand, POST /v1/contacts to GHL; reuses "ghl" HttpClient

## API
- `src/Nucleus.Api/Controllers/FinderController.cs`
  - RecordSession request model: + LeadName?, LeadEmail?, LeadPhone?
  - + GET /api/finder/{id}/analytics/export?days= → CSV file response
  - + POST /api/finder/{id}/variants → CreateFinderVariantCommand
  - + GET /api/finder/{id}/variants → GetFinderVariantsQuery

## Blazor
- `src/Nucleus.Web/Pages/Finder/Builder/Index.razor` — + Variants panel (agency gate) with weight sliders + add variant modal
- `src/Nucleus.Web/Pages/Finder/Builder/Results.razor` — replace Nav.NavigateTo redirect with visual condition editor (step/option dropdowns → generates ConditionJson)
- `src/Nucleus.Web/Pages/Finder/Analytics/Index.razor` — + Export CSV button (calls export endpoint) + variant breakdown table

## Implementation Sequence
1. Migration → `dotnet ef migrations add FinderV2`
2. FinderVariant domain entity + NucleusDbContext registration
3. FinderSession + Finder domain: add new properties
4. GhlLeadCaptureJob
5. RecordFinderSessionCommand (variant assignment + lead fields)
6. RecordFinderConversionCommand (enqueue GHL job)
7. CreateFinderVariantCommand + GetFinderVariantsQuery
8. GetFinderAnalyticsQuery + ExportFinderAnalyticsCsvQuery + DTOs
9. GetPublicFinderQuery + PublicFinderDto + FinderBuilderDto
10. FinderController (4 changes/additions)
11. Builder/Index.razor + Builder/Results.razor + Analytics/Index.razor
12. `dotnet build && dotnet test`

## Edge Cases & Risks
- Variant weight: normalize weights so they always sum correctly (use proportional weighted random)
- GHL job fire: only enqueue if finder's brand has GhlApiKey set (skip silently otherwise)
- lead_capture StepType already a valid field value — no DB column needed, just new value string
- Migration timestamp: use 20260728000001 (after last migration 20260721005306)
- CSV export: use CsvHelper or manual StringBuilder; no new package — use StringBuilder

## Verification
```
dotnet ef migrations add FinderV2 --project src/Nucleus.Infrastructure --startup-project src/Nucleus.Api
dotnet build Nucleus.sln && dotnet test
git push origin master
# POST /api/finder/{embedToken}/session with leadEmail → session.LeadEmail persisted
# POST /api/finder/{embedToken}/convert → GHL job enqueued (check Hangfire dashboard)
# GET /api/finder/{id}/analytics/export → CSV download
# POST /api/finder/{id}/variants (agency JWT) → variant created; starter JWT → 403
```

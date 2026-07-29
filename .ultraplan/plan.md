# Implementation Plan: Sprint 31 — Wiring Phase

## Context
Replace stubs with real APIs (Claude + fal.ai Flux), add ITenantPlanService enforcement, build missing GET/PUT /api/studio/pages/{id}, Video Library page.

## Changes

### 1. IClaudeService — new interface
- **File**: `src/Nucleus.Application/Common/Interfaces/IClaudeService.cs` (new)
- **Change**: `Task<string> GenerateAsync(string systemPrompt, string userPrompt, string model, int maxTokens)`

### 2. ClaudeService — implementation
- **File**: `src/Nucleus.Infrastructure/Services/ClaudeService.cs` (new)
- **Change**: Named HttpClient "anthropic" → POST `https://api.anthropic.com/v1/messages`; headers: `x-api-key`, `anthropic-version: 2023-06-01`; parse `content[0].text`
- **Register**: `Program.cs` — `AddHttpClient("anthropic")` + `AddScoped<IClaudeService, ClaudeService>()`

### 3. IImageGenerationService — new interface
- **File**: `src/Nucleus.Application/Common/Interfaces/IImageGenerationService.cs` (new)
- **Change**: `Task<string> GenerateImageAsync(string prompt, int width, int height)` returns image URL

### 4. FalAiService — implementation
- **File**: `src/Nucleus.Infrastructure/Services/FalAiService.cs` (new)
- **Change**: POST `https://fal.run/fal-ai/flux/schnell`; `Authorization: Key {FAL_KEY}`; parse `images[0].url`; throw if FAL_KEY missing
- **Register**: `Program.cs` — `AddHttpClient("falai")` + `AddScoped<IImageGenerationService, FalAiService>()`

### 5. ITenantPlanService + TenantPlanService
- **File**: `src/Nucleus.Application/Common/Interfaces/ITenantPlanService.cs` (new)
- **File**: `src/Nucleus.Infrastructure/Multitenancy/TenantPlanService.cs` (new)
- **Change**: `IsFeatureAllowed(feature)` checks plan limits: starter={content_generation:5/mo, design_generation:3/mo, image_generation:0}; pro/agency=unlimited
- **Register**: `Program.cs` — `AddScoped<ITenantPlanService, TenantPlanService>()`

### 6. GenerateContentHandler — wire Claude + plan service
- **File**: `src/Nucleus.Application/ContentHub/Commands/GenerateContentCommand.cs:44`
- **Change**: Inject `IClaudeService` + `ITenantPlanService`; replace manual plan check with `_plan.IsFeatureAllowed("content_generation")`; replace `SimulateContentGeneration()` with real `_claude.GenerateAsync()`

### 7. GenerateDesignHandler — wire Claude + plan service
- **File**: `src/Nucleus.Application/StudioHub/Commands/GenerateDesignCommand.cs:54`
- **Change**: Inject `IClaudeService` + `ITenantPlanService`; replace `GenerateHtmlScaffold()` with Claude call; plan gate: design_generation pro+

### 8. GenerateImageHandler — wire fal.ai + plan service
- **File**: `src/Nucleus.Application/StudioHub/Commands/GenerateImageCommand.cs:46`
- **Change**: Inject `IImageGenerationService` + `ITenantPlanService`; replace picsum URL with `_imageGen.GenerateImageAsync()`; plan gate: agency only

### 9. GetWebsitePageQuery — new
- **File**: `src/Nucleus.Application/StudioHub/Queries/GetWebsitePageQuery.cs` (new)
- **Change**: Returns `WebsitePageDto?` by id scoped to TenantId; follows `GetPageLibraryQuery.cs` pattern

### 10. UpdateWebsitePageCommand — new
- **File**: `src/Nucleus.Application/StudioHub/Commands/UpdateWebsitePageCommand.cs` (new)
- **Change**: Updates title, pageType, htmlContent, seoTitle, metaDescription, ogImage, schemaJson; slug NOT updatable; verifies TenantId ownership

### 11. StudioController — GET/{id} + PUT/{id}
- **File**: `src/Nucleus.Api/Controllers/StudioController.cs:29`
- **Change**: Add `GET /api/studio/pages/{id:guid}` + `PUT /api/studio/pages/{id:guid}` endpoints

### 12. Editor.razor — wire load + save
- **File**: `src/Nucleus.Web/Pages/Studio/Pages/Editor.razor`
- **Change**: `LoadPageDetail()` calls `GET /api/studio/pages/{id}` and populates all fields; `Save()` on existing pages calls `PUT /api/studio/pages/{id}`

### 13. GetVideoLibraryQuery + AddVideoAssetCommand — new
- **File**: `src/Nucleus.Application/StudioHub/Queries/GetVideoLibraryQuery.cs` (new)
- **File**: `src/Nucleus.Application/StudioHub/Commands/AddVideoAssetCommand.cs` (new)
- **Reuses**: `VideoAsset` (DbContext:47), `VideoAssetDto` (StudioHub/DTOs/VideoAssetDto.cs)

### 14. StudioController — video endpoints
- **File**: `src/Nucleus.Api/Controllers/StudioController.cs`
- **Change**: Add `GET /api/studio/videos?brandId=&page=&pageSize=` + `POST /api/studio/videos`

### 15. Videos/Index.razor — new
- **File**: `src/Nucleus.Web/Pages/Studio/Videos/Index.razor` (new)
- **Change**: `@page "/studio/videos"` + `@layout StudioLayout`; list videos + add form; mirror Assets/Index.razor

### 16. StudioLayout.razor — Videos nav
- **File**: `src/Nucleus.Web/Layout/StudioLayout.razor`
- **Change**: Add Videos `<NavLink href="/studio/videos">` after Asset Library

## Railway env vars to add
- `ANTHROPIC_API_KEY`
- `FAL_KEY`

## Implementation Sequence
1. IClaudeService + ClaudeService + IImageGenerationService + FalAiService (parallel)
2. ITenantPlanService + TenantPlanService
3. Register all in Program.cs
4. GetWebsitePageQuery + UpdateWebsitePageCommand (parallel)
5. Update GenerateContentHandler + GenerateDesignHandler + GenerateImageHandler (parallel, depends 1+2)
6. StudioController — GET/{id} + PUT/{id} + video endpoints (depends 4)
7. Editor.razor (depends 6)
8. GetVideoLibraryQuery + AddVideoAssetCommand (parallel)
9. Videos/Index.razor + StudioLayout.razor nav
10. dotnet build + dotnet test

## Edge Cases & Risks
- ANTHROPIC_API_KEY/FAL_KEY missing: both services throw InvalidOperationException — visible in Railway deploy logs
- Claude latency 5-15s: existing spinner in Editor.razor handles it
- image_generation = agency only: return 402 with upgrade message for pro tenants
- Slug is immutable: UpdateWebsitePageCommand must not accept slug changes
- Anthropic response: parse content[0].text; non-"end_turn" stop_reason = throw

## Verification
```
dotnet build Nucleus.sln   # 0 errors, 0 warnings
dotnet test                # all tests pass
git push origin main
# POST /api/content/generate → real Claude HTML (no placeholder comment)
# POST /api/studio/images/generate → fal.ai URL (no picsum.photos)
# GET /api/studio/pages/{id} → 200 with full WebsitePageDto
# PUT /api/studio/pages/{id} → 200, DB row updated
# GET /studio/videos → Blazor page loads with video list
```

# Implementation Plan: Sprint 33 — Reports Hub

## Context
New cross-hub analytics hub — pure aggregation over existing tables, no new migrations.

## New Files

### Layout
- **File**: `src/Nucleus.Web/Layout/ReportsLayout.razor` (NEW)
- **Change**: Hub="reports" HubColor="#06b6d4" HubName="Reports Hub"; 6 nav items: Overview, Content, Search, Finders, Distribution, (root)

### Blazor Pages
- **File**: `src/Nucleus.Web/Pages/Reports/Index.razor` (NEW) — landing page with 5 feature cards
- **File**: `src/Nucleus.Web/Pages/Reports/Overview.razor` (NEW) — cross-hub KPIs: content published, keywords ranked top-10, finder starts/conversions, AI spend, email reach
- **File**: `src/Nucleus.Web/Pages/Reports/Content.razor` (NEW) — content volume by status + AI cost breakdown by feature
- **File**: `src/Nucleus.Web/Pages/Reports/Search.razor` (NEW) — keywords by position tier + top movers table
- **File**: `src/Nucleus.Web/Pages/Reports/Finders.razor` (NEW) — all finders for brand: starts, completions, conversions, leads captured
- **File**: `src/Nucleus.Web/Pages/Reports/Distribution.razor` (NEW) — email messages (opens/clicks) + social posts by platform + channel reach

### Application — DTOs
- **File**: `src/Nucleus.Application/ReportsHub/DTOs/BrandOverviewDto.cs` (NEW) — KPI snapshot
- **File**: `src/Nucleus.Application/ReportsHub/DTOs/ContentReportDto.cs` (NEW) — status counts + AI cost
- **File**: `src/Nucleus.Application/ReportsHub/DTOs/SearchReportDto.cs` (NEW) — position tiers + snapshot rows
- **File**: `src/Nucleus.Application/ReportsHub/DTOs/FinderReportDto.cs` (NEW) — per-finder stats
- **File**: `src/Nucleus.Application/ReportsHub/DTOs/DistributionReportDto.cs` (NEW) — email + social stats

### Application — Queries
- **File**: `src/Nucleus.Application/ReportsHub/Queries/GetBrandOverviewQuery.cs` (NEW)
  - Queries: ContentPages (published last N days), KeywordRanks (pos ≤ 10), FinderAnalytics (last N days sum), AiUsage (last N days sum CostUsd), SendLog (last N days sum RecipientCount)
- **File**: `src/Nucleus.Application/ReportsHub/Queries/GetContentReportQuery.cs` (NEW)
  - Queries: ContentPages grouped by Status + PageType; AiUsage grouped by Feature (sum cost + tokens)
- **File**: `src/Nucleus.Application/ReportsHub/Queries/GetSearchReportQuery.cs` (NEW)
  - Queries: KeywordRanks (current positions, grouped by tier); KeywordRankSnapshots (last 5 for each keyword to show trend)
- **File**: `src/Nucleus.Application/ReportsHub/Queries/GetFinderReportQuery.cs` (NEW)
  - Queries: Finders for brand; FinderAnalytics last N days grouped by FinderId; FinderSessions for lead capture rate
- **File**: `src/Nucleus.Application/ReportsHub/Queries/GetDistributionReportQuery.cs` (NEW)
  - Queries: EmailCampaignMessages (sum OpenCount, ClickCount, RecipientCount); SocialPosts grouped by Platform + Status; SendLog sum RecipientCount by Channel

### API
- **File**: `src/Nucleus.Api/Controllers/ReportsController.cs` (NEW)
  - `GET /api/reports/overview?brandId=&days=30`
  - `GET /api/reports/content?brandId=&days=30`
  - `GET /api/reports/search?brandId=`
  - `GET /api/reports/finders?brandId=&days=30`
  - `GET /api/reports/distribution?brandId=&days=30`

## Modified Files

- **File**: `src/Nucleus.Web/Layout/ShellLayout.razor` ~line 88
  - **Change**: Add Reports hub-pill button after Finder (Hub=="reports" active check, cyan bar chart SVG icon)

## Implementation Sequence
1. DTOs: BrandOverviewDto, ContentReportDto, SearchReportDto, FinderReportDto, DistributionReportDto
2. Queries: GetBrandOverviewQuery, GetContentReportQuery, GetSearchReportQuery, GetFinderReportQuery, GetDistributionReportQuery
3. ReportsController (5 endpoints)
4. ReportsLayout.razor
5. ShellLayout.razor — add hub button
6. Pages/Reports/Index.razor (landing)
7. Pages/Reports/Overview.razor
8. Pages/Reports/Content.razor
9. Pages/Reports/Search.razor
10. Pages/Reports/Finders.razor
11. Pages/Reports/Distribution.razor
12. `dotnet build Nucleus.sln`

## Edge Cases & Risks
- Brand ownership: all queries must filter by TenantId + BrandId (never cross-tenant)
- Empty data: queries return empty/zero structs, never null — Blazor pages show "No data yet" states
- KeywordRankSnapshot trend: only take last 5 per keyword ordered by CheckedAt desc (limit memory)
- Days param: clamp to 1-365 range

## Verification
```
dotnet build Nucleus.sln
# GET /api/reports/overview?brandId={id}&days=30 → 200 with KPI payload
# Navigate /reports → hub landing; /reports/overview → stats cards
```

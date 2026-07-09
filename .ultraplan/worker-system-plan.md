# Nucleus Autonomous Worker System — Implementation Plan

## Context
Build a Claude Agent SDK-powered worker system that autonomously builds Nucleus sprints, maintains
the codebase (security/perf/deps/features), and retires SEO Hub when Nucleus goes live. Tim gets
Slack notifications; no approval needed per sprint. Staging (Railway) gates every deploy before prod.

---

## File Structure to Create

```
nucleus/
├── .sprints/
│   ├── sprint-24.yaml            # Content Hub spec
│   ├── sprint-25.yaml            # Search Hub spec
│   ├── sprint-26.yaml            # Distribution Hub spec
│   ├── sprint-27.yaml            # Authority Hub spec
│   ├── sprint-28.yaml            # Studio Hub spec
│   └── retirement-checklist.md  # SEO Hub feature parity tracker
├── scripts/
│   ├── worker/
│   │   ├── package.json          # @anthropic-ai/sdk, js-yaml, dotenv
│   │   ├── run-sprint.js         # Entry: reads spec → Claude API → verify → deploy
│   │   ├── sprint-loader.js      # Loads + validates .sprints/sprint-N.yaml
│   │   ├── context-builder.js    # Injects CLAUDE.md + memory/ + codebase map into prompt
│   │   ├── verifier.js           # dotnet build + dotnet test + /health check
│   │   ├── deployer.js           # git push staging → health check → git push master
│   │   └── notifier.js           # Slack: sprint started / passed / failed + diff summary
│   └── maintenance/
│       ├── package.json          # shared with worker/
│       ├── security-sweep.js     # Weekly: NuGet audit + OWASP patterns + secrets scan
│       ├── perf-audit.js         # Monthly: API benchmarks + EF N+1 detection + heap
│       ├── feature-optimize.js   # Bi-weekly: AI cost per tenant, Haiku vs Sonnet routing
│       └── dep-update.js         # Weekly: NuGet patch updates, flag major versions
└── .maintenance/
    └── schedule.yaml             # Cron schedule for all maintenance workers
```

---

## Sprint Spec Format (.sprints/sprint-N.yaml)

```yaml
sprint: 24
name: Content Hub
status: pending          # pending | in_progress | complete | failed

entities:
  - name: ContentPage
    base: TenantEntity
    fields: [title, keyword_id, page_type, status, html_content, seo_title, meta_description]
    ef_config: true
  - name: ContentTemplate
    base: TenantEntity
    fields: [name, page_type, body, is_global]
    ef_config: true
  - name: AiUsage
    base: TenantEntity
    fields: [feature, tokens_used, cost_usd, model, created_at]
    ef_config: true
  - name: BannedWord
    base: TenantEntity
    fields: [word, reason, brand_id_nullable]
    ef_config: true

migration: ContentHub       # dotnet ef migrations add ContentHub

commands:
  - GenerateContentCommand
  - CreateContentPageCommand
  - ApproveContentPageCommand
  - AddBannedWordCommand

queries:
  - GetKeywordLibraryQuery
  - GetContentLibraryQuery
  - GetEditorialCalendarQuery
  - GetContentApprovalQueueQuery

blazor_pages:
  - Pages/Content/Keywords/Index.razor
  - Pages/Content/Generator/Index.razor
  - Pages/Content/Calendar/Index.razor
  - Pages/Content/Queue/Index.razor
  - Pages/Content/Library/Index.razor
  - Pages/Content/BrandVoice/Index.razor
  - Pages/Content/Templates/Index.razor

port_map:
  - seohub_file: src/services/seo-content-generator.js
    nucleus_target: Application/Content/Commands/GenerateContentCommand.cs
    notes: "Multi-tenant: scope by TenantId+BrandId. Track cost in AiUsage."
  - seohub_file: src/query-interface/routes/seo-content/keywords.js
    nucleus_target: Application/Content/Queries/GetKeywordLibraryQuery.cs
  - seohub_file: src/services/seo-editorial-calendar.js
    nucleus_target: Application/Content/Queries/GetEditorialCalendarQuery.cs

plan_gates:
  starter: [keyword_library, generator_limited_5_per_month]
  pro: [generator_unlimited, editorial_calendar, approval_queue]
  agency: [all_features, multi_brand_templates]

acceptance_criteria:
  - dotnet build succeeds (0 errors, 0 warnings)
  - dotnet test passes (all existing tests + new Content Hub tests)
  - EF migration applies cleanly to staging DB
  - /content/keywords loads for authenticated tenant
  - AI generator produces content scoped to correct TenantId
  - AiUsage row written after every generation
  - Starter plan blocked at 5 generations/month
```

---

## run-sprint.js Logic (Pseudocode)

```
1. Load sprint-N.yaml (sprint-loader.js)
2. Build Claude context prompt (context-builder.js):
   - Full CLAUDE.md content
   - memory/*.md content
   - Sprint spec YAML
   - Current codebase file tree
   - Port map with SEO Hub source code excerpts
3. Call Claude API (claude-sonnet-4-6) with:
   - tools: [read_file, write_file, bash]
   - system: "You are implementing Sprint N for Nucleus. Work autonomously.
              Run dotnet build after every file batch. Fix errors immediately.
              Do not stop until all acceptance_criteria are met."
4. Stream + log all tool calls to .sprints/sprint-N.log
5. On Claude completion → run verifier.js:
   - dotnet build (must be 0 errors)
   - dotnet test (must be 0 failures)
   - /health check on local run
6. If verify PASS → deployer.js:
   - git push origin staging
   - Wait for Railway staging health check (poll /health, 5-min timeout)
   - git push origin master (triggers Railway prod deploy)
   - Poll prod /health (5-min timeout)
7. Update sprint-N.yaml status: complete
8. notifier.js → Slack: sprint N done, diff summary, prod URL
9. Auto-start run-sprint.js N+1 (if sprint-N+1.yaml exists + status: pending)
10. If any step FAILS → notifier.js → Slack: BLOCKED + error + log path. STOP.
```

---

## Maintenance Worker Schedules (.maintenance/schedule.yaml)

```yaml
workers:
  - name: security-sweep
    script: scripts/maintenance/security-sweep.js
    schedule: "0 9 * * MON"    # Every Monday 9 AM
    what:
      - dotnet list package --vulnerable (NuGet CVE check)
      - grep for hardcoded secrets (API keys, connection strings in source)
      - Scan controllers for missing [Authorize] on new endpoints
      - Check RLS policies exist on all new EF entities
      - Slack report: PASS / issues found

  - name: perf-audit
    script: scripts/maintenance/perf-audit.js
    schedule: "0 9 1 * *"     # 1st of every month
    what:
      - Benchmark all API endpoints (response time p95)
      - EF Core query log analysis (N+1 detection, missing indexes)
      - Memory/heap baseline snapshot
      - Slack report: regressions flagged vs last month baseline

  - name: feature-optimize
    script: scripts/maintenance/feature-optimize.js
    schedule: "0 9 * * WED"   # Every Wednesday 9 AM
    what:
      - Query AiUsage table: cost per tenant, cost per feature
      - Flag any feature spending >$2/day per tenant → suggest Haiku routing
      - Review plan gate effectiveness (Starter hitting limits → upsell signal)
      - Slack report: cost breakdown + optimization recommendations

  - name: dep-update
    script: scripts/maintenance/dep-update.js
    schedule: "0 9 * * FRI"   # Every Friday 9 AM
    what:
      - dotnet outdated (NuGet package version check)
      - Auto-apply patch updates (X.Y.PATCH only, never minor/major)
      - Create PR for minor/major updates with changelog summary
      - Run dotnet test after patch updates; push if green
```

---

## Staging Environment Setup

**New Railway service:** `nucleus-staging`
- Same repo, same Dockerfile
- Env vars: `NUCLEUS_DB_CONNECTION` → staging Supabase DB (separate project)
- `JWT_SECRET` → different value
- `STRIPE_SECRET_KEY` → Stripe test mode key
- `RAILWAY_DEPLOY_WEBHOOK` → staging webhook (separate from prod)

**Deployer flow:**
```
staging branch → Railway staging service → health check passes → master → prod
```

**GitHub Actions update needed:**
- Add `staging` branch to ci.yml triggers
- Add staging deploy step in deploy.yml (fires staging webhook on staging branch push)

---

## SEO Hub Retirement Tracker (.sprints/retirement-checklist.md)

```markdown
# SEO Hub → Nucleus Feature Parity

Nucleus goes live = SEO Hub shuts down.
Workers update this file as each sprint ships.

| SEO Hub Feature              | Sprint | Status     | Nucleus Path                          |
|------------------------------|--------|------------|---------------------------------------|
| Keyword Library              | 24     | pending    | Pages/Content/Keywords/               |
| AI Content Generator         | 24     | pending    | Application/Content/Commands/         |
| Editorial Calendar           | 24     | pending    | Pages/Content/Calendar/               |
| Content Approval Queue       | 24     | pending    | Pages/Content/Queue/                  |
| Brand Voice / Banned Words   | 24     | pending    | Pages/Content/BrandVoice/             |
| DataForSEO Rankings          | 25     | pending    | Pages/Search/Rankings/                |
| Keyword Rank History         | 25     | pending    | Pages/Search/History/                 |
| Topic Clusters               | 25     | pending    | Pages/Search/Clusters/                |
| Content Gap Analysis         | 25     | pending    | Pages/Search/Gaps/                    |
| GHL Social Planner           | 26     | pending    | Pages/Distribution/Social/            |
| Email Blasts                 | 26     | pending    | Pages/Distribution/Email/             |
| Backlink Tracking            | 27     | pending    | Pages/Authority/Backlinks/            |
| Schema Manager               | 27     | pending    | Pages/Authority/Schema/               |
| Page Manager (CMS)           | 28     | pending    | Pages/Studio/Pages/                   |
| Design Studio                | 28     | pending    | Pages/Studio/Design/                  |
| Image Generator (Flux)       | 28     | pending    | Pages/Studio/Images/                  |

When all rows = complete: SEO Hub shuts down.
```

---

## Implementation Sequence

1. **Set up staging Railway service** — new service, separate Supabase DB, env vars
2. **Update GitHub Actions** — add staging branch support to ci.yml + deploy.yml
3. **Write scripts/worker/package.json** — @anthropic-ai/sdk, js-yaml, dotenv, node-fetch
4. **Write sprint-loader.js** — YAML parser + schema validation
5. **Write context-builder.js** — reads CLAUDE.md + memory/ + codebase tree + SEO Hub port files
6. **Write verifier.js** — dotnet build + dotnet test + health check polling
7. **Write deployer.js** — git push staging → poll health → git push master → poll health
8. **Write notifier.js** — Slack webhook (sprint started, passed, failed, maintenance reports)
9. **Write run-sprint.js** — orchestrates 1-8, calls Claude API, auto-chains next sprint
10. **Write .sprints/sprint-24.yaml through sprint-28.yaml** — full specs for all 5 hubs
11. **Write .sprints/retirement-checklist.md** — SEO Hub parity tracker (workers update it)
12. **Write 4 maintenance workers** (security-sweep, perf-audit, feature-optimize, dep-update)
13. **Write .maintenance/schedule.yaml** — cron definitions for all 4 workers
14. **Add Railway cron service** — points at maintenance scripts on schedule
15. **Kick off Sprint 24** — `node scripts/worker/run-sprint.js 24`

---

## Key Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| Claude API call exceeds context window during large sprint | context-builder.js chunks: CLAUDE.md + spec + top 20 most-relevant files only (not full tree) |
| dotnet build fails mid-sprint, Claude loops | Hard stop after 3 consecutive build-fail cycles → Slack BLOCKED alert |
| Staging DB out of sync with prod migrations | Staging DB is a fresh Supabase project; migrations apply from 0 each sprint |
| Worker pushes broken code to prod | Prod deploy ONLY happens after staging /health returns 200 for 60 consecutive seconds |
| Maintenance dep-update breaks the build | dep-update.js runs dotnet test after each patch update; reverts the package if test fails |
| SEO Hub shutdown too early | retirement-checklist.md must show all rows = complete before any shutdown action is taken |

---

## Verification Command

```bash
# Sprint worker smoke test (dry run — no API call, no deploy)
node scripts/worker/run-sprint.js 24 --dry-run

# Expected output:
# [loader]  sprint-24.yaml: VALID (4 entities, 4 commands, 4 queries, 7 pages)
# [context] Context size: ~45K tokens
# [verify]  dotnet build: would run
# [deploy]  staging push: would push to origin/staging
# [notify]  Slack: would POST to SLACK_NUCLEUS_WEBHOOK
# Dry run complete. Ready to execute.
```

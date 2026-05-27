# Nucleus Memory Index

Read ALL of these at the start of every session.

| File | What's in it | Read when |
|------|-------------|-----------|
| `strategy.md` | What Nucleus IS, relationship to SEO Hub, SaaS vision, north star principles | Every session |
| `architecture.md` | Tenant hierarchy, shell pattern, DB design, CQRS, file structure | Every session |
| `sprints.md` | Sprint history (1-22 complete), feature inventory, next sprint roadmap | Every session |
| `decisions.md` | Architectural decisions with rationale | Before making any structural change |

## Update Rules
- After every sprint: update `sprints.md` — mark complete, add what was built, update "next" section
- New architectural decision: add to `decisions.md` with date + rationale
- New entity added: update `architecture.md` entity table
- Strategic direction changes: update `strategy.md`

## Current State (as of 2026-05-26)
- Sprint 22 complete
- Next: Sprint 23 — Service Hub Architecture (shell pattern + 5 layouts + themes)
- Then: Sprint 24 — Content Hub (first fully working service hub)
- SEO Hub = test server | Nucleus = live production SaaS

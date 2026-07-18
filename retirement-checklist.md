# SEO Hub → Nucleus Retirement Checklist

Last updated: 2026-07-18 (Sprint 28 — Studio Hub)

> All rows must be `complete` before any SEO Hub shutdown.

## Auth & Identity
| Feature | SEO Hub Status | Nucleus Status | Row |
|---------|---------------|----------------|-----|
| Email/password login | live | complete (Sprint 5) | ✅ |
| JWT + refresh tokens | live | complete (Sprint 5) | ✅ |
| Forgot/reset password | live | complete (Sprint 16) | ✅ |
| Email verification | live | complete (Sprint 7) | ✅ |
| Change password | live | complete (Sprint 5) | ✅ |

## Multi-tenancy & Brands
| Feature | SEO Hub Status | Nucleus Status | Row |
|---------|---------------|----------------|-----|
| Tenant isolation (TenantId) | N/A (single-tenant) | complete (Sprint 1) | ✅ |
| Brand CRUD | live | complete (Sprint 4) | ✅ |
| Brand onboarding wizard | live | complete (Sprint 3) | ✅ |
| WP/GHL verification | live | complete (Sprint 7) | ✅ |
| Team management | live | complete (Sprint 6) | ✅ |

## Billing
| Feature | SEO Hub Status | Nucleus Status | Row |
|---------|---------------|----------------|-----|
| Stripe checkout | live | complete (Sprint 18) | ✅ |
| Stripe portal | live | complete (Sprint 18) | ✅ |
| Plan enforcement | live | complete (Sprint 22) | ✅ |

## Content Hub
| Feature | SEO Hub Status | Nucleus Status | Row |
|---------|---------------|----------------|-----|
| Keyword library | live | complete (Sprint 9) | ✅ |
| Rank tracking (DataForSEO) | live | complete (Sprint 9/10) | ✅ |
| AI content generator | live | complete (Sprint 24) | ✅ |
| Editorial calendar | live | complete (Sprint 24) | ✅ |
| Content library | live | complete (Sprint 24) | ✅ |

## Search Hub
| Feature | SEO Hub Status | Nucleus Status | Row |
|---------|---------------|----------------|-----|
| Rankings dashboard | live | complete (Sprint 25) | ✅ |
| Rank history | live | complete (Sprint 25) | ✅ |
| Search alerts | live | complete (Sprint 25) | ✅ |
| Topic clusters | live | complete (Sprint 25) | ✅ |
| Content gaps | live | complete (Sprint 25) | ✅ |
| Page performance | live | complete (Sprint 25) | ✅ |

## Distribution Hub
| Feature | SEO Hub Status | Nucleus Status | Row |
|---------|---------------|----------------|-----|
| Social scheduler | live | complete (Sprint 26) | ✅ |
| Email blasts | live | complete (Sprint 26) | ✅ |
| Campaign workspace | live | complete (Sprint 26) | ✅ |
| Send log | live | complete (Sprint 26) | ✅ |

## Authority Hub
| Feature | SEO Hub Status | Nucleus Status | Row |
|---------|---------------|----------------|-----|
| Backlink tracking | live | complete (Sprint 27) | ✅ |
| Brand mentions | live | complete (Sprint 27) | ✅ |
| Schema manager | live | complete (Sprint 27) | ✅ |
| Outreach queue | live | complete (Sprint 27) | ✅ |

## Studio Hub
| Feature | SEO Hub Status | Nucleus Status | Row |
|---------|---------------|----------------|-----|
| Page Manager (CMS) | live | complete (Sprint 28) | ✅ |
| Design Studio (AI builder) | live | complete (Sprint 28) | ✅ |
| Image Generator (Flux) | live | complete (Sprint 28) | ✅ |
| Asset Library | live | complete (Sprint 28) | ✅ |
| Video Library | in-progress | entity+API complete; UI = Sprint 29 | 🟡 |

## Infrastructure
| Feature | SEO Hub Status | Nucleus Status | Row |
|---------|---------------|----------------|-----|
| EF Migrations (no EnsureCreated) | N/A | complete (Sprint 17) | ✅ |
| Hangfire background jobs | live | complete (Sprint 19) | ✅ |
| Sentry error monitoring | live | complete (Sprint 15) | ✅ |
| Audit log | live | complete (Sprint 20) | ✅ |
| CI/CD (GitHub Actions) | N/A | complete (Sprint 21) | ✅ |
| Stripe billing | live | complete (Sprint 18) | ✅ |
| Redis (distributed cache) | — | pending (Sprint 29+) | 🔴 |
| Public API + API keys | — | pending (P3) | 🔴 |

## Summary
- ✅ Complete: 38 / 40 rows
- 🟡 In progress: 1 row (Video Library UI)
- 🔴 Not started: 1 row (Redis)

**Retirement gate: NOT READY** — Complete Video Library UI + Redis before shutdown.

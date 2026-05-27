# Nucleus — Strategy & North Star

## What Nucleus IS

Nucleus is a **multi-tenant SaaS Marketing OS** — a platform for businesses to manage all their digital marketing from one place. It competes in the space of HubSpot + SEMrush + Hootsuite + Webflow combined, but built specifically for the Montana registration / DMV services industry first, then expanded to any industry.

**The relationship to SEO Hub:**
- SEO Hub = Tim's internal operational tool (stays in TK-DL-Master-Databaset repo, keeps running)
- Nucleus = the SaaS product that SEO Hub's features get rebuilt into, correctly, for sale to others
- Features move from SEO Hub → Nucleus when they are ready to be productized and multi-tenanted

**The SaaS model:**
- Tenants = companies/owners who buy Nucleus
- Each tenant has one or many Brands
- Brands have their own GHL, WP, DataForSEO, email provider credentials
- Billing is per-tenant via Stripe (starter / pro / agency tiers)

---

## The North Star Principle (2026-05-26)

> **"Get the structure built the right way from the start."**

Every architectural decision in Nucleus is made with this in mind. If a pattern isn't correct, fix the pattern before building features on top of it. Technical debt is not acceptable in the foundation — it's acceptable only in optional features that can be replaced later.

The second rule follows directly:

> **"Only add services that are fully working."**

A service hub is not added to Nucleus until ALL of its core features work correctly end-to-end. No placeholder pages, no "coming soon" sections, no half-implemented features shipped to production. Each hub is built fully in development, tested, then released as a complete unit.

---

## The 5 Service Hubs

SEO Hub's menu sections map to 5 independent service hubs in Nucleus. Each hub is:
- Its own branded color theme
- Its own focus menu (contextual sub-navigation for that service)
- Accessible from the global nav at any time (no full page reload)
- Built and shipped as a complete unit (never partially)

| Hub | Theme Color | Core Purpose |
|-----|-------------|--------------|
| Content Hub | Blue (#3b82f6) | Keywords, AI generation, editorial calendar, content library |
| Search Hub | Green (#16a34a) | Rankings, SERP features, topic clusters, GEO/AI visibility |
| Authority Hub | Purple (#8b5cf6) | Backlinks, brand mentions, press releases, schema |
| Distribution Hub | Amber (#f59e0b) | Social, email blasts, campaigns, send log |
| Studio Hub | Pink (#ec4899) | Design, image gen, video, page manager, asset library |

---

## Relationship to Tim's Brands

Tim's companies (DL, RL, MRS, SLH, HMR) are the initial tenants of Nucleus. They serve as the proving ground. Once Nucleus works well for Tim's businesses, it is sold to other businesses.

**CRITICAL: DL, RL, MRS, SLH, HMR are SEPARATE companies.** Each is a separate Tenant or Brand in Nucleus. No cross-contamination. This is one of the core problems Nucleus fixes — SEO Hub has no hard tenant isolation.

---

## Migration Strategy

**Phase 1 (current):** Build Nucleus foundation — auth, tenants, brands, billing, core data models. (Sprints 1-22, complete)

**Phase 2 (next):** Add service hub architecture (shell pattern + layouts + themes). Then build each hub fully before adding the next one.

**Phase 3:** Tim's brands migrate from SEO Hub to Nucleus one hub at a time. SEO Hub stays running as fallback.

**Phase 4:** Open Nucleus to other customers. SEO Hub sunsets when Tim's team is fully on Nucleus.

---

## What Nucleus is NOT

- NOT a rebuild of the entire TK-DL-Master-Databaset repo — only the marketing/SEO Hub features
- NOT separate databases per tenant — one DB, TenantId-scoped RLS
- NOT tied to any single brand — it is brand-agnostic by design
- NOT adding features until the structure is proven — foundation first

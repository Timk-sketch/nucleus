# SEO Hub Retirement Checklist

> SEO Hub shuts down ONLY when every row in this table shows `complete`.
> Workers update this file as each sprint ships.
> Sprint 28 (Studio Hub) has `retirement_trigger: true` — final gate.

## Feature Parity Map

| SEO Hub Feature              | Sprint | Status   | Nucleus Path                              |
|------------------------------|--------|----------|-------------------------------------------|
| Keyword Library              | 24     | pending  | Pages/Content/Keywords/                   |
| AI Content Generator         | 24     | pending  | Application/Content/Commands/Generate     |
| Editorial Calendar           | 24     | pending  | Pages/Content/Calendar/                   |
| Content Approval Queue       | 24     | pending  | Pages/Content/Queue/                      |
| Content Library              | 24     | pending  | Pages/Content/Library/                    |
| Brand Voice / Banned Words   | 24     | pending  | Pages/Content/BrandVoice/                 |
| Content Templates            | 24     | pending  | Pages/Content/Templates/                  |
| DataForSEO Rankings          | 25     | pending  | Pages/Search/Rankings/                    |
| Keyword Rank History         | 25     | pending  | Pages/Search/History/                     |
| Search Alerts                | 25     | pending  | Pages/Search/Alerts/                      |
| Topic Clusters               | 25     | pending  | Pages/Search/Clusters/                    |
| Content Gap Analysis         | 25     | pending  | Pages/Search/Gaps/                        |
| GHL Social Planner           | 26     | pending  | Pages/Distribution/Social/                |
| Email Blasts                 | 26     | pending  | Pages/Distribution/Email/                 |
| Campaign Send Log            | 26     | pending  | Pages/Distribution/SendLog/               |
| Backlink Tracking            | 27     | pending  | Pages/Authority/Backlinks/                |
| Brand Mentions               | 27     | pending  | Pages/Authority/Mentions/                 |
| Schema Manager               | 27     | pending  | Pages/Authority/Schema/                   |
| Outreach Queue               | 27     | pending  | Pages/Authority/Outreach/                 |
| Page Manager (CMS)           | 28     | pending  | Pages/Studio/Pages/                       |
| Design Studio                | 28     | pending  | Pages/Studio/Design/                      |
| Image Generator (Flux)       | 28     | pending  | Pages/Studio/Images/                      |
| Asset Library                | 28     | pending  | Pages/Studio/Assets/                      |

## Retirement Gate

- [ ] All 23 rows above show `complete`
- [ ] Tim confirms all brands are live on Nucleus
- [ ] DNS for all brand domains points to Nucleus (not SEO Hub)
- [ ] SEO Hub Railway service stopped (do not delete — archive 30 days first)
- [ ] `TK-DL-Master-Databaset` repo archived (read-only) after 30-day buffer

## Update Instructions (for workers)

When a sprint ships, update each relevant row from `pending` to `complete`:
```
| Keyword Library | 24 | complete | Pages/Content/Keywords/ |
```

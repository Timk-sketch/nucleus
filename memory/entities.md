# Nucleus — Domain Entities Reference

Last updated: Sprint 24

## Base Classes

### TenantEntity
All tenant-scoped entities inherit this:
```csharp
public abstract class TenantEntity {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

## All Domain Entities

| Entity | Base | Table | Key Fields | Sprint |
|--------|------|-------|------------|--------|
| Tenant | — | tenants | Id, Name, Slug, Plan, StripeCustomerId, StripeSubscriptionId, SubscriptionStatus, IsActive | 1 |
| ApplicationUser | IdentityUser | nucleus_users | TenantId, FirstName, LastName, Role, RefreshToken | 1 |
| Brand | TenantEntity | brands | Code, Name, Domain, Slug, PrimaryColor, Status, GhlLocationId, GhlApiKey, WpSiteUrl, WpAppPassword, DataForSeoTag, DripApiToken, SendgridApiKey, EmailProvider | 1 |
| BrandProvisioningStep | TenantEntity | brand_provisioning_steps | BrandId, StepName, Status, Message | 3 |
| RefreshToken | — | nucleus_user_sessions | UserId, Token, ExpiresAt, IsRevoked | 5 |
| AuditLog | — | audit_logs | TenantId, Actor, Action, EntityType, EntityId, Changes (jsonb), CreatedAt | 20 |
| BrandKeyword | TenantEntity | brand_keywords | BrandId, Keyword, CurrentRank, PeakRank, TargetUrl, Notes | 9 |
| KeywordRank | TenantEntity | keyword_ranks | BrandId, KeywordId, Rank, RankedUrl, CheckedAt | 9 |
| GhlContact | TenantEntity | ghl_contacts | BrandId, GhlContactId, FirstName, LastName, Email, Phone | 11 |
| EmailCampaign | TenantEntity | email_campaigns | BrandId, Subject, HtmlBody, Status, RecipientCount, SentAt | 13 |
| **ContentPage** | **TenantEntity** | **content_pages** | **BrandId, KeywordId?, Title, PageType, Status, HtmlContent, SeoTitle, MetaDescription, AiModel, AiPrompt, WordCount, ScheduledAt, PublishedAt, ReviewNotes** | **24** |
| **ContentTemplate** | **TenantEntity** | **content_templates** | **BrandId, Name, PageType, Body, IsGlobal, IsActive** | **24** |
| **AiUsage** | **TenantEntity** | **ai_usages** | **BrandId, Feature, TokensUsed, CostUsd (decimal 10,6), Model, ContentPageId?** | **24** |
| **BannedWord** | **TenantEntity** | **banned_words** | **BrandId, Word, Reason** | **24** |
| KeywordRankSnapshot | TenantEntity | keyword_rank_snapshots | BrandId, KeywordId, Position, Url, SearchVolume, Competition, CheckedAt | 25 |
| SearchAlert | TenantEntity | search_alerts | BrandId, KeywordId, AlertType, Threshold, IsActive, TriggeredAt, Message | 25 |
| TopicCluster | TenantEntity | topic_clusters | BrandId, Name, PillarKeyword, ClusterKeywordsJson (jsonb), Status, Notes | 25 |
| SocialPost | TenantEntity | social_posts | BrandId, Platform, Caption, ImageUrl, ScheduledAt, PublishedAt, Status, ExternalPostId, Provider | 26 |
| EmailCampaignMessage | TenantEntity | email_campaign_messages | BrandId, CampaignId, Subject, HtmlBody, SentAt, OpenCount, ClickCount, RecipientCount, Status | 26 |
| SendLog | TenantEntity | send_logs | BrandId, CampaignId?, SocialPostId?, Channel, RecipientCount, SentAt, Provider, Status, ErrorMessage | 26 |
| BacklinkRecord | TenantEntity | backlink_records | BrandId, SourceUrl, TargetUrl, AnchorText, DomainRating, FirstSeenAt, LastSeenAt, IsActive | 27 |
| BrandMention | TenantEntity | brand_mentions | BrandId, SourceUrl, MentionText, Sentiment, DiscoveredAt, IsReviewed | 27 |
| SchemaTemplate | TenantEntity | schema_templates | BrandId, PageType, SchemaType, TemplateJson (jsonb), IsActive | 27 |
| OutreachQueueItem | TenantEntity | outreach_queue_items | BrandId, TargetUrl, ContactEmail, Status, Notes, OutreachAt | 27 |
| WebsitePage | TenantEntity | website_pages | BrandId, Slug, Title, PageType, HtmlContent, SeoTitle, MetaDescription, OgImage, Status, PublishedAt, SchemaJson (jsonb) | 28 |
| DesignAsset | TenantEntity | design_assets | BrandId, Name, AssetType, Url, Width, Height, FileSize, UploadedAt, PromptUsed, MimeType | 28 |
| VideoAsset | TenantEntity | video_assets | BrandId, Name, Url, ThumbnailUrl, DurationSeconds, Platform, UploadedAt, Description | 28 |
| SiteDomain | TenantEntity | site_domains | BrandId, Hostname (globally unique), IsPrimary, SslVerified, VerifiedAt | 29 |
| SiteDeployment | TenantEntity | site_deployments | BrandId, DeployedBy, PageCount, Status, DeployedAt, Notes | 29 |
| PageCache | TenantEntity | page_caches | BrandId, Slug (unique per brand), RenderedHtml, Etag, CachedAt, InvalidatedAt | 29 |
| SiteVisit | TenantEntity | site_visits | BrandId, Slug, Referrer, UserAgent, IpHash (SHA-256), VisitedAt | 29 |
| Finder | TenantEntity | finders | BrandId, Name, Slug (unique per brand), IntroText, Status, PublishedAt, EmbedToken (globally unique) | 30 |
| FinderStep | TenantEntity | finder_steps | FinderId, StepOrder, StepType, QuestionText, HelperText, IsRequired | 30 |
| FinderOption | TenantEntity | finder_options | StepId, Label, Value, IconUrl, Description, SortOrder | 30 |
| FinderResult | TenantEntity | finder_results | FinderId, ConditionJson (jsonb), ProductKey, Headline, Body, CtaLabel, CtaUrl | 30 |
| FinderSession | TenantEntity | finder_sessions | FinderId, SessionToken (globally unique), AnswersJson (jsonb), ResultKey, Converted, CompletedAt | 30 |
| FinderAnalytics | TenantEntity | finder_analytics | FinderId, Date (DateOnly), Starts, Completions, Conversions, DropOffStepId (unique on FinderId+Date) | 30 |

## Content Hub Entity Detail (Sprint 24)

### ContentPage
```
status: "draft" | "review" | "approved" | "published" | "rejected"
page_type: "blog_post" | "landing_page" | "service_page" | "pillar" | "cluster" | "other"
- KeywordId is nullable FK to BrandKeyword
- AiModel/AiPrompt are null for manually authored content
- ScheduledAt drives editorial calendar placement
- ReviewNotes written by reviewer on approve/reject
```

### ContentTemplate
```
page_type: "blog_post" | "landing_page" | "service_page" | "pillar" | "cluster"
- Body supports {{keyword}}, {{brand}}, {{service}}, {{location}} placeholders
- IsGlobal = true → template available to all brands in the tenant
```

### AiUsage
```
feature: "content_generation" | "design_studio" | "image_gen"
- Counted per TenantId + Feature + monthly window for plan gating
- Starter plan: max 5 content_generation rows per calendar month
- CostUsd uses decimal(10,6) for sub-cent precision
```

### BannedWord
```
- Normalised to lowercase before storage (prevents case duplicates)
- Unique per (BrandId, Word)
- Injected into AI prompt as "never use" instructions
```

## Key Indexes (all entities have TenantId index + composite (TenantId, BrandId/FinderId))

### Additional domain indexes
- `brand_keywords`: (BrandId, Keyword) unique
- `ghl_contacts`: (BrandId, GhlContactId) unique
- `keyword_ranks`: (KeywordId, CheckedAt)
- `search_alerts`: (KeywordId, IsActive)
- `social_posts`: (BrandId, ScheduledAt)
- `email_campaign_messages`: CampaignId
- `send_logs`: (BrandId, SentAt)
- `backlink_records`: (BrandId, IsActive), (BrandId, FirstSeenAt)
- `brand_mentions`: (BrandId, IsReviewed), (BrandId, DiscoveredAt)
- `schema_templates`: (BrandId, PageType), (BrandId, IsActive)
- `outreach_queue_items`: (BrandId, Status)
- `website_pages`: (BrandId, Status), **(BrandId, Slug) UNIQUE**
- `design_assets`: (BrandId, AssetType), (BrandId, UploadedAt)
- `video_assets`: (BrandId, Platform), (BrandId, UploadedAt)
- `site_domains`: **Hostname GLOBALLY UNIQUE**
- `page_caches`: **(BrandId, Slug) UNIQUE**, InvalidatedAt
- `site_visits`: (BrandId, Slug), (BrandId, VisitedAt)
- `site_deployments`: (BrandId, CreatedAt)
- `finders`: **EmbedToken GLOBALLY UNIQUE**, **(BrandId, Slug) UNIQUE**, (BrandId, Status)
- `finder_sessions`: **SessionToken GLOBALLY UNIQUE**
- `finder_analytics`: **(FinderId, Date) UNIQUE**
- **`content_pages`**: (BrandId,Status), (BrandId,ScheduledAt), (BrandId,KeywordId)
- **`content_templates`**: (BrandId,PageType), (BrandId,IsActive)
- **`ai_usages`**: (TenantId,Feature,CreatedAt) — for plan gate counting
- **`banned_words`**: (BrandId,Word)

## EF Configuration Notes
- `Brand.WpAppPassword`, `GhlApiKey`, `DripApiToken`, `SendgridApiKey` → encrypted at rest via EncryptedStringConverter
- `TopicCluster.ClusterKeywordsJson` → jsonb
- `SchemaTemplate.TemplateJson` → jsonb
- `WebsitePage.SchemaJson` → jsonb (nullable)
- `AuditLog.Changes` → jsonb
- `Brand.ServicesProvisioned` → jsonb
- `FinderResult.ConditionJson` → jsonb (default `{}`)
- `FinderSession.AnswersJson` → jsonb (default `{}`)
- `AiUsage.CostUsd` → decimal(10,6)
- Global TenantId query filter applied to all TenantEntity subclasses in OnModelCreating

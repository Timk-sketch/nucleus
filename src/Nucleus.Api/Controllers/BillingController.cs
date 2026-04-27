using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Nucleus.Application.Common;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Infrastructure.Data;
using Stripe;
using Stripe.Checkout;
using System.Security.Claims;

namespace Nucleus.Api.Controllers;

[ApiController]
[Route("api/v1/billing")]
[Authorize]
[Produces("application/json")]
public class BillingController(
    NucleusDbContext db,
    ICurrentTenantService tenantService,
    IConfiguration config,
    IMemoryCache cache,
    ILogger<BillingController> logger) : ControllerBase
{
    private static string SubCacheKey(Guid tenantId) => $"subscription:{tenantId}";

    // GET /api/v1/billing/subscription
    [HttpGet("subscription")]
    public async Task<IActionResult> GetSubscription(CancellationToken ct)
    {
        var tenantId = tenantService.TenantId;
        var cacheKey = SubCacheKey(tenantId);

        if (!cache.TryGetValue(cacheKey, out object? cached))
        {
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
            if (tenant is null) return NotFound(ApiResponse.Fail("Tenant not found."));

            cached = new
            {
                tenant.Plan,
                tenant.SubscriptionStatus,
                tenant.StripeSubscriptionId,
                StripeConfigured = !string.IsNullOrEmpty(config["STRIPE_SECRET_KEY"]),
            };
            cache.Set(cacheKey, cached, TimeSpan.FromSeconds(30));
        }

        return Ok(ApiResponse.Ok(cached));
    }

    // POST /api/v1/billing/checkout
    // Creates a Stripe Checkout session and returns the hosted URL
    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout([FromBody] CheckoutRequest req, CancellationToken ct)
    {
        var secretKey = config["STRIPE_SECRET_KEY"];
        if (string.IsNullOrEmpty(secretKey))
            return BadRequest(ApiResponse.Fail("Stripe is not configured."));

        var priceId = req.PriceId ?? config["STRIPE_PRICE_ID"];
        if (string.IsNullOrEmpty(priceId))
            return BadRequest(ApiResponse.Fail("No Stripe price ID configured."));

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantService.TenantId, ct);
        if (tenant is null) return NotFound(ApiResponse.Fail("Tenant not found."));

        var appUrl = config["APP_URL"]?.TrimEnd('/') ?? "http://localhost:5000";

        StripeConfiguration.ApiKey = secretKey;

        // Create or reuse Stripe customer
        var customerId = tenant.StripeCustomerId;
        if (string.IsNullOrEmpty(customerId))
        {
            var customerEmail = User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue("email")
                ?? tenant.Name;

            var customerService = new CustomerService();
            var customer = await customerService.CreateAsync(new CustomerCreateOptions
            {
                Email = customerEmail,
                Name = tenant.Name,
                Metadata = new Dictionary<string, string> { ["tenant_id"] = tenant.Id.ToString() },
            }, cancellationToken: ct);

            tenant.StripeCustomerId = customer.Id;
            customerId = customer.Id;
            await db.SaveChangesAsync(ct);
        }

        var sessionService = new SessionService();
        var session = await sessionService.CreateAsync(new SessionCreateOptions
        {
            Customer = customerId,
            Mode = "subscription",
            LineItems = [new SessionLineItemOptions { Price = priceId, Quantity = 1 }],
            SuccessUrl = $"{appUrl}/billing?success=1",
            CancelUrl = $"{appUrl}/billing?canceled=1",
            Metadata = new Dictionary<string, string> { ["tenant_id"] = tenant.Id.ToString() },
        }, cancellationToken: ct);

        return Ok(ApiResponse.Ok(new { url = session.Url }));
    }

    // POST /api/v1/billing/portal
    // Opens Stripe Customer Portal so the user can manage/cancel their subscription
    [HttpPost("portal")]
    public async Task<IActionResult> CreatePortal(CancellationToken ct)
    {
        var secretKey = config["STRIPE_SECRET_KEY"];
        if (string.IsNullOrEmpty(secretKey))
            return BadRequest(ApiResponse.Fail("Stripe is not configured."));

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantService.TenantId, ct);
        if (tenant is null || string.IsNullOrEmpty(tenant.StripeCustomerId))
            return BadRequest(ApiResponse.Fail("No Stripe customer found for this account."));

        StripeConfiguration.ApiKey = secretKey;

        var appUrl = config["APP_URL"]?.TrimEnd('/') ?? "http://localhost:5000";

        var portalService = new Stripe.BillingPortal.SessionService();
        var portal = await portalService.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = tenant.StripeCustomerId,
            ReturnUrl = $"{appUrl}/billing",
        }, cancellationToken: ct);

        return Ok(ApiResponse.Ok(new { url = portal.Url }));
    }

    // POST /api/v1/billing/webhook
    // Stripe sends events here — validates signature before processing
    [HttpPost("webhook")]
    [AllowAnonymous]
    [Consumes("application/json")]
    public async Task<IActionResult> Webhook()
    {
        var webhookSecret = config["STRIPE_WEBHOOK_SECRET"];
        if (string.IsNullOrEmpty(webhookSecret))
            return BadRequest("Webhook secret not configured.");

        string json;
        using (var reader = new StreamReader(Request.Body))
            json = await reader.ReadToEndAsync();

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                webhookSecret);
        }
        catch (StripeException ex)
        {
            logger.LogWarning("Stripe webhook signature validation failed: {Msg}", ex.Message);
            return BadRequest("Invalid webhook signature.");
        }

        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
            {
                var session = (Session)stripeEvent.Data.Object;
                if (!Guid.TryParse(session.Metadata?.GetValueOrDefault("tenant_id"), out var tenantId)) break;

                var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
                if (tenant is null) break;

                tenant.Plan = "pro";
                tenant.StripeCustomerId ??= session.CustomerId;
                tenant.StripeSubscriptionId = session.SubscriptionId;
                tenant.SubscriptionStatus = "active";
                await db.SaveChangesAsync();
                cache.Remove(SubCacheKey(tenantId));
                logger.LogInformation("Tenant {TenantId} upgraded to pro via Stripe checkout", tenantId);
                break;
            }

            case EventTypes.CustomerSubscriptionUpdated:
            {
                var sub = (Subscription)stripeEvent.Data.Object;
                var tenant = await db.Tenants
                    .FirstOrDefaultAsync(t => t.StripeSubscriptionId == sub.Id);
                if (tenant is null) break;

                tenant.SubscriptionStatus = sub.Status;
                if (sub.Status == "canceled") tenant.Plan = "starter";
                await db.SaveChangesAsync();
                cache.Remove(SubCacheKey(tenant.Id));
                logger.LogInformation("Tenant {TenantId} subscription status → {Status}", tenant.Id, sub.Status);
                break;
            }

            case EventTypes.CustomerSubscriptionDeleted:
            {
                var sub = (Subscription)stripeEvent.Data.Object;
                var tenant = await db.Tenants
                    .FirstOrDefaultAsync(t => t.StripeSubscriptionId == sub.Id);
                if (tenant is null) break;

                tenant.Plan = "starter";
                tenant.SubscriptionStatus = "canceled";
                tenant.StripeSubscriptionId = null;
                await db.SaveChangesAsync();
                cache.Remove(SubCacheKey(tenant.Id));
                logger.LogInformation("Tenant {TenantId} subscription canceled", tenant.Id);
                break;
            }

            default:
                logger.LogDebug("Unhandled Stripe event type: {Type}", stripeEvent.Type);
                break;
        }

        return Ok();
    }
}

public record CheckoutRequest(string? PriceId);

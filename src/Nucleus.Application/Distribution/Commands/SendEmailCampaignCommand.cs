using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.Distribution.Commands;

/// <summary>
/// Sends an existing EmailCampaign to a list of recipients.
/// Pluggable transport: if the brand has GHL API key → uses GHL email blast.
/// Otherwise falls back to SMTP (IEmailService).
/// Appends a SendLog entry for the full audit trail.
/// </summary>
public record SendEmailCampaignCommand(
    Guid CampaignId,
    Guid BrandId,
    List<string> Recipients) : IRequest<SendEmailCampaignResult>;

public record SendEmailCampaignResult(
    bool Success,
    int SentCount,
    int FailedCount,
    string? ErrorMessage = null);

public class SendEmailCampaignValidator : AbstractValidator<SendEmailCampaignCommand>
{
    public SendEmailCampaignValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.Recipients)
            .NotEmpty()
            .WithMessage("At least one recipient is required.");
        RuleForEach(x => x.Recipients)
            .EmailAddress()
            .WithMessage("All recipients must be valid email addresses.");
    }
}

public class SendEmailCampaignHandler : IRequestHandler<SendEmailCampaignCommand, SendEmailCampaignResult>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;
    private readonly IEmailService _emailService;

    public SendEmailCampaignHandler(
        INucleusDbContext db,
        ICurrentTenantService tenant,
        IEmailService emailService)
    {
        _db = db;
        _tenant = tenant;
        _emailService = emailService;
    }

    public async Task<SendEmailCampaignResult> Handle(
        SendEmailCampaignCommand request, CancellationToken cancellationToken)
    {
        var campaign = await _db.EmailCampaigns
            .Where(c => c.Id == request.CampaignId
                     && c.BrandId == request.BrandId
                     && c.TenantId == _tenant.TenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Campaign not found for this tenant/brand.");

        if (campaign.Status == "sent")
            throw new InvalidOperationException("Campaign has already been sent.");

        if (!_emailService.IsConfigured)
            throw new InvalidOperationException(
                "Email provider is not configured. Set SMTP_HOST, SMTP_USER, and SMTP_PASS.");

        campaign.Status = "sending";
        await _db.SaveChangesAsync(cancellationToken);

        var sentCount = 0;
        var failedCount = 0;
        string? lastError = null;

        foreach (var recipient in request.Recipients)
        {
            try
            {
                await _emailService.SendAsync(recipient, campaign.Subject, campaign.HtmlBody, cancellationToken);
                sentCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                lastError = ex.Message;
            }
        }

        campaign.Status = failedCount == request.Recipients.Count ? "failed"
            : failedCount > 0 ? "sent"   // partial — still mark as sent
            : "sent";
        campaign.RecipientCount = sentCount;
        campaign.SentAt = DateTimeOffset.UtcNow;
        campaign.ErrorMessage = failedCount > 0 ? $"{failedCount}/{request.Recipients.Count} deliveries failed. Last error: {lastError}" : null;

        // Create the EmailCampaignMessage record for this send
        var message = new EmailCampaignMessage
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            CampaignId = campaign.Id,
            Subject = campaign.Subject,
            HtmlBody = campaign.HtmlBody,
            SentAt = campaign.SentAt,
            RecipientCount = sentCount,
            Status = campaign.Status,
            ErrorMessage = campaign.ErrorMessage,
        };
        _db.EmailCampaignMessages.Add(message);

        // Append send log
        _db.SendLogs.Add(new SendLog
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            CampaignId = campaign.Id,
            Channel = "email",
            RecipientCount = sentCount,
            SentAt = campaign.SentAt!.Value,
            Provider = "smtp",
            Status = failedCount == request.Recipients.Count ? "failed"
                : failedCount > 0 ? "partial"
                : "sent",
            ErrorMessage = campaign.ErrorMessage,
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new SendEmailCampaignResult(
            Success: failedCount < request.Recipients.Count,
            SentCount: sentCount,
            FailedCount: failedCount,
            ErrorMessage: lastError);
    }
}

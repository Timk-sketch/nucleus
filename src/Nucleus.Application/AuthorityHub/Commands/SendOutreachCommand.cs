using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;

namespace Nucleus.Application.AuthorityHub.Commands;

/// <summary>
/// Sends an outreach email for a queue item via IEmailService and marks the item as "emailed".
/// Returns true on success.
/// </summary>
public record SendOutreachCommand(
    Guid OutreachItemId,
    string Subject,
    string HtmlBody) : IRequest<SendOutreachResult>;

public record SendOutreachResult(bool Success, string? ErrorMessage = null);

public class SendOutreachValidator : AbstractValidator<SendOutreachCommand>
{
    public SendOutreachValidator()
    {
        RuleFor(x => x.OutreachItemId).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(300);
        RuleFor(x => x.HtmlBody).NotEmpty();
    }
}

public class SendOutreachHandler : IRequestHandler<SendOutreachCommand, SendOutreachResult>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;
    private readonly IEmailService _email;

    public SendOutreachHandler(INucleusDbContext db, ICurrentTenantService tenant, IEmailService email)
    {
        _db = db;
        _tenant = tenant;
        _email = email;
    }

    public async Task<SendOutreachResult> Handle(
        SendOutreachCommand request, CancellationToken cancellationToken)
    {
        // Global query filter scopes to current tenant automatically
        var item = await _db.OutreachQueueItems
            .FirstOrDefaultAsync(o => o.Id == request.OutreachItemId, cancellationToken);

        if (item is null)
            return new SendOutreachResult(false, "Outreach item not found.");

        if (string.IsNullOrWhiteSpace(item.ContactEmail))
            return new SendOutreachResult(false, "Outreach item has no contact email.");

        if (item.Status == "emailed")
            return new SendOutreachResult(false, "Outreach already sent to this contact.");

        try
        {
            await _email.SendAsync(
                toEmail: item.ContactEmail,
                subject: request.Subject,
                htmlBody: request.HtmlBody,
                ct: cancellationToken);

            item.Status = "emailed";
            item.OutreachAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            return new SendOutreachResult(true);
        }
        catch (Exception ex)
        {
            return new SendOutreachResult(false, ex.Message);
        }
    }
}

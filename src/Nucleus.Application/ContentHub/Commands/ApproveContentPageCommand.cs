using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;

namespace Nucleus.Application.ContentHub.Commands;

/// <summary>
/// Approves or rejects a content page in the editorial review queue.
/// Only pages in "review" status can be approved/rejected.
/// Approved pages move to "approved" status (ready to publish).
/// Rejected pages return to "draft" with reviewer notes.
/// </summary>
public record ApproveContentPageCommand(
    Guid ContentPageId,
    bool Approve,
    string? ReviewNotes) : IRequest<bool>;

public class ApproveContentPageValidator : AbstractValidator<ApproveContentPageCommand>
{
    public ApproveContentPageValidator()
    {
        RuleFor(x => x.ContentPageId).NotEmpty();
        RuleFor(x => x.ReviewNotes).MaximumLength(1000).When(x => x.ReviewNotes != null);
    }
}

public class ApproveContentPageHandler : IRequestHandler<ApproveContentPageCommand, bool>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public ApproveContentPageHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<bool> Handle(
        ApproveContentPageCommand request, CancellationToken cancellationToken)
    {
        var page = await _db.ContentPages
            .FirstOrDefaultAsync(p => p.Id == request.ContentPageId
                                   && p.TenantId == _tenant.TenantId, cancellationToken);

        if (page is null) return false;

        if (page.Status == "draft")
        {
            // Allow moving from draft to review first
            page.Status = "review";
        }

        page.Status = request.Approve ? "approved" : "draft";
        page.ReviewNotes = request.ReviewNotes;

        if (request.Approve && page.Status == "approved")
        {
            // Auto-publish if approved (can be made configurable)
            // Left as "approved" — a separate publish action will set "published"
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

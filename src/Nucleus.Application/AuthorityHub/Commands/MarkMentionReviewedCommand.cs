using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;

namespace Nucleus.Application.AuthorityHub.Commands;

/// <summary>
/// Marks a brand mention as reviewed (or unreviewed if toggled).
/// Returns true if the mention was found and updated; false if not found.
/// </summary>
public record MarkMentionReviewedCommand(
    Guid MentionId,
    bool IsReviewed = true) : IRequest<bool>;

public class MarkMentionReviewedValidator : AbstractValidator<MarkMentionReviewedCommand>
{
    public MarkMentionReviewedValidator()
    {
        RuleFor(x => x.MentionId).NotEmpty();
    }
}

public class MarkMentionReviewedHandler : IRequestHandler<MarkMentionReviewedCommand, bool>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public MarkMentionReviewedHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<bool> Handle(
        MarkMentionReviewedCommand request, CancellationToken cancellationToken)
    {
        // Global query filter already scopes to current tenant
        var mention = await _db.BrandMentions
            .FirstOrDefaultAsync(m => m.Id == request.MentionId, cancellationToken);

        if (mention is null) return false;

        mention.IsReviewed = request.IsReviewed;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

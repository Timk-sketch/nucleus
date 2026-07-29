using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;

namespace Nucleus.Application.ContentHub.Commands;

/// <summary>
/// Removes a banned word from the Brand Voice configuration.
/// Scoped to current tenant — cannot delete words belonging to other tenants.
/// </summary>
public record RemoveBannedWordCommand(Guid BannedWordId) : IRequest<bool>;

public class RemoveBannedWordHandler : IRequestHandler<RemoveBannedWordCommand, bool>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public RemoveBannedWordHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<bool> Handle(RemoveBannedWordCommand request, CancellationToken cancellationToken)
    {
        var word = await _db.BannedWords
            .FirstOrDefaultAsync(
                w => w.Id == request.BannedWordId && w.TenantId == _tenant.TenantId,
                cancellationToken);

        if (word is null) return false;

        _db.BannedWords.Remove(word);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

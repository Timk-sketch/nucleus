using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.FinderHub.DTOs;

namespace Nucleus.Application.FinderHub.Queries;

/// <summary>
/// Retrieves a FinderSession by EmbedToken + SessionToken.
/// No authentication required — used by the embed widget to resume sessions.
/// Returns null if not found.
/// </summary>
public record GetFinderSessionQuery(
    string EmbedToken,
    string SessionToken) : IRequest<FinderSessionDto?>;

public class GetFinderSessionHandler : IRequestHandler<GetFinderSessionQuery, FinderSessionDto?>
{
    private readonly INucleusDbContext _db;

    public GetFinderSessionHandler(INucleusDbContext db)
    {
        _db = db;
    }

    public async Task<FinderSessionDto?> Handle(
        GetFinderSessionQuery request, CancellationToken cancellationToken)
    {
        // Resolve finder globally by embed token
        var finder = await _db.Finders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                f => f.EmbedToken == request.EmbedToken,
                cancellationToken);

        if (finder is null)
            return null;

        var session = await _db.FinderSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                s => s.SessionToken == request.SessionToken && s.FinderId == finder.Id,
                cancellationToken);

        if (session is null)
            return null;

        return new FinderSessionDto
        {
            Id = session.Id,
            FinderId = session.FinderId,
            SessionToken = session.SessionToken,
            AnswersJson = session.AnswersJson,
            ResultKey = session.ResultKey,
            Converted = session.Converted,
            CompletedAt = session.CompletedAt,
            CreatedAt = session.CreatedAt,
        };
    }
}

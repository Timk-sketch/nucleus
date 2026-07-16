using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;
using System.Text.Json;

namespace Nucleus.Application.Search.Commands;

/// <summary>
/// Creates a topic cluster grouping a pillar keyword with supporting cluster keywords.
/// </summary>
public record CreateTopicClusterCommand(
    Guid BrandId,
    string Name,
    string PillarKeyword,
    List<string> ClusterKeywords,
    string? Notes = null) : IRequest<Guid>;

public class CreateTopicClusterValidator : AbstractValidator<CreateTopicClusterCommand>
{
    public CreateTopicClusterValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PillarKeyword).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ClusterKeywords)
            .NotNull()
            .Must(kws => kws.Count <= 50)
            .WithMessage("Maximum 50 cluster keywords per cluster.");
    }
}

public class CreateTopicClusterHandler : IRequestHandler<CreateTopicClusterCommand, Guid>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public CreateTopicClusterHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(CreateTopicClusterCommand request, CancellationToken cancellationToken)
    {
        // Verify brand belongs to tenant
        var brandExists = await _db.Brands
            .AnyAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId, cancellationToken);

        if (!brandExists)
            throw new InvalidOperationException("Brand not found.");

        var cluster = new TopicCluster
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            Name = request.Name,
            PillarKeyword = request.PillarKeyword,
            ClusterKeywordsJson = JsonSerializer.Serialize(request.ClusterKeywords),
            Status = "planning",
            Notes = request.Notes,
        };

        _db.TopicClusters.Add(cluster);
        await _db.SaveChangesAsync(cancellationToken);

        return cluster.Id;
    }
}

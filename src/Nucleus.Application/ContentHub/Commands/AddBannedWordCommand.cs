using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.ContentHub.DTOs;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.ContentHub.Commands;

/// <summary>
/// Adds a banned word/phrase to the Brand Voice configuration.
/// The AI generator and content review process will flag uses of this word.
/// Prevents duplicate entries per brand.
/// </summary>
public record AddBannedWordCommand(
    Guid BrandId,
    string Word,
    string? Reason) : IRequest<BannedWordDto>;

public class AddBannedWordValidator : AbstractValidator<AddBannedWordCommand>
{
    public AddBannedWordValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.Word).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Reason).MaximumLength(500).When(x => x.Reason != null);
    }
}

public class AddBannedWordHandler : IRequestHandler<AddBannedWordCommand, BannedWordDto>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public AddBannedWordHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<BannedWordDto> Handle(
        AddBannedWordCommand request, CancellationToken cancellationToken)
    {
        var brandExists = await _db.Brands
            .AnyAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId, cancellationToken);

        if (!brandExists)
            throw new InvalidOperationException("Brand not found for this tenant.");

        var normalised = request.Word.Trim().ToLowerInvariant();

        // Prevent duplicates per brand
        var exists = await _db.BannedWords
            .AnyAsync(w => w.BrandId == request.BrandId && w.Word == normalised, cancellationToken);

        if (exists)
            throw new InvalidOperationException($"The word '{normalised}' is already banned for this brand.");

        var banned = new BannedWord
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            Word = normalised,
            Reason = request.Reason?.Trim(),
        };

        _db.BannedWords.Add(banned);
        await _db.SaveChangesAsync(cancellationToken);

        return new BannedWordDto
        {
            Id = banned.Id,
            BrandId = banned.BrandId,
            Word = banned.Word,
            Reason = banned.Reason,
            CreatedAt = banned.CreatedAt,
        };
    }
}

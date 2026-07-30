using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;
using Nucleus.Infrastructure.Data;
using Xunit;

namespace Nucleus.Architecture.Tests;

/// <summary>
/// Tenant-isolation gate — the machine-checkable half of the Nucleus Constitution's
/// multi-tenant invariants (docs/architecture/nucleus-constitution.md, §3).
/// A red test here means a change weakened tenant isolation. Do not merge until green.
///
/// Skipped tests are executable specs for known gaps — each names its backlog item.
/// Un-skip it when that item ships.
/// </summary>
public class TenantIsolationTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static NucleusDbContext BuildContext(Guid tenantId, string dbName)
    {
        var tenant = Substitute.For<ICurrentTenantService>();
        tenant.TenantId.Returns(tenantId);

        var options = new DbContextOptionsBuilder<NucleusDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new NucleusDbContext(options, tenant);
    }

    // Invariant 4a — every TenantEntity-derived type carries a global query filter.
    // PASSES today. Locks in the reflection loop so a new entity can't ship unfiltered.
    [Fact]
    public void Every_tenant_entity_has_a_global_query_filter()
    {
        using var ctx = BuildContext(TenantA, nameof(Every_tenant_entity_has_a_global_query_filter));

        var offenders = ctx.Model.GetEntityTypes()
            .Where(e => typeof(TenantEntity).IsAssignableFrom(e.ClrType))
            .Where(e => e.GetQueryFilter() is null)
            .Select(e => e.ClrType.Name)
            .ToArray();

        offenders.Should().BeEmpty(
            "every TenantEntity needs a global tenant query filter or it can leak cross-tenant data");
    }

    // Invariant 4b — the filter must be evaluated PER REQUEST, not frozen to a constant.
    // PASSES as of NUC-ISO-4: OnModelCreating now builds the filter against the DbContext
    // instance property CurrentTenantId instead of baking Expression.Constant(Guid), so EF
    // rewrites it into a query parameter re-read from the executing context. A regression
    // that reintroduces a captured Guid turns this red again — which is the point.
    [Fact]
    public void Tenant_query_filter_is_evaluated_dynamically_not_frozen()
    {
        using var ctx = BuildContext(TenantA, nameof(Tenant_query_filter_is_evaluated_dynamically_not_frozen));

        var filter = ctx.Model.GetEntityTypes()
            .Where(e => typeof(TenantEntity).IsAssignableFrom(e.ClrType))
            .Select(e => e.GetQueryFilter())
            .First(f => f is not null)!;

        var finder = new ConstantGuidFinder();
        finder.Visit(filter);

        finder.Found.Should().BeFalse(
            "a hard-coded Guid in the query filter means it is frozen to one tenant for the whole process; " +
            "it must reference the current tenant so EF re-evaluates it on every query");
    }

    // Invariant 4b, behavioural half — added with NUC-ISO-4.
    //
    // The test above inspects the shape of the expression; this one proves the effect. Both are
    // needed: a filter can be shaped correctly and still resolve once, if EF fails to recognise the
    // reference as the context. It also exercises the exact mechanism that caused the bug — EF
    // builds the model once per context type and shares it across every context in this assembly,
    // so the second and third contexts here are deliberately reading through a model they did not
    // build. Under the old frozen filter this fails.
    [Fact]
    public async Task Each_context_sees_only_its_own_tenants_rows_through_the_shared_model()
    {
        const string db = nameof(Each_context_sees_only_its_own_tenants_rows_through_the_shared_model);

        using (var seed = BuildContext(TenantA, db))
        {
            seed.Brands.Add(new Brand { TenantId = TenantA, Code = "a", Name = "Tenant A brand" });
            seed.Brands.Add(new Brand { TenantId = TenantB, Code = "b", Name = "Tenant B brand" });
            await seed.SaveChangesAsync();
        }

        using (var asA = BuildContext(TenantA, db))
        {
            var codes = await asA.Brands.Select(b => b.Code).ToListAsync();
            codes.Should().BeEquivalentTo(new[] { "a" }, "tenant A must never see tenant B's rows");
        }

        using (var asB = BuildContext(TenantB, db))
        {
            var codes = await asB.Brands.Select(b => b.Code).ToListAsync();
            codes.Should().BeEquivalentTo(new[] { "b" },
                "the filter must re-evaluate per context; a frozen one would still be serving tenant A");
        }

        using (var unauthenticated = BuildContext(Guid.Empty, db))
        {
            var leaked = await unauthenticated.Brands.AnyAsync();
            leaked.Should().BeFalse(
                "a context with no tenant must match no rows — failing closed, never open");
        }
    }

    // Invariant 3 — TenantId is stamped automatically on insert.
    // FAILS today: SaveChangesAsync only updates UpdatedAt. Definition of done for NUC-ISO-2.
    [Fact(Skip = "NUC-ISO-2: auto-stamp TenantId on Added entities in NucleusDbContext.SaveChangesAsync; un-skip when done.")]
    public async Task SaveChanges_stamps_TenantId_on_new_entities()
    {
        using var ctx = BuildContext(TenantA, nameof(SaveChanges_stamps_TenantId_on_new_entities));

        var concreteType = ctx.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .First(t => typeof(TenantEntity).IsAssignableFrom(t)
                        && !t.IsAbstract
                        && t.GetConstructor(Type.EmptyTypes) is not null);

        var entity = (TenantEntity)Activator.CreateInstance(concreteType)!;
        ctx.Add(entity);                       // deliberately do NOT set entity.TenantId
        await ctx.SaveChangesAsync();

        entity.TenantId.Should().Be(TenantA);
    }

    private sealed class ConstantGuidFinder : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is Guid) Found = true;
            return base.VisitConstant(node);
        }
    }
}

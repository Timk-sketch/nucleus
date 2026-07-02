# Work Order — NUC-ISO-4: Make the tenant query filter dynamic (per request)

**Type:** bug / security-critical &nbsp;·&nbsp; **Priority:** HIGHEST &nbsp;·&nbsp; **Constitution:** §3 invariant 4
**Depends on:** tenant-isolation gate merged &nbsp;·&nbsp; **Unblocks:** un-skip `Tenant_query_filter_is_evaluated_dynamically_not_frozen`

## The bug
`NucleusDbContext.OnModelCreating` builds the global tenant filter from a captured constant:

```csharp
var tenantId = tenantService.TenantId;                 // read ONCE, at model-build time
...
Expression.Equal(prop, Expression.Constant(tenantId)); // baked in as a literal
```

EF Core builds the model **once** and caches it for the whole process — there is **no
`IModelCacheKeyFactory`** registered (`Program.cs` uses the plain `AddDbContext`). So the
filter is frozen to whatever `tenantService.TenantId` was at first model build. In practice
the model is first built by the startup health check (`AddDbContextCheck<NucleusDbContext>`
in `Program.cs`), which has no HTTP user → `TenantId = Guid.Empty`.

**Effect:** every tenant's queries are filtered `WHERE tenant_id = <first value>` for the life
of the process — either all tenants see nothing (`Guid.Empty`) or one tenant's rows leak to
everyone. **App-layer tenant isolation does not currently work.** Latent only because Nucleus
has no live multi-tenant traffic yet; must be fixed before any tenant data flows.

## The fix (direction — prove it with the tests, don't over-prescribe the exact expression)
Make the filter reference the **current** tenant so EF re-evaluates it per query instead of
baking a constant. EF Core parameterizes query filters that reference a DbContext **instance
member** and re-evaluates them on every query.

- Expose the current tenant as an instance member on the context (e.g. a private
  `Guid CurrentTenantId => tenantService.TenantId;`) and build each entity's filter to compare
  `TenantId` against **that member**, not a captured local.
- The compiled filter must contain **no `Guid` constant** — that is exactly what the gate test
  `Tenant_query_filter_is_evaluated_dynamically_not_frozen` checks.
- Do **not** add a per-tenant `IModelCacheKeyFactory` (rebuilds the whole model per tenant —
  expensive and unnecessary). Referencing an instance member is the correct, cheap fix.
- `Guid.Empty` (no tenant) must yield **zero** rows — deny-by-default. Confirm it does.
- `Tenant` (the tenant root) is **not** a `TenantEntity` — do not filter it.

## Acceptance criteria (this IS the outcome rubric)
- [ ] `Tenant_query_filter_is_evaluated_dynamically_not_frozen` un-skipped and passing.
- [ ] `Every_tenant_entity_has_a_global_query_filter` still passing.
- [ ] Integration test: in a single process, run queries under **two different tenants** and
      confirm each sees only its own rows (proves the filter is not frozen to the first).
- [ ] With `TenantId = Guid.Empty`, tenant queries return **zero** rows.
- [ ] No change to `Tenant`-root query behaviour.
- [ ] Constitution §3 invariant 4 status updated to ✅; changelog entry added.

## Out of scope
NUC-ISO-1 (RLS backstop), NUC-ISO-2 (auto-stamp), NUC-ISO-3 (dev secrets). Separate work orders.

## Why this is first
A live isolation break, small, and fixing it makes the app actually isolate tenants at the app
layer. RLS (NUC-ISO-1) then adds the independent database backstop.

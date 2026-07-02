# Work Order — NUC-ISO-1: Database-layer tenant isolation (RLS)

**Type:** infrastructure / security &nbsp;·&nbsp; **Priority:** HIGH &nbsp;·&nbsp; **Constitution:** §3 invariant 2
**Depends on:** tenant-isolation gate merged; NUC-ISO-4 (dynamic filter) recommended first
**Unblocks:** flipping the `gate.sh` RLS check from advisory to blocking

## Why
Nucleus enforces tenant isolation only in the app layer (the EF global query filter — and per
NUC-ISO-4 that filter is currently broken). Any path that bypasses EF (raw SQL, a Hangfire job
on its own connection, a mis-scoped admin tool, or a future worker's mistake) reads across
tenants with **no backstop**. This adds the database-enforced second boundary the Constitution
requires — which holds even if the app filter is wrong.

Tenant tables (backing `TenantEntity` subclasses): `brands`, `brand_keywords`, `ghl_contacts`,
`keyword_ranks`, `email_campaigns`, `brand_provisioning_steps`, `audit_logs`. (`tenants` and the
Identity tables are not `TenantEntity` — scope them per their own ownership rules, not this policy.)

## The approach — DECIDED, do not improvise
These are direct Postgres/EF connections, so RLS **cannot** use Supabase `auth.jwt()` (that only
exists on PostgREST/GoTrue connections). Use the **session-variable** pattern:

1. The app sets a per-connection Postgres variable `app.current_tenant` from `ICurrentTenantService`.
2. Each tenant table has an RLS policy: rows visible/writable only when
   `tenant_id = current_setting('app.current_tenant', true)::uuid`.
3. Tables use **both** `ENABLE` and `FORCE ROW LEVEL SECURITY`.

### Non-obvious correctness — get any of these wrong and RLS silently does nothing
- **FORCE is required.** `ENABLE` alone does not apply RLS to the table owner; EF migrations run
  as owner. `FORCE ROW LEVEL SECURITY` closes that.
- **The runtime role must not be `BYPASSRLS`/superuser.** Confirm which role the app connects as
  (`Program.cs` builds the Npgsql connection string) and document it. If it's a superuser/BYPASSRLS
  role, RLS is ignored entirely — STOP and raise this.
- **Connection pooling leaks session state.** Set `app.current_tenant` on **every connection open**
  (a `DbConnectionInterceptor`), never once per session. Prove no leakage with a test.
- **Unset tenant must deny, not error.** `current_setting('app.current_tenant', true)` (missing_ok)
  → NULL → zero rows. Deny-by-default also protects Hangfire jobs that forget context.
- **`WITH CHECK` for writes** so a row can't be inserted/updated into another tenant.

## Implementation
1. **Connection interceptor** (`Nucleus.Infrastructure`): a `DbConnectionInterceptor` whose
   `ConnectionOpenedAsync` (+ sync) runs `SELECT set_config('app.current_tenant', @tenant, false)`
   from `ICurrentTenantService.TenantId`. Register it on the `AddDbContext` options in `Program.cs`.
2. **Migration** (raw `migrationBuilder.Sql`) for each tenant table:
   ```sql
   ALTER TABLE <t> ENABLE ROW LEVEL SECURITY;
   ALTER TABLE <t> FORCE  ROW LEVEL SECURITY;
   CREATE POLICY tenant_isolation ON <t>
     USING      (tenant_id = current_setting('app.current_tenant', true)::uuid)
     WITH CHECK (tenant_id = current_setting('app.current_tenant', true)::uuid);
   ```
   Provide a working `Down`. Guard idempotently.
3. **Background / admin paths:** any Hangfire job or maintenance path touching tenant data must set
   `app.current_tenant`, or route through an explicitly `// tenant-gate:allow`-annotated admin flow.
4. **Flip the gate:** set `WARN_ONLY=0` in `scripts/tenant-gate/gate.sh`.
5. **Docs:** Constitution §3 invariant 2 → ✅; changelog entry.

## Acceptance criteria (outcome rubric)
- [ ] Integration test (real Postgres): seed tenants A and B; with `app.current_tenant = A`, a query
      — **including raw SQL and one using `IgnoreQueryFilters()`** — returns only A's rows.
- [ ] With no tenant set, tenant-table queries return **zero** rows.
- [ ] Insert/update with `tenant_id` ≠ current tenant is rejected (`WITH CHECK`).
- [ ] No session-variable leakage across pooled connections.
- [ ] Every `TenantEntity` table has ENABLE + FORCE + policy (assert via `pg_policies` /
      `pg_class.relforcerowsecurity`).
- [ ] `gate.sh` RLS check flipped to blocking and passing.
- [ ] Migration `Down` cleanly reverses; Constitution updated; changelog added.

## Out of scope
NUC-ISO-4, NUC-ISO-2, NUC-ISO-3. Separate work orders.

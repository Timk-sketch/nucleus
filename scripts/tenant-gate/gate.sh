#!/usr/bin/env bash
# Tenant-isolation gate — static guards that complement Nucleus.Architecture.Tests.
# Runs in CI on every PR; a non-zero exit blocks the merge.
# See docs/architecture/nucleus-constitution.md, section 3.
set -uo pipefail
fail=0

echo "== 1. No secrets in committed appsettings (invariant 1 — advisory until NUC-ISO-3) =="
# Server appsettings must not carry connection-string passwords, signing keys, or live
# Stripe keys. wwwroot/appsettings*.json is client config — still no secrets.
# Advisory (warn, don't fail) until the pre-existing dev creds are moved out (NUC-ISO-3);
# then set SECRETS_WARN_ONLY=0 to make it blocking.
SECRETS_WARN_ONLY=1
if grep -RInE 'Password=[^";[:space:]]+|sk_live_[0-9A-Za-z]+|sk_test_[0-9A-Za-z]+|"(Jwt)?(Signing)?Key"[[:space:]]*:[[:space:]]*"[^"]{16,}"' \
     --include='appsettings*.json' src/ ; then
  if [ "$SECRETS_WARN_ONLY" -eq 1 ]; then
    echo "  !  (advisory) secret-like values in committed appsettings — dev creds pending NUC-ISO-3."
  else
    echo "  x  Possible secret in committed appsettings. Move it to user-secrets / env."
    fail=1
  fi
else
  echo "  ok clean"
fi

echo "== 2. Query-filter bypasses annotated (invariant 4 — advisory until NUC-ISO-5) =="
# IgnoreQueryFilters() removes tenant isolation. A legitimate use must be tagged with
#   // tenant-gate:allow <reason>   on the same line.
# Advisory until the existing reviewed-legitimate bypasses are annotated (NUC-ISO-5);
# then set BYPASS_WARN_ONLY=0 to block any NEW un-annotated bypass.
BYPASS_WARN_ONLY=1
if grep -RIn --include='*.cs' 'IgnoreQueryFilters' src/ | grep -v 'tenant-gate:allow' ; then
  if [ "$BYPASS_WARN_ONLY" -eq 1 ]; then
    echo "  !  (advisory) un-annotated IgnoreQueryFilters() present — annotate reviewed bypasses (NUC-ISO-5)."
  else
    echo "  x  Un-annotated IgnoreQueryFilters() found — bypasses tenant isolation."
    fail=1
  fi
else
  echo "  ok none (or all annotated)"
fi

echo "== 3. RLS per tenant table in migrations (invariant 2 — coarse advisory; see note) =="
# COARSE TRIPWIRE ONLY. Greps migrations for an RLS statement mentioning each expected tenant
# table. A substring can't prove FORCE or a correct policy, so this stays ADVISORY — the
# AUTHORITATIVE per-table check (ENABLE + FORCE + policy) is the NUC-ISO-1 integration test
# against pg_policies / pg_class.relforcerowsecurity. Do not treat this grep as the gate.
TENANT_TABLES="brands brand_keywords ghl_contacts keyword_ranks email_campaigns brand_provisioning_steps audit_logs"
rls_files=$(grep -RIlE 'ENABLE ROW LEVEL SECURITY' src/Nucleus.Infrastructure/Migrations 2>/dev/null)
missing=""
for t in $TENANT_TABLES; do
  if [ -z "$rls_files" ] || ! printf '%s\n' "$rls_files" | xargs -r grep -lE "\\b$t\\b" >/dev/null 2>&1 ; then
    missing="$missing $t"
  fi
done
if [ -n "$missing" ]; then
  echo "  !  (advisory) tenant tables with no RLS statement in migrations:$missing (NUC-ISO-1)."
else
  echo "  ok every tenant table is referenced by an RLS migration"
fi

exit $fail

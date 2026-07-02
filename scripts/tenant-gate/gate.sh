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

echo "== 3. RLS present in migrations (Constitution invariant 2 — advisory until NUC-ISO-1) =="
# Every tenant table should get RLS enabled in a migration. Advisory (warn, don't fail)
# until RLS ships; then set WARN_ONLY=0 to make it blocking.
WARN_ONLY=1
if ! grep -RIlE 'ENABLE ROW LEVEL SECURITY' src/ >/dev/null 2>&1 ; then
  if [ "$WARN_ONLY" -eq 1 ]; then
    echo "  !  (advisory) No 'ENABLE ROW LEVEL SECURITY' in any migration — DB-layer isolation missing (NUC-ISO-1)."
  else
    echo "  x  No RLS found in migrations — DB-layer tenant isolation is missing."
    fail=1
  fi
else
  echo "  ok found"
fi

exit $fail

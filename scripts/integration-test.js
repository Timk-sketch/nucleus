#!/usr/bin/env node
/**
 * Nucleus Integration Tests
 * Tests real API endpoints against staging. Runs on every push to master.
 *
 * Secrets required:
 *   NUCLEUS_STAGING_URL        — e.g. https://nucleus-staging-0a33.up.railway.app
 *   NUCLEUS_TEST_EMAIL         — a dedicated test account email
 *   NUCLEUS_TEST_PASSWORD      — its password
 *   SLACK_NUCLEUS_WEBHOOK      — optional Slack notification
 */

const BASE = process.env.NUCLEUS_STAGING_URL?.replace(/\/$/, '');
const TEST_EMAIL = process.env.NUCLEUS_TEST_EMAIL;
const TEST_PASSWORD = process.env.NUCLEUS_TEST_PASSWORD;
const SLACK_WEBHOOK = process.env.SLACK_NUCLEUS_WEBHOOK;

if (!BASE) { console.error('NUCLEUS_STAGING_URL not set'); process.exit(1); }

const results = [];
let accessToken = null;

async function api(method, path, body, useAuth = false) {
  const headers = { 'Content-Type': 'application/json' };
  if (useAuth && accessToken) headers['Authorization'] = `Bearer ${accessToken}`;
  const res = await fetch(`${BASE}${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
    signal: AbortSignal.timeout(10000),
  });
  let json;
  try { json = await res.json(); } catch { json = {}; }
  return { status: res.status, json };
}

async function test(name, fn) {
  try {
    const result = await fn();
    const pass = result !== false;
    results.push({ name, pass, error: null });
    console.log(`${pass ? '✅' : '❌'} ${name}`);
  } catch (err) {
    results.push({ name, pass: false, error: err.message });
    console.log(`❌ ${name} — ${err.message}`);
  }
}

async function main() {
  console.log(`\nNucleus Integration Tests — ${BASE}`);
  console.log(`Time: ${new Date().toISOString()}\n`);

  // ── Health ─────────────────────────────────────────────────────────
  await test('GET /health returns 200', async () => {
    const { status, json } = await api('GET', '/health');
    if (status !== 200) throw new Error(`HTTP ${status}`);
    if (json.status !== 'ok') throw new Error(`status=${json.status}`);
    console.log(`    db: ${json.db}`);
  });

  await test('GET /health db is connected', async () => {
    const { json } = await api('GET', '/health');
    if (json.db !== 'connected') throw new Error(`db=${json.db} (check NUCLEUS_DB_CONNECTION in Railway)`);
  });

  // ── Auth — unauthenticated rejections ──────────────────────────────
  await test('GET /api/v1/auth/me returns 401 without token', async () => {
    const { status } = await api('GET', '/api/v1/auth/me');
    if (status !== 401) throw new Error(`Expected 401, got ${status}`);
  });

  await test('GET /api/v1/brands returns 401 without token', async () => {
    const { status } = await api('GET', '/api/v1/brands');
    if (status !== 401) throw new Error(`Expected 401, got ${status}`);
  });

  await test('GET /api/v1/billing/status returns 401 without token', async () => {
    const { status } = await api('GET', '/api/v1/billing/status');
    if (status !== 401) throw new Error(`Expected 401, got ${status}`);
  });

  // ── Auth — login ───────────────────────────────────────────────────
  if (TEST_EMAIL && TEST_PASSWORD) {
    await test('POST /api/v1/auth/login with valid credentials', async () => {
      const { status, json } = await api('POST', '/api/v1/auth/login', { email: TEST_EMAIL, password: TEST_PASSWORD });
      if (status !== 200) throw new Error(`HTTP ${status} — ${json.message ?? JSON.stringify(json)}`);
      if (!json.data?.accessToken) throw new Error('No accessToken in response');
      accessToken = json.data.accessToken;
    });

    if (accessToken) {
      await test('GET /api/v1/auth/me returns user after login', async () => {
        const { status, json } = await api('GET', '/api/v1/auth/me', null, true);
        if (status !== 200) throw new Error(`HTTP ${status}`);
        if (!json.data?.email) throw new Error('No email in /me response');
        console.log(`    user: ${json.data.email} (${json.data.role})`);
      });

      await test('GET /api/v1/brands returns brand list', async () => {
        const { status, json } = await api('GET', '/api/v1/brands', null, true);
        if (status !== 200) throw new Error(`HTTP ${status}`);
        console.log(`    brands: ${json.data?.length ?? 0}`);
      });

      await test('GET /api/v1/billing/status returns subscription info', async () => {
        const { status, json } = await api('GET', '/api/v1/billing/status', null, true);
        if (status !== 200) throw new Error(`HTTP ${status}`);
        console.log(`    plan status: ${json.data?.subscriptionStatus ?? 'null'}`);
      });

      await test('POST /api/v1/auth/refresh rotates tokens', async () => {
        const loginRes = await api('POST', '/api/v1/auth/login', { email: TEST_EMAIL, password: TEST_PASSWORD });
        const refreshToken = loginRes.json.data?.refreshToken;
        if (!refreshToken) throw new Error('No refreshToken from login');
        const { status, json } = await api('POST', '/api/v1/auth/refresh', { refreshToken });
        if (status !== 200) throw new Error(`HTTP ${status}`);
        if (!json.data?.accessToken) throw new Error('No new accessToken from refresh');
      });
    }
  } else {
    console.log('⚠️  Skipping auth tests — NUCLEUS_TEST_EMAIL / NUCLEUS_TEST_PASSWORD not set');
  }

  // ── Public finder endpoint ─────────────────────────────────────────
  await test('GET /api/finder/nonexistent returns 404', async () => {
    const { status } = await api('GET', '/api/finder/nonexistent-embed-token');
    if (status !== 404) throw new Error(`Expected 404, got ${status}`);
  });

  // ── Summary ────────────────────────────────────────────────────────
  const passed = results.filter(r => r.pass).length;
  const failed = results.filter(r => !r.pass);
  console.log(`\n${passed}/${results.length} tests passed`);

  if (failed.length > 0) {
    console.log('\nFailed:');
    failed.forEach(f => console.log(`  • ${f.name}${f.error ? ': ' + f.error : ''}`));

    if (SLACK_WEBHOOK) {
      const msg = `🔴 *Nucleus Integration Tests — ${failed.length} FAILED* (${passed}/${results.length} passed)\n` +
        failed.map(f => `• ${f.name}${f.error ? ': ' + f.error : ''}`).join('\n') +
        `\nBranch: ${process.env.GITHUB_REF ?? 'unknown'} · ${new Date().toISOString()}`;
      await fetch(SLACK_WEBHOOK, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ text: msg }),
      }).catch(() => {});
    }
    process.exit(1);
  } else {
    if (SLACK_WEBHOOK && process.env.GITHUB_EVENT_NAME === 'schedule') {
      await fetch(SLACK_WEBHOOK, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ text: `✅ Nucleus Integration Tests — all ${passed} passed` }),
      }).catch(() => {});
    }
  }
}

main().catch(err => {
  console.error('Test runner crashed:', err);
  process.exit(1);
});

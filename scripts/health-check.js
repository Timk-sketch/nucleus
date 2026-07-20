#!/usr/bin/env node
/**
 * Nucleus Staging Health Check
 * Runs every 30 min via GitHub Actions. Posts to Slack on failure.
 */

const BASE_URL = process.env.NUCLEUS_STAGING_URL?.replace(/\/$/, '');
const SLACK_WEBHOOK = process.env.SLACK_NUCLEUS_WEBHOOK;

if (!BASE_URL) {
  console.error('NUCLEUS_STAGING_URL not set');
  process.exit(1);
}

const checks = [
  { name: 'Health endpoint', path: '/health', expectStatus: 200, expectBody: s => s.includes('"status":"ok"') },
  { name: 'API reachable',   path: '/api/v1/auth/me', expectStatus: 401, expectBody: null }, // 401 = app is up, auth required
  { name: 'Billing status',  path: '/api/v1/billing/status', expectStatus: 401, expectBody: null },
];

async function runCheck({ name, path, expectStatus, expectBody }) {
  const url = `${BASE_URL}${path}`;
  try {
    const res = await fetch(url, { signal: AbortSignal.timeout(8000) });
    const body = await res.text();
    const statusOk = res.status === expectStatus;
    const bodyOk = expectBody ? expectBody(body) : true;
    const pass = statusOk && bodyOk;
    console.log(`${pass ? '✅' : '❌'} ${name} — HTTP ${res.status}${!statusOk ? ` (expected ${expectStatus})` : ''}${!bodyOk ? ' (body mismatch)' : ''}`);
    return { name, pass, status: res.status, body: body.slice(0, 200) };
  } catch (err) {
    console.log(`❌ ${name} — ERROR: ${err.message}`);
    return { name, pass: false, status: 0, body: err.message };
  }
}

async function postSlack(message) {
  if (!SLACK_WEBHOOK) return;
  await fetch(SLACK_WEBHOOK, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ text: message }),
  }).catch(() => {});
}

async function main() {
  console.log(`\nNucleus Health Check — ${BASE_URL}`);
  console.log(`Time: ${new Date().toISOString()}\n`);

  const results = await Promise.all(checks.map(runCheck));
  const failed = results.filter(r => !r.pass);

  console.log(`\n${results.length - failed.length}/${results.length} checks passed`);

  if (failed.length > 0) {
    const msg = `🚨 *Nucleus Staging — ${failed.length} health check(s) FAILED*\n` +
      failed.map(f => `• ${f.name}: HTTP ${f.status} — ${f.body}`).join('\n') +
      `\n_Time: ${new Date().toISOString()}_`;
    await postSlack(msg);
    process.exit(1);
  } else {
    console.log('All checks passed ✅');
  }
}

main().catch(err => {
  console.error('Health check crashed:', err);
  process.exit(1);
});

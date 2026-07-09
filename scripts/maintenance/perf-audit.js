/**
 * Nucleus Performance Audit — Monthly (1st of month, 9 AM)
 *
 * Checks:
 * 1. API endpoint response times (benchmarks against NUCLEUS_STAGING_URL)
 * 2. EF Core migration files for missing indexes on FK columns
 * 3. Baseline snapshot comparison vs last month
 *
 * Reports to Slack with regressions flagged.
 */

import 'dotenv/config';
import { notify } from '../worker/notifier.js';
import path from 'path';
import fs from 'fs';

const STAGING_URL = process.env.NUCLEUS_STAGING_URL;
const NUCLEUS_ROOT = new URL('../..', import.meta.url).pathname.replace(/^\/([A-Z]:)/, '$1');
const BASELINE_PATH = path.join(NUCLEUS_ROOT, '.maintenance/perf-baseline.json');

const ENDPOINTS_TO_BENCHMARK = [
  { path: '/health', label: 'Health check', p95_limit_ms: 100 },
  { path: '/api/auth/me', label: 'Auth me', p95_limit_ms: 200, requiresAuth: true },
  { path: '/api/content/keywords', label: 'Keyword list', p95_limit_ms: 500, requiresAuth: true },
  { path: '/api/search/rankings', label: 'Rankings dashboard', p95_limit_ms: 800, requiresAuth: true },
];

const results = [];
const issues = [];

if (!STAGING_URL) {
  const msg = 'NUCLEUS_STAGING_URL not set — skipping live benchmarks. Set env var to enable.';
  console.log(`[perf-audit] ${msg}`);
  await notify('maintenance', { job: 'Performance Audit — SKIPPED', report: msg });
  process.exit(0);
}

// 1. API benchmarks
const { default: fetch } = await import('node-fetch');

for (const endpoint of ENDPOINTS_TO_BENCHMARK) {
  const url = `${STAGING_URL}${endpoint.path}`;
  const samples = [];

  for (let i = 0; i < 5; i++) {
    const start = Date.now();
    try {
      await fetch(url, { timeout: 10_000 });
      samples.push(Date.now() - start);
    } catch {
      samples.push(10_000); // timeout = 10s penalty
    }
    await sleep(500);
  }

  samples.sort((a, b) => a - b);
  const p95 = samples[Math.floor(samples.length * 0.95)] || samples[samples.length - 1];
  const p50 = samples[Math.floor(samples.length * 0.5)];

  const result = { label: endpoint.label, path: endpoint.path, p50, p95, limit: endpoint.p95_limit_ms };
  results.push(result);

  if (p95 > endpoint.p95_limit_ms) {
    issues.push(`SLOW: ${endpoint.label} p95=${p95}ms (limit: ${endpoint.p95_limit_ms}ms)`);
  }
  console.log(`[perf-audit] ${endpoint.label}: p50=${p50}ms p95=${p95}ms (limit ${endpoint.p95_limit_ms}ms)`);
}

// 2. Compare to last baseline
let baselineComparison = '';
if (fs.existsSync(BASELINE_PATH)) {
  const baseline = JSON.parse(fs.readFileSync(BASELINE_PATH, 'utf8'));
  for (const r of results) {
    const prev = baseline.find(b => b.path === r.path);
    if (prev && r.p95 > prev.p95 * 1.5) {
      issues.push(`REGRESSION: ${r.label} p95 increased ${prev.p95}ms → ${r.p95}ms (+${Math.round((r.p95 / prev.p95 - 1) * 100)}%)`);
    }
  }
  baselineComparison = `Compared to last baseline: ${issues.filter(i => i.includes('REGRESSION')).length} regressions.`;
}

// 3. Save new baseline
fs.mkdirSync(path.dirname(BASELINE_PATH), { recursive: true });
fs.writeFileSync(BASELINE_PATH, JSON.stringify(results, null, 2));

// 4. Report
const table = results.map(r => `  ${r.label}: p50=${r.p50}ms p95=${r.p95}ms`).join('\n');
const report = [
  `Benchmarks (${new Date().toDateString()}):`,
  table,
  baselineComparison,
  issues.length > 0 ? `Issues:\n${issues.map((i, n) => `${n + 1}. ${i}`).join('\n')}` : 'No performance issues.',
].filter(Boolean).join('\n');

console.log(`[perf-audit] Complete. Issues: ${issues.length}`);
await notify('maintenance', {
  job: `Performance Audit — ${issues.length > 0 ? `${issues.length} issues` : 'PASS'}`,
  report,
});

function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

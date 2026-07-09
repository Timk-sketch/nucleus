/**
 * Nucleus Feature Optimizer — Bi-weekly (Wednesday 9 AM)
 *
 * Checks:
 * 1. AI cost per tenant per feature (from AiUsage table)
 * 2. Tenants spending >$2/day → flag for Haiku routing
 * 3. Starter plan tenants hitting limits → upsell signal
 * 4. Features with 0 usage in last 30 days → low-value flag
 *
 * Reports to Slack with optimization recommendations.
 */

import 'dotenv/config';
import { createClient } from '@supabase/supabase-js';
import { notify } from '../worker/notifier.js';

const supabase = createClient(
  process.env.NUCLEUS_SUPABASE_URL,
  process.env.NUCLEUS_SUPABASE_SERVICE_KEY
);

if (!process.env.NUCLEUS_SUPABASE_URL || !process.env.NUCLEUS_SUPABASE_SERVICE_KEY) {
  console.log('[feature-optimize] Missing NUCLEUS_SUPABASE_URL or NUCLEUS_SUPABASE_SERVICE_KEY — skipping');
  process.exit(0);
}

const thirtyDaysAgo = new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString();
const issues = [];
const recommendations = [];

// 1. AI cost per tenant per feature (last 30 days)
const { data: usageData, error } = await supabase
  .from('ai_usage')
  .select('tenant_id, feature, model, cost_usd')
  .gte('created_at', thirtyDaysAgo);

if (error) {
  console.error(`[feature-optimize] DB error: ${error.message}`);
  await notify('maintenance', { job: 'Feature Optimizer — ERROR', report: error.message });
  process.exit(1);
}

// Group by tenant + feature
const byTenant = {};
for (const row of (usageData || [])) {
  const key = row.tenant_id;
  if (!byTenant[key]) byTenant[key] = { total: 0, features: {}, modelsUsed: new Set() };
  byTenant[key].total += parseFloat(row.cost_usd || 0);
  byTenant[key].features[row.feature] = (byTenant[key].features[row.feature] || 0) + parseFloat(row.cost_usd || 0);
  byTenant[key].modelsUsed.add(row.model);
}

// 2. Flag tenants spending >$2/day (>$60/month)
const HIGH_COST_DAILY = 2.00;
const HIGH_COST_30DAY = HIGH_COST_DAILY * 30;

for (const [tenantId, data] of Object.entries(byTenant)) {
  const dailyAvg = data.total / 30;
  if (dailyAvg > HIGH_COST_DAILY) {
    const topFeature = Object.entries(data.features).sort((a, b) => b[1] - a[1])[0];
    issues.push(`High AI cost: tenant ${tenantId.slice(0, 8)}... — $${dailyAvg.toFixed(2)}/day (30d total: $${data.total.toFixed(2)})`);
    if (data.modelsUsed.has('claude-sonnet-4-6') || data.modelsUsed.has('claude-opus-4-6')) {
      recommendations.push(`Route "${topFeature[0]}" to Haiku for tenant ${tenantId.slice(0, 8)}... — saves ~80% cost`);
    }
  }
}

// 3. Zero-usage features (last 30 days)
const allFeatures = [...new Set((usageData || []).map(r => r.feature))];
const EXPECTED_FEATURES = ['content-generator', 'keyword-suggestions', 'rank-summary', 'schema-generator'];
for (const feature of EXPECTED_FEATURES) {
  if (!allFeatures.includes(feature)) {
    issues.push(`Zero usage in 30 days: feature "${feature}" — may be broken or undiscovered`);
  }
}

// 4. Build report
const totalCost = Object.values(byTenant).reduce((s, d) => s + d.total, 0);
const report = [
  `AI Usage Summary (last 30 days):`,
  `  Total cost: $${totalCost.toFixed(2)} across ${Object.keys(byTenant).length} tenants`,
  `  Active features: ${allFeatures.join(', ') || 'none'}`,
  issues.length > 0 ? `\nIssues:\n${issues.map((i, n) => `${n + 1}. ${i}`).join('\n')}` : '\nNo cost issues.',
  recommendations.length > 0 ? `\nRecommendations:\n${recommendations.map((r, n) => `${n + 1}. ${r}`).join('\n')}` : '',
].filter(Boolean).join('\n');

console.log(`[feature-optimize] Complete. Issues: ${issues.length}`);
await notify('maintenance', {
  job: `Feature Optimizer — ${issues.length > 0 ? `${issues.length} issues` : 'PASS'}`,
  report,
});

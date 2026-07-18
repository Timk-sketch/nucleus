import { execSync } from 'child_process';
import { pollHealth } from './verifier.js';

const NUCLEUS_ROOT = new URL('../..', import.meta.url).pathname.replace(/^\/([A-Z]:)/, '$1');
const STAGING_URL = process.env.NUCLEUS_STAGING_URL;
const PROD_URL = process.env.NUCLEUS_PROD_URL || 'https://nucleus-production.up.railway.app';

export async function deploy(sprintNumber, dryRun = false) {
  const git = (cmd) => {
    console.log(`[deployer] git ${cmd}`);
    if (!dryRun) execSync(`git ${cmd}`, { cwd: NUCLEUS_ROOT, stdio: 'inherit' });
    else console.log(`[deployer] (dry-run) skipped`);
  };

  // 1. Commit sprint changes
  git(`add -A`);
  git(`commit -m "feat: Sprint ${sprintNumber} — ${getSprintName(sprintNumber)}"`);

  // 1b. Rebase on latest master to avoid push rejection if master moved during build
  git(`fetch origin master`);
  git(`rebase origin/master`);

  // 2. Push to staging branch — Railway auto-deploys via GitHub integration (no webhook needed)
  git(`push origin HEAD:staging`);
  console.log(`[deployer] Pushed to staging branch — Railway will auto-deploy`);

  // 3. Wait for staging health (Railway deploys asynchronously — poll until live)
  if (STAGING_URL && !dryRun) {
    console.log(`[deployer] Waiting for Railway staging deploy to complete...`);
    await sleep(30_000); // give Railway ~30s to start the deploy
    await pollHealth(`${STAGING_URL}/health`, 'staging', 300_000);
  } else {
    console.log(`[deployer] ${dryRun ? '(dry-run)' : 'No NUCLEUS_STAGING_URL set'} — skipping staging health wait`);
  }

  // 4. Promote to master (triggers prod Railway deploy)
  git(`push origin HEAD:master`);
  console.log(`[deployer] Pushed to master — prod deploy triggered`);

  // 5. Wait for prod health — non-blocking: warn on failure, don't block the sprint
  // NUCLEUS_PROD_URL may point to the Blazor Web frontend, not the API.
  // Set NUCLEUS_PROD_API_URL to the actual API service URL to enable hard health gating.
  if (!dryRun) {
    const prodApiUrl = process.env.NUCLEUS_PROD_API_URL || PROD_URL;
    console.log('[deployer] Waiting 60s for Railway prod deploy to start...');
    await sleep(60_000);
    try {
      await pollHealth(`${prodApiUrl}/health`, 'production', 300_000);
    } catch (err) {
      console.warn(`[deployer] WARNING: prod health check did not pass — ${err.message}`);
      console.warn('[deployer] Code is on master. Set NUCLEUS_PROD_API_URL secret to the API service URL to enable hard health gating.');
    }
  }

  console.log(`[deployer] Sprint ${sprintNumber} deployed to production: ${PROD_URL}`);
}

function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

function getSprintName(n) {
  const names = { 24: 'Content Hub', 25: 'Search Hub', 26: 'Distribution Hub', 27: 'Authority Hub', 28: 'Studio Hub' };
  return names[n] || `Sprint ${n}`;
}

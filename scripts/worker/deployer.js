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

  // 2. Push to staging branch
  git(`push origin HEAD:staging`);
  console.log(`[deployer] Pushed to staging branch`);

  // 3. Wait for staging health
  if (STAGING_URL && !dryRun) {
    await pollHealth(`${STAGING_URL}/health`, 'staging', 300_000);
  } else {
    console.log(`[deployer] ${dryRun ? '(dry-run)' : 'No NUCLEUS_STAGING_URL'} — skipping staging health wait`);
  }

  // 4. Promote to master (triggers prod Railway deploy)
  git(`push origin HEAD:master`);
  console.log(`[deployer] Pushed to master — prod deploy triggered`);

  // 5. Wait for prod health
  if (!dryRun) {
    await pollHealth(`${PROD_URL}/health`, 'production', 300_000);
  }

  console.log(`[deployer] Sprint ${sprintNumber} deployed to production: ${PROD_URL}`);
}

function getSprintName(n) {
  const names = { 24: 'Content Hub', 25: 'Search Hub', 26: 'Distribution Hub', 27: 'Authority Hub', 28: 'Studio Hub' };
  return names[n] || `Sprint ${n}`;
}

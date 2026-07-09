/**
 * Nucleus Dependency Updater — Weekly (Friday 9 AM)
 *
 * Actions:
 * 1. Check for outdated NuGet packages (dotnet outdated)
 * 2. Auto-apply PATCH updates only (X.Y.PATCH) — run tests after each
 * 3. Flag MINOR and MAJOR updates for manual review (no auto-apply)
 * 4. Push patch updates if tests pass
 *
 * Reports to Slack.
 */

import 'dotenv/config';
import { execSync } from 'child_process';
import { notify } from '../worker/notifier.js';
import path from 'path';

const NUCLEUS_ROOT = new URL('../..', import.meta.url).pathname.replace(/^\/([A-Z]:)/, '$1');

const applied = [];
const flagged = [];
const failed = [];

// 1. Check for outdated packages
let outdatedOutput = '';
try {
  outdatedOutput = execSync('dotnet outdated --output json 2>&1 || echo "[]"', {
    cwd: NUCLEUS_ROOT,
    encoding: 'utf8',
  });
} catch (err) {
  // dotnet-outdated may not be installed
  const report = 'dotnet-outdated tool not installed. Install with: dotnet tool install -g dotnet-outdated-tool';
  console.log(`[dep-update] ${report}`);
  await notify('maintenance', { job: 'Dependency Update — SKIPPED', report });
  process.exit(0);
}

let packages = [];
try {
  // dotnet-outdated outputs JSON with upgrade suggestions
  const jsonStart = outdatedOutput.indexOf('[');
  if (jsonStart >= 0) {
    packages = JSON.parse(outdatedOutput.slice(jsonStart));
  }
} catch {
  packages = [];
}

for (const pkg of packages) {
  const current = pkg.resolvedVersion || '';
  const latest = pkg.latestVersion || '';
  const [curMaj, curMin, curPatch] = current.split('.').map(Number);
  const [latMaj, latMin] = latest.split('.').map(Number);

  const isMajor = latMaj > curMaj;
  const isMinor = !isMajor && latMin > curMin;
  const isPatch = !isMajor && !isMinor;

  if (isMajor || isMinor) {
    flagged.push(`${pkg.name}: ${current} → ${latest} (${isMajor ? 'MAJOR' : 'MINOR'} — manual review required)`);
    continue;
  }

  if (isPatch) {
    // Auto-apply patch update
    try {
      execSync(`dotnet add package ${pkg.name} --version ${latest}`, {
        cwd: path.join(NUCLEUS_ROOT, pkg.projectFilePath ? path.dirname(pkg.projectFilePath) : ''),
        encoding: 'utf8',
        stdio: 'pipe',
      });

      // Run tests to validate
      execSync('dotnet test Nucleus.sln -c Release --no-build --verbosity quiet', {
        cwd: NUCLEUS_ROOT,
        stdio: 'pipe',
      });

      applied.push(`${pkg.name}: ${current} → ${latest}`);
    } catch (err) {
      // Revert by restoring old version
      try {
        execSync(`dotnet add package ${pkg.name} --version ${current}`, {
          cwd: path.join(NUCLEUS_ROOT, pkg.projectFilePath ? path.dirname(pkg.projectFilePath) : ''),
          stdio: 'pipe',
        });
      } catch { /* best effort revert */ }
      failed.push(`${pkg.name}: patch ${latest} failed tests — reverted to ${current}`);
    }
  }
}

// Push patch updates if any applied
if (applied.length > 0) {
  try {
    execSync(`git add -A && git commit -m "chore: auto-update ${applied.length} NuGet patch dependencies"`, {
      cwd: NUCLEUS_ROOT,
      stdio: 'pipe',
    });
    execSync('git push origin master', { cwd: NUCLEUS_ROOT, stdio: 'pipe' });
    console.log(`[dep-update] Pushed ${applied.length} patch updates to master`);
  } catch (err) {
    failed.push(`Git push failed: ${err.message}`);
  }
}

// Report
const report = [
  applied.length > 0 ? `Patch updates applied (${applied.length}):\n${applied.map(a => `  + ${a}`).join('\n')}` : 'No patch updates available.',
  flagged.length > 0 ? `Needs manual review (${flagged.length}):\n${flagged.map(f => `  ! ${f}`).join('\n')}` : '',
  failed.length > 0 ? `Failed/reverted (${failed.length}):\n${failed.map(f => `  x ${f}`).join('\n')}` : '',
].filter(Boolean).join('\n\n');

console.log(`[dep-update] Applied: ${applied.length} | Flagged: ${flagged.length} | Failed: ${failed.length}`);
await notify('maintenance', {
  job: `Dependency Update — ${applied.length} patched, ${flagged.length} flagged`,
  report,
});

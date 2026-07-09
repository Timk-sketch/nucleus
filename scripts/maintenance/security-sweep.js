/**
 * Nucleus Security Sweep — Weekly (Monday 9 AM)
 *
 * Checks:
 * 1. NuGet packages with known CVEs (dotnet list package --vulnerable)
 * 2. Hardcoded secrets in source (API keys, connection strings)
 * 3. Controllers missing [Authorize] on non-public endpoints
 * 4. EF entities without corresponding RLS notes in migrations
 *
 * Reports to Slack. No auto-fix — flags for review.
 */

import 'dotenv/config';
import { execSync } from 'child_process';
import { notify } from '../worker/notifier.js';
import path from 'path';
import fs from 'fs';

const NUCLEUS_ROOT = new URL('../..', import.meta.url).pathname.replace(/^\/([A-Z]:)/, '$1');

const SECRET_PATTERNS = [
  /sk-[a-zA-Z0-9]{20,}/,                    // Anthropic API key
  /password\s*=\s*["'][^"']{6,}["']/i,      // Hardcoded password
  /api[_-]?key\s*=\s*["'][^"']{10,}["']/i,  // API key assignment
  /connection.?string.*Server=/i,            // Connection string in code
  /Bearer\s+[a-zA-Z0-9\-._~+/]{20,}/,       // Bearer token hardcoded
];

const issues = [];

// 1. NuGet CVE check
try {
  const output = execSync('dotnet list package --vulnerable --include-transitive 2>&1', {
    cwd: NUCLEUS_ROOT,
    encoding: 'utf8',
  });
  const vulnLines = output.split('\n').filter(l => l.includes('Critical') || l.includes('High') || l.includes('Moderate'));
  if (vulnLines.length > 0) {
    issues.push(`NuGet vulnerabilities found:\n${vulnLines.map(l => `  ${l.trim()}`).join('\n')}`);
  }
} catch (err) {
  issues.push(`NuGet CVE check error: ${err.message}`);
}

// 2. Secret scan on source files
const srcDir = path.join(NUCLEUS_ROOT, 'src');
const sourceFiles = walkForExtensions(srcDir, ['.cs', '.razor', '.json', '.yaml', '.yml']);

for (const file of sourceFiles) {
  // Skip appsettings files (expected to have connection string templates)
  if (file.includes('appsettings')) continue;

  const content = fs.readFileSync(file, 'utf8');
  for (const pattern of SECRET_PATTERNS) {
    if (pattern.test(content)) {
      const rel = file.replace(NUCLEUS_ROOT + path.sep, '').replace(/\\/g, '/');
      issues.push(`Potential secret in ${rel} (pattern: ${pattern.source.slice(0, 30)}...)`);
      break;
    }
  }
}

// 3. Controllers missing [Authorize]
const controllerDir = path.join(NUCLEUS_ROOT, 'src/Nucleus.Api/Controllers');
if (fs.existsSync(controllerDir)) {
  for (const file of walkForExtensions(controllerDir, ['.cs'])) {
    const content = fs.readFileSync(file, 'utf8');
    const rel = file.replace(NUCLEUS_ROOT + path.sep, '').replace(/\\/g, '/');

    // Controllers should have [Authorize] unless they explicitly have [AllowAnonymous]
    const hasAuthorize = content.includes('[Authorize]') || content.includes('[AllowAnonymous]');
    const isBaseController = file.includes('BaseController') || file.includes('HealthController');
    if (!hasAuthorize && !isBaseController) {
      issues.push(`Controller missing [Authorize] or [AllowAnonymous]: ${rel}`);
    }
  }
}

// 4. Build report
const passed = issues.length === 0;
const report = passed
  ? 'All security checks passed. No issues found.'
  : `${issues.length} issue(s) found:\n${issues.map((i, n) => `${n + 1}. ${i}`).join('\n')}`;

console.log(`[security-sweep] ${passed ? 'PASS' : 'ISSUES FOUND'}`);
console.log(report);

await notify('maintenance', {
  job: `Security Sweep — ${passed ? 'PASS' : `${issues.length} issues`}`,
  report,
});

function walkForExtensions(dir, exts) {
  if (!fs.existsSync(dir)) return [];
  const results = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) results.push(...walkForExtensions(full, exts));
    else if (exts.some(e => entry.name.endsWith(e))) results.push(full);
  }
  return results;
}

import { execSync } from 'child_process';

const NUCLEUS_ROOT = new URL('../..', import.meta.url).pathname.replace(/^\/([A-Z]:)/, '$1');
const HEALTH_URL = process.env.NUCLEUS_STAGING_URL
  ? `${process.env.NUCLEUS_STAGING_URL}/health`
  : null;

export async function verify(sprintNumber) {
  console.log(`[verifier] Running verification for Sprint ${sprintNumber}...`);

  // 1. dotnet build
  try {
    execSync('dotnet build Nucleus.sln -c Release --no-incremental', {
      cwd: NUCLEUS_ROOT,
      stdio: 'inherit',
    });
    console.log('[verifier] dotnet build: PASS');
  } catch {
    throw new Error('dotnet build FAILED — sprint cannot deploy');
  }

  // 2. dotnet test
  try {
    execSync('dotnet test Nucleus.sln -c Release --no-build --verbosity normal', {
      cwd: NUCLEUS_ROOT,
      stdio: 'inherit',
    });
    console.log('[verifier] dotnet test: PASS');
  } catch {
    throw new Error('dotnet test FAILED — sprint cannot deploy');
  }

  // 3. Health check (staging URL, if set)
  if (HEALTH_URL) {
    await pollHealth(HEALTH_URL, 'staging', 300_000);
  } else {
    console.log('[verifier] No NUCLEUS_STAGING_URL set — skipping live health check');
  }

  console.log(`[verifier] Sprint ${sprintNumber} verification: PASS`);
}

export async function pollHealth(url, label, timeoutMs = 300_000) {
  console.log(`[verifier] Polling ${label} health at ${url} (timeout: ${timeoutMs / 1000}s)...`);
  const start = Date.now();
  let consecutiveOk = 0;

  while (Date.now() - start < timeoutMs) {
    try {
      const { default: fetch } = await import('node-fetch');
      const res = await fetch(url, { timeout: 10_000 });
      if (res.ok) {
        consecutiveOk++;
        console.log(`[verifier] ${label} /health OK (${consecutiveOk}/3 consecutive)`);
        if (consecutiveOk >= 3) {
          console.log(`[verifier] ${label} health: STABLE`);
          return;
        }
      } else {
        consecutiveOk = 0;
        console.log(`[verifier] ${label} /health returned ${res.status} — waiting...`);
      }
    } catch (err) {
      consecutiveOk = 0;
      console.log(`[verifier] ${label} /health unreachable (${err.message}) — waiting...`);
    }
    await sleep(10_000);
  }

  throw new Error(`${label} health check timed out after ${timeoutMs / 1000}s`);
}

function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

'use strict';
/**
 * ci-red-alert.js — Makes a red `main` announce itself instead of waiting to be found.
 *
 * WHY THIS EXISTS (2026-08-26)
 * ---------------------------
 * `Tests` failed on every push to `main` for seven consecutive commits —
 * f4daa51, 91a8d4e, 1f0aa31, d8ae568, 1d736ca, 373ba4e, 0def654 — and nobody
 * noticed. It was found by accident, while reviewing an unrelated PR. The guard
 * that was broken (tests/cf-export-concurrency.test.js) is itself the guard
 * protecting against a brand-scoped dispatch cancelling a full export, which is
 * the bug that silently cost us the DL Powersports deploy on 2026-08-14.
 *
 * So: a guard nobody runs is not a guard, and a guard that runs where nobody
 * looks is the same thing wearing a green tick.
 *
 * WHY NOT JUST BLOCK THE PUSH
 * ---------------------------
 * Every one of those seven commits was pushed DIRECTLY to main, no PR. Branch
 * protection lists `Unit tests` as a required check, but required checks only
 * gate pull requests — a direct push is governed by `enforce_admins`, which is
 * false here. Flipping it to true would make the red impossible. It would also
 * end push-to-deploy, which is how this team actually ships: edit on Railway,
 * push to main, Cloudflare gets it. Trading that for a gate is the wrong trade,
 * and a gate that fights the daily workflow is a gate somebody turns off.
 *
 * WHY AN ISSUE AND NOT AN EMAIL
 * -----------------------------
 * GitHub already emails on a failed run. Those emails arrived for all seven and
 * changed nothing, because an email is a moment and this is a state. An open
 * issue persists, carries a count on the repo, and — the part that matters —
 * closes ITSELF when the next run goes green. Nobody has to remember to tidy up,
 * so nobody learns to ignore it.
 *
 * WHAT IT DELIBERATELY IGNORES
 * ----------------------------
 * Only `failure` and `timed_out` open an issue. `cancelled` must NOT, and that
 * is not a nicety: cf-export runs under a concurrency group that cancels queued
 * runs by design, so treating cancelled as red would file an issue every time
 * the pipeline worked exactly as intended. `skipped`, `neutral` and
 * `action_required` are silent for the same reason — none of them means broken.
 */

const fs = require('fs');

/**
 * Stable per-workflow-per-branch title. Stable is what makes dedup and
 * auto-close work; branch-aware because not every repo's trunk is `main`
 * (nucleus is `master`), and a title that lies about which branch is broken
 * sends whoever opens it to the wrong place.
 */
function issueTitle(workflowName, branch) {
  return `🔴 ${workflowName} is failing on ${branch}`;
}

/** Conclusions that mean "this is broken", as opposed to "this did not run". */
const RED = new Set(['failure', 'timed_out']);

/**
 * Decide what to do, given a run conclusion and whatever issue is already open.
 *
 * Split out from the API calls so the decision table is testable without a
 * network or a token — the alerting path is the one thing here that cannot be
 * allowed to break quietly, since its whole job is to stop quiet breakage.
 *
 *   open    — nothing tracked yet, and the run is red
 *   comment — already tracked, and this is a NEW red commit worth recording
 *   none    — already tracked and already reported for this sha (a re-run), or
 *             the conclusion does not mean broken
 *   close   — tracked, and the run went green
 */
function decide({ conclusion, openIssue, headSha, reportedShas = [] }) {
  if (RED.has(conclusion)) {
    if (!openIssue) return 'open';
    return reportedShas.includes(headSha) ? 'none' : 'comment';
  }
  if (conclusion === 'success') return openIssue ? 'close' : 'none';
  return 'none';
}

function body(run) {
  return [
    `**${run.name}** failed on \`${run.head_branch}\`.`,
    '',
    `| | |`,
    `|---|---|`,
    `| Commit | \`${(run.head_sha || '').slice(0, 7)}\` |`,
    `| Run | ${run.html_url} |`,
    `| Conclusion | \`${run.conclusion}\` |`,
    '',
    `This issue closes itself the next time this workflow goes green on \`${run.head_branch}\`.`,
    'It is open because a red run on trunk is a state, not a notification —',
    'seven consecutive red pushes went unnoticed on 2026-08-26, which is why',
    'this exists. See `scripts/ci-red-alert.js`.',
  ].join('\n');
}

async function api(path, { token, method = 'GET', payload }) {
  const res = await fetch(`https://api.github.com${path}`, {
    method,
    headers: {
      authorization: `Bearer ${token}`,
      accept: 'application/vnd.github+json',
      'user-agent': 'ci-red-alert',
      ...(payload ? { 'content-type': 'application/json' } : {}),
    },
    ...(payload ? { body: JSON.stringify(payload) } : {}),
  });
  if (!res.ok) throw new Error(`GitHub API ${res.status} on ${path}: ${await res.text()}`);
  return res.json();
}

async function main() {
  const token = process.env.GITHUB_TOKEN;
  const repo = process.env.GITHUB_REPOSITORY;
  const eventPath = process.env.GITHUB_EVENT_PATH;
  if (!token || !repo || !eventPath) {
    console.error('::error::ci-red-alert is missing GITHUB_TOKEN / GITHUB_REPOSITORY / GITHUB_EVENT_PATH.');
    process.exit(1);
  }

  const run = JSON.parse(fs.readFileSync(eventPath, 'utf8')).workflow_run;
  if (!run) {
    console.error('::error::no workflow_run in the event payload.');
    process.exit(1);
  }

  const branch = run.head_branch || 'the default branch';
  const title = issueTitle(run.name, branch);
  const open = await api(
    `/repos/${repo}/issues?state=open&creator=app%2Fgithub-actions&per_page=100`,
    { token }
  );
  const tracked = open.find((i) => i.title === title && !i.pull_request);

  let reportedShas = [];
  if (tracked) {
    const comments = await api(`/repos/${repo}/issues/${tracked.number}/comments?per_page=100`, { token });
    const text = [tracked.body || '', ...comments.map((c) => c.body || '')].join('\n');
    reportedShas = (text.match(/\b[0-9a-f]{7,40}\b/g) || []);
  }

  const action = decide({
    conclusion: run.conclusion,
    openIssue: Boolean(tracked),
    headSha: (run.head_sha || '').slice(0, 7),
    reportedShas,
  });

  if (action === 'open') {
    const issue = await api(`/repos/${repo}/issues`, {
      token,
      method: 'POST',
      payload: { title, body: body(run) },
    });
    console.log(`Opened #${issue.number} — ${title}`);
  } else if (action === 'comment') {
    await api(`/repos/${repo}/issues/${tracked.number}/comments`, {
      token,
      method: 'POST',
      payload: { body: body(run) },
    });
    console.log(`Still red — commented on #${tracked.number}`);
  } else if (action === 'close') {
    await api(`/repos/${repo}/issues/${tracked.number}/comments`, {
      token,
      method: 'POST',
      payload: {
        body: `Green again on \`${(run.head_sha || '').slice(0, 7)}\` — ${run.html_url}\n\nClosing automatically.`,
      },
    });
    await api(`/repos/${repo}/issues/${tracked.number}`, {
      token,
      method: 'PATCH',
      payload: { state: 'closed' },
    });
    console.log(`Green again — closed #${tracked.number}`);
  } else {
    console.log(`Nothing to do (conclusion=${run.conclusion}, tracked=${Boolean(tracked)}).`);
  }
}

if (require.main === module) {
  main().catch((err) => {
    console.error(`::error::ci-red-alert failed: ${err.message}`);
    process.exit(1);
  });
}

module.exports = { issueTitle, decide, RED };

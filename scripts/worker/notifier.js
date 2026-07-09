const WEBHOOK = process.env.SLACK_NUCLEUS_WEBHOOK;
const PROD_URL = process.env.NUCLEUS_PROD_URL || 'https://nucleus-production.up.railway.app';

export async function notify(type, payload) {
  if (!WEBHOOK) {
    console.log(`[notifier] No SLACK_NUCLEUS_WEBHOOK set — skipping Slack (${type})`);
    return;
  }

  const message = buildMessage(type, payload);

  try {
    const { default: fetch } = await import('node-fetch');
    const res = await fetch(WEBHOOK, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(message),
    });
    if (!res.ok) console.error(`[notifier] Slack returned ${res.status}`);
  } catch (err) {
    console.error(`[notifier] Slack error: ${err.message}`);
  }
}

function buildMessage(type, payload) {
  const icons = { started: ':rocket:', passed: ':white_check_mark:', failed: ':x:', maintenance: ':wrench:' };
  const icon = icons[type] || ':bell:';

  const texts = {
    started: `${icon} *Nucleus Sprint ${payload.sprint} starting* — ${payload.name}\nWorker is now building autonomously. Next update on completion.`,
    passed: `${icon} *Sprint ${payload.sprint} shipped to production*\n${payload.name} is live at ${PROD_URL}\n${payload.summary || ''}`,
    failed: `${icon} *Sprint ${payload.sprint} BLOCKED*\nStep: ${payload.step}\nError: \`${payload.error}\`\nLog: \`${payload.logPath || 'see worker output'}\`\nManual intervention required.`,
    maintenance: `${icon} *Nucleus maintenance: ${payload.job}*\n${payload.report}`,
  };

  return {
    text: texts[type] || `Nucleus worker: ${type}`,
    unfurl_links: false,
  };
}

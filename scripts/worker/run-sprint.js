/**
 * Nucleus Sprint Worker — run-sprint.js
 *
 * Usage:
 *   node run-sprint.js <sprint-number> [--dry-run]
 *
 * Required env vars:
 *   ANTHROPIC_API_KEY          — Claude API key
 *   SLACK_NUCLEUS_WEBHOOK      — Slack webhook for notifications
 *   NUCLEUS_STAGING_URL        — e.g. https://nucleus-staging.up.railway.app
 *   NUCLEUS_PROD_URL           — e.g. https://nucleus-production.up.railway.app (default)
 *
 * Optional:
 *   MAX_BUILD_FAIL_CYCLES=3    — Stop after N consecutive build failures (default: 3)
 */

import 'dotenv/config';
import Anthropic from '@anthropic-ai/sdk';
import fs from 'fs';
import path from 'path';
import { execSync } from 'child_process';

import { loadSprint, markSprintStatus, nextPendingSprint } from './sprint-loader.js';
import { buildContext } from './context-builder.js';
import { verify } from './verifier.js';
import { deploy } from './deployer.js';
import { notify } from './notifier.js';

const NUCLEUS_ROOT = new URL('../..', import.meta.url).pathname.replace(/^\/([A-Z]:)/, '$1');
const MAX_FAIL_CYCLES = parseInt(process.env.MAX_BUILD_FAIL_CYCLES || '3', 10);

const sprintNumber = parseInt(process.argv[2], 10);
const dryRun = process.argv.includes('--dry-run');

if (!sprintNumber || isNaN(sprintNumber)) {
  console.error('Usage: node run-sprint.js <sprint-number> [--dry-run]');
  process.exit(1);
}

if (dryRun) {
  console.log('[worker] DRY RUN MODE — no API calls, no deploys, no git pushes');
}

await runSprint(sprintNumber);

async function runSprint(n) {
  const logPath = path.join(NUCLEUS_ROOT, '.sprints', `sprint-${n}.log`);
  const log = createLogger(logPath);

  log(`=== Nucleus Sprint Worker: Sprint ${n} ===`);
  log(`Started: ${new Date().toISOString()}`);
  log(`Dry run: ${dryRun}`);

  // 1. Load spec
  let spec;
  try {
    spec = loadSprint(n);
    log(`Loaded spec: Sprint ${n} — ${spec.name}`);
  } catch (err) {
    log(`FATAL: ${err.message}`);
    await notify('failed', { sprint: n, step: 'spec-load', error: err.message, logPath });
    process.exit(1);
  }

  // 2. Notify start
  await notify('started', { sprint: n, name: spec.name });
  markSprintStatus(n, 'in_progress');

  // 3. Build context for Claude
  log('Building Claude context...');
  let context;
  try {
    context = buildContext(spec);
    log(`Context built: ~${Math.round(context.length / 4)} tokens estimated`);
  } catch (err) {
    log(`FATAL context build: ${err.message}`);
    await notify('failed', { sprint: n, step: 'context-build', error: err.message, logPath });
    process.exit(1);
  }

  if (dryRun) {
    log(`[dry-run] Context size: ${context.length} chars`);
    log(`[dry-run] Would call Claude API with sprint spec`);
    log(`[dry-run] Would run: dotnet build, dotnet test, git push staging, git push master`);
    console.log('\nDry run complete. Ready to execute.');
    return;
  }

  // 4. Call Claude API — implement the sprint
  log('Calling Claude API...');
  let buildFailCycles = 0;

  try {
    const client = new Anthropic();

    const systemPrompt = `You are an expert C# / .NET 9 / Blazor WASM engineer implementing Sprint ${n} for Nucleus.

Nucleus is a multi-tenant SaaS Marketing OS. Every entity must:
- Inherit TenantEntity (adds TenantId, BrandId FK, CreatedAt, UpdatedAt)
- Have EF Core configuration in Nucleus.Api/Data/
- Have MediatR Commands/Queries in Nucleus.Application/
- Have thin API controller endpoints in Nucleus.Api/Controllers/
- Have Blazor WASM pages in Nucleus.Web/Pages/

Critical rules:
- Every query filters by TenantId (ICurrentTenantService)
- All AI generation calls go through AiUsage tracking
- Plan gates enforced via TenantPlanService
- EF migrations: use "dotnet ef migrations add ${spec.migration}" from src/Nucleus.Api/
- Run "dotnet build Nucleus.sln" after every batch of file writes to catch errors early
- Fix all build errors before continuing
- The sprint is complete when ALL acceptance_criteria pass

Work autonomously. Read existing files before writing to follow established patterns.
Start with Domain entities, then EF config, then Application commands/queries, then Controllers, then Blazor pages.`;

    const userMessage = `${context}

## Your Task
Implement Sprint ${n}: ${spec.name}

Work through these in order:
1. Create Domain entities (${spec.entities.map(e => e.name).join(', ')})
2. Add EF Core configurations
3. Run: dotnet ef migrations add ${spec.migration} (from src/Nucleus.Api/)
4. Implement MediatR Commands: ${spec.commands.join(', ')}
5. Implement MediatR Queries: ${spec.queries.join(', ')}
6. Add thin API controller endpoints
7. Build Blazor pages: ${spec.blazor_pages.join(', ')}
8. Run dotnet build and dotnet test — fix all errors
9. Confirm all acceptance_criteria are met

Reference the SEO Hub port map above for business logic. Apply multi-tenancy to everything.`;

    const stream = client.messages.stream({
      model: 'claude-sonnet-4-6',
      max_tokens: 32000,
      system: systemPrompt,
      messages: [{ role: 'user', content: userMessage }],
      tools: getTools(),
    });

    for await (const event of stream) {
      if (event.type === 'content_block_delta' && event.delta.type === 'text_delta') {
        process.stdout.write(event.delta.text);
      }
      if (event.type === 'content_block_start' && event.content_block?.type === 'tool_use') {
        log(`Tool call: ${event.content_block.name}`);
      }
    }

    const finalMessage = await stream.finalMessage();
    log(`Claude finished. Stop reason: ${finalMessage.stop_reason}`);
    log(`Tokens used: input=${finalMessage.usage.input_tokens} output=${finalMessage.usage.output_tokens}`);

  } catch (err) {
    log(`Claude API error: ${err.message}`);
    await notify('failed', { sprint: n, step: 'claude-api', error: err.message, logPath });
    process.exit(1);
  }

  // 5. Verify
  log('Running verification...');
  try {
    await verify(n);
  } catch (err) {
    buildFailCycles++;
    log(`Verification failed (attempt ${buildFailCycles}/${MAX_FAIL_CYCLES}): ${err.message}`);
    if (buildFailCycles >= MAX_FAIL_CYCLES) {
      await notify('failed', { sprint: n, step: 'verify', error: `Failed ${MAX_FAIL_CYCLES} times: ${err.message}`, logPath });
      markSprintStatus(n, 'failed');
      process.exit(1);
    }
    // Could retry here — for now treat as fatal after first failure post-Claude
    await notify('failed', { sprint: n, step: 'verify', error: err.message, logPath });
    markSprintStatus(n, 'failed');
    process.exit(1);
  }

  // 6. Deploy staging → prod
  log('Deploying...');
  try {
    await deploy(n, dryRun);
  } catch (err) {
    log(`Deploy failed: ${err.message}`);
    await notify('failed', { sprint: n, step: 'deploy', error: err.message, logPath });
    markSprintStatus(n, 'failed');
    process.exit(1);
  }

  // 7. Mark complete + notify
  markSprintStatus(n, 'complete');
  log(`Sprint ${n} complete at ${new Date().toISOString()}`);

  await notify('passed', {
    sprint: n,
    name: spec.name,
    summary: `Entities: ${spec.entities.map(e => e.name).join(', ')} | Pages: ${spec.blazor_pages.length}`,
  });

  // 8. Auto-chain next sprint
  const next = nextPendingSprint(n);
  if (next) {
    log(`Auto-starting Sprint ${next}...`);
    await runSprint(next);
  } else {
    log('All sprints complete. SEO Hub retirement checklist ready for review.');
    await notify('passed', {
      sprint: 'ALL',
      name: 'All 5 Nucleus hubs built',
      summary: 'Review .sprints/retirement-checklist.md — SEO Hub can now be retired.',
    });
  }
}

function getTools() {
  return [
    {
      name: 'read_file',
      description: 'Read a file from the Nucleus repository',
      input_schema: {
        type: 'object',
        properties: {
          path: { type: 'string', description: 'Relative path from Nucleus root' },
        },
        required: ['path'],
      },
    },
    {
      name: 'write_file',
      description: 'Write or overwrite a file in the Nucleus repository',
      input_schema: {
        type: 'object',
        properties: {
          path: { type: 'string', description: 'Relative path from Nucleus root' },
          content: { type: 'string', description: 'Full file content' },
        },
        required: ['path', 'content'],
      },
    },
    {
      name: 'bash',
      description: 'Run a shell command in the Nucleus root directory (build, test, ef migrations)',
      input_schema: {
        type: 'object',
        properties: {
          command: { type: 'string', description: 'Shell command to execute' },
        },
        required: ['command'],
      },
    },
  ];
}

// Tool execution (Claude's tool calls come back as tool_result — handled in stream above)
// This is a simplified version; a full agentic loop would process tool_use events and feed results back
// For full tool-use loop, extend the stream handler to accumulate tool calls and respond

function createLogger(logPath) {
  fs.mkdirSync(path.dirname(logPath), { recursive: true });
  return (msg) => {
    const line = `[${new Date().toISOString()}] ${msg}`;
    console.log(line);
    fs.appendFileSync(logPath, line + '\n');
  };
}

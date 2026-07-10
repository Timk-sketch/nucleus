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
 *   MAX_TURNS=50               — Max Claude agentic turns per sprint (default: 50)
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
const MAX_TURNS = parseInt(process.env.MAX_TURNS || '50', 10);

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

  // 4. Call Claude API — agentic tool loop
  log('Calling Claude API (agentic loop)...');

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
- Fix ALL build errors before declaring done
- Nullable reference types are enabled — use string? where nullable
- The sprint is complete when ALL acceptance_criteria pass and dotnet build succeeds with 0 errors

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
8. Run dotnet build Nucleus.sln and fix ALL errors (0 errors required)
9. Run dotnet test and fix any failures
10. Confirm all acceptance_criteria are met

Reference the SEO Hub port map above for business logic. Apply multi-tenancy to everything.`;

    const messages = [{ role: 'user', content: userMessage }];
    let totalInputTokens = 0;
    let totalOutputTokens = 0;
    let turn = 0;

    while (turn < MAX_TURNS) {
      turn++;
      log(`Claude turn ${turn}/${MAX_TURNS}...`);

      const response = await client.messages.create({
        model: 'claude-sonnet-4-6',
        max_tokens: 32000,
        system: systemPrompt,
        messages,
        tools: getTools(),
      });

      totalInputTokens += response.usage.input_tokens;
      totalOutputTokens += response.usage.output_tokens;

      // Print any text Claude produces
      for (const block of response.content) {
        if (block.type === 'text' && block.text) {
          process.stdout.write(block.text);
        }
      }

      // Append assistant turn to message history
      messages.push({ role: 'assistant', content: response.content });

      if (response.stop_reason === 'end_turn') {
        log(`Claude finished after ${turn} turns. Total tokens: input=${totalInputTokens} output=${totalOutputTokens}`);
        break;
      }

      if (response.stop_reason === 'tool_use') {
        const toolResults = [];

        for (const block of response.content) {
          if (block.type !== 'tool_use') continue;

          const preview = JSON.stringify(block.input).substring(0, 120);
          log(`Tool: ${block.name} — ${preview}`);

          let result;
          try {
            result = executeTool(block.name, block.input, log);
          } catch (err) {
            result = `ERROR: ${err.message}`;
            log(`Tool error (${block.name}): ${err.message}`);
          }

          toolResults.push({
            type: 'tool_result',
            tool_use_id: block.id,
            content: typeof result === 'string' ? result : JSON.stringify(result),
          });
        }

        messages.push({ role: 'user', content: toolResults });
        continue;
      }

      // max_tokens or unexpected stop
      log(`Stop reason: ${response.stop_reason} — ending loop`);
      break;
    }

    if (turn >= MAX_TURNS) {
      log(`WARNING: Reached MAX_TURNS (${MAX_TURNS}) — Claude may not have finished`);
    }

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
    log(`Verification failed: ${err.message}`);
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

function executeTool(name, input, log) {
  switch (name) {
    case 'read_file': {
      const filePath = path.join(NUCLEUS_ROOT, input.path);
      if (!fs.existsSync(filePath)) return `File not found: ${input.path}`;
      return fs.readFileSync(filePath, 'utf8');
    }

    case 'write_file': {
      const filePath = path.join(NUCLEUS_ROOT, input.path);
      fs.mkdirSync(path.dirname(filePath), { recursive: true });
      fs.writeFileSync(filePath, input.content, 'utf8');
      log(`Written: ${input.path} (${input.content.length} chars)`);
      return `OK: ${input.path}`;
    }

    case 'bash': {
      try {
        const output = execSync(input.command, {
          cwd: NUCLEUS_ROOT,
          encoding: 'utf8',
          stdio: 'pipe',
          timeout: 180000, // 3 min per command
        });
        return output || '(no output)';
      } catch (err) {
        return `EXIT ${err.status || 1}:\nSTDOUT: ${err.stdout || ''}\nSTDERR: ${err.stderr || ''}`;
      }
    }

    default:
      return `Unknown tool: ${name}`;
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
          path: { type: 'string', description: 'Relative path from Nucleus root (e.g. src/Nucleus.Domain/Entities/Content.cs)' },
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
          content: { type: 'string', description: 'Full file content to write' },
        },
        required: ['path', 'content'],
      },
    },
    {
      name: 'bash',
      description: 'Run a shell command in the Nucleus root directory (dotnet build, dotnet test, dotnet ef migrations, etc.)',
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

function createLogger(logPath) {
  fs.mkdirSync(path.dirname(logPath), { recursive: true });
  return (msg) => {
    const line = `[${new Date().toISOString()}] ${msg}`;
    console.log(line);
    fs.appendFileSync(logPath, line + '\n');
  };
}

import fs from 'fs';
import path from 'path';

const NUCLEUS_ROOT = path.resolve(import.meta.dirname, '../..');
const SEOHUB_ROOT = path.resolve(NUCLEUS_ROOT, '../../TK-DL-Master-Databaset');

export function buildContext(spec) {
  const parts = [];

  // 1. Project guide
  const claudeMd = readFile(path.join(NUCLEUS_ROOT, 'CLAUDE.md'));
  parts.push(`# CLAUDE.md (Project Guide)\n${claudeMd}`);

  // 2. Memory files
  const memoryDir = path.join(NUCLEUS_ROOT, 'memory');
  for (const file of ['strategy.md', 'architecture.md', 'sprints.md', 'decisions.md']) {
    const content = readFile(path.join(memoryDir, file));
    parts.push(`# memory/${file}\n${content}`);
  }

  // 3. Sprint spec
  parts.push(`# Sprint ${spec.sprint} Spec\n${JSON.stringify(spec, null, 2)}`);

  // 4. Current codebase structure (key files only — not full tree)
  parts.push(`# Current Nucleus File Structure\n${getCriticalFileTree()}`);

  // 5. Port map — SEO Hub source excerpts (first 200 lines of each mapped file)
  const portExcerpts = [];
  for (const entry of (spec.port_map || [])) {
    const seoPath = path.join(SEOHUB_ROOT, entry.seohub_file);
    if (fs.existsSync(seoPath)) {
      const lines = fs.readFileSync(seoPath, 'utf8').split('\n').slice(0, 200).join('\n');
      portExcerpts.push(`## ${entry.seohub_file} → ${entry.nucleus_target}\nNotes: ${entry.notes || 'none'}\n\`\`\`js\n${lines}\n\`\`\``);
    } else {
      portExcerpts.push(`## ${entry.seohub_file} → ${entry.nucleus_target}\n[File not found in SEO Hub — implement from spec only]`);
    }
  }
  if (portExcerpts.length > 0) {
    parts.push(`# SEO Hub Port Map (Source References)\n${portExcerpts.join('\n\n')}`);
  }

  return parts.join('\n\n---\n\n');
}

function getCriticalFileTree() {
  const dirs = [
    'src/Nucleus.Domain/Entities',
    'src/Nucleus.Application',
    'src/Nucleus.Api/Controllers',
    'src/Nucleus.Api/Data',
    'src/Nucleus.Api/Jobs',
    'src/Nucleus.Web/Pages',
    'src/Nucleus.Web/Layout',
  ];

  const lines = [];
  for (const dir of dirs) {
    const fullPath = path.join(NUCLEUS_ROOT, dir);
    if (!fs.existsSync(fullPath)) continue;
    const files = walkDir(fullPath);
    for (const f of files) {
      lines.push(f.replace(NUCLEUS_ROOT + path.sep, '').replace(/\\/g, '/'));
    }
  }
  return lines.join('\n');
}

function walkDir(dir) {
  const results = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) results.push(...walkDir(full));
    else results.push(full);
  }
  return results;
}

function readFile(filePath) {
  if (!fs.existsSync(filePath)) return '[file not found]';
  return fs.readFileSync(filePath, 'utf8');
}

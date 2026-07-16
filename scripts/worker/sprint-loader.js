import fs from 'fs';
import path from 'path';
import yaml from 'js-yaml';

const SPRINTS_DIR = path.resolve(import.meta.dirname, '../../.sprints');

const REQUIRED_FIELDS = ['sprint', 'name', 'entities', 'migration', 'commands', 'queries', 'blazor_pages', 'port_map', 'acceptance_criteria'];

export function loadSprint(sprintNumber) {
  const filePath = path.join(SPRINTS_DIR, `sprint-${sprintNumber}.yaml`);

  if (!fs.existsSync(filePath)) {
    throw new Error(`Sprint spec not found: ${filePath}`);
  }

  const raw = fs.readFileSync(filePath, 'utf8');
  const spec = yaml.load(raw);

  for (const field of REQUIRED_FIELDS) {
    if (spec[field] === undefined) {
      throw new Error(`Sprint ${sprintNumber} spec missing required field: ${field}`);
    }
  }

  if (spec.status === 'complete') {
    throw new Error(`Sprint ${sprintNumber} is already marked complete.`);
  }

  if (spec.status === 'in_progress') {
    console.warn(`[loader] Warning: sprint-${sprintNumber}.yaml is still in_progress — resuming.`);
  }

  return spec;
}

export function markSprintStatus(sprintNumber, status) {
  const filePath = path.join(SPRINTS_DIR, `sprint-${sprintNumber}.yaml`);
  let raw = fs.readFileSync(filePath, 'utf8');
  raw = raw.replace(/^status:.*$/m, `status: ${status}`);
  fs.writeFileSync(filePath, raw, 'utf8');
}

export function nextPendingSprint(currentNumber) {
  for (let n = currentNumber + 1; n <= 30; n++) {
    const filePath = path.join(SPRINTS_DIR, `sprint-${n}.yaml`);
    if (!fs.existsSync(filePath)) continue;
    const raw = fs.readFileSync(filePath, 'utf8');
    const spec = yaml.load(raw);
    if (spec.status === 'pending') return n;
  }
  return null;
}

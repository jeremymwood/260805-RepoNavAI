import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, extname, join, resolve } from 'node:path';

const root = process.cwd();
const files = ['README.md', ...walk(join(root, 'docs')), ...walk(join(root, '.github'))]
  .map(path => resolve(path))
  .filter(path => extname(path).toLowerCase() === '.md');
const errors = [];

for (const file of files) {
  const text = readFileSync(file, 'utf8');
  const relative = file.slice(root.length + 1).replaceAll('\\', '/');
  if (!text.endsWith('\n')) errors.push(`${relative}: file must end with a newline`);
  text.split(/\r?\n/).forEach((line, index) => {
    if (/[ \t]+$/.test(line)) errors.push(`${relative}:${index + 1}: trailing whitespace`);
  });

  for (const match of text.matchAll(/!?\[[^\]]*\]\(([^)]+)\)/g)) {
    let target = match[1].trim().replace(/^<|>$/g, '');
    if (!target || target.startsWith('#') || /^[a-z][a-z0-9+.-]*:/i.test(target)) continue;
    target = target.split('#', 1)[0].split('?', 1)[0];
    try { target = decodeURIComponent(target); } catch { errors.push(`${relative}: invalid encoded link ${match[1]}`); continue; }
    const local = resolve(dirname(file), target);
    if (!existsSync(local)) errors.push(`${relative}: broken local link ${match[1]}`);
  }
}

if (errors.length) {
  console.error(`Documentation validation failed:\n${errors.map(error => `- ${error}`).join('\n')}`);
  process.exitCode = 1;
} else {
  console.log(`Validated ${files.length} Markdown files.`);
}

function walk(directory) {
  if (!existsSync(directory)) return [];
  return readdirSync(directory).flatMap(name => {
    const path = join(directory, name);
    return statSync(path).isDirectory() ? walk(path) : [path];
  });
}

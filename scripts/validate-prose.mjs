import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { basename, extname, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const includedExtensions = new Set([
  '.bicep', '.cs', '.csproj', '.css', '.html', '.js', '.jsx', '.md', '.mjs',
  '.ps1', '.scss', '.sh', '.sql', '.ts', '.tsx', '.yaml', '.yml'
]);
const includedNames = new Set(['Dockerfile']);
const excludedDirectories = new Set([
  '.git', 'bin', 'coverage', 'dist', 'node_modules', 'obj', 'playwright-report',
  'test-results'
]);
const excludedPathSegments = [
  'src/RepoNavAI.Infrastructure/Persistence/Migrations/'
];
const excludedFileSuffixes = ['.Designer.cs', 'AppDbContextModelSnapshot.cs'];

export function findEmDashViolations(text, path = 'input') {
  const violations = [];
  text.split(/\r?\n/).forEach((line, lineIndex) => {
    let column = line.indexOf('\u2014');
    while (column !== -1) {
      violations.push({ path, line: lineIndex + 1, column: column + 1 });
      column = line.indexOf('\u2014', column + 1);
    }
  });
  return violations;
}

export function isCoveredPath(path) {
  const normalized = path.split(sep).join('/');
  if (excludedPathSegments.some(segment => normalized.includes(segment))) return false;
  if (excludedFileSuffixes.some(suffix => normalized.endsWith(suffix))) return false;
  return includedNames.has(basename(normalized)) || includedExtensions.has(extname(normalized).toLowerCase());
}

export function validateProse(root) {
  const violations = [];
  for (const file of walk(root)) {
    const path = relative(root, file).split(sep).join('/');
    if (!isCoveredPath(path)) continue;
    violations.push(...findEmDashViolations(readFileSync(file, 'utf8'), path));
  }
  return violations;
}

function walk(directory) {
  if (!existsSync(directory)) return [];
  return readdirSync(directory).flatMap(name => {
    if (excludedDirectories.has(name)) return [];
    const path = resolve(directory, name);
    return statSync(path).isDirectory() ? walk(path) : [path];
  });
}

const isDirectRun = process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isDirectRun) {
  const root = resolve(process.cwd());
  const violations = validateProse(root);
  if (violations.length) {
    console.error('Prose validation failed. Replace em dash characters with context-appropriate punctuation:');
    for (const violation of violations) console.error(`- ${violation.path}:${violation.line}:${violation.column}`);
    process.exitCode = 1;
  } else {
    console.log('Validated covered repository prose: no em dash characters found.');
  }
}

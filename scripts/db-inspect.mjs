import { spawnSync } from 'node:child_process';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const composeFile = resolve(root, 'docker-compose.yml');
const allowed = new Set(['--check']);
const unknown = process.argv.slice(2).filter(argument => !allowed.has(argument));

if (unknown.length) {
  console.error(`Unsupported argument: ${unknown[0]}`);
  console.error('Usage: node scripts/db-inspect.mjs [--check]');
  process.exit(2);
}

const check = process.argv.includes('--check');
const psql = check
  ? 'exec psql -X --set ON_ERROR_STOP=on --tuples-only --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --command "SELECT current_database(), current_setting(\'default_transaction_read_only\'), extversion FROM pg_extension WHERE extname = \'vector\';"'
  : 'exec psql -X --set ON_ERROR_STOP=on --username "$POSTGRES_USER" --dbname "$POSTGRES_DB"';

console.log('Opening the local RepoNavAI PostgreSQL database in enforced read-only mode.');
const result = spawnSync('docker', [
  'compose', '--file', composeFile, '--project-directory', root,
  'exec', '--env', 'PGOPTIONS=-c default_transaction_read_only=on',
  'postgres', 'sh', '-c', psql,
], { cwd: root, stdio: 'inherit', shell: false });

if (result.error) {
  console.error(`Unable to start Docker Compose: ${result.error.message}`);
  process.exit(1);
}
process.exit(result.status ?? 1);

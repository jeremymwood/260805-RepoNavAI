import assert from 'node:assert/strict';
import test from 'node:test';
import { findEmDashViolations, isCoveredPath } from './validate-prose.mjs';

test('reports every em dash with an actionable line and column', () => {
  const emDash = String.fromCodePoint(0x2014);
  assert.deepEqual(findEmDashViolations(`first${emDash}line\nclean\nthird ${emDash} line`, 'docs/example.md'), [
    { path: 'docs/example.md', line: 1, column: 6 },
    { path: 'docs/example.md', line: 3, column: 7 }
  ]);
});

test('covers repository prose and source while excluding generated output', () => {
  assert.equal(isCoveredPath('README.md'), true);
  assert.equal(isCoveredPath('.github/ISSUE_TEMPLATE/feature.yml'), true);
  assert.equal(isCoveredPath('src/RepoNavAI.Web/src/App.tsx'), true);
  assert.equal(isCoveredPath('src/RepoNavAI.Api/Program.cs'), true);
  assert.equal(isCoveredPath('src/RepoNavAI.Infrastructure/Persistence/Migrations/Example.cs'), false);
  assert.equal(isCoveredPath('src/Example.Designer.cs'), false);
  assert.equal(isCoveredPath('src/RepoNavAI.Web/package-lock.json'), false);
  assert.equal(isCoveredPath('tests/browser/example.png'), false);
});

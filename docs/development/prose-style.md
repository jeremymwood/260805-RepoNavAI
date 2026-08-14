# Repository prose style

## No-em-dash convention

Do not use the em dash character in version-controlled repository prose. Choose punctuation that reflects the sentence instead:

- use a colon to introduce an explanation or list;
- use a semicolon to join closely related independent clauses;
- use commas or parentheses for a nonessential phrase;
- split a crowded sentence into two sentences.

This convention covers:

- `README.md`, Markdown documentation, ADRs, and runbooks;
- issue forms, pull-request templates, and workflow prose under `.github`;
- source files, including comments and user-visible strings;
- scripts, infrastructure templates, stylesheets, and repository-owned SQL.

Run the checks locally from the repository root:

```bash
node --test scripts/validate-prose.test.mjs
node scripts/validate-prose.mjs
node scripts/validate-docs.mjs
```

The prose validator reports every violation as `path:line:column` so an editor and CI log can locate it directly.

## Exclusions

The validator ignores binary files and file types that do not contain repository prose. It also excludes dependency, build, coverage, and browser-test output directories: `.git`, `node_modules`, `bin`, `obj`, `dist`, `coverage`, `playwright-report`, and `test-results`.

Entity Framework migration files, generated `*.Designer.cs` files, and `AppDbContextModelSnapshot.cs` are excluded because changing generated artifacts outside their generator can make migrations unsafe. Lockfiles, snapshots, and images are outside the covered text extensions. Vendored content must remain in its vendor directory and must not be edited solely for this convention.

External quotations are not excluded automatically. Prefer a link and a short paraphrase. If an exact quotation must retain an em dash, add the narrowest exact-file exclusion to `scripts/validate-prose.mjs`, document the source and justification here, and keep the surrounding repository prose compliant.

Do not add broad mechanical replacement commands to the workflow. Review each sentence so its punctuation and meaning remain correct.

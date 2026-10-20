# Work management and delivery

RepoNav AI uses GitHub Issues as the work-item source of truth and GitHub Project 14 as the prioritized delivery view.

## Intake

Use the repository issue forms for features, bugs, and technical work. New issues and pull requests are added to the project by `.github/workflows/project-intake.yml`.

The workflow requires a repository Actions secret named `PROJECT_TOKEN`. Create a dedicated fine-grained personal access token with the minimum access needed to read this repository's issues and pull requests and write the owner's Projects, then add it under **Repository settings → Secrets and variables → Actions**. Do not reuse a developer CLI token or store the token in source.

New items enter `Backlog`. During triage, set:

- `P0`: blocks the next usable product increment or addresses an urgent production/security risk.
- `P1`: required for the next major capability but does not block current foundational work.
- `P2`: valuable work that depends on earlier capabilities or can be scheduled later.
- `Size`: relative delivery uncertainty and effort (`XS` through `XL`), not elapsed time.

Only actively owned work should be `In progress`. Pull requests must link their issue using `Closes #123` when they complete the item.

## Work-item handoff

At the end of each implementation item, provide exact local startup/rebuild commands, the URL and required configuration, numbered manual tests with observable pass criteria, and the automated checks actually run. Note unverified cases and environment limitations explicitly. Add reusable checks to the [manual acceptance runbook](../testing/manual-acceptance.md), linking a feature-specific guide where useful. Wait for acceptance/review before beginning the next item; do not imply that passing mocked tests validates live provider behavior.

## Continuous integration

Every pull request and push to `main` runs backend restore/build/tests, frontend production-dependency audit/lint/tests/build, and non-publishing container builds. The production audit must report zero advisories; registry failures also fail the gate. The full dependency audit is still tracked separately under [Tailwind security maintenance (#96)](https://github.com/jeremymwood/260805-RepoNavAI/issues/96), not silently waived by this production-only check. Workflow permissions default to read-only, concurrent obsolete CI runs are canceled, and jobs have bounded timeouts.

## Artifact delivery

Pushes to `main` publish API and web images to GitHub Container Registry with immutable commit tags and a moving `main` tag:

- `ghcr.io/jeremymwood/reponav-ai-api`
- `ghcr.io/jeremymwood/reponav-ai-web`

This is artifact delivery, not runtime deployment. A separate ADR must select the hosting platform, environment promotion model, database service, secret store, rollback strategy, and production approval policy before continuous deployment is enabled.

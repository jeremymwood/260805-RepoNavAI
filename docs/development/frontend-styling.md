# Frontend styling guide

RepoNavAI uses Tailwind utilities for one-off layout and a small semantic component layer in `src/RepoNavAI.Web/src/styles.css` for repeated interface patterns. New screens should preserve the established visual identity and reuse these primitives before adding another control or surface treatment.

## Tokens

Theme-aware color tokens live as RGB channel values under `:root` and `:root[data-theme='dark']`. Tailwind exposes them as `canvas`, `ink`, `slate-*`, and `brand-*`. Use those names instead of raw colors for ordinary surfaces and text. Status colors may use Tailwind's red, amber, and emerald scales, with dark-theme overrides kept in the shared stylesheet.

Control height and panel and control radii are CSS custom properties. Add a token only when the value represents a deliberate, repeated design decision.

## Shared primitives

- `panel`, `panel-header`, and `panel-icon` define application surfaces.
- `field` wraps a label, control, and optional help text. Inputs, selects, and textareas inside it receive `control` automatically.
- `control` is available for controls that cannot use a visible wrapping label. They still require an accessible name.
- `button-primary`, `button-secondary`, `button-danger`, and `icon-button` cover current actions. Disabled state belongs on the native element.
- `alert-error`, `alert-success`, and `alert-warning` provide visual status. Add `role="alert"` for errors requiring immediate attention and `role="status"` for polite updates.
- `empty-state`, `code-preview`, and `responsive-table` provide intentional containment for common content.

Use Tailwind utilities for spacing, responsive layout, and unique composition around these primitives. Do not create a component variant for a pattern used once.

## Responsive and content rules

Supported layouts start at 320px and use Tailwind's `sm`, `md`, and `lg` breakpoints. Flex and grid children that contain user or repository content need `min-w-0`. Preserve full identifiers with `break-all`, `break-words`, or `[overflow-wrap:anywhere]`; use truncation only when the complete value remains available through nearby accessible text or a title.

Tables and code blocks should scroll internally. Page-level horizontal scrolling is a defect. Interactive controls should remain at least 44px tall unless they are compact controls within a dense, labeled row.

## Interaction and accessibility

The global `:focus-visible` rule supplies a consistent keyboard indicator. Do not remove it without replacing it with an equally visible state. Every icon-only button needs an accessible name. Loading and success text should use a live status where an update occurs after user action, and errors should use an alert.

Motion must have a `motion-reduce` alternative. Hover treatment cannot be the only indication of state or the only way to discover an action.

## Validation

Run the following before submitting frontend changes:

```sh
cd src/RepoNavAI.Web
npm run lint
npm test
npm run build
npm run test:e2e
```

Check authentication, repository results, endpoint tables, organization management, and theme settings at mobile, tablet, and desktop widths. Test long repository paths, branches, routes, email addresses, code, empty states, failures, loading, disabled controls, and keyboard focus.

The browser suite uses the bundled fixture demo, so it does not require credentials, a database, or a live API. It runs axe against representative light and dark screens, verifies page containment at supported widths, and compares screenshots with committed Linux baselines. When an intentional visual change occurs, review the rendered output and run `npm run test:e2e:update` in the same Linux environment used by CI before committing updated snapshots.

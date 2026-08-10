# Frontend icon conventions

RepoNavAI uses `lucide-react` for interface icons. Lucide is released under the ISC license and its per-icon ES module exports allow Vite to tree-shake unused icons.

## Usage

- Render Lucide icons through `AppIcon` so stroke weight, supported sizes, and accessibility behavior remain consistent.
- Icons are decorative by default and receive `aria-hidden="true"`. Controls must keep a visible label or an `aria-label` on the button or link.
- Pass `label` only when an icon communicates information that is not repeated in nearby text. This gives the SVG an image role and accessible name.
- Use the semantic size scale: `xs` for dense metadata, `sm` for compact actions, `md` for ordinary controls, `lg` for section accents, and `xl` for feature artwork.
- Apply color through `currentColor` utility classes. Do not encode status using an icon or color alone.
- Import individual icons from `lucide-react`; do not import the whole icon catalog or add emoji and text glyphs as interface substitutes.

The current inventory covers branding, application navigation, authentication features, organization management, repository registration and cards, indexing status, assistant results, and the read-only demo. Run `rg "from 'lucide-react'" src/RepoNavAI.Web/src` when reviewing additions.

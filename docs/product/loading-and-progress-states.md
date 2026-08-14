# Loading and progress states

## Decision

Keep current operations inline, progressive, or in the background. No current operation demonstrates a need for a blocking modal. A modal must not be introduced merely to make progress prominent.

Use `OperationState` for shared loading, progress, stopped, timeout, failure, and retry presentation. Status text is authoritative; animation is supplementary and becomes static under reduced-motion preferences. Show only known stages or checkpoints, never an estimated percentage unless the service can provide measured progress.

## Operation inventory

| Operation | Classification | Feedback and recovery |
| --- | --- | --- |
| Session and organization bootstrap | Genuinely blocking bootstrap | Full-page named loading state; API outage changes to a reconnect state with manual retry. The application does not expose conflicting controls before identity and tenant context are known. |
| Sign in, registration, organization creation, rename, invitations, membership changes, favorites, and repository registration | Inline mutation | Disable only the submitting control, keep its label specific, retain surrounding context, and show an inline alert on failure. Retry is the original control after it is restored. |
| Repository list, capabilities, endpoint catalog, and saved orientation retrieval | Non-blocking retrieval | Show an inline named loading state in the result region. Keep navigation available. On timeout or failure, preserve inputs and expose retry where the query supports it. |
| Repository indexing | Background, checkpoint-based progress | Keep repository navigation and other cards responsive. Announce queued or active state, show the latest real checkpoint, and expose Cancel only while the API supports cancellation. Failed, cancelled, and timed-out work retains context and offers Retry. |
| Repository search, cited answer generation, orientation generation, and code-flow analysis | Progressive | Keep the surrounding workspace visible. Replace Ask with Stop while the request owns an abort controller. Stream partial answers where supported; otherwise announce a stable working state without false progress. Stopping restores inputs immediately and retains a stopped notice. |
| Pagination and disclosure | Inline retrieval | Disable only the disclosure control while fetching the next page and preserve already loaded content. Failure leaves the prior page usable. |

## State contract

- **Loading** means retrieval has started but no meaningful stage is available.
- **Progress** names a real stage or checkpoint. It does not infer completion time or percentage.
- **Stopped** confirms a user-supported cancellation and immediately restores controls.
- **Timeout** says that waiting ended without claiming the server stopped. Preserve safe input and allow retry.
- **Failure** includes an actionable explanation when available and places Retry beside the message when retry is safe.
- **Unexpected dismissal** is not a current application path because progress is not placed in dismissible overlays. Navigating away aborts in-flight assistant requests during cleanup; durable indexing continues in the background.

## Accessibility and responsive behavior

- Loading and progress use `role="status"`, polite announcements, and atomic updates. Timeout and failure use `role="alert"`.
- Every state has visible text. Icons are decorative and color is never the only signal.
- Spinners use `motion-reduce:animate-none`; the stable icon and text remain visible.
- State containers wrap long repository names and messages, and actions remain reachable at the 320-pixel minimum viewport.
- Cancellation is rendered only for operations backed by cancellation. Activating it aborts the client request and restores the relevant inputs without waiting for another response.
- Existing screens retain focus because no overlay is introduced. If a future operation proves that a blocking modal is necessary, it must make the page inert, move focus into a labelled dialog, prevent unexpected dismissal, and restore focus to the invoking control.


# ADR-007: Source-grounded repository chat over authenticated SSE

## Status

Accepted

## Context

Developers need answers synthesized from repository evidence rather than only ranked chunks. Responses should begin quickly, remain cancellable, preserve tenant isolation, and expose immutable citations. Browser `EventSource` cannot attach the existing bearer token to a POST request, and model output or indexed source cannot be treated as trusted application data.

## Decision

Application defines a provider-neutral answer-stream port and orchestrates authorization, repository existence, embedding, latest-snapshot retrieval, and citation numbering before generation. Infrastructure uses Semantic Kernel's OpenAI chat-completion connector behind that port. The provider model, context bound, and output-token bound are explicit configuration.

The API accepts an authenticated POST and returns typed server-sent events (`citations`, `delta`, `completed`, or `error`). The React client consumes the response with `fetch` and `ReadableStream`, which supports bearer authentication and `AbortController` cancellation. Provider exceptions after response headers are sent become a sanitized `error` event; preflight authorization and validation errors remain ordinary problem responses.

Retrieved repository content is delimited as untrusted evidence and cannot supply model instructions. The model is told to answer only from evidence and cite numbered sources. Citation URLs are created by the application from indexed metadata and rendered separately; model output is plain text and never interpreted as HTML.

Only operational metadata is persisted: organization, repository, requesting user, model, timestamps, and terminal status. Questions, prompts, retrieved source, and model output are not stored or logged. A configurable rolling 24-hour organization request limit provides an initial cost-control boundary.

## Consequences

- Tenant authorization and retrieval complete before model generation begins.
- Streaming improves time to first content but partial output is harder to moderate than a complete response.
- Cancellation stops browser consumption and propagates through retrieval and provider calls.
- One-shot answers establish the streaming foundation; durable multi-turn conversation history remains separate work.
- Citation correctness and insufficient-evidence behavior require repeatable RAG evaluation fixtures as the feature evolves.

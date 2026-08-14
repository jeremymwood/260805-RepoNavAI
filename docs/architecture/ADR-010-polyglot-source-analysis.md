# ADR-010: Extensible polyglot source analysis

## Status

Accepted

## Decision

Repository acquisition classifies files through a centralized language registry rather than a web-oriented extension allowlist. The initial executable languages are C#, TypeScript, JavaScript, Python, C, C++, Go, Java, Rust, Ruby, Kotlin, shell, PowerShell, and SQL. Documentation and common project formats remain searchable evidence without being described as executable source.

Every acquired snapshot stores aggregate per-language indexed, excluded, binary, and unsupported counts with non-sensitive reason categories. Coverage is `full` when recognized executable source is indexed without recognized-source exclusions, `partial` when some recognized executable source is indexed and some is excluded, and `none` when no executable source is indexed. Unsupported assets do not silently become analyzed content.

All supported UTF-8 text uses the provider-neutral deterministic chunker and embedding pipeline, so Python, C/C++, and the other registered languages are available to semantic search and cited assistant modes. Null-containing and invalid UTF-8 files are treated as binary. Vendor and generated directories are excluded before content is persisted. Archive traversal, link rejection, byte/file limits, cancellation, tenant scoping, and immutable commit citations remain the boundaries defined by ADR-004 and issue #53.

`ISourceSymbolParser` and `IRepositoryEndpointAnalyzer` remain composable analysis ports. The current symbol implementation is intentionally C#-specific; generic semantic evidence must not be presented as AST-level symbols or dependency proof. A future language analyzer registers behind these ports and adds fixtures before enabling language-specific symbol or relationship claims.

## Adding a language

Add its extensions and executable classification to `SourceLanguageRegistry`, add safe text and representative repository fixtures, and verify ingestion, chunking, coverage, exclusion, and citation behavior. Add a specialized parser only when its output can be validated independently of generic text retrieval.

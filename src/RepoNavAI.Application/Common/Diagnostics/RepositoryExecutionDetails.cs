using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using RepoNavAI.Application.Repositories;

namespace RepoNavAI.Application.Common.Diagnostics;

public enum RepositoryResult { Success, Cancelled, Timeout, EmptyEvidence, InvalidStructure, ProviderFailure, HistoryPersistenceFailure, Rejected, Failure }
public enum RepositoryPrompt { AnswerV1, OrientationV1, CodeFlowV1 }
public enum ProviderCallKind { Embedding, Answer, Orientation, CodeFlow }
public enum ProviderUsageStatus { Unknown, Reported, Partial }

// Deliberately no path, source, prompt, response, exception or free-form provider metadata fields.
public sealed record EvidenceCandidate(Guid ChunkId, int Rank, double? Score, string CommitSha, int StartLine, int EndLine);
public sealed record RetrievalManifest(Guid? SnapshotId, string CommitSha, string Version, int Limit,
    int CandidateCount, IReadOnlyList<EvidenceCandidate> Candidates, bool MetadataTruncated,
    int? QueryDimensions, IReadOnlyList<string> IndexedEmbeddingModels);
public sealed record EvidenceSelection(EvidenceCandidate Candidate, int CitationNumber, int ChunkCharacters,
    int ContentOffset, int IncludedCharacters, int? PromptOffset, bool Truncated, bool Omitted,
    int? IncludedStartLine, int? IncludedEndLine, bool EndsMidLine, bool CitationRangeMayOverstate);
public sealed record ContextManifest(RepositoryPrompt Template, int BudgetCharacters, int PromptCharacters,
    int CandidateCount, IReadOnlyList<EvidenceSelection> Evidence, bool MetadataTruncated);
public sealed record ProviderUsage(long? InputTokens, long? OutputTokens, long? TotalTokens)
{
    public static readonly ProviderUsage Unknown = new(null, null, null);
}
public sealed record ProviderCallDetails(ProviderCallKind Kind, string Provider, string Model, bool Streaming,
    double? Temperature, int? MaxOutputTokens, int? EmbeddingDimensions, int InputCount, int InputCharacters,
    double? TimeoutSeconds, int? ObservedTransportAttempts, ProviderUsageStatus UsageStatus, ProviderUsage Usage,
    RepositoryResult Result, double DurationMilliseconds, double? FirstDeltaMilliseconds, bool OutputStarted,
    string ResponseFormat, string ReturnedModel);

public sealed partial class RepositoryRun
{
    internal static readonly JsonSerializerOptions MetadataJson = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };
    public const int MaximumEvidenceEntries = 32;
    public const int MaximumDetailRecords = 64;
    private readonly List<RetrievalManifest> _retrievals = [];
    private readonly List<ContextManifest> _contexts = [];
    private readonly List<ProviderCallDetails> _providerCalls = [];
    private int _providerCallCount;
    private int _generationCallCount;
    private bool _metadataTruncated;
    public RepositorySnapshotReference? ExpectedSnapshot { get; private set; }

    public RepositoryResult? Result { get; private set; }
    public bool HistoryPersistenceFailed { get; private set; }
    public bool OutputStarted { get; private set; }
    public bool ResponseStarted { get; private set; }
    public IReadOnlyList<RetrievalManifest> Retrievals => _retrievals.AsReadOnly();
    public IReadOnlyList<ContextManifest> Contexts => _contexts.AsReadOnly();
    public IReadOnlyList<ProviderCallDetails> ProviderCalls => _providerCalls.AsReadOnly();
    public int ProviderCallCount => _providerCallCount;
    public int GenerationCallCount => _generationCallCount;
    public bool MetadataTruncated => _metadataTruncated;
    public string PipelineVersion => Operation switch
    {
        RepositoryOperation.Search => "semantic-search-v1",
        RepositoryOperation.Answer => "grounded-answer-v1",
        RepositoryOperation.Orientation => "orientation-v1",
        RepositoryOperation.CodeFlow => "code-flow-three-query-v1",
        _ => "not-applicable"
    };

    public void RecordResult(RepositoryResult result)
    {
        if (result == RepositoryResult.HistoryPersistenceFailure) HistoryPersistenceFailed = true;
        // Retain the original cause if a cleanup/history write also fails.
        if (Result is null or RepositoryResult.Success or RepositoryResult.EmptyEvidence) Result = result;
    }

    public void MarkOutputStarted() => OutputStarted = true;
    public void MarkResponseStarted() => ResponseStarted = true;

    public void ExpectSnapshot(RepositorySnapshotReference? snapshot)
    {
        ExpectedSnapshot = snapshot is null ? null : snapshot with { CommitSha = SafeCommit(snapshot.CommitSha) };
        _activity?.SetTag("reponav.snapshot.expected_id", snapshot?.Id.ToString());
        _activity?.SetTag("reponav.snapshot.expected_commit", SafeCommit(snapshot?.CommitSha));
    }

    public void RecordRetrieval(Guid? snapshotId, string? commitSha, int limit, IReadOnlyCollection<SemanticSearchResult> sources,
        int? queryDimensions = null, IEnumerable<string>? indexedModels = null)
    {
        var manifest = new RetrievalManifest(snapshotId, SafeCommit(commitSha), "cosine-latest-v1", limit, sources.Count,
            sources.Take(MaximumEvidenceEntries).Select((source, index) => Candidate(source, index + 1)).ToArray(), sources.Count > MaximumEvidenceEntries,
            queryDimensions, indexedModels?.Select(SafeModel).Distinct().Take(MaximumEvidenceEntries).ToArray() ?? []);
        Retain(_retrievals, manifest);
        _metadataTruncated |= manifest.MetadataTruncated;
        using var span = StartDetailActivity("repository.evidence");
        span?.SetTag("reponav.retrieval.manifest", JsonSerializer.Serialize(manifest, MetadataJson));
    }

    public void RecordContext(ContextManifest manifest)
    {
        Retain(_contexts, manifest);
        _metadataTruncated |= manifest.MetadataTruncated;
        using var span = StartDetailActivity("repository.context");
        span?.SetTag("reponav.context.manifest", JsonSerializer.Serialize(manifest, MetadataJson));
    }

    public ProviderCallScope StartProviderCall(ProviderCallKind kind, string model, bool streaming, int inputCount,
        int inputCharacters, CancellationToken cancellationToken, double? temperature = null, int? maxOutputTokens = null,
        int? dimensions = null, double? timeoutSeconds = null, int? observedTransportAttempts = null) =>
        new(this, kind, model, streaming, inputCount, inputCharacters, cancellationToken, temperature, maxOutputTokens, dimensions, timeoutSeconds, observedTransportAttempts);

    internal Activity? StartDetailActivity(string name)
    {
        var activity = RepositoryDiagnostics.Source.StartActivity(name);
        activity?.SetTag("reponav.run.id", RunId);
        return activity;
    }

    internal void RecordProviderCall(ProviderCallDetails details)
    {
        _providerCallCount++;
        if (details.Kind != ProviderCallKind.Embedding) _generationCallCount++;
        Retain(_providerCalls, details);
    }

    private void Retain<T>(List<T> items, T item)
    {
        if (items.Count < MaximumDetailRecords) items.Add(item);
        else _metadataTruncated = true;
    }

    private void WriteDiagnosticSummary()
    {
        _activity?.SetTag("reponav.result", Result.ToString());
        _activity?.SetTag("reponav.diagnostics.version", "2");
        _activity?.SetTag("reponav.pipeline.version", PipelineVersion);
        _activity?.SetTag("reponav.output.started", OutputStarted);
        _activity?.SetTag("reponav.response.started", ResponseStarted);
        _activity?.SetTag("reponav.history.failed", HistoryPersistenceFailed);
        _activity?.SetTag("reponav.provider.calls", _providerCallCount);
        _activity?.SetTag("reponav.generation.calls", _generationCallCount);
        _activity?.SetTag("reponav.metadata.truncated", _metadataTruncated);
    }

    public static EvidenceCandidate Candidate(SemanticSearchResult source, int rank) =>
        new(source.ChunkId, rank, double.IsFinite(source.Score) ? source.Score : null, SafeCommit(source.CommitSha), source.StartLine, source.EndLine);

    internal static string SafeCommit(string? value) => value is { Length: >= 7 and <= 64 } && value.All(char.IsAsciiHexDigit) ? value : "unknown";
    internal static string SafeModel(string? value) => value is { Length: > 0 and <= 100 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':' or '/') ? value : "unknown";
}

/// <summary>One application invocation, not a guessed count of hidden SDK retries.</summary>
public sealed class ProviderCallScope : IDisposable
{
    private readonly RepositoryRun _run;
    private readonly CancellationToken _token;
    private readonly Activity? _activity;
    private readonly long _started = Stopwatch.GetTimestamp();
    private ProviderCallDetails _details;
    private bool _completed;
    private bool _disposed;

    internal ProviderCallScope(RepositoryRun run, ProviderCallKind kind, string model, bool streaming, int inputCount,
        int inputCharacters, CancellationToken token, double? temperature, int? maxOutputTokens, int? dimensions,
        double? timeoutSeconds, int? attempts)
    {
        _run = run; _token = token;
        _activity = run.StartDetailActivity("repository.provider");
        _details = new(kind, "OpenAI", RepositoryRun.SafeModel(model), streaming, temperature, maxOutputTokens, dimensions,
            inputCount, inputCharacters, timeoutSeconds, attempts, ProviderUsageStatus.Unknown, ProviderUsage.Unknown,
            RepositoryResult.Failure, 0, null, false,
            kind is ProviderCallKind.Orientation or ProviderCallKind.CodeFlow ? "json_object" : kind == ProviderCallKind.Answer ? "text" : "not-applicable", "unknown");
    }

    public void RecordReturnedModel(string? model)
    {
        if (!string.IsNullOrWhiteSpace(model)) _details = _details with { ReturnedModel = RepositoryRun.SafeModel(model) };
    }

    public void RecordUsage(ProviderUsage usage)
    {
        // Only caller-extracted numeric SDK fields cross the boundary. Do not serialize Metadata or InnerContent.
        static long? Valid(long? value) => value is >= 0 ? value : null;
        var safe = new ProviderUsage(Valid(usage.InputTokens), Valid(usage.OutputTokens), Valid(usage.TotalTokens));
        var status = safe.InputTokens.HasValue && safe.TotalTokens.HasValue &&
            (_details.Kind == ProviderCallKind.Embedding || safe.OutputTokens.HasValue) ? ProviderUsageStatus.Reported :
            safe != ProviderUsage.Unknown ? ProviderUsageStatus.Partial : ProviderUsageStatus.Unknown;
        _details = _details with { Usage = safe, UsageStatus = status };
    }

    public void FirstDelta()
    {
        if (_details.FirstDeltaMilliseconds is null)
            _details = _details with { FirstDeltaMilliseconds = Stopwatch.GetElapsedTime(_started).TotalMilliseconds, OutputStarted = true };
    }

    public void Complete() { _completed = true; _details = _details with { Result = RepositoryResult.Success }; }

    public void InvalidStructure()
    {
        _details = _details with { Result = RepositoryResult.InvalidStructure };
        _run.RecordResult(RepositoryResult.InvalidStructure);
    }

    public void Fail(Exception exception)
    {
        if (_details.Result == RepositoryResult.InvalidStructure) return;
        var result = exception switch
        {
            OperationCanceledException when _token.IsCancellationRequested => RepositoryResult.Cancelled,
            OperationCanceledException or TimeoutException => RepositoryResult.Timeout,
            _ => RepositoryResult.ProviderFailure
        };
        _details = _details with { Result = result };
        _run.RecordResult(result);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (!_completed && _token.IsCancellationRequested && _details.Result == RepositoryResult.Failure)
            _details = _details with { Result = RepositoryResult.Cancelled };
        if (_details.Result != RepositoryResult.Success) _run.RecordResult(_details.Result);
        _details = _details with { DurationMilliseconds = Stopwatch.GetElapsedTime(_started).TotalMilliseconds };
        _run.RecordProviderCall(_details);
        _activity?.SetTag("reponav.provider.details", JsonSerializer.Serialize(_details, RepositoryRun.MetadataJson));
        _activity?.SetTag("reponav.result", _details.Result.ToString());
        _activity?.SetStatus(_details.Result is RepositoryResult.Success or RepositoryResult.Cancelled ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        _activity?.Dispose();
    }
}

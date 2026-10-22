using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using FluentValidation;
using RepoNavAI.Application.Common.Exceptions;

namespace RepoNavAI.Application.Common.Diagnostics;

public enum RepositoryOperation { Search, Answer, Orientation, CodeFlow, Register, Reindex, Retry, Indexing }
public enum RepositoryStage { Authorization, Embedding, Retrieval, Generation, Acquisition, HistoryPersistence }
public enum RepositoryOutcome { Running, Completed, Failed, Cancelled, Rejected, Deferred, LeaseLost }

/// <summary>Only typed operations and opaque identifiers may enter this telemetry boundary.</summary>
public static class RepositoryDiagnostics
{
    public const string SourceName = "RepoNavAI.Repository";
    public static readonly ActivitySource Source = new(SourceName);
    private static readonly Meter Meter = new(SourceName);
    private static readonly Counter<long> Runs = Meter.CreateCounter<long>("reponav.repository.runs");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("reponav.repository.duration", "s");
    private static readonly Histogram<double> StageDuration = Meter.CreateHistogram<double>("reponav.repository.stage.duration", "s");
    private static readonly AsyncLocal<RepositoryRun?> Ambient = new();
    public static RepositoryRun? Current => Ambient.Value;

    public static RepositoryRun Start(RepositoryOperation operation, CancellationToken cancellationToken = default)
    {
        var run = new RepositoryRun(operation, cancellationToken, Ambient.Value);
        Ambient.Value = run;
        return run;
    }

    internal static void End(RepositoryRun run, RepositoryRun? previous)
    {
        var tags = new TagList { { "operation", run.Operation.ToString() }, { "outcome", run.Outcome.ToString() } };
        Runs.Add(1, tags);
        Duration.Record(run.Elapsed.TotalSeconds, tags);
        Ambient.Value = previous;
    }

    public static async Task<T> TraceAsync<T>(RepositoryStage stage, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        using var span = new StageScope(stage, cancellationToken);
        try { var result = await action(); span.Complete(); return result; }
        catch (Exception exception)
        {
            if (stage == RepositoryStage.HistoryPersistence &&
                exception is not (ValidationException or UnauthorizedException or ForbiddenException or NotFoundException or ConflictException or RateLimitException) &&
                !(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
                Current?.RecordResult(RepositoryResult.HistoryPersistenceFailure);
            span.Fail(exception); throw;
        }
    }

    public static Task PersistAsync(Func<Task> action, CancellationToken cancellationToken) =>
        TraceAsync(RepositoryStage.HistoryPersistence, async () => { await action(); return true; }, cancellationToken);

    public static Task<T> PersistAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken) =>
        TraceAsync(RepositoryStage.HistoryPersistence, action, cancellationToken);

    public static T ValidateStructure<T>(Func<T> action)
    {
        try { return action(); }
        catch { Current?.RecordResult(RepositoryResult.InvalidStructure); throw; }
    }

    public static async IAsyncEnumerable<T> TraceStream<T>(RepositoryStage stage, IAsyncEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var span = new StageScope(stage, cancellationToken);
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);
        while (true)
        {
            bool next;
            try { next = await enumerator.MoveNextAsync(); }
            catch (Exception exception) { span.Fail(exception); throw; }
            if (!next) { span.Complete(); yield break; }
            yield return enumerator.Current;
        }
    }

    public static string ErrorCategory(Exception exception, CancellationToken cancellationToken) => exception switch
    {
        OperationCanceledException when cancellationToken.IsCancellationRequested => "cancelled",
        OperationCanceledException or TimeoutException => "timeout",
        ValidationException => "validation",
        UnauthorizedException or ForbiddenException => "access",
        NotFoundException => "not_found",
        ConflictException or InvalidOperationException => "conflict",
        RateLimitException => "rate_limit",
        ExternalServiceException or HttpRequestException => "provider",
        _ => "internal"
    };

    private sealed class StageScope : IDisposable
    {
        private readonly RepositoryStage _stage;
        private readonly CancellationToken _token;
        private readonly long _started = Stopwatch.GetTimestamp();
        private readonly RepositoryRun? _run = Current;
        private readonly Activity? _activity;
        private string _outcome = "failed";

        public StageScope(RepositoryStage stage, CancellationToken token)
        {
            _stage = stage; _token = token;
            if (_run is null) return;
            _activity = Source.StartActivity($"repository.{stage.ToString().ToLowerInvariant()}");
            _activity?.SetTag("reponav.run.id", _run.RunId);
        }

        public void Complete() => _outcome = "completed";
        public void Fail(Exception exception)
        {
            var category = _stage == RepositoryStage.HistoryPersistence && _run?.HistoryPersistenceFailed == true ? "persistence" : ErrorCategory(exception, _token);
            _outcome = category == "cancelled" ? "cancelled" : "failed";
            _activity?.SetTag("error.type", category);
        }

        public void Dispose()
        {
            if (_run is null) return;
            if (_token.IsCancellationRequested) _outcome = "cancelled";
            _activity?.SetTag("reponav.outcome", _outcome);
            _activity?.SetStatus(_outcome == "failed" ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
            _activity?.Dispose();
            StageDuration.Record(Stopwatch.GetElapsedTime(_started).TotalSeconds,
                new TagList { { "operation", _run.Operation.ToString() }, { "stage", _stage.ToString() }, { "outcome", _outcome } });
        }
    }
}

public sealed partial class RepositoryRun : IDisposable
{
    private readonly RepositoryRun? _previous;
    private readonly CancellationToken _token;
    private readonly Activity? _activity;
    private readonly long _started = Stopwatch.GetTimestamp();
    private bool _disposed;

    internal RepositoryRun(RepositoryOperation operation, CancellationToken token, RepositoryRun? previous)
    {
        Operation = operation; _token = token; _previous = previous;
        _activity = RepositoryDiagnostics.Source.StartActivity($"repository.{operation.ToString().ToLowerInvariant()}.run");
        TraceId = (_activity?.TraceId ?? Activity.Current?.TraceId ?? ActivityTraceId.CreateRandom()).ToString();
        _activity?.SetTag("reponav.run.id", RunId);
        _activity?.SetTag("reponav.operation", operation.ToString());
    }

    public string RunId { get; } = Guid.NewGuid().ToString("N");
    public string TraceId { get; }
    public RepositoryOperation Operation { get; }
    public RepositoryOutcome Outcome { get; private set; } = RepositoryOutcome.Running;
    public string? ErrorCategory { get; private set; }
    public Guid? JobId { get; private set; }
    public int? Attempt { get; private set; }
    public TimeSpan Elapsed => Stopwatch.GetElapsedTime(_started);

    public void LinkJob(Guid jobId, int? attempt = null)
    {
        JobId = jobId; Attempt = attempt;
        _activity?.SetTag("reponav.job.id", jobId.ToString());
        if (attempt.HasValue) _activity?.SetTag("reponav.job.attempt", attempt.Value);
    }

    public void Finish(RepositoryOutcome outcome)
    {
        if (outcome == RepositoryOutcome.Completed && Result is RepositoryResult.HistoryPersistenceFailure or RepositoryResult.InvalidStructure or RepositoryResult.ProviderFailure or RepositoryResult.Timeout)
            outcome = RepositoryOutcome.Failed;
        if (Outcome == RepositoryOutcome.Running) Outcome = outcome;
        if (outcome == RepositoryOutcome.Cancelled) RecordResult(RepositoryResult.Cancelled);
        if (Result is null) Result = outcome switch
        {
            RepositoryOutcome.Completed => RepositoryResult.Success,
            RepositoryOutcome.Cancelled => RepositoryResult.Cancelled,
            RepositoryOutcome.Rejected => RepositoryResult.Rejected,
            _ => RepositoryResult.Failure
        };
    }

    public void Fail(Exception exception)
    {
        ErrorCategory = Result switch
        {
            RepositoryResult.HistoryPersistenceFailure => "persistence",
            RepositoryResult.InvalidStructure => "invalid_structure",
            RepositoryResult.ProviderFailure => "provider",
            RepositoryResult.Timeout => "timeout",
            _ => RepositoryDiagnostics.ErrorCategory(exception, _token)
        };
        if (!(Result == RepositoryResult.EmptyEvidence && ErrorCategory == "conflict")) RecordResult(ErrorCategory switch
        {
            "cancelled" => RepositoryResult.Cancelled,
            "timeout" => RepositoryResult.Timeout,
            "provider" => RepositoryResult.ProviderFailure,
            "validation" or "access" or "not_found" or "conflict" or "rate_limit" => RepositoryResult.Rejected,
            _ => RepositoryResult.Failure
        });
        Outcome = ErrorCategory switch
        {
            "cancelled" => RepositoryOutcome.Cancelled,
            "validation" or "access" or "not_found" or "conflict" or "rate_limit" => RepositoryOutcome.Rejected,
            _ => RepositoryOutcome.Failed
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Outcome == RepositoryOutcome.Running) Outcome = _token.IsCancellationRequested ? RepositoryOutcome.Cancelled : RepositoryOutcome.Failed;
        Finish(Outcome);
        WriteDiagnosticSummary();
        _activity?.SetTag("reponav.outcome", Outcome.ToString());
        if (ErrorCategory is not null) _activity?.SetTag("error.type", ErrorCategory);
        _activity?.SetStatus(Outcome is RepositoryOutcome.Failed or RepositoryOutcome.LeaseLost ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
        _activity?.Dispose();
        RepositoryDiagnostics.End(this, _previous);
    }
}

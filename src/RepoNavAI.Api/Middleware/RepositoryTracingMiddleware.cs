using RepoNavAI.Application.Common.Diagnostics;
using RepoNavAI.Infrastructure.Observability;

namespace RepoNavAI.Api.Middleware;

[AttributeUsage(AttributeTargets.Method)]
public sealed class TraceRepositoryAttribute(RepositoryOperation operation) : Attribute
{
    public RepositoryOperation Operation { get; } = operation;
}

public sealed class RepositoryTracingMiddleware(RequestDelegate next)
{
    public const string RunIdHeader = "X-RepoNav-Run-Id";

    public async Task InvokeAsync(HttpContext context, RepositoryRunLog log)
    {
        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<TraceRepositoryAttribute>();
        if (metadata is null) { await next(context); return; }

        using var run = RepositoryDiagnostics.Start(metadata.Operation, context.RequestAborted);
        using var scope = log.BeginScope(run);
        context.Response.Headers[RunIdHeader] = run.RunId;
        log.Started(run);
        try
        {
            await next(context);
            run.Finish(context.RequestAborted.IsCancellationRequested ? RepositoryOutcome.Cancelled :
                context.Response.StatusCode >= 500 ? RepositoryOutcome.Failed :
                context.Response.StatusCode >= 400 ? RepositoryOutcome.Rejected : RepositoryOutcome.Completed);
        }
        catch (Exception exception) { run.Fail(exception); throw; }
        finally
        {
            if (context.Response.HasStarted && run.Outcome == RepositoryOutcome.Completed) run.MarkResponseStarted();
            if (run.Outcome == RepositoryOutcome.Running) run.Finish(RepositoryOutcome.Failed);
            log.Finished(run);
        }
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RepoNavAI.Application.Common.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace RepoNavAI.Infrastructure.Observability;

public static class RepositoryTelemetry
{
    public static IServiceCollection AddRepositoryTelemetry(this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        services.AddSingleton<RepositoryRunLog>();
        var endpoint = configuration["Observability:OtlpEndpoint"];
        if (string.IsNullOrWhiteSpace(endpoint)) return services;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/")
            throw new InvalidOperationException("Observability OTLP endpoint must be an HTTP(S) origin without credentials, path, query or fragment.");

        var resource = ResourceBuilder.CreateEmpty().AddService(serviceName);
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.SetResourceBuilder(resource)
                // Intentionally exclude automatic HTTP, SQL and Semantic Kernel sources: they may carry content.
                .AddSource(RepositoryDiagnostics.SourceName)
                .SetSampler(new AlwaysOnSampler())
                .AddOtlpExporter(exporter =>
                {
                    Configure(exporter, new Uri(uri, "v1/traces"));
                    exporter.ExportProcessorType = ExportProcessorType.Batch;
                    exporter.BatchExportProcessorOptions = new BatchExportActivityProcessorOptions
                    { MaxQueueSize = 512, MaxExportBatchSize = 64, ScheduledDelayMilliseconds = 1000, ExporterTimeoutMilliseconds = 2000 };
                }))
            .WithMetrics(metrics => metrics.SetResourceBuilder(resource)
                .AddMeter(RepositoryDiagnostics.SourceName, "RepoNavAI.Indexing.Acquisition", "RepoNavAI.Indexing")
                .AddOtlpExporter((exporter, reader) =>
                {
                    Configure(exporter, new Uri(uri, "v1/metrics"));
                    reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 10000;
                    reader.PeriodicExportingMetricReaderOptions.ExportTimeoutMilliseconds = 2000;
                }));
        return services;
    }

    private static void Configure(OtlpExporterOptions options, Uri endpoint)
    {
        options.Endpoint = endpoint;
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
        options.TimeoutMilliseconds = 1500;
        options.Headers = null;
    }
}

/// <summary>Only this logger can emit records from a protected repository run.</summary>
public sealed class RepositoryRunLog(ILogger<RepositoryRunLog> logger)
{
    public IDisposable? BeginScope(RepositoryRun run) => logger.BeginScope(new Dictionary<string, object>
    { ["RunId"] = run.RunId, ["RepositoryTraceId"] = run.TraceId });

    public void Started(RepositoryRun run) => logger.LogInformation(
        "Repository operation {Operation} started for job {JobId} attempt {Attempt}", run.Operation, run.JobId, run.Attempt);

    public void Finished(RepositoryRun run) => logger.LogInformation(
        "Repository operation {Operation} finished with {Outcome} and result {Result}, category {FailureCategory} in {ElapsedMilliseconds} ms for job {JobId} attempt {Attempt}; provider calls {ProviderCalls}, generation calls {GenerationCalls}, response started {ResponseStarted}, output started {OutputStarted}, history failed {HistoryFailed}",
        run.Operation, run.Outcome, run.Result, run.ErrorCategory, run.Elapsed.TotalMilliseconds, run.JobId, run.Attempt,
        run.ProviderCallCount, run.GenerationCallCount, run.ResponseStarted, run.OutputStarted, run.HistoryPersistenceFailed);
}

/// <summary>Suppress content-bearing third-party/request/exception logs while an AI or indexing run is active.</summary>
public sealed class RepositoryLogFilter : ILogEventFilter
{
    private static readonly HashSet<string> AllowedProperties =
    ["SourceContext", "RunId", "RepositoryTraceId", "Operation", "Outcome", "FailureCategory", "ElapsedMilliseconds", "JobId", "Attempt",
        "Result", "ProviderCalls", "GenerationCalls", "ResponseStarted", "OutputStarted", "HistoryFailed"];

    public bool IsEnabled(LogEvent logEvent)
    {
        var name = logEvent.Properties.TryGetValue("SourceContext", out var source) && source is ScalarValue { Value: string value }
            ? value : string.Empty;
        // These run before/after middleware and can log raw paths, query strings and route values even at Debug.
        if (name.StartsWith("Microsoft.AspNetCore.Hosting.Diagnostics", StringComparison.Ordinal) ||
            name.StartsWith("Microsoft.AspNetCore.Routing", StringComparison.Ordinal)) return false;
        if (RepositoryDiagnostics.Current is null) return true;
        if (logEvent.Exception is not null || name != typeof(RepositoryRunLog).FullName) return false;
        // Framework/log scopes may attach request values even to an otherwise safe summary.
        foreach (var property in logEvent.Properties.Keys.Where(x => !AllowedProperties.Contains(x)).ToArray())
            logEvent.RemovePropertyIfPresent(property);
        return true;
    }
}

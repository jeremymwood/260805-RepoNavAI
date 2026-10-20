using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RepoNavAI.Api.Middleware;
using RepoNavAI.Application.Common.Diagnostics;
using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Application.Organizations;
using RepoNavAI.Application.Repositories;
using RepoNavAI.Infrastructure.Observability;
using RepoNavAI.Infrastructure.Repositories;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace RepoNavAI.Api.IntegrationTests;

// The ActivitySource and Meter are process-wide: isolate listener/export tests from other HTTP fixtures.
[CollectionDefinition("Repository telemetry", DisableParallelization = true)]
public sealed class RepositoryTelemetryCollection;

[Collection("Repository telemetry")]
public sealed class RepositoryTelemetryTests
{
    private const string Canary = "PRIVATE-CANARY-prompt-source-token@example.test";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void RunIds_ExistWithoutListeners_AndRestoreAmbientScope()
    {
        RepositoryDiagnostics.Current.Should().BeNull();
        using (var outer = RepositoryDiagnostics.Start(RepositoryOperation.Search))
        {
            Guid.TryParseExact(outer.RunId, "N", out _).Should().BeTrue();
            outer.TraceId.Should().HaveLength(32);
            using (var nested = RepositoryDiagnostics.Start(RepositoryOperation.Answer))
            {
                nested.RunId.Should().NotBe(outer.RunId);
                RepositoryDiagnostics.Current.Should().BeSameAs(nested);
                nested.Finish(RepositoryOutcome.Completed);
            }
            RepositoryDiagnostics.Current.Should().BeSameAs(outer);
            outer.Finish(RepositoryOutcome.Completed);
        }
        RepositoryDiagnostics.Current.Should().BeNull();
    }

    [Fact]
    public void SamplingOutTraces_DoesNotRemoveRunId()
    {
        using var provider = Sdk.CreateTracerProviderBuilder().AddSource(RepositoryDiagnostics.SourceName)
            .SetSampler(new AlwaysOffSampler()).Build();
        using var run = RepositoryDiagnostics.Start(RepositoryOperation.Search);
        run.RunId.Should().HaveLength(32);
        run.TraceId.Should().HaveLength(32);
        run.Finish(RepositoryOutcome.Completed);
    }

    [Fact]
    public async Task ExportedStages_AreChildrenOfTheRun_AndContainOnlyAllowlistedMetadata()
    {
        using var exporter = new RecordingExporter();
        using var provider = Provider(exporter);
        string runId;
        using (var run = RepositoryDiagnostics.Start(RepositoryOperation.Answer))
        {
            runId = run.RunId;
            foreach (var stage in new[] { RepositoryStage.Authorization, RepositoryStage.Embedding, RepositoryStage.Retrieval })
                await RepositoryDiagnostics.TraceAsync(stage, () => Task.FromResult(Canary), CancellationToken.None);
            await foreach (var _ in RepositoryDiagnostics.TraceStream(RepositoryStage.Generation, Values(), CancellationToken.None)) { }
            run.Finish(RepositoryOutcome.Completed);
        }
        var spans = exporter.Items.ToArray();
        var root = spans.Single(x => x.OperationName == "repository.answer.run");
        root.GetTagItem("reponav.outcome").Should().Be("Completed");
        spans.Should().HaveCount(5);
        spans.Where(x => x != root).Should().OnlyContain(x => x.ParentSpanId == root.SpanId && x.TraceId == root.TraceId);
        spans.Should().OnlyContain(x => (string?)x.GetTagItem("reponav.run.id") == runId);
        AssertSafe(spans);
    }

    [Fact]
    public async Task CancelledStream_ClosesGenerationSpanAndRun()
    {
        using var exporter = new RecordingExporter();
        using var provider = Provider(exporter);
        using var cancellation = new CancellationTokenSource();
        using (var run = RepositoryDiagnostics.Start(RepositoryOperation.Answer, cancellation.Token))
        {
            await foreach (var _ in RepositoryDiagnostics.TraceStream(RepositoryStage.Generation, Values(), cancellation.Token))
            { cancellation.Cancel(); break; }
        }
        exporter.Items.Single(x => x.OperationName == "repository.answer.run").GetTagItem("reponav.outcome").Should().Be("Cancelled");
        exporter.Items.Single(x => x.OperationName == "repository.generation").GetTagItem("reponav.outcome").Should().Be("cancelled");
        RepositoryDiagnostics.Current.Should().BeNull();
    }

    [Fact]
    public void JobId_CorrelatesEnqueueAndAttempts_WithoutReusingRunIds()
    {
        using var exporter = new RecordingExporter();
        using var provider = Provider(exporter);
        var jobId = Guid.NewGuid();
        using (var enqueue = RepositoryDiagnostics.Start(RepositoryOperation.Register))
        { enqueue.LinkJob(jobId); enqueue.Finish(RepositoryOutcome.Completed); }
        using (var first = RepositoryDiagnostics.Start(RepositoryOperation.Indexing))
        { first.LinkJob(jobId, 1); first.Finish(RepositoryOutcome.Deferred); }
        using (var recovered = RepositoryDiagnostics.Start(RepositoryOperation.Indexing))
        { recovered.LinkJob(jobId, 2); recovered.Finish(RepositoryOutcome.Completed); }
        exporter.Items.Should().HaveCount(3).And.OnlyContain(x => (string?)x.GetTagItem("reponav.job.id") == jobId.ToString());
        exporter.Items.Select(x => x.GetTagItem("reponav.run.id")).Should().OnlyHaveUniqueItems();
        exporter.Items.Where(x => x.OperationName == "repository.indexing.run").Select(x => x.GetTagItem("reponav.job.attempt")).Should().BeEquivalentTo(new object[] { 1, 2 });
    }

    [Fact]
    public async Task Metrics_UseBoundedDimensions_NotRunOrJobIdentifiers()
    {
        var measurements = new ConcurrentBag<KeyValuePair<string, object?>[]>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        { if (instrument.Meter.Name == RepositoryDiagnostics.SourceName) meterListener.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) => measurements.Add(tags.ToArray()));
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) => measurements.Add(tags.ToArray()));
        listener.Start();
        using (var run = RepositoryDiagnostics.Start(RepositoryOperation.Search))
        {
            run.LinkJob(Guid.NewGuid(), 1);
            await RepositoryDiagnostics.TraceAsync(RepositoryStage.Retrieval, () => Task.FromResult(Canary), CancellationToken.None);
            run.Finish(RepositoryOutcome.Completed);
        }
        measurements.Should().HaveCount(3);
        measurements.SelectMany(x => x).Should().OnlyContain(x => x.Key == "operation" || x.Key == "outcome" || x.Key == "stage");
        measurements.SelectMany(x => x).Select(x => x.Value?.ToString()).Should().OnlyContain(x => x == "Search" || x == "Retrieval" || x == "completed" || x == "Completed");
    }

    [Fact]
    public async Task ProviderFailure_NeverExportsRequestResponseOrExceptionContent()
    {
        using var exporter = new RecordingExporter();
        using var provider = Provider(exporter);
        using var client = new HttpClient(new FailureHandler()) { BaseAddress = new Uri("https://provider.invalid/") };
        var embeddings = new OpenAIEmbeddingGenerator(client, Options.Create(new OpenAIOptions { ApiKey = Canary }));
        using (var run = RepositoryDiagnostics.Start(RepositoryOperation.Search))
        {
            try { await embeddings.GenerateAsync([Canary], CancellationToken.None); }
            catch (ExternalServiceException exception) { run.Fail(exception); }
        }
        exporter.Items.Single(x => x.OperationName == "repository.embedding").GetTagItem("error.type").Should().Be("provider");
        exporter.Items.Single(x => x.OperationName == "repository.search.run").Status.Should().Be(ActivityStatusCode.Error);
        AssertSafe(exporter.Items);
    }

    [Fact]
    public void LogBoundary_AllowsOnlySafeRunSummaries_AndBlocksEarlyFrameworkContent()
    {
        var sink = new RecordingSink();
        using var serilog = new LoggerConfiguration().MinimumLevel.Verbose().Enrich.FromLogContext()
            .Filter.With<RepositoryLogFilter>().WriteTo.Sink(sink).CreateLogger();
        using var factory = new Serilog.Extensions.Logging.SerilogLoggerFactory(serilog);
        using var unsafeAmbientScope = Serilog.Context.LogContext.PushProperty("RequestPath", Canary);
        var runLog = new RepositoryRunLog(factory.CreateLogger<RepositoryRunLog>());
        serilog.ForContext("SourceContext", "Microsoft.AspNetCore.Hosting.Diagnostics").Information("Request {Query}", Canary);
        serilog.ForContext("SourceContext", "Microsoft.AspNetCore.Routing.EndpointRoutingMiddleware").Debug("Route {Value}", Canary);
        using (var run = RepositoryDiagnostics.Start(RepositoryOperation.Answer))
        using (runLog.BeginScope(run))
        {
            runLog.Started(run);
            foreach (var category in new[] { "System.Net.Http.HttpClient", "Microsoft.EntityFrameworkCore.Database.Command", "Microsoft.SemanticKernel", "RepoNavAI.Api.Middleware.ExceptionHandlingMiddleware" })
                serilog.ForContext("SourceContext", category).Error(new Exception(Canary), "Unsafe body {Body}", Canary);
            run.Fail(new HttpRequestException(Canary));
            runLog.Finished(run);
        }
        sink.Items.Should().HaveCount(2).And.OnlyContain(x => x.Exception == null);
        sink.Items.Should().OnlyContain(x => x.Properties.ContainsKey("RunId") && x.Properties.ContainsKey("RepositoryTraceId"));
        string.Join('\n', sink.Items.Select(x => x.RenderMessage() + string.Join(',', x.Properties))).Should().NotContain(Canary);
    }

    [Theory]
    [InlineData("semantic-search?query=PRIVATE-CANARY-prompt-source-token%40example.test", "Search")]
    [InlineData("chat", "Answer")]
    [InlineData("orientation-plan", "Orientation")]
    [InlineData("code-flow", "CodeFlow")]
    public async Task DeniedRequests_HaveOpaqueIds_AndStopBeforeProviderWork(string route, string operation)
    {
        using var exporter = new RecordingExporter();
        using var provider = Provider(exporter);
        await using var factory = new OrganizationApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(RepositoryTracingMiddleware.RunIdHeader, Canary);
        var url = $"/api/organizations/{Guid.NewGuid()}/repositories/{Guid.NewGuid()}/{route}";
        using var response = operation == "Search" ? await client.GetAsync(url) :
            await client.PostAsJsonAsync(url, new { question = Canary, objective = Canary, role = 0, experience = 0, focus = 0, timeBudgetMinutes = 30 });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var id = response.Headers.GetValues(RepositoryTracingMiddleware.RunIdHeader).Single();
        Guid.TryParseExact(id, "N", out _).Should().BeTrue();
        var spans = exporter.Items.Where(x => (string?)x.GetTagItem("reponav.run.id") == id).ToArray();
        var root = spans.Single(x => x.OperationName.EndsWith(".run", StringComparison.Ordinal));
        root.GetTagItem("reponav.operation").Should().Be(operation);
        root.GetTagItem("reponav.outcome").Should().Be("Rejected");
        spans.Should().NotContain(x => x.OperationName == "repository.embedding" || x.OperationName == "repository.retrieval" || x.OperationName == "repository.generation");
        AssertSafe(spans);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnswerStream_ReportsSuccessOrFailure_EvenWhenHttpStatusIs200(bool fail)
    {
        using var exporter = new RecordingExporter();
        using var provider = Provider(exporter);
        await using var baseFactory = new OrganizationApiFactory();
        await using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRepositoryAnswerGenerator>();
            services.AddSingleton<IRepositoryAnswerGenerator>(new AnswerGenerator(fail));
        }));
        using var client = factory.CreateClient();
        using var organizationResponse = await client.PostAsJsonAsync("/api/organizations", new { name = "Telemetry test" });
        var organization = (await organizationResponse.Content.ReadFromJsonAsync<OrganizationSummary>(JsonOptions))!;
        using var repositoryResponse = await client.PostAsJsonAsync($"/api/organizations/{organization.Id}/repositories", new { url = "https://github.com/acme/test" });
        var repository = (await repositoryResponse.Content.ReadFromJsonAsync<RepositoryDto>(JsonOptions))!;
        using var response = await client.PostAsJsonAsync($"/api/organizations/{organization.Id}/repositories/{repository.Id}/chat", new { question = Canary });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(fail ? "event: error" : "event: completed");
        if (fail) body.Should().NotContain(Canary);
        var id = response.Headers.GetValues(RepositoryTracingMiddleware.RunIdHeader).Single();
        id.Should().NotBe(repositoryResponse.Headers.GetValues(RepositoryTracingMiddleware.RunIdHeader).Single());
        var spans = exporter.Items.Where(x => (string?)x.GetTagItem("reponav.run.id") == id).ToArray();
        spans.Single(x => x.OperationName == "repository.answer.run").GetTagItem("reponav.outcome").Should().Be(fail ? "Failed" : "Completed");
        spans.Should().Contain(x => x.OperationName == "repository.generation");
        AssertSafe(spans);
    }

    [Fact]
    public async Task UnavailableExporter_DoesNotBreakOperations_AndDoesNotSubscribeToUnsafeSources()
    {
        using var exporter = new RecordingExporter();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Observability:OtlpEndpoint"] = "http://127.0.0.1:1" }).Build();
        using var host = new HostBuilder().ConfigureServices(services =>
        {
            services.AddRepositoryTelemetry(configuration, "RepoNavAI.Test");
            services.AddOpenTelemetry().WithTracing(tracing => tracing.AddProcessor(new SimpleActivityExportProcessor(exporter)));
        }).Build();
        await host.StartAsync();
        using (var unsafeSource = new ActivitySource("System.Net.Http"))
        using (var unsafeSpan = unsafeSource.StartActivity(Canary))
        using (var run = RepositoryDiagnostics.Start(RepositoryOperation.Search))
        {
            await RepositoryDiagnostics.TraceAsync(RepositoryStage.Retrieval, () => Task.FromResult(1), CancellationToken.None);
            run.Finish(RepositoryOutcome.Completed);
        }
        var provider = host.Services.GetRequiredService<TracerProvider>();
        provider.ForceFlush(3000); // No collector is listening; failure is owned by the exporter, not the operation.
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StopAsync(deadline.Token);
        exporter.Items.Should().HaveCount(2).And.OnlyContain(x => x.Source.Name == RepositoryDiagnostics.SourceName);
        AssertSafe(exporter.Items);
    }

    [Theory]
    [InlineData("https://token@collector.invalid")]
    [InlineData("https://collector.invalid?key=secret")]
    [InlineData("https://collector.invalid/v1/traces")]
    [InlineData("file:///tmp/traces")]
    public void InvalidExporterOrigins_AreRejectedWithoutEchoingSecrets(string endpoint)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Observability:OtlpEndpoint"] = endpoint }).Build();
        var action = () => new ServiceCollection().AddRepositoryTelemetry(config, "RepoNavAI.Test");
        action.Should().Throw<InvalidOperationException>().Which.Message.Should().NotContain(endpoint);
    }

    private static TracerProvider Provider(RecordingExporter exporter) => Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService("RepoNavAI.Test"))
        .AddSource(RepositoryDiagnostics.SourceName).SetSampler(new AlwaysOnSampler())
        .AddProcessor(new SimpleActivityExportProcessor(exporter)).Build();

    private static void AssertSafe(IEnumerable<Activity> spans)
    {
        string[] allowed = ["reponav.run.id", "reponav.operation", "reponav.outcome", "reponav.job.id", "reponav.job.attempt", "error.type"];
        foreach (var span in spans)
        {
            span.TagObjects.Should().OnlyContain(x => allowed.Contains(x.Key));
            span.Events.Should().BeEmpty();
            span.StatusDescription.Should().BeNull();
            string.Join(',', span.TagObjects).Should().NotContain(Canary);
            span.DisplayName.Should().NotContain(Canary);
        }
    }

    private static async IAsyncEnumerable<string> Values()
    { await Task.Yield(); yield return Canary; }

    private sealed class RecordingExporter : BaseExporter<Activity>
    {
        public ConcurrentBag<Activity> Items { get; } = [];
        public override ExportResult Export(in Batch<Activity> batch)
        { foreach (var item in batch) Items.Add(item); return ExportResult.Success; }
    }

    private sealed class RecordingSink : ILogEventSink
    {
        public List<LogEvent> Items { get; } = [];
        public void Emit(LogEvent logEvent) => Items.Add(logEvent);
    }

    private sealed class FailureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent(Canary) });
    }

    private sealed class AnswerGenerator(bool fail) : IRepositoryAnswerGenerator
    {
        public bool IsConfigured => true;
        public string Model => "test-model";
        public async IAsyncEnumerable<string> StreamAsync(string question, IReadOnlyCollection<SemanticSearchResult> sources,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return "Partial answer [1].";
            if (fail) throw new HttpRequestException(Canary);
        }
    }
}

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;
using RepoNavAI.Application.Common.Diagnostics;
using RepoNavAI.Application.Common.Exceptions;
using RepoNavAI.Application.Repositories;
using RepoNavAI.Domain.Repositories;
using RepoNavAI.Infrastructure.Repositories;
using Xunit;

namespace RepoNavAI.Api.IntegrationTests;

[Collection("Repository telemetry")]
public sealed class RepositoryExecutionDetailsTests
{
    private const string Canary = "PRIVATE-CANARY-source-prompt-response@example.test";
    private const string Commit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly IOptions<OpenAIOptions> Options = Microsoft.Extensions.Options.Options.Create(new OpenAIOptions { ApiKey = "test-only", EmbeddingDimensions = 3 });
    private static readonly OrientationProfile Profile = new(OrientationRole.Developer, OrientationExperience.Senior, OrientationFocus.Architecture, 30, Canary);

    [Theory]
    [InlineData(RepositoryPrompt.AnswerV1)]
    [InlineData(RepositoryPrompt.OrientationV1)]
    [InlineData(RepositoryPrompt.CodeFlowV1)]
    public void Context_RecordsExactPromptOffsets_TruncationAndOmittedSources(RepositoryPrompt template)
    {
        using var run = RepositoryDiagnostics.Start(RepositoryOperation.Answer);
        var sources = new[] { Source("File: private.cs\nline one\n" + new string('x', 5000)), Source(Canary) };
        var prompt = Build(template, sources, 1000);
        var manifest = run.Contexts.Should().ContainSingle().Subject;
        manifest.Template.Should().Be(template);
        manifest.PromptCharacters.Should().Be(prompt.Length);
        manifest.CandidateCount.Should().Be(2);
        var first = manifest.Evidence[0];
        first.Truncated.Should().BeTrue();
        first.Omitted.Should().BeFalse();
        first.EndsMidLine.Should().BeTrue();
        first.IncludedStartLine.Should().Be(10);
        first.IncludedEndLine.Should().Be(11);
        first.CitationRangeMayOverstate.Should().BeTrue();
        prompt.Substring(first.PromptOffset!.Value, first.IncludedCharacters).Should().Be(sources[0].Content[..first.IncludedCharacters]);
        first.Candidate.ChunkId.Should().Be(sources[0].ChunkId);
        first.Candidate.CommitSha.Should().Be(Commit);
        manifest.Evidence[1].Omitted.Should().BeTrue();
        manifest.Evidence[1].PromptOffset.Should().BeNull();
        manifest.Evidence[1].IncludedCharacters.Should().Be(0);
        JsonSerializer.Serialize(manifest).Should().NotContain(Canary).And.NotContain("private.cs").And.NotContain("line one");
    }

    [Theory]
    [InlineData(RepositoryPrompt.AnswerV1)]
    [InlineData(RepositoryPrompt.OrientationV1)]
    [InlineData(RepositoryPrompt.CodeFlowV1)]
    public void OversizedHeading_PreservesExistingStopSelection_AndReportsLaterCandidatesAsOmitted(RepositoryPrompt template)
    {
        using var run = RepositoryDiagnostics.Start(RepositoryOperation.Answer);
        var prompt = Build(template, [Source(Canary) with { Path = new string('p', 2000) }, Source("SMALL-SOURCE")], 1000);
        run.Contexts.Single().Evidence.Should().OnlyContain(x => x.Omitted && x.IncludedCharacters == 0);
        prompt.Should().NotContain("<repository_evidence>").And.NotContain("SMALL-SOURCE");
    }

    [Fact]
    public void ChunkerTruncation_IsNotMisrepresentedAsFullCitationRange()
    {
        var chunk = new SourceChunker().Chunk("private.cs", string.Join('\n', Enumerable.Repeat(new string('x', 1000), 100))).First();
        using var run = RepositoryDiagnostics.Start(RepositoryOperation.Answer);
        var source = Source(chunk.Content) with { StartLine = chunk.StartLine, EndLine = chunk.EndLine };
        Build(RepositoryPrompt.AnswerV1, [source], 32_000);
        var entry = run.Contexts.Single().Evidence.Single();
        entry.Truncated.Should().BeFalse("the prompt copied all of this already-truncated chunk");
        entry.CitationRangeMayOverstate.Should().BeTrue();
        entry.IncludedEndLine.Should().BeLessThan(source.EndLine);
    }

    [Fact]
    public void ContextConstruction_IsByteForByteCompatibleWithExistingAnswerPrompt()
    {
        var source = Source("one\ntwo");
        var expected = new StringBuilder().AppendLine("Developer question:").AppendLine(Canary).AppendLine().AppendLine("Repository evidence:")
            .Append($"\n[1] {source.Path}:{source.StartLine}-{source.EndLine} at commit {source.CommitSha}\n<repository_evidence>\n")
            .Append(source.Content).Append("\n</repository_evidence>\n")
            .AppendLine("\nAnswer the developer question using only this evidence and cite claims as [n].").ToString();
        Build(RepositoryPrompt.AnswerV1, [source], 32_000).Should().Be(expected);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActualPinnedConnector_ReadsUsageOnlyChunk_OrKeepsUnknown(bool includeUsage)
    {
        using var handler = new StreamingHandler(includeUsage);
        using var client = new HttpClient(handler);
        var service = new OpenAIChatCompletionService("gpt-4.1-mini", "test-only", httpClient: client);
        var generator = new SemanticKernelRepositoryAnswerGenerator(service, Options);
        using var run = RepositoryDiagnostics.Start(RepositoryOperation.Answer);
        var answer = new StringBuilder();
        await foreach (var delta in generator.StreamAsync(Canary, [Source(Canary)], CancellationToken.None)) answer.Append(delta);
        answer.ToString().Should().Be("Answer [1].");
        var call = run.ProviderCalls.Single();
        call.FirstDeltaMilliseconds.Should().NotBeNull();
        call.DurationMilliseconds.Should().BeGreaterThanOrEqualTo(call.FirstDeltaMilliseconds!.Value);
        call.Result.Should().Be(RepositoryResult.Success);
        call.ObservedTransportAttempts.Should().BeNull();
        call.UsageStatus.Should().Be(includeUsage ? ProviderUsageStatus.Reported : ProviderUsageStatus.Unknown);
        call.Usage.Should().Be(includeUsage ? new ProviderUsage(13, 4, 17) : ProviderUsage.Unknown);
        using var body = JsonDocument.Parse(handler.RequestBody!);
        body.RootElement.GetProperty("stream_options").GetProperty("include_usage").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("temperature").GetDouble().Should().Be(call.Temperature);
        run.GenerationCallCount.Should().Be(1);
        run.Contexts.Single().PromptCharacters.Should().Be(body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelBeforeOrAfterFirstDelta_IsNotSuccess_AndDoesNotInventUsage(bool afterDelta)
    {
        using var cancellation = new CancellationTokenSource();
        using var run = RepositoryDiagnostics.Start(RepositoryOperation.Answer, cancellation.Token);
        var generator = new SemanticKernelRepositoryAnswerGenerator(new FakeCompletion(), Options);
        if (!afterDelta) cancellation.Cancel();
        var action = async () =>
        {
            await foreach (var _ in generator.StreamAsync(Canary, [Source(Canary)], cancellation.Token)) cancellation.Cancel();
        };
        await action.Should().ThrowAsync<OperationCanceledException>();
        var call = run.ProviderCalls.Single();
        call.Result.Should().Be(RepositoryResult.Cancelled);
        call.UsageStatus.Should().Be(ProviderUsageStatus.Unknown);
        call.Usage.Should().Be(ProviderUsage.Unknown);
        call.OutputStarted.Should().Be(afterDelta);
        if (afterDelta) call.FirstDeltaMilliseconds.Should().NotBeNull();
        else call.FirstDeltaMilliseconds.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StructuredGenerators_CaptureTypedUsageAndSettings(bool orientation)
    {
        var fake = new FakeCompletion { Response = """{"summary":"ok","steps":[],"missingEvidence":[]}""", IncludeUsage = true };
        using var run = RepositoryDiagnostics.Start(orientation ? RepositoryOperation.Orientation : RepositoryOperation.CodeFlow);
        if (orientation) await Orientation(fake).GenerateAsync(Profile, [Source(Canary)], CancellationToken.None);
        else await Flow(fake).GenerateAsync(Canary, [Source(Canary)], CancellationToken.None);
        var call = run.ProviderCalls.Single();
        call.Result.Should().Be(RepositoryResult.Success);
        call.Usage.Should().Be(new ProviderUsage(11, 7, 18));
        call.Temperature.Should().Be(0.1);
        call.MaxOutputTokens.Should().Be(orientation ? 2400 : Options.Value.CodeFlowMaxOutputTokens);
        call.FirstDeltaMilliseconds.Should().BeNull();
        call.Streaming.Should().BeFalse();
        call.ObservedTransportAttempts.Should().BeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvalidJson_HasDistinctOutcome_AndDoesNotExposeResponse(bool orientation)
    {
        using var capture = new ActivityCapture();
        using (var run = RepositoryDiagnostics.Start(orientation ? RepositoryOperation.Orientation : RepositoryOperation.CodeFlow))
        {
            var fake = new FakeCompletion { Response = Canary };
            var action = async () =>
            {
                if (orientation) await Orientation(fake).GenerateAsync(Profile, [Source(Canary)], CancellationToken.None);
                else await Flow(fake).GenerateAsync(Canary, [Source(Canary)], CancellationToken.None);
            };
            await action.Should().ThrowAsync<ExternalServiceException>();
            run.Result.Should().Be(RepositoryResult.InvalidStructure);
            run.ProviderCalls.Single().Result.Should().Be(RepositoryResult.InvalidStructure);
        }
        capture.AssertNoContent();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TimeoutAndProviderFailure_AreDistinct(bool timeout)
    {
        using var capture = new ActivityCapture();
        using (var run = RepositoryDiagnostics.Start(RepositoryOperation.CodeFlow))
        {
            var action = () => Flow(new FakeCompletion { Failure = timeout ? new OperationCanceledException(Canary) : new HttpRequestException(Canary) })
                .GenerateAsync(Canary, [Source(Canary)], CancellationToken.None);
            await action.Should().ThrowAsync<Exception>();
            var expected = timeout ? RepositoryResult.Timeout : RepositoryResult.ProviderFailure;
            run.Result.Should().Be(expected);
            run.ProviderCalls.Single().Result.Should().Be(expected);
            run.ProviderCalls.Single().FirstDeltaMilliseconds.Should().BeNull();
        }
        capture.AssertNoContent();
    }

    [Theory]
    [InlineData("{\"prompt_tokens\":9,\"total_tokens\":9}", ProviderUsageStatus.Reported)]
    [InlineData("{\"prompt_tokens\":9}", ProviderUsageStatus.Partial)]
    [InlineData("null", ProviderUsageStatus.Unknown)]
    public async Task Embeddings_CaptureReportedBatchUsage_AndDistinguishMissingFields(string usage, ProviderUsageStatus status)
    {
        using var handler = new JsonHandler("{\"data\":[{\"index\":0,\"embedding\":[1,0,0]}],\"usage\":" + usage + "}");
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://provider.invalid/") };
        using var run = RepositoryDiagnostics.Start(RepositoryOperation.Indexing);
        await new OpenAIEmbeddingGenerator(client, Options).GenerateAsync([Canary], CancellationToken.None);
        var call = run.ProviderCalls.Single();
        call.Kind.Should().Be(ProviderCallKind.Embedding);
        call.UsageStatus.Should().Be(status);
        call.Usage.OutputTokens.Should().BeNull("embeddings do not report completion tokens");
        call.Usage.TotalTokens.Should().Be(status == ProviderUsageStatus.Reported ? 9 : null);
        call.EmbeddingDimensions.Should().Be(3);
        call.InputCount.Should().Be(1);
        call.InputCharacters.Should().Be(Canary.Length);
        call.ObservedTransportAttempts.Should().BeNull("SendAsync count does not establish the number of transport attempts");
        run.ProviderCallCount.Should().Be(1);
        run.GenerationCallCount.Should().Be(0);
    }

    [Fact]
    public async Task FailedHistoryWrite_IsDistinct_WithoutOverwritingOriginalProviderFailure()
    {
        using var run = RepositoryDiagnostics.Start(RepositoryOperation.Answer);
        var write = () => RepositoryDiagnostics.PersistAsync(() => Task.FromException(new InvalidOperationException(Canary)), CancellationToken.None);
        await write.Should().ThrowAsync<InvalidOperationException>();
        run.Result.Should().Be(RepositoryResult.HistoryPersistenceFailure);
        run.HistoryPersistenceFailed.Should().BeTrue();
        run.Finish(RepositoryOutcome.Completed);
        run.Outcome.Should().Be(RepositoryOutcome.Failed, "a normal return must not hide a history-storage failure");
        using var second = RepositoryDiagnostics.Start(RepositoryOperation.Answer);
        second.RecordResult(RepositoryResult.ProviderFailure);
        await write.Should().ThrowAsync<InvalidOperationException>();
        second.Result.Should().Be(RepositoryResult.ProviderFailure);
        second.HistoryPersistenceFailed.Should().BeTrue();
    }

    [Fact]
    public void EmptyEvidence_RemainsDistinctFromSuccessAndConflict()
    {
        using var run = RepositoryDiagnostics.Start(RepositoryOperation.Orientation);
        run.RecordResult(RepositoryResult.EmptyEvidence);
        run.Fail(new ConflictException(Canary));
        run.Result.Should().Be(RepositoryResult.EmptyEvidence);
        using var search = RepositoryDiagnostics.Start(RepositoryOperation.Search);
        search.RecordResult(RepositoryResult.EmptyEvidence);
        search.Finish(RepositoryOutcome.Completed);
        search.Result.Should().Be(RepositoryResult.EmptyEvidence);
    }

    [Fact]
    public async Task QuotaRejection_IsNotMisclassifiedAsHistoryPersistenceFailure()
    {
        using var run = RepositoryDiagnostics.Start(RepositoryOperation.Answer);
        var action = () => RepositoryDiagnostics.PersistAsync(() => Task.FromException(new RateLimitException("Quota reached.")), CancellationToken.None);
        var exception = (await action.Should().ThrowAsync<RateLimitException>()).Which;
        run.Fail(exception);
        run.Result.Should().Be(RepositoryResult.Rejected);
        run.Outcome.Should().Be(RepositoryOutcome.Rejected);
        run.HistoryPersistenceFailed.Should().BeFalse();
    }

    [Fact]
    public void MetadataIsBounded_WithoutChangingSourceSelection_AndRetainedWithoutExporter()
    {
        using var run = RepositoryDiagnostics.Start(RepositoryOperation.Answer);
        var sources = Enumerable.Range(0, 40).Select(_ => Source("x")).ToArray();
        var prompt = Build(RepositoryPrompt.AnswerV1, sources, 32_000);
        prompt.Should().Contain("[40]");
        run.Contexts.Single().Evidence.Should().HaveCount(RepositoryRun.MaximumEvidenceEntries);
        run.Contexts.Single().MetadataTruncated.Should().BeTrue();
        for (var i = 0; i < 70; i++) run.RecordRetrieval(Guid.NewGuid(), Commit, 10, sources);
        run.Retrievals.Should().HaveCount(RepositoryRun.MaximumDetailRecords);
        run.MetadataTruncated.Should().BeTrue();
    }

    [Fact]
    public void RetrievalAndContextMetadata_ExcludePathsAndUnsafeFields_AndExposeSnapshotMismatch()
    {
        using var capture = new ActivityCapture();
        using (var run = RepositoryDiagnostics.Start(RepositoryOperation.Answer))
        {
            var expected = new RepositorySnapshotReference(Guid.NewGuid(), Commit);
            run.ExpectSnapshot(expected);
            var source = Source(Canary) with { Path = Canary, CommitSha = new string('b', 40) };
            run.RecordRetrieval(Guid.NewGuid(), source.CommitSha, 8, [source], 512, ["text-embedding-3-small"]);
            Build(RepositoryPrompt.AnswerV1, [source], 32_000);
            run.ExpectedSnapshot.Should().Be(expected);
            run.Retrievals.Single().CommitSha.Should().NotBe(expected.CommitSha);
            run.Contexts.Single().Evidence.Single().Candidate.CommitSha.Should().Be(source.CommitSha);
            run.Finish(RepositoryOutcome.Completed);
        }
        capture.AssertNoContent();
    }

    private static SemanticSearchResult Source(string content) => new(Guid.NewGuid(), "private.cs", 10, 20, content, .9, Commit, "https://private.invalid/" + Canary);
    private static string Build(RepositoryPrompt template, IReadOnlyCollection<SemanticSearchResult> sources, int budget) => template switch
    {
        RepositoryPrompt.AnswerV1 => SemanticKernelRepositoryAnswerGenerator.BuildGroundedQuestion(Canary, sources, budget),
        RepositoryPrompt.OrientationV1 => SemanticKernelRepositoryOrientationGenerator.BuildRequest(Profile, sources, budget),
        _ => SemanticKernelRepositoryCodeFlowGenerator.BuildRequest(Canary, sources, budget)
    };
    private static SemanticKernelRepositoryOrientationGenerator Orientation(IChatCompletionService service) => new(service, Options, NullLogger<SemanticKernelRepositoryOrientationGenerator>.Instance);
    private static SemanticKernelRepositoryCodeFlowGenerator Flow(IChatCompletionService service) => new(service, Options, NullLogger<SemanticKernelRepositoryCodeFlowGenerator>.Instance);

    private sealed class FakeCompletion : IChatCompletionService
    {
        public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();
        public string Response { get; init; } = "{}";
        public bool IncludeUsage { get; init; }
        public Exception? Failure { get; init; }
        public Task<IReadOnlyList<Microsoft.SemanticKernel.ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
        {
            if (Failure is not null) throw Failure;
            IReadOnlyDictionary<string, object?> metadata = IncludeUsage ? new Dictionary<string, object?>
            {
                ["Usage"] = OpenAIChatModelFactory.ChatTokenUsage(inputTokenCount: 11, outputTokenCount: 7, totalTokenCount: 18),
                ["Unsafe"] = Canary
            } : new Dictionary<string, object?>();
            return Task.FromResult<IReadOnlyList<Microsoft.SemanticKernel.ChatMessageContent>>([new(AuthorRole.Assistant, Response, metadata: metadata)]);
        }
        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, Canary);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private sealed class StreamingHandler(bool includeUsage) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            const string first = """{"id":"chat-test","object":"chat.completion.chunk","created":1,"model":"gpt-4.1-mini","choices":[{"index":0,"delta":{"role":"assistant","content":"Answer [1]."},"finish_reason":null}]}""";
            const string last = """{"id":"chat-test","object":"chat.completion.chunk","created":1,"model":"gpt-4.1-mini","choices":[],"usage":{"prompt_tokens":13,"completion_tokens":4,"total_tokens":17}}""";
            var body = "data: " + first + "\n\n" + (includeUsage ? "data: " + last + "\n\n" : string.Empty) + "data: [DONE]\n\n";
            var content = new StreamContent(new NonSeekableStream(Encoding.UTF8.GetBytes(body)));
            content.Headers.ContentType = new("text/event-stream");
            return new(HttpStatusCode.OK) { Content = content };
        }
    }

    // Match an HTTP response stream; the pinned SDK must not try to rewind an in-memory StringContent stream.
    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
    }

    private sealed class JsonHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    private sealed class ActivityCapture : IDisposable
    {
        private readonly ConcurrentBag<Activity> _activities = [];
        private readonly ActivityListener _listener;
        public ActivityCapture()
        {
            _listener = new()
            {
                ShouldListenTo = source => source.Name == RepositoryDiagnostics.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = _activities.Add
            };
            ActivitySource.AddActivityListener(_listener);
        }
        public void AssertNoContent()
        {
            _activities.Should().NotBeEmpty();
            var text = string.Join('\n', _activities.Select(activity => activity.DisplayName + string.Join(',', activity.TagObjects)));
            text.Should().NotContain(Canary).And.NotContain("private.cs").And.NotContain("private.invalid");
            _activities.Should().OnlyContain(activity => !activity.Events.Any() && activity.StatusDescription == null);
        }
        public void Dispose() => _listener.Dispose();
    }
}

using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using RepoNavAI.Infrastructure.Repositories;
using Xunit;

namespace RepoNavAI.Api.IntegrationTests;

public sealed class SemanticSearchTests
{
    [Fact]
    public void Chunker_PreservesLinesWithDeterministicOverlap()
    {
        var content = string.Join('\n', Enumerable.Range(1, 250).Select(x => $"line {x}"));
        var chunks = new SourceChunker().Chunk("src/Service.cs", content).ToArray();
        chunks.Should().HaveCount(3);
        chunks[0].StartLine.Should().Be(1); chunks[0].EndLine.Should().Be(120);
        chunks[1].StartLine.Should().Be(101); chunks[1].EndLine.Should().Be(220);
        chunks[2].StartLine.Should().Be(201); chunks[2].EndLine.Should().Be(250);
        chunks.Should().OnlyHaveUniqueItems(x => x.Ordinal);
    }

    [Fact]
    public async Task EmbeddingGenerator_SendsConfiguredModelAndDimensions()
    {
        var handler = new RecordingHandler();
        var options = Options.Create(new OpenAIOptions { ApiKey = "test-key", EmbeddingModel = "text-embedding-3-small", EmbeddingDimensions = 3 });
        var generator = new OpenAIEmbeddingGenerator(new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") }, options);

        var vectors = await generator.GenerateAsync(["first", "second"], CancellationToken.None);

        vectors.Should().HaveCount(2); vectors[0].Should().Equal(1f, 0f, 0f);
        handler.Body.Should().Contain("\"dimensions\":3").And.Contain("\"text-embedding-3-small\"");
        handler.Authorization.Should().Be("Bearer test-key");
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string Body { get; private set; } = string.Empty;
        public string Authorization { get; private set; } = string.Empty;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken); Authorization = request.Headers.Authorization!.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"data":[{"index":0,"embedding":[1,0,0]},{"index":1,"embedding":[0,1,0]}]}""", Encoding.UTF8, "application/json") };
        }
    }
}

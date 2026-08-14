using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RepoNavAI.Infrastructure.Repositories;
using Xunit;

namespace RepoNavAI.Api.IntegrationTests;

public sealed class GitHubSnapshotProviderTests
{
    [Fact]
    public async Task FetchAsync_StreamsValidGzipTarAndRemovesArchiveRoot()
    {
        var provider = CreateProvider(Archive(("root/src/App.cs", "class App {}"), ("root/image.png", "ignored")));

        var snapshot = await provider.FetchAsync("owner", "repo", "main", CancellationToken.None);

        snapshot.CommitSha.Should().Be("abcdef12");
        snapshot.Files.Should().ContainSingle();
        snapshot.Files.Single().Path.Should().Be("src/App.cs");
    }

    [Fact]
    public async Task FetchAsync_RejectsUnsupportedCompressionBeforeTraversal()
    {
        var provider = CreateProvider(Encoding.UTF8.GetBytes("not a gzip archive"));

        var act = () => provider.FetchAsync("owner", "repo", "main", CancellationToken.None);

        var failure = await act.Should().ThrowAsync<RepositoryAcquisitionException>();
        failure.Which.Code.Should().Be("ARCHIVE_FORMAT_UNSUPPORTED");
        failure.Which.Retryable.Should().BeFalse();
    }

    [Fact]
    public async Task FetchAsync_RejectsTruncatedArchive()
    {
        var archive = Archive(("root/src/App.cs", "class App {}"));
        var provider = CreateProvider(archive[..(archive.Length / 2)]);

        var act = () => provider.FetchAsync("owner", "repo", "main", CancellationToken.None);

        new[] { "ARCHIVE_MALFORMED", "ARCHIVE_TRUNCATED" }.Should().Contain((await act.Should().ThrowAsync<RepositoryAcquisitionException>()).Which.Code);
    }

    [Fact]
    public async Task FetchAsync_RejectsPathTraversalAndLinks()
    {
        var traversal = CreateProvider(Archive(("root/../secret.cs", "secret")));
        var traversalAct = () => traversal.FetchAsync("owner", "repo", "main", CancellationToken.None);
        (await traversalAct.Should().ThrowAsync<RepositoryAcquisitionException>()).Which.Code.Should().Be("ARCHIVE_PATH_UNSAFE");

        var link = CreateProvider(ArchiveWithLink());
        var linkAct = () => link.FetchAsync("owner", "repo", "main", CancellationToken.None);
        (await linkAct.Should().ThrowAsync<RepositoryAcquisitionException>()).Which.Code.Should().Be("ARCHIVE_LINK_UNSUPPORTED");
    }

    [Fact]
    public async Task FetchAsync_EnforcesExpandedEntryAndSupportedFileLimits()
    {
        var archive = Archive(("root/src/App.cs", new string('a', 256)));
        var expanded = CreateProvider(archive, new IndexingOptions { MaximumExpandedBytes = 32 });
        Func<Task> expandedAct = () => expanded.FetchAsync("owner", "repo", "main", CancellationToken.None);
        (await expandedAct.Should().ThrowAsync<RepositoryAcquisitionException>()).Which.Code.Should().Be("ARCHIVE_EXPANDED_LIMIT");

        var entries = CreateProvider(archive, new IndexingOptions { MaximumArchiveEntries = 0 });
        Func<Task> entriesAct = () => entries.FetchAsync("owner", "repo", "main", CancellationToken.None);
        (await entriesAct.Should().ThrowAsync<RepositoryAcquisitionException>()).Which.Code.Should().Be("ARCHIVE_ENTRY_LIMIT");

        var file = CreateProvider(archive, new IndexingOptions { MaximumFileBytes = 64 });
        Func<Task> fileAct = () => file.FetchAsync("owner", "repo", "main", CancellationToken.None);
        (await fileAct.Should().ThrowAsync<RepositoryAcquisitionException>()).Which.Code.Should().Be("ARCHIVE_FILE_LIMIT");
    }

    [Fact]
    public async Task FetchAsync_ClassifiesProviderAvailabilityAsRetryable()
    {
        var provider = CreateProvider([], archiveStatus: HttpStatusCode.ServiceUnavailable);

        var act = () => provider.FetchAsync("owner", "repo", "main", CancellationToken.None);

        var failure = await act.Should().ThrowAsync<RepositoryAcquisitionException>();
        failure.Which.Code.Should().Be("ARCHIVE_PROVIDER_TRANSIENT");
        failure.Which.Retryable.Should().BeTrue();
    }

    [Fact]
    public async Task FetchAsync_HonorsCallerCancellation()
    {
        var provider = CreateProvider(Archive(("root/src/App.cs", "class App {}")));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = () => provider.FetchAsync("owner", "repo", "main", cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static GitHubSnapshotProvider CreateProvider(byte[] archive, IndexingOptions? options = null, HttpStatusCode archiveStatus = HttpStatusCode.OK)
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"sha\":\"abcdef12\"}", Encoding.UTF8, "application/json") },
            ArchiveResponse(archive, archiveStatus));
        return new GitHubSnapshotProvider(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") },
            Options.Create(new GitHubOptions()), Options.Create(options ?? new IndexingOptions()), NullLogger<GitHubSnapshotProvider>.Instance);
    }

    private static HttpResponseMessage ArchiveResponse(byte[] archive, HttpStatusCode status)
    {
        var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(archive) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-gzip");
        return response;
    }

    private static byte[] Archive(params (string Name, string Content)[] files)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        using (var tar = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, file.Name) { DataStream = new MemoryStream(Encoding.UTF8.GetBytes(file.Content)) };
                tar.WriteEntry(entry);
            }
        }
        return output.ToArray();
    }

    private static byte[] ArchiveWithLink()
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        using (var tar = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: true))
            tar.WriteEntry(new PaxTarEntry(TarEntryType.SymbolicLink, "root/link.cs") { LinkName = "root/src/App.cs" });
        return output.ToArray();
    }

    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private int index;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(responses[index++]);
        }
    }
}

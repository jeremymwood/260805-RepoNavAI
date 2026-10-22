using System.Text;
using RepoNavAI.Application.Common.Diagnostics;
using RepoNavAI.Application.Repositories;

namespace RepoNavAI.Infrastructure.Repositories;

/// <summary>Describes the existing character-budget selection without changing prompts or citation behavior.</summary>
internal static class EvidencePromptBuilder
{
    public static string Finish(StringBuilder builder, IReadOnlyCollection<SemanticSearchResult> sources,
        int maximumCharacters, RepositoryPrompt template, string? suffix = null)
    {
        var evidence = new List<EvidenceSelection>();
        var exhausted = false;
        var index = 0;
        foreach (var source in sources)
        {
            index++;
            var heading = $"\n[{index}] {source.Path}:{source.StartLine}-{source.EndLine} at commit {source.CommitSha}\n<repository_evidence>\n";
            const string closing = "\n</repository_evidence>\n";
            var remaining = maximumCharacters - builder.Length - heading.Length - closing.Length;
            if (remaining <= 0) exhausted = true;
            var included = exhausted ? 0 : Math.Min(source.Content.Length, remaining);
            int? offset = null;
            if (!exhausted)
            {
                builder.Append(heading);
                offset = builder.Length;
                builder.Append(source.Content.AsSpan(0, included)).Append(closing);
            }
            if (evidence.Count >= RepositoryRun.MaximumEvidenceEntries) continue;

            // Chunker prefixes are metadata, not physical source lines. Character offsets remain canonical.
            var prefix = $"File: {source.Path}\n";
            var prefixLength = source.Content.StartsWith(prefix, StringComparison.Ordinal) ? prefix.Length : 0;
            var sourceCharacters = Math.Max(0, included - prefixLength);
            int? startLine = sourceCharacters > 0 ? source.StartLine : null;
            var text = source.Content.AsSpan(prefixLength, sourceCharacters);
            int? endLine = startLine is null ? null : startLine + text.Count('\n') - (text.EndsWith("\n", StringComparison.Ordinal) ? 1 : 0);
            var midLine = included > prefixLength && included < source.Content.Length && source.Content[included - 1] != '\n' && source.Content[included] != '\n';
            evidence.Add(new(RepositoryRun.Candidate(source, index), index, source.Content.Length, 0, included, offset,
                included < source.Content.Length, exhausted, startLine, endLine, midLine,
                exhausted || included < source.Content.Length || endLine < source.EndLine));
        }
        if (suffix is not null) builder.AppendLine(suffix);
        RepositoryDiagnostics.Current?.RecordContext(new(template, maximumCharacters, builder.Length, sources.Count,
            evidence.ToArray(), sources.Count > RepositoryRun.MaximumEvidenceEntries));
        return builder.ToString();
    }
}

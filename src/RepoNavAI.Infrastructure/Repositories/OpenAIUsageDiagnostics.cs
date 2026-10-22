using OpenAI.Chat;
using RepoNavAI.Application.Common.Diagnostics;

namespace RepoNavAI.Infrastructure.Repositories;

internal static class OpenAIUsageDiagnostics
{
    public static void Record(ProviderCallScope? call, IReadOnlyDictionary<string, object?>? metadata, object? innerContent)
    {
        // Pinned Semantic Kernel exposes the typed OpenAI usage in Metadata["Usage"]. Streaming
        // connector versions can also expose it only on the typed inner update. Never serialize either bag.
        var usage = metadata?.GetValueOrDefault("Usage") as ChatTokenUsage ?? innerContent switch
        {
            ChatCompletion completion => completion.Usage,
            StreamingChatCompletionUpdate update => update.Usage,
            _ => null
        };
        call?.RecordReturnedModel(innerContent switch
        {
            ChatCompletion completion => completion.Model,
            StreamingChatCompletionUpdate update => update.Model,
            _ => null
        });
        if (usage is not null) call?.RecordUsage(new(usage.InputTokenCount, usage.OutputTokenCount, usage.TotalTokenCount));
    }
}

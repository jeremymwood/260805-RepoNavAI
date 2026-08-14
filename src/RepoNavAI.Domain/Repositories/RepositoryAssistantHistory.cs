using RepoNavAI.Domain.Common;

namespace RepoNavAI.Domain.Repositories;

public enum RepositoryAssistantHistoryMode { Search, Answer, Orientation, CodeFlow }
public enum RepositoryAssistantHistoryStatus { Processing, Completed, Cancelled, Failed }

public sealed class RepositoryAssistantHistory : Entity
{
    private RepositoryAssistantHistory() { }

    public RepositoryAssistantHistory(Guid organizationId, Guid repositoryId, Guid userId,
        RepositoryAssistantHistoryMode mode, string prompt, string commitSha, DateTimeOffset createdAtUtc) : base(Guid.NewGuid())
    {
        if (string.IsNullOrWhiteSpace(prompt)) throw new ArgumentException("A prompt is required.", nameof(prompt));
        OrganizationId = organizationId; RepositoryId = repositoryId; UserId = userId; Mode = mode;
        Prompt = prompt; DisplayTitle = DefaultTitle(prompt); CommitSha = commitSha;
        CreatedAtUtc = UpdatedAtUtc = createdAtUtc;
    }

    public Guid OrganizationId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid UserId { get; private set; }
    public RepositoryAssistantHistoryMode Mode { get; private set; }
    public RepositoryAssistantHistoryStatus Status { get; private set; } = RepositoryAssistantHistoryStatus.Processing;
    public string Prompt { get; private set; } = string.Empty;
    public string DisplayTitle { get; private set; } = string.Empty;
    public string CommitSha { get; private set; } = string.Empty;
    public string? SchemaVersion { get; private set; }
    public string? ResultJson { get; private set; }
    public Guid? OrientationPlanId { get; private set; }
    public bool IsStarred { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public void Complete(string schemaVersion, string? resultJson, Guid? orientationPlanId, DateTimeOffset completedAtUtc)
    {
        if (Status != RepositoryAssistantHistoryStatus.Processing) throw new InvalidOperationException("Only processing history can complete.");
        if (string.IsNullOrWhiteSpace(schemaVersion)) throw new ArgumentException("A result schema version is required.", nameof(schemaVersion));
        SchemaVersion = schemaVersion; ResultJson = resultJson; OrientationPlanId = orientationPlanId;
        Status = RepositoryAssistantHistoryStatus.Completed; CompletedAtUtc = UpdatedAtUtc = completedAtUtc;
    }

    public void FinishIncomplete(RepositoryAssistantHistoryStatus status, DateTimeOffset completedAtUtc)
    {
        if (status is not (RepositoryAssistantHistoryStatus.Cancelled or RepositoryAssistantHistoryStatus.Failed)) throw new ArgumentOutOfRangeException(nameof(status));
        if (Status != RepositoryAssistantHistoryStatus.Processing) return;
        Status = status; CompletedAtUtc = UpdatedAtUtc = completedAtUtc;
    }

    public void SetStarred(bool isStarred, DateTimeOffset updatedAtUtc) { IsStarred = isStarred; UpdatedAtUtc = updatedAtUtc; }
    public void Rename(string title, DateTimeOffset updatedAtUtc)
    {
        var value = title.Trim();
        if (value.Length is < 1 or > 120) throw new ArgumentOutOfRangeException(nameof(title));
        DisplayTitle = value; UpdatedAtUtc = updatedAtUtc;
    }

    private static string DefaultTitle(string prompt)
    {
        var value = prompt.Trim();
        return value.Length <= 120 ? value : value[..117] + "...";
    }
}

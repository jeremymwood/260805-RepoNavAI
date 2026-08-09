using RepoNavAI.Domain.Common;

namespace RepoNavAI.Domain.Repositories;

public enum OrientationRole { Developer, Tester, Architect, DevOps, Product }
public enum OrientationExperience { NewToSoftware, Junior, MidLevel, Senior }
public enum OrientationFocus { GeneralOnboarding, ImplementFeature, FixBug, Architecture, Operations }
public enum OrientationEvidenceLevel { Confirmed, Inferred, Missing }

public sealed class RepositoryOrientationPlan : Entity
{
    private RepositoryOrientationPlan() { }

    public RepositoryOrientationPlan(Guid organizationId, Guid repositoryId, Guid userId, Guid snapshotId,
        string commitSha, OrientationRole role, OrientationExperience experience, OrientationFocus focus,
        int timeBudgetMinutes, string model, string planJson, DateTimeOffset createdAtUtc) : base(Guid.NewGuid())
    {
        OrganizationId = organizationId;
        RepositoryId = repositoryId;
        UserId = userId;
        SnapshotId = snapshotId;
        CommitSha = commitSha;
        Role = role;
        Experience = experience;
        Focus = focus;
        TimeBudgetMinutes = timeBudgetMinutes;
        Model = model;
        PlanJson = planJson;
        CompletedStepKeysJson = "[]";
        CreatedAtUtc = UpdatedAtUtc = createdAtUtc;
    }

    public Guid OrganizationId { get; private set; }
    public Guid RepositoryId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid SnapshotId { get; private set; }
    public string CommitSha { get; private set; } = string.Empty;
    public OrientationRole Role { get; private set; }
    public OrientationExperience Experience { get; private set; }
    public OrientationFocus Focus { get; private set; }
    public int TimeBudgetMinutes { get; private set; }
    public string Model { get; private set; } = string.Empty;
    public string PlanJson { get; private set; } = string.Empty;
    public string CompletedStepKeysJson { get; private set; } = "[]";

    public void SetProgress(string completedStepKeysJson, DateTimeOffset updatedAtUtc)
    {
        CompletedStepKeysJson = completedStepKeysJson;
        UpdatedAtUtc = updatedAtUtc;
    }
}

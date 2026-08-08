using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RepoNavAI.Domain.Organizations;
using RepoNavAI.Infrastructure.Identity;
using RepoNavAI.Domain.Repositories;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace RepoNavAI.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<OrganizationInvitation> OrganizationInvitations => Set<OrganizationInvitation>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<RegisteredRepository> RegisteredRepositories => Set<RegisteredRepository>();
    public DbSet<RepositoryIndexingRequest> RepositoryIndexingRequests => Set<RepositoryIndexingRequest>();
    public DbSet<RepositorySnapshot> RepositorySnapshots => Set<RepositorySnapshot>();
    public DbSet<RepositoryDocument> RepositoryDocuments => Set<RepositoryDocument>();
    public DbSet<RepositorySymbol> RepositorySymbols => Set<RepositorySymbol>();
    public DbSet<RepositoryEndpoint> RepositoryEndpoints => Set<RepositoryEndpoint>();
    public DbSet<RepositoryChunk> RepositoryChunks => Set<RepositoryChunk>();
    public DbSet<RepositoryChatSession> RepositoryChatSessions => Set<RepositoryChatSession>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("reponav");
        builder.HasPostgresExtension("vector");

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        });
        builder.Entity<IdentityRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoleAssignments");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("UserRoleClaims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        builder.Entity<Organization>(entity =>
        {
            entity.ToTable("Organizations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Slug).IsUnique();
        });
        builder.Entity<OrganizationMember>(entity =>
        {
            entity.ToTable("OrganizationMembers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
            entity.HasOne(x => x.Organization).WithMany(x => x.Members).HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<OrganizationInvitation>(entity =>
        {
            entity.ToTable("OrganizationInvitations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.OrganizationId, x.Email });
            entity.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.InvitedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Project>(entity =>
        {
            entity.ToTable("Projects");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
            entity.HasOne(x => x.Organization).WithMany(x => x.Projects).HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<RegisteredRepository>(entity =>
        {
            entity.ToTable("RegisteredRepositories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Provider).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ProviderRepositoryId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Owner).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Ignore(x => x.FullName);
            entity.Property(x => x.DefaultBranch).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.WebUrl).HasMaxLength(2048).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.Provider, x.Owner, x.Name }).IsUnique();
            entity.HasOne(x => x.Organization).WithMany(x => x.Repositories).HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RegisteredByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<RepositoryIndexingRequest>(entity =>
        {
            entity.ToTable("RepositoryIndexingRequests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Checkpoint).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.CommitSha).HasMaxLength(64);
            entity.Property(x => x.ErrorCode).HasMaxLength(64);
            entity.Property(x => x.ErrorMessage).HasMaxLength(500);
            entity.Property(x => x.LeaseOwnerId).IsConcurrencyToken();
            entity.HasIndex(x => new { x.OrganizationId, x.Status });
            entity.HasOne(x => x.Repository).WithMany(x => x.IndexingRequests).HasForeignKey(x => x.RepositoryId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<RepositorySnapshot>(entity =>
        {
            entity.ToTable("RepositorySnapshots"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.CommitSha).HasMaxLength(64).IsRequired(); entity.HasIndex(x => new { x.RepositoryId, x.CommitSha }).IsUnique();
            entity.HasOne(x => x.Repository).WithMany(x => x.Snapshots).HasForeignKey(x => x.RepositoryId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<RepositoryDocument>(entity =>
        {
            entity.ToTable("RepositoryDocuments"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Path).HasMaxLength(1024).IsRequired(); entity.Property(x => x.Language).HasMaxLength(32).IsRequired(); entity.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => new { x.SnapshotId, x.Path }).IsUnique(); entity.HasOne(x => x.Snapshot).WithMany(x => x.Documents).HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<RepositorySymbol>(entity =>
        {
            entity.ToTable("RepositorySymbols"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Name).HasMaxLength(255).IsRequired(); entity.Property(x => x.QualifiedName).HasMaxLength(1024).IsRequired(); entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.DocumentId, x.QualifiedName, x.Kind }); entity.HasOne(x => x.Document).WithMany(x => x.Symbols).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<RepositoryEndpoint>(entity =>
        {
            entity.ToTable("RepositoryEndpoints"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.HttpMethod).HasMaxLength(16).IsRequired(); entity.Property(x => x.Route).HasMaxLength(2048).IsRequired(); entity.Property(x => x.Handler).HasMaxLength(1024).IsRequired(); entity.Property(x => x.Path).HasMaxLength(1024).IsRequired(); entity.Property(x => x.DownstreamSymbols).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.SnapshotId, x.HttpMethod, x.Route, x.Handler }).IsUnique(); entity.HasOne(x => x.Snapshot).WithMany(x => x.Endpoints).HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<RepositoryChunk>(entity =>
        {
            entity.ToTable("RepositoryChunks"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.ContentHash).HasMaxLength(64).IsRequired(); entity.Property(x => x.EmbeddingModel).HasMaxLength(100).IsRequired(); entity.Property(x => x.Content).IsRequired();
            entity.Property<Vector>("Embedding").HasColumnType("vector(512)");
            entity.HasIndex(x => new { x.SnapshotId, x.DocumentId, x.Ordinal }).IsUnique();
            entity.HasIndex("Embedding").HasMethod("hnsw").HasOperators("vector_cosine_ops").HasStorageParameter("m", 16).HasStorageParameter("ef_construction", 64);
            entity.HasOne(x => x.Snapshot).WithMany(x => x.Chunks).HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Document).WithMany(x => x.Chunks).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<RepositoryChatSession>(entity =>
        {
            entity.ToTable("RepositoryChatSessions"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Model).HasMaxLength(100).IsRequired(); entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.OrganizationId, x.CreatedAtUtc });
            entity.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<RegisteredRepository>().WithMany().HasForeignKey(x => x.RepositoryId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

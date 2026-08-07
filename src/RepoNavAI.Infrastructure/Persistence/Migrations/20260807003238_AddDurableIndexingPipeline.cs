using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RepoNavAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableIndexingPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                schema: "reponav",
                table: "RepositoryIndexingRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancellationRequestedAtUtc",
                schema: "reponav",
                table: "RepositoryIndexingRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Checkpoint",
                schema: "reponav",
                table: "RepositoryIndexingRequests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Queued");

            migrationBuilder.AddColumn<string>(
                name: "CommitSha",
                schema: "reponav",
                table: "RepositoryIndexingRequests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAtUtc",
                schema: "reponav",
                table: "RepositoryIndexingRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorCode",
                schema: "reponav",
                table: "RepositoryIndexingRequests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                schema: "reponav",
                table: "RepositoryIndexingRequests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LeaseExpiresAtUtc",
                schema: "reponav",
                table: "RepositoryIndexingRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAtUtc",
                schema: "reponav",
                table: "RepositoryIndexingRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RepositorySnapshots",
                schema: "reponav",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommitSha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositorySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositorySnapshots_RegisteredRepositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalSchema: "reponav",
                        principalTable: "RegisteredRepositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RepositoryDocuments",
                schema: "reponav",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ByteCount = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryDocuments_RepositorySnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalSchema: "reponav",
                        principalTable: "RepositorySnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RepositorySymbols",
                schema: "reponav",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    QualifiedName = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Line = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositorySymbols", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositorySymbols_RepositoryDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "reponav",
                        principalTable: "RepositoryDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryDocuments_SnapshotId_Path",
                schema: "reponav",
                table: "RepositoryDocuments",
                columns: new[] { "SnapshotId", "Path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepositorySnapshots_RepositoryId_CommitSha",
                schema: "reponav",
                table: "RepositorySnapshots",
                columns: new[] { "RepositoryId", "CommitSha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RepositorySymbols_DocumentId_QualifiedName_Kind",
                schema: "reponav",
                table: "RepositorySymbols",
                columns: new[] { "DocumentId", "QualifiedName", "Kind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositorySymbols",
                schema: "reponav");

            migrationBuilder.DropTable(
                name: "RepositoryDocuments",
                schema: "reponav");

            migrationBuilder.DropTable(
                name: "RepositorySnapshots",
                schema: "reponav");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                schema: "reponav",
                table: "RepositoryIndexingRequests");

            migrationBuilder.DropColumn(
                name: "CancellationRequestedAtUtc",
                schema: "reponav",
                table: "RepositoryIndexingRequests");

            migrationBuilder.DropColumn(
                name: "Checkpoint",
                schema: "reponav",
                table: "RepositoryIndexingRequests");

            migrationBuilder.DropColumn(
                name: "CommitSha",
                schema: "reponav",
                table: "RepositoryIndexingRequests");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                schema: "reponav",
                table: "RepositoryIndexingRequests");

            migrationBuilder.DropColumn(
                name: "ErrorCode",
                schema: "reponav",
                table: "RepositoryIndexingRequests");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                schema: "reponav",
                table: "RepositoryIndexingRequests");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAtUtc",
                schema: "reponav",
                table: "RepositoryIndexingRequests");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                schema: "reponav",
                table: "RepositoryIndexingRequests");
        }
    }
}

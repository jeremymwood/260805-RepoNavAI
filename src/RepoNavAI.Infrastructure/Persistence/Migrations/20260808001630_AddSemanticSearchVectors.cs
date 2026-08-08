using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace RepoNavAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSemanticSearchVectors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "RepositoryChunks",
                schema: "reponav",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    StartLine = table.Column<int>(type: "integer", nullable: false),
                    EndLine = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EmbeddingModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(512)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryChunks_RepositoryDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "reponav",
                        principalTable: "RepositoryDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RepositoryChunks_RepositorySnapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalSchema: "reponav",
                        principalTable: "RepositorySnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryChunks_DocumentId",
                schema: "reponav",
                table: "RepositoryChunks",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryChunks_Embedding",
                schema: "reponav",
                table: "RepositoryChunks",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" })
                .Annotation("Npgsql:StorageParameter:ef_construction", 64)
                .Annotation("Npgsql:StorageParameter:m", 16);

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryChunks_SnapshotId_DocumentId_Ordinal",
                schema: "reponav",
                table: "RepositoryChunks",
                columns: new[] { "SnapshotId", "DocumentId", "Ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositoryChunks",
                schema: "reponav");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
